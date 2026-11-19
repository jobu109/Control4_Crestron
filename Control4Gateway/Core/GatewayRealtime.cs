using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using Newtonsoft.Json.Linq;

namespace Control4Crestron
{
    public sealed partial class GatewayCore
    {
        private readonly HashSet<ushort> _registeredLights = new HashSet<ushort>();
        private readonly Dictionary<ushort, int> _panelLightReferences = new Dictionary<ushort, int>();

        internal bool RegisterPanelLight(ushort id)
        {
            bool cached;
            lock (_sync)
            {
                int count;
                _panelLightReferences.TryGetValue(id, out count);
                cached = _lights.ContainsKey(id);

                _panelLightReferences[id] = count + 1;
            }

            return cached;
        }

        internal void UnregisterPanelLight(ushort id)
        {
            lock (_sync)
            {
                int count;
                if (!_panelLightReferences.TryGetValue(id, out count)) return;
                if (count <= 1) _panelLightReferences.Remove(id);
                else _panelLightReferences[id] = count - 1;
            }
        }

        // Caller holds _sync.
        private ushort[] ActiveLightIds()
        {
            var ids = new HashSet<ushort>(_registeredLights);
            ids.UnionWith(_panelLightReferences.Keys);
            var result = new ushort[ids.Count];
            ids.CopyTo(result);
            return result;
        }
        private readonly Dictionary<ushort, int> _lightEventVersions = new Dictionary<ushort, int>();
        private int _feedbackEpoch;
        private RealtimeSession _realtime;

        private sealed class RealtimeSession
        {
            internal volatile bool Stopped;
            internal readonly object Sync = new object();
            internal Control4WebSocketTransport Transport;
            internal string CloseReason;
            internal void Stop()
            {
                lock (Sync)
                {
                    Stopped = true;
                    if (Transport != null) Transport.Dispose();
                }
            }
        }

        internal void RegisterLight(ushort id)
        {
            if (id == 0) return;
            bool online;
            lock (_sync) { _registeredLights.Add(id); online = _online; }
            if (online) QueryLight(id);
        }

        private void StopRealtime()
        {
            StopRoomModules();
            RealtimeSession old;
            lock (_sync) { old = _realtime; _realtime = null; }
            if (old != null) old.Stop();
            InvalidateLightFeedback();
        }

        private void StartRealtime(string host, int gen)
        {
            var session = new RealtimeSession();
            lock (_sync)
            {
                if (_generation != gen) return;
                _realtime = session;
            }
            var thread = new Thread(delegate() { RealtimeLoop(session, host, gen); });
            thread.IsBackground = true;
            thread.Start();
        }

        private void InvalidateLightFeedback()
        {
            ushort[] ids;
            lock (_sync)
            {
                StopRoomModules();
                InvalidateShadeFeedback();
                InvalidateAudioFeedback();
                ids = ActiveLightIds();
                _lights.Clear();
                ++_feedbackEpoch;
            }
            foreach (ushort id in ids)
            {
                var handler = LightChanged;
                if (handler != null) SafeEvent(delegate { handler(this, new LightStatusEventArgs { ItemId = id, Valid = 0 }); });
            }
        }

