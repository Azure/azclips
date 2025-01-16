package main

import (
	"os"

	"github.com/Azure/azure-cli-v3/core"
)

const START_CORE_FLAG = "__CORE__"

func main() {
	args := os.Args[1:]
	if len(args) == 1 && args[0] == START_CORE_FLAG {
		core.StartCorePlugin()
	} else {
		StartClient(args)
	}
}
