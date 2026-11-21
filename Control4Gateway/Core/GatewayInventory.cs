using System;
using System.IO;
using System.Globalization;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Control4Crestron
{
    public sealed partial class GatewayCore
    {
        public event EventHandler<GatewayMessageEventArgs> InventoryExported;
        public event EventHandler<GatewayMessageEventArgs> SetupReportExported;
        public event EventHandler<GatewayMessageEventArgs> SetupJsonExported;
        private int _inventoryExportPending;
        private static readonly string[] InventoryCategories =
            { "lights", "motorization", "audio_video", "comfort", "sensors", "cameras" };

        private System.Collections.Generic.Dictionary<ushort, JObject> _panelRooms =
            new System.Collections.Generic.Dictionary<ushort, JObject>();

        private int _digitalMediaInventoryGeneration = -1, _digitalMediaInventoryId;

        private void CachePanelRoomNames(string json, int generation)
        {
            var items = JArray.Parse(json);
            var rooms = BuildPanelRoomCatalog(items);
            int digitalMedia = FindDigitalMedia(items);
            lock (_sync)
            {
                if (_generation != generation) return;
                _panelRooms = rooms;
                _digitalMediaInventoryGeneration = generation;
                _digitalMediaInventoryId = digitalMedia;
            }
        }

        internal static System.Collections.Generic.Dictionary<ushort, JObject> BuildPanelRoomCatalog(JArray items)
        {
            var floors = new System.Collections.Generic.Dictionary<ushort, JObject>();
            var rooms = new System.Collections.Generic.Dictionary<ushort, JObject>();
            foreach (var item in items)
            {
                ushort id;
                if (!(item is JObject) || ScalarText(item["type"]) != "4" ||
                    !UInt16.TryParse(ScalarText(item["id"] ?? item["item_id"]), out id) || id == 0) continue;
                floors[id] = (JObject)item;
            }
            foreach (var item in items)
            {
                ushort id;
                if (!(item is JObject) || ScalarText(item["type"]) != "8" ||
                    !UInt16.TryParse(ScalarText(item["id"] ?? item["item_id"]), out id) || id == 0) continue;
                string name = ScalarText(item["name"]);
                if (String.IsNullOrWhiteSpace(name) || name.Length > 160) continue;
                var room = new JObject { ["id"] = id, ["name"] = name };
                ushort floorId;
                if (UInt16.TryParse(ScalarText(item["floorId"]), out floorId) && floorId != 0)
                {
                    JObject floor;
                    floors.TryGetValue(floorId, out floor);
                    string floorName = ScalarText(floor?["name"]);
                    if (String.IsNullOrWhiteSpace(floorName)) floorName = ScalarText(item["floorName"]);
                    if (!String.IsNullOrWhiteSpace(floorName) && floorName.Length <= 160)
                    {
                        room["floor_id"] = floorId;
                        room["floor_name"] = floorName;
                        ushort floorOrder;
                        if (UInt16.TryParse(ScalarText(floor?["floorOrder"]), out floorOrder))
                            room["floor_order"] = floorOrder;
                    }
                }
                ushort roomOrder;
                if (UInt16.TryParse(ScalarText(item["roomOrder"]), out roomOrder)) room["room_order"] = roomOrder;
                rooms[id] = room;
            }
            return rooms;
        }

        internal string PanelRoomName(ushort room)
        {
            lock (_sync)
            {
                JObject entry;
                return _panelRooms.TryGetValue(room, out entry) ? (string)entry["name"] : "Room " + room;
            }
        }

        internal JArray PanelRoomCatalog(System.Collections.Generic.IEnumerable<ushort> scope)
        {
            var result = new JArray();
            lock (_sync)
                foreach (ushort room in scope)
                {
                    JObject entry;
                    result.Add(_panelRooms.TryGetValue(room, out entry) ? entry.DeepClone() :
                        new JObject { ["id"] = room, ["name"] = "Room " + room });
                }
            return result;
        }

        private int _relayGeneration = -1;
        private JObject _relayCatalog;
        private JObject _experienceButtons;
        private JObject _fans;
        internal event Action<ushort, ushort?> FanChanged;
        private static readonly System.Collections.Generic.HashSet<string> ExperienceButtonDrivers =
            new System.Collections.Generic.HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "experience-button-scenario.c4z",
                "reset_outlet.c4z",
                "christmas_tree.c4z",
                "grounded.c4z",
                "timer_countdown.c4z"
            };
        internal event Action<ushort, bool?> ExperienceButtonChanged;
        internal event Action<ushort, JObject> CountdownChanged;

        internal static bool? ParseExperienceButtonState(string response)
        {
            if (String.IsNullOrEmpty(response) || response.Length > 262144)
                throw new InvalidDataException("Invalid Experience Button variable response");
            bool? state = null;
            foreach (var row in JArray.Parse(response))
            {
                if (!(row is JObject) || ScalarText(row["varName"]) != "STATE") continue;
                var value = row["value"];
                if (value?.Type != JTokenType.String) return null;
                string text = (string)value;
                if (text != "On" && text != "Off") return null;
                bool on = text == "On";
                if (state.HasValue && state.Value != on) return null;
                state = on;
            }
            return state;
        }

        internal static JObject ParseCountdownState(string response)
        {
            if (String.IsNullOrEmpty(response) || response.Length > 262144)
                throw new InvalidDataException("Invalid Countdown variable response");
            int? running = null, remaining = null;
            foreach (var row in JArray.Parse(response))
            {
                if (!(row is JObject)) continue;
                string name = ScalarText(row["varName"]);
                if (name != "STATE" && name != "COUNTDOWN") continue;
                int value;
                if (!Int32.TryParse(ScalarText(row["value"]), NumberStyles.None, CultureInfo.InvariantCulture, out value)) return null;
                if (name == "STATE")
                {
                    if (value > 1 || (running.HasValue && running.Value != value)) return null;
                    running = value;
                }
                else
                {
                    if (remaining.HasValue && remaining.Value != value) return null;
                    remaining = value;
                }
            }
            if (!running.HasValue || !remaining.HasValue) return null;
            return new JObject { ["running"] = running.Value == 1, ["remaining"] = remaining.Value };
        }

        private void RouteExperienceButtonEvent(JToken message, int generation)
        {
            if (!(message is JObject) || ScalarText(message["evtName"]) != "OnDataToUI" ||
                (message["data"] as JObject)?["icon_description"]?.Type != JTokenType.String) return;
            ushort id;
            if (UInt16.TryParse(ScalarText(message["iddevice"]), out id)) QueueCatalogDeviceState(id, generation);
        }

        private readonly System.Collections.Generic.Dictionary<ushort, CatalogDeviceRead> _catalogDeviceReads =
            new System.Collections.Generic.Dictionary<ushort, CatalogDeviceRead>();

        private sealed class CatalogDeviceRead
        {
            internal int Generation;
            internal JObject Device;
            internal bool Reread;
        }

        private void QueueCatalogDeviceState(ushort id, int generation, bool fan = false)
        {
            CatalogDeviceRead work;
            lock (_sync)
            {
                if (_generation != generation || _relayGeneration != generation || !_online || _experienceButtons == null) return;
                if (_catalogDeviceReads.TryGetValue(id, out work) && work.Generation == generation)
                {
                    work.Reread = true;
                    return;
                }
                JObject button = null;
                foreach (var room in (fan ? _fans : _experienceButtons).Properties())
                {
                    foreach (JObject candidate in (JArray)room.Value)
                        if ((ushort)candidate["id"] == id) { button = candidate; break; }
                    if (button != null) break;
                }
                if (button == null) return;
                work = new CatalogDeviceRead { Generation = generation, Device = button };
                _catalogDeviceReads[id] = work;
            }
            Queue(delegate
            {
                try
                {
                    bool countdown = !fan && (string)work.Device["kind"] == "countdown";
                    string field = fan ? "speed" : countdown ? "countdown" : "on";
                    while (CurrentGeneration(generation))
                    {
                        JToken state = new JValue((object)null);
                        try
                        {
                            string host, token;
                            if (!GetConnection(out host, out token) || !CurrentGeneration(generation)) return;
                            string path = "/api/v1/items/" + id.ToString(CultureInfo.InvariantCulture) + "/variables";
                            string response = ReadWithTimeoutRetry(host, token, path, generation);
                            if (fan)
                            {
                                ushort? speed = ParseFanSpeed(response);
                                if (speed.HasValue) state = new JValue(speed.Value);
                            }
                            else if (countdown)
                            {
                                var reading = ParseCountdownState(response);
                                if (reading != null) state = reading;
                            }
                            else
                            {
                                bool? on = ParseExperienceButtonState(response);
                                if (on.HasValue) state = new JValue(on.Value);
                            }
                        }
                        catch (Exception ex)
                        {
                            InvalidateRejectedToken(ex);
                            if (CurrentGeneration(generation)) EmitMessage((fan ? "Fan " : "Experience Button ") + id + " state read failed: " + FailureKind(ex));
                        }
                        bool changed;
                        lock (_sync)
                        {
                            if (_generation != generation || !_online) return;
                            changed = !JToken.DeepEquals(work.Device[field], state);
                            work.Device[field] = state;
                        }
                        if (changed)
                        {
                            if (fan)
                            {
                                var handler = FanChanged;
                                if (handler != null) SafeEvent(delegate { handler(id, (ushort?)state); });
                            }
                            else if (countdown)
                            {
                                var handler = CountdownChanged;
                                if (handler != null) SafeEvent(delegate { handler(id, state as JObject); });
                            }
                            else
                            {
                                var handler = ExperienceButtonChanged;
                                if (handler != null) SafeEvent(delegate { handler(id, (bool?)state); });
                            }
                        }
                        lock (_sync)
                        {
                            if (_generation != generation || !_online) return;
                            if (!work.Reread)
                            {
                                _catalogDeviceReads.Remove(id);
                                return;
                            }
                            work.Reread = false;
                        }
                    }
                }
                finally
                {
                    lock (_sync)
                    {
                        CatalogDeviceRead pending;
                        if (_catalogDeviceReads.TryGetValue(id, out pending) && ReferenceEquals(work, pending))
                            _catalogDeviceReads.Remove(id);
                    }
                }
            });
        }
        private readonly System.Collections.Generic.Dictionary<ushort, bool> _relayEvents =
            new System.Collections.Generic.Dictionary<ushort, bool>();
        internal event Action<ushort, bool> RelayChanged;

        internal JObject PanelRelays(out bool ready, out JObject buttons, out JObject fans)
        {
            lock (_sync)
            {
                var result = new JObject();
                ready = _relayGeneration == _generation && _relayCatalog != null && _online;
                buttons = ready ? (JObject)_experienceButtons.DeepClone() : new JObject();
                fans = ready ? (JObject)_fans.DeepClone() : new JObject();
                if (!ready) return result;
                foreach (var room in _relayCatalog.Properties())
                {
                    var relays = (JArray)room.Value.DeepClone();
                    foreach (JObject relay in relays)
                    {
                        bool closed;
                        if (_relayEvents.TryGetValue((ushort)relay["id"], out closed))
                            relay["closed"] = closed;
                    }
                    result[room.Name] = relays;
                }
                return result;
            }
        }

        private void RouteRelayEvent(JToken message, int generation)
        {
            var packet = message as JObject;
            if (packet == null || ScalarText(packet["evtName"]) != "OnDataToUI") return;
            var state = (packet["data"] as JObject)?["state"];
            ushort id;
            if (state?.Type != JTokenType.Boolean || !UInt16.TryParse(ScalarText(packet["iddevice"]), out id) || id == 0)
                return;
            bool closed = (bool)state;
            lock (_sync)
            {
                if (_generation != generation || !_online) return;
                if (_relayGeneration != generation)
                {
                    _relayGeneration = generation;
                    _relayEvents.Clear();
                    _relayCatalog = null;
                }
                bool previous;
                if (_relayEvents.TryGetValue(id, out previous) && previous == closed) return;
                _relayEvents[id] = closed;
            }
            var handler = RelayChanged;
            if (handler != null) SafeEvent(delegate { handler(id, closed); });
        }

        internal void SendRelayCommand(ushort room, ushort id, string action)
        {
            string command;
            if (action == "open") command = "OPEN";
            else if (action == "close") command = "CLOSE";
            else return;

            int generation;
            lock (_sync) generation = _generation;
            Queue(delegate
            {
                try
                {
                    string host, token;
                    if (!GetConnection(out host, out token) || !CurrentGeneration(generation)) return;
                    lock (_sync)
                    {
                        if (_generation != generation || _relayGeneration != generation) return;
                        var relays = _relayCatalog?[room.ToString(CultureInfo.InvariantCulture)] as JArray;
                        if (relays == null || !System.Linq.Enumerable.Any(relays, relay => (ushort)relay["id"] == id)) return;
                    }
                    string body = new JObject { ["async"] = true, ["command"] = command, ["tParams"] = new JObject() }
                        .ToString(Formatting.None);
                    Request(host, token, "POST", "/api/v1/items/" + id.ToString(CultureInfo.InvariantCulture) + "/commands", body);
                    if (CurrentGeneration(generation)) EmitMessage("Relay " + id + " " + command + " accepted");
                }
                catch (Exception ex)
                {
                    if (!CurrentGeneration(generation)) return;
                    InvalidateRejectedToken(ex);
                    EmitMessage("Relay " + id + " " + command + " failed: " + FailureKind(ex));
                }
            });
        }

        private bool? ReadRelayClosed(string host, string token, ushort id, int generation)
        {
            try
            {
                var values = JArray.Parse(ReadWithTimeoutRetry(host, token,
                    "/api/v1/items/" + id.ToString(CultureInfo.InvariantCulture) + "/variables", generation));
                string observed = null;
                foreach (var value in values)
                {
                    var variable = value as JObject;
                    if (variable == null || ScalarText(variable["variableId"]) != "1000") continue;
                    string next = ScalarText(variable["value"]);
                    if ((next != "0" && next != "1") || (observed != null && observed != next)) return null;
                    observed = next;
                }
                return observed == null ? (bool?)null : observed == "1";
            }
            catch (Exception ex)
            {
                InvalidateRejectedToken(ex);
                if (CurrentGeneration(generation)) EmitMessage("Relay " + id + " readback failed: " + FailureKind(ex));
                return null;
            }
        }

        private void DiscoverAuthenticatedRoomDevices(int generation, string host, string token, string json)
        {
            Queue(delegate
            {
                string stage = "read inventory";
                int skipped = 0, detailReads = 0, bulk = 0;
                var stopwatch = System.Diagnostics.Stopwatch.StartNew();
                ushort firstSkipped = 0;
                try
                {
                    if (!CurrentGeneration(generation)) return;
                    EmitMessage("Room discovery started via Director");
                    var candidates = JArray.Parse(json);
                    stage = "select proxy candidates";
                    var rooms = new System.Collections.Generic.Dictionary<ushort, System.Collections.Generic.Dictionary<ushort, Control4_Room_Devices.Entry>>();
                    var seen = new System.Collections.Generic.HashSet<ushort>();
                    var relays = new JObject();
                    var buttons = new JObject();
                    var fans = new JObject();
                    var fanDrivers = new System.Collections.Generic.HashSet<string>(StringComparer.Ordinal);
                    var experienceDrivers = new System.Collections.Generic.Dictionary<string, bool>(StringComparer.Ordinal);
                    foreach (var driver in candidates)
                    {
                        if (!(driver is JObject) || ScalarText(driver["type"]) != "6") continue;
                        string filename = ScalarText(driver["filename"]);
                        string driverId = ScalarText(driver["id"] ?? driver["item_id"]);
                        if (!String.IsNullOrEmpty(driverId) && ExperienceButtonDrivers.Contains(filename))
                            experienceDrivers[driverId] = String.Equals(filename, "timer_countdown.c4z", StringComparison.OrdinalIgnoreCase);
                        if (String.Equals(filename, "fan_speed_controller.c4i", StringComparison.OrdinalIgnoreCase))
                            fanDrivers.Add(driverId);
                    }
                    int scanned = 0;
                    foreach (JToken value in candidates)
                    {
                        ushort id;
                        var candidate = value as JObject;
                        if (candidate == null || ScalarText(candidate["type"]) != "7" ||
                            !UInt16.TryParse(ScalarText(candidate["id"] ?? candidate["item_id"]), NumberStyles.None, CultureInfo.InvariantCulture, out id) || id == 0 || !seen.Add(id)) continue;
                        if (!CurrentGeneration(generation)) return;
                        scanned++;
                        if (scanned == 1 || scanned % 25 == 0)
                            EmitMessage("Room discovery checking proxy " + id + " (candidate " + scanned + ")");
                        JObject detail = candidate;
                        if (HasRoomCatalogMetadata(candidate) &&
                            ((!String.Equals(ScalarText(candidate["filename"]), "uibutton.c4i", StringComparison.OrdinalIgnoreCase) &&
                              !String.Equals(ScalarText(candidate["filename"]), "fan.c4i", StringComparison.OrdinalIgnoreCase)) ||
                            !String.IsNullOrEmpty(ScalarText(candidate["protocolId"])))) bulk++;
                        else
                        {
                            detailReads++;
                            stage = "request item " + id;
                            try
                            {
                                string response = ReadWithTimeoutRetry(host, token, "/api/v1/items/" + id.ToString(CultureInfo.InvariantCulture), generation);
                                stage = "parse item " + id;
                                detail = ServiceDetail(response, id);
                            }
                            catch (Exception ex) when (ex is ArgumentException || ex is InvalidDataException ||
                                FailureKind(ex) == "HTTP 404")
                            {
                                if (firstSkipped == 0) firstSkipped = id;
                                skipped++;
                                continue;
                            }
                        }
                        if (ScalarText(detail["type"]) != "7") continue;
                        string filename = ScalarText(detail["filename"]);
                        bool shade = String.Equals(filename, "blind.c4i", StringComparison.OrdinalIgnoreCase);
                        bool relay = String.Equals(filename, "relaysingle_relay_c4.c4i", StringComparison.OrdinalIgnoreCase);
                        bool button = String.Equals(filename, "uibutton.c4i", StringComparison.OrdinalIgnoreCase);
                        bool fan = String.Equals(filename, "fan.c4i", StringComparison.OrdinalIgnoreCase);
                        if (!fan && !button && !relay && !shade && !String.Equals(filename, "light.c4i", StringComparison.OrdinalIgnoreCase) &&
                            !String.Equals(filename, "light_v2.c4i", StringComparison.OrdinalIgnoreCase)) continue;
                        ushort room;
                        string label = ScalarText(detail["name"]);
                        if (!UInt16.TryParse(ScalarText(detail["roomId"]), out room) || room == 0 ||
                            String.IsNullOrWhiteSpace(label) || label.Length > 320) continue;
                        if (fan)
                        {
                            ushort protocol;
                            string protocolId = ScalarText(detail["protocolId"]);
                            if (!UInt16.TryParse(protocolId, out protocol) || !fanDrivers.Contains(protocolId)) continue;
                            string roomKey = room.ToString(CultureInfo.InvariantCulture);
                            var entries = fans[roomKey] as JArray;
                            if (entries == null) fans[roomKey] = entries = new JArray();
                            entries.Add(new JObject { ["id"] = id, ["name"] = label, ["protocol"] = protocol });
                            continue;
                        }
                        if (button)
                        {
                            string protocol = ScalarText(detail["protocolId"]);
                            bool countdown;
                            if (String.IsNullOrEmpty(protocol) || !experienceDrivers.TryGetValue(protocol, out countdown)) continue;
                            string roomKey = room.ToString(CultureInfo.InvariantCulture);
                            var entries = buttons[roomKey] as JArray;
                            if (entries == null) buttons[roomKey] = entries = new JArray();
                            var entry = new JObject { ["id"] = id, ["name"] = label };
                            if (countdown) entry["kind"] = "countdown";
                            entries.Add(entry);
                            continue;
                        }
                        if (relay)
                        {
                            string roomKey = room.ToString(CultureInfo.InvariantCulture);
                            var entries = relays[roomKey] as JArray;
                            if (entries == null) relays[roomKey] = entries = new JArray();
                            bool? closed = ReadRelayClosed(host, token, id, generation);
                            entries.Add(new JObject { ["id"] = id, ["name"] = label,
                                ["closed"] = closed.HasValue ? new JValue(closed.Value) : new JValue((object)null) });
                            continue;
                        }
                        System.Collections.Generic.Dictionary<ushort, Control4_Room_Devices.Entry> devices;
                        if (!rooms.TryGetValue(room, out devices))
                            rooms.Add(room, devices = new System.Collections.Generic.Dictionary<ushort, Control4_Room_Devices.Entry>());
                        devices.Add(id, new Control4_Room_Devices.Entry { Id = id, Name = label, Shade = shade });
                    }
                    stage = "publish catalog";
                    lock (_sync)
                    {
                        if (_generation != generation || !_online) return;
                        Control4_Room_Devices.ReplaceLive(_instance, rooms);
                        if (_relayGeneration != generation) _relayEvents.Clear();
                        _relayGeneration = generation;
                        _relayCatalog = relays;
                        _experienceButtons = buttons;
                        _fans = fans;
                    }
                    foreach (var room in buttons.Properties())
                        foreach (JObject button in (JArray)room.Value)
                            QueueCatalogDeviceState((ushort)button["id"], generation);
                    foreach (var room in fans.Properties())
                        foreach (JObject fan in (JArray)room.Value)
                            QueueCatalogDeviceState((ushort)fan["id"], generation, true);
                    int count = 0;
                    foreach (var room in rooms.Values) count += room.Count;
                    EmitMessage("Discovered " + count + " room devices via Director. Skipped " + skipped +
                        (skipped == 0 ? "" : " (first item " + firstSkipped + ")") +
                        ". Bulk " + bulk + ", detail reads " + detailReads + ", " + stopwatch.ElapsedMilliseconds + " ms.");
                }
                catch (Exception ex)
                {
                    if (!CurrentGeneration(generation)) return;
                    InvalidateRejectedToken(ex);
                    EmitMessage("Room discovery failed at " + stage + ": " + FailureKind(ex));
                }
            });
        }


        private static string ScalarText(JToken value)
        {
            return value != null && (value.Type == JTokenType.String || value.Type == JTokenType.Integer)
                ? value.ToString() : null;
        }

        private static bool HasRoomCatalogMetadata(JObject row)
        {
            ushort room;
            string filename = ScalarText(row["filename"]);
            string label = ScalarText(row["name"]);
            return !String.IsNullOrWhiteSpace(filename) && !String.IsNullOrWhiteSpace(label) && label.Length <= 320 &&
                UInt16.TryParse(ScalarText(row["roomId"]), NumberStyles.None, CultureInfo.InvariantCulture, out room) && room != 0;
        }

        public void ExportInventory()
        { ExportInventoryInternal(false); }
        public void ExportSetupReport()
        { ExportInventoryInternal(true); }
        private void ExportInventoryInternal(bool setup)
        {
            int gen;
            lock (_sync) { gen = _generation; }
            Queue(delegate
            {
                if (System.Threading.Interlocked.CompareExchange(ref _inventoryExportPending, 1, 0) != 0)
                { EmitMessage("Inventory export already running"); return; }
                try
                {
                    string host, token;
                    if (!GetConnection(out host, out token)) { EmitMessage("Connect before exporting inventory"); return; }
                    if (!CurrentGeneration(gen)) return;
                    EmitMessage("Exporting inventory JSON");
                    var errors = new JArray();
                    var groups = new JObject();
                    string rawInventory = Request(host, token, "GET", "/api/v1/items", null);
                    JArray all = InventoryItems(rawInventory);
                    foreach (string category in InventoryCategories)
                    {
                        if (!CurrentGeneration(gen)) return;
                        try { groups[category] = InventoryItems(Request(host, token, "GET", "/api/v1/categories/" + category, null)); }
                        catch (Exception ex)
                        {
                            InvalidateRejectedToken(ex);
                            lock (_sync) { if (!_online) throw; }
                            groups[category] = new JArray();
                            errors.Add(new JObject(new JProperty("category", category), new JProperty("error", FailureKind(ex))));
                        }
                    }
                    var inventory = new JObject(
                        new JProperty("generated_utc", DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture)),
                        new JProperty("gateway_id", _instance),
                        new JProperty("director_address", host),
                        new JProperty("id_note", "item_id is the Director REST item identifier. Use the appropriate proxy item for device modules; protocol driver items can have different IDs."),
                        new JProperty("all_items", all), new JProperty("categories", groups), new JProperty("errors", errors));
                    JObject capture = AudioFeedbackCaptureReport();
                    if (capture != null) inventory["audio_feedback_capture"] = capture;
                    inventory["room_transport_commands"] = TransportCommandReports();
                    inventory["room_state_inspections"] = RoomStateInspectionReports();
                    inventory["room_media_inspections"] = RoomMediaInspectionReports();
                    inventory["room_now_playing_inspections"] = NowPlayingReports();
                    inventory["room_media_catalogs"] = RoomCatalogReports();
                    if (setup)
                    {
                        EmitMessage("Setup report: discovering room Watch/Listen sources");
                        string ui = null;
                        try { ui = Request(host, token, "GET", "/api/v1/agents/ui_configuration", null); }
                        catch (Exception ex)
                        {
                            InvalidateRejectedToken(ex);
                            lock (_sync) { if (!_online) throw; }
                            errors.Add(new JObject(new JProperty("section", "room_sources"), new JProperty("error", FailureKind(ex))));
                        }
                        var mappings = new JArray();
                        lock (_sync)
                        {
                            foreach (var entry in _audioRooms)
                                mappings.Add(new JObject(new JProperty("room_id", entry.Key),
                                    new JProperty("watch_id", entry.Value.WatchId), new JProperty("listen_id", entry.Value.ListenId),
                                    new JProperty("listen_output_id", entry.Value.ListenOutputId)));
                        }
                        JObject report = BuildSetupReport(inventory, ui, mappings, _instance);
                        inventory["setup_report"] = report;
                        inventory["media_sessions_probe"] = InspectMediaSessions(all, host, token, gen);
                        inventory["media_sessions_events"] = RotateMediaSessionCapture(gen);
                        inventory["live_room_item_detail_probe"] = ProbeRoomItems(host, token, gen);
                        inventory["floor_layout_probe"] = ProbeFloorLayout(rawInventory);
                        inventory["light_feedback_probe"] = ProbeLightFeedback(host, token, gen);
                        inventory["site_weather_probe"] = ProbeSiteAndWeather(all, host, token, gen);
                        inventory["experience_button_probe"] = ProbeDeviceCommandsAndVariables(host, token, gen,
                            new ushort[] { 2218, 2219, 573, 574, 811, 812, 825, 826, 1595, 1596, 503, 504, 505, 506 });
                        inventory["door_lock_probe"] = ProbeDeviceCommandsAndVariables(host, token, gen,
                            new ushort[] { 328, 329, 507, 508 });
                        // These diagnostic IDs come from the demo site's inventory.
                        inventory["fan_speed_probe"] = ProbeDeviceCommandsAndVariables(host, token, gen,
                            new ushort[] { 399, 400, 401, 446, 447, 448, 2190, 2191 });
                        string reportBase = "/user/Control4Gateway_" + _instance + "_setup";
                        lock (_sync)
                        {
                            if (_generation != gen || !_online) return;
                            WriteSetupFile(reportBase + ".json", report.ToString(Formatting.Indented));
                            WriteSetupFile(reportBase + ".txt", (string)report["readable_text"]);
                        }
                    }
                    string path = "/user/Control4Gateway_" + _instance + "_inventory.json";
                    string temporary = path + ".tmp";
                    lock (_sync)
                    {
                        if (_generation != gen) return;
                        using (var writer = new Crestron.SimplSharp.CrestronIO.StreamWriter(temporary, false, new UTF8Encoding(false)))
                            writer.Write(inventory.ToString(Formatting.Indented));
                        if (Crestron.SimplSharp.CrestronIO.File.Exists(path)) Crestron.SimplSharp.CrestronIO.File.Delete(path);
                        Crestron.SimplSharp.CrestronIO.File.Move(temporary, path);
                    }
                    var handler = InventoryExported;
                    if (handler != null) SafeEvent(delegate { handler(this, new GatewayMessageEventArgs { Message = path }); });
                    if (setup)
                    {
                        string reportBase = "/user/Control4Gateway_" + _instance + "_setup";
                        var textHandler = SetupReportExported;
                        var jsonHandler = SetupJsonExported;
                        if (textHandler != null) SafeEvent(delegate { textHandler(this, new GatewayMessageEventArgs { Message = reportBase + ".txt" }); });
                        if (jsonHandler != null) SafeEvent(delegate { jsonHandler(this, new GatewayMessageEventArgs { Message = reportBase + ".json" }); });
                        EmitMessage("Setup report saved; unresolved mappings are labeled in the report");
                    }
                    EmitMessage("Inventory JSON saved: " + path + (errors.Count == 0 ? "" : " (some categories unavailable; see errors)"));
                }
                catch (Exception ex)
                {
                    if (CurrentGeneration(gen)) { InvalidateRejectedToken(ex); EmitMessage("Inventory export failed: " + FailureKind(ex)); }
                }
                finally { System.Threading.Interlocked.Exchange(ref _inventoryExportPending, 0); }
            });
        }

        private readonly JArray _mediaSessionEvents = new JArray();
        private int _mediaSessionCaptureGeneration = -1;
        private DateTime _mediaSessionCaptureUntil;
        private int _mediaSessionCaptureAgent, _mediaSessionCaptureDigitalMedia;
        private int _mediaSessionCaptureOmitted;

        private JObject RotateMediaSessionCapture(int generation)
        {
            lock (_sync)
            {
                bool previous = _mediaSessionCaptureGeneration == generation;
                var report = new JObject { ["had_capture"] = previous,
                    ["events"] = previous ? _mediaSessionEvents.DeepClone() : new JArray(),
                    ["omitted"] = previous ? _mediaSessionCaptureOmitted : 0 };
                _mediaSessionEvents.Clear();
                _mediaSessionCaptureOmitted = 0;
                _mediaSessionCaptureGeneration = generation;
                _mediaSessionCaptureUntil = DateTime.UtcNow.AddMinutes(5);
                return report;
            }
        }

        private void CaptureMediaSessionEvent(JToken message, int generation)
        {
            var obj = message as JObject;
            if (obj == null) return;
            lock (_sync)
            {
                if (_generation != generation || _mediaSessionCaptureGeneration != generation ||
                    DateTime.UtcNow > _mediaSessionCaptureUntil) return;
                string name = ScalarText(obj["evtName"]);
                string device = ScalarText(obj["iddevice"]);
                int id;
                if (name == null || !System.Text.RegularExpressions.Regex.IsMatch(name, "^[A-Za-z0-9_]{1,64}$") ||
                    SensitiveMediaKey(name)) return;
                bool agent = Int32.TryParse(device, NumberStyles.None, CultureInfo.InvariantCulture, out id) &&
                    id > 0 && (id == _mediaSessionCaptureAgent || id == _mediaSessionCaptureDigitalMedia);
                var data = obj["data"];
                int budget = 96;
                if (!agent && !HasMediaSessionMarker(name) && !ContainsMediaSessionMarker(data, 0, ref budget)) return;
                if (_mediaSessionEvents.Count >= 60) { _mediaSessionCaptureOmitted++; return; }
                budget = 72;
                _mediaSessionEvents.Add(new JObject { ["iddevice"] = id, ["event"] = name,
                    ["received_utc"] = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture),
                    ["data"] = NowPlayingShape(data, "", 0, ref budget) });
            }
        }

        private static bool HasMediaSessionMarker(string name)
        {
            name = name.Replace("_", "").ToLowerInvariant();
            return name.Contains("session") || name.Contains("group") || name.Contains("zone") ||
                name.Contains("queue") || name.Contains("roommap");
        }

        private static bool ContainsMediaSessionMarker(JToken token, int depth, ref int budget)
        {
            if (depth > 5 || --budget < 0) return false;
            var obj = token as JObject;
            if (obj != null)
            {
                foreach (var field in obj.Properties())
                    if (HasMediaSessionMarker(field.Name) || ContainsMediaSessionMarker(field.Value, depth + 1, ref budget))
                        return true;
            }
            else if (token is JArray)
                foreach (var item in (JArray)token)
                    if (ContainsMediaSessionMarker(item, depth + 1, ref budget)) return true;
            return false;
        }

        private JArray InspectMediaSessions(JArray inventory, string host, string token, int generation)
        {
            var report = new JArray();
            foreach (JObject item in inventory)
            {
                if (!CurrentGeneration(generation)) break;
                string name = ScalarText(item["name"]);
                string type = ScalarText(item["type"]);
                bool sessionAgent = type == "9" && String.Equals(name, "Media Sessions", StringComparison.OrdinalIgnoreCase);
                bool digitalMedia = type == "7" && String.Equals(name, "Digital Media", StringComparison.OrdinalIgnoreCase);
                if (!sessionAgent && !digitalMedia) continue;
                int id;
                if (!Int32.TryParse(ScalarText(item["item_id"]), NumberStyles.None, CultureInfo.InvariantCulture, out id) || id <= 0)
                    continue;
                lock (_sync)
                {
                    if (_generation != generation) break;
                    if (sessionAgent) _mediaSessionCaptureAgent = id;
                    if (digitalMedia) _mediaSessionCaptureDigitalMedia = id;
                }
                var agent = new JObject { ["item_id"] = id, ["name"] = name };
                report.Add(agent);
                string path = "/api/v1/items/" + id.ToString(CultureInfo.InvariantCulture);
                foreach (string resource in new[] { "commands", "variables" })
                {
                    if (!CurrentGeneration(generation)) break;
                    try
                    {
                        string response = ReadWithTimeoutRetry(host, token, path + "/" + resource, generation);
                        if (resource == "commands")
                            agent[resource] = BuildCommandMetadataReport(response, 200, id);
                        else
                        {
                            agent[resource] = InspectNowPlayingVariables(response);
                            if (digitalMedia) agent["session_values"] = DigitalMediaSessionValues(response);
                        }
                    }
                    catch (Exception ex)
                    {
                        InvalidateRejectedToken(ex);
                        agent[resource + "_error"] = FailureKind(ex);
                        lock (_sync) if (!_online) break;
                    }
                }
                if (report.Count >= 4) break;
            }
            return report;
        }

        internal void ReadPanelAudioSessions(ushort[] scope, Action<int, JObject> completed)
        {
            int generation;
            lock (_sync) generation = _generation;
            Queue(delegate
            {
                JObject result = new JObject { ["room_id"] = 0, ["available"] = false };
                try
                {
                    string host, token;
                    if (GetConnection(out host, out token) && CurrentGeneration(generation))
                        result = FetchPanelAudioSessions(host, token, generation, scope, true);
                }
                catch (Exception ex)
                {
                    InvalidateRejectedToken(ex);
                    EmitMessage("Whole house audio read failed: " + FailureKind(ex));
                }
                if (CurrentGeneration(generation)) completed(generation, result);
            });
        }

        private JObject FetchPanelAudioSessions(string host, string token, int generation, ushort[] scope, bool readLevels)
        {
            int digitalMedia;
            string variables = ReadDigitalMediaVariables(host, token, generation, out digitalMedia);
            var result = variables == null ? new JObject { ["room_id"] = 0, ["available"] = false }
                : BuildPanelAudioSessions(variables, scope);
            if (!readLevels || (bool?)result["available"] != true) return result;
            var active = new System.Collections.Generic.HashSet<ushort>();
            foreach (var session in (JArray)result["sessions"])
                foreach (var member in (JArray)session["room_ids"]) active.Add((ushort)(int)member);
            foreach (JObject room in (JArray)result["rooms"])
            {
                if (!CurrentGeneration(generation)) break;
                ushort id = (ushort)(int)room["id"];
                room["name"] = PanelRoomName(id);
                if (!active.Contains(id)) continue;
                string roomPath = "/api/v1/items/" + id.ToString(CultureInfo.InvariantCulture) + "/variables?varnames=";
                try
                {
                    ushort value;
                    if (TryAudioVariable(ReadWithTimeoutRetry(host, token, roomPath + "CURRENT_VOLUME", generation),
                        "CURRENT_VOLUME", out value)) room["volume"] = value;
                    if (TryAudioVariable(ReadWithTimeoutRetry(host, token, roomPath + "IS_MUTED", generation),
                        "IS_MUTED", out value)) room["muted"] = value != 0;
                }
                catch (Exception ex)
                {
                    InvalidateRejectedToken(ex);
                    EmitMessage("Whole house audio room " + id + " read failed: " + FailureKind(ex));
                    lock (_sync) if (!_online) break;
                }
            }
            AttachSessionNowPlaying(result, host, token, generation);
            return result;
        }

        /// <summary>
        /// Title, artist, album and artwork for each session's source: the realtime queue cache when
        /// it has a valid track, otherwise the owner room's CURRENT MEDIA INFO (no artwork).
        /// </summary>
        private void AttachSessionNowPlaying(JObject result, string host, string token, int generation)
        {
            var sessions = result["sessions"] as JArray;
            if (sessions == null) return;
            foreach (JObject session in sessions)
            {
                if (!CurrentGeneration(generation)) return;
                int source = (int?)session["source_item_id"] ?? 0;
                if (source <= 0) continue;
                JObject playing = null, queued = null;
                lock (_sync)
                {
                    PlayingQueue cached;
                    if (_playingQueues.TryGetValue(source, out cached) && cached.Data != null) queued = (JObject)cached.Data.DeepClone();
                }
                if (queued != null && (bool?)queued["valid"] == true) playing = queued;
                else
                {
                    var members = session["room_ids"] as JArray;
                    int owner = (int?)session["owner_room_id"] ?? (members != null && members.Count > 0 ? (int)members[0] : 0);
                    if (owner <= 0) continue;
                    try
                    {
                        string json = ReadWithTimeoutRetry(host, token, "/api/v1/items/" + owner.ToString(CultureInfo.InvariantCulture) +
                            "/variables?varnames=CURRENT%20MEDIA%20INFO", generation);
                        playing = ParsePlayingRoom(json, source);
                        if (playing != null) RestorePlayingArtwork(playing, queued);
                    }
                    catch (Exception ex)
                    {
                        InvalidateRejectedToken(ex);
                        EmitMessage("Whole house audio now playing read failed: " + FailureKind(ex));
                    }
                }
                var summary = SessionNowPlaying(playing);
                if (summary != null) session["now_playing"] = summary;
            }
        }

        internal static JObject SessionNowPlaying(JObject playing)
        {
            if (playing == null || (bool?)playing["valid"] != true) return null;
            var summary = new JObject();
            foreach (string key in new[] { "title", "artist", "album", "station", "artwork" })
            {
                string value = (string)playing[key];
                if (!String.IsNullOrWhiteSpace(value)) summary[key] = value;
            }
            return summary.Count > 0 ? summary : null;
        }

        private static int FindDigitalMedia(JArray inventory)
        {
            int digitalMedia = 0;
            foreach (var item in inventory)
            {
                int id;
                if (!(item is JObject) || ScalarText(item["type"]) != "7" || ScalarText(item["name"]) != "Digital Media" ||
                    !Int32.TryParse(ScalarText(item["id"] ?? item["item_id"]), NumberStyles.None,
                        CultureInfo.InvariantCulture, out id) || id <= 0) continue;
                if (digitalMedia != 0 && digitalMedia != id) return -1;
                digitalMedia = id;
            }
            return digitalMedia;
        }

        private int ResolveDigitalMedia(string host, string token, int generation, bool refresh)
        {
            lock (_sync)
            {
                if (_generation != generation) return 0;
                if (!refresh && _digitalMediaInventoryGeneration == generation &&
                    _digitalMediaInventoryId != 0) return _digitalMediaInventoryId;
            }
            int id = FindDigitalMedia(JArray.Parse(ReadWithTimeoutRetry(host, token, "/api/v1/items", generation)));
            lock (_sync)
            {
                if (_generation != generation) return 0;
                _digitalMediaInventoryGeneration = generation;
                _digitalMediaInventoryId = id;
            }
            return id;
        }

        private string ReadDigitalMediaVariables(string host, string token, int generation, out int digitalMedia)
        {
            digitalMedia = ResolveDigitalMedia(host, token, generation, false);
            if (digitalMedia < 0) throw new InvalidDataException("Ambiguous Digital Media item");
            if (digitalMedia == 0 || !CurrentGeneration(generation)) return null;
            string path = "/api/v1/items/" + digitalMedia.ToString(CultureInfo.InvariantCulture) + "/variables";
            try { return ReadWithTimeoutRetry(host, token, path, generation); }
            catch (Exception ex) when (FailureKind(ex) == "HTTP 404" && CurrentGeneration(generation))
            {
                int previous = digitalMedia;
                digitalMedia = ResolveDigitalMedia(host, token, generation, true);
                if (digitalMedia < 0) throw new InvalidDataException("Ambiguous Digital Media item");
                if (digitalMedia == previous) throw;
                if (digitalMedia == 0 || !CurrentGeneration(generation)) return null;
                path = "/api/v1/items/" + digitalMedia.ToString(CultureInfo.InvariantCulture) + "/variables";
                return ReadWithTimeoutRetry(host, token, path, generation);
            }
        }

        internal static bool CanChangePanelAudioMembership(JObject snapshot, int queue, ushort room,
            bool add, out ushort owner)
        {
            owner = 0;
            var sessions = snapshot == null ? null : snapshot["sessions"] as JArray;
            var rooms = snapshot == null ? null : snapshot["rooms"] as JArray;
            if (sessions == null || rooms == null || room == 0) return false;
            bool eligible = false;
            foreach (var entry in rooms)
                if ((int?)entry["id"] == room) { eligible = true; break; }
            if (!eligible) return false;
            bool member = false;
            foreach (var entry in sessions)
            {
                int? queueId = (int?)entry["queue_id"];
                bool contains = false;
                foreach (var id in (JArray)entry["room_ids"])
                    if ((int?)id == room) { contains = true; break; }
                if (queueId != queue)
                {
                    if (add && contains) return false;
                    continue;
                }
                int? candidate = (int?)entry["owner_room_id"];
                if (candidate.HasValue && candidate.Value > 0 && candidate.Value <= UInt16.MaxValue)
                    owner = (ushort)candidate.Value;
                member = contains;
            }
            return owner != 0 && owner != room && (add ? !member : member);
        }

        internal static string BuildAudioMembershipCommand(bool add, ushort owner, ushort room)
        {
            return new JObject { ["async"] = true,
                ["command"] = add ? "ADD_ROOMS_TO_SESSION" : "REMOVE_ROOMS_FROM_SESSION",
                ["tParams"] = new JObject { ["ROOM_ID"] = owner,
                    ["ROOM_ID_LIST"] = room.ToString(CultureInfo.InvariantCulture) } }.ToString(Formatting.None);
        }

        internal void ChangePanelAudioMembership(ushort room, ushort expectedOwner, int queue,
            int generation, bool add, Action refresh)
        {
            Queue(delegate
            {
                try
                {
                    string host, token;
                    if (!CurrentGeneration(generation) || !GetConnection(out host, out token) || !CurrentGeneration(generation)) return;
                    int digitalMedia;
                    string variables = ReadDigitalMediaVariables(host, token, generation, out digitalMedia);
                    var scope = new[] { expectedOwner, room };
                    ushort owner;
                    if (variables == null || !CanChangePanelAudioMembership(BuildPanelAudioSessions(variables, scope),
                        queue, room, add, out owner) || owner != expectedOwner || !CurrentGeneration(generation))
                    {
                        EmitMessage("Whole house audio membership command stopped: queue or room state changed");
                        return;
                    }
                    string path = "/api/v1/items/" + digitalMedia.ToString(CultureInfo.InvariantCulture) + "/commands";
                    Request(host, token, "POST", path, BuildAudioMembershipCommand(add, owner, room));
                    bool verified = false;
                    for (int attempt = 0; attempt < 3 && CurrentGeneration(generation); attempt++)
                    {
                        System.Threading.Thread.Sleep(350);
                        if (!CurrentGeneration(generation)) break;
                        string current = ReadWithTimeoutRetry(host, token,
                            "/api/v1/items/" + digitalMedia.ToString(CultureInfo.InvariantCulture) + "/variables", generation);
                        var snapshot = BuildPanelAudioSessions(current, scope);
                        if ((bool?)snapshot["available"] == true &&
                            PanelAudioSessionContains(snapshot, queue, room) == add)
                        { verified = true; break; }
                    }
                    if (!CurrentGeneration(generation)) return;
                    EmitMessage("Whole house audio room " + room + (add ? " add" : " remove") +
                        (verified ? " verified in queue " + queue : " accepted; membership change unconfirmed"));
                }
                catch (Exception ex)
                {
                    InvalidateRejectedToken(ex);
                    EmitMessage("Whole house audio membership request failed: " + FailureKind(ex));
                }
                finally { if (CurrentGeneration(generation)) refresh(); }
            });
        }

        internal void ControlPanelAudioSessionRoom(ushort room, int queue, int generation, string operation,
            ushort value, Action refresh)
        {
            Queue(delegate
            {
                try
                {
                    string host, token;
                    if (!CurrentGeneration(generation) || !GetConnection(out host, out token) || !CurrentGeneration(generation)) return;
                    var current = FetchPanelAudioSessions(host, token, generation, new[] { room }, false);
                    if (!PanelAudioSessionContains(current, queue, room))
                    {
                        EmitMessage("Whole house audio command stopped: room is no longer in the selected queue");
                        return;
                    }
                    if (!CurrentGeneration(generation)) return;
                    string path = "/api/v1/items/" + room.ToString(CultureInfo.InvariantCulture) + "/commands";
                    Request(host, token, "POST", path, BuildAudioCommand(operation, value));
                    EmitMessage("Whole house audio room " + room + " " + operation + " accepted");
                    if (CurrentGeneration(generation)) refresh();
                }
                catch (Exception ex)
                {
                    InvalidateRejectedToken(ex);
                    EmitMessage("Whole house audio command failed: " + FailureKind(ex));
                }
            });
        }

        internal static ushort[] ActivePanelAudioRooms(JObject snapshot, ushort[] scope)
        {
            var active = new System.Collections.Generic.HashSet<ushort>();
            var sessions = snapshot?["sessions"] as JArray;
            if (sessions != null)
                foreach (var session in sessions)
                {
                    var members = session["room_ids"] as JArray;
                    if (members != null)
                        foreach (var member in members) active.Add((ushort)member);
                }
            return Array.FindAll(scope, active.Remove);
        }

        internal static System.Collections.Generic.Dictionary<ushort, ushort> PlanPanelAudioShutdown(JObject snapshot, ushort[] targets)
        {
            var plan = new System.Collections.Generic.Dictionary<ushort, ushort>();
            var allowed = new System.Collections.Generic.HashSet<ushort>(targets);
            foreach (var session in snapshot?["sessions"] as JArray ?? new JArray())
            {
                ushort owner = (ushort?)session["owner_room_id"] ?? 0;
                foreach (var member in (JArray)session["room_ids"])
                {
                    ushort room = (ushort)member;
                    if (allowed.Contains(room) && !plan.ContainsKey(room))
                        plan.Add(room, room == owner ? (ushort)0 : owner);
                }
            }
            return plan;
        }

        internal void TurnOffPanelAudioRooms(ushort[] targets, int generation, Action refresh)
        {
            Queue(delegate
            {
                try
                {
                    string host, token;
                    if (!CurrentGeneration(generation) || !GetConnection(out host, out token) ||
                        !CurrentGeneration(generation)) return;
                    int digitalMedia;
                    string variables = ReadDigitalMediaVariables(host, token, generation, out digitalMedia);
                    if (variables == null || !CurrentGeneration(generation)) return;

                    var plan = PlanPanelAudioShutdown(BuildPanelAudioSessions(variables, null), targets);
                    string mediaPath = "/api/v1/items/" + digitalMedia.ToString(CultureInfo.InvariantCulture) + "/commands";
                    foreach (var entry in plan)
                        if (entry.Value != 0 && CurrentGeneration(generation))
                            SendPanelAudioShutdown(host, token, entry.Key, "remove", mediaPath,
                                BuildAudioMembershipCommand(false, entry.Value, entry.Key));

                    var joinedRooms = new System.Collections.Generic.List<ushort>();
                    foreach (var entry in plan)
                        if (entry.Value != 0) joinedRooms.Add(entry.Key);
                    if (joinedRooms.Count != 0)
                    {
                        bool removed = false;
                        var joined = joinedRooms.ToArray();
                        for (int attempt = 0; attempt < 3 && CurrentGeneration(generation); attempt++)
                        {
                            System.Threading.Thread.Sleep(350);
                            if (!CurrentGeneration(generation)) break;
                            var current = FetchPanelAudioSessions(host, token, generation, joined, false);
                            if ((bool?)current["available"] == true && ActivePanelAudioRooms(current, joined).Length == 0)
                            { removed = true; break; }
                        }
                        if (!removed && CurrentGeneration(generation))
                            EmitMessage("All Zones Off: joined-room removal unconfirmed");
                    }

                    string roomOff = BuildAudioCommand("RoomOff", 0);
                    foreach (var entry in plan)
                        if (entry.Value == 0 && CurrentGeneration(generation))
                            SendPanelAudioShutdown(host, token, entry.Key, "RoomOff",
                                "/api/v1/items/" + entry.Key.ToString(CultureInfo.InvariantCulture) + "/commands", roomOff);
                }
                catch (Exception ex)
                {
                    InvalidateRejectedToken(ex);
                    EmitMessage("All Zones Off failed: " + FailureKind(ex));
                }
                finally
                {
                    if (CurrentGeneration(generation)) refresh();
                }
            });
        }

        private void SendPanelAudioShutdown(string host, string token, ushort room, string operation, string path, string body)
        {
            try
            {
                Request(host, token, "POST", path, body);
                EmitMessage("All Zones Off: room " + room + " " + operation + " accepted");
            }
            catch (Exception ex)
            {
                InvalidateRejectedToken(ex);
                EmitMessage("All Zones Off: room " + room + " " + operation + " failed: " + FailureKind(ex));
            }
        }

        internal static bool PanelAudioSessionContains(JObject snapshot, int queue, ushort room)
        {
            var sessions = snapshot == null ? null : snapshot["sessions"] as JArray;
            if (sessions == null) return false;
            foreach (var entry in sessions)
                if ((int?)entry["queue_id"] == queue)
                    foreach (var member in (JArray)entry["room_ids"])
                        if ((int?)member == room) return true;
            return false;
        }

        private static JObject DigitalMediaSessionValues(string response)
        {
            return BuildPanelAudioSessions(response, null);
        }

        /// <summary>
        /// QUEUE_STATUS_V2 "queues" is an object while something plays. With nothing playing Director
        /// sends a string instead; empty text means no queues (Whole House Audio is still available
        /// with zero sessions), JSON text is parsed, and anything else stays unreadable.
        /// </summary>
        internal static JObject QueueStatusObject(JToken queues)
        {
            if (queues is JObject) return (JObject)queues;
            if (queues == null || queues.Type != JTokenType.String) return null;
            string text = ((string)queues).Trim();
            if (text.Length == 0) return new JObject();
            if (!text.StartsWith("{", StringComparison.Ordinal) || text.Length > 262144) return null;
            try { return JObject.Parse(text); }
            catch (JsonReaderException) { return null; }
        }

        private static JObject BuildPanelAudioSessions(string response, ushort[] scope)
        {
            if (String.IsNullOrEmpty(response) || response.Length > 262144)
                throw new InvalidDataException("Invalid Digital Media variable response");
            var variables = JArray.Parse(response);
            JObject queueStatus = null;
            JArray roomSettings = null;
            foreach (var row in variables)
            {
                if (!(row is JObject)) continue;
                string name = ScalarText(row["varName"]);
                var value = row["value"] as JObject;
                if (value == null) continue;
                if (name == "QUEUE_STATUS_V2") queueStatus = QueueStatusObject(value["queues"]);
                if (name == "ROOM_QUEUE_SETTINGS")
                {
                    var settings = value["room_queue_settings"] as JObject;
                    if (settings != null) roomSettings = settings["room_info"] as JArray;
                }
            }

            var allowed = scope == null ? null : new System.Collections.Generic.HashSet<ushort>(scope);
            var roomIds = new System.Collections.Generic.SortedSet<ushort>();
            var sessions = new JArray();
            if (roomSettings != null)
                foreach (var row in roomSettings)
                {
                    ushort id;
                    if (row is JObject && UInt16.TryParse(ScalarText(row["roomid"]), out id) &&
                        id != 0 && (allowed == null || allowed.Contains(id))) roomIds.Add(id);
                }
            var queueData = queueStatus == null ? null : queueStatus["queue"];
            var queues = queueData as JArray;
            if (queueData is JObject) queues = new JArray(queueData.DeepClone());
            else if (queueStatus != null && (queueData == null || queueData.Type == JTokenType.Null))
                queues = new JArray();
            var seenQueues = new System.Collections.Generic.HashSet<int>();
            if (queues != null)
                foreach (var row in queues)
                {
                    int id;
                    if (!(row is JObject) || !Int32.TryParse(ScalarText(row["id"]), out id) || id < 0 ||
                        !seenQueues.Add(id)) continue;
                    var container = row["rooms"] as JObject;
                    var memberData = container == null ? null : container["id"];
                    var members = memberData as JArray;
                    if (members == null && (memberData?.Type == JTokenType.Integer || memberData?.Type == JTokenType.String))
                        members = new JArray(memberData.DeepClone());
                    if (members == null) continue;
                    var visible = new System.Collections.Generic.SortedSet<ushort>();
                    foreach (var member in members)
                    {
                        ushort room;
                        if (UInt16.TryParse(ScalarText(member), out room) &&
                            room != 0 && (allowed == null || allowed.Contains(room))) visible.Add(room);
                    }
                    if (visible.Count == 0) continue;
                    foreach (ushort room in visible) roomIds.Add(room);
                    var session = new JObject { ["queue_id"] = id, ["room_ids"] = new JArray(visible) };
                    ushort owner;
                    if (UInt16.TryParse(ScalarText(row["owner"]), out owner) && visible.Contains(owner))
                        session["owner_room_id"] = owner;
                    int source;
                    if (Int32.TryParse(ScalarText(row["device_id"]), out source) && source > 0)
                        session["source_item_id"] = source;
                    string state = (ScalarText(row["state"]) ?? "").ToLowerInvariant();
                    if (state == "play" || state == "playing" || state == "pause" || state == "paused" ||
                        state == "stop" || state == "stopped" || state == "idle") session["state"] = state;
                    sessions.Add(session);
                }

            var rooms = new JArray();
            foreach (ushort id in roomIds) rooms.Add(new JObject { ["id"] = id });
            return new JObject { ["room_id"] = 0, ["available"] = queues != null,
                ["rooms"] = rooms, ["sessions"] = sessions };
        }

        internal sealed class PanelAction
        {
            internal string Name, Kind, Command, Parameter;
            internal ushort Agent;
            internal JToken Value;
        }

        internal void DiscoverPanelActions(Action<int, PanelAction[]> completed)
        {
            int generation;
            lock (_sync) generation = _generation;
            Queue(delegate
            {
                var resolved = new System.Collections.Generic.List<PanelAction>();
                try
                {
                    string host, token;
                    if (GetConnection(out host, out token) && CurrentGeneration(generation))
                    {
                        var agents = InventoryItems(ReadWithTimeoutRetry(host, token, "/api/v1/items", generation));
                        foreach (string kind in new[] { "light", "macro" })
                        {
                            string agentName = kind == "light" ? "Advanced Lighting" : "Macros";
                            string command = kind == "light" ? "ACTIVATE_SCENE" : "EXECUTE_MACRO";
                            string parameter = kind == "light" ? "SCENE_ID" : "id";
                            if (!CurrentGeneration(generation)) return;
                            ushort agentId = 0;
                            foreach (JObject row in agents)
                            {
                                if (ScalarText(row["type"]) != "9" ||
                                    !String.Equals(ScalarText(row["name"]), agentName, StringComparison.OrdinalIgnoreCase)) continue;
                                ushort candidate;
                                if (!UInt16.TryParse(ScalarText(row["item_id"]), out candidate) || candidate == 0) continue;
                                if (agentId != 0 && agentId != candidate) { agentId = 0; break; }
                                agentId = candidate;
                            }
                            if (agentId == 0) continue;
                            try
                            {
                                string path = "/api/v1/items/" + agentId.ToString(CultureInfo.InvariantCulture) + "/commands";
                                string metadata = ReadWithTimeoutRetry(host, token, path, generation);
                                ReadPanelActionOptions(metadata, kind, agentId, command, parameter,
                                    host, token, generation, resolved);
                            }
                            catch (Exception ex)
                            {
                                InvalidateRejectedToken(ex);
                                EmitMessage("Panel " + kind + " action discovery failed: " + FailureKind(ex));
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    InvalidateRejectedToken(ex);
                    EmitMessage("Panel action discovery failed: " + FailureKind(ex));
                }
                if (CurrentGeneration(generation)) completed(generation, resolved.ToArray());
            });
        }

        private static bool SupportsActionId(string type)
        {
            switch ((type ?? "").ToLowerInvariant())
            {
                case "string": case "str": case "integer": case "int": case "uint": case "number": return true;
                default: return false;
            }
        }

        internal void RunPanelAction(PanelAction action, int generation)
        {
            Queue(delegate
            {
                try
                {
                    string host, token;
                    if (!CurrentGeneration(generation) || !GetConnection(out host, out token) ||
                        !CurrentGeneration(generation))
                    { EmitMessage("Panel action stopped: refresh the action list"); return; }
                    var body = new JObject { ["async"] = true, ["command"] = action.Command,
                        ["tParams"] = new JObject { [action.Parameter] = action.Value.DeepClone() } };
                    int status;
                    string path = "/api/v1/items/" + action.Agent.ToString(CultureInfo.InvariantCulture) + "/commands";
                    string response = Request(host, token, "POST", path, body.ToString(Formatting.None), out status);
                    EmitMessage(action.Name + " request HTTP " + status + ", " + SceneCommandResponse(response));
                }
                catch (Exception ex)
                {
                    if (!CurrentGeneration(generation)) return;
                    InvalidateRejectedToken(ex);
                    EmitMessage("Panel action request failed: " + FailureKind(ex));
                }
            });
        }

        private static string SceneCommandResponse(string response)
        {
            if (String.IsNullOrWhiteSpace(response)) return "empty response";
            if (response.Length > 16384) return "large response";
            try
            {
                var body = JToken.Parse(response);
                var obj = body as JObject;
                if (obj != null)
                {
                    foreach (string key in new[] { "success", "ok", "result", "error", "status" })
                    {
                        var field = obj[key];
                        if (field == null) continue;
                        if (field.Type == JTokenType.Boolean || field.Type == JTokenType.Integer)
                            return key + "=" + field.ToString();
                        return key + " field (" + field.Type + ")";
                    }
                }
                return "response type " + body.Type;
            }
            catch (JsonReaderException) { return "non-JSON response"; }
        }

        private void ReadPanelActionOptions(string metadata, string kind, ushort agentId, string commandName,
            string parameterName, string host, string token, int generation,
            System.Collections.Generic.List<PanelAction> actions)
        {
            if (String.IsNullOrEmpty(metadata) || metadata.Length > 262144)
                throw new InvalidDataException("Invalid scene command metadata size");
            var commands = JToken.Parse(metadata) as JArray;
            if (commands == null) return;
            foreach (var command in commands)
            {
                if (!(command is JObject) || ScalarText(command["command"]) != commandName) continue;
                var parameters = command["params"] as JArray;
                if (parameters == null) continue;
                foreach (var parameter in parameters)
                {
                    if (!(parameter is JObject) || ScalarText(parameter["name"]) != parameterName) continue;
                    var source = parameter["valueSrc"] as JObject;
                    string path = source == null ? null : ScalarText(source["path"]);
                    string field = ScalarText(parameter["valueField"]);
                    string display = ScalarText(parameter["valueDisplay"]);
                    string type = ScalarText(parameter["valueType"]);
                    if (source == null || !String.Equals(ScalarText(source["method"]), "GET", StringComparison.OrdinalIgnoreCase) ||
                        path == null || !System.Text.RegularExpressions.Regex.IsMatch(path, "^/api/v1/agents/[A-Za-z0-9_-]{1,80}$") ||
                        !SupportsActionId(type) || !System.Text.RegularExpressions.Regex.IsMatch(field ?? "", "^[A-Za-z_][A-Za-z0-9_]{0,47}$"))
                        throw new InvalidDataException("Unrecognized scene option source");
                    if (!CurrentGeneration(generation)) return;
                    string data = ReadWithTimeoutRetry(host, token, path, generation);
                    if (String.IsNullOrEmpty(data) || data.Length > 262144)
                        throw new InvalidDataException("Invalid scene list size");
                    var options = JToken.Parse(data) as JArray;
                    if (options == null) throw new InvalidDataException("Expected scene list");
                    var found = new System.Collections.Generic.Dictionary<string, PanelAction>(StringComparer.Ordinal);
                    var conflicts = new System.Collections.Generic.HashSet<string>(StringComparer.Ordinal);
                    foreach (var option in options)
                    {
                        var row = option as JObject;
                        if (row == null) continue;
                        string name = SceneOptionName(row, field, display);
                        string id = ScalarText(row[field]);
                        if (String.IsNullOrWhiteSpace(name) || name.Length > 160 || id == null ||
                            !System.Text.RegularExpressions.Regex.IsMatch(id, "^[A-Za-z0-9_{}-]{1,64}$")) continue;
                        JToken value;
                        try { value = NativeMediaTypedValue(new JValue(type), row[field]); }
                        catch (InvalidDataException) { continue; }
                        string key = value.ToString(Formatting.None);
                        PanelAction previous;
                        if (found.TryGetValue(key, out previous))
                        {
                            if (!String.Equals(previous.Name, name, StringComparison.Ordinal)) conflicts.Add(key);
                            continue;
                        }
                        if (found.Count >= 512) throw new InvalidDataException("Too many scene options");
                        found.Add(key, new PanelAction { Name = name, Kind = kind, Agent = agentId,
                            Command = commandName, Parameter = parameterName, Value = value });
                    }
                    foreach (var entry in found)
                        if (!conflicts.Contains(entry.Key)) actions.Add(entry.Value);
                    return;
                }
            }
        }

        private static string SceneOptionName(JObject row, string idField, string display)
        {
            if (!String.IsNullOrEmpty(display))
            {
                if (System.Text.RegularExpressions.Regex.IsMatch(display, "^[A-Za-z_][A-Za-z0-9_]{0,47}$"))
                {
                    string declared = ScalarText(row[display]);
                    if (!String.IsNullOrWhiteSpace(declared)) return declared;
                }
                var fields = System.Text.RegularExpressions.Regex.Matches(display, "[A-Za-z_][A-Za-z0-9_]{0,47}");
                foreach (System.Text.RegularExpressions.Match match in fields)
                {
                    if (match.Value == idField || SensitiveMediaKey(match.Value)) continue;
                    var value = row[match.Value];
                    if (value != null && value.Type == JTokenType.String) return ScalarText(value);
                }
            }
            foreach (string field in new[] { "name", "scene_name", "macro_name", "label", "display_name" })
            {
                if (field == idField) continue;
                var value = row[field];
                if (value != null && value.Type == JTokenType.String) return ScalarText(value);
            }
            return null;
        }

        internal void SelectExperienceButton(ushort room, ushort id)
        {
            int generation;
            lock (_sync) generation = _generation;
            Queue(delegate
            {
                try
                {
                    string host, token;
                    if (!GetConnection(out host, out token) || !CurrentGeneration(generation)) return;
                    lock (_sync)
                    {
                        if (_generation != generation || _relayGeneration != generation) return;
                        var buttons = _experienceButtons?[room.ToString(CultureInfo.InvariantCulture)] as JArray;
                        if (buttons == null || !System.Linq.Enumerable.Any(buttons, button => (ushort)button["id"] == id)) return;
                    }
                    string payload = new JObject { ["async"] = true, ["command"] = "Select", ["tParams"] = new JObject() }
                        .ToString(Formatting.None);
                    Request(host, token, "POST", "/api/v1/items/" + id.ToString(CultureInfo.InvariantCulture) + "/commands", payload);
                    if (CurrentGeneration(generation)) EmitMessage("Experience Button " + id + " Select accepted");
                }
                catch (Exception ex)
                {
                    if (!CurrentGeneration(generation)) return;
                    InvalidateRejectedToken(ex);
                    EmitMessage("Experience Button " + id + " Select failed: " + FailureKind(ex));
                }
            });
        }

        internal static ushort? ParseFanSpeed(string response)
        {
            if (String.IsNullOrEmpty(response) || response.Length > 262144)
                throw new InvalidDataException("Invalid fan variable response");
            string on = null, speed = null;
            foreach (var row in JArray.Parse(response))
            {
                if (!(row is JObject)) continue;
                string name = ScalarText(row["varName"]), value = ScalarText(row["value"]);
                if (name == "IS_ON")
                {
                    if (on != null && on != value) return null;
                    on = value;
                }
                else if (name == "CURRENT_SPEED")
                {
                    if (speed != null && speed != value) return null;
                    speed = value;
                }
            }
            if (on == "0") return 0;
            ushort level;
            return on == "1" && UInt16.TryParse(speed, NumberStyles.None, CultureInfo.InvariantCulture, out level) &&
                level >= 1 && level <= 4 ? (ushort?)level : null;
        }

        private void RouteFanEvent(JToken message, int generation)
        {
            ushort item;
            if (!(message is JObject) || ScalarText(message["evtName"]) != "OnDataToUI" ||
                !(message["data"] is JObject) || !UInt16.TryParse(ScalarText(message["iddevice"]), out item)) return;
            var targets = new System.Collections.Generic.List<ushort>();
            lock (_sync)
            {
                if (_generation != generation || _relayGeneration != generation || _fans == null) return;
                foreach (var room in _fans.Properties())
                    foreach (JObject fan in (JArray)room.Value)
                        if ((ushort)fan["id"] == item || (ushort)fan["protocol"] == item)
                            targets.Add((ushort)fan["id"]);
            }
            foreach (ushort id in targets) QueueCatalogDeviceState(id, generation, true);
        }

        internal static string BuildFanSpeedCommand(ushort speed)
        {
            if (speed > 4) throw new ArgumentOutOfRangeException("speed");
            return new JObject { ["async"] = true, ["command"] = "SET_SPEED",
                ["tParams"] = new JObject { ["SPEED"] = speed.ToString(CultureInfo.InvariantCulture) } }
                .ToString(Formatting.None);
        }

        internal void SetPanelFanSpeed(ushort room, ushort id, ushort speed)
        {
            string payload = BuildFanSpeedCommand(speed);
            int generation;
            lock (_sync) generation = _generation;
            Queue(delegate
            {
                try
                {
                    string host, token;
                    if (!GetConnection(out host, out token) || !CurrentGeneration(generation)) return;
                    lock (_sync)
                    {
                        if (_generation != generation || _relayGeneration != generation) return;
                        var fans = _fans?[room.ToString(CultureInfo.InvariantCulture)] as JArray;
                        if (fans == null || !System.Linq.Enumerable.Any(fans, fan => (ushort)fan["id"] == id)) return;
                    }
                    string path = "/api/v1/items/" + id.ToString(CultureInfo.InvariantCulture);
                    if (!CurrentGeneration(generation)) return;
                    Request(host, token, "POST", path + "/commands", payload);
                    if (!CurrentGeneration(generation)) return;
                    EmitMessage("Fan " + id + " SET_SPEED " + speed + " accepted");
                    System.Threading.Thread.Sleep(350);
                    QueueCatalogDeviceState(id, generation, true);
                }
                catch (Exception ex)
                {
                    if (!CurrentGeneration(generation)) return;
                    InvalidateRejectedToken(ex);
                    EmitMessage("Fan " + id + " speed command failed: " + FailureKind(ex));
                }
            });
        }

        private static bool IsDoorLockProbeItem(ushort id)
        {
            return id == 328 || id == 329 || id == 503 || id == 504 ||
                id == 505 || id == 506 || id == 507 || id == 508;
        }

        private static bool IsDoorLockProbeState(string value)
        {
            return value != null && System.Text.RegularExpressions.Regex.IsMatch(value.Trim(),
                "^(lock|locked|unlock|unlocked|locking|unlocking|open|closed|jammed|enabled|disabled|unknown|not[- _]locked)$",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        }

        private JArray ProbeDeviceCommandsAndVariables(string host, string token, int generation, ushort[] ids)
        {
            var result = new JArray();
            foreach (ushort id in ids)
            {
                if (!CurrentGeneration(generation)) break;
                var entry = new JObject { ["item_id"] = id };
                result.Add(entry);
                string path = "/api/v1/items/" + id.ToString(CultureInfo.InvariantCulture);
                try
                {
                    var detail = ServiceDetail(ReadWithTimeoutRetry(host, token, path, generation), id);
                    foreach (string field in new[] { "name", "filename", "type", "roomId", "protocolId" })
                        CopyInventoryField(detail, entry, field);
                }
                catch (Exception ex)
                {
                    InvalidateRejectedToken(ex);
                    entry["detail_error"] = FailureKind(ex);
                }

                if (!CurrentGeneration(generation)) break;
                try
                {
                    string response = ReadWithTimeoutRetry(host, token, path + "/commands", generation);
                    entry["commands"] = BuildCommandMetadataReport(response, 200, id);
                }
                catch (Exception ex)
                {
                    InvalidateRejectedToken(ex);
                    entry["commands_error"] = FailureKind(ex);
                }

                if (!CurrentGeneration(generation)) break;
                try
                {
                    string response = ReadWithTimeoutRetry(host, token, path + "/variables", generation);
                    if (String.IsNullOrEmpty(response) || response.Length > 262144)
                        throw new InvalidDataException("Invalid diagnostic variable response");
                    var variables = new JArray();
                    foreach (var variable in JArray.Parse(response))
                    {
                        if (!(variable is JObject) || variables.Count >= 64) continue;
                        var row = new JObject();
                        CopyInventoryField((JObject)variable, row, "variableId");
                        foreach (string key in new[] { "name", "varName" })
                        {
                            string name = ScalarText(variable[key]);
                            if (name != null && !SensitiveMediaKey(name) &&
                                System.Text.RegularExpressions.Regex.IsMatch(name, "^[A-Za-z0-9_ -]{1,64}$"))
                                row[key] = name;
                        }
                        var value = variable["value"];
                        string text = ScalarText(value);
                        if (value?.Type == JTokenType.Boolean || value?.Type == JTokenType.Integer)
                            row["value"] = value.DeepClone();
                        else if (String.Equals(text, "On", StringComparison.OrdinalIgnoreCase) ||
                            String.Equals(text, "Off", StringComparison.OrdinalIgnoreCase) ||
                            String.Equals(text, "Locked", StringComparison.OrdinalIgnoreCase) ||
                            String.Equals(text, "Unlocked", StringComparison.OrdinalIgnoreCase) ||
                            (text != null && System.Text.RegularExpressions.Regex.IsMatch(text, "^(High|Med[- _]?High|Medium[- _]?High|Medium|Low)$",
                                System.Text.RegularExpressions.RegexOptions.IgnoreCase)) ||
                            (IsDoorLockProbeItem(id) && IsDoorLockProbeState(text)))
                            row["value"] = text;
                        variables.Add(row);
                    }
                    entry["variables"] = variables;
                }
                catch (Exception ex)
                {
                    InvalidateRejectedToken(ex);
                    entry["variables_error"] = FailureKind(ex);
                }
            }
            return result;
        }

        private static JObject ProbeFloorLayout(string response)
        {
            var floors = new JArray();
            var rooms = new JArray();
            foreach (var row in JArray.Parse(response))
            {
                var item = row as JObject;
                if (item == null) continue;
                string type = ScalarText(item["type"]);
                if (type != "4" && type != "8") continue;
                var entry = new JObject();
                CopyInventoryField(item, entry, item["id"] == null ? "item_id" : "id");
                foreach (string field in new[] { "name", "typeName", "parentId", "floorId", "floorName", "floorOrder", "roomOrder" })
                    CopyInventoryField(item, entry, field);
                if (type == "4") floors.Add(entry);
                else rooms.Add(entry);
            }
            return new JObject { ["floors"] = floors, ["rooms"] = rooms };
        }

        private JArray ProbeRoomItems(string host, string token, int generation)
        {
            var result = new JArray();
            foreach (int id in new[] { 13, 212, 424, 226, 437, 371 })
            {
                if (!CurrentGeneration(generation)) break;
                var entry = new JObject { ["item_id"] = id, ["probe_revision"] = 2 };
                result.Add(entry);
                try
                {
                    string path = "/api/v1/items/" + id.ToString(CultureInfo.InvariantCulture);
                    string json = Request(host, token, "GET", path, null);
                    if (String.IsNullOrEmpty(json) || json.Length > 1048576)
                        throw new InvalidDataException("Invalid item detail response size");
                    entry["stage"] = "parse_detail";
                    var item = ServiceDetail(json, (ushort)id);
                    entry["http_result"] = "success";
                    var keys = new JArray();
                    foreach (JProperty property in item.Properties())
                        if (keys.Count < 48 && !SensitiveMediaKey(property.Name) && System.Text.RegularExpressions.Regex.IsMatch(property.Name, "^[A-Za-z_][A-Za-z0-9_]{0,63}$")) keys.Add(property.Name);
                    entry["fields"] = keys;
                    CopyInventoryField(item, entry, "room_id");
                    CopyInventoryField(item, entry, "room_name");
                    CopyInventoryField(item, entry, "parent_id");
                    CopyInventoryField(item, entry, "location_id");
                    foreach (string field in new[] { "roomId", "locationId", "parentId", "driver_id", "driverId", "type", "floorId", "floorName", "floorOrder", "roomOrder" })
                        CopyInventoryField(item, entry, field);
                    entry.Remove("stage");
                }
                catch (Exception ex)
                {
                    InvalidateRejectedToken(ex);
                    entry["error"] = FailureKind(ex);
                    lock (_sync) if (!_online) break;
                }
            }
            return result;
        }

        private readonly JArray _lightProbeEvents = new JArray();
        private int _lightProbeGeneration = -1;
        private readonly System.Collections.Generic.HashSet<ushort> _lightProbeIds =
            new System.Collections.Generic.HashSet<ushort>();

        private JArray ProbeLightFeedback(string host, string token, int gen)
        {
            var output = new JArray();
            var observedIds = new System.Collections.Generic.HashSet<ushort>();
            // Item 606 is the Composer-verified relay for this diagnostic capture only.
            foreach (ushort id in new ushort[] { 437, 524, 602, 1932, 606 })
            {
                if (!CurrentGeneration(gen)) break;
                var row = new JObject { ["proxy_id"] = id };
                output.Add(row);
                try
                {
                    observedIds.Add(id);
                    string path = "/api/v1/items/" + id.ToString(CultureInfo.InvariantCulture);
                    JObject detail = ServiceDetail(ReadWithTimeoutRetry(host, token, path, gen), id);
                    ushort protocol;
                    if (UInt16.TryParse(ScalarText(detail["protocolId"]), out protocol) && protocol != 0)
                    {
                        row["protocol_id"] = protocol;
                        observedIds.Add(protocol);
                        if (protocol != id)
                        row["protocol_variables"] = ReadLightProbeVariables(host, token, protocol, gen);
                    }
                    if (id == 606)
                    {
                        string filename = ScalarText(detail["filename"]);
                        if (filename != null && System.Text.RegularExpressions.Regex.IsMatch(filename, "^[A-Za-z0-9_. -]{1,128}$"))
                            row["filename"] = filename;
                        ushort room;
                        if (UInt16.TryParse(ScalarText(detail["roomId"]), out room))
                            row["room_id"] = room;
                    }
                    row["proxy_variables"] = ReadLightProbeVariables(host, token, id, gen);
                }
                catch (Exception ex)
                {
                    InvalidateRejectedToken(ex);
                    row["error"] = FailureKind(ex);
                }
            }
            lock (_sync)
            {
                if (_generation != gen) return output;
                if (_lightProbeGeneration == gen)
                    output.Add(new JObject { ["event_samples"] = _lightProbeEvents.DeepClone() });
                _lightProbeGeneration = gen;
                _lightProbeIds.Clear();
                foreach (ushort id in observedIds) _lightProbeIds.Add(id);
                _lightProbeIds.Add(2218); // Scenario Experience Button driver under test.
                _lightProbeIds.Add(2219); // Living Remote Find proxy under test.
                _lightProbeIds.Add(1595); // Countdown driver under test.
                _lightProbeIds.Add(1596); // Emma Lights Countdown proxy under test.
                foreach (ushort id in new ushort[] { 328, 329, 503, 504, 505, 506, 507, 508 }) _lightProbeIds.Add(id);
                _lightProbeEvents.Clear();
            }
            return output;
        }

        private JArray ReadLightProbeVariables(string host, string token, ushort id, int gen)
        {
            string path = "/api/v1/items/" + id.ToString(CultureInfo.InvariantCulture) + "/variables";
            string json = ReadWithTimeoutRetry(host, token, path, gen);
            if (json.Length > 262144) throw new InvalidDataException("Oversized variable list");
            var report = new JArray();
            foreach (var entry in JArray.Parse(json))
            {
                if (!(entry is JObject) || report.Count >= 64) continue;
                string name = ScalarText(entry["name"]);
                if (name == null || SensitiveMediaKey(name) ||
                    !System.Text.RegularExpressions.Regex.IsMatch(name, "^[A-Za-z0-9_ -]{1,64}$")) continue;
                var value = entry["value"];
                var row = new JObject { ["name"] = name, ["type"] = value == null ? "missing" : value.Type.ToString() };
                if (id == 606)
                {
                    uint variableId;
                    foreach (var field in ((JObject)entry).Properties())
                        if (field.Name.IndexOf("id", StringComparison.OrdinalIgnoreCase) >= 0 &&
                            System.Text.RegularExpressions.Regex.IsMatch(field.Name, "^[A-Za-z0-9_]{1,32}$") &&
                            UInt32.TryParse(ScalarText(field.Value), out variableId))
                            row[field.Name] = variableId;
                    long integer;
                    if (Int64.TryParse(ScalarText(value), NumberStyles.Integer, CultureInfo.InvariantCulture, out integer))
                        row["value"] = integer;
                }
                if (value != null && (name.IndexOf("level", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    name.IndexOf("state", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    name.IndexOf("open", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    name.IndexOf("closed", StringComparison.OrdinalIgnoreCase) >= 0))
                {
                    decimal number;
                    bool boolean;
                    if (Decimal.TryParse(value.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out number) &&
                        number >= 0 && number <= 100) row["value"] = number;
                    else if (Boolean.TryParse(value.ToString(), out boolean)) row["value"] = boolean;
                    else if (String.Equals(ScalarText(value), "open", StringComparison.OrdinalIgnoreCase)) row["value"] = "open";
                    else if (String.Equals(ScalarText(value), "closed", StringComparison.OrdinalIgnoreCase)) row["value"] = "closed";
                }
                report.Add(row);
            }
            return report;
        }

        private void CaptureLightProbeEvent(JToken message, int gen)
        {
            if (!(message is JObject)) return;
            ushort id;
            if (!UInt16.TryParse(ScalarText(message["iddevice"]), out id)) return;
            lock (_sync)
            {
                if (_generation != gen || _lightProbeGeneration != gen || !_lightProbeIds.Contains(id) ||
                    _lightProbeEvents.Count >= 64) return;
                string name = ScalarText(message["evtName"]);
                if (name == null || !System.Text.RegularExpressions.Regex.IsMatch(name, "^[A-Za-z0-9_]{1,64}$")) return;
                var fields = new JObject();
                var data = message["data"] as JObject;
                if (data != null)
                    foreach (JProperty property in data.Properties())
                    {
                        if (fields.Count >= 32 || SensitiveMediaKey(property.Name) ||
                            !System.Text.RegularExpressions.Regex.IsMatch(property.Name, "^[A-Za-z0-9_ -]{1,64}$")) continue;
                        fields[property.Name] = property.Value.Type.ToString();
                    }
                var sample = new JObject { ["id"] = id, ["event"] = name, ["fields"] = fields };
                if (id == 606 && name == "OnDataToUI" && data?["state"]?.Type == JTokenType.Boolean)
                    sample["state"] = (bool)data["state"];
                if (id == 2218 || id == 2219)
                {
                    var stateFields = new JObject();
                    CollectExperienceStateFields(message, "", 0, stateFields);
                    if (stateFields.Count != 0) sample["state_fields"] = stateFields;
                }
                if ((id == 1595 || id == 1596) && data != null)
                {
                    var countdown = new JObject();
                    foreach (JProperty field in data.Properties())
                    {
                        int value;
                        if (System.Text.RegularExpressions.Regex.IsMatch(field.Name, "^(countdown|runtime|state|remaining|time)$",
                            System.Text.RegularExpressions.RegexOptions.IgnoreCase) &&
                            Int32.TryParse(ScalarText(field.Value), NumberStyles.None, CultureInfo.InvariantCulture, out value))
                            countdown[field.Name] = value;
                    }
                    if (countdown.Count != 0) sample["countdown_fields"] = countdown;
                }
                if (IsDoorLockProbeItem(id) && data != null)
                {
                    var lockFields = new JObject();
                    foreach (var field in data.Properties())
                    {
                        if (!System.Text.RegularExpressions.Regex.IsMatch(field.Name, "^(lock_?state|state|status|locked|is_?locked|icon_description)$",
                            System.Text.RegularExpressions.RegexOptions.IgnoreCase)) continue;
                        string value = ScalarText(field.Value);
                        if (IsDoorLockProbeState(value) || field.Value.Type == JTokenType.Boolean ||
                            field.Value.Type == JTokenType.Integer)
                            lockFields[field.Name] = field.Value.DeepClone();
                    }
                    if (lockFields.Count != 0) sample["lock_fields"] = lockFields;
                }
                _lightProbeEvents.Add(sample);
            }
        }

        private static void CollectExperienceStateFields(JToken token, string path, int depth, JObject result)
        {
            if (depth > 4 || result.Count >= 24) return;
            var obj = token as JObject;
            if (obj != null)
            {
                foreach (var field in obj.Properties())
                {
                    if (result.Count >= 24) break;
                    if (SensitiveMediaKey(field.Name) ||
                        !System.Text.RegularExpressions.Regex.IsMatch(field.Name, "^[A-Za-z0-9_]{1,48}$")) continue;
                    CollectExperienceStateFields(field.Value, path + "." + field.Name, depth + 1, result);
                }
                return;
            }
            var list = token as JArray;
            if (list != null)
            {
                for (int i = 0; i < list.Count && i < 8 && result.Count < 24; i++)
                    CollectExperienceStateFields(list[i], path + "[" + i + "]", depth + 1, result);
                return;
            }
            string value = ScalarText(token);
            if (String.Equals(value, "On", StringComparison.OrdinalIgnoreCase) ||
                String.Equals(value, "Off", StringComparison.OrdinalIgnoreCase) ||
                String.Equals(value, "STATE", StringComparison.OrdinalIgnoreCase))
                result[path] = value;
            else if ((path.EndsWith(".variableId", StringComparison.OrdinalIgnoreCase) ||
                path.EndsWith(".varId", StringComparison.OrdinalIgnoreCase)) && value == "1001")
                result[path] = 1001;
        }

        private static JArray InventoryItems(string json)
        {
            if (string.IsNullOrEmpty(json) || json.Length > 8388608) throw new InvalidDataException("Invalid inventory size");
            var source = JToken.Parse(json) as JArray;
            if (source == null) throw new InvalidDataException("Expected inventory list");
            var result = new JArray();
            foreach (JToken value in source)
            {
                var item = value as JObject;
                if (item == null) throw new InvalidDataException("Invalid inventory entry");
                JToken id = item["id"] ?? item["item_id"];
                long numericId;
                if (id == null || (id.Type != JTokenType.Integer && id.Type != JTokenType.String) ||
                    !Int64.TryParse(id.ToString(), NumberStyles.None, CultureInfo.InvariantCulture, out numericId) || numericId <= 0)
                    throw new InvalidDataException("Missing inventory item ID");
                // Export a metadata allowlist, never arbitrary responses, credentials or tokens.
                var entry = new JObject(new JProperty("item_id", numericId));
                CopyInventoryField(item, entry, "name");
                CopyInventoryField(item, entry, "type");
                CopyInventoryField(item, entry, "room_id");
                CopyInventoryField(item, entry, "room_name");
                CopyInventoryField(item, entry, "parent_id");
                result.Add(entry);
            }
            return result;
        }

        private static void CopyInventoryField(JObject source, JObject target, string name)
        {
            JToken value = source[name];
            if (value == null || (value.Type != JTokenType.String && value.Type != JTokenType.Integer)) return;
            if (value.Type == JTokenType.String)
            {
                string text = (string)value;
                target[name] = text.Length > 512 ? text.Substring(0, 512) : text;
            }
            else target[name] = value.DeepClone();
        }
    }
}
