package core

import (
	auth "github.com/Azure/azure-cli-lite/modules/auth"
)

func (c *coreImpl) loadBuiltInModules() {
	c.manager.RegisterBuiltIn(&BuiltInInfo{
		Name: "auth",
		Impl: auth.NewAuthModule(c.logger),
	})
}
