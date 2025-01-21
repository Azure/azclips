package core

import (
	"strings"

	rest "github.com/Azure/azure-cli-v3/modules/rest/config"
	"github.com/Azure/azure-cli-v3/shared/proto"
	hclog "github.com/hashicorp/go-hclog"
)

type coreImpl struct {
	logger hclog.Logger
	// the modules loaded in the core
	manager *Manager
}

func NewCore(logger hclog.Logger) *coreImpl {
	manager := NewManager(logger)
	return &coreImpl{
		logger:  logger,
		manager: manager,
	}
}

func (c *coreImpl) Init() {
	c.manager.Init()
	c.manager.LoadModule(rest.MODULE_NAME)
}

func (c *coreImpl) Dispose() {
	c.manager.Dispose()
}

func (c *coreImpl) GetCoreInfo() (*proto.CoreInfo, error) {
	return &proto.CoreInfo{
		Version: VERSION,
	}, nil
}

func (c *coreImpl) RunCommand(args []string) string {
	c.logger.Info("Message from core plugin", "args", args)

	restModule, err := c.manager.GetModule(rest.MODULE_NAME)
	if err != nil {
		c.logger.Error("Error getting rest module", "error", err)
		return ""
	}
	c.logger.Info("Rest module loaded")

	metadata, err := restModule.Metadata(c)
	if err != nil {
		c.logger.Error("Error getting metadata", "error", err)
		return ""
	}
	c.logger.Info("Metadata", "metadata", metadata)

	return strings.Join([]string{metadata.Name, metadata.Version}, " ")
}
