using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net;
using Newtonsoft.Json.Linq;

namespace Control4Crestron
{
    public sealed partial class GatewayCore
    {
        private readonly HashSet<ushort> _roomMediaInspectionPending = new HashSet<ushort>();
        private readonly Queue<JObject> _roomMediaInspections = new Queue<JObject>();
        internal static JObject InspectServicePresetCatalog(string json, string reference)
        {
            if (!IsServiceSelector(reference)) throw new InvalidDataException("Invalid reference selector");
            if (string.IsNullOrEmpty(json) || json.Length > 1048576) throw new InvalidDataException("Invalid catalog size");
            JToken root = JToken.Parse(json);
            var items = root as JArray;
            if (items == null && root is JObject) items = MediaMetadataField((JObject)root, "items", "movies", "playlists", "albums", "stations", "results", "media", "data") as JArray;
            if (items == null) throw new InvalidDataException("Unknown catalog schema");
            var matches = new JArray(); var samples = new JArray();
            int visited = 0;
            foreach (JToken item in items)
            {
                if (++visited > 2048) break;
                var paths = new JArray(); int budget = 256;
                JObject shape = ServicePresetRecordShape(item, reference, "$", paths, 0, ref budget);
                if (paths.Count > 0) matches.Add(new JObject(new JProperty("record_index", visited), new JProperty("selector_paths", paths), new JProperty("record_shape", shape)));
                else if (samples.Count < 3) samples.Add(shape);
                if (matches.Count >= 16) break;
            }
            return new JObject(new JProperty("source_count", items.Count), new JProperty("visited", Math.Min(visited, 2048)),
                new JProperty("scan_incomplete", visited < items.Count || visited > 2048), new JProperty("exact_selector_matches", matches),
                new JProperty("sample_shapes", samples), new JProperty("note", "Exact copied-selector matches only. Selector text and unrelated string values suppressed; driver ownership is not inferred."));
        }
        private static JObject ServicePresetRecordShape(JToken node, string reference, string path, JArray matches, int depth, ref int budget)
        {
            var shape = new JObject(new JProperty("type", node.Type.ToString()));
            if (--budget <= 0 || depth >= 5) { shape["truncated"] = true; return shape; }
            var obj = node as JObject;
            if (obj != null)
            {
                var fields = new JObject(); int omitted = 0;
                foreach (JProperty field in obj.Properties())
                {
                    if (fields.Count >= 48 || budget <= 0 || SensitiveMediaKey(field.Name) ||
                        !System.Text.RegularExpressions.Regex.IsMatch(field.Name, "^[A-Za-z_][A-Za-z0-9_]{0,63}$")) { omitted++; continue; }
                    JObject child = ServicePresetRecordShape(field.Value, reference, path + "." + field.Name, matches, depth + 1, ref budget);
                    string lower = field.Name.ToLowerInvariant(); ulong id;
                    if ((lower == "id" || lower.EndsWith("_id") || lower.EndsWith("id") || lower == "device" || lower == "source" || lower == "context") &&
                        (field.Value.Type == JTokenType.Integer || field.Value.Type == JTokenType.String) &&
                        UInt64.TryParse(field.Value.ToString(), NumberStyles.None, CultureInfo.InvariantCulture, out id)) child["numeric_id"] = id.ToString(CultureInfo.InvariantCulture);
                    fields[field.Name] = child;
                }
                shape["fields"] = fields; shape["omitted"] = omitted;
            }
            else if (node is JArray)
            {
                var entries = new JArray(); var array = (JArray)node;
                foreach (JToken item in array) { if (entries.Count >= 16 || budget <= 0) break; entries.Add(ServicePresetRecordShape(item, reference, path + "[" + entries.Count + "]", matches, depth + 1, ref budget)); }
                shape["entries"] = entries; shape["omitted"] = array.Count - entries.Count;
            }
            else if (node.Type == JTokenType.String && String.Equals(node.ToString(), reference, StringComparison.OrdinalIgnoreCase) && matches.Count < 16)
            { matches.Add(path); shape["matches_reference_selector"] = true; }
            return shape;
        }
        public void InspectServicePresets(ushort roomId, ushort driverId, string reference)
        {
            if (driverId == 0 || !IsServiceSelector(reference))
            { EmitMessage("Set service driver ID and known selector before inspection"); return; }
            int gen, epoch; AudioRoom registration;
            lock (_sync)
            {
                if (!_online || !_roomModules.ContainsKey(roomId) || !_audioRooms.TryGetValue(roomId, out registration))
                { EmitMessage("Connect and initialize Sources before service inspection"); return; }
                if (!_roomMediaInspectionPending.Add(roomId)) { EmitMessage("Room media inspection already running"); return; }
                gen = _generation; epoch = _feedbackEpoch;
            }
            Queue(delegate
            {
                try
                {
                    string host, token;
                    lock (_sync) { if (!InspectionCurrent(roomId, registration, gen, epoch)) return; }
                    if (!GetConnection(out host, out token)) return;
                    string metadata = Request(host, token, "GET", "/api/v1/items/" + roomId + "/commands", null);
                    JObject plan = BuildRoomMediaPlan(metadata, host, roomId);
                    string path = RoomCatalogPath(roomId, "audio_stations"); bool allowed = false;
                    foreach (JToken link in (JArray)plan["advertised_get_paths"]) if ((string)link == path) allowed = true;
                    if (!allowed) throw new InvalidDataException("Broadcast catalog GET not advertised");
                    lock (_sync) { if (!InspectionCurrent(roomId, registration, gen, epoch)) return; }
                    string json = Request(host, token, "GET", path, null);
                    JObject report = InspectServicePresetCatalog(json, reference);
                    report["inspection_kind"] = "service_preset_catalog_mapping";
                    report["room_id"] = roomId; report["requested_driver_id"] = driverId;
                    report["generated_utc"] = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture);
                    lock (_sync)
                    {
                        if (!InspectionCurrent(roomId, registration, gen, epoch)) return;
                        _roomMediaInspections.Enqueue(report); while (_roomMediaInspections.Count > 4) _roomMediaInspections.Dequeue();
                    }
                    EmitMessage("Service preset inspection recorded; matching records " + ((JArray)report["exact_selector_matches"]).Count + "; Export_Inventory");
                }
                catch (Exception ex)
                {
                    lock (_sync) { if (!InspectionCurrent(roomId, registration, gen, epoch)) return; }
                    InvalidateRejectedToken(ex); EmitMessage("Service preset inspection failed: " + FailureKind(ex));
                }
                finally { lock (_sync) { _roomMediaInspectionPending.Remove(roomId); } }
            });
        }
        public void InspectRoomMediaCatalog(ushort roomId)
        {
            int gen, epoch; AudioRoom registration;
            lock (_sync)
            {
                if (!_online || !_roomModules.ContainsKey(roomId) || !_audioRooms.TryGetValue(roomId, out registration))
                { EmitMessage("Connect and initialize Sources before inspecting media catalogs"); return; }
                if (!_roomMediaInspectionPending.Add(roomId)) { EmitMessage("Room media inspection already running"); return; }
                gen = _generation; epoch = _feedbackEpoch;
            }
            Queue(delegate
            {
                try
                {
                    string host, token;
                    if (!GetConnection(out host, out token)) return;
                    var errors = new JArray(); var responses = new JArray();
                    JObject plan = null;
                    lock (_sync) { if (!InspectionCurrent(roomId, registration, gen, epoch)) return; }
                    try
                    {
                        string json = Request(host, token, "GET", "/api/v1/items/" + roomId + "/commands", null);
                        plan = BuildRoomMediaPlan(json, host, roomId);
                    }
                    catch (Exception ex)
                    {
                        InvalidateRejectedToken(ex);
                        lock (_sync) { if (!InspectionCurrent(roomId, registration, gen, epoch)) throw; }
                        errors.Add(new JObject(new JProperty("section", "media_command_parameters"), new JProperty("error_type", FailureKind(ex))));
                    }
                    string prefix = "/api/v1/locations/rooms/" + roomId + "/media/";
                    var paths = new List<string> { prefix + "movies" }; // Official OS3.4 release-note endpoint.
                    if (plan != null)
                        foreach (JToken candidate in (JArray)plan["advertised_get_paths"])
                        {
                            string path = (string)candidate;
                            if (paths.Count < 7 && !paths.Contains(path)) paths.Add(path);
                        }
                    foreach (string path in paths)
                    {
                        lock (_sync) { if (!InspectionCurrent(roomId, registration, gen, epoch)) return; }
                        int status = 0;
                        try
                        {
                            EmitMessage("Room " + roomId + " media inspection GET " + path);
                            string json = Request(host, token, "GET", path, null, out status);
                            JObject response = BuildCatalogResponseInspection(json);
                            response["request_path"] = path; response["http_status"] = status;
                            responses.Add(response);
                        }
                        catch (Exception ex)
                        {
                            var web = ex as WebException;
                            var response = web == null ? null : web.Response as HttpWebResponse;
                            if (response != null) status = (int)response.StatusCode;
                            InvalidateRejectedToken(ex);
                            lock (_sync) { if (!InspectionCurrent(roomId, registration, gen, epoch)) throw; }
                            responses.Add(new JObject(new JProperty("request_path", path), new JProperty("http_status", status == 0 ? (object)null : status),
                                new JProperty("error_type", FailureKind(ex))));
                        }
                    }
                    var report = new JObject(new JProperty("room_id", roomId),
                        new JProperty("generated_utc", DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture)),
                        new JProperty("command_parameter_inspection", plan), new JProperty("catalog_responses", responses), new JProperty("errors", errors),
                        new JProperty("browse_endpoint_status", "inspection_only_not_established"),
                        new JProperty("note", "GET only. No source/content selected. Media selection commands and catalog IDs are separate; no streaming-service library access is claimed."));
                    lock (_sync)
                    {
                        if (!InspectionCurrent(roomId, registration, gen, epoch)) return;
                        _roomMediaInspections.Enqueue(report);
                        while (_roomMediaInspections.Count > 4) _roomMediaInspections.Dequeue();
                    }
                    EmitMessage("Room " + roomId + " media inspection recorded; Export_Inventory includes room_media_inspections");
                }
                catch (Exception ex)
                {
                    lock (_sync) { if (!InspectionCurrent(roomId, registration, gen, epoch)) return; }
                    InvalidateRejectedToken(ex); EmitMessage("Room media inspection failed: " + FailureKind(ex));
                }
                finally { lock (_sync) { _roomMediaInspectionPending.Remove(roomId); } }
            });
        }
        private static bool RoomMediaPath(string path, ushort roomId)
        {
            string prefix = "/api/v1/locations/rooms/" + roomId + "/media";
            return path == prefix || path.StartsWith(prefix + "/", StringComparison.Ordinal);
        }
        private static JObject BuildRoomMediaPlan(string json, string host, ushort roomId)
        {
            if (String.IsNullOrEmpty(json) || json.Length > 524288) throw new InvalidDataException("Invalid media command metadata size");
            JToken root = JToken.Parse(json);
            JArray list = root as JArray;
            if (list == null && root is JObject) list = MediaMetadataField((JObject)root, "commands") as JArray;
            if (list == null) throw new InvalidDataException("Unknown media command metadata schema");
            var definitions = new JArray();
            var gets = new SortedSet<string>(StringComparer.Ordinal);
            var unknown = new SortedSet<string>(StringComparer.Ordinal);
            int visited = 0; bool truncated = list.Count > 256;
            foreach (JToken item in list)
            {
                if (++visited > 256) break;
                var command = item as JObject;
                if (command == null) continue;
                string name = MediaMetadataName(MediaMetadataField(command, "command", "commandName", "command_name", "name"));
                switch (name)
                {
                    case "SELECT_AUDIO_MEDIA:PLAYLIST": case "SELECT_AUDIO_MEDIA:ALBUM": case "SELECT_AUDIO_MEDIA:BROADCAST_AUDIO":
                    case "SELECT_AUDIO_MEDIA:MOVIE": case "SELECT_VIDEO_MEDIA:MOVIE": case "SELECT_VIDEO_MEDIA:BROADCAST_VIDEO": break;
                    default: continue;
                }
                JToken parameters = MediaMetadataField(command, "parameters", "params", "tParams");
                JToken media = null;
                var map = parameters as JObject;
                if (map != null) media = MediaMetadataField(map, "mediaid");
                var array = parameters as JArray;
                if (array != null)
                {
                    int count = 0;
                    foreach (JToken parameter in array)
                    {
                        if (++count > 32) { truncated = true; break; }
                        var definition = parameter as JObject;
                        if (definition == null) continue;
                        JToken field = MediaMetadataField(definition, "name", "parameter", "parameterName");
                        if (field != null && field.Type == JTokenType.String && String.Equals((string)field, "mediaid", StringComparison.OrdinalIgnoreCase))
                        { media = definition; break; }
                    }
                }
                if (media == null) continue;
                if (definitions.Count >= 16) { truncated = true; break; }
                int shapeBudget = 256, linkBudget = 256;
                JToken shape = CatalogInspectionShape(media, 0, ref shapeBudget);
                InspectMediaLinks(media, host, null, gets, unknown, 0, ref linkBudget);
                int contractBudget = 512;
                definitions.Add(new JObject(new JProperty("command", name), new JProperty("parameter", "mediaid"), new JProperty("shape", shape),
                    new JProperty("command_contract", MediaCommandContract(command, host, roomId, "", 0, ref contractBudget))));
                if (shapeBudget <= 0 || linkBudget <= 0) truncated = true;
            }
            var roomGets = new JArray(); var roomUnknown = new JArray();
            foreach (string path in gets) if (RoomMediaPath(path, roomId)) roomGets.Add(path);
            foreach (string path in unknown) if (RoomMediaPath(path, roomId)) roomUnknown.Add(path);
            return new JObject(new JProperty("definitions", definitions), new JProperty("advertised_get_paths", roomGets),
                new JProperty("advertised_paths_method_unknown", roomUnknown), new JProperty("inspection_truncated", truncated),
                new JProperty("follow_policy", "Only explicit GET links in this room's /media subtree are eligible. Unknown-method links are not followed; queries/fragments omitted."));
        }
        private static JObject MediaCommandContract(JToken node, string host, ushort roomId, string key, int depth, ref int budget)
        {
            var result = new JObject(new JProperty("type", node == null ? "missing" : node.Type.ToString()));
            if (node == null || --budget <= 0 || depth >= 7) { result["truncated"] = true; return result; }
            var obj = node as JObject;
            if (obj != null)
            {
                var fields = new JObject(); int omitted = 0;
                foreach (JProperty field in obj.Properties())
                {
                    string lower = field.Name.ToLowerInvariant();
                    if (budget <= 0 || fields.Count >= 32 || SensitiveMediaKey(lower) ||
                        lower == "description" || lower == "label" || lower == "title" ||
                        !System.Text.RegularExpressions.Regex.IsMatch(field.Name, "^[A-Za-z][A-Za-z0-9_.:-]{0,63}$")) { omitted++; continue; }
                    fields[field.Name] = MediaCommandContract(field.Value, host, roomId, lower, depth + 1, ref budget);
                }
                result["fields"] = fields; result["omitted"] = omitted;
            }
            else if (node is JArray)
            {
                var entries = new JArray(); var array = (JArray)node;
                foreach (JToken child in array) { if (budget <= 0 || entries.Count >= 32) break; entries.Add(MediaCommandContract(child, host, roomId, key, depth + 1, ref budget)); }
                result["entries"] = entries; result["omitted"] = array.Count - entries.Count;
            }
            else if (node.Type == JTokenType.Boolean || node.Type == JTokenType.Integer)
                result["schema_value"] = node.DeepClone();
            else if (node.Type == JTokenType.String)
            {
                string value = node.ToString();
                if ((key == "type" || key == "valuetype" || key == "valuefield" || key == "valuedisplay" || key == "method") && System.Text.RegularExpressions.Regex.IsMatch(value, "^[A-Za-z][A-Za-z0-9_.:-]{0,95}$") ||
                    key == "name" && (value == "mediaid" || value == "deselect") ||
                    key == "command" && value.StartsWith("SELECT_", StringComparison.Ordinal) && value.Length < 96 ||
                    (key == "default" || key == "value") && (value == "true" || value == "false" || value == "0" || value == "1"))
                    result["schema_value"] = value;
                else if (KnownMediaUriKey(key))
                {
                    string path = MediaLocalPath(value, host);
                    if (path != null && RoomMediaPath(path, roomId)) result["local_path"] = path;
                }
            }
            return result;
        }
        private static bool CatalogShapeKey(string key)
        {
            if (KnownMediaUriKey(key)) return true;
            switch (key.ToLowerInvariant())
            {
                case "name": case "type": case "required": case "id": case "mediaid": case "value": case "values":
                case "options": case "choices": case "data": case "results": case "total": case "count": case "start": case "limit":
                case "playlist": case "album": case "movie": case "station": case "item": case "entries": case "entry": return true;
                default: return false;
            }
        }
        private static JObject CatalogInspectionShape(JToken value, int depth, ref int budget)
        {
            var shape = new JObject(new JProperty("type", value == null ? "missing" : value.Type.ToString()));
            if (value == null || --budget < 0) return shape;
            var container = value as JContainer;
            if (container == null) return shape;
            shape["count"] = container.Count;
            if (depth >= 4) { shape["children_omitted"] = true; return shape; }
            var children = new JArray();
            var obj = value as JObject;
            int skipped = 0;
            if (obj != null) foreach (JProperty property in obj.Properties())
            {
                if (budget <= 0 || children.Count >= 24 || SensitiveMediaKey(property.Name) || !CatalogShapeKey(property.Name)) { skipped++; continue; }
                children.Add(new JObject(new JProperty("key", property.Name), new JProperty("shape", CatalogInspectionShape(property.Value, depth + 1, ref budget))));
            }
            else foreach (JToken child in container.Children())
            {
                if (children.Count >= 2 || budget <= 0) break;
                children.Add(CatalogInspectionShape(child, depth + 1, ref budget));
            }
            shape["children"] = children; shape["unreported_key_count"] = skipped;
            return shape;
        }
        private static JObject BuildCatalogResponseInspection(string json)
        {
            if (String.IsNullOrEmpty(json) || json.Length > 1048576) throw new InvalidDataException("Invalid catalog response size");
            JToken root = JToken.Parse(json);
            JArray items = root as JArray;
            if (items == null && root is JObject)
                items = MediaMetadataField((JObject)root, "items", "movies", "playlists", "albums", "stations", "results", "media", "data") as JArray;
            int budget = 256;
            var sample = new JArray();
            if (items != null) foreach (JToken item in items)
            {
                if (sample.Count >= 8) break;
                var entry = item as JObject;
                JToken id = entry == null ? null : MediaMetadataField(entry, "mediaid", "id");
                ulong number;
                if (id != null && (id.Type == JTokenType.Integer || id.Type == JTokenType.String) &&
                    UInt64.TryParse(id.ToString(), NumberStyles.None, CultureInfo.InvariantCulture, out number) && number > 0)
                    sample.Add(number.ToString(CultureInfo.InvariantCulture));
            }
            return new JObject(new JProperty("response_type", root.Type.ToString()),
                new JProperty("list_recognized", items != null), new JProperty("item_count", items == null ? (object)null : items.Count),
                new JProperty("sample_numeric_ids", sample), new JProperty("shape", CatalogInspectionShape(root, 0, ref budget)),
                new JProperty("inspection_truncated", budget <= 0),
                new JProperty("content_note", "Names, artwork, arbitrary strings and payloads suppressed; numeric IDs are observations, not verified playable service content."));
        }
        private JArray RoomMediaInspectionReports()
        {
            var reports = new JArray();
            lock (_sync) foreach (JObject report in _roomMediaInspections) reports.Add(report.DeepClone());
            return reports;
        }
    }
}
