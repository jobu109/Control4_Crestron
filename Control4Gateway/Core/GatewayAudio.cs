using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;

namespace Control4Crestron
{
    public sealed partial class GatewayCore
    {
        public event EventHandler<AudioStatusEventArgs> AudioChanged;
        private readonly Dictionary<ushort, AudioRoom> _audioRooms = new Dictionary<ushort, AudioRoom>();
        private readonly Dictionary<ushort, AudioQueryWork> _audioQueries = new Dictionary<ushort, AudioQueryWork>();
        private sealed class AudioRoom
        {
            internal bool AutomaticMapping;
            internal ushort WatchId, ListenId, ListenOutputId, Power, Volume, Muted, PowerValid, VolumeValid, MuteValid;
            internal int References, VolumeVersion, MuteVersion, SourceVersion;
            internal RoomActiveSourceEventArgs SourceState;
        }
        private sealed class AudioQueryWork
        {
            internal int Generation, Epoch;
            internal bool Dirty;
            internal AudioRoom Configuration;
        }

        internal bool RegisterAudio(ushort roomId, ushort watchId, ushort listenId, ushort listenOutputId)
        {
            if (roomId == 0) { EmitMessage("Audio room ID must be nonzero"); return false; }
            if ((listenId == 0) != (listenOutputId == 0)) { EmitMessage("Configure both Listen endpoint and output ID, or set both to zero"); return false; }
            bool online;
            lock (_sync)
            {
                AudioRoom room;
                if (_audioRooms.TryGetValue(roomId, out room))
                {
                    if (room.AutomaticMapping)
                    {
                        room.WatchId = watchId; room.ListenId = listenId; room.ListenOutputId = listenOutputId;
                        room.AutomaticMapping = false; room.VolumeVersion++; room.MuteVersion++;
                    }
                    if (room.WatchId != watchId || room.ListenId != listenId || room.ListenOutputId != listenOutputId)
                    { EmitMessage("Audio room feedback mapping conflict"); return false; }
                    room.References++;
                }
                else _audioRooms[roomId] = new AudioRoom { WatchId = watchId, ListenId = listenId, ListenOutputId = listenOutputId, References = 1 };
                online = _online;
            }
            if (online) QueryAudio(roomId);
            return true;
        }
        internal void UnregisterAudio(ushort roomId)
        {
            lock (_sync)
            {
                AudioRoom room;
                if (!_audioRooms.TryGetValue(roomId, out room)) return;
                if (--room.References == 0) { _audioRooms.Remove(roomId); _audioQueries.Remove(roomId); }
            }
        }
        public void PublishCurrentAudio(ushort roomId)
        {
            lock (_sync) { AudioRoom room; if (_audioRooms.TryGetValue(roomId, out room)) PublishAudio(roomId, room); }
        }
        private void PublishAudio(ushort id, AudioRoom room)
        {
            var handler = AudioChanged;
            if (handler != null) SafeEvent(delegate { handler(this, new AudioStatusEventArgs
            {
                RoomId = id, Power = room.Power, PowerValid = room.PowerValid,
                Volume = room.Volume, VolumeValid = room.VolumeValid, Muted = room.Muted, MuteValid = room.MuteValid,
                Valid = (ushort)(room.PowerValid == 1 && room.VolumeValid == 1 && room.MuteValid == 1 ? 1 : 0)
            }); });
        }
        private void InvalidateAudioFeedback()
        {
            lock (_sync)
            {
                _audioQueries.Clear();
                InvalidatePlaying();
                foreach (var item in new List<KeyValuePair<ushort, AudioRoom>>(_audioRooms))
                {
                    item.Value.PowerValid = item.Value.VolumeValid = item.Value.MuteValid = 0;
                    item.Value.SourceState = null;
                    PublishAudio(item.Key, item.Value);
                    PublishRoomSource(item.Key, null);
                }
            }
        }
        private void SynchronizeAudio()
        {
            ushort[] ids;
            lock (_sync) { ids = new ushort[_audioRooms.Count]; _audioRooms.Keys.CopyTo(ids, 0); }
            foreach (ushort id in ids) QueryAudio(id);
        }

