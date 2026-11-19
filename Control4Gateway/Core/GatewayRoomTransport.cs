using System;
using System.Collections.Generic;
using System.Globalization;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Control4Crestron
{
    public sealed partial class GatewayCore
    {
        internal static readonly string[] Sr260Keys = {
            "Room_Off","Watch","Control4","Listen","List","Custom_1","Custom_2","Custom_3",
            "Guide","Page_Up","Page_Down","Previous","Volume_Up","Volume_Down","Channel_Up","Channel_Down",
            "Up","Down","Left","Right","Select","Mute","Info","Menu","Back","DVR","Rewind","Fast_Forward",
            "Skip_Back","Play","Skip_Forward","Record","Pause","Stop","Red","Green","Yellow","Blue",
            "Digit_1","Digit_2","Digit_3","Digit_4","Digit_5","Digit_6","Digit_7","Digit_8","Digit_9","Star","Digit_0","Pound"
        };
        public event EventHandler<RoomTransportEventArgs> RoomTransportChanged;
        private sealed class TransportRoom
        {
            internal int References;
            internal object Work;
            internal JObject Report;
        }
        private readonly Dictionary<ushort, TransportRoom> _transportRooms = new Dictionary<ushort, TransportRoom>();
        internal bool RegisterTransportRoom(ushort id)
        {
            if (id == 0) { EmitMessage("Transport Room_ID must be nonzero"); return false; }
            bool discover;
            lock (_sync)
            {
                TransportRoom room;
                if (!_transportRooms.TryGetValue(id, out room))
                {
                    if (_transportRooms.Count >= 64) { EmitMessage("Transport room limit reached"); return false; }
                    _transportRooms[id] = room = new TransportRoom();
                }
                room.References++;
                PublishTransport(id, room);
                discover = _online && _roomRealtimeReady;
            }
            if (discover) DiscoverTransportCommands(id);
            return true;
        }
        internal void UnregisterTransportRoom(ushort id)
        {
            lock (_sync)
            {
                TransportRoom room;
                if (_transportRooms.TryGetValue(id, out room) && --room.References == 0) _transportRooms.Remove(id);
            }
        }
        private void SynchronizeTransportRooms()
        {
            ushort[] ids;
            lock (_sync) { ids = new ushort[_transportRooms.Count]; _transportRooms.Keys.CopyTo(ids, 0); }
            foreach (ushort id in ids) DiscoverTransportCommands(id);
        }
        private void InvalidateTransportRooms()
        {
            lock (_sync)
                foreach (var item in _transportRooms)
                { item.Value.Report = null; item.Value.Work = null; PublishTransport(item.Key, item.Value); }
        }
        private bool TransportCurrent(ushort id, TransportRoom room, int gen, int epoch)
        {
            TransportRoom current;
            return _online && gen == _generation && epoch == _feedbackEpoch &&
                _transportRooms.TryGetValue(id, out current) && Object.ReferenceEquals(current, room);
        }
        public void DiscoverTransportCommands(ushort id)
        {
            TransportRoom room; object work; int gen, epoch;
            lock (_sync)
            {
                if (!_online || !_transportRooms.TryGetValue(id, out room))
                { EmitMessage("Connect and initialize Transport before discovering commands"); return; }
                if (room.Work != null) return;
                room.Work = work = new object(); gen = _generation; epoch = _feedbackEpoch;
                room.Report = null; PublishTransport(id, room);
            }
            Queue(delegate
            {
                try
                {
                    string host, token;
                    if (!GetConnection(out host, out token)) return;
                    lock (_sync) { if (!TransportCurrent(id, room, gen, epoch) || !Object.ReferenceEquals(room.Work, work)) return; }
                    string json = Request(host, token, "GET", "/api/v1/items/" + id + "/commands", null);
                    JObject report = BuildCommandMetadataReport(json, 200, id);
                    report.Remove("source_id"); report["room_id"] = id;
                    report["generated_utc"] = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture);
                    report["execution_note"] = "Metadata only; advertised commands are not runtime verification. Missing entries do not prove lack of support.";
                    lock (_sync)
                    {
                        if (!TransportCurrent(id, room, gen, epoch) || !Object.ReferenceEquals(room.Work, work)) return;
                        room.Report = report; PublishTransport(id, room);
                    }
                    EmitMessage("Room " + id + " REST command metadata collected; Export_Inventory includes room_transport_commands");
                }
                catch (Exception ex)
                {
                    lock (_sync) { if (!TransportCurrent(id, room, gen, epoch) || !Object.ReferenceEquals(room.Work, work)) return; }
                    InvalidateRejectedToken(ex);
                    EmitMessage("Room transport discovery failed: " + FailureKind(ex));
                }
                finally { lock (_sync) { if (Object.ReferenceEquals(room.Work, work)) room.Work = null; } }
            });
        }
        private static string TransportCommandName(string key)
        {
            switch (key)
            {
                case "Play": return "PLAY";
                case "Pause": return "PAUSE";
                case "Stop": return "STOP";
                case "Room_Off": return "ROOM_OFF";
                case "Volume_Up": return "PULSE_VOL_UP";
                case "Volume_Down": return "PULSE_VOL_DOWN";
                case "Mute": return "MUTE_TOGGLE";
                case "Control4": return "CONTROL4";
                case "Custom_1": return "CUSTOM_1";
                case "Custom_2": return "CUSTOM_2";
                case "Custom_3": return "CUSTOM_3";
                case "Guide": return "GUIDE";
                case "Page_Up": return "PAGE_UP";
                case "Page_Down": return "PAGE_DOWN";
                case "Previous": return "RECALL";
                case "Channel_Up": return "PULSE_CH_UP";
                case "Channel_Down": return "PULSE_CH_DOWN";
                case "Up": return "UP";
                case "Down": return "DOWN";
                case "Left": return "LEFT";
                case "Right": return "RIGHT";
                case "Select": return "ENTER";
                case "Info": return "INFO";
                case "Menu": return "MENU";
                case "Back": return "BACK";
                case "DVR": return "PVR";
                case "Rewind": return "SCAN_REV";
                case "Fast_Forward": return "SCAN_FWD";
                case "Skip_Back": return "SKIP_REV";
                case "Skip_Forward": return "SKIP_FWD";
                case "Record": return "RECORD";
                case "Digit_0": return "NUMBER_0";
                case "Digit_1": return "NUMBER_1";
                case "Digit_2": return "NUMBER_2";
                case "Digit_3": return "NUMBER_3";
                case "Digit_4": return "NUMBER_4";
                case "Digit_5": return "NUMBER_5";
                case "Digit_6": return "NUMBER_6";
                case "Digit_7": return "NUMBER_7";
                case "Digit_8": return "NUMBER_8";
                case "Digit_9": return "NUMBER_9";
                case "Star": return "STAR";
                case "Pound": return "POUND";
                default: return null;
            }
        }
        private static bool AdvertisedTransportCommand(JObject report, string key, out string command)
        {
            command = null;
            string expected = TransportCommandName(key);
            if (expected == null || report == null || report["command_list_recognized"] == null || !(bool)report["command_list_recognized"]) return false;
            foreach (JToken definition in (JArray)report["commands"])
            {
                string container = (string)definition["parameter_container_type"];
                // This controller omits the parameter field on its zero-argument commands.
                if ((string)definition["command_name"] != expected || (container != "missing" && container != "Array" && container != "Object")) continue;
                if (((JArray)definition["parameter_definitions"]).Count != 0 || (int)definition["unreported_parameter_count"] != 0 ||
                    (bool)definition["parameters_truncated"]) continue;
                command = expected; return true;
            }
            return false;
        }
        private void PublishTransport(ushort id, TransportRoom room)
        {
            var keys = new JArray(); ushort mapped = 0;
            string ignored;
            foreach (string key in Sr260Keys)
            {
                bool local = key == "Watch" || key == "Listen" || key == "List";
                bool advertised = AdvertisedTransportCommand(room.Report, key, out ignored);
                if (advertised) mapped++;
                keys.Add(new JObject(new JProperty("key", key), new JProperty("state", local ? "local_ui" : advertised ? "advertised" : "unavailable")));
            }
            var args = new RoomTransportEventArgs {
                RoomId = id, DiscoveryValid = (ushort)(room.Report != null && (bool)room.Report["command_list_recognized"] ? 1 : 0),
                CommandCount = (ushort)(room.Report == null ? 0 : (int)room.Report["exported_command_count"]), MappedCount = mapped,
                PlayAvailable = (ushort)(AdvertisedTransportCommand(room.Report, "Play", out ignored) ? 1 : 0),
                PauseAvailable = (ushort)(AdvertisedTransportCommand(room.Report, "Pause", out ignored) ? 1 : 0),
                StopAvailable = (ushort)(AdvertisedTransportCommand(room.Report, "Stop", out ignored) ? 1 : 0),
                Json = new JObject(new JProperty("room_id", id), new JProperty("keys", keys)).ToString(Formatting.None)
            };
            var handler = RoomTransportChanged;
            if (handler != null) SafeEvent(delegate { handler(this, args); });
        }
        private JArray TransportCommandReports()
        {
            var reports = new JArray();
            lock (_sync) foreach (var item in _transportRooms)
                reports.Add(item.Value.Report == null ?
                    new JObject(new JProperty("room_id", item.Key), new JProperty("status", "not_available")) : item.Value.Report.DeepClone());
            return reports;
        }
        public void PressTransportKey(ushort id, string key)
        {
            TransportRoom room; string command; int gen, epoch;
            lock (_sync)
            {
                if (!_online || !_transportRooms.TryGetValue(id, out room))
                { EmitMessage("Transport rejected: room not initialized or gateway offline"); return; }
                if (!AdvertisedTransportCommand(room.Report, key, out command))
                { EmitMessage("SR-260 " + key + " unavailable: REST command/argument mapping not established"); return; }
                gen = _generation; epoch = _feedbackEpoch;
            }
            string body = new JObject(new JProperty("async", true), new JProperty("command", command), new JProperty("tParams", new JObject())).ToString(Formatting.None);
            Queue(delegate
            {
                try
                {
                    string host, token;
                    lock (_sync) { string current; if (!TransportCurrent(id, room, gen, epoch) || !AdvertisedTransportCommand(room.Report, key, out current) || current != command) return; }
                    if (!GetConnection(out host, out token)) return;
                    lock (_sync) { string current; if (!TransportCurrent(id, room, gen, epoch) || !AdvertisedTransportCommand(room.Report, key, out current) || current != command) return; }
                    Request(host, token, "POST", "/api/v1/items/" + id + "/commands", body);
                    lock (_sync) { if (!TransportCurrent(id, room, gen, epoch)) return; }
                    EmitMessage("Room " + id + " " + command + " accepted (not measured playback state)");
                }
                catch (Exception ex)
                {
                    lock (_sync) { if (!TransportCurrent(id, room, gen, epoch)) return; }
                    InvalidateRejectedToken(ex);
                    EmitMessage("Transport command failed: " + FailureKind(ex));
                }
            });
        }
    }
}

