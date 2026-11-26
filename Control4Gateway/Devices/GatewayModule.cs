using System;
namespace Control4Crestron
{
    // SIMPL+ main gateway owns connection configuration, no network activity at construction.
    public sealed class Control4_Gateway
    {
        private GatewayCore _core;
        private string _username = "";
        private string _password = "";
        public event EventHandler<GatewayStatusEventArgs> NetworkStatusChanged;
        public event EventHandler<GatewayMessageEventArgs> DiagnosticReceived;
        public event EventHandler<GatewayMessageEventArgs> InventoryExported;
        public event EventHandler<GatewayMessageEventArgs> SetupReportExported;
        public event EventHandler<GatewayMessageEventArgs> SetupJsonExported;
        public event EventHandler<GatewayMessageEventArgs> MediaCapabilitiesExported;
        public event EventHandler<GatewayMessageEventArgs> MediaCommandsExported;
        public void Initialize(ushort gatewayId, string ipAddress)
        {
            if (_core != null)
            {
                _core.StatusChanged -= OnStatus; _core.MessageReceived -= OnMessage;
                _core.SetupReportExported -= OnSetupReport; _core.SetupJsonExported -= OnSetupJson; _core.InventoryExported -= OnInventory; _core.MediaCapabilitiesExported -= OnMediaCapabilities;
                _core.MediaCommandsExported -= OnMediaCommands;
            }
            _core = GatewayRegistry.Get(gatewayId);
            _core.StatusChanged += OnStatus;
            _core.MessageReceived += OnMessage;
            _core.SetupReportExported += OnSetupReport; _core.SetupJsonExported += OnSetupJson; _core.InventoryExported += OnInventory;
            _core.MediaCapabilitiesExported += OnMediaCapabilities;
            _core.MediaCommandsExported += OnMediaCommands;
            _core.Configure(ipAddress);
            _core.SetAccountCredentials(_username, _password);
        }
        public void SetAccountCredentials(string username, string password)
        {
            _username = username ?? "";
            _password = password ?? "";
            if (_core != null) _core.SetAccountCredentials(_username, _password);
        }
        public void Authenticate() { if (_core != null) _core.Authenticate(); }
        public void Connect() { if (_core != null) _core.Connect(); }
        public void Disconnect() { if (_core != null) _core.Disconnect(); }
        public void ExportSetupReport() { if (_core != null) _core.ExportSetupReport(); }
        public void ExportInventory() { if (_core != null) _core.ExportInventory(); }
        public void ExportMediaCapabilities() { if (_core != null) _core.ExportMediaCapabilities(); }
        public void ExportMediaCommands() { if (_core != null) _core.ExportMediaCommands(); }
        public void CaptureAudioFeedback() { if (_core != null) _core.StartAudioFeedbackCapture(); }
        private void OnMediaCommands(object sender, GatewayMessageEventArgs e) { var h = MediaCommandsExported; if (h != null) h(this,e); }
        private void OnMediaCapabilities(object sender, GatewayMessageEventArgs e) { var h = MediaCapabilitiesExported; if (h != null) h(this,e); }
        private void OnSetupReport(object sender, GatewayMessageEventArgs e) { var h = SetupReportExported; if (h != null) h(this,e); }
        private void OnSetupJson(object sender, GatewayMessageEventArgs e) { var h = SetupJsonExported; if (h != null) h(this,e); }
        private void OnInventory(object sender, GatewayMessageEventArgs e) { var h = InventoryExported; if (h != null) h(this,e); }
        private void OnStatus(object sender, GatewayStatusEventArgs e) { var h = NetworkStatusChanged; if (h != null) h(this,e); }
        private void OnMessage(object sender, GatewayMessageEventArgs e) { var h = DiagnosticReceived; if (h != null) h(this,e); }
    }
}

