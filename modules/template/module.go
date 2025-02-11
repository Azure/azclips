package template

import (
	"github.com/Azure/azure-cli-lite/modules/template/config"
	"github.com/Azure/azure-cli-lite/shared"
	"github.com/Azure/azure-cli-lite/shared/proto"
	hclog "github.com/hashicorp/go-hclog"
)

type module struct {
	logger hclog.Logger
}

func New(logger hclog.Logger) *module {
	return &module{logger: logger}
}

func (r *module) Metadata(azCtx shared.AzContext) (*proto.ModuleMetadata, error) {
	coreInfo, err := azCtx.GetCoreInfo()
	if err != nil {
		return nil, err
	}
	r.logger.Info("Core info", "coreInfo", coreInfo)

	return &proto.ModuleMetadata{
		Name: config.MODULE_NAME,
		Cls: &proto.ModuleMetadata_Plugin{
			Plugin: &proto.PluginModule{
				Version: config.VERSION,
			},
		},
	}, nil
}

func (r *module) Check(_ shared.AzContext, uri shared.CMDUri) (*shared.CMDUri, error) {
	return &uri, nil
}

func (r *module) Interface(_ shared.AzContext, uri shared.CMDUri) (*proto.CMDInterface, error) {
	return nil, nil
}
