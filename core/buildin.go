package core

import (
	auth "github.com/Azure/azure-cli-v3/modules/auth"
)

func (c *coreImpl) loadBuildInModules() {
	c.manager.RegisterBuildIn(&BuildInInfo{
		Name: "auth",
		Impl: auth.NewAuthModule(c.logger),
	})
}
