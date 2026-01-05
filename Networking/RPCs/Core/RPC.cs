using System;
using Networking.Utils;

namespace Networking.RPCs.Core
{
    public enum RPCResponseTimeType
    {
        InstantSync,
        UserInputBased
    }

    public class RPC : IRPCInternal
    {
        public RPCState State { get; private set; } = RPCState.None;
        private RPCRequest _request;
        private RPCResponse _response;
        private Action<RPCResponse> _responseCallback;
        private Action _failedCallback;

        public bool IsFailed => State == RPCState.Failed;
        public bool IsFinished => State == RPCState.Responded || State == RPCState.Failed;

        public RPC()
        {
        }

        public RPC(RPCRequest request)
        {
            SetRequest(request);
        }

        RPCRequest IRPCInternal.Call()
        {
            if (State == RPCState.Initialized)
            {
                if (_responseCallback == null)
                    Logger.LogWarning($"Calling RPC has null response callback");

                if (_failedCallback == null)
                    Logger.LogWarning($"Calling RPC has null failed callback");

                State = RPCState.WaitingForResponse;
                return _request;
            }
            else
            {
                Logger.LogError($"Can't call RPC at state: {State}");
                return null;
            }
        }

        public RPC(RPCRequest request, Action<RPCResponse> responseCallback, Action failedCallback)
        {
            SetRequest(request);
            SetResponseCallback(responseCallback);
            SetFailedCallback(failedCallback);
        }

        public void SetRequest(RPCRequest request)
        {
            if (State == RPCState.None || State == RPCState.Initialized)
            {
                if (_request != null)
                    Logger.LogWarning($"Overriding not null request data of RPC: {request.GetType()}");

                _request = request;
                State = RPCState.Initialized;
            }
            else
            {
                Logger.LogError($"Can't change request data of RPC at state: {State}");
            }
        }

        public void SetResponseCallback(Action<RPCResponse> responseCallback)
        {
            if (State == RPCState.None || State == RPCState.Initialized || State == RPCState.WaitingForResponse)
            {
                if (_responseCallback != null)
                    Logger.LogWarning($"Overriding not null response callback of RPC");

                _responseCallback = responseCallback;
            }
            else
            {
                Logger.LogError($"Can't change failed callback of RPC at state: {State}");
            }
        }

        public void SetFailedCallback(Action failedCallback)
        {
            if (State == RPCState.None || State == RPCState.Initialized || State == RPCState.WaitingForResponse)
            {
                if (_failedCallback != null)
                    Logger.LogWarning($"Overriding not null failed callback of RPC");

                _failedCallback = failedCallback;
            }
            else
            {
                Logger.LogError($"Can't change response callback of RPC at state: {State}");
            }
        }

        public void SetResponse(RPCResponse response)
        {
            if (State == RPCState.WaitingForResponse)
            {
                if (response == null)
                {
                    SetAsFailed();
                }
                else
                {
                    _response = response;
                    State = RPCState.Responded;
                    _responseCallback?.Invoke(response);
                }
            }
            else
            {
                Logger.LogError($"Can't call set response at state: {State}");
            }
        }

        public RPCResponse GetResponse()
        {
            if (State == RPCState.Responded)
            {
                return _response;
            }
            else
            {
                Logger.LogError($"Can't get RPC response at state: {State}");
                return null;
            }
        }

        public void SetAsFailed()
        {
            if (State == RPCState.WaitingForResponse)
            {
                Logger.LogError($"RPC failed: {_request.GetType()}");
                State = RPCState.Failed;
                _failedCallback?.Invoke();
            }
            else
            {
                Logger.LogError($"Can't call set failed at state: {State}");
            }
        }

        public override string ToString()
        {
            return $"RPC request: {_request}, response: {_response}, state: {State}";
        }
    }
}