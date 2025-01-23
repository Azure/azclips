package core

import (
	"os"
	"path/filepath"
	"strings"
)

func getConfigDir() string {
	configDir := os.Getenv("AZURE_CONFIG_DIR")
	if configDir == "" {
		homeDir, _ := os.UserHomeDir()
		configDir = filepath.Join(homeDir, ".azure")
	}
	return configDir
}

func getPluginPaths() []string {
	pluginPaths := os.Getenv("AZURE_CLI_PLUGIN_PATH")
	if pluginPaths == "" {
		return []string{filepath.Join(getConfigDir(), "plugins")}
	}
	return strings.Split(pluginPaths, ";")
}
