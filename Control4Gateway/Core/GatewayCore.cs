using System;
using System.Collections.Generic;
using System.IO;
using System.Globalization;
using System.Net;
using System.Net.Security;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using Crestron.SimplSharp;
using Newtonsoft.Json.Linq;

namespace Control4Crestron
{
    // Single REST transport and token store. No traffic or threads in the constructor.
    public sealed partial class GatewayCore
    {
        private const string DiagnosticRevision = "Stage1-CP29a-TuneInCommandInspection";
        public event EventHandler<GatewayStatusEventArgs> StatusChanged;
        public event EventHandler<LightStatusEventArgs> LightChanged;
        public event EventHandler<GatewayMessageEventArgs> MessageReceived;
        private readonly object _sync = new object();
        private readonly object _authSync = new object();
        private readonly ushort _instance;
        private readonly Dictionary<ushort, LightSnapshot> _lights = new Dictionary<ushort, LightSnapshot>();
        private string _host = "";
        private string _token = "";
        private string _username = "";
        private string _password = "";
        private DateTime _tokenRefreshUtc = DateTime.MinValue;
        private int _busy;
        private bool _online;
        private int _generation;
        private sealed class LightSnapshot { public ushort Level; public ushort On; public bool Measured; public ushort Dimming; }
        public GatewayCore(ushort instanceId) { _instance = instanceId; }
        public ushort InstanceId { get { return _instance; } }
        public void Configure(string host)
        {
            if (string.IsNullOrWhiteSpace(host)) { EmitMessage(InitializationMessage("Host is blank")); return; }
            // Deliberately accept only host/IP, not a URL, preventing arbitrary URL injection.
            System.Net.IPAddress addr;
            if (!System.Net.IPAddress.TryParse(host.Trim(), out addr))
            { EmitMessage(InitializationMessage("Use the controller IPv4 address")); return; }
            lock (_sync) { _host = addr.ToString(); }
            EmitMessage(InitializationMessage("Host configured"));
        }
        private string InitializationMessage(string result)
        {
            string version = "unknown";
            try
            {
                Version assemblyVersion = typeof(GatewayCore).Assembly.GetName().Version;
                if (assemblyVersion != null) version = assemblyVersion.ToString();
            }
            catch (Exception) { /* Diagnostics must not prevent SIMPL+ initialization. */ }
            return "Gateway " + _instance + " initialized; Control4Gateway " + DiagnosticRevision + " (assembly " + version + "); " + result;
        }
        // Credentials and tokens are held in memory only; never echo or persist them.
        public void SetAccountCredentials(string username, string password)
        {
            string user = username == null ? "" : username.Trim();
            string pass = password ?? "";
            lock (_sync)
            {
                if (_username == user && _password == pass) return;
                _username = user;
                _password = pass;
                _token = "";
                _tokenRefreshUtc = DateTime.MinValue;
                ++_generation;
            }
            SetOnline(false);
            StopRealtime();
            EmitMessage(user.Length > 0 && pass.Length > 0
                ? "Account credentials configured (values hidden)"
                : "Account credentials incomplete");
        }
        public void Authenticate()
        {
            StopRealtime();
            int gen;
            lock (_sync) { gen = ++_generation; }
            Queue(delegate
            {
                try
                {
                    string token = EnsureDirectorToken(gen);
                    if (token != null && CurrentGeneration(gen))
                        EmitMessage("Director token ready (value hidden)");
                }
                catch (Exception ex)
                {
                    if (CurrentGeneration(gen)) EmitMessage("Authentication failed: " + FailureKind(ex));
                }
            });
        }
        public void Connect()
        {
            StopRealtime();
            string host;
            int gen;
            lock (_sync) { host = _host; gen = ++_generation; }
            if (string.IsNullOrEmpty(host))
            { EmitMessage("Configure IP before Connect"); SetOnline(false); return; }
            Queue(delegate
            {
                try
                {
                    string token = EnsureDirectorToken(gen);
                    if (token == null || !CurrentGeneration(gen)) return;
                    EmitMessage("Director token ready (value hidden)");
                    string inventory = Request(host, token, "GET", "/api/v1/items", null);
                    if (CurrentGeneration(gen))
                    {
                        CachePanelRoomNames(inventory, gen);
                        SetOnline(true);
                        EmitMessage("REST connected; starting WebSocket (panel feedback build)");
                        StartRealtime(host, gen);
                        DiscoverAuthenticatedRoomDevices(gen, host, token, inventory);
                    }
                }
                catch (Exception ex) { if (CurrentGeneration(gen)) { InvalidateRejectedToken(ex); SetOnline(false); EmitMessage("Connect failed: " + FailureKind(ex)); } }
            });
        }
        public void Disconnect()
        {
            lock (_sync) { ++_generation; }
            StopRealtime();
            SetOnline(false);
            EmitMessage("Disconnected");
        }
        public void SetLightLevel(ushort itemId, ushort level)
        { SetLightLevelInternal(itemId, level, false); }

