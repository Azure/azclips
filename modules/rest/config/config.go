package config

import "github.com/Azure/azure-cli-lite/shared"

const VERSION = "0.0.1"
const MODULE_NAME = "rest" // must be the same as the module folder name
const MIN_CORE_VERSION = "0.0.1"

var Handshake = shared.ModuleHandshakeConfig(MODULE_NAME)
