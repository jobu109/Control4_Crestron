using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Control4Crestron
{
    public sealed partial class GatewayCore
    {
        private sealed class NowPlayingCapture
        {
            internal int Generation, Epoch, SourceId, Bytes;
            internal bool Video;
            internal AudioRoom Registration;
            internal DateTime Until;
            internal HashSet<int> Devices = new HashSet<int>();
            internal JObject Report;
        }
        private readonly Dictionary<ushort, NowPlayingCapture> _nowPlayingCaptures = new Dictionary<ushort, NowPlayingCapture>();
        private readonly List<JObject> _previousVideoCaptures = new List<JObject>();
        private static string NowPlayingLabel(JToken value)
        {
            if (value == null || value.Type != JTokenType.String) return null;
            string text = value.ToString();
            return !SensitiveMediaKey(text) && Regex.IsMatch(text, "^[A-Za-z_][A-Za-z0-9_ .:-]{0,95}$") ? text : null;
        }
        private static bool ArtworkField(string name)
        {
            return Regex.IsMatch(name, "^(img|image|image_url|artwork|artwork_url|coverart|cover_art|album_cover_url)$", RegexOptions.IgnoreCase);
        }
        private static JObject ArtworkAddress(string text)
        {
            var result = new JObject(new JProperty("present", text.Length > 0));
            Uri uri;
            if (!Uri.TryCreate(text, UriKind.Absolute, out uri) || (uri.Scheme != "https" && uri.Scheme != "http"))
            { result["format"] = text.StartsWith("/") && !text.StartsWith("//") ? "relative_path" : "unrecognized"; return result; }
            // Inspect addressing without exporting paths, query values or fragments.
            result["format"] = "absolute_http"; result["scheme"] = uri.Scheme;
            result["host"] = uri.Host; result["port"] = uri.Port;
            result["has_query"] = uri.Query.Length > 0; result["has_fragment"] = uri.Fragment.Length > 0;
            result["has_userinfo"] = uri.UserInfo.Length > 0;
            result["path_segments"] = uri.Segments.Length;
            string extension = Path.GetExtension(uri.AbsolutePath);
            if (Regex.IsMatch(extension, "^\\.(jpg|jpeg|png|webp|gif)$", RegexOptions.IgnoreCase)) result["extension"] = extension.ToLowerInvariant();
            return result;
        }
        private static bool MediaTextField(string name)
        {
            string key = name.Replace("_", "").ToLowerInvariant();
            if (key.StartsWith("current", StringComparison.Ordinal)) key = key.Substring(7);
            return key == "text" || key == "subtext" || key == "title" || key == "track" || key == "tracktitle" || key == "songtitle" || key == "artist" || key == "album" || key == "albumtitle" ||
                key == "trackname" || key == "songname" || key == "trackartist" || key == "albumartist" || key == "station" || key == "stationname" || key == "playbackstate" || key == "playstate";
        }
        private static JObject ObservedDeviceCommand(JObject command)
        {
            var result = new JObject(new JProperty("command", NowPlayingLabel(command["command"])));
            JArray parameters = command["params"] is JObject ? command["params"]["param"] as JArray : null;
            var output = new JArray(); result["parameters"] = output;
            if (parameters == null || parameters.Count > 32) return result;
            foreach (JToken token in parameters)
            {
                JObject param = token as JObject; if (param == null) continue;
                string name = NowPlayingLabel(param["name"]);
                if (name == null) continue;
                JObject value = param["value"] as JObject;
                var field = new JObject(new JProperty("name", name), new JProperty("type", value == null ? null : NowPlayingLabel(value["type"])));
                JToken raw = value == null ? null : value["static"];
                string text = raw != null && (raw.Type == JTokenType.String || raw.Type == JTokenType.Integer) ? raw.ToString() : "";
                if (Regex.IsMatch(name, "^(room|roomid|room_id|station|stationid|station_id|guideid|guide_id|preset|presetid|preset_id|id)$", RegexOptions.IgnoreCase) && Regex.IsMatch(text, "^(?:[A-Za-z][0-9]{1,16}|[0-9]{1,16})$")) field["public_id"] = text;
                else if (raw != null)
                {
                    field["value_type"] = raw.Type.ToString();
                    if (raw.Type == JTokenType.String)
                    {
                        field["value_length"] = text.Length;
                        if (text.Length <= 65536 && text.TrimStart().StartsWith("<"))
                        {
                            var tags = new JArray(); var seen = new HashSet<string>();
                            foreach (Match match in Regex.Matches(text, @"<([A-Za-z_][A-Za-z0-9_:.-]{0,63})(?:\s|>|/>)"))
                            { if (tags.Count >= 32) break; string tag = match.Groups[1].Value; if (!SensitiveMediaKey(tag) && seen.Add(tag)) tags.Add(tag); }
                            field["xml_tags"] = tags;
                        }
                    }
                }
                output.Add(field);
            }
            return result;
        }
        internal static JObject NowPlayingShape(JToken node, string context, int depth, ref int budget)
        {
            var shape = new JObject(new JProperty("type", node == null ? "missing" : node.Type.ToString()));
            if (node == null) return shape;
            if (--budget <= 0 || depth >= 10) { shape["truncated"] = true; return shape; }
            var obj = node as JObject;
            if (obj != null)
            {
                if (context == "devicecommand") shape["observed_command"] = ObservedDeviceCommand(obj);
                string variable = NowPlayingLabel(MediaMetadataField(obj, "varName", "variableName", "variable_name"));
                if (variable != null) shape["variable_name"] = variable;
                var fields = new JObject(); int omitted = 0;
                foreach (JProperty field in obj.Properties())
                {
                    if (fields.Count >= 40 || budget <= 0 || SensitiveMediaKey(field.Name) || !Regex.IsMatch(field.Name, "^[A-Za-z_][A-Za-z0-9_]{0,63}$")) { omitted++; continue; }
                    fields[field.Name] = NowPlayingShape(field.Value, field.Name.Equals("value", StringComparison.OrdinalIgnoreCase) && variable != null ? variable : field.Name, depth + 1, ref budget);
                }
                shape["fields"] = fields; shape["omitted"] = omitted;
            }
            else if (node is JArray)
            {
                var array = (JArray)node; var entries = new JArray();
                foreach (JToken item in array) { if (entries.Count >= 128 || budget <= 0) break; entries.Add(NowPlayingShape(item, context, depth + 1, ref budget)); }
                shape["entries"] = entries; shape["omitted"] = array.Count - entries.Count;
            }
            else if (node.Type == JTokenType.String)
            {
                string text = node.ToString();
                if (text.Length > 65536) { shape["truncated"] = true; return shape; }
                bool stateContext = context == "NowPlaying" || context == "Items" || context == "streamStatus";
                if (stateContext) text = System.Net.WebUtility.HtmlDecode(text);
                string trimmed = text.TrimStart();
                if (trimmed.StartsWith("{") || trimmed.StartsWith("["))
                {
                    try { shape["json_content"] = NowPlayingShape(JToken.Parse(text), "", depth + 1, ref budget); }
                    catch (JsonReaderException) { shape["content_format"] = "unrecognized_json"; }
                }
                else if (trimmed.StartsWith("<"))
                {
                    // Outline only until the controller's XML schema is established.
                    var tags = new JArray(); var seen = new HashSet<string>();
                    foreach (Match match in Regex.Matches(text, @"<([A-Za-z_][A-Za-z0-9_:.-]{0,63})(?:\s|>|/>)"))
                    { if (tags.Count >= 40) break; if (!SensitiveMediaKey(match.Groups[1].Value) && seen.Add(match.Groups[1].Value)) tags.Add(match.Groups[1].Value); }
                    shape["content_format"] = "xml_outline_only"; shape["xml_tags"] = tags;
                }
                else if (ArtworkField(context))
                { shape["artwork_reference_present"] = text.Length > 0; shape["address"] = ArtworkAddress(text); }
                else if ((context == "NAME" || context == "varName" || context == "variableName") && NowPlayingLabel(node) != null)
                { shape["identifier"] = NowPlayingLabel(node); }
                else if (Regex.IsMatch(context, "^(guide_id|preset_id|now_playing_id|show_id|genre_id|item)$", RegexOptions.IgnoreCase) && Regex.IsMatch(text, "^(?:[A-Za-z][0-9]{1,16}|[0-9]{1,16})$"))
                { shape["public_id"] = text; }
                else if (Regex.IsMatch(context, "^(default_action|itemType|element)$", RegexOptions.IgnoreCase) && NowPlayingLabel(node) != null)
                { shape["identifier"] = NowPlayingLabel(node); }
                else if (stateContext)
                {
                    var candidates = new JArray(); var seen = new HashSet<string>();
                    foreach (Match match in Regex.Matches(text, @"\b(play|playing|pause|paused|stop|stopped|buffering|idle|true|false)\b", RegexOptions.IgnoreCase))
                    { string candidate = match.Value.ToLowerInvariant(); if (seen.Add(candidate)) candidates.Add(candidate); }
                    shape["state_token_candidates"] = candidates;
                    shape["semantics"] = "unverified; may describe available commands rather than current state";
                    if (Regex.IsMatch(trimmed, "^(play|playing|pause|paused|stop|stopped|buffering|idle|true|false)$", RegexOptions.IgnoreCase)) shape["exact_state_candidate"] = trimmed.ToLowerInvariant();
                }
                else if (MediaTextField(context) && !Regex.IsMatch(text, @"https?://|\bBearer\s|\beyJ[A-Za-z0-9_-]+\.", RegexOptions.IgnoreCase))
                {
                    text = Regex.Replace(text, @"[\x00-\x1f]", " ").Trim();
                    shape["display_value"] = text.Length > 512 ? text.Substring(0, 512) : text;
                }
            }
            else if ((node.Type == JTokenType.Integer || node.Type == JTokenType.Float || node.Type == JTokenType.Boolean) &&
                Regex.IsMatch(context, "^(duration|duration_ms|position|elapsed|progress|length|offset|canSeek|playing|paused|play_state|playback_state|NowPlayingIndex|deviceid|notifyDevId|medSrcDev|queueid|streamid)$", RegexOptions.IgnoreCase))
            { shape["observed_value"] = node.DeepClone(); shape["units_or_semantics"] = "unverified"; }
            return shape;
        }
        private static JObject InspectNowPlayingVariables(string json)
        {
            if (string.IsNullOrEmpty(json) || json.Length > 262144) throw new InvalidDataException("Invalid metadata variable response");
            JToken root = JToken.Parse(json); if (!(root is JArray)) throw new InvalidDataException("Expected metadata variable list");
            int budget = 2048;
            return NowPlayingShape(root, "", 0, ref budget);
        }
        public void InspectNowPlaying(ushort roomId)
        {
            NowPlayingCapture capture;
            lock (_sync)
            {
                AudioRoom registration;
                if (!_online || !_roomModules.ContainsKey(roomId) || !_audioRooms.TryGetValue(roomId, out registration))
                { EmitMessage("Connect and initialize the room before metadata inspection"); return; }
                var state = registration.SourceState;
                bool video = state != null && state.VideoValid == 1 && state.VideoId > 0;
                int sourceId = state == null ? 0 : video ? state.VideoId : state.AudioValid == 1 ? state.AudioId : 0;
                if (sourceId <= 0 || sourceId > UInt16.MaxValue)
                { EmitMessage("Select a source and wait for measured source feedback before metadata inspection"); return; }
                if (_nowPlayingCaptures.ContainsKey(roomId) && DateTime.UtcNow < _nowPlayingCaptures[roomId].Until && NowPlayingCurrent(roomId, _nowPlayingCaptures[roomId]))
                { EmitMessage("Now-playing capture already active; change tracks then Export_Inventory"); return; }
                if (!_nowPlayingCaptures.ContainsKey(roomId) && _nowPlayingCaptures.Count >= 4)
                {
                    ushort oldest = 0; DateTime until = DateTime.MaxValue;
                    foreach (var existing in _nowPlayingCaptures) if (existing.Value.Until < until) { oldest = existing.Key; until = existing.Value.Until; }
                    RetainVideoCapture(_nowPlayingCaptures[oldest]);
                    _nowPlayingCaptures.Remove(oldest);
                }
                capture = new NowPlayingCapture { Generation = _generation, Epoch = _feedbackEpoch, Registration = registration, SourceId = sourceId, Video = video, Until = DateTime.UtcNow.AddSeconds(60) };
                capture.Devices.Add(roomId); capture.Devices.Add(capture.SourceId);
                capture.Report = new JObject(new JProperty("room_id", roomId), new JProperty("source_id", capture.SourceId), new JProperty("generated_utc", DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture)),
                    new JProperty("source_kind", video ? "video" : "audio"), new JProperty("capture_seconds", 60), new JProperty("variables", new JArray()), new JProperty("events", new JArray()), new JProperty("dropped_events", 0));
                NowPlayingCapture prior;
                if (_nowPlayingCaptures.TryGetValue(roomId, out prior)) RetainVideoCapture(prior);
                _nowPlayingCaptures[roomId] = capture;
            }
            Queue(delegate
            {
                try
                {
                    string host, token; if (!GetConnection(out host, out token)) return;
                    lock (_sync) { if (!NowPlayingCurrent(roomId, capture)) return; }
                    string detail = Request(host, token, "GET", "/api/v1/items/" + capture.SourceId, null);
                    JObject mapping; ushort driver = ServiceDriverCandidate(detail, (ushort)capture.SourceId, out mapping);
                    lock (_sync) { if (!NowPlayingCurrent(roomId, capture)) return; if (driver > 0) { capture.Devices.Add(driver); capture.Report["driver_id"] = driver; } }
                    var devices = new List<int> { roomId, capture.SourceId }; if (driver > 0 && !devices.Contains(driver)) devices.Add(driver);
                    for (int i = 0; i < devices.Count && i < 4; i++)
                    {
                        lock (_sync) { if (!NowPlayingCurrent(roomId, capture)) return; }
                        int device = devices[i]; JObject observation;
                        try
                        {
                            string json = Request(host, token, "GET", "/api/v1/items/" + device + "/variables", null);
                            observation = InspectNowPlayingVariables(json);
                            if (device == roomId && !capture.Video)
                                foreach (JToken variable in (JArray)JToken.Parse(json))
                                {
                                    var entry = variable as JObject; if (entry == null) continue;
                                    if (MediaMetadataName(MediaMetadataField(entry, "varName", "variableName")) != "CURRENT_AUDIO_DEVICE") continue;
                                    JToken raw = MediaMetadataField(entry, "value"); int renderer;
                                    if (raw != null && Int32.TryParse(raw.ToString(), NumberStyles.None, CultureInfo.InvariantCulture, out renderer) && renderer > 0 && !devices.Contains(renderer) && devices.Count < 4) devices.Add(renderer);
                                }
                        }
                        catch (Exception ex) { InvalidateRejectedToken(ex); observation = new JObject(new JProperty("error_type", FailureKind(ex))); }
                        observation["item_id"] = device;
                        lock (_sync)
                        {
                            if (!NowPlayingCurrent(roomId, capture)) return;
                            capture.Devices.Add(device); ((JArray)capture.Report["variables"]).Add(observation);
                        }
                    }
                    EmitMessage("Now-playing " + (capture.Video ? "video" : "audio") + " variables recorded; change content, then Export_Inventory after 60s");
                }
                catch (Exception ex) { lock (_sync) { if (!NowPlayingCurrent(roomId, capture)) return; } InvalidateRejectedToken(ex); EmitMessage("Now-playing inspection failed: " + FailureKind(ex)); }
            });
        }
        private bool NowPlayingCurrent(ushort roomId, NowPlayingCapture capture)
        {
            NowPlayingCapture current;
            return InspectionCurrent(roomId, capture.Registration, capture.Generation, capture.Epoch) &&
                _nowPlayingCaptures.TryGetValue(roomId, out current) && Object.ReferenceEquals(current, capture) &&
                InspectionSourceCurrent(capture);
        }
        private static bool InspectionSourceCurrent(NowPlayingCapture capture)
        {
            var state = capture.Registration.SourceState;
            if (state == null) return !capture.Video;
            if (capture.Video) return state.VideoValid == 1 && state.VideoId == capture.SourceId;
            return (state.VideoValid == 0 || state.VideoId == 0) &&
                (state.AudioValid == 0 || state.AudioId == capture.SourceId);
        }

        private void CaptureNowPlayingEvent(JToken message, int generation)
        {
            if (!(message is JObject)) return;
            int device; JToken raw = message["iddevice"];
            if (raw == null || !Int32.TryParse(raw.ToString(), out device)) return;
            lock (_sync)
                foreach (var entry in _nowPlayingCaptures)
                {
                    NowPlayingCapture capture = entry.Value;
                    if (generation != capture.Generation || DateTime.UtcNow > capture.Until || !capture.Devices.Contains(device) || !NowPlayingCurrent(entry.Key, capture)) continue;
                    if (!capture.Video && capture.Registration.SourceState?.AudioValid != 1) continue;
                    var events = (JArray)capture.Report["events"];
                    JObject evt = message["data"] is JObject ? message["data"]["EVENT"] as JObject : null;
                    if (evt != null && (string)evt["NAME"] == "ProgressChanged")
                    {
                        int samples = 0;
                        foreach (JObject prior in events)
                            if ((int)prior["item_id"] == device && (string)prior.SelectToken("data.fields.EVENT.fields.NAME.identifier") == "ProgressChanged") samples++;
                        if (samples >= 2)
                        { capture.Report["coalesced_progress_events"] = (int?)capture.Report["coalesced_progress_events"] + 1 ?? 1; continue; }
                    }
                    if (events.Count >= 32 || capture.Bytes >= 131072) { capture.Report["dropped_events"] = (int)capture.Report["dropped_events"] + 1; continue; }
                    int budget = 256;
                    var observation = new JObject(new JProperty("item_id", device), new JProperty("received_utc", DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture)),
                        new JProperty("event_type", MediaMetadataName(message["evtName"]) ?? "unrecognized"),
                        new JProperty("data", NowPlayingShape(message["data"], "", 0, ref budget)));
                    int size = observation.ToString(Formatting.None).Length;
                    if (capture.Bytes + size > 131072) { capture.Report["dropped_events"] = (int)capture.Report["dropped_events"] + 1; continue; }
                    events.Add(observation); capture.Bytes += size;
                }
        }
        // Keep prior video sources available for a single diagnostic export.
        private void RetainVideoCapture(NowPlayingCapture capture)
        {
            if (!capture.Video || capture.Generation != _generation) return;
            _previousVideoCaptures.Add(capture.Report);
            if (_previousVideoCaptures.Count > 8) _previousVideoCaptures.RemoveAt(0);
        }

        private JArray NowPlayingReports()
        {
            var reports = new JArray();
            lock (_sync)
            {
                foreach (JObject report in _previousVideoCaptures) reports.Add(report.DeepClone());
                foreach (NowPlayingCapture capture in _nowPlayingCaptures.Values) reports.Add(capture.Report.DeepClone());
            }
            return reports;
        }
    }
}
