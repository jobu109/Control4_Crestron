using System;
using System.Collections.Generic;
using System.IO;
using System.Globalization;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Control4Crestron
{
    public sealed partial class GatewayCore
    {
        public event EventHandler<ShadeStatusEventArgs> ShadeChanged;
        private readonly Dictionary<ushort, ushort> _shadePositions = new Dictionary<ushort, ushort>();
        private readonly Dictionary<ushort, int> _registeredShades = new Dictionary<ushort, int>();
        private readonly Dictionary<ushort, ShadeQueryWork> _shadeQueries = new Dictionary<ushort, ShadeQueryWork>();
        private sealed class ShadeQueryWork
        {
            internal int Generation, Epoch;
            internal bool Dirty, FromEvent;
        }

        internal bool RegisterShade(ushort id, bool reuseRegistration = false)
        {
            if (id == 0) return false;
            bool online, shared;
            lock (_sync)
            {
                int count;
                _registeredShades.TryGetValue(id, out count);
                _registeredShades[id] = count + 1;
                shared = count > 0;
                online = _online && (!reuseRegistration || !shared);
            }
            if (online) QueryShade(id);
            return shared;
        }
        internal void UnregisterShade(ushort id)
        {
            lock (_sync)
            {
                int count;
                if (!_registeredShades.TryGetValue(id, out count)) return;
                if (count <= 1) _registeredShades.Remove(id);
                else _registeredShades[id] = count - 1;
            }
        }
        private void SynchronizeShades()
        {
            ushort[] ids;
            lock (_sync) { ids = new ushort[_registeredShades.Count]; _registeredShades.Keys.CopyTo(ids, 0); }
            foreach (ushort id in ids) QueryShade(id);
        }

        private bool TryRegisteredShadeEvent(JToken message, int gen, out ushort id)
        {
            id = 0;
            if (message == null || message.Type != JTokenType.Object) return false;
            JToken name = message["evtName"], device = message["iddevice"];
            if (name == null || name.Type != JTokenType.String || (string)name != "OnDataToUI" ||
                device == null || (device.Type != JTokenType.Integer && device.Type != JTokenType.String) ||
                !UInt16.TryParse(device.ToString(), NumberStyles.None, CultureInfo.InvariantCulture, out id)) return false;
            lock (_sync) { return gen == _generation && _online && _registeredShades.ContainsKey(id); }
        }

        internal static string BuildShadeCommand(string operation, ushort level)
        {
            string command;
            var parameters = new JObject();
            switch (operation)
            {
                case "Open": command = "SET_LEVEL_TARGET:LEVEL_TARGET_OPEN"; break;
                case "Close": command = "SET_LEVEL_TARGET:LEVEL_TARGET_CLOSED"; break;
                case "Stop": command = "STOP"; break;
                case "SetLevel":
                    if (level > 100) throw new ArgumentOutOfRangeException("level");
                    command = "SET_LEVEL_TARGET";
                    parameters["LEVEL_TARGET"] = level;
                    break;
                default: throw new ArgumentException("Unknown shade operation", "operation");
            }
            return new JObject(new JProperty("async", true), new JProperty("command", command),
                new JProperty("tParams", parameters)).ToString(Formatting.None);
        }

        public void SendShadeCommand(ushort proxyId, string operation, ushort level)
        {
            if (proxyId == 0 || (operation == "SetLevel" && level > 100))
            { EmitMessage("Invalid shade proxy ID or level"); return; }
            string body;
            try { body = BuildShadeCommand(operation, level); }
            catch (ArgumentException) { EmitMessage("Invalid shade command"); return; }
            int gen;
            lock (_sync) { gen = _generation; }
            Queue(delegate
            {
                try
                {
                    if (!CurrentGeneration(gen)) return;
                    string host, token;
                    if (!GetConnection(out host, out token)) { EmitMessage("Shade command: gateway offline"); return; }
                    if (!CurrentGeneration(gen)) return;
                    Request(host, token, "POST", "/api/v1/items/" + proxyId + "/commands", body);
                    if (CurrentGeneration(gen)) EmitMessage("Shade " + proxyId + " " + operation + " accepted");
                    // The acknowledgement is not measured position; wait for device events.
                }
                catch (Exception ex)
                {
                    if (CurrentGeneration(gen)) { InvalidateRejectedToken(ex); EmitMessage("Shade command failed: " + FailureKind(ex)); }
                }
            });
        }

        public void QueryShade(ushort proxyId)
        {
            QueueShadeQuery(proxyId, false);
        }

        private void QueueShadeQuery(ushort proxyId, bool fromEvent)
        {
            if (proxyId == 0) { EmitMessage("Invalid shade proxy ID"); return; }
            ShadeQueryWork work;
            lock (_sync)
            {
                if (_shadeQueries.TryGetValue(proxyId, out work) && work.Generation == _generation && work.Epoch == _feedbackEpoch)
                {
                    work.Dirty = true;
                    work.FromEvent |= fromEvent;
                    return;
                }
                work = new ShadeQueryWork { Generation = _generation, Epoch = _feedbackEpoch, FromEvent = fromEvent };
                _shadeQueries[proxyId] = work;
            }
            EmitMessage("Shade query queued; gateway " + _instance + "; proxy " + proxyId);
            Queue(delegate
            {
                try
                {
                    while (true)
                    {
                        bool eventRead;
                        lock (_sync)
                        {
                            if (_generation != work.Generation || _feedbackEpoch != work.Epoch) return;
                            work.Dirty = false;
                            eventRead = work.FromEvent;
                        }
                        string host, token;
                        if (!GetConnection(out host, out token))
                        {
                            InvalidateShade(proxyId);
                            EmitMessage("Shade query: gateway offline");
                            return;
                        }
                        if (!CurrentGeneration(work.Generation)) return;
                        string json = ReadWithTimeoutRetry(host, token, "/api/v1/items/" + proxyId + "/variables?varnames=Level", work.Generation);
                        ushort position;
                        if (!TryParseLightLevel(json, out position)) throw new InvalidDataException("Invalid shade Level response");
                        lock (_sync)
                        {
                            if (_generation != work.Generation || _feedbackEpoch != work.Epoch || !_online) return;
                            _shadePositions[proxyId] = position;
                            PublishShade(proxyId, position, 1);
                            if (!work.Dirty) _shadeQueries.Remove(proxyId);
                        }
                        EmitMessage("Shade " + proxyId + " position " + position + "% (" +
                            (eventRead ? "WebSocket-triggered REST readback" : "REST readback") + ")");
                        lock (_sync) { if (!work.Dirty) return; }
                    }
                }
                catch (Exception ex)
                {
                    lock (_sync) { if (_generation != work.Generation || _feedbackEpoch != work.Epoch) return; }
                    InvalidateRejectedToken(ex);
                    InvalidateShade(proxyId);
                    EmitMessage("Shade " + proxyId + " readback failed: " + FailureKind(ex));
                }
                finally
                {
                    lock (_sync)
                    {
                        ShadeQueryWork current;
                        if (_shadeQueries.TryGetValue(proxyId, out current) && Object.ReferenceEquals(current, work))
                            _shadeQueries.Remove(proxyId);
                    }
                }
            });
        }

        public void PublishCurrentShade(ushort proxyId)
        {
            lock (_sync)
            {
                ushort position;
                if (_shadePositions.TryGetValue(proxyId, out position)) PublishShade(proxyId, position, 1);
            }
        }

        private void PublishShade(ushort proxyId, ushort position, ushort valid)
        {
            var handler = ShadeChanged;
            if (handler != null) SafeEvent(delegate { handler(this, new ShadeStatusEventArgs
            {
                ItemId = proxyId, Position = position, Valid = valid,
                FullyOpen = (ushort)(valid == 1 && position == 100 ? 1 : 0),
                FullyClosed = (ushort)(valid == 1 && position == 0 ? 1 : 0)
            }); });
        }

        private void InvalidateShade(ushort proxyId)
        {
            lock (_sync) { _shadePositions.Remove(proxyId); PublishShade(proxyId, 0, 0); }
        }

        private void InvalidateShadeFeedback()
        {
            lock (_sync)
            {
                ushort[] ids = new ushort[_registeredShades.Count];
                _registeredShades.Keys.CopyTo(ids, 0);
                _shadePositions.Clear();
                _shadeQueries.Clear();
                foreach (ushort id in ids) PublishShade(id, 0, 0);
            }
        }
    }
}
