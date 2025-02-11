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
