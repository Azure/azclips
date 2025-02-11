package shared

import (
	"context"

	"github.com/Azure/azure-cli-lite/shared/proto"
	plugin "github.com/hashicorp/go-plugin"
	"google.golang.org/grpc"
)

// GRPCClient is an implementation of KV that talks over RPC.
type GRPCClient struct {
	broker *plugin.GRPCBroker
	client proto.ModuleClient
}

func createAzContextServer(broker *plugin.GRPCBroker, azCtx AzContext) (uint32, func()) {
	server := &GRPCAzContextServer{Impl: azCtx}
	var s *grpc.Server
	serverFunc := func(opts []grpc.ServerOption) *grpc.Server {
		s = grpc.NewServer(opts...)
		proto.RegisterAzContextServer(s, server)
		return s
	}

	azCtxServer := broker.NextId()
	go broker.AcceptAndServe(azCtxServer, serverFunc)
	return azCtxServer, func() {
		s.Stop()
	}
}

func (m *GRPCClient) Metadata(azCtx AzContext) (*proto.ModuleMetadata, error) {
	azCtxServer, stop := createAzContextServer(m.broker, azCtx)
	defer stop()

	r, err := m.client.Metadata(context.Background(), &proto.MetadataRequest{
		AzCtxServer: azCtxServer,
	})
	return r.Metadata, err
}

func (m *GRPCClient) Check(azCtx AzContext, uri CMDUri) (*CMDUri, error) {
	azCtxServer, stop := createAzContextServer(m.broker, azCtx)
	defer stop()

	r, err := m.client.Check(context.Background(), &proto.CheckRequest{
		AzCtxServer: azCtxServer,
		Uri:         uri.String(),
	})
	if err != nil {
		return nil, err
	}
	if len(r.Uri) == 0 {
		// if the command is not exists, the r.Uri is empty string and NewCMDUri will return nil
		return nil, nil
	}
	u, err := ParseCMDUri(r.Uri)
	if err != nil {
		return nil, err
	}
	return u, nil
}

func (m *GRPCClient) Interface(azCtx AzContext, uri CMDUri) (*proto.CMDInterface, error) {
	azCtxServer, stop := createAzContextServer(m.broker, azCtx)
	defer stop()

	r, err := m.client.Interface(context.Background(), &proto.InterfaceRequest{
		AzCtxServer: azCtxServer,
		Uri:         uri.String(),
	})
	if err != nil {
		return nil, err
	}
	return r.Interface, nil
}

// Here is the gRPC server that GRPCClient talks to.
type GRPCServer struct {
	// This is the real implementation of the module
	Impl Module

	broker *plugin.GRPCBroker
}

func (m *GRPCServer) Metadata(ctx context.Context, req *proto.MetadataRequest) (*proto.MetadataResponse, error) {
	conn, err := m.broker.Dial(req.AzCtxServer)
	if err != nil {
		return nil, err
	}
	defer conn.Close()
	azCtx := &GRPCAzContextClient{client: proto.NewAzContextClient(conn)}

	r, err := m.Impl.Metadata(azCtx)
	if err != nil {
		return nil, err
	}
	return &proto.MetadataResponse{
		Metadata: r,
	}, nil
}

func (m *GRPCServer) Check(ctx context.Context, req *proto.CheckRequest) (*proto.CheckResponse, error) {
	conn, err := m.broker.Dial(req.AzCtxServer)
	if err != nil {
		return nil, err
	}
	defer conn.Close()
	azCtx := &GRPCAzContextClient{client: proto.NewAzContextClient(conn)}

	// Parse the request URI
	uri, err := ParseCMDUri(req.Uri)
	if err != nil {
		// failed to parse the request URI
		return nil, err
	}
	r, err := m.Impl.Check(azCtx, *uri)
	if err != nil {
		return nil, err
	}
	if r == nil {
		// The command is not exists
		return &proto.CheckResponse{
			Uri: "",
		}, nil
	}
	return &proto.CheckResponse{
		Uri: r.String(),
	}, nil
}

func (m *GRPCServer) Interface(ctx context.Context, req *proto.InterfaceRequest) (*proto.InterfaceResponse, error) {
	conn, err := m.broker.Dial(req.AzCtxServer)
	if err != nil {
		return nil, err
	}
	defer conn.Close()
	azCtx := &GRPCAzContextClient{client: proto.NewAzContextClient(conn)}

	// Parse the request URI
	uri, err := ParseCMDUri(req.Uri)
	if err != nil {
		// failed to parse the request URI
		return nil, err
	}

	r, err := m.Impl.Interface(azCtx, *uri)
	if err != nil {
		return nil, err
	}
	return &proto.InterfaceResponse{
		Interface: r,
	}, nil
}

type GRPCAzContextClient struct {
	client proto.AzContextClient
}

func (m *GRPCAzContextClient) GetCoreInfo() (*proto.CoreInfo, error) {
	req := &proto.GetCoreInfoRequest{}
	r, err := m.client.GetCoreInfo(context.Background(), req)
	if err != nil {
		return nil, err
	}
	return r.CoreInfo, nil
}

type GRPCAzContextServer struct {
	// This is the real implementation
	Impl AzContext
}

func (m *GRPCAzContextServer) GetCoreInfo(ctx context.Context, req *proto.GetCoreInfoRequest) (*proto.GetCoreInfoResponse, error) {
	r, err := m.Impl.GetCoreInfo()
	if err != nil {
		return nil, err
	}
	return &proto.GetCoreInfoResponse{
		CoreInfo: r,
	}, nil
}
