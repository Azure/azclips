package main

import (
	"fmt"
	"os"

	"github.com/Azure/azure-cli-lite/core"
	hclog "github.com/hashicorp/go-hclog"
)

func main() {
	clientName := "azlite"
	description := "Azure CLI Lite, the lightweight Azure CLI alternative."
	logger := hclog.New(&hclog.LoggerOptions{
		Name:   clientName,
		Output: os.Stderr,
		Level:  hclog.Trace,
		// JSONFormat: true,
	})

	core := core.NewCore(logger, clientName, description)
	defer core.Dispose()
	core.Init()

	// run command with out the first argument
	result, err := core.RunCommand(os.Args[1:])
	if err != nil {
		fmt.Println(err)
	}
	fmt.Println(result)
}
