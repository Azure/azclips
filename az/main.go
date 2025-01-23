package main

import (
	"fmt"
	"os"

	"github.com/Azure/azure-cli-v3/core"
	hclog "github.com/hashicorp/go-hclog"
)

func main() {
	logger := hclog.New(&hclog.LoggerOptions{
		Name:   "az",
		Output: os.Stderr,
		Level:  hclog.Trace,
		// JSONFormat: true,
	})

	core := core.NewCore(logger)
	defer core.Dispose()
	core.Init()

	args := os.Args[1:]
	if len(args) == 0 {
		args = []string{"--help"}
	}
	result, err := core.RunCommand(args)
	if err != nil {
		fmt.Println(err)
	}
	fmt.Println(result)
}
