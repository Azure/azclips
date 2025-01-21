package shared

import (
	"context"

	"github.com/Azure/azure-cli-v3/shared/proto"
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

type AzContext interface {
	GetCoreInfo() (*proto.CoreInfo, error)
}

type Module interface {
	// GetName returns the name of the module
	Metadata(azCtx AzContext) (*proto.ModuleMetadata, error)

	// Check the command returns the command uri if the command is exists, else returns empty string
	// This approach can support the command redirect to another command
	Check(azCtx AzContext, uri CMDUri) (CMDUri, error)

	// // Return the help message for the command with the given cmd uri
	// Help(azCtx AzContext, uri CMDUri) interface{}

	// // Execute executes the command with the given command name uri
	// Execute(azCtx AzContext, uri CMDUri, args CMDArgs) (CMDResult, error)
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
