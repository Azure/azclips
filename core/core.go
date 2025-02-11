package core

import (
	"strings"

	"github.com/Azure/azure-cli-lite/shared"
	hclog "github.com/hashicorp/go-hclog"
)

type coreImpl struct {
	logger hclog.Logger
	// the modules loaded in the core
	manager *ModuleManager
}

func NewCore(logger hclog.Logger) *coreImpl {
	manager := NewModuleManager(logger)
	return &coreImpl{
		logger:  logger,
		manager: manager,
	}
}

func (c *coreImpl) Init() {
	c.manager.Init()
	c.loadBuildInModules()
}

func (c *coreImpl) Dispose() {
	c.manager.Dispose()
}

func (c *coreImpl) RunCommand(args []string) (string, error) {
	// c.logger.Info("Message from core plugin", "args", args)

	// restModule, err := c.manager.GetModule(rest.MODULE_NAME)
	// if err != nil {
	// 	c.logger.Error("Error getting rest module", "error", err)
	// 	return ""
	// }
	// c.logger.Info("Rest module loaded")

	// metadata, err := restModule.Metadata(c)
	// if err != nil {
	// 	c.logger.Error("Error getting metadata", "error", err)
	// 	return ""
	// }
	// c.logger.Info("Metadata", "metadata", metadata)

	// return strings.Join([]string{metadata.Name, metadata.Version}, " ")

	_, err := c.parseArgs(args)
	if err != nil {
		return "", err
	}
	return "", nil
}

func (c *coreImpl) parseArgs(args []string) (interface{}, error) {
	// find the first args that starts with '-'
	i := 0
	for ; i < len(args); i++ {
		if strings.HasPrefix(args[i], "-") {
			break
		}
	}
	cmds := args[:i]
	// flags := args[i:]

	if len(cmds) == 0 {
		// list all the modules available
		// for the installed modules: show the versions and short description
		// for the uninstalled modules: show the short description
	} else {
		uri := &shared.CMDUri{
			ModuleName: cmds[0],
			Cmds:       cmds[1:],
			Executable: true,
		}
		module, err := c.manager.GetModule(uri.ModuleName)
		if err != nil {
			return nil, err
		}
		uri, err = module.Check(c, *uri)
		if err != nil {
			return nil, err
		}
		// module.Check(c, uri)
	}
	return nil, nil
}
