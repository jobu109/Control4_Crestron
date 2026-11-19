using System;

namespace Control4Crestron
{
    public sealed class Control4_Room_Transport
    {
        private GatewayCore _core;
        private ushort _roomId;
        private bool _registered;
        public event EventHandler<GatewayMessageEventArgs> DiagnosticReceived;
        public event EventHandler<GatewayMessageEventArgs> LocalActionRequested;
        public event EventHandler<RoomTransportEventArgs> CapabilitiesChanged;
        internal void Release()
        {
            if (_core == null) return;
            _core.MessageReceived -= OnDiagnostic;
            _core.RoomTransportChanged -= OnCapabilities;
            if (_registered) _core.UnregisterTransportRoom(_roomId);
            _registered = false;
            _core = null;
        }

        public ushort Initialize(ushort gatewayId, ushort roomId)
        {
            Release();
            _core = GatewayRegistry.Get(gatewayId); _roomId = roomId; _registered = false;
            _core.MessageReceived += OnDiagnostic; _core.RoomTransportChanged += OnCapabilities;
            // Flag first so the registration's initial unavailable status is forwarded.
            _registered = roomId != 0;
            _registered = _core.RegisterTransportRoom(roomId);
            return (ushort)(_registered ? 1 : 0);
        }
        public void DiscoverCommands() { if (_registered) _core.DiscoverTransportCommands(_roomId); }
        public void Press(string key)
        {
            if (!_registered) return;
            if (Array.IndexOf(GatewayCore.Sr260Keys, key) < 0) { OnDiagnostic(this, new GatewayMessageEventArgs { Message = "Unknown SR-260 key" }); return; }
            if (key == "Watch" || key == "Listen" || key == "List")
            {
                var local = LocalActionRequested;
                if (local != null) local(this, new GatewayMessageEventArgs { Message = key });
                return;
            }
            _core.PressTransportKey(_roomId, key);
        }
        private void OnDiagnostic(object sender, GatewayMessageEventArgs args)
        { var h = DiagnosticReceived; if (h != null) h(this, args); }
        private void OnCapabilities(object sender, RoomTransportEventArgs args)
        { if (_registered && args.RoomId == _roomId) {
            var h = CapabilitiesChanged;
            if (h != null) h(this, new RoomTransportEventArgs { RoomId = args.RoomId, DiscoveryValid = args.DiscoveryValid,
                CommandCount = args.CommandCount, MappedCount = args.MappedCount, PlayAvailable = args.PlayAvailable,
                PauseAvailable = args.PauseAvailable, StopAvailable = args.StopAvailable,
                Json = UiSerialFrames.CompactKeys(args.Json, _roomId) });
        } }
    }
}
