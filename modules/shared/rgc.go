package shared

import (
	"net/rpc"
)

type RPCClient struct {
	client *rpc.Client
}

func (c *RPCClient) Metadata() ModuleMetadata {
	var resp ModuleMetadata
	err := c.client.Call("Plugin.Metadata", nil, &resp)
	if err != nil {
		panic(err)
	}
	return resp
}

func (c *RPCClient) Check(uri CMDUri) CMDUri {
	var resp CMDUri
	err := c.client.Call("Plugin.Check", uri, &resp)
	if err != nil {
		panic(err)
	}
	return resp
}

type RPCServer struct {
	Impl Module
}

func (s *RPCServer) Metadata() ModuleMetadata {
	return s.Impl.Metadata()
}

func (s *RPCServer) Check(uri CMDUri) CMDUri {
	return s.Impl.Check(uri)
}
