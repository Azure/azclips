package config

import (
	"github.com/Azure/azure-cli-v3/modules/shared"
	"github.com/hashicorp/go-plugin"
)

const VERSION = "0.0.1"
const MODULE_NAME = "rest"

var Handshake = plugin.HandshakeConfig{
	ProtocolVersion:  1,
	MagicCookieKey:   shared.HANDSHAKE_KEY,
	MagicCookieValue: MODULE_NAME,
}
