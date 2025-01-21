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

func getModulePaths() []string {
	modulePaths := os.Getenv("AZURE_CLI_MODULE_PATH")
	if modulePaths == "" {
		return []string{filepath.Join(getConfigDir(), "modules")}
	}
	return strings.Split(modulePaths, ";")
}
