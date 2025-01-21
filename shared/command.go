package shared

// The command uri is the '/' joined command names with the module name as host such as 'az://{the module name}/vm/list' or 'az://{the module name}/network/vnet/create'
// The command uri also support to express the command group when it's end with '/' such as 'az://{the module name}/vm/' or 'az://{the module name}/network/'
// `az://{the module name}/' represents the module level, and it's useful for get the help message for the top level command groups in module
type CMDUri string

// type CMDArgs interface{}

// type CMDResult interface{}
