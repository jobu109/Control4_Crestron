using System;
namespace Control4Crestron
{
    // Room controls and measured state refreshed from Watch/Listen endpoint events.
    public sealed class Control4_Audio
    {
        private ushort _roomId;
        private GatewayCore _core;
        private bool _registered;
        public event EventHandler<AudioStatusEventArgs> FeedbackChanged;
        public event EventHandler<GatewayMessageEventArgs> DiagnosticReceived;
        public ushort Initialize(ushort gatewayId, ushort roomId, ushort watchFeedbackId, ushort listenFeedbackId, ushort listenOutputId)
        {
            if (_core != null)
            {
                _core.AudioChanged -= OnAudio;
                _core.MessageReceived -= OnMessage;
                if (_registered) _core.UnregisterAudio(_roomId);
            }
            _core = GatewayRegistry.Get(gatewayId);
            _roomId = roomId;
            _core.AudioChanged += OnAudio;
            _core.MessageReceived += OnMessage;
            _registered = false;
            _registered = _core.RegisterAudio(roomId, watchFeedbackId, listenFeedbackId, listenOutputId);
            if (_registered) _core.PublishCurrentAudio(roomId);
            return (ushort)(_registered ? 1 : 0);
        }
        public void Query() { if (_core != null && _registered) _core.QueryAudio(_roomId); }
        public void SetVolume(ushort level) { Send("SetVolume", level); }
        public void MuteOn() { Send("MuteOn", 0); }
        public void MuteOff() { Send("MuteOff", 0); }
        public void RoomOff() { Send("RoomOff", 0); }
        public void SelectAudioSource(string sourceId) { SelectSource("SelectAudioSource", sourceId); }
        public void SelectVideoSource(string sourceId) { SelectSource("SelectVideoSource", sourceId); }
        private void SelectSource(string operation, string sourceId)
        {
            int id;
            if (!Int32.TryParse(sourceId, System.Globalization.NumberStyles.None,
                System.Globalization.CultureInfo.InvariantCulture, out id) || id <= 0)
            { OnMessage(this, new GatewayMessageEventArgs { Message = "Invalid audio source ID: use a positive decimal item ID" }); return; }
            Send(operation, id);
        }
        private void Send(string operation, int value)
        { if (_core != null && _registered) _core.SendAudioCommand(_roomId, operation, value); }
        private void OnAudio(object sender, AudioStatusEventArgs args)
        {
            if (!_registered || args.RoomId != _roomId) return;
            var handler = FeedbackChanged;
            if (handler != null) handler(this, args);
        }
        private void OnMessage(object sender, GatewayMessageEventArgs args)
        {
            var handler = DiagnosticReceived;
            if (handler != null) handler(this, args);
        }
    }
}
