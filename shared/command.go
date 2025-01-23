package shared

import (
	"fmt"
	"strings"
)

// The command uri is the '/' joined command names with the module name as host such as 'az://{the module name}/application/list' or 'az://{the module name}/vnet/create'
// The command uri also support to express the command group when it's end with '/' such as 'az://{the module name}/application/' or 'az://{the module name}/vnet/'
// `az://{the module name}/' represents the module level
type CMDUri string

func BuildCMDUri(moduleName string, cmds []string, isGroup bool) CMDUri {
	if isGroup && len(cmds) > 0 {
		return CMDUri(fmt.Sprintf("az://%s/%s/", moduleName, strings.Join(cmds, "/")))
	} else {
		return CMDUri(fmt.Sprintf("az://%s/%s", moduleName, strings.Join(cmds, "/")))
	}
}

func (c CMDUri) ModuleName() string {
	return strings.Split(string(c), "/")[1]
}

func (c CMDUri) IsModuleLevel() bool {
	parts := strings.Split(string(c), "/")
	// Module level has format "az://{module}/" which splits into ["az:", "", "{module}", ""]
	return len(parts) == 4 && parts[len(parts)-1] == ""
}

func (c CMDUri) IsGroupLevel() bool {
	// Check if it ends with "/" but is not module level
	return strings.HasSuffix(string(c), "/") && !c.IsModuleLevel()
}

func (c CMDUri) IsCommandLevel() bool {
	return !strings.HasSuffix(string(c), "/")
}
