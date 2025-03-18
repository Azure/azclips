package core

import (
	"fmt"
	"sort"
	"strings"

	"github.com/Azure/azure-cli-lite/shared"
)

type HelpLevel string

const (
	HelpLevelGroup   HelpLevel = "CommandGroup"
	HelpLevelCommand HelpLevel = "Command"
	HelpLevelArg     HelpLevel = "Argument"
	HelpLevelArgProp HelpLevel = "ArgumentProperty"
)

type HelpInfo interface {
	Name() string
	Level() HelpLevel
	Flags() []string
	DetailedDescription() string
	Segments() []string
	Subs(segment string) []string
	SubFlags(segment string, sub string) []string
	SubShortDescription(segment string, sub string) string
	SubDetailedDescription(segment string, sub string) string
}

func (c *coreImpl) retrieveHelpInfoForGroup(uris []*shared.CMDUri) (HelpInfo, error) {
	// if uri is nil, return the help info of all the commands
	helpInfo := &GroupHelpInfoImpl{}
	if len(uris) == 0 {
		helpInfo.names = []string{c.clientName}
		helpInfo.help = helpDescription{
			long: c.description,
		}
		for _, moduleName := range c.manager.getModuleNames() {
			uri := shared.CMDUri{
				ModuleName: moduleName,
				Cmds:       []string{},
			}
			uris = append(uris, &uri)
		}
	}

	for _, uri := range uris {
		module, err := c.manager.getModule(uri.ModuleName)
		if err != nil {
			return nil, err
		}
		cmdInterface, err := module.Interface(c, *uri)
		if err != nil {
			return nil, err
		}
		if cmdInterface.GetGroup() == nil {
			c.logger.Error(fmt.Sprintf("Expected a group, but got a command for %s", uri.String()))
			continue
		}
		helpInfo.names = append([]string{c.clientName}, uri.Cmds...)
		helpInfo.help.long = cmdInterface.Help.Long
		helpInfo.help.short = cmdInterface.Help.Short

		for _, cmd := range cmdInterface.GetGroup().Commands {
			helpInfo.subCommands[cmd.Name] = commandDescription{
				version: cmd.GetCommand().Version,
				help:    helpDescription{long: cmd.Help.Long, short: cmd.Help.Short},
			}
		}
		for _, group := range cmdInterface.GetGroup().Groups {
			helpInfo.subGroups[group.Name] = helpDescription{
				long:  group.Help.Long,
				short: group.Help.Short,
			}
		}
	}

	return helpInfo, nil
}

