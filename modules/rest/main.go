package main

import (
	"os"

	"github.com/Azure/azure-cli-v3/modules/rest/config"
	"github.com/Azure/azure-cli-v3/modules/shared"
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
	})
}

func ModuleImpl() shared.Module {
	logger := hclog.New(&hclog.LoggerOptions{
		Name:       config.MODULE_NAME,
		Level:      hclog.Info,
		Output:     os.Stderr,
		JSONFormat: true,
	})

	return &restModule{
		logger: logger,
	}
}

type restModule struct {
	logger hclog.Logger
}

func (r *restModule) Metadata() shared.ModuleMetadata {
	return shared.ModuleMetadata{
		Name:    config.MODULE_NAME,
		Version: config.VERSION,
	}
}

func (r *restModule) Check(uri shared.CMDUri) shared.CMDUri {
	return uri
}
