package core

import (
	"github.com/Azure/azure-cli-lite/shared/proto"
)

func (c *coreImpl) GetCoreInfo() (*proto.CoreInfo, error) {
	return &proto.CoreInfo{
		Version: VERSION,
	}, nil
}