        internal void SetPanelLightLevel(ushort itemId, ushort level)
        { SetLightLevelInternal(itemId, level, true); }

        private void SetLightLevelInternal(ushort itemId, ushort level, bool readBack)
        {
            if (itemId == 0 || level > 100) { EmitMessage("Invalid light ID or level"); return; }
            int generation;
            lock (_sync) { generation = _generation; }
            Queue(delegate
            {
                try
                {
                    string host, token;
                    if (!GetConnection(out host, out token)) { EmitMessage("Gateway offline"); return; }
                    if (!CurrentGeneration(generation)) return;
                    string body = "{\"async\":true,\"command\":\"SET_LEVEL\",\"tParams\":{\"LEVEL\":" + level + "}}";
                    Request(host, token, "POST", "/api/v1/items/" + itemId + "/commands", body);
                    EmitMessage("Light " + itemId + " SET_LEVEL accepted");
                    if (readBack)
                    {
                        Thread.Sleep(400);
                        if (CurrentGeneration(generation)) ReadLight(itemId, generation);
                    }
                }
                catch (Exception ex) { InvalidateRejectedToken(ex); EmitMessage("Set level failed: " + FailureKind(ex)); }
            });
        }
        public void QueryLight(ushort itemId)
        {
            if (itemId == 0) { EmitMessage("Invalid light ID"); return; }
            int gen;
            lock (_sync) { gen = _generation; }
            Queue(delegate { ReadLight(itemId, gen); });
        }

        internal void QueryPanelLights(ushort room, System.Collections.Generic.List<ushort> ids)
        {
            EmitMessage("Panel room " + room + " initial light reads: " + ids.Count + " [" + String.Join(",", ids) + "]");
            if (ids.Count == 0) return;
            int generation;
            lock (_sync) { generation = _generation; }
            Queue(delegate
            {
                foreach (ushort id in ids)
                {
                    bool cached;
                    lock (_sync)
                    {
                        if (_generation != generation || !_online) return;
                        if (!_panelLightReferences.ContainsKey(id)) continue;
                        cached = HasLightCapability(id);
                    }
                    if (cached) PublishCurrentLight(id);
                    else ReadLight(id, generation);
                }
            });
        }

