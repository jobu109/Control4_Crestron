using System;
namespace Control4Crestron
{
    public sealed class Control4_Light
    {
        private GatewayCore _core;
        private ushort _itemId;
        public event EventHandler<LightStatusEventArgs> FeedbackChanged;
        public void Initialize(ushort gatewayId, ushort itemId)
        {
            if (_core != null) _core.LightChanged -= OnLight;
            _itemId = itemId;
            _core = GatewayRegistry.Get(gatewayId);
            _core.LightChanged += OnLight;
            _core.RegisterLight(itemId);
            _core.PublishCurrentLight(itemId);
        }
        public void SetLevel(ushort level) { if (_core != null) _core.SetLightLevel(_itemId, level); }
        public void On() { SetLevel(100); }
        public void Off() { SetLevel(0); }
        public void Query() { if (_core != null) _core.QueryLight(_itemId); }
        private void OnLight(object sender, LightStatusEventArgs e)
        {
            if (e.ItemId != _itemId) return;
            var h = FeedbackChanged;
            if (h != null) h(this, e);
        }
    }
}
