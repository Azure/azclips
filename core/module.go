package core

import (
	"errors"
	"os/exec"
	"path/filepath"
	"strings"
	"sync"

	"github.com/Azure/azure-cli-lite/shared"
	hclog "github.com/hashicorp/go-hclog"
	"github.com/hashicorp/go-plugin"
)

// TODO: support build-in modules

type PluginInfo struct {
	Name   string
	path   string
	client *plugin.Client
	// initialized is used to ensure that the client is initialized only once
	initialized sync.Once
}

type BuiltInInfo struct {
	Name string
	Impl shared.Module
}

type ModuleManager struct {
	logger   hclog.Logger
	plugins  map[string]*PluginInfo
	builtIns map[string]*BuiltInInfo
	// doneCh is a channel that receives a signal when the manager is disposed
	doneCh chan struct{}
}

func NewModuleManager(logger hclog.Logger) *ModuleManager {
	return &ModuleManager{
		logger:   logger,
		plugins:  make(map[string]*PluginInfo),
		builtIns: make(map[string]*BuiltInInfo),
		doneCh:   make(chan struct{}),
	}
}

func (m *ModuleManager) dispose() {
	close(m.doneCh)
	// using plugin internal function to dispose of the clients
	plugin.CleanupClients()
}

func (m *ModuleManager) init() {
	pluginPaths := getPluginPaths()
	// reverse iteration the modulePaths so that the modules in the last folder are loaded first and
	// will be overridden by the modules in the previous folder if they have the same name
	for i := len(pluginPaths) - 1; i >= 0; i-- {
		path := pluginPaths[i]
		plugins, err := plugin.Discover("*.azm", path)
		if err != nil {
			m.logger.Error("Error discovering modules in path", "path", path, "error", err)
			continue
		}
		// get the file name without the .azm extension
		for _, p := range plugins {
			name := filepath.Base(p)
			name = strings.TrimSuffix(name, ".azm")
			m.plugins[name] = &PluginInfo{
				Name: name,
				path: p,
			}
		}
	}

	for _, p := range m.plugins {
		m.logger.Info("Detected plugin", "name", p.Name, "path", p.path)
	}
}

func (m *ModuleManager) getModuleNames() []string {
	names := make([]string, 0, len(m.plugins)+len(m.builtIns))
	for name := range m.builtIns {
		names = append(names, name)
	}
	for name := range m.plugins {
		names = append(names, name)
	}
	return names
}

// Load the plugin module
func (m *ModuleManager) loadPlugin(name string) error {
	// check if is closing
	select {
	case <-m.doneCh:
		return ErrManagerDisposed
	default:
	}

	pg, pg_ok := m.plugins[name]
	if !pg_ok {
		return ErrPluginNotFound
	}

	var loadErr error

	pg.initialized.Do(func() {
		client := plugin.NewClient(&plugin.ClientConfig{
			HandshakeConfig: shared.ModuleHandshakeConfig(name),
			Plugins: map[string]plugin.Plugin{
				name: &shared.ModulePlugin{},
			},
			Cmd:              exec.Command(pg.path),
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
			pg.client = client
		}
	})

	return loadErr
}

func (m *ModuleManager) registerBuiltIn(info *BuiltInInfo) error {
	m.builtIns[info.Name] = info
	return nil
}

func (m *ModuleManager) getModule(name string) (shared.Module, error) {
	pg, pg_ok := m.plugins[name]
	bi, bi_ok := m.builtIns[name]

	// the plugin module has higher priority than the build-in module
	// this can be used to override the build-in module for hot fix
	if pg_ok {
		if pg.client == nil {
			if err := m.loadPlugin(name); err != nil {
				return nil, err
			}
			if pg.client == nil {
				return nil, ErrPluginUnloaded
			}
		}

		// connect via rRPC
		grpcClient, err := pg.client.Client()
		if err != nil {
			return nil, err
		}

		// Request the plugin
		raw, err := grpcClient.Dispense(name)
		if err != nil {
			return nil, err
		}
		return raw.(shared.Module), nil
	}

	if bi_ok {
		return bi.Impl, nil
	}

	return nil, ErrModuleNotFound
}

func (m *ModuleManager) dynamicDownloadPlugin(name string) error {
	// TODO: dynamic download the plugin from the internet
	return ErrPluginNotFound
}

var ErrPluginNotFound = errors.New("plugin not found plugins")
var ErrPluginUnloaded = errors.New("plugin is not loaded")

var ErrModuleNotFound = errors.New("module is not found")
var ErrManagerDisposed = errors.New("manager is disposed")
