package auth

import (
	"github.com/Azure/azure-cli-lite/shared"
	"github.com/Azure/azure-cli-lite/shared/proto"
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
		// The top priority, which are not allowed to be overridden by plugins
		Priority: uint32(shared.PRIORITY_BUILTIN),
		Cls: &proto.ModuleMetadata_BuiltIn{
			BuiltIn: &proto.BuiltInModule{},
		},
	}, nil
}

func (m *authModule) Check(azCtx shared.AzContext, uri shared.CMDUri) (*shared.CMDUri, error) {
	return &uri, nil
}

func (m *authModule) Interface(azCtx shared.AzContext, uri shared.CMDUri) (*proto.CMDInterface, error) {
	return nil, nil
}
