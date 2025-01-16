package main

import (
	"fmt"
	"log"
	"os"
	"os/exec"

	"github.com/Azure/azure-cli-v3/core/shared"
	hclog "github.com/hashicorp/go-hclog"
	"github.com/hashicorp/go-plugin"
)

func StartClient(args []string) {
	logger := hclog.New(&hclog.LoggerOptions{
		Name:       "az",
		Output:     os.Stderr,
		Level:      hclog.Warn,
		JSONFormat: true,
	})

	client := plugin.NewClient(&plugin.ClientConfig{
		HandshakeConfig: shared.Handshake,
		Plugins:         shared.PluginMap,
		Cmd:             exec.Command("./az", START_CORE_FLAG),
		Logger:          logger,
	})

	defer client.Kill()

	// Connect via RPC
	rpcClient, err := client.Client()
	if err != nil {
		log.Fatal(err)
	}

	// Request the plugin
	raw, err := rpcClient.Dispense(shared.PLUGIN_NAME)
	if err != nil {
		log.Fatal(err)
	}

	// We should have a Core now! This feels like a normal interface
	// implementation but is in fact over an RPC connection.
	core := raw.(shared.Core)
	if len(args) == 0 {
		args = []string{"--help"}
	}
	result := core.RunCommand(args)
	fmt.Println(result)
}
