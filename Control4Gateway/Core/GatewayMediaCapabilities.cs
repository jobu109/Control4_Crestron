using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Control4Crestron
{
    public sealed partial class GatewayCore
    {
        public event EventHandler<GatewayMessageEventArgs> MediaCapabilitiesExported;
        public event EventHandler<GatewayMessageEventArgs> MediaCommandsExported;
        private int _mediaCapabilitiesPending;
        private static readonly int[] MediaComparisonSources = { 20, 65, 599, 100002 };

        // Each explicit export compares the four researched sources using GET only.
        public void ExportMediaCapabilities()
        {
            ExportMediaReport(false);
        }

        public void ExportMediaCommands()
        {
            ExportMediaReport(true);
        }

        private void ExportMediaReport(bool commands)
        {
            int gen;
            lock (_sync) { gen = _generation; }
            Queue(delegate
            {
                if (System.Threading.Interlocked.CompareExchange(ref _mediaCapabilitiesPending, 1, 0) != 0)
                { EmitMessage("Media export already running"); return; }
                try
                {
                    string host, token;
                    if (!GetConnection(out host, out token)) { EmitMessage("Connect before exporting media capabilities"); return; }
                    if (!CurrentGeneration(gen)) return;
                    var sources = new JArray();
                    foreach (int sourceId in MediaComparisonSources)
                    {
                        if (!CurrentGeneration(gen)) return;
                        EmitMessage("Reading " + MediaSourceLabel(sourceId) + " " + sourceId + (commands ? " commands (GET only)" : " capabilities (GET only)"));
                        int status = 0;
                        try
                        {
                            string requestPath = "/api/v1/items/" + sourceId.ToString(CultureInfo.InvariantCulture) + (commands ? "/commands" : "");
                            string json = Request(host, token, "GET", requestPath, null, out status);
                            JObject itemReport = commands ? BuildMediaCommandsForSource(json, status, sourceId) : BuildMediaCapabilitiesForSource(json, host, status, sourceId);
                            itemReport["source_label"] = MediaSourceLabel(sourceId);
                            sources.Add(itemReport);
                        }
                        catch (Exception ex)
                        {
                            if (!CurrentGeneration(gen)) return;
                            var web = ex as WebException;
                            var response = web == null ? null : web.Response as HttpWebResponse;
                            if (response != null) status = (int)response.StatusCode;
                            InvalidateRejectedToken(ex);
                            if (status == 401 || status == 403) throw;
                            sources.Add(new JObject(new JProperty("source_id", sourceId), new JProperty("source_label", MediaSourceLabel(sourceId)),
                                new JProperty("http_status", status == 0 ? (object)null : status), new JProperty("error_type", FailureKind(ex)),
                                new JProperty("browse_endpoint_status", "not_established")));
                        }
                    }
                    JObject report = BuildMediaComparisonReport(sources, commands);
                    report["generated_utc"] = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture);
                    string path = "/user/Control4Gateway_" + _instance + (commands ? "_media_commands.json" : "_media_capabilities.json");
                    string temporary = path + ".tmp";
                    lock (_sync)
                    {
                        if (_generation != gen) return;
                        using (var writer = new Crestron.SimplSharp.CrestronIO.StreamWriter(temporary, false, new UTF8Encoding(false)))
                            writer.Write(report.ToString(Formatting.Indented));
                        if (Crestron.SimplSharp.CrestronIO.File.Exists(path)) Crestron.SimplSharp.CrestronIO.File.Delete(path);
                        Crestron.SimplSharp.CrestronIO.File.Move(temporary, path);
                    }
                    var handler = commands ? MediaCommandsExported : MediaCapabilitiesExported;
                    if (handler != null) SafeEvent(delegate { handler(this, new GatewayMessageEventArgs { Message = path }); });
                    EmitMessage((commands ? "Media commands saved: " : "Media capabilities saved: ") + path +
                        ((int)report["error_count"] == 0 ? "" : "; some sources failed, see JSON") + "; browsing not yet established");
                }
                catch (Exception ex)
                {
                    if (CurrentGeneration(gen)) { InvalidateRejectedToken(ex); EmitMessage((commands ? "Media command export failed: " : "Media capability export failed: ") + FailureKind(ex)); }
                }
                finally { System.Threading.Interlocked.Exchange(ref _mediaCapabilitiesPending, 0); }
            });
        }

        private static JObject BuildMediaCapabilitiesReport(string json, string host, int status)
        {
            return BuildMediaCapabilitiesForSource(json, host, status, 20);
        }

        private static JObject BuildMediaCapabilitiesForSource(string json, string host, int status, int sourceId)
        {
            MediaSourceLabel(sourceId); // Restrict inspection to the researched source set.
            if (status != 200 || string.IsNullOrEmpty(json) || json.Length > 262144)
                throw new InvalidDataException("Invalid media detail response");
            var items = JToken.Parse(json) as JArray;
            if (items == null || items.Count != 1 || items[0].Type != JTokenType.Object)
                throw new InvalidDataException("Expected one media item");
            JToken id = items[0]["id"];
            if (id != null && id.ToString() != sourceId.ToString(CultureInfo.InvariantCulture)) throw new InvalidDataException("Unexpected media item ID");
            JToken uris = items[0]["URIs"];
            var gets = new SortedSet<string>(StringComparer.Ordinal);
            var unknown = new SortedSet<string>(StringComparer.Ordinal);
            int budget = 256;
            InspectMediaLinks(uris, host, null, gets, unknown, 0, ref budget);
            return new JObject(
                new JProperty("source_id", sourceId), new JProperty("http_status", status), new JProperty("item_count", items.Count),
                new JProperty("uris_present", uris != null && uris.Type != JTokenType.Null),
                new JProperty("uri_structure", MediaUriShape(uris, 0)),
                new JProperty("advertised_local_get_paths", StringArray(gets)),
                new JProperty("advertised_local_paths_method_unknown", StringArray(unknown)),
                new JProperty("inspection_truncated", budget <= 0),
                new JProperty("path_filter", "Only same-controller HTTPS /api/v1 paths with recognized structural segments or numeric IDs; queries/fragments omitted. Unrecognized or sensitive data is suppressed."),
                new JProperty("browse_endpoint_status", "not_established"));
        }

        private static string MediaSourceLabel(int sourceId)
        {
            switch (sourceId)
            {
                case 20: return "TuneIn";
                case 65: return "Spotify";
                case 599: return "Amazon Music";
                case 100002: return "Digital Media";
                default: throw new ArgumentOutOfRangeException("sourceId");
            }
        }

        private static JObject BuildMediaComparisonReport(JArray sources, bool commands)
        {
            if (sources == null || sources.Count != MediaComparisonSources.Length)
                throw new InvalidDataException("Incomplete media comparison");
            int success = 0;
            var seen = new HashSet<int>();
            foreach (JToken source in sources)
            {
                var item = source as JObject;
                if (item == null || item["source_id"] == null || item["source_id"].Type != JTokenType.Integer)
                    throw new InvalidDataException("Invalid media source report");
                int sourceId = (int)item["source_id"];
                MediaSourceLabel(sourceId);
                if (!seen.Add(sourceId)) throw new InvalidDataException("Duplicate media source report");
                if (item["error_type"] == null && (int?)item["http_status"] == 200) success++;
            }
            return new JObject(new JProperty("schema_version", 2), new JProperty("request_method", "GET"),
                new JProperty("report_kind", commands ? "command_metadata_comparison" : "item_capability_comparison"),
                new JProperty("source_count", sources.Count), new JProperty("successful_source_count", success),
                new JProperty("error_count", sources.Count - success), new JProperty("sources", sources),
                new JProperty("browse_endpoint_status", "not_established"));
        }

        private static JArray StringArray(IEnumerable<string> values)
        {
            var array = new JArray();
            foreach (string value in values) array.Add(value);
            return array;
        }

        private static bool SensitiveMediaKey(string key)
        {
            string lower = key.ToLowerInvariant();
            foreach (string term in new[] { "token", "password", "credential", "cookie", "auth", "account", "profile", "user", "history" })
                if (lower.Contains(term)) return true;
            return false;
        }

        private static bool KnownMediaUriKey(string key)
        {
            switch (key.ToLowerInvariant())
            {
                case "get": case "post": case "put": case "delete": case "patch": case "head": case "options":
                case "method": case "href": case "url": case "uri": case "path": case "links":
                case "self": case "browse": case "search": case "catalog": case "items": case "children":
                case "commands": case "variables": case "metadata": case "list": case "media":
                case "playlists": case "albums": case "movies": case "stations": return true;
                default: return false;
            }
        }

        private static JObject MediaUriShape(JToken value, int depth)
        {
            var result = new JObject(new JProperty("type", value == null ? "missing" : value.Type.ToString()));
            if (value == null) return result;
            var container = value as JContainer;
            if (container == null) return result; // Never export scalar content.
            result["count"] = container.Count;
            if (depth >= 3) { result["children_omitted"] = true; return result; }
            var children = new JArray();
            var obj = value as JObject;
            int ignored = 0, emitted = 0;
            if (obj != null)
            {
                foreach (JProperty property in obj.Properties())
                {
                    if (!KnownMediaUriKey(property.Name) || SensitiveMediaKey(property.Name) || emitted >= 16) { ignored++; continue; }
                    children.Add(new JObject(new JProperty("key", property.Name.ToLowerInvariant()), new JProperty("shape", MediaUriShape(property.Value, depth + 1))));
                    emitted++;
                }
                result["unreported_key_count"] = ignored;
            }
            else
            {
                foreach (JToken child in container.Children())
                {
                    if (emitted++ >= 4) break;
                    children.Add(MediaUriShape(child, depth + 1));
                }
            }
            result["children"] = children;
            return result;
        }

        private static void InspectMediaLinks(JToken value, string host, string inheritedMethod,
            ISet<string> gets, ISet<string> unknown, int depth, ref int budget)
        {
            if (value == null) return;
            if (depth > 6) { budget = 0; return; }
            if (--budget < 0) return;
            var obj = value as JObject;
            if (obj != null)
            {
                string method = inheritedMethod;
                JToken explicitMethod = obj["method"] ?? obj["Method"];
                if (explicitMethod != null) method = explicitMethod.Type == JTokenType.String ? explicitMethod.ToString().ToUpperInvariant() : "INVALID";
                int count = 0;
                foreach (JProperty property in obj.Properties())
                {
                    if (count++ >= 64) { budget = 0; break; }
                    if (SensitiveMediaKey(property.Name)) continue;
                    string key = property.Name.ToLowerInvariant();
                    string childMethod = method;
                    if (key == "get" || key == "post" || key == "put" || key == "delete" || key == "patch" || key == "head" || key == "options")
                        childMethod = key.ToUpperInvariant();
                    if (property.Value.Type == JTokenType.String && (KnownMediaUriKey(key) && key != "method"))
                    {
                        string path = MediaLocalPath((string)property.Value, host);
                        if (path != null)
                        {
                            if (childMethod == "GET") gets.Add(path);
                            else if (childMethod == null) unknown.Add(path);
                        }
                    }
                    else if (property.Value is JContainer)
                        InspectMediaLinks(property.Value, host, childMethod, gets, unknown, depth + 1, ref budget);
                }
            }
            else if (value is JArray)
            {
                foreach (JToken child in (JArray)value)
                {
                    if (budget <= 0) break;
                    // Bare strings have no established link schema and are not interpreted.
                    InspectMediaLinks(child, host, inheritedMethod, gets, unknown, depth + 1, ref budget);
                }
            }
        }

        private static string MediaLocalPath(string raw, string host)
        {
            if (string.IsNullOrEmpty(raw) || raw.Length > 2048 || raw.IndexOfAny(new[] { '\r', '\n', '\0', '\\' }) >= 0) return null;
            string path;
            if (raw.StartsWith("/api/v1/", StringComparison.Ordinal)) path = raw.Split('?', '#')[0];
            else
            {
                Uri uri;
                if (!Uri.TryCreate(raw, UriKind.Absolute, out uri) || uri.Scheme != "https" || uri.Port != 443 ||
                    uri.UserInfo.Length != 0 || !uri.Host.Equals(host, StringComparison.OrdinalIgnoreCase)) return null;
                path = uri.AbsolutePath;
            }
            if (!path.StartsWith("/api/v1/", StringComparison.Ordinal) || path.Length > 512) return null;
            string[] segments = path.Substring(8).TrimEnd('/').Split('/');
            foreach (string segment in segments)
            {
                uint number;
                if (segment.Length > 0 && segment.Length <= 10 && UInt32.TryParse(segment, NumberStyles.None, CultureInfo.InvariantCulture, out number)) continue;
                switch (segment.ToLowerInvariant())
                {
                    case "items": case "commands": case "variables": case "browse": case "search": case "media":
                    case "list": case "lists": case "catalog": case "services": case "albums": case "artists":
                    case "tracks": case "playlists": case "stations": case "favorites": case "queue": case "capabilities":
                    case "menu": case "menus": case "metadata": case "root": case "children": case "folders": case "content":
                    case "locations": case "rooms": case "movies": case "broadcast_audio": case "broadcast_video": break;
                    default: return null;
                }
            }
            return path;
        }
    }
}
