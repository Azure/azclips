package main

import (
	"os"

	"github.com/Azure/azure-cli-lite/core"
	tea "github.com/charmbracelet/bubbletea"
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
	view, err := core.RunCommand(os.Args[1:])
	if err != nil {
		// TODO: handle errors
		logger.Error("run command failed", "error", err)
		os.Exit(1)
	}

	if _, err := tea.NewProgram(view).Run(); err != nil {
		// TODO: handle errors
		logger.Error("run command failed", "error", err)
		os.Exit(1)
	}
}