        public void QueryAudio(ushort roomId)
        {
            AudioQueryWork work;
            lock (_sync)
            {
                AudioRoom room;
                if (!_audioRooms.TryGetValue(roomId, out room)) { EmitMessage("Audio room is not configured"); return; }
                if (_audioQueries.TryGetValue(roomId, out work) && AudioWorkCurrent(roomId, work))
                { work.Dirty = true; return; }
                work = new AudioQueryWork { Generation = _generation, Epoch = _feedbackEpoch, Configuration = room };
                _audioQueries[roomId] = work;
            }
            EmitMessage("Audio query queued; gateway " + _instance + "; room " + roomId);
            Queue(delegate
            {
                try
                {
                    while (true)
                    {
                        lock (_sync) { if (!AudioWorkCurrent(roomId, work)) return; work.Dirty = false; }
                        string host, token;
                        if (!GetConnection(out host, out token))
                        {
                            lock (_sync)
                            {
                                if (!AudioWorkCurrent(roomId, work)) return;
                                work.Configuration.PowerValid = work.Configuration.VolumeValid = work.Configuration.MuteValid = 0;
                                PublishAudio(roomId, work.Configuration);
                            }
                            EmitMessage("Audio query: gateway offline"); return;
                        }
                        foreach (string field in new[] { "POWER_STATE", "CURRENT_VOLUME", "IS_MUTED" })
                        {
                            int version;
                            lock (_sync)
                            {
                                if (!AudioWorkCurrent(roomId, work)) return;
                                version = AudioFieldVersion(work.Configuration, field);
                            }
                            try
                            {
                                string json = Request(host, token, "GET", "/api/v1/items/" + roomId + "/variables?varnames=" + field, null);
                                ushort value;
                                if (!TryAudioVariable(json, field, out value)) throw new InvalidDataException("Invalid audio variable");
                                ApplyAudioReadback(roomId, work, field, value, 1, version);
                            }
                            catch (Exception ex)
                            {
                                lock (_sync) { if (!AudioWorkCurrent(roomId, work)) return; }
                                InvalidateRejectedToken(ex);
                                ApplyAudioReadback(roomId, work, field, 0, 0, version);
                                EmitMessage("Audio room " + roomId + " " + field + " query failed: " + FailureKind(ex));
                                lock (_sync) { if (!_online) { InvalidateAudioFeedback(); return; } }
                            }
                        }
                        ReadRoomSourceFeedback(roomId, work, host, token);
                        EmitMessage("Audio room " + roomId + " readback complete (REST)");
                        lock (_sync)
                        {
                            if (!AudioWorkCurrent(roomId, work)) return;
                            if (!work.Dirty) { _audioQueries.Remove(roomId); return; }
                        }
                    }
                }
                catch (Exception ex)
                {
                    lock (_sync)
                    {
                        if (!AudioWorkCurrent(roomId, work)) return;
                        work.Configuration.PowerValid = work.Configuration.VolumeValid = work.Configuration.MuteValid = 0;
                        PublishAudio(roomId, work.Configuration);
                    }
                    EmitMessage("Audio query failed: " + FailureKind(ex));
                }
                finally
                {
                    lock (_sync)
                    {
                        AudioQueryWork current;
                        if (_audioQueries.TryGetValue(roomId, out current) && Object.ReferenceEquals(current, work)) _audioQueries.Remove(roomId);
                    }
                }
            });
        }
        internal static string BuildAudioCommand(string operation, int value)
        {
            string command;
            var parameters = new JObject();
            switch (operation)
            {
                case "SetVolume":
                    if (value < 0 || value > 100) throw new ArgumentOutOfRangeException("value");
                    command = "SET_VOLUME_LEVEL"; parameters["LEVEL"] = value; break;
                case "MuteOn": command = "MUTE_ON"; break;
                case "MuteOff": command = "MUTE_OFF"; break;
                case "RoomOff": command = "ROOM_OFF"; break;
                case "SelectAudioSource": case "SelectVideoSource":
                    if (value <= 0) throw new ArgumentOutOfRangeException("value");
                    command = operation == "SelectAudioSource" ? "SELECT_AUDIO_DEVICE" : "SELECT_VIDEO_DEVICE";
                    parameters["deviceid"] = value; break;
                default: throw new ArgumentException("Unknown audio operation", "operation");
            }
            return new JObject(new JProperty("async", true), new JProperty("command", command),
                new JProperty("tParams", parameters)).ToString(Formatting.None);
        }

