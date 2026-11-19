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

        private System.Collections.Generic.Dictionary<ushort, string> _panelRoomNames =
            new System.Collections.Generic.Dictionary<ushort, string>();

        private void CachePanelRoomNames(string json, int generation)
        {
            var names = new System.Collections.Generic.Dictionary<ushort, string>();
            foreach (JToken item in JArray.Parse(json))
            {
                if (!(item is JObject) || ScalarText(item["type"]) != "8") continue;
                ushort id;
                string name = ScalarText(item["name"]);
                if (UInt16.TryParse(ScalarText(item["id"] ?? item["item_id"]), out id) &&
                    id != 0 && !String.IsNullOrWhiteSpace(name) && name.Length <= 160) names[id] = name;
            }
            lock (_sync) if (_generation == generation) _panelRoomNames = names;
        }

        internal string PanelRoomName(ushort room)
        {
            lock (_sync)
            {
                string name;
                return _panelRoomNames.TryGetValue(room, out name) ? name : "Room " + room;
            }
        }

        private void DiscoverAuthenticatedRoomDevices(int generation, string host, string token, string json)
        {
            Queue(delegate
            {
                string stage = "read inventory";
                int skipped = 0;
                ushort firstSkipped = 0;
                try
                {
                    if (!CurrentGeneration(generation)) return;
                    EmitMessage("Room discovery started via Director");
                    var candidates = JArray.Parse(json);
                    stage = "select proxy candidates";
                    var rooms = new System.Collections.Generic.Dictionary<ushort, System.Collections.Generic.Dictionary<ushort, Control4_Room_Devices.Entry>>();
                    var seen = new System.Collections.Generic.HashSet<ushort>();
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
                        stage = "request item " + id;
                        JObject detail;
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
                        if (ScalarText(detail["type"]) != "7") continue;
                        string filename = ScalarText(detail["filename"]);
                        bool shade = String.Equals(filename, "blind.c4i", StringComparison.OrdinalIgnoreCase);
                        if (!shade && !String.Equals(filename, "light.c4i", StringComparison.OrdinalIgnoreCase) &&
                            !String.Equals(filename, "light_v2.c4i", StringComparison.OrdinalIgnoreCase)) continue;
                        ushort room;
                        string label = ScalarText(detail["name"]);
                        if (!UInt16.TryParse(ScalarText(detail["roomId"]), out room) || room == 0 ||
                            String.IsNullOrWhiteSpace(label) || label.Length > 320) continue;
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
                    }
                    int count = 0;
                    foreach (var room in rooms.Values) count += room.Count;
                    EmitMessage("Discovered " + count + " room devices via Director. Skipped " + skipped + (skipped == 0 ? "" : " (first item " + firstSkipped + ")") + ". Reselect a room on the panel.");
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
                    JArray all = InventoryItems(Request(host, token, "GET", "/api/v1/items", null));
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
                        inventory["live_room_item_detail_probe"] = ProbeRoomItems(host, token, gen);
                        inventory["light_feedback_probe"] = ProbeLightFeedback(host, token, gen);
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

        // Diagnostic fixtures verified in Composer. These IDs are never used for routing.
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
                    foreach (string field in new[] { "roomId", "locationId", "parentId", "driver_id", "driverId", "type" })
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
            foreach (ushort id in new ushort[] { 437, 524, 602, 1932 })
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
                        row["protocol_variables"] = ReadLightProbeVariables(host, token, protocol, gen);
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
                if (value != null && (name.IndexOf("level", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    name.IndexOf("state", StringComparison.OrdinalIgnoreCase) >= 0))
                {
                    decimal number;
                    bool boolean;
                    if (Decimal.TryParse(value.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out number) &&
                        number >= 0 && number <= 100) row["value"] = number;
                    else if (Boolean.TryParse(value.ToString(), out boolean)) row["value"] = boolean;
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
                _lightProbeEvents.Add(new JObject { ["id"] = id, ["event"] = name, ["fields"] = fields });
            }
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
