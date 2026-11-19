using System;
using System.Collections.Generic;
namespace Control4Crestron
{
    // One gateway instance per gateway ID, regardless of the number of SIMPL modules.
    public static class GatewayRegistry
    {
        private static readonly object Gate = new object();
        private static readonly Dictionary<ushort, GatewayCore> Map = new Dictionary<ushort, GatewayCore>();
        public static GatewayCore Get(ushort instanceId)
        {
            lock (Gate)
            {
                GatewayCore gateway;
                if (!Map.TryGetValue(instanceId, out gateway))
                {
                    gateway = new GatewayCore(instanceId);
                    Map.Add(instanceId, gateway);
                }
                return gateway;
            }
        }
    }
}