        private void RealtimeLoop(RealtimeSession session, string host, int gen)
        {
            int retrySeconds = 1;
            while (!session.Stopped && CurrentGeneration(gen))
            {
                try
                {
                    session.CloseReason = null;
                    string token = EnsureDirectorToken(gen);
                    if (token == null || session.Stopped) break;
                    using (var transport = new Control4WebSocketTransport())
                    {
                        lock (session.Sync)
                        {
                            if (session.Stopped) break;
                            session.Transport = transport;
                        }
                        transport.Connect(host, token, delegate(object sender, System.Security.Cryptography.X509Certificates.X509Certificate cert,
                            System.Security.Cryptography.X509Certificates.X509Chain chain, System.Net.Security.SslPolicyErrors errors)
                            { return cert != null; });
                        RunSubscription(session, transport, host, token, gen, ref retrySeconds);
                    }
                }
                catch (Exception ex)
                {
                    if (!session.Stopped && CurrentGeneration(gen))
                    {
                        InvalidateRejectedToken(ex);
                        InvalidateLightFeedback();
                        string stage = session.Transport == null ? "token acquisition" : session.Transport.Stage;
                        EmitMessage("WebSocket " + stage + " failed: " + (session.CloseReason ?? FailureKind(ex)) + "; retry in " + retrySeconds + "s");
                    }
                }
                finally { lock (session.Sync) { session.Transport = null; } }
                // Short waits let Disconnect interrupt backoff without blocking SIMPL+.
                for (int i = 0; i < retrySeconds * 10 && !session.Stopped && CurrentGeneration(gen); i++) Thread.Sleep(100);
                retrySeconds = Math.Min(30, retrySeconds * 2);
            }
        }

        private void RunSubscription(RealtimeSession session, Control4WebSocketTransport transport, string host, string token, int gen, ref int retrySeconds)
        {
            string opening = transport.Receive();
            if (!opening.StartsWith("0")) throw new InvalidDataException("Missing Engine.IO opening");
            JObject handshake = JObject.Parse(opening.Substring(1));
            int interval = (int?)handshake["pingInterval"] ?? 25000;
            int timeout = (int?)handshake["pingTimeout"] ?? 60000;
            if (interval < 100 || interval > 300000 || timeout < 100 || timeout > 300000)
                throw new InvalidDataException("Invalid heartbeat settings");
            string subscription = null;
            bool ready = false;
            DateTime started = DateTime.UtcNow;
            DateTime tokenRefresh;
            lock (_sync) { tokenRefresh = _tokenRefreshUtc; }
            DateTime lastPing = DateTime.MinValue;
            bool awaitingPong = false;
            object heartbeatSync = new object();
            int packets = 0, events = 0;
            bool namespaceConnected = false;
            session.CloseReason = null;
            transport.Stage = "waiting for clientId";
            using (var heartbeat = new Timer(delegate(object ignored)
            {
                try
                {
                    lock (heartbeatSync)
                    {
                        DateTime now = DateTime.UtcNow;
                        bool expired = now >= tokenRefresh;
                        if (session.Stopped || !CurrentGeneration(gen)) { transport.Dispose(); return; }
                        if (expired) { session.CloseReason = "token renewal"; transport.Dispose(); return; }
                        if (!ready && (now - started).TotalSeconds > 15)
                        {
                            session.CloseReason = "subscription timeout (packets=" + packets + ", events=" + events +
                                ", namespace=" + (namespaceConnected ? "connected" : "pending") + ")";
                            transport.Dispose(); return;
                        }
                        if (awaitingPong && (now - lastPing).TotalMilliseconds > timeout)
                        { session.CloseReason = "heartbeat timeout"; transport.Dispose(); return; }
                        if (!awaitingPong && (now - lastPing).TotalMilliseconds >= interval)
                        {
                            transport.Send("2");
                            lastPing = now;
                            awaitingPong = true;
                        }
                    }
                }
                catch { transport.Dispose(); }
            }, null, 1000, 1000))
            {
                // Engine.IO 3 opens the default Socket.IO namespace server-side.
                while (!session.Stopped && CurrentGeneration(gen))
                {
                    string packet = transport.Receive();
                    System.Threading.Interlocked.Increment(ref packets);
                    if (packet == "40" || packet == "40/") { lock (heartbeatSync) { namespaceConnected = true; } continue; }
                    if (packet == "1" || packet == "41") throw new EndOfStreamException();
                    if (packet.StartsWith("3")) { lock (heartbeatSync) { awaitingPong = false; } continue; }
                    if (packet.StartsWith("2")) { transport.Send("3" + packet.Substring(1)); continue; }
                    string name;
                    JToken data;
                    string acknowledgement;
                    if (!Control4SocketProtocol.TryEventPacket(packet, out name, out data, out acknowledgement)) continue;
                    System.Threading.Interlocked.Increment(ref events);
                    if (acknowledgement != null) transport.Send("43" + acknowledgement + "[]");
                    if (name == "clientId" && subscription == null)
                    {
                        if (data == null || data.Type != JTokenType.String || data.ToString().Length > 1024)
                            throw new InvalidDataException("Invalid client ID");
                        transport.Send(Control4SocketProtocol.Event("2probe", null));
                        transport.Stage = "subscription GET";
                        string json = Request(host, token, "GET", "/api/v1/items/datatoui?JWT=" + Uri.EscapeDataString(token) +
                            "&SubscriptionClient=" + Uri.EscapeDataString((string)data), null);
                        JObject response = JObject.Parse(json);
                        JToken id = response["subscriptionId"];
                        if (id == null || id.Type != JTokenType.String || id.ToString().Length == 0 || id.ToString().Length > 1024)
                            throw new InvalidDataException("Missing subscription ID");
                        subscription = (string)id;
                        if (session.Stopped || !CurrentGeneration(gen)) return;
                        transport.Stage = "startSubscription send";
                        transport.Send(Control4SocketProtocol.Event("startSubscription", subscription));
                        lock (heartbeatSync) { ready = true; }
                        retrySeconds = 1;
                        SetOnline(true);
                        EmitMessage("WebSocket subscription started");
                        transport.Stage = "live receive";
                        ushort[] ids;
                        lock (_sync) { ids = ActiveLightIds(); }
                        foreach (ushort light in ids) QueryLight(light);
                        SynchronizeShades();
                        SynchronizeAudio();
                        StartRoomModules();
                    }
                    else if (name == subscription && subscription != null)
                    {
                        if (data != null && data.Type == JTokenType.Object && data["status"] != null)
                        { transport.Send(Control4SocketProtocol.Event("2", null)); continue; }
                        var messages = data as JArray;
                        if (messages != null) { foreach (JToken message in messages) RouteRealtimeMessage(message, gen); }
                        else RouteRealtimeMessage(data, gen);
                    }
                }
            }
        }

