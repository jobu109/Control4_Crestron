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
        private int[] _securityPartitions = new int[0];
        private int _securityInventoryGeneration = -1;
        private int[] _securityPanelItems = new int[0];
        private int _securityZoneRosterProbePending;
        private JObject _securitySocketCapture;
        private DateTime _securityCaptureUntil;
        private int _securityCaptureGeneration;
        private int _securityCaptureBytes;
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
            var partitions = new System.Collections.Generic.List<int>();
            var panelItems = new System.Collections.Generic.List<int>();
            var rosterProxies = new System.Collections.Generic.List<int>();
            foreach (JObject item in System.Linq.Enumerable.OfType<JObject>(items))
            {
                int id;
                string name = ScalarText(item["name"]) ?? "";
                if (partitions.Count < 12 && ScalarText(item["type"]) == "7" &&
                    (name.Equals("Security System", StringComparison.OrdinalIgnoreCase) ||
                     name.StartsWith("Security System ", StringComparison.OrdinalIgnoreCase)) &&
                    TrySourceId(item["id"] ?? item["item_id"], out id) && id > 0)
                    partitions.Add(id);
                if (panelItems.Count < 12 &&
                    (name.StartsWith("IQPanel", StringComparison.OrdinalIgnoreCase) ||
                     name.Equals("Security Panel", StringComparison.OrdinalIgnoreCase)) &&
                    TrySourceId(item["id"] ?? item["item_id"], out id) && id > 0)
                {
                    panelItems.Add(id);
                    if (name.Equals("Security Panel", StringComparison.OrdinalIgnoreCase) && ScalarText(item["type"]) == "7")
                        rosterProxies.Add(id);
                }
            }
            lock (_sync)
            {
                if (_generation != generation) return;
                _panelRooms = rooms;
                _securityPartitions = partitions.ToArray();
                _securityPanelItems = panelItems.ToArray();
                _securityRosterProxies = rosterProxies.ToArray();
                _securityInventoryGeneration = generation;
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

        private static bool IsKnownDimmerDriver(string name, string model, string filename)
        {
            if (String.Equals(name, "Keypad Dimmer", StringComparison.OrdinalIgnoreCase) ||
                String.Equals(name, "Forward Phase Dimmer", StringComparison.OrdinalIgnoreCase) ||
                String.Equals(name, "Essential Forward Phase Dimmer", StringComparison.OrdinalIgnoreCase)) return true;

            const string modelPattern = @"(?:^|[^A-Za-z0-9])(?:FPD|KD)[-_ ]?120(?:$|[^A-Za-z0-9])";
            return System.Text.RegularExpressions.Regex.IsMatch(
                String.Join(" ", name, model, filename), modelPattern,
                System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.CultureInvariant);
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
                    var dimmerDrivers = new System.Collections.Generic.HashSet<string>(StringComparer.Ordinal);
                    foreach (var driver in candidates)
                    {
                        if (!(driver is JObject) || ScalarText(driver["type"]) != "6") continue;
                        string filename = ScalarText(driver["filename"]);
                        string driverId = ScalarText(driver["id"] ?? driver["item_id"]);
                        if (!String.IsNullOrEmpty(driverId) && ExperienceButtonDrivers.Contains(filename))
                            experienceDrivers[driverId] = String.Equals(filename, "timer_countdown.c4z", StringComparison.OrdinalIgnoreCase);
                        if (!String.IsNullOrEmpty(driverId) && IsKnownDimmerDriver(
                            ScalarText(driver["name"]), ScalarText(driver["model"]), filename))
                            dimmerDrivers.Add(driverId);
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
                        devices.Add(id, new Control4_Room_Devices.Entry
                        {
                            Id = id, Name = label, Shade = shade,
                            KnownDimmer = !shade && dimmerDrivers.Contains(ScalarText(detail["protocolId"]))
                        });
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

        internal void BeginSecuritySocketCapture()
        {
            lock (_sync)
            {
                if (!_online || _securityInventoryGeneration != _generation ||
                    (_securityPartitions.Length == 0 && _securityPanelItems.Length == 0))
                {
                    EmitMessage("Security WebSocket capture unavailable. Connect with the security panel in inventory.");
                    return;
                }
                _securityCaptureGeneration = _generation;
                _securityCaptureUntil = DateTime.UtcNow.AddSeconds(90);
                _securityCaptureBytes = 0;
                _securitySocketCapture = new JObject
                {
                    ["started_utc"] = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture),
                    ["watch_ids"] = new JArray(System.Linq.Enumerable.Concat(_securityPanelItems, _securityPartitions)),
                    ["events"] = new JArray(),
                    ["dropped"] = 0
                };
            }
            EmitMessage("Security WebSocket capture started (90 seconds)");
        }

        internal event Action<JObject> SecurityZonesReceived;
        private event Action<bool> SecurityRefreshRequested;
        private int[] _securityRosterProxies = new int[0];

        internal void RequestSecurityZones()
        {
            int generation;
            int[] proxies;
            lock (_sync)
            {
                generation = _generation;
                proxies = _securityInventoryGeneration == generation ? _securityRosterProxies : new int[0];
            }
            if (proxies.Length == 0) return;
            Queue(delegate
            {
                string host, token;
                if (!CurrentGeneration(generation) || !GetConnection(out host, out token)) return;
                foreach (int id in proxies)
                {
                    if (!CurrentGeneration(generation)) return;
                    try
                    {
                        Request(host, token, "POST", "/api/v1/items/" + id.ToString(CultureInfo.InvariantCulture) +
                            "/commands", "{\"command\":\"GET_ALL_ZONE_INFO\",\"tParams\":{}}");
                    }
                    catch (Exception ex)
                    {
                        InvalidateRejectedToken(ex);
                        EmitMessage("Security zone list request failed: " + FailureKind(ex));
                    }
                }
            });
        }

        private void RouteSecurityZoneEvent(JToken message, int generation)
        {
            var handler = SecurityZonesReceived;
            var packet = message as JObject;
            int id;
            if (handler == null || packet == null || ScalarText(packet["evtName"]) != "OnDataToUI" ||
                !TrySourceId(packet["iddevice"], out id) || !(packet["data"] is JObject)) return;
            bool panel;
            lock (_sync)
            {
                if (_generation != generation || _securityInventoryGeneration != generation) return;
                panel = Array.IndexOf(_securityRosterProxies, id) >= 0;
                if (!panel && Array.IndexOf(_securityPartitions, id) < 0) return;
            }
            var data = (JObject)packet["data"];
            if (!panel && (data["partition_state"] is JObject || data["zone_state"] is JObject))
            {
                SignalSecurityRefresh(false);
                return;
            }
            var roster = (data["all_zones"] as JObject)?["zone_info"] as JArray;
            if (panel && roster != null && roster.Count <= 256)
            {
                var rows = new JArray();
                foreach (JObject zone in System.Linq.Enumerable.OfType<JObject>(roster))
                {
                    int zoneId;
                    if (!TrySourceId(zone["id"], out zoneId) || zoneId == 0 ||
                        zone["name"]?.Type != JTokenType.String || (bool?)zone["hide_from_ui"] == true) continue;
                    string name = PlayingText(zone["name"]);
                    if (String.IsNullOrEmpty(name)) continue;
                    var row = new JObject { ["id"] = zoneId, ["name"] = name };
                    if (zone["open"]?.Type == JTokenType.Boolean) row["open"] = (bool)zone["open"];
                    rows.Add(row);
                }
                SafeEvent(delegate { handler(new JObject { ["room_id"] = 0, ["panel"] = id, ["zones"] = rows }); });
            }
            else if (panel && data["zone_state"] is JObject)
            {
                var state = (JObject)data["zone_state"];
                int zoneId;
                if (!TrySourceId(state["id"], out zoneId) || zoneId == 0 || state["open"]?.Type != JTokenType.Boolean) return;
                SafeEvent(delegate { handler(new JObject { ["security_zone"] = zoneId, ["panel"] = id, ["open"] = (bool)state["open"] }); });
            }
        }

        private void SignalSecurityRefresh(bool reconnect)
        {
            var handler = SecurityRefreshRequested;
            if (handler != null) SafeEvent(delegate { handler(reconnect); });
        }

        internal void SubscribeSecurityRefresh(Action<bool> handler) { SecurityRefreshRequested += handler; }
        internal void UnsubscribeSecurityRefresh(Action<bool> handler) { SecurityRefreshRequested -= handler; }

        internal void ProbeSecurityZoneRoster()
        {
            if (System.Threading.Interlocked.CompareExchange(ref _securityZoneRosterProbePending, 1, 0) != 0) return;
            Queue(delegate
            {
                try
                {
                    string host, token;
                    if (!GetConnection(out host, out token)) return;
                    int generation;
                    int[] candidates;
                    lock (_sync)
                    {
                        generation = _generation;
                        candidates = _securityInventoryGeneration == generation ? _securityPanelItems : new int[0];
                    }
                    BeginSecuritySocketCapture();
                    if (candidates.Length == 0) return;
                    var results = new JArray();
                    foreach (int id in candidates)
                    {
                        if (!CurrentGeneration(generation)) return;
                        var result = new JObject { ["item_id"] = id };
                        results.Add(result);
                        try
                        {
                            var body = new JObject { ["command"] = "GET_ALL_ZONE_INFO", ["tParams"] = new JObject() };
                            string reply = Request(host, token, "POST", "/api/v1/items/" +
                                id.ToString(CultureInfo.InvariantCulture) + "/commands", body.ToString(Formatting.None));
                            if (reply.Length <= 65536)
                            {
                                int budget = 384;
                                JToken payload = new JValue(reply);
                                try { payload = JToken.Parse(reply); } catch (JsonReaderException) { }
                                result["response"] = NowPlayingShape(payload, "", 0, ref budget);
                            }
                            else result["oversize"] = true;
                        }
                        catch (Exception ex)
                        {
                            InvalidateRejectedToken(ex);
                            result["error"] = FailureKind(ex);
                        }
                    }
                    lock (_sync)
                        if (_securitySocketCapture != null && _securityCaptureGeneration == generation)
                            _securitySocketCapture["roster_probe"] = results;
                    EmitMessage("Security zone roster probe complete. Export inventory.");
                }
                finally { System.Threading.Interlocked.Exchange(ref _securityZoneRosterProbePending, 0); }
            });
        }

        private void CaptureSecuritySocketEvent(JToken message, int generation)
        {
            var packet = message as JObject;
            int id;
            if (packet == null || !TrySourceId(packet["iddevice"], out id)) return;
            lock (_sync)
            {
                if (_securitySocketCapture == null || generation != _securityCaptureGeneration ||
                    generation != _generation || DateTime.UtcNow > _securityCaptureUntil ||
                    (Array.IndexOf(_securityPartitions, id) < 0 && Array.IndexOf(_securityPanelItems, id) < 0)) return;
                var events = (JArray)_securitySocketCapture["events"];
                if (events.Count >= 32 || _securityCaptureBytes >= 65536)
                {
                    _securitySocketCapture["dropped"] = (int)_securitySocketCapture["dropped"] + 1;
                    return;
                }
                int budget = 256;
                var observation = new JObject
                {
                    ["item_id"] = id,
                    ["event_type"] = MediaMetadataName(packet["evtName"]) ?? "unrecognized",
                    ["data"] = NowPlayingShape(packet["data"], "", 0, ref budget)
                };
                int size = observation.ToString(Formatting.None).Length;
                if (_securityCaptureBytes + size <= 65536)
                {
                    events.Add(observation);
                    _securityCaptureBytes += size;
                }
                else _securitySocketCapture["dropped"] = (int)_securitySocketCapture["dropped"] + 1;
            }
        }

        internal void ReadPanelSecurity(Action<JObject> completed)
        {
            int generation;
            int[] partitions;
            lock (_sync)
            {
                generation = _generation;
                partitions = _securityInventoryGeneration == generation ? _securityPartitions : new int[0];
            }
            Queue(delegate
            {
                var packet = new JObject { ["security"] = true };
                try
                {
                    string host, token;
                    if (!GetConnection(out host, out token) || !CurrentGeneration(generation)) return;
                    foreach (int id in partitions)
                    {
                        if (!CurrentGeneration(generation)) return;
                        string json = Request(host, token, "GET", "/api/v1/items/" +
                            id.ToString(CultureInfo.InvariantCulture) + "/variables", null);
                        var variables = JArray.Parse(json);
                        if (SecurityNumber(variables, "IS_ACTIVE", 1) != 1) continue;
                        if (packet["partition"] != null)
                        {
                            packet = new JObject { ["security"] = true, ["multiple"] = true };
                            break;
                        }
                        packet["partition"] = id;
                        int? disarmed = SecurityNumber(variables, "DISARMED_STATE", 1);
                        int? home = SecurityNumber(variables, "HOME_STATE", 1);
                        int? away = SecurityNumber(variables, "AWAY_STATE", 1);
                        if (disarmed == 1 && home == 0 && away == 0) packet["mode"] = "disarmed";
                        else if (disarmed == 0 && home == 1 && away == 0) packet["mode"] = "home";
                        else if (disarmed == 0 && home == 0 && away == 1) packet["mode"] = "away";
                        int? alarm = SecurityNumber(variables, "ALARM_STATE", 1);
                        if (alarm.HasValue) packet["alarm"] = alarm.Value == 1;
                        int? open = SecurityNumber(variables, "OPEN_ZONE_COUNT", UInt16.MaxValue);
                        if (open.HasValue) packet["open"] = open.Value;
                    }
                }
                catch (Exception ex)
                {
                    InvalidateRejectedToken(ex);
                    packet = new JObject { ["security"] = true };
                    EmitMessage("Security status read failed: " + FailureKind(ex));
                }
                finally
                {
                    if (!CurrentGeneration(generation)) packet = new JObject { ["security"] = true };
                    completed(packet);
                }
            });
        }

        internal void ExecuteSecurityKeypad(ushort partition, string action, string pin, Action<string> completed)
        {
            string expected = action == "Stay" ? "home" : action == "Away" ? "away" : action == "Disarm" ? "disarmed" : null;
            if (expected == null || pin == null || pin.Length > 10 ||
                (action == "Disarm" && pin.Length == 0) ||
                !System.Text.RegularExpressions.Regex.IsMatch(pin, @"^[0-9]*$"))
            {
                completed("invalid");
                return;
            }
            int generation;
            lock (_sync) generation = _generation;
            ReadPanelSecurity(snapshot =>
            {
                if (!CurrentGeneration(generation) || (int?)snapshot["partition"] != partition)
                {
                    completed("unavailable");
                    return;
                }
                if ((string)snapshot["mode"] == expected) { completed("confirmed"); return; }
                string host, token;
                if (!GetConnection(out host, out token)) { completed("unavailable"); return; }
                string result = "failed";
                try
                {
                    var parameters = new JObject { ["InterfaceID"] = "Sytrohs", ["UserCode"] = pin };
                    if (action != "Disarm")
                    {
                        parameters["ArmType"] = action;
                        parameters["Bypass"] = "False";
                    }
                    var request = new JObject {
                        ["command"] = action == "Disarm" ? "PARTITION_DISARM" : "PARTITION_ARM",
                        ["tParams"] = parameters
                    };
                    if (CurrentGeneration(generation))
                    {
                        Request(host, token, "POST", "/api/v1/items/" +
                            partition.ToString(CultureInfo.InvariantCulture) + "/commands", request.ToString(Formatting.None));
                        result = "sent";
                    }
                }
                catch (Exception ex) { InvalidateRejectedToken(ex); }
                finally { completed(result); }
            });
        }

        private static int? SecurityNumber(JArray variables, string field, int maximum)
        {
            JToken value;
            int number;
            if (!SourceVariable(variables, field, out value)) return null;
            if (maximum == 1 && value.Type == JTokenType.Boolean) return (bool)value ? 1 : 0;
            return Int32.TryParse(ScalarText(value), NumberStyles.None, CultureInfo.InvariantCulture, out number) &&
                number >= 0 && number <= maximum ? (int?)number : null;
        }

        internal void ExecuteClimateControl(int id, string action, string value, Action<bool> completed)
        {
            int generation;
            lock (_sync) generation = _generation;
            Queue(delegate
            {
                bool accepted = false;
                try
                {
                    string host, token;
                    if (!CurrentGeneration(generation) || !GetConnection(out host, out token)) return;
                    var items = InventoryItems(Request(host, token, "GET", "/api/v1/categories/comfort", null));
                    if (!System.Linq.Enumerable.Any(items, item =>
                        ScalarText(item["type"]) == "7" && ScalarText(item["item_id"]) == id.ToString(CultureInfo.InvariantCulture))) return;
                    string path = "/api/v1/items/" + id.ToString(CultureInfo.InvariantCulture);
                    var variables = JArray.Parse(Request(host, token, "GET", path + "/variables", null));
                    JToken modes;
                    if (SecurityNumber(variables, "IS_CONNECTED", 1) != 1 ||
                        !SourceVariable(variables, "HVAC_MODES_LIST", out modes)) return;
                    string command;
                    var parameters = new JObject();
                    switch (action)
                    {
                        case "mode":
                        case "fan":
                            if (action == "fan" && !SourceVariable(variables, "FAN_MODES_LIST", out modes)) return;
                            if (modes.Type != JTokenType.String || String.IsNullOrEmpty(value)) return;
                            string match = System.Linq.Enumerable.FirstOrDefault(((string)modes).Split(','),
                                mode => String.Equals(mode.Trim(), value, StringComparison.OrdinalIgnoreCase));
                            if (match == null) return;
                            parameters["MODE"] = match.Trim();
                            command = action == "mode" ? "SET_MODE_HVAC" : "SET_MODE_FAN";
                            break;
                        case "heat_set":
                        case "cool_set":
                            int temperature;
                            JToken scale;
                            if (!Int32.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out temperature) ||
                                temperature < 45 || temperature > 95 ||
                                !SourceVariable(variables, "SCALE", out scale) ||
                                !String.Equals(ScalarText(scale), "FAHRENHEIT", StringComparison.OrdinalIgnoreCase)) return;
                            parameters["FAHRENHEIT"] = temperature.ToString(CultureInfo.InvariantCulture);
                            command = action == "heat_set" ? "SET_SETPOINT_HEAT" : "SET_SETPOINT_COOL";
                            break;
                        case "heat_up": command = "INC_SETPOINT_HEAT"; break;
                        case "heat_down": command = "DEC_SETPOINT_HEAT"; break;
                        case "cool_up": command = "INC_SETPOINT_COOL"; break;
                        case "cool_down": command = "DEC_SETPOINT_COOL"; break;
                        default: return;
                    }
                    JObject metadata = BuildCommandMetadataReport(Request(host, token, "GET", path + "/commands", null), 200, id);
                    if (!System.Linq.Enumerable.Any((JArray)metadata["commands"], entry =>
                        String.Equals((string)entry["command_name"], command, StringComparison.Ordinal)) ||
                        !CurrentGeneration(generation)) return;
                    var payload = new JObject { ["command"] = command, ["tParams"] = parameters };
                    Request(host, token, "POST", path + "/commands", payload.ToString(Formatting.None));
                    accepted = true;
                }
                catch (Exception ex)
                {
                    InvalidateRejectedToken(ex);
                    EmitMessage("Climate command failed: " + FailureKind(ex));
                }
                finally { completed(accepted); }
            });
        }

        internal event Action ClimateChanged;
        private int[] _climateEventItems = new int[0];
        private int _climateEventGeneration = -1;

        private void RouteClimateEvent(JToken message, int generation)
        {
            var handler = ClimateChanged;
            var packet = message as JObject;
            int id;
            if (handler == null || packet == null || ScalarText(packet["evtName"]) != "OnDataToUI" ||
                !TrySourceId(packet["iddevice"], out id)) return;
            lock (_sync)
                if (_generation != generation || _climateEventGeneration != generation ||
                    Array.IndexOf(_climateEventItems, id) < 0) return;
            SafeEvent(handler);
        }

        internal void ReadThermostats(Action<JObject> completed)
        {
            int generation;
            lock (_sync) generation = _generation;
            Queue(delegate
            {
                var rows = new JArray();
                bool available = false;
                try
                {
                    string host, token;
                    if (!CurrentGeneration(generation) || !GetConnection(out host, out token)) return;
                    var items = InventoryItems(Request(host, token, "GET", "/api/v1/categories/comfort", null));
                    foreach (JObject item in System.Linq.Enumerable.OfType<JObject>(items))
                    {
                        if (!CurrentGeneration(generation) || rows.Count >= 32) break;
                        int id;
                        if (ScalarText(item["type"]) != "7" || !TrySourceId(item["item_id"], out id) || id <= 0) continue;
                        try
                        {
                            var vars = JArray.Parse(Request(host, token, "GET", "/api/v1/items/" +
                                id.ToString(CultureInfo.InvariantCulture) + "/variables", null));
                            JToken value;
                            if (!SourceVariable(vars, "HVAC_MODES_LIST", out value)) continue;
                            var row = new JObject { ["id"] = id, ["name"] = PlayingText(item["name"]) };
                            int? connected = SecurityNumber(vars, "IS_CONNECTED", 1);
                            if (connected.HasValue) row["connected"] = connected == 1;
                            foreach (string field in new[] { "SCALE", "HVAC_MODE", "HVAC_STATE", "FAN_MODE", "FAN_STATE", "HVAC_MODES_LIST", "FAN_MODES_LIST" })
                                if (SourceVariable(vars, field, out value) && value.Type == JTokenType.String)
                                    row[field] = PlayingText(value);
                            if (connected != 0)
                                foreach (string field in new[] { "DISPLAY_TEMPERATURE", "DISPLAY_HEATSETPOINT", "DISPLAY_COOLSETPOINT", "HUMIDITY" })
                                {
                                    decimal number;
                                    if (SourceVariable(vars, field, out value) &&
                                        Decimal.TryParse(ScalarText(value), NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint,
                                            CultureInfo.InvariantCulture, out number) && number >= -100 &&
                                        number <= (field == "HUMIDITY" ? 100 : 250) && (field != "HUMIDITY" || number >= 0))
                                        row[field] = number;
                                }
                            rows.Add(row);
                        }
                        catch (Exception ex)
                        {
                            InvalidateRejectedToken(ex);
                            EmitMessage("Thermostat read failed: " + FailureKind(ex));
                        }
                    }
                    lock (_sync)
                    {
                        available = _generation == generation;
                        if (available)
                        {
                            var ids = new System.Collections.Generic.HashSet<int>();
                            foreach (JObject row in System.Linq.Enumerable.OfType<JObject>(rows))
                            {
                                ids.Add((int)row["id"]);
                                foreach (JObject item in System.Linq.Enumerable.OfType<JObject>(items))
                                {
                                    int driver;
                                    if (ScalarText(item["type"]) == "6" &&
                                        String.Equals(PlayingText(item["name"]), (string)row["name"], StringComparison.Ordinal) &&
                                        TrySourceId(item["item_id"], out driver)) ids.Add(driver);
                                }
                            }
                            _climateEventItems = System.Linq.Enumerable.ToArray(ids);
                            _climateEventGeneration = generation;
                        }
                    }
                }
                catch (Exception ex)
                {
                    InvalidateRejectedToken(ex);
                    EmitMessage("Climate discovery failed: " + FailureKind(ex));
                }
                finally
                {
                    completed(new JObject { ["room_id"] = 0, ["available"] = available,
                        ["thermostats"] = available ? rows : new JArray() });
                }
            });
        }

        private JObject InspectCameraProxyReply(string host, string token, string path, string command,
            JObject parameters, string expectedTag)
        {
            try
            {
                var body = new JObject { ["command"] = command, ["tParams"] = parameters };
                int status;
                string reply = Request(host, token, "POST", path, body.ToString(Formatting.None), out status);
                return new JObject { ["http_status"] = status, ["reply_length"] = reply.Length,
                    ["empty_object"] = String.Equals(reply.Trim(), "{}", StringComparison.Ordinal),
                    ["expected_return_tag"] = reply.IndexOf("<" + expectedTag, StringComparison.OrdinalIgnoreCase) >= 0 };
            }
            catch (Exception ex)
            {
                InvalidateRejectedToken(ex);
                return new JObject { ["error"] = FailureKind(ex) };
            }
        }

        private JArray InspectCameraInventory(JArray items, string host, string token, int generation)
        {
            var reports = new JArray();
            foreach (JObject item in System.Linq.Enumerable.OfType<JObject>(items))
            {
                if (!CurrentGeneration(generation) || reports.Count >= 12) break;
                int id;
                if (!TrySourceId(item["item_id"], out id) || id <= 0) continue;
                var row = new JObject { ["item_id"] = id, ["name"] = item["name"], ["type"] = item["type"] };
                reports.Add(row);
                try
                {
                    string path = "/api/v1/items/" + id.ToString(CultureInfo.InvariantCulture);
                    row["command_metadata"] = BuildCommandMetadataReport(
                        Request(host, token, "GET", path + "/commands", null), 200, id);
                    if (ScalarText(item["type"]) == "7" && CurrentGeneration(generation))
                    {
                        row["snapshot_query"] = InspectCameraProxyReply(host, token, path + "/commands",
                            "GET_SNAPSHOT_QUERY_STRING", new JObject { ["SIZE_X"] = 320, ["SIZE_Y"] = 180 }, "snapshot_query_string");
                        if (CurrentGeneration(generation))
                            row["properties_query"] = InspectCameraProxyReply(host, token, path + "/commands",
                                "GET_PROPERTIES", new JObject(), "camera_properties");
                    }
                }
                catch (Exception ex)
                {
                    InvalidateRejectedToken(ex);
                    row["error"] = FailureKind(ex);
                }
            }
            return reports;
        }

        private static readonly System.Collections.Generic.Dictionary<string, string> IrrigationNames =
            new System.Collections.Generic.Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["run"] = "Start Program", ["stop"] = "Stop Program", ["pause"] = "Pause Program",
                ["resume"] = "Resume Program", ["mode"] = "Program Mode", ["day"] = "Enable or Disable Day",
                ["hour"] = "Start Hour", ["minute"] = "Start Minute"
            };

        internal static JObject IrrigationParameters(string action, string value, string state)
        {
            switch (action)
            {
                case "run": case "stop": case "pause": case "resume": return new JObject();
                case "mode":
                    return Array.IndexOf(new[] { "Off", "Every Day", "Odd Days", "Even Days", "Select Days" }, value) < 0
                        ? null : new JObject { ["Mode"] = value };
                case "day":
                    return Array.IndexOf(new[] { "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday", "Sunday" }, value) < 0 ||
                        (state != "Enable" && state != "Disable") ? null : new JObject { ["Day"] = value, ["State"] = state };
                case "hour": case "minute":
                    int number;
                    bool hour = action == "hour";
                    if (!Int32.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out number) ||
                        number < 0 || number > (hour ? 24 : 59)) return null;
                    return new JObject { [hour ? "Hour (0-24)" : "Minute (0-59)"] = number.ToString(CultureInfo.InvariantCulture) };
                default: return null;
            }
        }

        private void ReadIrrigationCommands(JObject row, JToken item, JArray items, string host, string token, int generation)
        {
            int parent;
            int id = (int)row["id"];
            TrySourceId(item["parent_id"], out parent);
            int protocol = 0;
            try
            {
                string path = "/api/v1/items/" + id.ToString(CultureInfo.InvariantCulture);
                var detail = JObject.Parse(ReadWithTimeoutRetry(host, token, path, generation));
                TrySourceId(detail["protocolId"] ?? detail["protocol_id"] ?? detail["driverId"] ?? detail["driver_id"], out protocol);
            }
            catch (Exception ex)
            {
                InvalidateRejectedToken(ex);
                EmitMessage("Irrigation protocol lookup failed: " + FailureKind(ex));
            }
            // Some Director versions omit the protocol relationship for this driver.
            // Match the verified driver and proxy identities only for an unambiguous pair.
            if (protocol == 0 && parent == 0 &&
                String.Equals((string)item["name"], "Irrigation Program", StringComparison.OrdinalIgnoreCase))
            {
                int matchingPrograms = System.Linq.Enumerable.Count(items, entry =>
                    ScalarText(entry["type"]) == "7" &&
                    String.Equals((string)entry["name"], "Irrigation Program", StringComparison.OrdinalIgnoreCase));
                var drivers = System.Linq.Enumerable.ToArray(System.Linq.Enumerable.Where(items, entry =>
                    ScalarText(entry["type"]) == "6" &&
                    String.Equals((string)entry["name"], "Irrigation Control", StringComparison.OrdinalIgnoreCase)));
                if (matchingPrograms == 1 && drivers.Length == 1)
                    TrySourceId(drivers[0]["item_id"], out protocol);
            }
            int target = protocol > 0 ? protocol : parent;
            if (CurrentGeneration(generation) && target > 0 && target != id &&
                System.Linq.Enumerable.Any(items, entry => ScalarText(entry["type"]) == "6" &&
                    ScalarText(entry["item_id"]) == target.ToString(CultureInfo.InvariantCulture)))
            {
                try
                {
                    string path = "/api/v1/items/" + target.ToString(CultureInfo.InvariantCulture) + "/commands";
                    var commands = JArray.Parse(ReadWithTimeoutRetry(host, token, path, generation));
                    foreach (var entry in commands)
                    {
                        if (!(entry is JObject)) continue;
                        string name = ScalarText(entry["command"]);
                        foreach (var command in IrrigationNames)
                        {
                            string field = command.Key + "_target";
                            if (command.Value == name && row[field] == null) row[field] = target;
                        }
                    }
                }
                catch (Exception ex)
                {
                    InvalidateRejectedToken(ex);
                    EmitMessage("Irrigation capability read failed: " + FailureKind(ex));
                }
            }
            var supported = new System.Collections.Generic.List<string>();
            foreach (var command in IrrigationNames)
                if (row[command.Key + "_target"] != null)
                    supported.Add(command.Key + "=" + row[command.Key + "_target"]);
            EmitMessage("Irrigation program " + id + " protocol " + protocol + "; command targets " +
                (supported.Count == 0 ? "none advertised" : String.Join(", ", supported)));
        }

        internal void ExecuteIrrigationCommand(int target, string action, JObject parameters, int generation, Action<bool> completed)
        {
            Queue(delegate
            {
                bool accepted = false;
                try
                {
                    string host, token;
                    if (target <= 0 || !CurrentGeneration(generation) || !GetConnection(out host, out token)) return;
                    var payload = new JObject { ["command"] = IrrigationNames[action], ["tParams"] = parameters };
                    Request(host, token, "POST", "/api/v1/items/" + target.ToString(CultureInfo.InvariantCulture) +
                        "/commands", payload.ToString(Formatting.None));
                    accepted = CurrentGeneration(generation);
                    if (accepted) EmitMessage("Irrigation " + action + " accepted on item " + target);
                }
                catch (Exception ex)
                {
                    InvalidateRejectedToken(ex);
                    EmitMessage("Irrigation " + action + " failed on item " + target + ": " + FailureKind(ex));
                }
                finally { completed(accepted); }
            });
        }

        internal void ReadPanelOutdoor(Action<int, JObject> completed)
        {
            int generation;
            lock (_sync) generation = _generation;
            Queue(delegate
            {
                var pool = new JArray();
                var irrigation = new JArray();
                try
                {
                    string host, token;
                    if (!GetConnection(out host, out token) || !CurrentGeneration(generation)) return;
                    var items = InventoryItems(ReadWithTimeoutRetry(host, token, "/api/v1/items", generation));
                    foreach (var item in items)
                    {
                        if (!CurrentGeneration(generation)) return;
                        if (pool.Count + irrigation.Count >= 16) break;
                        string name = (string)item["name"] ?? "";
                        int id;
                        if (ScalarText(item["type"]) != "7" || !TrySourceId(item["item_id"], out id) || id <= 0 ||
                            !System.Text.RegularExpressions.Regex.IsMatch(name, @"\b(pool|spa|intellicenter|irrigation)\b",
                                System.Text.RegularExpressions.RegexOptions.IgnoreCase)) continue;
                        try
                        {
                            string path = "/api/v1/items/" + id.ToString(CultureInfo.InvariantCulture) + "/variables";
                            var variables = JArray.Parse(ReadWithTimeoutRetry(host, token, path, generation));
                            JToken value;
                            bool isPool = SourceVariable(variables, "PUMPMODE", out value) &&
                                SourceVariable(variables, "HASPOOL", out value);
                            bool isProgram = SourceVariable(variables, "STEP", out value) &&
                                SourceVariable(variables, "STEP_TIME", out value) &&
                                SourceVariable(variables, "LAST_MENU_SELECTED", out value);
                            if (!isPool && !isProgram) continue;
                            var row = new JObject { ["id"] = id, ["name"] = name };
                            var fields = isPool
                                ? new[] { "PUMPMODE", "POOL_HEATMODE", "POOL_TEMPERATURE", "POOL_SETPOINT", "SPAMODE",
                                    "SPA_HEATMODE", "SPA_TEMPERATURE", "SPA_SETPOINT", "AIR_TEMPERATURE", "SCALE",
                                    "HASPOOL", "HASSPA", "HASAIR" }
                                : new[] { "STEP", "STEP_TIME" };
                            foreach (string field in fields)
                            {
                                if (!SourceVariable(variables, field, out value)) continue;
                                string reading = ScalarText(value);
                                if (!String.IsNullOrWhiteSpace(reading) && reading.Length <= 64)
                                    row[field] = reading;
                            }
                            if (isProgram && !isPool) ReadIrrigationCommands(row, item, items, host, token, generation);
                            (isPool ? pool : irrigation).Add(row);
                        }
                        catch (Exception ex)
                        {
                            InvalidateRejectedToken(ex);
                            EmitMessage("Outdoor device read failed: " + FailureKind(ex));
                        }
                    }
                }
                catch (Exception ex)
                {
                    InvalidateRejectedToken(ex);
                    EmitMessage("Outdoor discovery failed: " + FailureKind(ex));
                }
                if (CurrentGeneration(generation))
                    completed(generation, new JObject { ["room_id"] = 0, ["pool"] = pool, ["irrigation"] = irrigation });
            });
        }

        private sealed class IrrigationStateCapture
        {
            internal int Item;
            internal int Generation;
            internal readonly JArray Events = new JArray();
        }

        private IrrigationStateCapture _irrigationStateCapture;

        private void CaptureIrrigationStateEvent(JToken message, int generation)
        {
            var packet = message as JObject;
            int id;
            if (packet == null || !TrySourceId(packet["iddevice"], out id)) return;
            lock (_sync)
            {
                var capture = _irrigationStateCapture;
                if (capture == null || capture.Generation != generation || capture.Item != id ||
                    capture.Events.Count >= 16) return;
                int budget = 256;
                capture.Events.Add(new JObject
                {
                    ["event_type"] = MediaMetadataName(packet["evtName"]) ?? "unrecognized",
                    ["data"] = NowPlayingShape(packet["data"], "", 0, ref budget)
                });
            }
        }

        private JObject InspectIrrigationStateWithEvents(string host, string token, string path, int id, int generation)
        {
            var capture = new IrrigationStateCapture { Item = id, Generation = generation };
            lock (_sync) _irrigationStateCapture = capture;
            JObject result;
            try
            {
                result = InspectIrrigationState(host, token, path);
                if ((bool?)result["empty_object"] == true && CurrentGeneration(generation))
                    System.Threading.Thread.Sleep(2500);
            }
            finally
            {
                lock (_sync) _irrigationStateCapture = null;
            }
            result["websocket_events"] = capture.Events;
            return result;
        }

        private JObject InspectIrrigationState(string host, string token, string path)
        {
            var body = new JObject { ["command"] = "GetState", ["tParams"] = new JObject() };
            int status;
            string reply = Request(host, token, "POST", path, body.ToString(Formatting.None), out status);
            var result = new JObject { ["http_status"] = status, ["reply_length"] = reply.Length };
            if (reply.Length > 262144) return result;
            try
            {
                var parsed = JToken.Parse(reply);
                int budget = 256;
                result["empty_object"] = parsed is JObject && !parsed.HasValues;
                result["shape"] = NowPlayingShape(parsed, "", 0, ref budget);
            }
            catch (JsonReaderException)
            {
                result["non_json"] = true;
            }
            return result;
        }

        private JArray InspectOutdoorInventory(JArray items, string host, string token, int generation)
        {
            var results = new JArray();
            foreach (var item in items)
            {
                if (!CurrentGeneration(generation) || results.Count >= 16) break;
                var name = (string)item["name"] ?? "";
                int id;
                if (!System.Text.RegularExpressions.Regex.IsMatch(name,
                    @"\b(pool|spa|intellicenter|irrigation)\b", System.Text.RegularExpressions.RegexOptions.IgnoreCase) ||
                    !TrySourceId(item["item_id"], out id) || id <= 0) continue;
                var row = new JObject { ["item_id"] = id, ["name"] = name, ["type"] = item["type"] };
                results.Add(row);
                foreach (var resource in new[] { "variables", "commands" })
                {
                    if (!CurrentGeneration(generation)) return results;
                    try
                    {
                        string response = Request(host, token, "GET", "/api/v1/items/" +
                            id.ToString(CultureInfo.InvariantCulture) + "/" + resource, null);
                        row[resource] = resource == "commands" ? BuildCommandMetadataReport(response, 200, id) : InspectNowPlayingVariables(response);
                        if (resource == "commands" && ScalarText(item["type"]) == "7" &&
                            String.Equals(name, "Irrigation Program", StringComparison.OrdinalIgnoreCase) &&
                            CurrentGeneration(generation) &&
                            System.Linq.Enumerable.Any((JArray)row["commands"]["commands"], entry =>
                                ScalarText(entry["command_name"]) == "GetState"))
                        {
                            try
                            {
                                row["get_state"] = InspectIrrigationStateWithEvents(host, token,
                                    "/api/v1/items/" + id.ToString(CultureInfo.InvariantCulture) + "/commands", id, generation);
                            }
                            catch (Exception ex)
                            {
                                InvalidateRejectedToken(ex);
                                row["get_state"] = new JObject { ["error"] = FailureKind(ex) };
                            }
                        }
                    }
                    catch (Exception error)
                    {
                        row[resource + "_error"] = error.GetType().Name;
                    }
                }
            }
            return results;
        }

        private JArray InspectComfortInventory(JArray items, JArray comfort, string host, string token, int generation)
        {
            var results = new JArray();
            var seen = new System.Collections.Generic.HashSet<int>();
            var candidates = System.Linq.Enumerable.Concat(comfort,
                System.Linq.Enumerable.Where(items, item => System.Text.RegularExpressions.Regex.IsMatch(
                    (string)item["name"] ?? "", @"thermostat|hvac|ecobee", System.Text.RegularExpressions.RegexOptions.IgnoreCase)));
            foreach (JObject item in System.Linq.Enumerable.OfType<JObject>(candidates))
            {
                if (!CurrentGeneration(generation) || results.Count >= 12) break;
                int id;
                if (!TrySourceId(item["item_id"], out id) || id <= 0 || !seen.Add(id)) continue;
                var report = new JObject { ["item_id"] = id, ["name"] = item["name"] };
                results.Add(report);
                try
                {
                    string path = "/api/v1/items/" + id.ToString(CultureInfo.InvariantCulture);
                    string response = Request(host, token, "GET", path + "/variables", null);
                    report["variables"] = InspectNowPlayingVariables(response);
                    var variables = JArray.Parse(response);
                    var values = new JObject();
                    foreach (string field in new[] { "TEMPERATURE", "CURRENT_TEMPERATURE", "ROOM_TEMPERATURE", "OUTDOOR_TEMPERATURE",
                        "HEAT_SETPOINT", "COOL_SETPOINT", "HEATING_SETPOINT", "COOLING_SETPOINT", "HUMIDITY",
                        "TEMPERATURE_F", "TEMPERATURE_C", "HEAT_SETPOINT_F", "HEAT_SETPOINT_C", "COOL_SETPOINT_F",
                        "COOL_SETPOINT_C", "DISPLAY_TEMPERATURE", "DISPLAY_HEATSETPOINT", "DISPLAY_COOLSETPOINT", "IS_CONNECTED" })
                    {
                        JToken value;
                        decimal number;
                        if (SourceVariable(variables, field, out value) &&
                            Decimal.TryParse(ScalarText(value), NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint,
                                CultureInfo.InvariantCulture, out number) && number >= -2000 && number <= 3000)
                            values[field] = number;
                    }
                    report["temperature_and_humidity_samples"] = values;
                    var settings = new JObject();
                    foreach (string field in new[] { "SCALE", "HVAC_MODE", "HVAC_STATE", "FAN_MODE", "FAN_STATE",
                        "HVAC_MODES_LIST", "FAN_MODES_LIST", "HOLD_MODE", "HOLD_MODES_LIST" })
                    {
                        JToken value;
                        if (SourceVariable(variables, field, out value) && value.Type == JTokenType.String &&
                            System.Text.RegularExpressions.Regex.IsMatch((string)value, @"^[A-Za-z0-9 ,;|_-]{0,160}$"))
                            settings[field] = value.DeepClone();
                    }
                    report["thermostat_settings"] = settings;
                    if (ScalarText(item["type"]) == "7" && values["TEMPERATURE"] != null && CurrentGeneration(generation))
                    {
                        try
                        {
                            string metadata = Request(host, token, "GET", path + "/commands", null);
                            report["command_metadata"] = BuildCommandMetadataReport(metadata, 200, id);
                        }
                        catch (Exception ex)
                        {
                            InvalidateRejectedToken(ex);
                            report["command_metadata_error"] = FailureKind(ex);
                        }
                    }
                }
                catch (Exception ex)
                {
                    InvalidateRejectedToken(ex);
                    report["error"] = FailureKind(ex);
                }
            }
            return results;
        }

        private JArray InspectSecurityInventory(JArray items, string host, string token, int generation)
        {
            var results = new JArray();
            bool hasIqPanel = System.Linq.Enumerable.Any(items, item =>
                ((string)item["name"] ?? "").IndexOf("IQPanel", StringComparison.OrdinalIgnoreCase) >= 0);
            if (!hasIqPanel) return results;
            foreach (JObject item in System.Linq.Enumerable.OfType<JObject>(items))
            {
                if (!CurrentGeneration(generation) || results.Count >= 12) break;
                string name = (string)item["name"] ?? "";
                int id;
                if ((!name.StartsWith("IQPanel", StringComparison.OrdinalIgnoreCase) &&
                     !name.Equals("Security Panel", StringComparison.OrdinalIgnoreCase) &&
                     !name.StartsWith("Security System", StringComparison.OrdinalIgnoreCase)) ||
                    !TrySourceId(item["item_id"], out id) || id == 0) continue;
                var observation = new JObject { ["item_id"] = id, ["name"] = name };
                results.Add(observation);
                try
                {
                    string json = Request(host, token, "GET", "/api/v1/items/" +
                        id.ToString(CultureInfo.InvariantCulture) + "/variables", null);
                    observation["variables"] = InspectNowPlayingVariables(json);
                    var states = new JObject();
                    var variables = JArray.Parse(json);
                    foreach (string field in new[] { "ARMED", "ARM_STATE", "ARMED_STATE", "ARMING_STATE",
                        "PARTITION_STATE", "PARTITION_STATUS", "ALARM_STATE", "ALARM_STATUS", "READY", "IS_READY",
                        "HOME_STATE", "AWAY_STATE", "DISARMED_STATE", "IS_ACTIVE", "OPEN_ZONE_COUNT", "ARMED_TYPE" })
                    {
                        JToken value;
                        if (!SourceVariable(variables, field, out value)) continue;
                        int numeric;
                        int maximum = field == "OPEN_ZONE_COUNT" ? UInt16.MaxValue : 1;
                        if (value.Type == JTokenType.Boolean && field != "OPEN_ZONE_COUNT")
                            states[field] = value.DeepClone();
                        else if ((value.Type == JTokenType.Integer || value.Type == JTokenType.String) &&
                            Int32.TryParse(value.ToString(), NumberStyles.None, CultureInfo.InvariantCulture, out numeric) &&
                            numeric >= 0 && numeric <= maximum)
                            states[field] = numeric;
                        else if (value.Type == JTokenType.String &&
                            System.Text.RegularExpressions.Regex.IsMatch((string)value,
                                @"^(disarmed|armed|armed[_ -](home|away|stay|night|instant)|arm(ed)?[_ -]?(away|stay|home|night)|ready|not[_ -]ready|arming|alarm|none|normal|on|off)$",
                                System.Text.RegularExpressions.RegexOptions.IgnoreCase))
                            states[field] = value.DeepClone();
                    }
                    observation["recognized_states"] = states;
                    if (SecurityNumber(variables, "IS_ACTIVE", 1) == 1 &&
                        name.StartsWith("Security System", StringComparison.OrdinalIgnoreCase) &&
                        CurrentGeneration(generation))
                    {
                        try
                        {
                            string metadata = Request(host, token, "GET", "/api/v1/items/" +
                                id.ToString(CultureInfo.InvariantCulture) + "/commands", null);
                            observation["keypad_command_metadata"] = BuildCommandMetadataReport(metadata, 200, id);
                        }
                        catch (Exception ex)
                        {
                            InvalidateRejectedToken(ex);
                            observation["keypad_metadata_error"] = FailureKind(ex);
                        }
                    }
                }
                catch (Exception ex)
                {
                    InvalidateRejectedToken(ex);
                    observation["error"] = FailureKind(ex);
                }
            }
            return results;
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
                    inventory["security_inspection"] = InspectSecurityInventory(all, host, token, gen);
                    inventory["comfort_inspection"] = InspectComfortInventory(all, (JArray)groups["comfort"], host, token, gen);
                    inventory["camera_inspection"] = InspectCameraInventory((JArray)groups["cameras"], host, token, gen);
                    inventory["outdoor_inspection"] = InspectOutdoorInventory(all, host, token, gen);
                    lock (_sync)
                        if (_securitySocketCapture != null && _securityCaptureGeneration == gen)
                            inventory["security_websocket_capture"] = _securitySocketCapture.DeepClone();
                    lock (_sync)
                        if (_lightingSceneOwnership != null && _lightingSceneOwnershipGeneration == _generation)
                            inventory["lighting_scene_ownership"] = _lightingSceneOwnership.DeepClone();
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

        private JObject _lightingSceneOwnership;
        private int _lightingSceneOwnershipGeneration;

        internal static JObject DescribeLightingSceneOwnership(JArray options, string idField, string displayField)
        {
            var scenes = new JArray();
            foreach (JObject row in System.Linq.Enumerable.Take(System.Linq.Enumerable.OfType<JObject>(options), 128))
            {
                var fields = new JObject();
                foreach (JProperty field in System.Linq.Enumerable.Take(row.Properties(), 48))
                {
                    if (!System.Text.RegularExpressions.Regex.IsMatch(field.Name, "^[A-Za-z_][A-Za-z0-9_]{0,63}$") || SensitiveMediaKey(field.Name)) continue;
                    int budget = 48;
                    JObject shape = NowPlayingShape(field.Value, field.Name, 0, ref budget);
                    int roomId;
                    string key = field.Name.Replace("_", "").ToLowerInvariant();
                    if ((key == "roomid" || key == "room") &&
                        Int32.TryParse(ScalarText(field.Value), NumberStyles.None, CultureInfo.InvariantCulture, out roomId) && roomId > 0)
                        shape["room_candidate_id"] = roomId;
                    fields[field.Name] = shape;
                }
                scenes.Add(new JObject { ["name"] = PlayingText(new JValue(SceneOptionName(row, idField, displayField))),
                    ["fields"] = fields });
            }
            return new JObject { ["generated_utc"] = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture),
                ["scene_count"] = options.Count, ["scenes"] = scenes };
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
                    if (kind == "light")
                    {
                        JObject report = DescribeLightingSceneOwnership(options, field, display);
                        lock (_sync)
                        {
                            if (_generation != generation) return;
                            _lightingSceneOwnership = report;
                            _lightingSceneOwnershipGeneration = generation;
                        }
                    }
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

        private sealed class TuneInBrowse
        {
            internal int Generation;
            internal ushort Proxy, Protocol;
            internal int Sequence;
            internal bool WaitForContent;
            internal JToken EmptyReply;
            internal readonly System.Threading.ManualResetEvent Completed = new System.Threading.ManualResetEvent(false);
            internal JToken Data;
        }

        private readonly System.Collections.Generic.Dictionary<string, TuneInBrowse> _tuneInBrowses =
            new System.Collections.Generic.Dictionary<string, TuneInBrowse>(StringComparer.Ordinal);

        private void CaptureTuneInBrowseResponse(JToken message, int generation)
        {
            if (!(message is JObject) || ScalarText(message["evtName"]) != "OnDataToUI") return;
            ushort item;
            if (!UInt16.TryParse(ScalarText(message["iddevice"]), out item)) return;
            var response = (message["data"] as JObject)?["RESPONSE"] as JObject;
            string navigator = ScalarText(response?["NAVID"]);
            if (navigator == null) return;
            lock (_sync)
            {
                TuneInBrowse browse;
                if (_generation != generation || !_tuneInBrowses.TryGetValue(navigator, out browse) ||
                    browse.Generation != generation || (browse.Proxy != item && (browse.Protocol == 0 || browse.Protocol != item)) || browse.Data != null ||
                    ScalarText(response["SEQ"]) != browse.Sequence.ToString(System.Globalization.CultureInfo.InvariantCulture)) return;
                JToken data = response["DATA"] ?? new JValue((string)null);
                if (browse.WaitForContent && (data.Type == JTokenType.Null ||
                    data.Type == JTokenType.String && String.IsNullOrWhiteSpace(data.ToString())))
                {
                    browse.EmptyReply = data;
                    return;
                }
                browse.Data = data;
                browse.Completed.Set();
            }
        }

        private JObject RequestTuneInMenu(string host, string token, ushort room, ushort proxy, int generation,
            Func<bool> current, string navigator, int sequence, string screen, string url)
        {
            var args = new System.Xml.Linq.XElement("args",
                new System.Xml.Linq.XElement("arg", new System.Xml.Linq.XAttribute("name", "screen"), screen));
            if (url != null)
                args.Add(new System.Xml.Linq.XElement("arg", new System.Xml.Linq.XAttribute("name", "URL"), url));
            return WaitForNavigatorData(proxy, 0, generation, current, navigator, sequence, 8000, false, () =>
                SendTuneInNavigator(host, token, room, proxy, navigator, sequence, "GetBrowseMenu",
                    args.ToString(System.Xml.Linq.SaveOptions.DisableFormatting))) as JObject;
        }

        private JToken WaitForNavigatorData(ushort proxy, ushort protocol, int generation, Func<bool> current,
            string navigator, int sequence, int timeoutMs, bool waitForContent, Action send)
        {
            var browse = new TuneInBrowse { Generation = generation, Proxy = proxy, Protocol = protocol, Sequence = sequence, WaitForContent = waitForContent };
            bool registered = false;
            try
            {
                lock (_sync)
                {
                    if (!current()) return null;
                    _tuneInBrowses.Add(navigator, browse);
                    registered = true;
                }
                send();
                browse.Completed.WaitOne(timeoutMs);
                lock (_sync)
                {
                    if (!current()) return null;
                    JToken reply = browse.Data ?? browse.EmptyReply;
                    if (reply == null) throw new TimeoutException("Navigator callback");
                    return reply;
                }
            }
            finally
            {
                if (registered) lock (_sync) _tuneInBrowses.Remove(navigator);
                browse.Completed.Dispose();
            }
        }

        internal static JObject DescribeAmazonNavigatorData(JToken data)
        {
            int budget = 256;
            if (data.Type != JTokenType.String) return NowPlayingShape(data, "", 0, ref budget);
            string text = data.ToString();
            var report = new JObject { ["type"] = "String", ["length"] = text.Length };
            if (text.Length > 262144) { report["format"] = "too_large"; return report; }
            string decoded = System.Net.WebUtility.HtmlDecode(text).Trim().TrimStart('\uFEFF').TrimStart();
            report["html_encoded"] = !String.Equals(text, System.Net.WebUtility.HtmlDecode(text), StringComparison.Ordinal);
            if (decoded.Length == 0) { report["format"] = "empty"; return report; }
            if (decoded[0] == '<')
            {
                report["format"] = "xml";
                var tags = new JArray();
                var seen = new System.Collections.Generic.HashSet<string>(StringComparer.Ordinal);
                foreach (System.Text.RegularExpressions.Match match in System.Text.RegularExpressions.Regex.Matches(
                    decoded, @"<([A-Za-z_][A-Za-z_0-9.-]{0,63})(?=[\s/>])"))
                {
                    string tag = match.Groups[1].Value;
                    if (!SensitiveMediaKey(tag) && seen.Add(tag)) tags.Add(tag);
                    if (tags.Count >= 40) break;
                }
                report["xml_tags"] = tags;
                return report;
            }
            if (decoded[0] == '{' || decoded[0] == '[' || decoded[0] == '"')
            {
                try
                {
                    JToken json = JToken.Parse(decoded);
                    report["format"] = "json";
                    report["decoded_shape"] = NowPlayingShape(json, "", 0, ref budget);
                }
                catch (Newtonsoft.Json.JsonReaderException) { report["format"] = "invalid_json"; }
                return report;
            }
            Uri uri;
            report["format"] = Uri.TryCreate(decoded, UriKind.Absolute, out uri) &&
                (uri.Scheme == "https" || uri.Scheme == "http") ? "url" : "opaque_text";
            foreach (string status in new[] { "OK", "ACK", "PENDING", "SUCCESS", "ERROR", "UNSUPPORTED", "NOT_SUPPORTED", "NOT_IMPLEMENTED" })
                if (String.Equals(decoded, status, StringComparison.OrdinalIgnoreCase)) { report["status"] = status; break; }
            return report;
        }

        private static System.Collections.Generic.IEnumerable<JObject> AmazonDefinitionRows(JToken rows)
        {
            if (rows is JObject) yield return (JObject)rows;
            var array = rows as JArray;
            if (array == null) yield break;
            for (int i = 0; i < Math.Min(array.Count, 32); i++)
                if (array[i] is JObject) yield return (JObject)array[i];
        }

        internal static JArray DescribeAmazonTabs(JToken data)
        {
            var result = new JArray();
            var tabs = ((data as JObject)?["Tabs"] as JObject)?["Tab"];
            foreach (JObject tab in AmazonDefinitionRows(tabs))
            {
                string screen = NowPlayingLabel(tab["ScreenId"]);
                var row = new JObject { ["screen_id"] = screen };
                var command = tab["ScreenCommand"] as JObject;
                if (command != null)
                {
                    var args = new JArray();
                    foreach (JObject parameter in AmazonDefinitionRows((command["Params"] as JObject)?["Param"]))
                    {
                        string name = NowPlayingLabel(parameter["Name"]);
                        string type = NowPlayingLabel(parameter["Type"]);
                        JToken value = parameter["Value"];
                        var arg = new JObject { ["name"] = name, ["type"] = type, ["has_value"] = value != null };
                        if (value?.Type == JTokenType.String)
                        {
                            string text = value.ToString();
                            arg["value_length"] = text.Length;
                            if (String.Equals(name, "screenId", StringComparison.OrdinalIgnoreCase))
                            {
                                foreach (string screenId in new[] { screen, "HomeScreen", "ListScreen", "SearchScreen" })
                                    if (screenId != null && text == screenId) { arg["screen_value"] = screenId; break; }
                            }
                        }
                        args.Add(arg);
                    }
                    row["screen_command"] = new JObject { ["name"] = NowPlayingLabel(command["Name"]),
                        ["type"] = NowPlayingLabel(command["Type"]), ["parameters"] = args };
                }
                result.Add(row);
            }
            return result;
        }

        internal void InspectAmazonBrowse(ushort room, ushort proxy)
        {
            AudioRoom registration;
            int generation, epoch;
            lock (_sync)
            {
                if (!_online || !_audioRooms.TryGetValue(room, out registration)) return;
                generation = _generation;
                epoch = _feedbackEpoch;
            }
            Func<bool> current = () => InspectionCurrent(room, registration, generation, epoch);
            Queue(delegate
            {
                string phase = "source identification";
                try
                {
                    string host, token;
                    if (!GetConnection(out host, out token)) return;
                    lock (_sync) { if (!current()) return; }
                    string path = "/api/v1/items/" + proxy;
                    string detail = Request(host, token, "GET", path, null);
                    JObject mapping;
                    ushort driver = ServiceDriverCandidate(detail, proxy, out mapping);
                    if (driver == 0 || !ServiceDriverIdentity(Request(host, token, "GET", "/api/v1/items/" + driver, null), driver))
                        throw new InvalidDataException("Amazon Music proxy relationship unavailable");
                    phase = "GetTabList";
                    string navigator = Guid.NewGuid().ToString("N");
                    const string command = "GetTabList";
                    var args = new JObject { ["ROOMID"] = room, ["NAVID"] = navigator, ["SEQ"] = 1, ["LOCALE"] = "en_US" };
                    var payload = new JObject { ["async"] = true, ["command"] = command, ["tParams"] = args };
                    JToken data = WaitForNavigatorData(proxy, driver, generation, current, navigator, 1, 15000, false, () =>
                    {
                        int status;
                        Request(host, token, "POST", path + "/commands", payload.ToString(Formatting.None), out status, true);
                        EmitMessage("Amazon Music GetTabList HTTP " + status + "; waiting for callback");
                    });
                    if (data == null) return;
                    JArray tabs = DescribeAmazonTabs(data);
                    lock (_sync)
                    {
                        if (!current()) return;
                        _roomMediaInspections.Enqueue(new JObject { ["inspection_kind"] = "amazon_browse",
                            ["room_id"] = room, ["source_id"] = proxy, ["command"] = command,
                            ["data"] = DescribeAmazonNavigatorData(data), ["tab_definitions"] = tabs,
                            ["generated_utc"] = DateTime.UtcNow.ToString("o", System.Globalization.CultureInfo.InvariantCulture) });
                        while (_roomMediaInspections.Count > 4) _roomMediaInspections.Dequeue();
                    }
                    EmitMessage("Amazon Music tab definitions recorded (" + tabs.Count + " tabs)");
                    phase = "Browse";
                    args["SEQ"] = 2;
                    args["screenId"] = "HomeScreen";
                    payload["command"] = "Browse";
                    JToken menu = WaitForNavigatorData(proxy, driver, generation, current, navigator, 2, 15000, true, () =>
                    {
                        int status;
                        Request(host, token, "POST", path + "/commands", payload.ToString(Formatting.None), out status, true);
                        EmitMessage("Amazon Music active-playback Browse HTTP " + status + "; waiting for menu data");
                    });
                    if (menu == null) return;
                    JObject shape = DescribeAmazonNavigatorData(menu);
                    lock (_sync)
                    {
                        if (!current()) return;
                        _roomMediaInspections.Enqueue(new JObject { ["inspection_kind"] = "amazon_browse",
                            ["room_id"] = room, ["source_id"] = proxy, ["command"] = "Browse", ["data"] = shape,
                            ["generated_utc"] = DateTime.UtcNow.ToString("o", System.Globalization.CultureInfo.InvariantCulture) });
                        while (_roomMediaInspections.Count > 4) _roomMediaInspections.Dequeue();
                    }
                    var list = (menu as JObject)?["List"] as JObject;
                    EmitMessage(list != null ? "Amazon Music active-playback Browse returned a List; pulse Export_Inventory" :
                        "Amazon Music active-playback Browse returned " + ((string)shape["format"] ?? menu.Type.ToString()) + "; pulse Export_Inventory");
                }
                catch (Exception ex)
                {
                    lock (_sync) { if (!current()) return; }
                    InvalidateRejectedToken(ex);
                    EmitMessage("Amazon Music " + phase + " test failed: " + FailureKind(ex));
                }
            });
        }

        private void SendTuneInNavigator(string host, string token, ushort room, ushort proxy, string navigator,
            int sequence, string command, string args)
        {
            var parameters = new JObject { ["ROOMID"] = room, ["NAVID"] = navigator, ["SEQ"] = sequence, ["ARGS"] = args };
            string payload = new JObject { ["async"] = true, ["command"] = command, ["tParams"] = parameters }.ToString(Formatting.None);
            int status;
            Request(host, token, "POST", "/api/v1/items/" + proxy + "/commands", payload, out status, true);
        }

        private static string TuneInGuideId(JObject row)
        {
            string id = ScalarText(row?["guide_id"]);
            return String.IsNullOrEmpty(id) ? ScalarText(row?["preset_id"]) : id;
        }

        internal static string BuildTuneInStationArgs(JObject row)
        {
            string id = TuneInGuideId(row);
            if (id == null || !System.Text.RegularExpressions.Regex.IsMatch(id, @"\As[1-9][0-9]{0,9}\z")) return null;
            string url = ScalarText(row["URL"]);
            Uri stationUrl = TuneInUrl(url, "/Tune.ashx");
            if (stationUrl == null ||
                !System.Text.RegularExpressions.Regex.IsMatch(stationUrl.Query, @"[?&]id=" + id + @"(?:&|$)")) return null;
            return BuildTuneInRowArgs(row);
        }

        private static Uri TuneInUrl(string url, string path)
        {
            Uri uri;
            return url != null && url.Length <= 2048 && Uri.TryCreate(url, UriKind.Absolute, out uri) &&
                (uri.Scheme == "http" || uri.Scheme == "https") && uri.IsDefaultPort &&
                String.Equals(uri.Host, "opml.radiotime.com", StringComparison.OrdinalIgnoreCase) &&
                uri.UserInfo.Length == 0 && uri.Fragment.Length == 0 &&
                String.Equals(uri.AbsolutePath, path, StringComparison.OrdinalIgnoreCase) ? uri : null;
        }

        private static string BuildTuneInRowArgs(JObject row)
        {
            var args = new System.Xml.Linq.XElement("args",
                new System.Xml.Linq.XElement("arg", new System.Xml.Linq.XAttribute("name", "screen"), "MyFavorites"));
            foreach (string name in new[] { "type", "URL", "guide_id", "item", "is_preset", "key", "text", "image" })
            {
                JToken value = row[name];
                string text = value?.Type == JTokenType.Boolean ? ((bool)value ? "true" : "false") : ScalarText(value);
                if (text != null && text.Length <= 2048)
                    args.Add(new System.Xml.Linq.XElement("arg", new System.Xml.Linq.XAttribute("name", name), text));
            }
            return args.ToString(System.Xml.Linq.SaveOptions.DisableFormatting);
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
