package auth

import (
	"github.com/Azure/azure-cli-v3/shared"
	"github.com/Azure/azure-cli-v3/shared/proto"
	hclog "github.com/hashicorp/go-hclog"
)

type authModule struct {
	logger hclog.Logger
}

func NewAuthModule(logger hclog.Logger) *authModule {
	return &authModule{logger: logger}
}

func (m *authModule) Metadata(azCtx shared.AzContext) (*proto.ModuleMetadata, error) {
	coreInfo, err := azCtx.GetCoreInfo()
	if err != nil {
		return nil, err
	}
	m.logger.Info("Core info", "coreInfo", coreInfo)

	return &proto.ModuleMetadata{
		Name: "auth",
		Cls: &proto.ModuleMetadata_BuildIn{
			BuildIn: &proto.BuildInModule{},
		},
	}, nil
}

func (m *authModule) Check(azCtx shared.AzContext, uri shared.CMDUri) (*shared.CMDUri, error) {
	return &uri, nil
}

func (m *authModule) Interface(azCtx shared.AzContext, uri shared.CMDUri) (*proto.CMDInterface, error) {
	return nil, nil
}
