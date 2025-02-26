package core

import (
	"fmt"
	"slices"
	"sort"
	"strings"
	"sync"

	"github.com/Azure/azure-cli-lite/shared"
	hclog "github.com/hashicorp/go-hclog"
)

type Core interface {
	RunCommand(args []string) (View, error)
}

type coreImpl struct {
	logger hclog.Logger
	// the modules loaded in the core
	manager     *ModuleManager
	clientName  string
	description string
}

func NewCore(logger hclog.Logger, clientName string, description string) *coreImpl {
	manager := NewModuleManager(logger)
	return &coreImpl{
		logger:      logger,
		manager:     manager,
		clientName:  clientName,
		description: description,
	}
}

func (c *coreImpl) Init() {
	c.manager.init()
	c.loadBuiltInModules()
}

func (c *coreImpl) Dispose() {
	c.manager.dispose()
}

func (c *coreImpl) RunCommand(args []string) (View, error) {
	i := 0
	for ; i < len(args); i++ {
		if strings.HasPrefix(args[i], "-") {
			break
		}
	}
	cmds := args[:i]
	flags := args[i:]

	helpInfo, err := c.NeedHelp(cmds, flags)
	if err != nil {
		return nil, err
	}

	if helpInfo != nil {
		return &HelpView{core: c, helpInfo: helpInfo}, nil
	}

	executor, err := c.NeedExecute(cmds, flags)
	if err != nil {
		return nil, err
	}

	if executor != nil {
		return &RunView{executor: executor}, nil
	}

	return nil, fmt.Errorf("no command found")
}

// Check user need help or not, if need, return the help info, otherwise return nil
func (c *coreImpl) NeedHelp(cmds []string, flags []string) (HelpInfo, error) {
	hasHelpFlag := false
	for _, flag := range flags {
		if flag == "--help" || flag == "-h" {
			hasHelpFlag = true
			break
		}
	}

	if len(cmds) == 0 {
		// return the help of all the commands
		return c.retrieveHelpInfoForGroup(nil)
	}

	uris, err := c.checkCmds(cmds)
	if err != nil {
		return nil, err
	}

	if len(uris) == 0 {
		// no command found
		return nil, fmt.Errorf("no module found")
	}

	if !hasHelpFlag && uris[0].Executable {
		// not need help for execution
		return nil, nil
	}

	if uris[0].Executable {
		// if len(uris) > 1 {
		// 	// TODO: has duplicate commands, may need to suggest possible command choices in the help info
		// }
		return c.retrieveHelpInfoForCommand(uris[0])
	} else {
		return c.retrieveHelpInfoForGroup(uris)
	}
}

// Check user need execute or not, if need, return the execute info, otherwise return nil
func (c *coreImpl) NeedExecute(cmds []string, flags []string) (CmdExecutor, error) {
	if len(cmds) == 0 {
		return nil, fmt.Errorf("no command found")
	}

	uris, err := c.checkCmds(cmds)
	if err != nil {
		return nil, err
	}

	if len(uris) == 0 {
		return nil, fmt.Errorf("no module found")
	}

	if !uris[0].Executable {
		return nil, fmt.Errorf("no executable command found")
	}

	uri := uris[0]
	params, err := c.parseParams(uri, flags)
	if err != nil {
		return nil, err
	}

	return &cmdExecutorImpl{core: c, cmd: *uri, params: params}, nil
}

