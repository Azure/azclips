package shared

import (
	"net/rpc"

	"github.com/hashicorp/go-plugin"
)

const HANDSHAKE_KEY = "AZURE_CLI_MODULE"

type Module interface {
	// GetName returns the name of the module
	Metadata() ModuleMetadata

	// Check the command returns the command uri if the command is exists, else returns empty string
	// This approach can support the command redirect to another command
	Check(uri CMDUri) CMDUri

	// // Return the help message for the command with the given cmd uri
	// Help(uri CMDUri, ctx *AzContext) interface{}

	// // Execute executes the command with the given command name uri
	// Execute(uri CMDUri, args CMDArgs, ctx *AzContext) (CMDResult, error)
}

type ModuleMetadata struct {
	Name    string
	Version string
}

type ModulePlugin struct {
	Impl Module
}

func (p *ModulePlugin) Server(*plugin.MuxBroker) (interface{}, error) {
	return &RPCServer{Impl: p.Impl}, nil
}

func (p *ModulePlugin) Client(b *plugin.MuxBroker, c *rpc.Client) (interface{}, error) {
	return &RPCClient{client: c}, nil
}