        private void ReadLight(ushort itemId, int gen)
        {
            try
            {
                string host, token;
                if (!GetConnection(out host, out token)) { EmitMessage("Gateway offline"); return; }
                if (!CurrentGeneration(gen)) return;
                int feedbackEpoch, eventVersion;
                lock (_sync) { feedbackEpoch = _feedbackEpoch; _lightEventVersions.TryGetValue(itemId, out eventVersion); }
                string s = ReadWithTimeoutRetry(host, token, "/api/v1/items/" + itemId + "/variables?varnames=LIGHT_LEVEL", gen);
                ushort level;
                bool switchState = !TryParseLightLevel(s, out level);
                if (switchState)
                {
                    if (!CurrentGeneration(gen)) return;
                    string state = ReadWithTimeoutRetry(host, token, "/api/v1/items/" + itemId + "/variables?varnames=LIGHT_STATE", gen);
                    if (!TryParseLightState(state, out level))
                    {
                        EmitMessage("Light " + itemId + " returned no usable level or switch state");
                        return;
                    }
                }
                lock (_sync)
                {
                    if (_generation != gen || !_online) return;
                    int currentVersion;
                    _lightEventVersions.TryGetValue(itemId, out currentVersion);
                    if (_feedbackEpoch != feedbackEpoch) return;
                    ushort dimming = switchState ? (ushort)1 : (ushort)2;
                    LightSnapshot previous;
                    if (_lights.TryGetValue(itemId, out previous))
                    {
                        if (switchState && previous.Dimming == 2) dimming = 2;
                        if (currentVersion != eventVersion || switchState && previous.Measured)
                        {
                            if (previous.Dimming != dimming) StoreLightFeedback(itemId, previous.Level, previous.Measured, dimming);
                            return;
                        }
                    }
                    if (currentVersion != eventVersion) return;
                    StoreLightFeedback(itemId, level, !switchState, dimming);
                }
                EmitMessage("Light " + itemId + (switchState ? (level == 0 ? " Off" : " On") + " (LIGHT_STATE readback)" : " level " + level + "% (REST readback)"));
            }
            catch (Exception ex) { InvalidateRejectedToken(ex); EmitMessage("Light " + itemId + " readback failed: " + FailureKind(ex)); }
        }
        private static bool TryParseLightState(string json, out ushort level)
        {
            level = 0;
            if (String.IsNullOrEmpty(json) || json.Length > 16384) return false;
            try
            {
                var values = JToken.Parse(json) as JArray;
                if (values == null || values.Count != 1 || values[0].Type != JTokenType.Object) return false;
                var value = values[0]["value"];
                if (value == null) return false;
                bool on;
                if (value.Type == JTokenType.Boolean) on = (bool)value;
                else if (value.Type == JTokenType.Integer || value.Type == JTokenType.String)
                {
                    string text = value.ToString();
                    if (text == "1") on = true;
                    else if (text == "0") on = false;
                    else if (!Boolean.TryParse(text, out on)) return false;
                }
                else return false;
                level = on ? (ushort)100 : (ushort)0;
                return true;
            }
            catch (Newtonsoft.Json.JsonReaderException) { return false; }
        }

        // Exact response shape used by pyControl4: [{"value": <0..100>}].
        // Reject missing, undefined, fractional and out-of-range values; never log payloads.
        private static bool TryParseLightLevel(string json, out ushort level)
        {
            level = 0;
            if (string.IsNullOrEmpty(json) || json.Length > 16384) return false;
            try
            {
                JArray values = JToken.Parse(json) as JArray;
                if (values == null) return false;
                if (values.Count != 1 || values[0].Type != JTokenType.Object) return false;
                JToken value = values[0]["value"];
                if (value == null || (value.Type != JTokenType.Integer &&
                    value.Type != JTokenType.Float && value.Type != JTokenType.String)) return false;
                decimal number;
                if (!Decimal.TryParse(value.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out number)
                    || number < 0 || number > 100 || Decimal.Truncate(number) != number) return false;
                level = (ushort)number;
                return true;
            }
            catch (Newtonsoft.Json.JsonReaderException) { return false; }
        }
        // This method is the safe event ingress for later Socket.IO integration.
        public void AcceptLightEvent(ushort id, ushort level)
        {
            StoreLightFeedback(id, level, true);
        }

        // Caller holds _sync.
        private bool HasLightCapability(ushort id)
        {
            LightSnapshot snapshot;
            return _lights.TryGetValue(id, out snapshot) && snapshot.Dimming != 0;
        }

