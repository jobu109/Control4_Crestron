using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;

namespace Control4Crestron
{
    // Read-only setup report probe for the Home page location, weather and
    // sunrise/sunset. It discovers candidate items at runtime (project, site and
    // building items, plus anything named like weather) and records only field
    // names and allowlisted location/weather values. Nothing is written to Director.
    public sealed partial class GatewayCore
    {
        // Revision 2: variable rows carry the variable name in varName; name is the item name.
        // Revision 3: also reads the project root's /setup (Composer project settings such as
        // location, country, time zone and temperature scale).
        // Revision 4: /setup is 404 on the EA-5, so follow the sub-resources Director advertises in
        // the root and weather items' URIs field, plus a few properties-style paths, all read-only.
        // Revision 5: OpenWeather's /properties answers (51 rows) but revision 4 dropped every name
        // with punctuation; list all names and withhold only values of blocked names.
        private const int SiteProbeRevision = 5;

        private static readonly string[] SiteValueHints = {
            "city", "town", "state", "province", "region", "country", "zip", "postal", "latitude", "longitude",
            "lat", "lon", "lng", "timezone", "time_zone", "tz", "location", "locale", "unit", "scale", "temperature",
            "temp", "condition", "weather", "description", "summary", "humidity", "sunrise", "sunset", "dawn", "dusk",
            "forecast", "wind", "high", "low", "feels", "dew", "pressure", "uv", "visibility", "precip", "rain",
            "cloud", "icon", "updated", "observation", "name", "place", "units"
        };

        // Secrets, street-level location and network identity never leave the controller.
        private static readonly string[] SiteBlockedTerms = {
            "key", "secret", "token", "password", "credential", "auth", "account", "user", "email", "phone",
            "address", "street", "house_number", "mac", "serial", "ip", "url", "uri", "host", "cookie"
        };

