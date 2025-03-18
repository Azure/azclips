package core

import (
	"github.com/Azure/azure-cli-lite/shared"
	"github.com/Azure/azure-cli-lite/shared/proto"
)

type CmdExecutor interface {
	// Execute(executeInfo *executeInfo) (View, error)
}

type cmdExecutorImpl struct {
	core    *coreImpl
	cmd     shared.CMDUri
	intface *proto.CMDInterface
	params  interface{}
}