        public void SendAudioCommand(ushort roomId, string operation, int value)
        {
            string body;
            try { body = BuildAudioCommand(operation, value); }
            catch (ArgumentException) { EmitMessage("Invalid audio command or value"); return; }
            AudioQueryWork session;
            lock (_sync)
            {
                AudioRoom room;
                if (!_online || !_audioRooms.TryGetValue(roomId, out room))
                { EmitMessage("Audio command rejected: gateway offline or room not configured"); return; }
                session = new AudioQueryWork { Generation = _generation, Epoch = _feedbackEpoch, Configuration = room };
            }
            Queue(delegate
            {
                try
                {
                    lock (_sync) { if (!_online || !AudioWorkCurrent(roomId, session)) return; }
                    string host, token;
                    if (!GetConnection(out host, out token)) { EmitMessage("Audio command: gateway offline"); return; }
                    lock (_sync) { if (!_online || !AudioWorkCurrent(roomId, session)) return; }
                    Request(host, token, "POST", "/api/v1/items/" + roomId + "/commands", body);
                    lock (_sync)
                    {
                        if (!_online || !AudioWorkCurrent(roomId, session)) return;
                        session.Configuration.VolumeVersion++;
                        session.Configuration.MuteVersion++;
                    }
                    EmitMessage("Audio room " + roomId + " " + operation + " accepted; querying measured state");
                    if (operation == "SelectAudioSource" || operation == "SelectVideoSource" || operation == "RoomOff")
                        InvalidateRoomSource(roomId, session);
                    QueryAudio(roomId);
                }
                catch (Exception ex)
                {
                    lock (_sync) { if (!AudioWorkCurrent(roomId, session)) return; }
                    InvalidateRejectedToken(ex);
                    EmitMessage("Audio command failed: " + FailureKind(ex));
                }
            });
        }
        // Call with _sync held. Configuration identity also rejects removed/re-added rooms.
        private bool AudioWorkCurrent(ushort id, AudioQueryWork work)
        {
            AudioRoom room;
            return work.Generation == _generation && work.Epoch == _feedbackEpoch &&
                _audioRooms.TryGetValue(id, out room) && Object.ReferenceEquals(room, work.Configuration);
        }
        private static int AudioFieldVersion(AudioRoom room, string field)
        {
            return field == "CURRENT_VOLUME" ? room.VolumeVersion : field == "IS_MUTED" ? room.MuteVersion : 0;
        }
        private void ApplyAudioReadback(ushort id, AudioQueryWork work, string field, ushort value, ushort valid, int version)
        {
            lock (_sync)
            {
                if (!AudioWorkCurrent(id, work) || AudioFieldVersion(work.Configuration, field) != version) return;
                AudioRoom room = work.Configuration;
                switch (field)
                {
                    case "POWER_STATE": room.Power = value; room.PowerValid = valid; break;
                    case "CURRENT_VOLUME": room.Volume = value; room.VolumeValid = valid; break;
                    case "IS_MUTED": room.Muted = value; room.MuteValid = valid; break;
                    default: return;
                }
                PublishAudio(id, room);
            }
        }

