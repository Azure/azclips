package main

import (
	"os"

	"github.com/Azure/azure-cli-v3/modules/rest/config"
	"github.com/Azure/azure-cli-v3/shared"
	"github.com/Azure/azure-cli-v3/shared/proto"
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
	return &restModule{
		logger: logger,
	}
}

type restModule struct {
	logger hclog.Logger
}

func (r *restModule) Metadata(azCtx shared.AzContext) (*proto.ModuleMetadata, error) {
	coreInfo, err := azCtx.GetCoreInfo()
	if err != nil {
		return nil, err
	}
	r.logger.Info("Core info", "coreInfo", coreInfo)

	return &proto.ModuleMetadata{
		Name:    config.MODULE_NAME,
		Version: config.VERSION,
	}, nil
}

func (r *restModule) Check(_ shared.AzContext, uri shared.CMDUri) (shared.CMDUri, error) {
	return uri, nil
}