        private void StoreLightFeedback(ushort id, ushort level, bool measured, ushort dimming = 0)
        {
            if (id == 0 || level > 100) return;
            LightSnapshot snapshot;
            lock (_sync)
            {
                LightSnapshot previous;
                if (dimming == 0 && _lights.TryGetValue(id, out previous)) dimming = previous.Dimming;
                if (measured && level > 0 && level < 100) dimming = 2;
                snapshot = new LightSnapshot { Level = level, On = (ushort)(level > 0 ? 1 : 0), Measured = measured, Dimming = dimming };
                _lights[id] = snapshot;
            }
            PublishLightSnapshot(id, snapshot);
        }

        private void PublishLightSnapshot(ushort id, LightSnapshot snapshot)
        {
            var handler = LightChanged;
            if (handler != null) SafeEvent(delegate { handler(this, new LightStatusEventArgs { ItemId = id, Level = snapshot.Level, On = snapshot.On, Valid = 1, Dimming = snapshot.Dimming }); });
        }

        public void PublishCurrentLight(ushort id)
        {
            LightSnapshot snap;
            lock (_sync) { _lights.TryGetValue(id, out snap); }
            if (snap != null) PublishLightSnapshot(id, snap);
        }

        private bool GetConnection(out string host, out string token)
        {
            int gen;
            lock (_sync)
            {
                host = _host;
                gen = _generation;
                if (!_online || host.Length == 0) { token = ""; return false; }
            }
            try { token = EnsureDirectorToken(gen); }
            catch { SetOnline(false); throw; }
            return token != null && CurrentGeneration(gen);
        }
        private string EnsureDirectorToken(int gen)
        {
            lock (_authSync)
            {
                string user, pass;
                lock (_sync)
                {
                    if (_generation != gen) return null;
                    if (_token.Length > 0 && DateTime.UtcNow < _tokenRefreshUtc) return _token;
                    user = _username;
                    pass = _password;
                }
                if (user.Length == 0 || pass.Length == 0)
                    throw new InvalidOperationException("Account credentials missing");
                Control4AccountClient.DirectorAuthorization authorization =
                    Control4AccountClient.AcquireDirectorToken(user, pass);
                lock (_sync)
                {
                    if (_generation != gen) return null;
                    _token = authorization.Token;
                    _tokenRefreshUtc = DateTime.UtcNow.AddSeconds(Math.Max(1, authorization.ValidSeconds - 60));
                    return _token;
                }
            }
        }
        private static string FailureKind(Exception ex)
        {
            return FailureKind(ex, 0);
        }
        private static string FailureKind(Exception ex, int depth)
        {
            // Report types and socket/status codes only. Exception messages can contain
            // private URLs, JWT query strings, or HTTP payloads and must never be echoed.
            if (depth >= 6) return ex.GetType().Name;
            var aggregate = ex as AggregateException;
            if (aggregate != null)
            {
                var failures = aggregate.Flatten().InnerExceptions;
                if (failures.Count > 0) return "AggregateException > " + FailureKind(failures[0], depth + 1);
            }
            var socket = ex as System.Net.Sockets.SocketException;
            if (socket != null) return "SocketException " + socket.SocketErrorCode;
            var web = ex as WebException;
            if (web != null)
            {
                var response = web.Response as HttpWebResponse;
                if (response != null)
                {
                    string status = "HTTP " + (int)response.StatusCode;
                    response.Close();
                    return status;
                }
                return "WebException " + web.Status;
            }
            if (ex is InvalidOperationException && ex.Message == "Account credentials missing")
                return "account credentials missing";
            return ex.GetType().Name + (ex.InnerException == null ? "" : " > " + FailureKind(ex.InnerException, depth + 1));
        }
        private void InvalidateRejectedToken(Exception ex)
        {
            var web = ex as WebException;
            var response = web == null ? null : web.Response as HttpWebResponse;
            if (response == null ||
                (response.StatusCode != HttpStatusCode.Unauthorized && response.StatusCode != HttpStatusCode.Forbidden)) return;
            lock (_sync) { _token = ""; _tokenRefreshUtc = DateTime.MinValue; }
            SetOnline(false);
        }
        private bool CurrentGeneration(int gen) { lock (_sync) { return _generation == gen; } }
        private void SetOnline(bool state)
        {
            lock (_sync) { _online = state; }
            PublishStatus();
        }
        private void PublishStatus()
        {
            bool online;
            lock (_sync) { online = _online; }
            var h = StatusChanged;
            if (h != null) SafeEvent(delegate { h(this, new GatewayStatusEventArgs { Connected = (ushort)(online ? 1 : 0), Busy = (ushort)(System.Threading.Interlocked.CompareExchange(ref _busy, 0, 0) > 0 ? 1 : 0), Message = online ? "Connected" : "Disconnected" }); });
        }
        private void Queue(Action action)
        {
            System.Threading.Interlocked.Increment(ref _busy);
            PublishStatus();
            try { ThreadPool.QueueUserWorkItem(delegate(object state) { try { action(); } catch (Exception e) { EmitMessage("Worker failure: " + e.GetType().Name); } finally { System.Threading.Interlocked.Decrement(ref _busy); PublishStatus(); } }); }
            catch (Exception ex) { System.Threading.Interlocked.Decrement(ref _busy); PublishStatus(); EmitMessage("Queue failed: " + ex.GetType().Name); }
        }
        private static void SafeEvent(Action action)
        { try { action(); } catch (Exception ex) { ErrorLog.Error("Control4: event handler failure: {0}", ex.GetType().Name); } }
        private void EmitMessage(string msg)
        {
            var h = MessageReceived;
            if (h != null) SafeEvent(delegate { h(this, new GatewayMessageEventArgs { Message = msg }); });
        }
        private string ReadWithTimeoutRetry(string host, string token, string path, int generation)
        {
            int status;
            try { return Request(host, token, "GET", path, null, out status, true); }
            catch (WebException ex) when (ex.Status == WebExceptionStatus.Timeout)
            {
                Thread.Sleep(300);
                if (!CurrentGeneration(generation)) throw;
                return Request(host, token, "GET", path, null, out status, true);
            }
        }

