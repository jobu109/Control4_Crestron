using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Newtonsoft.Json.Linq;

namespace Control4Crestron
{
    public sealed partial class GatewayCore
    {
        public event EventHandler<RoomCatalogEventArgs> RoomCatalogChanged;
        private readonly Dictionary<ushort, object> _roomCatalogWork = new Dictionary<ushort, object>();
        private readonly Queue<JObject> _roomCatalogHistory = new Queue<JObject>();
        private sealed class ServicePresetList
        {
            internal ushort DriverId;
            internal JObject Public;
            internal Dictionary<string, string> Selectors;
        }
        private readonly Dictionary<ushort, ServicePresetList> _servicePresetLists = new Dictionary<ushort, ServicePresetList>();
        internal void ClearRoomMediaForSourceChange(ushort roomId)
        {
            lock (_sync)
            {
                _listenOpenWork.Remove(roomId);
                _roomCatalogWork.Remove(roomId);
                _nativeCatalogSelections.Remove(roomId);
                _servicePresetLists.Remove(roomId);
                PublishRoomCatalog(roomId, "service_presets", null);
            }
        }
        private static JObject ServiceDetail(string json, ushort expectedId)
        {
            if (string.IsNullOrEmpty(json) || json.Length > 262144) throw new InvalidDataException("Invalid service detail size");
            var list = JToken.Parse(json) as JArray;
            if (list == null || list.Count != 1 || !(list[0] is JObject)) throw new InvalidDataException("Expected one service detail item");
            var item = (JObject)list[0]; ushort id;
            JToken raw = item["id"] ?? item["item_id"];
            if (raw == null || !UInt16.TryParse(raw.ToString(), NumberStyles.None, CultureInfo.InvariantCulture, out id) || id != expectedId) throw new InvalidDataException("Service identity mismatch");
            return item;
        }
        internal static ushort ServiceDriverCandidate(string json, ushort sourceId, out JObject report)
        {
            JObject item = ServiceDetail(json, sourceId);
            report = new JObject(new JProperty("source_id", sourceId), new JProperty("status", "unresolved"));
            var shapes = new JObject();
            foreach (JProperty field in item.Properties())
                if (shapes.Count < 48 && !SensitiveMediaKey(field.Name) && System.Text.RegularExpressions.Regex.IsMatch(field.Name, "^[A-Za-z_][A-Za-z0-9_]{0,63}$")) shapes[field.Name] = field.Value.Type.ToString();
            report["source_detail_fields"] = shapes;
            int type;
            if (item["type"] == null || !Int32.TryParse(item["type"].ToString(), out type) || type != 7)
            { report["reason"] = "source_not_verified_proxy_type"; return 0; }
            var candidates = new HashSet<ushort>(); var relationships = new JObject(); bool explicitDriver = false;
            foreach (string name in new[] { "driver_id", "driverId", "protocol_id", "protocolId" })
                if (item[name] != null) explicitDriver = true;
            string[] fields = explicitDriver ? new[] { "driver_id", "driverId", "protocol_id", "protocolId" } : new[] { "parent_id", "parentId", "parent" };
            bool invalid = false;
            foreach (string name in fields)
            {
                JToken value = item[name]; if (value == null) continue;
                if (value is JObject) value = value["id"] ?? value["item_id"];
                ushort candidate;
                if (value == null || (value.Type != JTokenType.Integer && value.Type != JTokenType.String) || !UInt16.TryParse(value.ToString(), NumberStyles.None, CultureInfo.InvariantCulture, out candidate) || candidate == 0 || candidate == sourceId)
                { invalid = true; continue; }
                candidates.Add(candidate); relationships[name] = candidate;
            }
            report["relationships"] = relationships;
            if (invalid || candidates.Count != 1) { report["reason"] = "missing_ambiguous_or_invalid_relationship"; return 0; }
            foreach (ushort candidate in candidates) { report["candidate_driver_id"] = candidate; return candidate; }
            return 0;
        }
        internal static bool ServiceDriverIdentity(string json, ushort driverId)
        {
            JObject item = ServiceDetail(json, driverId); int type;
            return item["type"] != null && Int32.TryParse(item["type"].ToString(), out type) && type == 6;
        }
        internal static JObject BuildServicePresetList(string json, ushort roomId, ushort driverId, ushort sourceId, out Dictionary<string, string> selectors)
        {
            if (roomId == 0 || driverId == 0 || sourceId == 0 || string.IsNullOrEmpty(json) || json.Length > 1048576) throw new InvalidDataException("Invalid service catalog");
            JToken root = JToken.Parse(json); var source = root as JArray;
            if (source == null && root is JObject) source = MediaMetadataField((JObject)root, "items", "stations", "results", "media", "data") as JArray;
            if (source == null) throw new InvalidDataException("Unknown service catalog schema");
            selectors = new Dictionary<string, string>(StringComparer.Ordinal);
            var items = new JArray(); int omitted = 0, visited = 0;
            var duplicateIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (JToken record in source)
            {
                if (++visited > 2048) break;
                var item = record as JObject;
                int owner;
                if (item == null || !Int32.TryParse((item["device_id"] ?? new JValue("")).ToString(), NumberStyles.None, CultureInfo.InvariantCulture, out owner) || owner != sourceId) continue;
                ulong number;
                string id = (item["id"] ?? new JValue("")).ToString();
                string name = SetupName(item["name"], "").Trim();
                if (name.Length > 512) name = name.Substring(0, 512);
                string selector = item["location"] != null && item["location"].Type == JTokenType.String ? (string)item["location"] : "";
                if (!UInt64.TryParse(id, NumberStyles.None, CultureInfo.InvariantCulture, out number) || number == 0 || name.Length == 0 || !IsServiceSelector(selector))
                { omitted++; continue; }
                id = number.ToString(CultureInfo.InvariantCulture);
                if (selectors.ContainsKey(id) || duplicateIds.Contains(id)) { duplicateIds.Add(id); selectors.Remove(id); omitted++; continue; }
                selectors.Add(id, selector);
                items.Add(new JObject(new JProperty("id", id), new JProperty("name", name), new JProperty("label_available", true)));
            }
            for (int i = items.Count - 1; i >= 0; i--) if (duplicateIds.Contains((string)items[i]["id"])) items.RemoveAt(i);
            return new JObject(new JProperty("room_id", roomId), new JProperty("kind", "service_presets"), new JProperty("scope", "service_saved_presets"),
                new JProperty("driver_id", driverId), new JProperty("source_id", sourceId), new JProperty("revision", Guid.NewGuid().ToString("N")),
                new JProperty("generated_utc", DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture)), new JProperty("items", items), new JProperty("total", items.Count),
                new JProperty("source_total", source.Count), new JProperty("omitted", omitted), new JProperty("unnamed", 0), new JProperty("truncated", source.Count > 2048),
                new JProperty("selection_supported", items.Count > 0));
        }
        public void BrowseServicePresets(ushort id, ushort driverId, ushort sourceId)
        {
            if (sourceId == 0) { EmitMessage("Select an audio source before automatic service browsing"); return; }
            int gen, epoch; AudioRoom registration; object work = new object();
            lock (_sync)
            {
                if (!_online || !_roomModules.ContainsKey(id) || !_audioRooms.TryGetValue(id, out registration)) { EmitMessage("Connect and initialize Sources before service browsing"); return; }
                gen = _generation; epoch = _feedbackEpoch; _roomCatalogWork[id] = work;
                _nativeCatalogSelections.Remove(id); _servicePresetLists.Remove(id); PublishRoomCatalog(id, "service_presets", null);
            }
            Queue(delegate
            {
                try
                {
                    string host, token; if (!GetConnection(out host, out token)) return;
                    lock (_sync) { if (!CatalogWorkCurrent(id, registration, gen, epoch, work)) return; }
                    string driverCommands = null; JObject mapping = null;
                    if (driverId == 0)
                    {
                        try
                        {
                            string detail = Request(host, token, "GET", "/api/v1/items/" + sourceId, null);
                            driverId = ServiceDriverCandidate(detail, sourceId, out mapping);
                            if (driverId == 0) throw new InvalidDataException("No explicit protocol relationship");
                            string driverDetail = Request(host, token, "GET", "/api/v1/items/" + driverId, null);
                            if (!ServiceDriverIdentity(driverDetail, driverId)) { mapping["reason"] = "candidate_not_protocol_type"; throw new InvalidDataException("Candidate is not a protocol driver"); }
                            driverCommands = Request(host, token, "GET", "/api/v1/items/" + driverId + "/commands", null);
                            mapping["driver_commands"] = BuildCommandMetadataReport(driverCommands, 200, driverId);
                            mapping["status"] = "verified_relationship";
                        }
                        finally
                        {
                            if (mapping != null)
                            {
                                mapping["inspection_kind"] = "service_driver_mapping"; mapping["room_id"] = id;
                                mapping["generated_utc"] = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture);
                                lock (_sync) { if (CatalogWorkCurrent(id, registration, gen, epoch, work)) { _roomMediaInspections.Enqueue(mapping); while (_roomMediaInspections.Count > 4) _roomMediaInspections.Dequeue(); } }
                            }
                        }
                        lock (_sync) { if (!CatalogWorkCurrent(id, registration, gen, epoch, work)) return; }
                    }
                    string commands = Request(host, token, "GET", "/api/v1/items/" + id + "/commands", null);
                    JObject plan = BuildRoomMediaPlan(commands, host, id); string path = RoomCatalogPath(id, "audio_stations"); bool allowed = false;
                    foreach (JToken link in (JArray)plan["advertised_get_paths"]) if ((string)link == path) allowed = true;
                    if (!allowed) throw new InvalidDataException("Service catalog GET not advertised");
                    if (driverCommands == null) driverCommands = Request(host, token, "GET", "/api/v1/items/" + driverId + "/commands", null);
                    lock (_sync) { if (!CatalogWorkCurrent(id, registration, gen, epoch, work)) return; }
                    string json = Request(host, token, "GET", path, null);
                    Dictionary<string, string> selectors;
                    JObject catalog = BuildServicePresetList(json, id, driverId, sourceId, out selectors);
                    var adapters = new SortedSet<string>(StringComparer.Ordinal);
                    foreach (string mediaId in new List<string>(selectors.Keys))
                    {
                        string selector = selectors[mediaId];
                        try
                        {
                            if (IsPandoraItem(selector)) { BuildPandoraItemSelection(driverCommands, id, driverId, selector, false); adapters.Add("Play Item"); }
                            else { BuildServicePresetSelection(driverCommands, id, driverId, selector); adapters.Add("SelectPreset"); }
                        }
                        catch (InvalidDataException) { selectors.Remove(mediaId); catalog["omitted"] = (int)catalog["omitted"] + 1; }
                    }
                    var publicItems = (JArray)catalog["items"];
                    for (int i = publicItems.Count - 1; i >= 0; i--) if (!selectors.ContainsKey((string)publicItems[i]["id"])) publicItems.RemoveAt(i);
                    catalog["total"] = publicItems.Count; catalog["selection_supported"] = publicItems.Count > 0;
                    catalog["selection_adapters"] = StringArray(adapters);
                    if (adapters.Contains("Play Item")) { catalog["scope"] = "service_saved_items"; catalog["pandora_shuffle"] = "Off"; }
                    lock (_sync)
                    {
                        if (!CatalogWorkCurrent(id, registration, gen, epoch, work)) return;
                        bool shuffle;
                        if (adapters.Contains("Play Item") && _pandoraShuffle.TryGetValue(id, out shuffle)) catalog["pandora_shuffle"] = shuffle ? "On" : "Off";
                        if (mapping != null) { mapping["status"] = "verified_relationship_and_catalog_inspected"; mapping["selection_adapters"] = StringArray(adapters); mapping["selectable_items"] = publicItems.Count; }
                        _servicePresetLists[id] = new ServicePresetList { DriverId = driverId, Public = catalog, Selectors = selectors };
                        _roomCatalogHistory.Enqueue((JObject)catalog.DeepClone()); while (_roomCatalogHistory.Count > 4) _roomCatalogHistory.Dequeue();
                        PublishRoomCatalog(id, "service_presets", catalog);
                    }
                    EmitMessage("Room " + id + " service presets loaded (" + catalog["total"] + " items); browsing did not start playback");
                }
                catch (Exception ex)
                {
                    lock (_sync) { if (!CatalogWorkCurrent(id, registration, gen, epoch, work)) return; PublishRoomCatalog(id, "service_presets", null); }
                    InvalidateRejectedToken(ex); EmitMessage("Service preset browse failed: " + FailureKind(ex) + "; Export_Inventory includes available mapping evidence");
                }
                finally { lock (_sync) { object current; if (_roomCatalogWork.TryGetValue(id, out current) && Object.ReferenceEquals(current, work)) _roomCatalogWork.Remove(id); } }
            });
        }
        private bool ServiceListCurrent(ushort roomId, object expected)
        {
            ServicePresetList current;
            return expected == null || _servicePresetLists.TryGetValue(roomId, out current) && Object.ReferenceEquals(current, expected);
        }
        private void SelectListedServicePreset(ushort roomId, string revision, string mediaId)
        {
            ServicePresetList list; string selector;
            lock (_sync)
            {
                if (!_online || !_servicePresetLists.TryGetValue(roomId, out list) || (string)list.Public["revision"] != revision || !list.Selectors.TryGetValue(mediaId, out selector))
                { EmitMessage("Service preset selection rejected: stale or unavailable list/item"); return; }
            }
            SelectServicePresetCore(roomId, list.DriverId, selector, list);
        }
        private static string RoomCatalogPath(ushort id, string kind)
        {
            if (id == 0) throw new ArgumentOutOfRangeException("id");
            string suffix;
            switch (kind)
            {
                case "playlists": suffix = "albums/playlists"; break;
                case "albums": suffix = "albums"; break;
                case "audio_stations": suffix = "broadcast_audio"; break;
                case "movies": suffix = "movies"; break;
                case "video_stations": suffix = "broadcast_video"; break;
                default: throw new ArgumentException("Unknown native media kind");
            }
            return "/api/v1/locations/rooms/" + id + "/media/" + suffix;
        }
        public void BrowseRoomCatalog(ushort id, string kind)
        {
            string path;
            try { path = RoomCatalogPath(id, kind); }
            catch (ArgumentException) { EmitMessage("Unknown native room media list"); return; }
            int gen, epoch; AudioRoom registration; object work = new object();
            lock (_sync)
            {
                if (!_online || !_roomModules.ContainsKey(id) || !_audioRooms.TryGetValue(id, out registration))
                { EmitMessage("Connect and initialize Sources before browsing room media"); return; }
                gen = _generation; epoch = _feedbackEpoch; _roomCatalogWork[id] = work;
                _nativeCatalogSelections.Remove(id);
                _servicePresetLists.Remove(id);
                PublishRoomCatalog(id, kind, null);
            }
            Queue(delegate
            {
                try
                {
                    string host, token;
                    if (!GetConnection(out host, out token)) return;
                    lock (_sync) { if (!CatalogWorkCurrent(id, registration, gen, epoch, work)) return; }
                    string commands = Request(host, token, "GET", "/api/v1/items/" + id + "/commands", null);
                    JObject plan = BuildRoomMediaPlan(commands, host, id);
                    bool advertised = false;
                    foreach (JToken link in (JArray)plan["advertised_get_paths"]) if ((string)link == path) advertised = true;
                    if (!advertised) throw new InvalidDataException("Catalog GET not advertised");
                    lock (_sync) { if (!CatalogWorkCurrent(id, registration, gen, epoch, work)) return; }
                    string json = Request(host, token, "GET", path, null);
                    JObject catalog = BuildRoomCatalog(json, id, kind);
                    bool selectionAllowed = false;
                    try
                    {
                        var items = (JArray)catalog["items"];
                        if (items.Count > 0)
                        {
                            BuildNativeMediaSelection(commands, id, kind, (string)items[0]["id"]);
                            selectionAllowed = true;
                        }
                    }
                    catch (InvalidDataException) { selectionAllowed = false; }
                    catalog["selection_supported"] = selectionAllowed;
                    lock (_sync)
                    {
                        if (!CatalogWorkCurrent(id, registration, gen, epoch, work)) return;
                        _nativeCatalogSelections[id] = new NativeCatalogSelection { Registration = registration, Generation = gen, Epoch = epoch,
                            Catalog = catalog, Enabled = selectionAllowed };
                        _roomCatalogHistory.Enqueue((JObject)catalog.DeepClone());
                        while (_roomCatalogHistory.Count > 4) _roomCatalogHistory.Dequeue();
                        PublishRoomCatalog(id, kind, catalog);
                    }
                    EmitMessage("Room " + id + " native " + kind + " list loaded (" + catalog["total"] + " items); read-only");
                }
                catch (Exception ex)
                {
                    lock (_sync) { if (!CatalogWorkCurrent(id, registration, gen, epoch, work)) return; }
                    InvalidateRejectedToken(ex);
                    lock (_sync)
                    {
                        if (!CatalogWorkCurrent(id, registration, gen, epoch, work)) return;
                        PublishRoomCatalog(id, kind, null);
                    }
                    EmitMessage("Native room media list failed: " + FailureKind(ex));
                }
                finally
                {
                    lock (_sync) { object current; if (_roomCatalogWork.TryGetValue(id, out current) && Object.ReferenceEquals(current, work)) _roomCatalogWork.Remove(id); }
                }
            });
        }
        private bool CatalogWorkCurrent(ushort id, AudioRoom registration, int gen, int epoch, object work)
        {
            object current;
            return InspectionCurrent(id, registration, gen, epoch) && _roomCatalogWork.TryGetValue(id, out current) && Object.ReferenceEquals(work, current);
        }
        private void PublishRoomCatalog(ushort id, string kind, JObject catalog)
        {
            var handler = RoomCatalogChanged;
            if (handler != null)
            {
                var args = new RoomCatalogEventArgs { RoomId = id, Kind = kind, Valid = (ushort)(catalog == null ? 0 : 1), Json = catalog == null ? "" : catalog.ToString() };
                SafeEvent(delegate { handler(this, args); });
            }
        }
        private void InvalidateRoomCatalogs()
        {
            lock (_sync)
            {
                _roomCatalogWork.Clear();
                _nativeCatalogSelections.Clear();
                _servicePresetLists.Clear();
                foreach (ushort id in new List<ushort>(_roomModules.Keys)) PublishRoomCatalog(id, "", null);
            }
        }
        internal static JObject BuildRoomCatalog(string json, ushort id, string kind)
        {
            RoomCatalogPath(id, kind);
            if (String.IsNullOrEmpty(json) || json.Length > 1048576) throw new InvalidDataException("Invalid catalog size");
            JToken root = JToken.Parse(json);
            var source = root as JArray;
            if (source == null && root is JObject) source = MediaMetadataField((JObject)root, "items", "movies", "playlists", "albums", "stations", "results", "media", "data") as JArray;
            if (source == null) throw new InvalidDataException("Unknown room catalog schema");
            var items = new JArray(); var seen = new HashSet<string>(StringComparer.Ordinal);
            int visited = 0, omitted = 0, unnamed = 0;
            foreach (JToken item in source)
            {
                if (++visited > 2048) break;
                var entry = item as JObject;
                JToken rawId = entry == null ? null : MediaMetadataField(entry, "mediaid", "id");
                ulong number;
                if (rawId == null || (rawId.Type != JTokenType.Integer && rawId.Type != JTokenType.String) ||
                    !UInt64.TryParse(rawId.ToString(), NumberStyles.None, CultureInfo.InvariantCulture, out number) || number == 0)
                { omitted++; continue; }
                string key = number.ToString(CultureInfo.InvariantCulture);
                if (!seen.Add(key)) { omitted++; continue; }
                JToken label = MediaMetadataField(entry, "name", "title", "label", "mediaName");
                string name = SetupName(label, "").Trim();
                bool labelAvailable = name.Length != 0;
                if (!labelAvailable) { name = kind + " " + key; unnamed++; }
                items.Add(new JObject(new JProperty("id", key), new JProperty("name", name), new JProperty("label_available", labelAvailable)));
            }
            return new JObject(new JProperty("room_id", id), new JProperty("kind", kind), new JProperty("revision", Guid.NewGuid().ToString("N")), new JProperty("scope", "native_room_catalog_not_service_library"),
                new JProperty("generated_utc", DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture)),
                new JProperty("total", items.Count), new JProperty("source_total", source.Count), new JProperty("omitted", omitted),
                new JProperty("unnamed", unnamed), new JProperty("truncated", source.Count > 2048), new JProperty("items", items),
                new JProperty("selection_supported", false));
        }
        private JArray RoomCatalogReports()
        {
            var result = new JArray();
            lock (_sync) foreach (JObject report in _roomCatalogHistory) result.Add(report.DeepClone());
            return result;
        }
    }
}

