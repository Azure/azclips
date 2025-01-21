package core

import (
	"errors"
	"os/exec"
	"path/filepath"
	"strings"
	"sync"

	"github.com/Azure/azure-cli-v3/shared"
	hclog "github.com/hashicorp/go-hclog"
	"github.com/hashicorp/go-plugin"
)

type ModuleInfo struct {
	Name   string
	path   string
	client *plugin.Client
	// initialized is used to ensure that the client is initialized only once
	initialized sync.Once
}

type Manager struct {
	logger  hclog.Logger
	modules map[string]*ModuleInfo
	// doneCh is a channel that receives a signal when the manager is disposed
	doneCh chan struct{}
}

func NewManager(logger hclog.Logger) *Manager {
	return &Manager{
		logger:  logger,
		modules: make(map[string]*ModuleInfo),
		doneCh:  make(chan struct{}),
	}
}

func (m *Manager) Dispose() {
	close(m.doneCh)
	// using plugin internal function to dispose of the clients
	plugin.CleanupClients()
}

func (m *Manager) Init() {
	modulePaths := getModulePaths()
	// reverse iteration the modulePaths so that the modules in the last folder are loaded first and
	// will be overridden by the modules in the previous folder if they have the same name
	for i := len(modulePaths) - 1; i >= 0; i-- {
		path := modulePaths[i]
		plugins, err := plugin.Discover("*.azm", path)
		if err != nil {
			m.logger.Error("Error discovering modules in path", "path", path, "error", err)
			continue
		}
		// get the file name without the .azm extension
		for _, plugin := range plugins {
			name := filepath.Base(plugin)
			name = strings.TrimSuffix(name, ".azm")
			module := &ModuleInfo{
				Name: name,
				path: plugin,
			}
			m.modules[name] = module
		}
	}

	for _, module := range m.modules {
		m.logger.Info("Detected module", "name", module.Name, "path", module.path)
	}
}

func (m *Manager) LoadModule(name string) error {
	// check if is closing
	select {
	case <-m.doneCh:
		return ErrManagerDisposed
	default:
	}

	module, ok := m.modules[name]
	if !ok {
		return ErrModuleNotFound
	}

	var loadErr error

	module.initialized.Do(func() {
		client := plugin.NewClient(&plugin.ClientConfig{
			HandshakeConfig: shared.ModuleHandshakeConfig(name),
			Plugins: map[string]plugin.Plugin{
				name: &shared.ModulePlugin{},
			},
			Cmd:              exec.Command(module.path),
			AllowedProtocols: []plugin.Protocol{plugin.ProtocolGRPC},
			Logger:           m.logger,
			// use the managed client to dispose of the client when the manager is disposed
			Managed: true,
		})

		// check if is closing
		select {
		case <-m.doneCh:
			client.Kill()
			loadErr = ErrManagerDisposed
		default:
			module.client = client
		}
	})

	return loadErr
}

func (m *Manager) GetModule(name string) (shared.Module, error) {
	if _, ok := m.modules[name]; !ok {
		return nil, ErrModuleNotFound
	}

	client := m.modules[name].client
	if client == nil {
		return nil, ErrModuleUnloaded
	}

	// Connect via RPC
	rpcClient, err := client.Client()
	if err != nil {
		return nil, err
	}

	// Request the plugin
	raw, err := rpcClient.Dispense(name)
	if err != nil {
		return nil, err
	}
	return raw.(shared.Module), nil
}

var ErrModuleNotFound = errors.New("module not found in registered modules")

var ErrModuleUnloaded = errors.New("module is not loaded")

var ErrManagerDisposed = errors.New("manager is disposed")
