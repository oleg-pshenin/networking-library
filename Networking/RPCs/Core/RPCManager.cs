using System;
using System.Collections.Generic;
using Networking.Broadcasting;
using Networking.Connections;
using Networking.Utils;

namespace Networking.RPCs.Core
{
    public class RPCManager : IRPCManager, IMainThreadUpdateable
    {
        internal static bool LogCalls;
        internal static bool LogInteractions;

        private readonly Dictionary<Connection, Dictionary<int, RPC>> _waitingForResponseRPC = new();

        private readonly Dictionary<Type, (object listener, Func<Connection, RPCRequest, RPCResponse> callback)> _requestHandlers = new();

        private readonly Dictionary<Type, (object listener, Action<Connection, RPCRequest, Action<RPCResponse>> callback)> _asyncRequestHandlers = new();

        private readonly List<Type> _requestTypesToUnHandle = new();

        private readonly IDataBroadcaster _dataBroadcaster;
        private readonly IConnectionManager _connectionManager;
        private int _rpcId;

        internal RPCManager(IConnectionManager connectionManager, IDataBroadcaster dataBroadcaster)
        {
            _dataBroadcaster = dataBroadcaster;
            _dataBroadcaster.ListenForReceive<RPCRequestData>(this, RPCRequestDataHandler);
            _dataBroadcaster.ListenForReceive<RPCResponseData>(this, RPCResponseDataHandler);


            _connectionManager = connectionManager;
            _connectionManager.ConnectionAdded += ConnectionAddedHandler;
            _connectionManager.ConnectionRemoved += ConnectionRemovedHandler;
        }

        private void ConnectionAddedHandler(Connection connection)
        {
            _waitingForResponseRPC.Add(connection, new Dictionary<int, RPC>());
        }

        private void ConnectionRemovedHandler(Connection connection)
        {
            foreach (var rpc in _waitingForResponseRPC[connection].Values)
            {
                rpc.SetAsFailed();
            }

            _waitingForResponseRPC[connection].Clear();
            _waitingForResponseRPC.Remove(connection);
        }

        /// <summary>
        /// Call RPC means send request data and get the callback with response or failed result
        /// </summary>
        /// <param name="connection">Connection of rpc target (the one who should be subscribed and respond)</param>
        /// <param name="request">RPC request data</param>
        /// <param name="responseCallback">
        /// Delayed callback when the one will receive request data and will answer back. It is not
        /// called if RPC is failed by timeout or any other reason
        /// </param>
        /// <param name="failedCallback">Called when RPC is failed (timeout or any other reason)</param>
        /// <typeparam name="TRequest">RPCType.Request</typeparam>
        /// <typeparam name="TResponse">RPCType.Response</typeparam>
        /// <returns>
        /// Return RPC - rpc state type which grants different type of configuration RPC call or allows to access state,
        /// request and response data after the call itself
        /// </returns>
        public RPC CallRPC<TRequest, TResponse>(Connection connection, TRequest request, Action<TResponse> responseCallback, Action failedCallback)
            where TRequest : RPCRequest where TResponse : RPCResponse
        {
            void ResponseCallback(RPCResponse response)
            {
                responseCallback?.Invoke(response as TResponse);
            }

            if (request == null)
            {
                Logger.LogError($"Can't send RPC with null RPC.Request");
                return null;
            }

            var rpc = new RPC(request, ResponseCallback, failedCallback);

            SendRPCRequestData(connection, rpc);

            return rpc;
        }

        private void SendRPCRequestData(Connection connection, RPC rpc)
        {
            _rpcId++;

            var requestData = new RPCRequestData()
            {
                CallId = _rpcId,
                RPCRequest = (rpc as IRPCInternal).Call(),
            };

            if (LogCalls)
                Logger.Log($"Sending RPC: {rpc} to: {connection}");

            if (!_waitingForResponseRPC.ContainsKey(connection))
            {
                Logger.LogError($"Unhandled connection by RPCManager: {connection}");
                _waitingForResponseRPC[connection] = new Dictionary<int, RPC>();
            }

            _waitingForResponseRPC[connection][_rpcId] = rpc;

            _dataBroadcaster.AddDataToSend(connection, requestData, BroadcastingChannel.Reliable);
        }

        /// <summary>
        /// Method allows to subscribe for request with requirement of generating response.
        /// Should be called once at initialization state. Only one listener can handle certain request type, no multiple
        /// listeners for request allowed.
        /// If sniffing for requests is needed, call  _dataBroadcaster.ListenForReceive RPCRequestData () and work with
        /// RPCRequestData.RPCRequest
        /// </summary>
        /// <param name="listener">The subscriber, that's the only way to handle unsubscribing of delegates</param>
        /// <param name="callback">Func callback, which gives request, connection of request and waits for response synchronously</param>
        /// <typeparam name="TRequest">RPCType.Request</typeparam>
        /// <typeparam name="TResponse">RPCType.Response</typeparam>
        public void HandleRPCSync<TRequest, TResponse>(object listener, Func<Connection, TRequest, TResponse> callback) where TRequest : RPCRequest where TResponse : RPCResponse
        {
            if (LogInteractions)
                Logger.Log($"RPC of type: {typeof(TRequest)}, {typeof(TResponse)} is asked to be handled by {listener}");

            var requestType = typeof(TRequest);
            if (_requestHandlers.ContainsKey(requestType))
            {
                var requestHandler = _requestHandlers[requestType];
                if (requestHandler.listener != null)
                {
                    Logger.LogError($"Attempt to override rpc request handler of type: {typeof(TRequest)} by {listener}");
                    return;
                }
                else
                {
                    Logger.LogError($"Null listener for rpc request handler of type: {typeof(TRequest)}, probably it was incorrectly unlistened");
                    _requestHandlers.Remove(requestType);
                }
            }

            _requestHandlers.Add(requestType, (listener, (connection, request) => callback?.Invoke(connection, request as TRequest)));
        }