        internal event Action AudioSessionReadbackHint;

        private void SignalAudioSessionReadback(JToken message, int generation)
        {
            var handler = AudioSessionReadbackHint;
            if (handler == null || !CurrentGeneration(generation)) return;
            var packet = message as JObject;
            if (packet == null || ScalarText(packet["evtName"]) != "OnDataToUI") return;
            var data = packet["data"] as JObject;
            if (data == null) return;
            var media = data["EVENT"] as JObject;
            string name = media == null ? null : ScalarText(media["NAME"]);
            if (name != "DashboardChanged" && name != "QueueChanged" &&
                !(data["active_state_changed"] is JObject)) return;
            SafeEvent(handler);
        }

        private void RouteRealtimeMessage(JToken message, int gen)
        {
            CaptureAudioFeedbackEvent(message, gen);
            CaptureMediaSessionEvent(message, gen);
            SignalAudioSessionReadback(message, gen);
            CaptureNowPlayingEvent(message, gen);
            RoutePlayingEvent(message, gen);
            CaptureLightProbeEvent(message, gen);
            RouteRealtimeLight(message, gen);
            RouteRealtimeAudio(message, gen);
            ushort shade;
            if (TryRegisteredShadeEvent(message, gen, out shade)) QueueShadeQuery(shade, true);
        }

        private void RouteRealtimeLight(JToken message, int gen)
        {
            ushort id, level;
            if (!Control4SocketProtocol.TryLight(message, out id, out level)) return;
            lock (_sync)
            {
                if (_generation != gen || (!_registeredLights.Contains(id) && !_panelLightReferences.ContainsKey(id))) return;
                int version;
                _lightEventVersions.TryGetValue(id, out version);
                _lightEventVersions[id] = version + 1;
                AcceptLightEvent(id, level);
            }
            EmitMessage("Light " + id + " level " + level + "% (WebSocket)");
        }
    }
}

