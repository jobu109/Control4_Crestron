namespace Control4Crestron
{
    // Reserved device adapter. Commands are intentionally disabled until verified against this EA-5.
    public sealed class Control4_Generic
    {
        private ushort _itemId;
        private GatewayCore _core;
        public void Initialize(ushort gatewayId, ushort itemId)
        {
            _core = GatewayRegistry.Get(gatewayId);
            _itemId = itemId;
        }
    }
}
