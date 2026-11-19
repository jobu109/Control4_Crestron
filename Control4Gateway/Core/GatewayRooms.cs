using System;
using System.Collections.Generic;
using System.Globalization;

using Newtonsoft.Json.Linq;

namespace Control4Crestron
{
    public sealed partial class GatewayCore
    {
        public event EventHandler<RoomDiscoveryEventArgs> RoomDiscovered;
        private readonly Dictionary<ushort, int> _roomModules = new Dictionary<ushort, int>();
        private bool _roomRealtimeReady;
        private JObject _roomDiscovery;
        private int _roomDiscoveryGeneration = -1, _roomDiscoveryEpoch = -1;
        private object _roomDiscoveryWork;
        private readonly Dictionary<string, string> _roomAvHints = new Dictionary<string, string>();

        // Called with _sync held after session validation. Hints trigger measured
        // room reads; their endpoint values are never assigned to a room.
        private void AddRoomEventRefreshes(ushort endpoint, JToken data, List<ushort> refresh)
        {
            if (_roomModules.Count == 0) return;
            ushort activityRoom = 0, value;
            string key = endpoint.ToString(CultureInfo.InvariantCulture);
            var state = new JObject();
            JToken activity = data["active_state_changed"];
            if (endpoint == 7 && activity != null && activity.Type == JTokenType.Object)
            {
                JToken id = activity["room_id"], type = activity["type"], active = activity["active"];
                if (id == null || id.Type != JTokenType.Integer || !UInt16.TryParse(id.ToString(), out activityRoom) ||
                    !_roomModules.ContainsKey(activityRoom) || type == null || type.Type != JTokenType.String ||
                    ((string)type != "watch" && (string)type != "listen") || active == null || active.Type != JTokenType.Boolean) return;
                key = "activity:" + activityRoom + ":" + (string)type;
                state["active"] = (bool)active;
            }
            else
            {
                if (TryAudioVolume(data["volume_level"], out value)) state["volume"] = value;
                foreach (string name in new[] { "mute", "powerstate" })
                    if (data[name] != null && data[name].Type == JTokenType.Boolean) state[name] = (bool)data[name];
                JToken output = data["OutputStatus"];
                if (output != null && output.Type == JTokenType.Object)
                {
                    JToken id = output["OutputID"], video = output["IsVideoBinding"];
                    ushort outputId;
                    if (id != null && id.Type == JTokenType.Integer && UInt16.TryParse(id.ToString(), out outputId) && outputId > 0 &&
                        video != null && video.Type == JTokenType.Boolean && !(bool)video)
                    {
                        key += ":output:" + outputId;
                        if (TryAudioVolume(output["VolumeLevel"], out value)) state["volume"] = value;
                        if (output["MuteFlag"] != null && output["MuteFlag"].Type == JTokenType.Boolean) state["mute"] = (bool)output["MuteFlag"];
                        int input;
                        if (output["InputID"] != null && output["InputID"].Type == JTokenType.Integer &&
                            Int32.TryParse(output["InputID"].ToString(), out input) && input >= -1) state["input"] = input;
                    }
                }
                if (state.Count == 0) return;
            }
            JObject prior = null;
            string previous;
            if (_roomAvHints.TryGetValue(key, out previous)) prior = JObject.Parse(previous);
            bool changed = prior == null;
            if (prior == null) prior = new JObject();
            foreach (JProperty field in state.Properties())
            {
                if (prior[field.Name] == null || prior[field.Name].ToString() != field.Value.ToString()) changed = true;
                prior[field.Name] = field.Value.DeepClone();
            }
            if (!changed) return;
            if (_roomAvHints.Count >= 256 && !_roomAvHints.ContainsKey(key)) _roomAvHints.Clear();
            _roomAvHints[key] = prior.ToString(Newtonsoft.Json.Formatting.None);
            foreach (ushort id in _roomModules.Keys)
            {
                AudioRoom room;
                if (!_audioRooms.TryGetValue(id, out room) || refresh.Contains(id)) continue;
                if (activityRoom != 0 ? id != activityRoom : room.WatchId != 0 && room.ListenId != 0) continue;
                room.VolumeVersion++; room.MuteVersion++;
                room.SourceVersion++;
                refresh.Add(id);
            }
        }

