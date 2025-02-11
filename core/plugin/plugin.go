package plugin

import (
	"os"

	"github.com/Azure/azure-cli-lite/core"
	hclog "github.com/hashicorp/go-hclog"
	"github.com/hashicorp/go-plugin"
)

func StartCorePlugin() {
	logger := hclog.New(&hclog.LoggerOptions{
		Name:   PLUGIN_NAME,
		Level:  hclog.Trace,
		Output: os.Stderr,
		// JSONFormat: true,
	})

	core := core.NewCore(logger)
	core.Init()
	defer core.Dispose()

	plugin.Serve(&plugin.ServeConfig{
		HandshakeConfig: Handshake,
		Plugins: map[string]plugin.Plugin{
			PLUGIN_NAME: &CorePlugin{Impl: core},
		},
	})
}