func (c *coreImpl) retrieveHelpInfoForCommand(uri *shared.CMDUri) (HelpInfo, error) {
	module, err := c.manager.getModule(uri.ModuleName)
	if err != nil {
		return nil, err
	}
	cmdInterface, err := module.Interface(c, *uri)
	if err != nil {
		return nil, err
	}
	if cmdInterface.GetCommand() == nil {
		c.logger.Error(fmt.Sprintf("Expected a command, but got a group for %s", uri.String()))
		return nil, nil
	}

	helpInfo := &CommandHelpInfoImpl{
		names:     append([]string{c.clientName}, uri.Cmds...),
		version:   cmdInterface.GetCommand().Version,
		help:      helpDescription{long: cmdInterface.Help.Long, short: cmdInterface.Help.Short},
		argGroups: make(map[string]argGroupDescription),
	}

	for _, arg := range cmdInterface.GetCommand().Args {
		argGroup := arg.Group
		if argGroup == "" {
			argGroup = "Default"
		}
		if _, ok := helpInfo.argGroups[argGroup]; !ok {
			helpInfo.argGroups[argGroup] = argGroupDescription{args: make(map[string]argDescription)}
		}

		argType := "Unknown"
		if a := arg.GetStr(); a != nil {
			if a.GetDate() != nil {
				argType = "Date"
			} else if a.GetDateTime() != nil {
				argType = "DateTime"
			} else if a.GetTime() != nil {
				argType = "Time"
			} else if a.GetUuid() != nil {
				argType = "UUID"
			} else if a.GetPassword() != nil {
				argType = "Password"
			} else if a.GetSubscriptionId() != nil {
				argType = "SubscriptionId"
			} else if a.GetResourceGroupName() != nil {
				argType = "ResourceGroupName"
			} else if a.GetResourceId() != nil {
				argType = "ResourceId"
			} else if a.GetResourceLocation() != nil {
				argType = "Location"
			} else {
				argType = "String"
			}
		} else if a := arg.GetNumber(); a != nil {
			if a.GetInt32() != nil {
				argType = "Int32"
			}
			if a.GetInt64() != nil {
				argType = "Int64"
			}
			if a.GetFloat() != nil {
				argType = "Float"
			}
			if a.GetDouble() != nil {
				argType = "Double"
			}
		} else if arg.GetBoolean() != nil {
			argType = "Boolean"
		} else if arg.GetObject() != nil {
			argType = "Object"
		} else if arg.GetArray() != nil {
			argType = "Array"
		} else if arg.GetAnyType() != nil {
			argType = "AnyType"
		} else if arg.GetCls() != nil {
			clsName := arg.GetCls().ClsType
			refArg := retrieveClsArgImplInCmd(cmdInterface, clsName)
			if refArg != nil {
				if refArg.GetObject() != nil {
					argType = "Object"
				} else if refArg.GetArray() != nil {
					argType = "Array"
				}
			}
		}

		helpInfo.argGroups[argGroup].args[arg.Name] = argDescription{
			name:         arg.Name,
			options:      arg.Options,
			typ:          argType,
			help:         helpDescription{long: arg.Help.Long, short: arg.Help.Short},
			required:     arg.Required,
			nullable:     arg.Nullable,
			defaultValue: arg.Default.Value,
		}
	}

	return helpInfo, nil
}

type GroupHelpInfoImpl struct {
	names       []string
	help        helpDescription
	subCommands map[string]commandDescription
	subGroups   map[string]helpDescription
}

func (h *GroupHelpInfoImpl) Name() string {
	return strings.Join(h.names, " ")
}

func (h *GroupHelpInfoImpl) Level() HelpLevel {
	return HelpLevelGroup
}

func (h *GroupHelpInfoImpl) Flags() []string {
	return []string{}
}

func (h *GroupHelpInfoImpl) DetailedDescription() string {
	return h.help.long
}

func (h *GroupHelpInfoImpl) Segments() []string {
	segments := []string{}
	if len(h.subGroups) > 0 {
		segments = append(segments, segmentCommandGroups)
	}
	if len(h.subCommands) > 0 {
		segments = append(segments, segmentCommands)
	}
	return segments
}

func (h *GroupHelpInfoImpl) Subs(segment string) []string {
	subs := []string{}
	if segment == segmentCommandGroups {
		for name := range h.subGroups {
			subs = append(subs, name)
		}
	}
	if segment == segmentCommands {
		for name := range h.subCommands {
			subs = append(subs, name)
		}
	}
	sort.Strings(subs)
	return subs
}

func (h *GroupHelpInfoImpl) SubFlags(segment string, sub string) []string {
	if segment == segmentCommands {
		return []string{h.subCommands[sub].version}
	}
	return []string{}
}

func (h *GroupHelpInfoImpl) SubShortDescription(segment string, sub string) string {
	if segment == segmentCommandGroups {
		return h.subGroups[sub].short
	}
	if segment == segmentCommands {
		return h.subCommands[sub].help.short
	}
	return ""
}

func (h *GroupHelpInfoImpl) SubDetailedDescription(segment string, sub string) string {
	if segment == segmentCommandGroups {
		return h.subGroups[sub].long
	}
	if segment == segmentCommands {
		return h.subCommands[sub].help.long
	}
	return ""
}

type CommandHelpInfoImpl struct {
	names     []string
	version   string
	help      helpDescription
	argGroups map[string]argGroupDescription
}