        private JObject ProbeSiteAndWeather(JArray all, string host, string token, int gen)
        {
            var report = new JObject { ["probe_revision"] = SiteProbeRevision };
            var items = new JArray();
            report["items"] = items;
            var candidates = new List<KeyValuePair<ushort, JObject>>();
            foreach (JToken row in all)
            {
                ushort id; int type;
                if (!(row is JObject) || !UInt16.TryParse(ScalarText(row["item_id"]), NumberStyles.None, CultureInfo.InvariantCulture, out id) ||
                    id == 0 || !Int32.TryParse(ScalarText(row["type"]), NumberStyles.None, CultureInfo.InvariantCulture, out type)) continue;
                string name = ScalarText(row["name"]) ?? "";
                if ((type >= 1 && type <= 4) || name.IndexOf("weather", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    name.IndexOf("daylight", StringComparison.OrdinalIgnoreCase) >= 0)
                    candidates.Add(new KeyValuePair<ushort, JObject>(id, new JObject { ["item_id"] = id, ["type"] = type, ["name"] = SafeLabel(name) }));
                if (candidates.Count >= 10) break;
            }
            foreach (var candidate in candidates)
            {
                if (!CurrentGeneration(gen)) break;
                JObject entry = candidate.Value;
                items.Add(entry);
                ProbeSiteItem(host, token, gen, candidate.Key, entry, true);
                int candidateType = (int)entry["type"];
                if (candidateType == 1) ProbeSiteSetup(host, token, gen, candidate.Key, entry);
                if (candidateType == 1 || candidateType == 9) ProbeSiteResources(host, token, gen, candidate.Key, entry);
            }
            report["agents"] = ProbeWeatherAgents(host, token, gen);
            return report;
        }

        private void ProbeSiteItem(string host, string token, int gen, ushort id, JObject entry, bool followProtocol)
        {
            string path = "/api/v1/items/" + id.ToString(CultureInfo.InvariantCulture);
            try
            {
                JObject detail = ServiceDetail(ReadWithTimeoutRetry(host, token, path, gen), id);
                entry["fields"] = SiteFieldNames(detail);
                entry["values"] = SiteProbeValues(detail);
                JArray uris = SiteResourcePaths(detail["URIs"], id);
                if (uris.Count > 0) entry["uris"] = uris;
                ushort protocol;
                if (followProtocol && UInt16.TryParse(ScalarText(detail["protocolId"]), NumberStyles.None, CultureInfo.InvariantCulture, out protocol) &&
                    protocol != 0 && protocol != id && CurrentGeneration(gen))
                {
                    var driver = new JObject { ["item_id"] = protocol };
                    entry["protocol"] = driver;
                    ProbeSiteItem(host, token, gen, protocol, driver, false);
                }
            }
            catch (Exception ex)
            {
                InvalidateRejectedToken(ex);
                entry["detail_error"] = FailureKind(ex);
                lock (_sync) if (!_online) return;
            }
            try
            {
                string json = ReadWithTimeoutRetry(host, token, path + "/variables", gen);
                if (json.Length > 262144) throw new InvalidDataException("Oversized variable list");
                entry["variables"] = SiteVariableRows(JToken.Parse(json));
            }
            catch (Exception ex)
            {
                InvalidateRejectedToken(ex);
                entry["variables_error"] = FailureKind(ex);
            }
        }

        /// <summary>Read-only GET of an item's setup document; records its shape and allowlisted values.</summary>
        private void ProbeSiteSetup(string host, string token, int gen, ushort id, JObject entry)
        {
            var setup = new JObject();
            entry["setup"] = setup;
            try
            {
                string json = ReadWithTimeoutRetry(host, token, "/api/v1/items/" + id.ToString(CultureInfo.InvariantCulture) + "/setup", gen);
                if (String.IsNullOrEmpty(json) || json.Length > 262144) throw new InvalidDataException("Invalid setup response size");
                JToken parsed = JToken.Parse(json);
                setup["response_type"] = parsed.Type.ToString();
                JObject first = parsed as JObject ?? (parsed is JArray && ((JArray)parsed).Count > 0 ? ((JArray)parsed)[0] as JObject : null);
                if (parsed is JArray) setup["count"] = ((JArray)parsed).Count;
                if (first != null) { setup["fields"] = SiteFieldNames(first); setup["values"] = SiteProbeValues(first); }
            }
            catch (Exception ex)
            {
                InvalidateRejectedToken(ex);
                setup["error"] = FailureKind(ex);
            }
        }

        // Unadvertised paths worth one read-only try; a 404 is recorded and harmless.
        private static readonly string[] SiteExtraSuffixes = { "/properties", "/setup", "/info", "/settings", "/location" };

        /// <summary>
        /// Read-only GETs of the item's advertised sub-resources (URIs) and a few properties-style
        /// paths. Records each response's shape plus allowlisted name/value rows.
        /// </summary>
        private void ProbeSiteResources(string host, string token, int gen, ushort id, JObject entry)
        {
            string itemPath = "/api/v1/items/" + id.ToString(CultureInfo.InvariantCulture);
            var paths = new List<string>();
            JArray advertised = entry["uris"] as JArray;
            if (advertised != null)
                foreach (JToken uri in advertised)
                {
                    string path = ScalarText(uri["path"]);
                    // Variables were already read; commands are a known list, not settings.
                    if (path == null || path.EndsWith("/variables", StringComparison.OrdinalIgnoreCase) ||
                        path.EndsWith("/commands", StringComparison.OrdinalIgnoreCase)) continue;
                    if (!paths.Contains(path)) paths.Add(path);
                }
            foreach (string suffix in SiteExtraSuffixes)
            {
                // The root's /setup was already read by ProbeSiteSetup.
                if (suffix == "/setup" && entry["setup"] != null) continue;
                if (!paths.Contains(itemPath + suffix)) paths.Add(itemPath + suffix);
            }
            var results = new JArray();
            entry["resources"] = results;
            foreach (string path in paths)
            {
                if (results.Count >= 14 || !CurrentGeneration(gen)) break;
                var row = new JObject { ["path"] = path };
                results.Add(row);
                try
                {
                    string json = ReadWithTimeoutRetry(host, token, path, gen);
                    if (String.IsNullOrEmpty(json) || json.Length > 262144) throw new InvalidDataException("Invalid response size");
                    DescribeSiteResource(JToken.Parse(json), row);
                }
                catch (Exception ex)
                {
                    InvalidateRejectedToken(ex);
                    row["error"] = FailureKind(ex);
                    lock (_sync) if (!_online) return;
                }
            }
        }

        /// <summary>Advertised sub-resource paths, limited to this item's own /api/v1 paths.</summary>
        internal static JArray SiteResourcePaths(JToken uris, ushort id)
        {
            var result = new JArray();
            if (uris == null) return result;
            var pairs = new List<KeyValuePair<string, JToken>>();
            if (uris is JObject) foreach (JProperty p in ((JObject)uris).Properties()) pairs.Add(new KeyValuePair<string, JToken>(p.Name, p.Value));
            else if (uris is JArray) foreach (JToken t in (JArray)uris) pairs.Add(new KeyValuePair<string, JToken>(null, t));
            string prefix = "/api/v1/items/" + id.ToString(CultureInfo.InvariantCulture);
            foreach (var pair in pairs)
            {
                if (result.Count >= 24) break;
                string path = pair.Value != null && pair.Value.Type == JTokenType.String ? (string)pair.Value : null;
                if (path == null || !Regex.IsMatch(path, "^/api/v1/[A-Za-z0-9_/.-]{1,120}$") ||
                    !(path.Equals(prefix, StringComparison.Ordinal) || path.StartsWith(prefix + "/", StringComparison.Ordinal))) continue;
                var row = new JObject { ["path"] = path };
                if (pair.Key != null && SafeName(pair.Key)) row["name"] = pair.Key;
                result.Add(row);
            }
            return result;
        }

        /// <summary>Shape of a response plus allowlisted rows (name/value lists) or values (objects).</summary>
        internal static void DescribeSiteResource(JToken parsed, JObject row)
        {
            row["response_type"] = parsed.Type.ToString();
            if (parsed is JObject)
            {
                row["fields"] = SiteFieldNames((JObject)parsed);
                row["values"] = SiteProbeValues((JObject)parsed);
                return;
            }
            if (!(parsed is JArray)) return;
            var list = (JArray)parsed;
            row["count"] = list.Count;
            var entries = new JArray();
            foreach (JToken item in list)
            {
                if (entries.Count >= 64) break;
                var obj = item as JObject;
                if (obj == null) continue;
                string raw = ScalarText(obj["varName"]) ?? ScalarText(obj["propertyName"]) ?? ScalarText(obj["key"]) ?? ScalarText(obj["name"]);
                if (row["fields"] == null) row["fields"] = SiteFieldNames(obj);
                if (String.IsNullOrEmpty(raw)) continue;
                // Revision 5: every name is listed (punctuation replaced) so nothing is silently
                // dropped; values still need a location/weather hint and never a blocked term.
                string name = SiteEntryName(raw);
                var e = new JObject { ["name"] = name };
                JToken value = obj["value"];
                if (value != null)
                {
                    e["type"] = value.Type.ToString();
                    if (SiteBlocked(name)) e["withheld"] = true;
                    else if (SiteHinted(name))
                    {
                        JToken safe = SafeSiteValue(value);
                        if (safe != null) e["value"] = safe;
                    }
                }
                entries.Add(e);
            }
            row["entries"] = entries;
        }

        /// <summary>Entry name limited to printable ASCII (others become '?') and 64 characters.</summary>
        internal static string SiteEntryName(string raw)
        {
            string clean = Regex.Replace(raw, "[^\\x20-\\x7E]", "?").Trim();
            return clean.Length > 64 ? clean.Substring(0, 64) : clean;
        }

        private JObject ProbeWeatherAgents(string host, string token, int gen)
        {
            var result = new JObject();
            try
            {
                string json = ReadWithTimeoutRetry(host, token, "/api/v1/agents", gen);
                if (json.Length > 262144) throw new InvalidDataException("Oversized agent list");
                JToken list = JToken.Parse(json);
                result["response_type"] = list.Type.ToString();
                var names = new JArray();
                var matches = new List<string>();
                foreach (JToken agent in list is JArray ? (JArray)list : new JArray())
                {
                    string name = agent is JObject ? ScalarText(agent["name"]) ?? ScalarText(agent["agent"]) : ScalarText(agent);
                    if (name == null || !Regex.IsMatch(name, "^[A-Za-z0-9_]{1,64}$")) continue;
                    if (names.Count < 64) names.Add(name);
                    if (Regex.IsMatch(name, "weather|location|astro|sun|daylight", RegexOptions.IgnoreCase) && matches.Count < 4) matches.Add(name);
                }
                result["names"] = names;
                var details = new JArray();
                foreach (string name in matches)
                {
                    if (!CurrentGeneration(gen)) break;
                    var row = new JObject { ["agent"] = name };
                    details.Add(row);
                    try
                    {
                        string body = ReadWithTimeoutRetry(host, token, "/api/v1/agents/" + name, gen);
                        if (body.Length > 262144) throw new InvalidDataException("Oversized agent response");
                        JToken parsed = JToken.Parse(body);
                        row["response_type"] = parsed.Type.ToString();
                        JObject first = parsed as JObject ?? (parsed is JArray && ((JArray)parsed).Count > 0 ? ((JArray)parsed)[0] as JObject : null);
                        if (parsed is JArray) row["count"] = ((JArray)parsed).Count;
                        if (first != null) { row["fields"] = SiteFieldNames(first); row["values"] = SiteProbeValues(first); }
                    }
                    catch (Exception ex) { InvalidateRejectedToken(ex); row["error"] = FailureKind(ex); }
                }
                result["details"] = details;
            }
            catch (Exception ex)
            {
                InvalidateRejectedToken(ex);
                result["error"] = FailureKind(ex);
            }
            return result;
        }

        private static JArray SiteFieldNames(JObject detail)
        {
            var keys = new JArray();
            foreach (JProperty property in detail.Properties())
                if (keys.Count < 64 && SafeName(property.Name) && !SiteBlocked(property.Name)) keys.Add(property.Name);
            return keys;
        }

        /// <summary>Allowlisted scalar values, flattened to two levels with dotted names.</summary>
        internal static JObject SiteProbeValues(JObject detail)
        {
            var values = new JObject();
            CollectSiteValues(detail, "", 0, values);
            return values;
        }

        private static void CollectSiteValues(JObject node, string prefix, int depth, JObject values)
        {
            foreach (JProperty property in node.Properties())
            {
                if (values.Count >= 64) return;
                if (!SafeName(property.Name) || SiteBlocked(property.Name)) continue;
                string name = prefix + property.Name;
                if (property.Value is JObject)
                {
                    if (depth < 2) CollectSiteValues((JObject)property.Value, name + ".", depth + 1, values);
                    continue;
                }
                if (!SiteHinted(property.Name) && !SiteHinted(prefix)) continue;
                JToken safe = SafeSiteValue(property.Value);
                if (safe != null) values[name] = safe;
            }
        }

        internal static JArray SiteVariableRows(JToken list)
        {
            var rows = new JArray();
            if (!(list is JArray)) return rows;
            foreach (JToken entry in (JArray)list)
            {
                if (!(entry is JObject) || rows.Count >= 96) continue;
                // Director variable rows: varName is the variable; name is the owning item's name.
                string name = ScalarText(entry["varName"]) ?? ScalarText(entry["name"]);
                if (name == null || !Regex.IsMatch(name, "^[A-Za-z0-9_ .-]{1,64}$") || SiteBlocked(name)) continue;
                JToken value = entry["value"];
                var row = new JObject { ["name"] = name, ["type"] = value == null ? "missing" : value.Type.ToString() };
                if (value != null && SiteHinted(name))
                {
                    JToken safe = SafeSiteValue(value);
                    if (safe != null) row["value"] = safe;
                    else if (value.Type == JTokenType.String) row["length"] = ((string)value).Length;
                }
                rows.Add(row);
            }
            return rows;
        }

        private static JToken SafeSiteValue(JToken value)
        {
            switch (value.Type)
            {
                case JTokenType.Integer: case JTokenType.Float: case JTokenType.Boolean: return value.DeepClone();
                case JTokenType.String:
                    string text = (string)value;
                    if (text.Length > 120 || text.IndexOf("://", StringComparison.Ordinal) >= 0 || text.IndexOf('<') >= 0) return null;
                    return text;
                default: return null;
            }
        }

        private static bool SafeName(string name) { return Regex.IsMatch(name, "^[A-Za-z_][A-Za-z0-9_]{0,63}$"); }

        private static string SafeLabel(string name) { return name.Length > 80 ? name.Substring(0, 80) : name; }

        private static bool SiteHinted(string name)
        {
            string lower = name.ToLowerInvariant();
            foreach (string hint in SiteValueHints) if (lower.Contains(hint)) return true;
            return false;
        }

        private static bool SiteBlocked(string name)
        {
            string lower = name.ToLowerInvariant();
            foreach (string term in SiteBlockedTerms)
            {
                if (term == "ip" || term == "mac" || term == "uri")
                {
                    // Short terms only as whole words, so "zip", "precip" and "machine" are not blocked.
                    if (Regex.IsMatch(lower, "(^|[^a-z])" + term + "([^a-z]|$)")) return true;
                }
                else if (lower.Contains(term)) return true;
            }
            return false;
        }
    }
}
