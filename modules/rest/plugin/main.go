package main

import (
	"os"

	"github.com/Azure/azure-cli-v3/modules/rest"
	"github.com/Azure/azure-cli-v3/modules/rest/config"
	"github.com/Azure/azure-cli-v3/shared"
	hclog "github.com/hashicorp/go-hclog"
	"github.com/hashicorp/go-plugin"
)

func main() {
	module := ModuleImpl()
	plugin.Serve(&plugin.ServeConfig{
		HandshakeConfig: config.Handshake,
		Plugins: map[string]plugin.Plugin{
			config.MODULE_NAME: &shared.ModulePlugin{Impl: module},
		},
		// A non-nil value here enables gRPC serving for this plugin...
		GRPCServer: plugin.DefaultGRPCServer,
	})
}

func ModuleImpl() shared.Module {
	logger := hclog.New(&hclog.LoggerOptions{
		Name:   config.MODULE_NAME,
		Level:  hclog.Trace,
		Output: os.Stderr,
		// JSONFormat: true,
	})

	logger.Info("Rest module loaded")
	return rest.NewRestModule(logger)
}
