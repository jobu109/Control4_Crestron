using System;
namespace Control4Crestron
{
    // Shared gateway shade adapter; no independent connection or token.
    public sealed class Control4_Shade
    {
        private ushort _itemId;
        private GatewayCore _core;
        public event EventHandler<ShadeStatusEventArgs> FeedbackChanged;
        public event EventHandler<GatewayMessageEventArgs> DiagnosticReceived;
        public void Initialize(ushort gatewayId, ushort itemId)
        {
            if (_core != null)
            {
                _core.ShadeChanged -= OnShade;
                _core.MessageReceived -= OnMessage;
                _core.UnregisterShade(_itemId);
            }
            _itemId = itemId;
            _core = GatewayRegistry.Get(gatewayId);
            _core.ShadeChanged += OnShade;
            _core.MessageReceived += OnMessage;
            _core.PublishCurrentShade(itemId);
            _core.RegisterShade(itemId);
        }
        public void Query() { if (_core != null) _core.QueryShade(_itemId); }
        public void Open() { if (_core != null) _core.SendShadeCommand(_itemId, "Open", 0); }
        public void Close() { if (_core != null) _core.SendShadeCommand(_itemId, "Close", 0); }
        public void Stop() { if (_core != null) _core.SendShadeCommand(_itemId, "Stop", 0); }
        public void SetLevel(ushort level) { if (_core != null) _core.SendShadeCommand(_itemId, "SetLevel", level); }
        private void OnMessage(object sender, GatewayMessageEventArgs args)
        {
            var handler = DiagnosticReceived;
            if (handler != null) handler(this, args);
        }
        private void OnShade(object sender, ShadeStatusEventArgs args)
        {
            if (args.ItemId != _itemId) return;
            var handler = FeedbackChanged;
            if (handler != null) handler(this, args);
        }
    }
}
