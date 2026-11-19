using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Control4Crestron
{
    public sealed partial class GatewayCore
    {
        private sealed class NativeCatalogSelection
        {
            internal AudioRoom Registration;
            internal int Generation, Epoch, SelectionVersion;
            internal JObject Catalog;
            internal bool Enabled;
            internal readonly object SendSync = new object();
        }
        private readonly Dictionary<ushort, NativeCatalogSelection> _nativeCatalogSelections = new Dictionary<ushort, NativeCatalogSelection>();
        private readonly HashSet<ushort> _servicePresetPending = new HashSet<ushort>();
        private readonly Dictionary<ushort, bool> _pandoraShuffle = new Dictionary<ushort, bool>();
        public void SetPandoraShuffle(ushort roomId, bool enabled)
        {
            lock (_sync)
            {
                _pandoraShuffle[roomId] = enabled;
                ServicePresetList list;
                if (_servicePresetLists.TryGetValue(roomId, out list) && list.Public["pandora_shuffle"] != null)
                { list.Public["pandora_shuffle"] = enabled ? "On" : "Off"; PublishRoomCatalog(roomId, "service_presets", list.Public); }
            }
        }
        private static bool IsPandoraItem(string item)
        { return item != null && Regex.IsMatch(item, "^ST:0:[1-9][0-9]{0,19}$"); }
        private static bool IsServiceSelector(string selector)
        { return IsPandoraItem(selector) || !string.IsNullOrEmpty(selector) && selector.Length <= 2048 && selector.Length % 2 == 0 && Regex.IsMatch(selector, "^[0-9A-Fa-f]+$"); }
        internal static string BuildPandoraItemSelection(string metadata, ushort roomId, ushort driverId, string item, bool shuffle)
        {
            if (roomId == 0 || driverId == 0 || !IsPandoraItem(item)) throw new InvalidDataException("Invalid Pandora item");
            JObject report = BuildCommandMetadataReport(metadata, 200, driverId);
            if (!(bool)report["command_list_recognized"] || (bool)report["inspection_truncated"] || (int)report["unreported_command_count"] != 0)
                throw new InvalidDataException("Unrecognized Pandora command metadata");
            // This precise command/parameter contract comes from the user-copied
            // Composer action. The observed Pandora endpoint lists no commands.
            if ((int)report["command_count"] != 0)
                throw new InvalidDataException("Pandora metadata differs from captured empty list");
            return new JObject(new JProperty("async", true), new JProperty("command", "Play Item"),
                new JProperty("tParams", new JObject(new JProperty("Room", roomId.ToString(CultureInfo.InvariantCulture)),
                    new JProperty("Item", item), new JProperty("Shuffle", shuffle ? "On" : "Off")))).ToString(Formatting.None);
        }
        internal static string BuildServicePresetSelection(string metadata, ushort roomId, ushort driverId, string preset)
        {
            if (roomId == 0 || driverId == 0 || string.IsNullOrEmpty(preset) || preset.Length > 2048 || preset.Length % 2 != 0 ||
                !Regex.IsMatch(preset, "^[0-9A-Fa-f]+$")) throw new InvalidDataException("Invalid service preset selection");
            JObject report = BuildCommandMetadataReport(metadata, 200, driverId);
            if ((bool)report["inspection_truncated"]) throw new InvalidDataException("Incomplete service command metadata");
            JObject command = null;
            foreach (JToken entry in (JArray)report["commands"])
                if ((string)entry["command_name"] == "SelectPreset")
                { if (command != null) throw new InvalidDataException("Ambiguous service command"); command = (JObject)entry; }
            if (command == null || (int)command["unreported_parameter_count"] != 0 || (bool)command["parameters_truncated"])
                throw new InvalidDataException("Service command unavailable");
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (JToken parameter in (JArray)command["parameter_definitions"])
            {
                string name = (string)parameter["name"];
                if ((name != "Preset" && name != "Room") || !names.Add(name)) throw new InvalidDataException("Unexpected service parameters");
            }
            if (names.Count != 2) throw new InvalidDataException("Missing service parameters");
            return new JObject(new JProperty("async", true), new JProperty("command", "SelectPreset"),
                new JProperty("tParams", new JObject(new JProperty("Room", roomId.ToString(CultureInfo.InvariantCulture)),
                    new JProperty("Preset", preset)))).ToString(Formatting.None);
        }
        public void SelectServicePreset(ushort roomId, ushort driverId, string preset)
        { SelectServicePresetCore(roomId, driverId, preset, null); }
        private void SelectServicePresetCore(ushort roomId, ushort driverId, string preset, object expectedList)
        {
            if (roomId == 0 || driverId == 0 || !IsServiceSelector(preset))
            { EmitMessage("Service selection rejected: driver ID and supported copied selector required"); return; }
            int gen, epoch; AudioRoom registration; bool shuffle;
            lock (_sync)
            {
                if (!_online || !ServiceListCurrent(roomId, expectedList) || !_roomModules.ContainsKey(roomId) || !_audioRooms.TryGetValue(roomId, out registration))
                { EmitMessage("Service preset rejected: connect and initialize Sources"); return; }
                if (!_servicePresetPending.Add(roomId)) { EmitMessage("Service preset already pending for this room"); return; }
                _pandoraShuffle.TryGetValue(roomId, out shuffle);
                gen = _generation; epoch = _feedbackEpoch;
            }
            Queue(delegate
            {
                string phase = "metadata GET";
                try
                {
                    string host, token;
                    lock (_sync) { if (!InspectionCurrent(roomId, registration, gen, epoch) || !ServiceListCurrent(roomId, expectedList)) return; }
                    if (!GetConnection(out host, out token)) return;
                    string path = "/api/v1/items/" + driverId.ToString(CultureInfo.InvariantCulture) + "/commands";
                    string metadata = Request(host, token, "GET", path, null);
                    bool pandora = IsPandoraItem(preset);
                    string body = pandora ? BuildPandoraItemSelection(metadata, roomId, driverId, preset, shuffle) : BuildServicePresetSelection(metadata, roomId, driverId, preset);
                    lock (_sync) { if (!InspectionCurrent(roomId, registration, gen, epoch) || !ServiceListCurrent(roomId, expectedList)) return; }
                    phase = pandora ? "Play Item POST" : "SelectPreset POST";
                    Request(host, token, "POST", path, body);
                    lock (_sync) { if (!InspectionCurrent(roomId, registration, gen, epoch)) return; }
                    InvalidateRoomSource(roomId, new AudioQueryWork { Generation = gen, Epoch = epoch, Configuration = registration });
                    QueryAudio(roomId);
                    EmitMessage("Room " + roomId + " service preset accepted by driver " + driverId + " (not measured playback)");
                }
                catch (Exception ex)
                {
                    lock (_sync) { if (!InspectionCurrent(roomId, registration, gen, epoch)) return; }
                    InvalidateRejectedToken(ex);
                    EmitMessage("Service preset " + phase + " failed: " + NativeMediaFailureMessage(ex));
                }
                finally { lock (_sync) { _servicePresetPending.Remove(roomId); } }
            });
        }
        private static string NativeMediaErrorSummary(string body)
        {
            if (string.IsNullOrEmpty(body) || body.Length > 4096) return "server detail unavailable";
            try
            {
                JToken root = JToken.Parse(body);
                var details = new List<string>();
                int budget = 64;
                NativeMediaErrorFields(root, details, 0, ref budget);
                string text = details.Count == 0 ? "server JSON has no error fields; root=" + root.Type : string.Join("; ", details.ToArray());
                return text.Length > 420 ? text.Substring(0, 420) : text;
            }
            catch (JsonReaderException) { return "server detail is not JSON"; }
        }
        private static void NativeMediaErrorFields(JToken node, List<string> details, int depth, ref int budget)
        {
            if (node == null || depth > 4 || --budget < 0 || details.Count >= 8) return;
            var obj = node as JObject;
            if (obj != null)
                foreach (JProperty field in obj.Properties())
                {
                    string key = field.Name.ToLowerInvariant();
                    if (!Regex.IsMatch(key, "^(error|errors|error_code|errorcode|code|status|message|error_description|description|detail|reason|type)$")) continue;
                    if (field.Value is JContainer) NativeMediaErrorFields(field.Value, details, depth + 1, ref budget);
                    else if (field.Value.Type == JTokenType.String || field.Value.Type == JTokenType.Integer || field.Value.Type == JTokenType.Boolean)
                    {
                        string value = field.Value.ToString();
                        value = Regex.Replace(value, @"https?://[^\s""<>]+", "[url]", RegexOptions.IgnoreCase);
                        value = Regex.Replace(value, @"\bBearer\s+\S+|\beyJ[A-Za-z0-9_-]+\.[A-Za-z0-9_-]+\.[A-Za-z0-9_-]+", "[credential]", RegexOptions.IgnoreCase);
                        value = Regex.Replace(value, @"\b(token|password|secret|authorization)\b\s*[:=]\s*[^\s,;]+", "$1=[redacted]", RegexOptions.IgnoreCase);
                        value = Regex.Replace(value, @"[\r\n\t\x00-\x1f]", " ");
                        if (value.Length > 256) value = value.Substring(0, 256);
                        details.Add(key + "=" + value);
                    }
                    if (details.Count >= 8 || budget <= 0) break;
                }
            var array = node as JArray;
            if (array != null)
                foreach (JToken child in array)
                {
                    NativeMediaErrorFields(child, details, depth + 1, ref budget);
                    if (details.Count >= 8 || budget <= 0) break;
                }
        }
        private static string NativeMediaFailureDetail(Exception exception)
        {
            var web = exception as WebException;
            var response = web == null ? null : web.Response as HttpWebResponse;
            if (response == null || response.StatusCode != HttpStatusCode.BadRequest) return "";
            try
            {
                using (response)
                using (var stream = response.GetResponseStream())
                using (var reader = new StreamReader(stream))
                {
                    var buffer = new char[4097];
                    int total = 0, read;
                    while (total < buffer.Length && (read = reader.Read(buffer, total, buffer.Length - total)) > 0) total += read;
                    return "; " + NativeMediaErrorSummary(new string(buffer, 0, total));
                }
            }
            catch { return "; server detail unavailable"; }
        }
        private static string NativeMediaFailureMessage(Exception exception)
        {
            var web = exception as WebException;
            var response = web == null ? null : web.Response as HttpWebResponse;
            // FailureKind closes HTTP responses. Read the body first for HTTP400,
            // and capture the status before disposing its response/stream.
            if (response != null && response.StatusCode == HttpStatusCode.BadRequest)
                return "HTTP 400" + NativeMediaFailureDetail(exception);
            return FailureKind(exception);
        }
        private static string NativeMediaCommand(string metadata, ushort roomId, string kind)
        {
            string expected;
            switch (kind)
            {
                case "playlists": expected = "SELECT_AUDIO_MEDIA:PLAYLIST"; break;
                case "albums": expected = "SELECT_AUDIO_MEDIA:ALBUM"; break;
                case "audio_stations": expected = "SELECT_AUDIO_MEDIA:BROADCAST_AUDIO"; break;
                case "movies": expected = "SELECT_VIDEO_MEDIA:MOVIE"; break;
                case "video_stations": expected = "SELECT_VIDEO_MEDIA:BROADCAST_VIDEO"; break;
                default: throw new InvalidDataException("Unknown native media selection kind");
            }
            JObject report = BuildCommandMetadataReport(metadata, 200, roomId);
            if ((bool)report["inspection_truncated"]) throw new InvalidDataException("Incomplete media command metadata");
            JObject selected = null;
            foreach (JToken entry in (JArray)report["commands"])
                if ((string)entry["command_name"] == expected)
                { if (selected != null) throw new InvalidDataException("Ambiguous media command"); selected = (JObject)entry; }
            if (selected == null || (int)selected["unreported_parameter_count"] != 0 || (bool)selected["parameters_truncated"])
                throw new InvalidDataException("Media selection contract not established");
            string container = (string)selected["parameter_container_type"];
            if (container != "Array" && container != "Object") throw new InvalidDataException("Missing media parameters");
            bool mediaId = false;
            foreach (JToken parameter in (JArray)selected["parameter_definitions"])
            {
                string name = (string)parameter["name"];
                if (name == "mediaid")
                {
                    if (mediaId || (string)parameter["type"] != "list") throw new InvalidDataException("Unexpected mediaid contract");
                    mediaId = true;
                }
                else if (name != "deselect")
                    throw new InvalidDataException("Additional media argument contract required");
            }
            if (!mediaId) throw new InvalidDataException("No mediaid argument");
            return expected;
        }
        internal static string BuildNativeMediaSelection(string metadata, ushort roomId, string kind, string mediaId)
        {
            ulong number;
            if (!UInt64.TryParse(mediaId, NumberStyles.None, CultureInfo.InvariantCulture, out number) || number == 0)
                throw new InvalidDataException("Invalid native media ID");
            string command = NativeMediaCommand(metadata, roomId, kind);
            JToken root = JToken.Parse(metadata);
            JArray commands = root as JArray;
            if (commands == null && root is JObject) commands = MediaMetadataField((JObject)root, "commands") as JArray;
            JObject parameters = null;
            foreach (JToken entry in commands)
            {
                var definition = entry as JObject;
                if (definition == null || MediaMetadataName(MediaMetadataField(definition, "command", "commandName", "command_name", "name")) != command) continue;
                var list = MediaMetadataField(definition, "params", "parameters", "tParams") as JArray;
                if (list == null) throw new InvalidDataException("Typed parameter array required");
                parameters = new JObject();
                foreach (JToken item in list)
                {
                    var parameter = item as JObject;
                    if (parameter == null) throw new InvalidDataException("Typed parameter definition required");
                    string name = MediaMetadataName(MediaMetadataField(parameter, "name", "parameter", "parameterName"));
                    if (name != "mediaid" && name != "deselect" || parameters[name] != null) throw new InvalidDataException("Unexpected media argument");
                    JToken type = MediaMetadataField(parameter, "valueType");
                    JToken value = name == "mediaid" ? new JValue(mediaId) : MediaMetadataField(parameter, "value");
                    parameters[name] = NativeMediaTypedValue(type, value);
                }
            }
            if (parameters == null || parameters["mediaid"] == null) throw new InvalidDataException("Missing typed media argument");
            return new JObject(new JProperty("async", true), new JProperty("command", command),
                new JProperty("tParams", parameters)).ToString(Formatting.None);
        }
        private static JToken NativeMediaTypedValue(JToken type, JToken value)
        {
            if (type == null || type.Type != JTokenType.String || value == null || value is JContainer || value.Type == JTokenType.Null)
                throw new InvalidDataException("Missing declared media value/type");
            string text = value.ToString();
            switch (type.ToString().ToLowerInvariant())
            {
                case "string": case "str": return new JValue(text);
                case "integer": case "int": case "uint": case "number":
                    long signed;
                    if (Int64.TryParse(text, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out signed)) return new JValue(signed);
                    ulong unsigned;
                    if (UInt64.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out unsigned)) return new JValue(unsigned);
                    break;
                case "bool": case "boolean":
                    bool boolean;
                    if (Boolean.TryParse(text, out boolean)) return new JValue(boolean);
                    if (text == "0" || text == "1") return new JValue(text == "1");
                    break;
            }
            throw new InvalidDataException("Unsupported declared media value/type");
        }
        private static bool CatalogContainsMedia(JObject catalog, string kind, string revision, string mediaId)
        {
            if (catalog == null || (string)catalog["kind"] != kind || (string)catalog["revision"] != revision) return false;
            foreach (JToken item in (JArray)catalog["items"]) if ((string)item["id"] == mediaId) return true;
            return false;
        }
        private bool NativeSelectionCurrent(ushort id, NativeCatalogSelection selection, int version)
        {
            NativeCatalogSelection current;
            return selection.Enabled && selection.SelectionVersion == version &&
                _nativeCatalogSelections.TryGetValue(id, out current) && Object.ReferenceEquals(current, selection) &&
                InspectionCurrent(id, selection.Registration, selection.Generation, selection.Epoch);
        }
        public void SelectNativeRoomMedia(ushort id, string kind, string revision, string mediaId)
        {
            if (kind == "service_presets") { SelectListedServicePreset(id, revision, mediaId); return; }
            ulong number; NativeCatalogSelection selection; int version;
            if (!UInt64.TryParse(mediaId, NumberStyles.None, CultureInfo.InvariantCulture, out number) || number == 0)
            { EmitMessage("Invalid native media ID"); return; }
            mediaId = number.ToString(CultureInfo.InvariantCulture);
            lock (_sync)
            {
                if (!_nativeCatalogSelections.TryGetValue(id, out selection) ||
                    !NativeSelectionCurrent(id, selection, selection.SelectionVersion) ||
                    !CatalogContainsMedia(selection.Catalog, kind, revision, mediaId))
                { EmitMessage("Native media selection rejected: offline, unavailable or stale list/item"); return; }
                version = ++selection.SelectionVersion;
            }
            Queue(delegate
            {
                lock (selection.SendSync)
                {
                    string phase = "metadata GET";
                    try
                    {
                        string host, token;
                        lock (_sync) { if (!NativeSelectionCurrent(id, selection, version)) return; }
                        if (!GetConnection(out host, out token)) return;
                        string metadata = Request(host, token, "GET", "/api/v1/items/" + id + "/commands", null);
                        string body = BuildNativeMediaSelection(metadata, id, kind, mediaId);
                        lock (_sync)
                        {
                            if (!NativeSelectionCurrent(id, selection, version) ||
                                !CatalogContainsMedia(selection.Catalog, kind, revision, mediaId)) return;
                        }
                        phase = "selection POST";
                        Request(host, token, "POST", "/api/v1/items/" + id + "/commands", body);
                        lock (_sync) { if (!InspectionCurrent(id, selection.Registration, selection.Generation, selection.Epoch)) return; }
                        var work = new AudioQueryWork { Generation = selection.Generation, Epoch = selection.Epoch, Configuration = selection.Registration };
                        InvalidateRoomSource(id, work);
                        QueryAudio(id);
                        EmitMessage("Room " + id + " native " + kind + " selection accepted (not measured playback)");
                    }
                    catch (Exception ex)
                    {
                        lock (_sync) { if (!NativeSelectionCurrent(id, selection, version)) return; }
                        InvalidateRejectedToken(ex);
                        EmitMessage("Native media " + phase + " failed: " + NativeMediaFailureMessage(ex));
                    }
                }
            });
        }
    }
}
