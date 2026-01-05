using System;
using Networking.Connections;

namespace Networking.RPCs.Core
{
    public interface IRPCManager
    {
        RPC CallRPC<TRequest, TResponse>(Connection connection, TRequest request, Action<TResponse> responseCallback, Action failedCallback)
            where TRequest : RPCRequest where TResponse : RPCResponse;

        void HandleRPCSync<TRequest, TResponse>(object listener, Func<Connection, TRequest, TResponse> callback) where TRequest : RPCRequest where TResponse : RPCResponse;
        void HandleRPCAsync<TRequest, TResponse>(object listener, Action<Connection, TRequest, Action<TResponse>> callback) where TRequest : RPCRequest where TResponse : RPCResponse;
        void Unhandle(object listener);
    }
}