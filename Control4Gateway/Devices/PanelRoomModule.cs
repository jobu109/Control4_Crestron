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
        private readonly UiSerialFrames _actionsWire = new UiSerialFrames();
        private readonly UiSerialFrames _audioSessionsWire = new UiSerialFrames();
        private readonly UiSerialFrames _siteWire = new UiSerialFrames();
        private int _siteRequest;
        private bool _siteReady;
        // Whole-house lights and shades for every scoped room (Lighting/Shades pages).
        private readonly object _houseSync = new object();
        private readonly UiSerialFrames _houseWire = new UiSerialFrames();
        private GatewayCore _houseGateway;
        private Dictionary<ushort, Control4_Room_Devices.Entry> _houseDevices;
        private Dictionary<ushort, ushort> _houseRoomOf;
        private readonly Dictionary<ushort, int?> _houseLevels = new Dictionary<ushort, int?>();
        private EventHandler<LightStatusEventArgs> _houseLight;
        private EventHandler<ShadeStatusEventArgs> _houseShade;
        private bool _houseReady;
        // Opening the whole-house view registers every scoped device, so it runs off the SIMPL+
        // thread: a long CHANGE Request$ handler lets later requests overwrite the input unseen.
        private readonly object _houseWork = new object();
        private volatile int _houseRequest;
        private int _audioSessionsRequest;
        private bool _audioSessionsReady;
        private JObject _audioSessions;
        private int _audioSessionsGeneration;
        private string _audioSessionsRevision;
        private string _audioSessionsContent;
        // Membership, volume and mute only: a new track republishes without changing the revision,
        // so a zone command sent during a song change is not refused as stale.
        private string _audioSessionsCore;
        private GatewayCore _audioSessionsGateway;
        private System.Threading.Timer _audioSessionsTimer;
        private bool _audioSessionsReading, _audioSessionsDirty;
        private GatewayCore.PanelAction[] _actions = new GatewayCore.PanelAction[0];
        private int _actionRequest, _actionGeneration;
        private string _actionRevision;
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
                _actionRequest++;
                _actionRevision = null;
                CloseAudioSessions();
                _houseRequest++; // cancels a queued open
                lock (_houseWork) CloseHouse();
                _gateway = gatewayId;
                foreach (string entry in (roomIds ?? "").Split(','))
                {
                    ushort id;
                    if (UInt16.TryParse(entry.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out id) && id != 0)
                        if (_rooms.Add(id)) _roomOrder.Add(id);
                }
            }
        }

        /// <summary>
        /// Registers every scoped room's discovered lights and shades and publishes one catalog
        /// (room 0, kind house_devices). Later level changes are sent as {"h":1,"u":id,"v":level}.
        /// Registrations are reference-counted, so they coexist with the selected-room session.
        /// </summary>
        private void QueueHouse(bool open)
        {
            // Caller holds _requests; snapshot the scope so the worker never needs that lock.
            int request = ++_houseRequest;
            ushort gatewayId = _gateway;
            var roomOrder = new List<ushort>(_roomOrder);
            System.Threading.ThreadPool.QueueUserWorkItem(delegate
            {
                lock (_houseWork)
                {
                    if (request != _houseRequest) return; // superseded by a later open or close
                    try
                    {
                        if (open) OpenHouse(gatewayId, roomOrder);
                        else CloseHouse();
                    }
                    catch (Exception ex)
                    {
                        LogWarning("House view " + (open ? "open" : "close") + " failed: " + ex.Message);
                    }
                }
            });
        }

        private static void LogWarning(string message)
        {
            try { Crestron.SimplSharp.ErrorLog.Warn("Control4 Panel Room: " + message); } catch (Exception) { }
        }

        private Action<ushort, bool> _houseRelay;
        private readonly HashSet<ushort> _houseFanIds = new HashSet<ushort>();

        private void PublishFan(ushort id, ushort? speed)
        {
            lock (_houseSync)
                if (!_houseReady || !_houseFanIds.Contains(id)) return;
            var handler = PacketReady;
            if (handler != null) handler(this, new PanelRoomPacket { Stream = 0,
                Json = new JObject { ["h"] = 4, ["u"] = id, ["v"] = speed.HasValue ? new JValue(speed.Value) : new JValue((object)null) }
                    .ToString(Formatting.None) });
        }

        private readonly HashSet<ushort> _houseButtonIds = new HashSet<ushort>();

        private void PublishCountdown(ushort id, JObject state)
        {
            lock (_houseSync)
                if (!_houseReady || !_houseButtonIds.Contains(id)) return;
            var handler = PacketReady;
            if (handler != null) handler(this, new PanelRoomPacket { Stream = 0,
                Json = new JObject { ["h"] = 5, ["u"] = id, ["v"] = state == null ? new JValue((object)null) : state.DeepClone() }
                    .ToString(Formatting.None) });
        }

        private void PublishExperienceButton(ushort id, bool? on)
        {
            lock (_houseSync)
                if (!_houseReady || !_houseButtonIds.Contains(id)) return;
            var handler = PacketReady;
            if (handler != null) handler(this, new PanelRoomPacket { Stream = 0,
                Json = new JObject { ["h"] = 3, ["u"] = id, ["v"] = on.HasValue ? new JValue(on.Value) : new JValue((object)null) }
                    .ToString(Formatting.None) });
        }
        private readonly HashSet<ushort> _houseRelayIds = new HashSet<ushort>();

        private void PublishRelay(ushort id, bool closed)
        {
            lock (_houseSync)
                if (!_houseReady || !_houseRelayIds.Contains(id)) return;
            var handler = PacketReady;
            if (handler != null) handler(this, new PanelRoomPacket { Stream = 0,
                Json = new JObject { ["h"] = 2, ["u"] = id, ["v"] = closed }.ToString(Formatting.None) });
        }

        private void OpenHouse(ushort gatewayId, List<ushort> roomOrder)
        {
            CloseHouse();
            var gateway = GatewayRegistry.Get(gatewayId);
            var devices = new Dictionary<ushort, Control4_Room_Devices.Entry>();
            var roomOf = new Dictionary<ushort, ushort>();
            var rooms = new JArray();
            var missingLights = new List<ushort>();
            var missingShades = new List<ushort>();
            var light = new EventHandler<LightStatusEventArgs>((s, a) => HouseFeedback(a.ItemId, false, a.Level, a.Valid));
            var shade = new EventHandler<ShadeStatusEventArgs>((s, a) => HouseFeedback(a.ItemId, true, a.Position, a.Valid));
            lock (_houseSync)
            {
                foreach (ushort room in roomOrder)
                    foreach (var device in Control4_Room_Devices.Get(gatewayId, room).Values)
                        if (!devices.ContainsKey(device.Id)) { devices.Add(device.Id, device); roomOf.Add(device.Id, room); }
                _houseGateway = gateway;
                _houseDevices = devices;
                _houseRoomOf = roomOf;
                _houseLevels.Clear();
                _houseLight = light;
                _houseShade = shade;
                _houseRelay = PublishRelay;
            }
            gateway.LightChanged += light;
            gateway.ShadeChanged += shade;
            gateway.RelayChanged += _houseRelay;
            gateway.ExperienceButtonChanged += PublishExperienceButton;
            gateway.CountdownChanged += PublishCountdown;
            gateway.FanChanged += PublishFan;
            foreach (var device in devices.Values)
            {
                // No per-device queries here: ReadHouseLevels reads the missing ones serially.
                if (device.Shade) { gateway.RegisterShade(device.Id, true, false); missingShades.Add(device.Id); }
                else if (gateway.RegisterPanelLight(device.Id)) gateway.PublishCurrentLight(device.Id);
                else missingLights.Add(device.Id);
            }
            lock (_houseSync)
            {
                bool relayReady;
                JObject buttons, fans;
                var relayCatalog = gateway.PanelRelays(out relayReady, out buttons, out fans);
                foreach (ushort room in roomOrder)
                {
                    var relays = (JArray)relayCatalog[room.ToString(System.Globalization.CultureInfo.InvariantCulture)] ?? new JArray();
                    foreach (JObject relay in relays) _houseRelayIds.Add((ushort)relay["id"]);
                    var items = new JArray();
                    foreach (var device in devices.Values)
                    {
                        if (roomOf[device.Id] != room) continue;
                        int? level;
                        _houseLevels.TryGetValue(device.Id, out level);
                        items.Add(new JObject { ["id"] = device.Id, ["name"] = device.Name, ["shade"] = device.Shade,
                            ["value"] = level.HasValue ? new JValue(level.Value) : new JValue((object)null) });
                    }
                    var roomButtons = buttons[room.ToString(System.Globalization.CultureInfo.InvariantCulture)] as JArray ?? new JArray();
                    foreach (JObject button in roomButtons) _houseButtonIds.Add((ushort)button["id"]);
                    var roomFans = fans[room.ToString(System.Globalization.CultureInfo.InvariantCulture)] as JArray ?? new JArray();
                    foreach (JObject fan in roomFans) { _houseFanIds.Add((ushort)fan["id"]); fan.Remove("protocol"); }
                    rooms.Add(new JObject { ["id"] = room, ["devices"] = items, ["relays"] = relays, ["experience_buttons"] = roomButtons, ["fans"] = roomFans });
                }
                _houseWire.Set(0, "house_devices", new JObject { ["room_id"] = 0, ["rooms"] = rooms, ["relay_ready"] = relayReady }.ToString(Formatting.None));
                _houseReady = true;
            }
            PublishHouse(1);
            gateway.ReadHouseLevels(missingLights, missingShades);
        }

        private void CloseHouse()
        {
            GatewayCore gateway;
            Dictionary<ushort, Control4_Room_Devices.Entry> devices;
            EventHandler<LightStatusEventArgs> light;
            EventHandler<ShadeStatusEventArgs> shade;
            Action<ushort, bool> relay;
            lock (_houseSync)
            {
                gateway = _houseGateway; devices = _houseDevices; light = _houseLight; shade = _houseShade;
                _houseGateway = null; _houseDevices = null; _houseRoomOf = null; _houseLight = null; _houseShade = null;
                _houseReady = false;
                relay = _houseRelay;
                _houseRelay = null;
                _houseRelayIds.Clear();
                _houseButtonIds.Clear();
                _houseFanIds.Clear();
                _houseLevels.Clear();
            }
            if (gateway == null) return;
            gateway.LightChanged -= light;
            gateway.ShadeChanged -= shade;
            gateway.RelayChanged -= relay;
            gateway.ExperienceButtonChanged -= PublishExperienceButton;
            gateway.CountdownChanged -= PublishCountdown;
            gateway.FanChanged -= PublishFan;
            foreach (var device in devices.Values)
                if (device.Shade) gateway.UnregisterShade(device.Id);
                else gateway.UnregisterPanelLight(device.Id);
        }

        private void HouseFeedback(ushort id, bool shade, ushort value, ushort valid)
        {
            string packet;
            lock (_houseSync)
            {
                Control4_Room_Devices.Entry entry;
                if (_houseDevices == null || !_houseDevices.TryGetValue(id, out entry) || entry.Shade != shade) return;
                int? next = valid == 1 ? (int?)value : null;
                int? previous;
                if (_houseLevels.TryGetValue(id, out previous) && previous == next) return;
                _houseLevels[id] = next;
                if (!_houseReady) return; // Initial values go into the catalog itself.
                packet = new JObject { ["h"] = 1, ["u"] = id, ["v"] = next.HasValue ? new JValue(next.Value) : new JValue((object)null) }.ToString(Formatting.None);
            }
            var handler = PacketReady;
            if (handler != null) handler(this, new PanelRoomPacket { Stream = 0, Json = packet });
        }

        private void PublishHouse(ushort page)
        {
            string frame;
            lock (_houseSync)
            {
                if (!_houseReady || page > _houseWire.Count) return;
                frame = _houseWire.Read(ref page);
            }
            var handler = PacketReady;
            if (handler != null) handler(this, new PanelRoomPacket { Stream = 0, Json = frame });
        }

        /// <summary>Command for a device in any scoped room, checked against the whole-house catalog.</summary>
        private void HouseDeviceRequest(JObject body)
        {
            ushort room, id;
            GatewayCore gateway;
            Control4_Room_Devices.Entry device;
            if (!Number(body["room_id"], 1, UInt16.MaxValue, out room) || !_rooms.Contains(room) ||
                !Number(body["device_id"], 1, UInt16.MaxValue, out id)) return;
            lock (_houseSync)
            {
                ushort owner;
                gateway = _houseGateway;
                if (gateway == null || !_houseDevices.TryGetValue(id, out device) || !_houseRoomOf.TryGetValue(id, out owner) || owner != room) return;
            }
            DeviceAction(gateway, device, body);
        }

        private void ReadSite()
        {
            int request = ++_siteRequest;
            GatewayRegistry.Get(_gateway).ReadPanelSite((generation, result) =>
            {
                lock (_requests)
                {
                    if (request != _siteRequest) return;
                    _siteWire.Set(0, "site", result.ToString(Formatting.None));
                    _siteReady = true;
                    PublishSite(1);
                }
            });
        }

        private void PublishSite(ushort page)
        {
            if (!_siteReady || page > _siteWire.Count) return;
            var handler = PacketReady;
            if (handler != null) handler(this, new PanelRoomPacket { Stream = 0, Json = _siteWire.Read(ref page) });
        }

        private void OpenAudioSessions()
        {
            if (_audioSessionsGateway == null)
            {
                _audioSessionsGateway = GatewayRegistry.Get(_gateway);
                _audioSessionsTimer = new System.Threading.Timer(RefreshAudioSessions, null,
                    System.Threading.Timeout.Infinite, System.Threading.Timeout.Infinite);
                _audioSessionsGateway.AudioSessionReadbackHint += RequestAudioSessionRefresh;
            }
            ReadAudioSessions();
        }

        private void CloseAudioSessions()
        {
            _audioSessionsRequest++;
            _audioSessionsReady = false;
            _audioSessionsReading = false;
            _audioSessionsDirty = false;
            _audioSessionsContent = null;
            if (_audioSessionsGateway != null)
                _audioSessionsGateway.AudioSessionReadbackHint -= RequestAudioSessionRefresh;
            _audioSessionsGateway = null;
            if (_audioSessionsTimer != null) _audioSessionsTimer.Dispose();
            _audioSessionsTimer = null;
        }

        private void RequestAudioSessionRefresh()
        {
            // Director receive stays independent of SIMPL/panel request processing.
            System.Threading.ThreadPool.QueueUserWorkItem(delegate
            {
                lock (_requests)
                    if (_audioSessionsTimer != null)
                        _audioSessionsTimer.Change(500, System.Threading.Timeout.Infinite);
            });
        }

        private void RefreshAudioSessions(object state)
        {
            lock (_requests)
            {
                if (_audioSessionsGateway == null) return;
                if (_audioSessionsReading) { _audioSessionsDirty = true; return; }
                ReadAudioSessions(false);
            }
        }

        private void ReadAudioSessions(bool force = true)
        {
            int request = ++_audioSessionsRequest;
            if (force) _audioSessionsReady = false;
            _audioSessionsReading = true;
            GatewayRegistry.Get(_gateway).ReadPanelAudioSessions(_roomOrder.ToArray(), (generation, result) =>
            {
                lock (_requests)
                {
                    if (request != _audioSessionsRequest) return;
                    _audioSessionsReading = false;
                    string content = result.ToString(Formatting.None);
                    string core = AudioSessionsCore(result);
                    bool fresh = force || !_audioSessionsReady || _audioSessionsGeneration != generation;
                    if (fresh || !String.Equals(content, _audioSessionsContent, StringComparison.Ordinal))
                    {
                        _audioSessionsContent = content;
                        _audioSessions = result;
                        _audioSessionsGeneration = generation;
                        if (fresh || _audioSessionsRevision == null || !String.Equals(core, _audioSessionsCore, StringComparison.Ordinal))
                            _audioSessionsRevision = Guid.NewGuid().ToString("N").Substring(0, 16);
                        _audioSessionsCore = core;
                        result["revision"] = _audioSessionsRevision;
                        _audioSessionsWire.Set(0, "audio_sessions", result.ToString(Formatting.None));
                        _audioSessionsReady = true;
                        PublishAudioSessions(1);
                    }
                    if (_audioSessionsDirty && _audioSessionsTimer != null)
                    {
                        _audioSessionsDirty = false;
                        _audioSessionsTimer.Change(500, System.Threading.Timeout.Infinite);
                    }
                }
            });
        }

        /// <summary>The sessions document without now-playing metadata.</summary>
        internal static string AudioSessionsCore(JObject result)
        {
            var core = (JObject)result.DeepClone();
            var sessions = core["sessions"] as JArray;
            if (sessions != null) foreach (var session in sessions) if (session is JObject) ((JObject)session).Remove("now_playing");
            return core.ToString(Formatting.None);
        }

        /// <summary>
        /// After a zone command: read back now, and again shortly after. Director can apply a room's
        /// IS_MUTED / CURRENT_VOLUME change after the command returns, and no realtime event reports
        /// it to the sessions view, so a single immediate read can show the old value.
        /// </summary>
        private void ReadBackAudioSessions()
        {
            // Caller holds _requests.
            ReadAudioSessions(false);
            if (_audioSessionsTimer != null) _audioSessionsTimer.Change(1500, System.Threading.Timeout.Infinite);
        }

        private void TurnOffAllAudioZones(JObject body)
        {
            if (!_audioSessionsReady || body["revision"]?.Type != JTokenType.String ||
                (string)body["revision"] != _audioSessionsRevision) return;
            ushort[] targets = GatewayCore.ActivePanelAudioRooms(_audioSessions,
                new List<ushort>(_rooms).ToArray());
            if (targets.Length == 0) return;

            int request = ++_audioSessionsRequest;
            _audioSessionsReady = false;
            _audioSessionsReading = true;
            GatewayRegistry.Get(_gateway).TurnOffPanelAudioRooms(targets, _audioSessionsGeneration, () =>
            {
                lock (_requests)
                    if (request == _audioSessionsRequest) ReadBackAudioSessions();
            });
        }

        private void ControlAudioSessionRoom(string op, JObject body)
        {
            ushort room, value;
            int queue;
            if (!_audioSessionsReady || body["revision"] == null || body["revision"].Type != JTokenType.String ||
                body["revision"].ToString() != _audioSessionsRevision || !Number(body["room_id"], 1, UInt16.MaxValue, out room) ||
                !_rooms.Contains(room) || body["queue_id"] == null || body["queue_id"].Type != JTokenType.Integer ||
                !Int32.TryParse(body["queue_id"].ToString(), NumberStyles.None, CultureInfo.InvariantCulture, out queue) ||
                !GatewayCore.PanelAudioSessionContains(_audioSessions, queue, room)) return;
            // Room Off is how the host room leaves: the queue owner can't be removed from its own queue.
            bool roomOff = op == "audio_session_room_off";
            value = 0;
            if (!roomOff && !Number(body["value"], 0, op == "audio_session_volume" ? (ushort)100 : (ushort)1, out value)) return;

            string operation = roomOff ? "RoomOff" : op == "audio_session_volume" ? "SetVolume" : value == 1 ? "MuteOn" : "MuteOff";
            int request = _audioSessionsRequest;
            GatewayRegistry.Get(_gateway).ControlPanelAudioSessionRoom(room, queue, _audioSessionsGeneration, operation, value, () =>
            {
                lock (_requests)
                    if (request == _audioSessionsRequest) ReadBackAudioSessions();
            });
        }

        private void ChangeAudioSessionMembership(bool add, JObject body)
        {
            ushort room, owner;
            int queue;
            if (!_audioSessionsReady || body["revision"] == null || body["revision"].Type != JTokenType.String ||
                body["revision"].ToString() != _audioSessionsRevision || !Number(body["room_id"], 1, UInt16.MaxValue, out room) ||
                !_rooms.Contains(room) || body["queue_id"] == null || body["queue_id"].Type != JTokenType.Integer ||
                !Int32.TryParse(body["queue_id"].ToString(), NumberStyles.None, CultureInfo.InvariantCulture, out queue) ||
                !GatewayCore.CanChangePanelAudioMembership(_audioSessions, queue, room, add, out owner) ||
                !_rooms.Contains(owner)) return;

            int request = _audioSessionsRequest;
            _audioSessionsReady = false;
            _audioSessionsReading = true;
            GatewayRegistry.Get(_gateway).ChangePanelAudioMembership(room, owner, queue, _audioSessionsGeneration, add, () =>
            {
                lock (_requests)
                    if (request == _audioSessionsRequest) ReadBackAudioSessions();
            });
        }

        private void PublishAudioSessions(ushort page)
        {
            if (!_audioSessionsReady || page > _audioSessionsWire.Count) return;
            var handler = PacketReady;
            if (handler != null) handler(this, new PanelRoomPacket { Stream = 0, Json = _audioSessionsWire.Read(ref page) });
        }

        private void DiscoverActions()
        {
            int request = ++_actionRequest;
            _actionRevision = null;
            _actions = new GatewayCore.PanelAction[0];
            GatewayRegistry.Get(_gateway).DiscoverPanelActions((generation, actions) =>
            {
                lock (_requests)
                {
                    if (request != _actionRequest) return;
                    _actions = actions;
                    _actionGeneration = generation;
                    _actionRevision = Guid.NewGuid().ToString("N").Substring(0, 16);
                    var items = new JArray();
                    for (int i = 0; i < actions.Length; i++)
                        items.Add(new JObject { ["slot"] = i + 1, ["kind"] = actions[i].Kind, ["name"] = actions[i].Name });
                    _actionsWire.Set(0, "scenes", new JObject { ["room_id"] = 0,
                        ["revision"] = _actionRevision, ["actions"] = items }.ToString(Formatting.None));
                    PublishActions(1);
                }
            });
        }

        private void PublishActions(ushort page)
        {
            if (_actionRevision == null || page > _actionsWire.Count) return;
            var handler = PacketReady;
            if (handler != null) handler(this, new PanelRoomPacket { Stream = 0, Json = _actionsWire.Read(ref page) });
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
            ushort id;
            Control4_Room_Devices.Entry device;
            if (!Number(body["device_id"], 1, UInt16.MaxValue, out id) || !session.Devices.TryGetValue(id, out device)) return;
            DeviceAction(session.DeviceGateway, device, body);
        }

        private static void DeviceAction(GatewayCore gateway, Control4_Room_Devices.Entry device, JObject body)
        {
            ushort value;
            if (body["action"] == null || body["action"].Type != JTokenType.String) return;
            string action = (string)body["action"];
            if (!device.Shade)
            {
                if (action == "level" && Number(body["value"], 0, 100, out value))
                    gateway.SetPanelLightLevel(device.Id, value);
                return;
            }
            switch (action)
            {
                case "open": gateway.SendShadeCommand(device.Id, "Open", 0); break;
                case "close": gateway.SendShadeCommand(device.Id, "Close", 0); break;
                case "stop": gateway.SendShadeCommand(device.Id, "Stop", 0); break;
                case "level":
                    if (Number(body["value"], 0, 100, out value)) gateway.SendShadeCommand(device.Id, "SetLevel", value);
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
                if (op == "audio_sessions") { OpenAudioSessions(); return; }
                if (op == "audio_sessions_close") { CloseAudioSessions(); return; }
                if (op == "audio_sessions_off") { TurnOffAllAudioZones(body); return; }
                if (op == "audio_session_add" || op == "audio_session_remove")
                { ChangeAudioSessionMembership(op == "audio_session_add", body); return; }
                if (op == "audio_session_volume" || op == "audio_session_mute" || op == "audio_session_room_off")
                { ControlAudioSessionRoom(op, body); return; }
                if (op == "audio_sessions_page")
                {
                    if (Number(body["value"], 1, UInt16.MaxValue, out number)) PublishAudioSessions(number);
                    return;
                }
                if (op == "fan_speed")
                {
                    ushort fanRoom, fanId;
                    if (Number(body["room_id"], 1, UInt16.MaxValue, out fanRoom) && _rooms.Contains(fanRoom) &&
                        Number(body["device_id"], 1, UInt16.MaxValue, out fanId) && Number(body["value"], 0, 4, out number))
                        GatewayRegistry.Get(_gateway).SetPanelFanSpeed(fanRoom, fanId, number);
                    return;
                }
                if (op == "experience_select")
                {
                    ushort buttonRoom, buttonId;
                    if (Number(body["room_id"], 1, UInt16.MaxValue, out buttonRoom) && _rooms.Contains(buttonRoom) &&
                        Number(body["device_id"], 1, UInt16.MaxValue, out buttonId))
                        GatewayRegistry.Get(_gateway).SelectExperienceButton(buttonRoom, buttonId);
                    return;
                }
                if (op == "house_devices") { QueueHouse(true); return; }
                if (op == "house_devices_close") { QueueHouse(false); return; }
                if (op == "house_devices_page")
                {
                    if (Number(body["value"], 1, UInt16.MaxValue, out number)) PublishHouse(number);
                    return;
                }
                if (op == "relay")
                {
                    ushort relayRoom, relayId;
                    if (!Number(body["room_id"], 1, UInt16.MaxValue, out relayRoom) || !_rooms.Contains(relayRoom) ||
                        !Number(body["device_id"], 1, UInt16.MaxValue, out relayId)) return;
                    GatewayCore gateway;
                    lock (_houseSync)
                    {
                        if (!_houseReady || !_houseRelayIds.Contains(relayId)) return;
                        gateway = _houseGateway;
                    }
                    gateway?.SendRelayCommand(relayRoom, relayId, (string)body["action"]);
                    return;
                }
                if (op == "house_device") { HouseDeviceRequest(body); return; }
                if (op == "site") { ReadSite(); return; }
                if (op == "site_page")
                {
                    if (Number(body["value"], 1, UInt16.MaxValue, out number)) PublishSite(number);
                    return;
                }
                if (op == "scenes") { DiscoverActions(); return; }
                if (op == "scenes_page")
                {
                    if (Number(body["value"], 1, UInt16.MaxValue, out number)) PublishActions(number);
                    return;
                }
                if (op == "scene_activate")
                {
                    if (_actionRevision != null && body["revision"] != null &&
                        body["revision"].Type == JTokenType.String && body["revision"].ToString() == _actionRevision &&
                        Number(body["slot"], 1, (ushort)_actions.Length, out number))
                        GatewayRegistry.Get(_gateway).RunPanelAction(_actions[number - 1], _actionGeneration);
                    return;
                }
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
