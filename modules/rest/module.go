package rest

import (
	"github.com/Azure/azure-cli-v3/modules/rest/config"
	"github.com/Azure/azure-cli-v3/shared"
	"github.com/Azure/azure-cli-v3/shared/proto"
	hclog "github.com/hashicorp/go-hclog"
)

type restModule struct {
	logger hclog.Logger
}

func NewRestModule(logger hclog.Logger) *restModule {
	return &restModule{logger: logger}
}

func (r *restModule) Metadata(azCtx shared.AzContext) (*proto.ModuleMetadata, error) {
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

func (r *restModule) Check(_ shared.AzContext, uri shared.CMDUri) (*shared.CMDUri, error) {
	return &uri, nil
}

func (r *restModule) Interface(_ shared.AzContext, uri shared.CMDUri) (*proto.CMDInterface, error) {
	return nil, nil
}
