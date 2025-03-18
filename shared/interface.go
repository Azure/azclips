package shared

import (
	"context"

	"github.com/Azure/azure-cli-lite/shared/proto"
	"github.com/hashicorp/go-plugin"
	"google.golang.org/grpc"
)

const HANDSHAKE_KEY = "AZURE_CLI_MODULE"

func ModuleHandshakeConfig(name string) plugin.HandshakeConfig {
	return plugin.HandshakeConfig{
		ProtocolVersion:  1,
		MagicCookieKey:   HANDSHAKE_KEY,
		MagicCookieValue: name,
	}
}

// AzContext is the context of the Azure CLI provided by the core
type AzContext interface {
	// GetCoreInfo returns the core information
	GetCoreInfo() (*proto.CoreInfo, error)
}

type Module interface {
	// GetName returns the metadata of the module
	Metadata(azCtx AzContext) (*proto.ModuleMetadata, error)

	// Check the command returns the command uri if the command belongs to this module, else returns nil.
	// If the command is not downloaded, this function will download the command.
	// This approach can support the command redirect to another command even cross modules
	Check(azCtx AzContext, uri CMDUri) (*CMDUri, error)

	// Return the command interface definition
	Interface(azCtx AzContext, uri CMDUri) (*proto.CMDInterface, error)

	// check the available versions of the command
	// Versions(azCtx AzContext, uri CMDUri) (, error)

	// // Execute executes the command with the given command name uri
	// Execute(azCtx AzContext, uri CMDUri, args CMDArgs) (*proto.CMDOutput, error)
}

type ModulePlugin struct {
	plugin.NetRPCUnsupportedPlugin
	Impl Module
}

func (p *ModulePlugin) GRPCServer(broker *plugin.GRPCBroker, s *grpc.Server) error {
	proto.RegisterModuleServer(s, &GRPCServer{Impl: p.Impl, broker: broker})
	return nil
}

func (p *ModulePlugin) GRPCClient(ctx context.Context, broker *plugin.GRPCBroker, c *grpc.ClientConn) (interface{}, error) {
	return &GRPCClient{client: proto.NewModuleClient(c), broker: broker}, nil
}

type Priority uint32

const (
	PRIORITY_UNDEFINED Priority = 0
	// reserve 1-9 for future use
	// the built-in module has the highest priority, it's not allowed to be overridden by plugins
	// The command priority range is 10-29
	PRIORITY_BUILTIN Priority = 10
	// the plugin module has the high priority, it's allowed to be overridden by others
	// The command priority range is 30-49
	PRIORITY_HIGH Priority = 30
	// the plugin module has the medium priority, it's allowed to be overridden by others
	// The command priority range is 50-69
	PRIORITY_MEDIUM Priority = 50
	// the plugin module has the low priority, it's allowed to be overridden by others
	// The command priority range is 70-89
	PRIORITY_LOW Priority = 70
	// the plugin module has the lowest priority, it's allowed to be overridden by others, usually the rest module
	// The command priority range is 90-109
	PRIORITY_LOWEST Priority = 90
)

func (p Priority) IsBuiltIn() bool {
	return p >= PRIORITY_BUILTIN && p < PRIORITY_HIGH
}

func (p Priority) IsHigh() bool {
	return p >= PRIORITY_HIGH && p < PRIORITY_MEDIUM
}

func (p Priority) IsMedium() bool {
	return p >= PRIORITY_MEDIUM && p < PRIORITY_LOW
}

func (p Priority) IsLow() bool {
	return p >= PRIORITY_LOW && p < PRIORITY_LOWEST
}

func (p Priority) IsLowest() bool {
	return p >= PRIORITY_LOWEST
}
