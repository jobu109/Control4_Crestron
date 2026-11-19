using System;
using System.Globalization;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Control4Crestron
{
    // One instance per physical panel or independently wired XPanel.
    public sealed class Control4_Panel_Room
    {
        private readonly object _requests = new object();
        private ushort _gateway;
        private readonly HashSet<ushort> _rooms = new HashSet<ushort>();
        private readonly List<ushort> _roomOrder = new List<ushort>();
        private readonly UiSerialFrames _roomWire = new UiSerialFrames();
        private volatile Session _session;
        public event EventHandler<PanelRoomPacket> PacketReady;

        private sealed class Session
        {
            internal ushort Id;
            internal JObject State = new JObject();
            internal GatewayCore DeviceGateway;
            internal Dictionary<ushort, Control4_Room_Devices.Entry> Devices;
            internal readonly Dictionary<ushort, int?> DeviceLevels = new Dictionary<ushort, int?>();
            internal readonly UiSerialFrames DeviceWire = new UiSerialFrames();
            internal EventHandler<LightStatusEventArgs> LightChanged;
            internal EventHandler<ShadeStatusEventArgs> ShadeChanged;
            internal readonly Control4_Room Room = new Control4_Room();
            internal readonly Control4_Room_Transport Transport = new Control4_Room_Transport();
        }

        public void Initialize(ushort gatewayId, string roomIds)
        {
            lock (_requests)
            {
                Close();
                _rooms.Clear();
                _roomOrder.Clear();
                _gateway = gatewayId;
                foreach (string entry in (roomIds ?? "").Split(','))
                {
                    ushort id;
                    if (UInt16.TryParse(entry.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out id) && id != 0)
                        if (_rooms.Add(id)) _roomOrder.Add(id);
                }
            }
        }

        private void Close()
        {
            Session previous = _session;
            _session = null;
            if (previous == null) return;
            previous.Room.Release();
            previous.Transport.Release();
            if (previous.DeviceGateway != null)
            {
                previous.DeviceGateway.LightChanged -= previous.LightChanged;
                previous.DeviceGateway.ShadeChanged -= previous.ShadeChanged;
                foreach (var device in previous.Devices.Values)
                    if (device.Shade) previous.DeviceGateway.UnregisterShade(device.Id);
                    else previous.DeviceGateway.UnregisterPanelLight(device.Id);
            }
        }

        private void Send(Session session, ushort stream, string data)
        {
            if (!Object.ReferenceEquals(_session, session) || String.IsNullOrEmpty(data) || data.Length > 255) return;
            var handler = PacketReady;
            if (handler != null) handler(this, new PanelRoomPacket { Stream = stream, Json = data });
        }

        private void State(Session session, params object[] values)
        {
            lock (session.State)
            {
                session.State["r"] = session.Id;
                bool changed = false;
                for (int i = 0; i < values.Length; i += 2)
                {
                    var key = (string)values[i];
                    var value = JToken.FromObject(values[i + 1]);
                    if (!JToken.DeepEquals(session.State[key], value)) changed = true;
                    session.State[key] = value;
                }
                if (changed) Send(session, 0, session.State.ToString(Formatting.None));
            }
        }

        private void PublishDevicePage(Session session, ushort page)
        {
            lock (session.DeviceLevels)
                if (session.DeviceWire.Count > 0) Send(session, 0, session.DeviceWire.Read(ref page));
        }

        private void DeviceFeedback(Session session, ushort id, bool shade, ushort value, ushort valid)
        {
            Control4_Room_Devices.Entry entry;
            if (!session.Devices.TryGetValue(id, out entry) || entry.Shade != shade) return;
            int? next = valid == 1 ? (int?)value : null;
            lock (session.DeviceLevels)
            {
                int? previous;
                if (session.DeviceLevels.TryGetValue(id, out previous) && previous == next) return;
                session.DeviceLevels[id] = next;
                Send(session, 0, new JObject { ["r"] = session.Id, ["u"] = id,
                    ["v"] = next.HasValue ? new JValue(next.Value) : new JValue((object)null) }.ToString(Formatting.None));
            }
        }

        private void AttachDevices(Session session)
        {
            var missingLights = new List<ushort>();
            session.Devices = Control4_Room_Devices.Get(_gateway, session.Id);
            if (session.Devices.Count > 0)
            {
                var gateway = session.DeviceGateway = GatewayRegistry.Get(_gateway);
                session.LightChanged = (s, a) => DeviceFeedback(session, a.ItemId, false, a.Level, a.Valid);
                session.ShadeChanged = (s, a) => DeviceFeedback(session, a.ItemId, true, a.Position, a.Valid);
                gateway.LightChanged += session.LightChanged;
                gateway.ShadeChanged += session.ShadeChanged;
                foreach (var device in session.Devices.Values)
                {
                    if (device.Shade)
                    {
                        if (gateway.RegisterShade(device.Id, true)) gateway.PublishCurrentShade(device.Id);
                    }
                    else if (gateway.RegisterPanelLight(device.Id)) gateway.PublishCurrentLight(device.Id);
                    else missingLights.Add(device.Id);
                }
            }

            lock (session.DeviceLevels)
            {
                var items = new JArray();
                foreach (var device in session.Devices.Values)
                {
                    int? level;
                    session.DeviceLevels.TryGetValue(device.Id, out level);
                    items.Add(new JObject { ["id"] = device.Id, ["name"] = device.Name, ["shade"] = device.Shade,
                        ["value"] = level.HasValue ? new JValue(level.Value) : new JValue((object)null) });
                }
                var document = new JObject { ["room_id"] = session.Id, ["devices"] = items };
                session.DeviceWire.Set(session.Id, "d", document.ToString(Formatting.None));
            }
            PublishDevicePage(session, 1);
            if (session.DeviceGateway != null) session.DeviceGateway.QueryPanelLights(session.Id, missingLights);
        }

        private static void DeviceRequest(Session session, JObject body)
        {
            if (session.DeviceGateway == null) return;
            ushort id, value;
            Control4_Room_Devices.Entry device;
            if (!Number(body["device_id"], 1, UInt16.MaxValue, out id) || !session.Devices.TryGetValue(id, out device)) return;
            if (body["action"] == null || body["action"].Type != JTokenType.String) return;
            string action = (string)body["action"];
            if (!device.Shade)
            {
                if (action == "level" && Number(body["value"], 0, 100, out value))
                    session.DeviceGateway.SetPanelLightLevel(id, value);
                return;
            }
            switch (action)
            {
                case "open": session.DeviceGateway.SendShadeCommand(id, "Open", 0); break;
                case "close": session.DeviceGateway.SendShadeCommand(id, "Close", 0); break;
                case "stop": session.DeviceGateway.SendShadeCommand(id, "Stop", 0); break;
                case "level":
                    if (Number(body["value"], 0, 100, out value)) session.DeviceGateway.SendShadeCommand(id, "SetLevel", value);
                    break;
            }
        }

        private void Select(ushort id)
        {
            Close();
            var session = new Session { Id = id };
            session.Room.AutomaticServiceBrowse = false;
            _session = session;
            session.Room.FeedbackChanged += (s, a) => State(session, "volume", a.Volume, "volumeValid", a.VolumeValid,
                "muted", a.Muted, "muteValid", a.MuteValid, "power", a.Power);
            session.Room.BrowserChanged += (s, a) => State(session, "sourceValid", a.SourcesValid);
            session.Room.SelectedSlotsChanged += (s, a) => State(session, "audioIndex", a.AudioIndex, "audioValid", a.AudioValid,
                "videoIndex", a.VideoIndex, "videoValid", a.VideoValid);
            session.Room.SourceListsChanged += (s, a) => { Send(session, 1, a.AudioJson); Send(session, 2, a.VideoJson); };
            session.Room.MediaPageChanged += (s, a) => {
                State(session, "mediaValid", a.Valid, "selectReady", a.SelectionAvailable);
                Send(session, 3, a.Json);
            };
            session.Room.NowPlayingChanged += (s, a) => { State(session, "nowPlayingValid", a.Valid); Send(session, 4, a.Json); };
            session.Room.ProgressChanged += (s, a) => Send(session, 5, a.Json);
            session.Transport.CapabilitiesChanged += (s, a) => Send(session, 6, a.Json);
            session.Transport.Initialize(_gateway, id);
            session.Room.Initialize(_gateway, id);
            AttachDevices(session);
        }

        private static bool Number(JToken token, ushort min, ushort max, out ushort value)
        {
            value = 0;
            return token != null && token.Type == JTokenType.Integer &&
                UInt16.TryParse(token.ToString(), NumberStyles.None, CultureInfo.InvariantCulture, out value) &&
                value >= min && value <= max;
        }

        private void PublishRooms(ushort page, bool refresh)
        {
            if (refresh)
            {
                var gateway = GatewayRegistry.Get(_gateway);
                var rooms = new JArray();
                foreach (ushort id in _roomOrder)
                    rooms.Add(new JObject { ["id"] = id, ["name"] = gateway.PanelRoomName(id) });
                _roomWire.Set(0, "rooms", new JObject { ["room_id"] = 0, ["rooms"] = rooms }.ToString(Formatting.None));
            }
            if (_roomWire.Count == 0 || page > _roomWire.Count) return;
            var handler = PacketReady;
            if (handler != null) handler(this, new PanelRoomPacket { Stream = 0, Json = _roomWire.Read(ref page) });
        }

        public void Request(string json)
        {
            if (String.IsNullOrEmpty(json) || json.Length > 255) return;
            JObject body;
            try { body = JObject.Parse(json); } catch (JsonReaderException) { return; }
            ushort id, number;
            if (body["op"] == null || body["op"].Type != JTokenType.String) return;
            string op = (string)body["op"];
            lock (_requests)
            {
                if (_gateway == 0) return;
                if (op == "rooms") { PublishRooms(1, true); return; }
                if (op == "rooms_page")
                {
                    if (Number(body["value"], 1, UInt16.MaxValue, out number)) PublishRooms(number, false);
                    return;
                }
                if (!Number(body["room_id"], 1, UInt16.MaxValue, out id) || !_rooms.Contains(id)) return;
                if (op == "room") { Select(id); return; }
                Session session = _session;
                if (session == null || session.Id != id) return;
                var room = session.Room;
                var arg = body["value"];
                switch (op)
                {
                    case "device": DeviceRequest(session, body); break;
                    case "device_page": if (Number(arg, 1, UInt16.MaxValue, out number)) PublishDevicePage(session, number); break;
                    case "audio": if (arg != null && arg.Type == JTokenType.String) room.SelectAudioSourceId((string)arg); break;
                    case "video": if (arg != null && arg.Type == JTokenType.String) room.SelectVideoSourceId((string)arg); break;
                    case "item": room.SelectMediaRequest(json); break;
                    case "key": if (arg != null && arg.Type == JTokenType.String) session.Transport.Press((string)arg); break;
                    case "volume": if (Number(arg, 0, 100, out number)) room.SetVolume(number); break;
                    case "mute_on": room.MuteOn(); break;
                    case "mute_off": room.MuteOff(); break;
                    case "off": room.RoomOff(); break;
                    case "shuffle": if (Number(arg, 0, 1, out number)) room.SetPandoraShuffle(number); break;
                    case "audio_page": if (Number(arg, 1, UInt16.MaxValue, out number)) room.SetAudioListPage(number); break;
                    case "video_page": if (Number(arg, 1, UInt16.MaxValue, out number)) room.SetVideoListPage(number); break;
                    case "media_page": if (Number(arg, 1, UInt16.MaxValue, out number)) room.SetMediaPage(number); break;
                    case "playing_page": if (Number(arg, 1, UInt16.MaxValue, out number)) room.SetNowPlayingPage(number); break;
                }
            }
        }
    }

    public sealed class Control4_Room_Devices
    {
        internal sealed class Entry
        {
            internal ushort Id;
            internal bool Shade;
            internal string Name;
        }

        private static readonly object Gate = new object();
        private static readonly Dictionary<uint, Dictionary<ushort, Entry>> Rooms =
            new Dictionary<uint, Dictionary<ushort, Entry>>();

        private static readonly Dictionary<ushort, Dictionary<ushort, Dictionary<ushort, Entry>>> Live =
            new Dictionary<ushort, Dictionary<ushort, Dictionary<ushort, Entry>>>();

        internal static void ReplaceLive(ushort gateway, Dictionary<ushort, Dictionary<ushort, Entry>> rooms)
        {
            lock (Gate) Live[gateway] = rooms;
        }

        private static uint Key(ushort gateway, ushort room) { return ((uint)gateway << 16) | room; }

        internal static Dictionary<ushort, Entry> Get(ushort gateway, ushort room)
        {
            lock (Gate)
            {
                Dictionary<ushort, Dictionary<ushort, Entry>> liveRooms;
                Dictionary<ushort, Entry> liveRoom, manual;
                var result = Live.TryGetValue(gateway, out liveRooms) && liveRooms.TryGetValue(room, out liveRoom)
                    ? new Dictionary<ushort, Entry>(liveRoom) : new Dictionary<ushort, Entry>();
                if (Rooms.TryGetValue(Key(gateway, room), out manual))
                    foreach (var entry in manual) result[entry.Key] = entry.Value;
                return result;
            }
        }

        private static bool Parse(string text, bool shade, Dictionary<ushort, Entry> entries)
        {
            foreach (string field in (text ?? "").Split(';'))
            {
                if (String.IsNullOrWhiteSpace(field)) continue;
                int separator = field.IndexOf('=');
                ushort id;
                if (separator < 1 || !UInt16.TryParse(field.Substring(0, separator).Trim(),
                    NumberStyles.None, CultureInfo.InvariantCulture, out id) || id == 0 || entries.ContainsKey(id))
                    return false;
                string label = field.Substring(separator + 1).Trim();
                if (label.Length == 0 || label.Length > 80) return false;
                entries.Add(id, new Entry { Id = id, Shade = shade, Name = label });
            }
            return true;
        }

        // Additional symbols for the same room can register disjoint lists.
        public string Configure(ushort gateway, ushort room, string lights, string shades)
        {
            var entries = new Dictionary<ushort, Entry>();
            if (gateway == 0 || room == 0) return "Set nonzero Gateway_ID and Room_ID";
            if (!Parse(lights, false, entries) || !Parse(shades, true, entries))
                return "Invalid or duplicate device. Use ID=Label;ID=Label";
            lock (Gate)
            {
                uint key = Key(gateway, room);
                Dictionary<ushort, Entry> devices;
                if (!Rooms.TryGetValue(key, out devices)) devices = new Dictionary<ushort, Entry>();
                foreach (ushort id in entries.Keys)
                    if (devices.ContainsKey(id)) return "Duplicate device for room " + room;
                if (devices.Count + entries.Count > 128) return "Room device limit is 128";
                foreach (var entry in entries) devices.Add(entry.Key, entry.Value);
                Rooms[key] = devices;
            }
            return "Configured " + entries.Count + " devices for room " + room;
        }
    }

    public sealed class PanelRoomPacket : EventArgs
    {
        public ushort Stream { get; set; }
        public string Json { get; set; }
    }
}
