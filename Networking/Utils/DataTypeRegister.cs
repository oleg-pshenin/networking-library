using System;
using System.Collections.Generic;
using Networking.Data.Core;
using Networking.Entities.Core;
using Networking.RPCs.Core;
using ProtoBuf.Meta;

namespace Networking.Utils
{
    public static class DataTypeRegister
    {
        // Should be ordered from lowest in hierarchy to highest
        private static readonly List<Type> BaseTypes = new()
        {
            // rpc data types
            typeof(RPCRequest),
            typeof(RPCResponse),
            typeof(EntityInstantiateData),
            typeof(EntitySyncData),
            typeof(NetworkData)
        };

        private static Dictionary<Type, MetaType> _metaTypeInstances;
        private static Dictionary<Type, int> _metaTypeFieldOffset;
        private static RuntimeTypeModel _runtimeTypeModel;

        public static void Init()
        {
            _metaTypeInstances = new Dictionary<Type, MetaType>();
            _metaTypeFieldOffset = new Dictionary<Type, int>();
            _runtimeTypeModel = RuntimeTypeModel.Create();

            foreach (var type in BaseTypes)
            {
                if (!_metaTypeInstances.ContainsKey(type))
                {
                    _metaTypeInstances[type] = _runtimeTypeModel[type];
                    _metaTypeFieldOffset[type] = 5;
                }
            }

            _runtimeTypeModel.MakeDefault();
        }

        // For compatibility, order of calls to register should be the same between clients (so ordering should not happen at runtime)
        public static void Register(List<Type> dataTypes)
        {
            foreach (var dataType in dataTypes)
            {
                Register(dataType);
            }
        }

        public static void Register(Type dataType)
        {
            foreach (var type in BaseTypes)
            {
                if (!dataType.IsSubclassOf(type))
                    continue;

                try
                {
                    _metaTypeInstances[type].AddSubType(_metaTypeFieldOffset[type]++, dataType);
                }
                catch (Exception e)
                {
                    Logger.LogError(e.Message);
                }

                break;
            }
        }
    }
}