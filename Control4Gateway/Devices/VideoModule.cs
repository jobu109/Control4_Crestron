using System;
namespace Control4Crestron
{
    // Shares the room registry/readback and command transport with Audio.
    public sealed class Control4_Video
    {
        private readonly Control4_Audio _room = new Control4_Audio();
        public event EventHandler<AudioStatusEventArgs> FeedbackChanged;
        public event EventHandler<GatewayMessageEventArgs> DiagnosticReceived;
        public Control4_Video()
        {
            _room.FeedbackChanged += OnFeedback;
            _room.DiagnosticReceived += OnDiagnostic;
        }
        public ushort Initialize(ushort gatewayId, ushort roomId, ushort watchFeedbackId, ushort listenFeedbackId, ushort listenOutputId)
        { return _room.Initialize(gatewayId, roomId, watchFeedbackId, listenFeedbackId, listenOutputId); }
        public void Query() { _room.Query(); }
        public void SelectSource(string sourceId) { _room.SelectVideoSource(sourceId); }
        public void RoomOff() { _room.RoomOff(); }
        private void OnFeedback(object sender, AudioStatusEventArgs args)
        { var handler = FeedbackChanged; if (handler != null) handler(this, args); }
        private void OnDiagnostic(object sender, GatewayMessageEventArgs args)
        { var handler = DiagnosticReceived; if (handler != null) handler(this, args); }
    }
}