        private static string Request(string host, string token, string method, string path, string body)
        {
            int status;
            return Request(host, token, method, path, body, out status);
        }
        private static string Request(string host, string token, string method, string path, string body, out int status, bool enforceDeadline = false)
        {
            status = 0;
            // User-selected local-controller trust policy. Cloud requests are separate.
            var req = (HttpWebRequest)WebRequest.Create("https://" + host + path);
            req.AllowAutoRedirect = false;
            req.ServerCertificateValidationCallback = delegate(object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors errors)
            {
                return certificate != null && String.Equals(req.RequestUri.Host, host, StringComparison.OrdinalIgnoreCase);
            };
            req.Method = method;
            req.Timeout = 6000;
            req.ReadWriteTimeout = 6000;
            req.KeepAlive = false;
            req.Headers[HttpRequestHeader.Authorization] = "Bearer " + token;
            req.Accept = "application/json";
            if (body != null)
            {
                req.ContentType = "application/json";
                byte[] bytes = Encoding.UTF8.GetBytes(body);
                req.ContentLength = bytes.Length;
                using (var stream = req.GetRequestStream()) stream.Write(bytes, 0, bytes.Length);
            }
            int expired = 0;
            using (var deadline = enforceDeadline ? new Timer(delegate
            {
                System.Threading.Interlocked.Exchange(ref expired, 1);
                req.Abort();
            }, null, 8000, System.Threading.Timeout.Infinite) : null)
            try
            {
                using (var response = (HttpWebResponse)req.GetResponse())
                using (var stream = response.GetResponseStream())
                using (var reader = new StreamReader(stream))
                {
                    status = (int)response.StatusCode;
                    return reader.ReadToEnd();
                }
            }
            catch (Exception ex) when (System.Threading.Interlocked.CompareExchange(ref expired, 0, 0) != 0 &&
                (ex is WebException || ex is IOException))
            {
                throw new WebException("Director read deadline exceeded", ex, WebExceptionStatus.Timeout, null);
            }
        }
    }
}