func (c *coreImpl) checkCmds(cmds []string) ([]*shared.CMDUri, error) {
	// get module name or version from the last cmd word, for example:
	// `network vnet create:rest:v2024-01-01` then get the module name `rest` and version `v2024-01-01` from the last cmd word.
	// get version from the last cmd word when no module name is specified, for example:
	// `network vnet create:v2024-01-01` then get the version `v2024-01-01` from the last cmd word.
	// get module name from the last cmd word when no version is specified, for example:
	// `network vnet create:rest` then get the module name `rest` from the last cmd word.

	// Extract module name and version from the last command
	lastCmd := cmds[len(cmds)-1]
	moduleName := ""
	version := ""

	parts := strings.Split(lastCmd, ":")
	if len(parts) > 1 {
		// Remove the module/version parts from the command
		cmds[len(cmds)-1] = parts[0]

		if len(parts) == 3 {
			// Case: command:module:version
			if len(parts[1]) >= 2 {
				moduleName = parts[1]
			} else {
				return nil, fmt.Errorf("invalid module name: '%s'", parts[1])
			}
			if strings.HasPrefix(parts[2], "v") && len(parts[2]) > 1 && parts[2][1] >= '0' && parts[2][1] <= '9' {
				version = parts[2][1:]
			} else {
				return nil, fmt.Errorf("invalid version: '%s'", parts[2])
			}
		} else if len(parts) == 2 {
			// Case: command:version or command:module
			if strings.HasPrefix(parts[1], "v") && len(parts[1]) > 1 && parts[1][1] >= '0' && parts[1][1] <= '9' {
				// It's a version
				version = parts[1][1:]
			} else if len(parts[1]) >= 2 {
				// It's a module name
				moduleName = parts[1]
			} else {
				return nil, fmt.Errorf("invalid expression: '%s'", parts[1])
			}
		} else {
			return nil, fmt.Errorf("invalid expression: '%s'", lastCmd)
		}
	}

	moduleNames := c.manager.getModuleNames()

	if moduleName != "" {
		// make sure pluginName is in the pluginNames
		if !slices.Contains(moduleNames, moduleName) {
			err := c.manager.dynamicDownloadPlugin(moduleName)
			if err != nil {
				return nil, fmt.Errorf("module '%s' not found", moduleName)
			}
		}
		moduleNames = []string{moduleName}
	}

	checkResults := make(chan *shared.CMDUri, len(moduleNames))
	var wg sync.WaitGroup

	for _, moduleName := range moduleNames {
		wg.Add(1)
		go func(moduleName string) {
			defer wg.Done()
			module, err := c.manager.getModule(moduleName)
			if err != nil {
				c.logger.Error("Error loading module", "error", err)
				checkResults <- nil
				return
			}
			cmd_uri, err := module.Check(c, shared.CMDUri{
				ModuleName: moduleName,
				Cmds:       cmds,
				Version:    version,
			})
			if err != nil {
				c.logger.Error("Error checking module", "error", err)
				checkResults <- nil
				return
			}
			// The command priority should be always returned from the module
			if cmd_uri.Priority == shared.PRIORITY_UNDEFINED {
				c.logger.Error("No priority defined for the command", "command", cmd_uri.String())
				checkResults <- nil
				return
			}
			if cmd_uri.Priority.IsBuiltIn() && c.manager.builtIns[moduleName] == nil {
				// not allowed the plugin-in module to return the built-in priority
				c.logger.Error("Invalid priority of command uri for the plugin-in module", "command", cmd_uri.String())
				checkResults <- nil
				return
			}
			checkResults <- cmd_uri
		}(moduleName)
	}

	// Close channel after all goroutines complete
	go func() {
		wg.Wait()
		close(checkResults)
	}()

	// Read results until channel is closed
	var uriCandidates []*shared.CMDUri = make([]*shared.CMDUri, 0)
	for result := range checkResults {
		if result != nil {
			uriCandidates = append(uriCandidates, result)
		}
	}

	if len(uriCandidates) > 1 {
		// sort the uriCandidates by the priority
		sort.Slice(uriCandidates, func(i, j int) bool {
			return uriCandidates[i].Priority < uriCandidates[j].Priority ||
				// if the priority is the same, sort by the module name
				(uriCandidates[i].Priority == uriCandidates[j].Priority && uriCandidates[i].ModuleName < uriCandidates[j].ModuleName)
		})
	}

	return uriCandidates, nil
}

func (c *coreImpl) parseParams(uri *shared.CMDUri, flags []string) (interface{}, error) {
	module, err := c.manager.getModule(uri.ModuleName)
	if err != nil {
		return nil, err
	}
	cmd, err := module.Interface(c, *uri)
	if err != nil {
		return nil, err
	}

	if cmd.GetCommand() == nil {
		return nil, fmt.Errorf("no command found")
	}

	argActions, errs := parseArgs(cmd, flags)
	if len(errs) > 0 {
		// TODO: format the merged error messages
		return nil, fmt.Errorf("error parsing arguments: %v", errs)
	}

	// TODO: build the command params from the argActions, here we can apply default values and format the values, combine with the --input-file

	// TODO: verify the params based on the command interface

	// flagArgs := make([]string, 0)

	return nil, nil
}
