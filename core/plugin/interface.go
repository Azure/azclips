package plugin

import (
	"net/rpc"

	"github.com/hashicorp/go-plugin"
)

const HANDSHAKE_KEY = "AZURE_CLI_PLUGIN"
const PLUGIN_NAME = "azure-cli-core"

// Handshake is the handshake config for the core plugin
var Handshake = plugin.HandshakeConfig{
	ProtocolVersion:  1,
	MagicCookieKey:   HANDSHAKE_KEY,
	MagicCookieValue: PLUGIN_NAME,
}

// PluginMap is the map of plugins for the core plugin. used in client side
// to register the plugin
var PluginMap = map[string]plugin.Plugin{
	PLUGIN_NAME: &CorePlugin{},
}

// Core is the interface that we'll implement in the core plugin
type Core interface {
	RunCommand(args []string) string
}

// CoreImpl is the implementation of the Core interface
type CoreRPC struct {
	client *rpc.Client
}

func (c *CoreRPC) RunCommand(args []string) string {
	var response string
	err := c.client.Call("Plugin.RunCommand", args, &response)
	if err != nil {
		panic(err)
	}
	return response
}

type CoreRPCServer struct {
	Impl Core
}

func (s *CoreRPCServer) RunCommand(args []string, response *string) error {
	*response = s.Impl.RunCommand(args)
	return nil
}

type CorePlugin struct {
	Impl Core
}

func (p *CorePlugin) Server(*plugin.MuxBroker) (interface{}, error) {
	return &CoreRPCServer{Impl: p.Impl}, nil
}

func (CorePlugin) Client(b *plugin.MuxBroker, c *rpc.Client) (interface{}, error) {
	return &CoreRPC{client: c}, nil
}
