package core

import (
	"os"
	"strings"

	"github.com/Azure/azure-cli-v3/core/shared"
	hclog "github.com/hashicorp/go-hclog"
	"github.com/hashicorp/go-plugin"
)

func StartCorePlugin() {
	logger := hclog.New(&hclog.LoggerOptions{
		Name:       shared.PLUGIN_NAME,
		Level:      hclog.Info,
		Output:     os.Stderr,
		JSONFormat: true,
	})

	core := &CoreImpl{logger: logger}

	logger.Debug("Starting Core plugin server")

	plugin.Serve(&plugin.ServeConfig{
		HandshakeConfig: shared.Handshake,
		Plugins: map[string]plugin.Plugin{
			shared.PLUGIN_NAME: &shared.CorePlugin{Impl: core},
		},
	})
}

type CoreImpl struct {
	logger hclog.Logger
}

func (c *CoreImpl) RunCommand(args []string) string {
	c.logger.Info("Message from core plugin", "args", args)
	return strings.Join(args, " ")
}
