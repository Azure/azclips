package core

import (
	"github.com/Azure/azure-cli-v3/shared/proto"
)

func (c *coreImpl) GetCoreInfo() (*proto.CoreInfo, error) {
	return &proto.CoreInfo{
		Version: VERSION,
	}, nil
}
