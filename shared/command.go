package shared

import (
	"fmt"
	"strings"
)

const CMD_URI_SCHEME = "az://"

// The command uri is the '/' joined command names with the module name as host such as 'az://{the module name}/application/list' or 'az://{the module name}/vnet/create'
// The command uri also support to express the command group when it's end with '/' such as 'az://{the module name}/application/' or 'az://{the module name}/vnet/'
// `az://{the module name}/' represents the module level
type CMDUri struct {
	ModuleName string
	Cmds       []string
	Executable bool
	Version    string
	Params     map[string]string
}

func (c CMDUri) String() string {
	paramsSuffix := ""
	if c.Version != "" || len(c.Params) > 0 {
		paramsSuffix = "?"
		if c.Version != "" {
			paramsSuffix += fmt.Sprintf("version=%s", c.Version)
		}
		for k, v := range c.Params {
			paramsSuffix += fmt.Sprintf("&%s=%s", k, v)
		}
	}
	if len(c.Cmds) == 0 {
		return fmt.Sprintf("%s%s/%s", CMD_URI_SCHEME, c.ModuleName, paramsSuffix)
	}
	if c.Executable {
		return fmt.Sprintf("%s%s/%s%s", CMD_URI_SCHEME, c.ModuleName, strings.Join(c.Cmds, "/"), paramsSuffix)
	} else {
		return fmt.Sprintf("%s%s/%s/%s", CMD_URI_SCHEME, c.ModuleName, strings.Join(c.Cmds, "/"), paramsSuffix)
	}
}

func ParseCMDUri(raw string) (*CMDUri, error) {
	if !strings.HasPrefix(raw, CMD_URI_SCHEME) {
		return nil, fmt.Errorf("invalid URI scheme: must start with %s", CMD_URI_SCHEME)
	}
	// parse params
	version := ""
	params := make(map[string]string)
	paramsIndex := strings.Index(raw, "?")
	if paramsIndex != -1 {
		for _, part := range strings.Split(raw[paramsIndex+1:], "&") {
			parts := strings.Split(part, "=")
			if len(parts) == 2 {
				if parts[0] == "version" {
					version = parts[1]
				} else {
					params[parts[0]] = parts[1]
				}
			}
		}
		raw = raw[:paramsIndex]
	}
	parts := strings.Split(raw[len(CMD_URI_SCHEME):], "/")
	if strings.HasSuffix(raw, "/") {
		parts = parts[:len(parts)-1]
	}
	if len(parts) == 0 {
		return nil, fmt.Errorf("invalid URI: must have at least the module name")
	}
	moduleName := parts[0]
	if len(moduleName) == 0 {
		return nil, fmt.Errorf("invalid URI: module name is empty")
	}
	var cmds []string
	if len(parts) > 1 {
		cmds = parts[1:]
	}
	for _, cmd := range cmds {
		if len(cmd) == 0 {
			return nil, fmt.Errorf("invalid URI: command name is empty")
		}
	}
	if len(cmds) == 0 && !strings.HasSuffix(raw, "/") {
		return nil, fmt.Errorf("invalid URI: module name should end with '/'")
	}
	executable := len(cmds) > 0 && !strings.HasSuffix(raw, "/")
	return &CMDUri{ModuleName: moduleName, Cmds: cmds, Executable: executable, Version: version, Params: params}, nil
}