func (h *CommandHelpInfoImpl) Name() string {
	return strings.Join(h.names, " ")
}

func (h *CommandHelpInfoImpl) Level() HelpLevel {
	return HelpLevelCommand
}

func (h *CommandHelpInfoImpl) Flags() []string {
	return []string{h.version}
}

func (h *CommandHelpInfoImpl) DetailedDescription() string {
	return h.help.long
}

func (h *CommandHelpInfoImpl) Segments() []string {
	segments := []string{}
	for name, argGroup := range h.argGroups {
		if len(argGroup.args) > 0 {
			segments = append(segments, name)
		}
	}
	sort.Strings(segments)
	return segments
}

func (h *CommandHelpInfoImpl) Subs(segment string) []string {
	subs := []string{}
	for name := range h.argGroups[segment].args {
		subs = append(subs, name)
	}
	sort.Strings(subs)
	return subs
}

func (h *CommandHelpInfoImpl) SubFlags(segment string, sub string) []string {
	return []string{h.argGroups[segment].args[sub].typ}
}

func (h *CommandHelpInfoImpl) SubShortDescription(segment string, sub string) string {
	return h.argGroups[segment].args[sub].help.short
}

func (h *CommandHelpInfoImpl) SubDetailedDescription(segment string, sub string) string {
	return h.argGroups[segment].args[sub].help.long
}

type ArgHelpInfoImpl struct {
	varName      string
	name         string
	typ          string
	flags        []string
	help         helpDescription
	subProps     map[string]argDescription
	subElemType  string
	subElemProps map[string]argDescription
}

func (h *ArgHelpInfoImpl) Name() string {
	return h.name
}

func (h *ArgHelpInfoImpl) Level() HelpLevel {
	return HelpLevelArg
}

func (h *ArgHelpInfoImpl) Flags() []string {
	return []string{h.typ}
}

func (h *ArgHelpInfoImpl) DetailedDescription() string {
	return h.help.long
}

func (h *ArgHelpInfoImpl) Segments() []string {
	segments := []string{}
	if len(h.subProps) > 0 {
		segments = append(segments, segmentProperties)
	}
	if len(h.subElemProps) > 0 {
		segments = append(segments, segmentElementProperties)
	}
	return segments
}

func (h *ArgHelpInfoImpl) Subs(segment string) []string {
	subs := []string{}
	if segment == segmentProperties {
		for name := range h.subProps {
			subs = append(subs, name)
		}
	}
	if segment == segmentElementProperties {
		for name := range h.subElemProps {
			subs = append(subs, name)
		}
	}
	sort.Strings(subs)
	return subs
}

func (h *ArgHelpInfoImpl) SubFlags(segment string, sub string) []string {
	if segment == segmentProperties {
		return []string{h.subProps[sub].typ}
	}
	if segment == segmentElementProperties {
		return []string{h.subElemProps[sub].typ}
	}
	return []string{}
}

func (h *ArgHelpInfoImpl) SubShortDescription(segment string, sub string) string {
	if segment == segmentProperties {
		return h.subProps[sub].help.short
	}
	if segment == segmentElementProperties {
		return h.subElemProps[sub].help.short
	}
	return ""
}

func (h *ArgHelpInfoImpl) SubDetailedDescription(segment string, sub string) string {
	if segment == segmentProperties {
		return h.subProps[sub].help.long
	}
	if segment == segmentElementProperties {
		return h.subElemProps[sub].help.long
	}
	return ""
}

type helpDescription struct {
	short string
	long  string
}

type commandDescription struct {
	version string
	help    helpDescription
}

type argGroupDescription struct {
	name string
	args map[string]argDescription
}

type argDescription struct {
	name         string
	options      []string
	typ          string
	help         helpDescription
	required     bool
	nullable     bool
	defaultValue string
}

const (
	segmentCommandGroups = "Sub Command Groups"
	segmentCommands      = "Sub Commands"
)

const (
	segmentProperties        = "Properties"
	segmentElementProperties = "Element Properties"
)