        private static bool TryAudioBoolean(JToken value, out ushort result)
        {
            result = 0;
            if (value == null) return false;
            if (value.Type == JTokenType.Boolean) { result = (ushort)((bool)value ? 1 : 0); return true; }
            if (value.Type != JTokenType.Integer && value.Type != JTokenType.String) return false;
            string text = value.ToString();
            if (text == "0" || text.Equals("false", StringComparison.OrdinalIgnoreCase)) return true;
            if (text == "1" || text.Equals("true", StringComparison.OrdinalIgnoreCase)) { result = 1; return true; }
            return false;
        }
        private static bool TryAudioVolume(JToken value, out ushort result)
        {
            result = 0;
            if (value == null || (value.Type != JTokenType.Integer && value.Type != JTokenType.Float && value.Type != JTokenType.String)) return false;
            decimal number;
            if (!Decimal.TryParse(value.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out number) ||
                number < 0 || number > 100 || Decimal.Truncate(number) != number) return false;
            result = (ushort)number; return true;
        }
        private static bool TryAudioVariable(string json, string field, out ushort value)
        {
            value = 0;
            if (string.IsNullOrEmpty(json) || json.Length > 16384) return false;
            try
            {
                var entries = JToken.Parse(json) as JArray;
                if (entries == null || entries.Count != 1 || entries[0].Type != JTokenType.Object) return false;
                JToken raw = entries[0]["value"];
                if (field == "CURRENT_VOLUME") return TryAudioVolume(raw, out value);
                if (field == "POWER_STATE" || field == "IS_MUTED") return TryAudioBoolean(raw, out value);
                return false;
            }
            catch (Newtonsoft.Json.JsonReaderException) { return false; }
        }

        private void RouteRealtimeAudio(JToken message, int gen)
        {
            if (message == null || message.Type != JTokenType.Object) return;
            JToken name = message["evtName"], device = message["iddevice"], data = message["data"];
            ushort endpoint;
            if (name == null || name.Type != JTokenType.String || (string)name != "OnDataToUI" ||
                device == null || (device.Type != JTokenType.Integer && device.Type != JTokenType.String) ||
                !UInt16.TryParse(device.ToString(), out endpoint) || endpoint == 0 || data == null || data.Type != JTokenType.Object) return;
            ushort volume;
            bool hasVolume = TryAudioVolume(data["volume_level"], out volume);
            bool hasMute = data["mute"] != null && data["mute"].Type == JTokenType.Boolean;
            bool powerHint = data["powerstate"] != null && data["powerstate"].Type == JTokenType.Boolean;
            var refresh = new List<ushort>();
            lock (_sync)
            {
                if (_generation != gen || !_online) return;
                foreach (var item in new List<KeyValuePair<ushort, AudioRoom>>(_audioRooms))
                {
                    AudioRoom room = item.Value;
                    bool watch = room.WatchId == endpoint && (hasVolume || hasMute || powerHint);
                    bool listen = room.ListenId == endpoint && MatchesAudioMatrixOutput(data, room.ListenOutputId);
                    if (!watch && !listen) continue;
                    // Either endpoint can emit while the other owns the room's audio.
                    // Read authoritative room variables; never apply "last device event wins".
                    room.VolumeVersion++;
                    room.MuteVersion++;
                    room.SourceVersion++;
                    refresh.Add(item.Key);
                }
                AddRoomEventRefreshes(endpoint, data, refresh);
            }
            foreach (ushort roomId in refresh) QueryAudio(roomId);
        }

        private static bool MatchesAudioMatrixOutput(JToken data, ushort outputId)
        {
            if (outputId == 0 || data == null || data.Type != JTokenType.Object) return false;
            JToken status = data["OutputStatus"];
            if (status == null || status.Type != JTokenType.Object) return false;
            JToken output = status["OutputID"], video = status["IsVideoBinding"];
            ushort id;
            return output != null && output.Type == JTokenType.Integer && UInt16.TryParse(output.ToString(), out id) && id == outputId &&
                video != null && video.Type == JTokenType.Boolean && !(bool)video;
        }
    }
}