        public void HandleRPCAsync<TRequest, TResponse>(object listener, Action<Connection, TRequest, Action<TResponse>> callback) where TRequest : RPCRequest where TResponse : RPCResponse
        {
            if (LogInteractions)
                Logger.Log($"RPC of type: {typeof(TRequest)}, {typeof(TResponse)} is asked to be handled async by {listener}");

            var requestType = typeof(TRequest);
            if (_asyncRequestHandlers.ContainsKey(requestType))
            {
                var requestHandler = _asyncRequestHandlers[requestType];
                if (requestHandler.listener != null)
                {
                    Logger.LogError($"Attempt to override rpc request handler async of type: {typeof(TRequest)} by {listener}");
                    return;
                }
                else
                {
                    Logger.LogError($"Null listener for rpc request handler async of type: {typeof(TRequest)}, probably it was incorrectly unlistened");
                    _asyncRequestHandlers.Remove(requestType);
                }
            }

            _asyncRequestHandlers.Add(requestType, (listener, (connection, request, responseCallback) => callback?.Invoke(connection, request as TRequest, responseCallback)));
        }

        /// <summary>
        /// Allows unsubscribing from all HandleRPC by providing own reference (listener from HandleRPC)
        /// </summary>
        /// <param name="listener">The subscriber</param>
        public void Unhandle(object listener)
        {
            if (LogInteractions)
                Logger.Log($"RPCs of all types are asked not to be handled by {listener}");

            _requestTypesToUnHandle.Clear();

            foreach (var requestType in _requestHandlers.Keys)
            {
                if (_requestHandlers[requestType].listener == listener)
                    _requestTypesToUnHandle.Add(requestType);
            }

            foreach (var requestType in _requestTypesToUnHandle)
            {
                _requestHandlers.Remove(requestType);
            }

            _requestTypesToUnHandle.Clear();

            foreach (var requestType in _asyncRequestHandlers.Keys)
            {
                if (_asyncRequestHandlers[requestType].listener == listener)
                    _requestTypesToUnHandle.Add(requestType);
            }

            foreach (var requestType in _requestTypesToUnHandle)
            {
                _asyncRequestHandlers.Remove(requestType);
            }

            _requestTypesToUnHandle.Clear();
        }

        public void MainThreadUpdate()
        {
        }

        private void RPCRequestDataHandler(Connection connection, RPCRequestData requestData)
        {
            if (LogCalls)
                Logger.Log($"Received RPC.Request: {requestData}");

            if (requestData.RPCRequest == null)
            {
                Logger.LogError($"Received null RPC.Request: {requestData}");

                var responseData = new RPCResponseData()
                {
                    CallId = requestData.CallId,
                    RPCResponse = null,
                };

                _dataBroadcaster.AddDataToSend(connection, responseData, BroadcastingChannel.Reliable);
                return;
            }

            var requestType = requestData.RPCRequest.GetType();
            
            // going down by types to the one directly inherited from RPCRequest
            while (requestType != null && requestType != typeof(object) && requestType.BaseType != typeof(RPCRequest))
            {
                requestType = requestType.BaseType;
            }

            if (requestType == null || requestType == typeof(object))
            {
                Logger.LogError($"Missing handler for rpc request of type: {requestData.RPCRequest.GetType()}");
                return;
            }
            
            if (_requestHandlers.ContainsKey(requestType))
            {
                var handler = _requestHandlers[requestType];
                var response = handler.callback?.Invoke(connection, requestData.RPCRequest);
                if (response == null)
                {
                    Logger.LogError($"RPC.Response is null for RPC.Request: {requestData}, that should not be the case");
                }

                var responseData = new RPCResponseData()
                {
                    CallId = requestData.CallId,
                    RPCResponse = response,
                };

                if (LogCalls)
                    Logger.Log($"Sending synchronously RPC.Response: {responseData}");

                _dataBroadcaster.AddDataToSend(connection, responseData, BroadcastingChannel.Reliable);
            }
            else if (_asyncRequestHandlers.ContainsKey(requestType))
            {
                var handler = _asyncRequestHandlers[requestType];

                handler.callback?.Invoke(connection, requestData.RPCRequest, (response) =>
                {
                    var responseData = new RPCResponseData()
                    {
                        CallId = requestData.CallId,
                        RPCResponse = response,
                    };

                    if (LogCalls)
                        Logger.Log($"Sending asynchronously RPC.Response: {responseData}");

                    _dataBroadcaster.AddDataToSend(connection, responseData, BroadcastingChannel.Reliable);
                });
            }
            else
            {
                Logger.LogError($"Missing handler for rpc request of type: {requestData.RPCRequest.GetType()}");
            }
        }

        private void RPCResponseDataHandler(Connection connection, RPCResponseData responseData)
        {
            if (LogCalls)
                Logger.Log($"Received RPC.Response: {responseData}");

            if (!_waitingForResponseRPC.ContainsKey(connection))
            {
                Logger.LogError($"Unhandled connection by RPCManager: {connection}");
                return;
            }

            if (_waitingForResponseRPC[connection].ContainsKey(responseData.CallId))
            {
                var rpc = _waitingForResponseRPC[connection][responseData.CallId];
                rpc.SetResponse(responseData.RPCResponse);

                _waitingForResponseRPC[connection].Remove(responseData.CallId);
            }
            else
            {
                Logger.LogError($"Received rpc response even though it was not waiting for: {responseData.CallId}, {responseData.RPCResponse.GetType()}");
            }
        }
    }
}