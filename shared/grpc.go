package shared

import (
	"context"

	"github.com/Azure/azure-cli-v3/shared/proto"
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

func (m *GRPCClient) Check(azCtx AzContext, uri CMDUri) (CMDUri, error) {
	azCtxServer, stop := createAzContextServer(m.broker, azCtx)
	defer stop()

	r, err := m.client.Check(context.Background(), &proto.CheckRequest{
		AzCtxServer: azCtxServer,
		Uri:         string(uri),
	})
	if err != nil {
		return "", err
	}
	return CMDUri(r.Uri), nil
}

func (m *GRPCClient) Interface(azCtx AzContext, uri CMDUri) (*proto.CMDInterface, error) {
	azCtxServer, stop := createAzContextServer(m.broker, azCtx)
	defer stop()

	r, err := m.client.Interface(context.Background(), &proto.InterfaceRequest{
		AzCtxServer: azCtxServer,
		Uri:         string(uri),
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

	r, err := m.Impl.Check(azCtx, CMDUri(req.Uri))
	if err != nil {
		return nil, err
	}
	return &proto.CheckResponse{
		Uri: string(r),
	}, nil
}

func (m *GRPCServer) Interface(ctx context.Context, req *proto.InterfaceRequest) (*proto.InterfaceResponse, error) {
	conn, err := m.broker.Dial(req.AzCtxServer)
	if err != nil {
		return nil, err
	}
	defer conn.Close()
	azCtx := &GRPCAzContextClient{client: proto.NewAzContextClient(conn)}

	r, err := m.Impl.Interface(azCtx, CMDUri(req.Uri))
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