        internal bool RegisterRoomModule(ushort roomId, bool reuseDiscovery = false)
        {
            if (roomId == 0) { EmitMessage("Room_ID must be nonzero"); return false; }
            bool online, discover;
            lock (_sync)
            {
                if (!_roomModules.ContainsKey(roomId) && _roomModules.Count >= 64)
                { EmitMessage("Room module limit reached (64 rooms per gateway)"); return false; }
                int count; _roomModules.TryGetValue(roomId, out count); _roomModules[roomId] = count + 1;
                AudioRoom room;
                if (_audioRooms.TryGetValue(roomId, out room)) room.References++;
                else _audioRooms[roomId] = new AudioRoom { References = 1, AutomaticMapping = true };
                online = _online;
                discover = !reuseDiscovery || _roomDiscovery == null ||
                    _roomDiscoveryGeneration != _generation || _roomDiscoveryEpoch != _feedbackEpoch ||
                    (string)_roomDiscovery["source_discovery_status"] != "discovered";
            }
            if (online) QueryAudio(roomId);
            if (online && discover) SynchronizeRoomModules();
            return true;
        }
        internal void UnregisterRoomModule(ushort roomId)
        {
            lock (_sync)
            {
                int count;
                if (!_roomModules.TryGetValue(roomId, out count)) return;
                if (count == 1) { _roomModules.Remove(roomId); _playingRooms.Remove(roomId); } else _roomModules[roomId] = count - 1;
                UnregisterAudio(roomId);
            }
        }
        private void StartRoomModules() { lock (_sync) { _roomRealtimeReady = true; } SynchronizeRoomModules(); SynchronizeTransportRooms(); }
        private void SynchronizeRoomModules()
        {
            lock (_sync)
            {
                if (!_online || !_roomRealtimeReady || _roomModules.Count == 0) return;
            }
            DiscoverRooms();
        }
        private void StopRoomModules()
        {
            InvalidateRoomCatalogs();
            InvalidateTransportRooms();
            lock (_sync)
            {
                _roomAvHints.Clear(); _roomRealtimeReady = false; _roomDiscovery = null; _roomDiscoveryWork = null;
                foreach (ushort id in new List<ushort>(_roomModules.Keys)) PublishRoomDiscovery(id);
            }
        }
        public void DiscoverRooms()
        {
            object work;
            int gen, epoch;
            lock (_sync)
            {
                if (!_online) { EmitMessage("Connect before discovering room sources"); return; }
                if (_roomModules.Count == 0 || _roomDiscoveryWork != null) return;
                gen = _generation; epoch = _feedbackEpoch;
                work = new object(); _roomDiscoveryWork = work;
            }
            Queue(delegate
            {
                try
                {
                    string host, token;
                    if (!GetConnection(out host, out token)) return;
                    lock (_sync) { if (_generation != gen || _feedbackEpoch != epoch || !Object.ReferenceEquals(work, _roomDiscoveryWork)) return; }
                    JArray items = InventoryItems(Request(host, token, "GET", "/api/v1/items", null));
                    string ui = Request(host, token, "GET", "/api/v1/agents/ui_configuration", null);
                    JObject report = BuildSetupReport(new JObject(new JProperty("all_items", items)), ui, new JArray(), _instance);
                    lock (_sync)
                    {
                        if (!_online || _generation != gen || _feedbackEpoch != epoch || !Object.ReferenceEquals(work, _roomDiscoveryWork)) return;
                        _roomDiscovery = report; _roomDiscoveryGeneration = gen; _roomDiscoveryEpoch = epoch;
                        foreach (ushort id in new List<ushort>(_roomModules.Keys)) PublishRoomDiscovery(id);
                    }
                    EmitMessage("Room source discovery complete; content browse and transport remain unavailable");
                }
                catch (Exception ex)
                {
                    lock (_sync)
                    {
                        if (_generation != gen || _feedbackEpoch != epoch || !Object.ReferenceEquals(work, _roomDiscoveryWork)) return;
                        _roomDiscovery = null;
                        foreach (ushort id in new List<ushort>(_roomModules.Keys)) PublishRoomDiscovery(id);
                    }
                    InvalidateRejectedToken(ex);
                    EmitMessage("Room discovery failed: " + FailureKind(ex));
                }
                finally { lock (_sync) { if (Object.ReferenceEquals(work, _roomDiscoveryWork)) _roomDiscoveryWork = null; } }
            });
        }
        internal void PublishRoomDiscovery(ushort roomId)
        {
            lock (_sync)
            {
                JObject room = null;
                if (_online && _roomDiscovery != null && _roomDiscoveryGeneration == _generation && _roomDiscoveryEpoch == _feedbackEpoch &&
                    (string)_roomDiscovery["source_discovery_status"] == "discovered")
                    foreach (JToken candidate in (JArray)_roomDiscovery["rooms"])
                        if ((int)candidate["room_id"] == roomId) { room = (JObject)candidate; break; }
                var handler = RoomDiscovered;
                if (handler != null)
                {
                    var args = new RoomDiscoveryEventArgs { RoomId = roomId, Valid = (ushort)(room == null ? 0 : 1), Json = room == null ? "" : room.ToString() };
                    SafeEvent(delegate { handler(this, args); });
                }
            }
        }
    }
}


