package core

import (
	auth "github.com/Azure/azure-cli-lite/modules/auth"
)

func (c *coreImpl) loadBuildInModules() {
	c.manager.RegisterBuildIn(&BuildInInfo{
		Name: "auth",
		Impl: auth.NewAuthModule(c.logger),
	})
}
