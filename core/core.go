package core

import (
	"os"
	"strings"

	"github.com/Azure/azure-cli-v3/core/shared"
	hclog "github.com/hashicorp/go-hclog"
	"github.com/hashicorp/go-plugin"
)

func StartCorePlugin() {
	core := CoreImpl()
	plugin.Serve(&plugin.ServeConfig{
		HandshakeConfig: shared.Handshake,
		Plugins: map[string]plugin.Plugin{
			shared.PLUGIN_NAME: &shared.CorePlugin{Impl: core},
		},
	})
}

func CoreImpl() shared.Core {
	logger := hclog.New(&hclog.LoggerOptions{
		Name:       shared.PLUGIN_NAME,
		Level:      hclog.Info,
		Output:     os.Stderr,
		JSONFormat: true,
	})

	return &coreImpl{
		logger: logger,
	}
}

type coreImpl struct {
	logger hclog.Logger
}

func (c *coreImpl) RunCommand(args []string) string {
	c.logger.Info("Message from core plugin", "args", args)
	return strings.Join(args, " ")
}
