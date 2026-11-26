using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Control4Crestron
{
    public sealed partial class GatewayCore
    {
        private static void WriteSetupFile(string path, string content)
        {
            string temporary = path + ".tmp";
            using (var writer = new Crestron.SimplSharp.CrestronIO.StreamWriter(temporary, false, new UTF8Encoding(false)))
                writer.Write(content);
            if (Crestron.SimplSharp.CrestronIO.File.Exists(path)) Crestron.SimplSharp.CrestronIO.File.Delete(path);
            Crestron.SimplSharp.CrestronIO.File.Move(temporary, path);
        }
        private static int SetupId(JToken value)
        {
            int id;
            return value != null && (value.Type == JTokenType.Integer || value.Type == JTokenType.String) &&
                Int32.TryParse(value.ToString(), NumberStyles.None, CultureInfo.InvariantCulture, out id) && id > 0 ? id : 0;
        }
        private static string SetupName(JToken value, string fallback)
        {
            if (value == null || value.Type != JTokenType.String) return fallback;
            string name = (string)value;
            var result = new StringBuilder();
            foreach (char c in name) { if (result.Length >= 512) break; result.Append(Char.IsControl(c) ? ' ' : c); }
            return result.Length == 0 ? fallback : result.ToString();
        }
        private static IEnumerable<JToken> SetupList(JToken token)
        {
            if (token is JArray) return (JArray)token;
            return token is JObject ? new[] { token } : new JToken[0];
        }
        internal static JObject BuildSetupReport(JObject inventory, string uiJson, JArray mappings, ushort gatewayId)
        {
            var names = new Dictionary<int, string>();
            var devices = new JArray();
            var warnings = new JArray();
            var rooms = new SortedDictionary<int, JObject>();
            var text = new StringBuilder();
            JArray all = inventory["all_items"] as JArray ?? new JArray();
            if (all.Count > 10000) throw new InvalidDataException("Setup inventory limit");
            foreach (JToken item in all)
            {
                int id = SetupId(item["item_id"]);
                if (id == 0) continue;
                string name = SetupName(item["name"], "Unnamed item");
                names[id] = name;
                var device = new JObject(new JProperty("item_id", id), new JProperty("name", name));
                foreach (string key in new[] { "type", "room_id", "parent_id" })
                {
                    int number = SetupId(item[key]);
                    if (number != 0) device[key] = number;
                }
                devices.Add(device);
            }
            Func<int, string> label = id => names.ContainsKey(id) ? names[id] : "Item " + id;
            Func<int, JObject> roomFor = id =>
            {
                JObject room;
                if (!rooms.TryGetValue(id, out room))
                {
                    room = new JObject(new JProperty("room_id", id), new JProperty("name", label(id)),
                        new JProperty("room_module_parameters", new JObject(new JProperty("Gateway_ID", gatewayId), new JProperty("Room_ID", id))),
                        new JProperty("watch_sources", new JArray()), new JProperty("listen_sources", new JArray()),
                        new JProperty("endpoint_status", "unresolved"));
                    rooms[id] = room;
                }
                return room;
            };
            string sourceStatus = "unavailable";
            int omitted = 0, sourceCount = 0;
            var sourceIconFields = new JObject();
            if (!String.IsNullOrEmpty(uiJson))
            {
                try
                {
                    if (uiJson.Length > 8388608) throw new InvalidDataException("Setup UI size");
                    JObject ui = JToken.Parse(uiJson) as JObject;
                    JToken experiences = ui == null ? null : ui["experiences"];
                    if (experiences is JObject && experiences["experience"] != null) experiences = experiences["experience"];
                    if (!(experiences is JObject) && !(experiences is JArray)) throw new InvalidDataException("Setup UI format");
                    sourceStatus = "discovered";
                    int experienceCount = 0;
                    foreach (JToken exp in SetupList(experiences))
                    {
                        if (++experienceCount > 2048) { omitted++; break; }
                        if (!(exp is JObject)) { omitted++; continue; }
                        string kind = SetupName(exp["type"], "").ToLowerInvariant();
                        if (kind != "watch" && kind != "listen") continue;
                        int roomId = SetupId(exp["room_id"]);
                        if (roomId == 0 || (!rooms.ContainsKey(roomId) && rooms.Count >= 256)) { omitted++; continue; }
                        JObject room = roomFor(roomId);
                        JToken sources = exp["sources"];
                        if (sources is JObject && sources["source"] != null) sources = sources["source"];
                        if (sources != null && !(sources is JArray) && !(sources is JObject)) { omitted++; continue; }
                        foreach (JToken source in SetupList(sources))
                        {
                            if (++sourceCount > 8192) { omitted++; break; }
                            if (!(source is JObject)) { omitted++; continue; }
                            foreach (JProperty field in ((JObject)source).Properties())
                                if (sourceIconFields.Count < 32 &&
                                    System.Text.RegularExpressions.Regex.IsMatch(field.Name, "^(icon|image|logo|artwork)[A-Za-z0-9_]{0,31}$",
                                        System.Text.RegularExpressions.RegexOptions.IgnoreCase))
                                    sourceIconFields[field.Name] = field.Value.Type.ToString();
                            int id = SetupId(source["id"]);
                            if (id == 0) { omitted++; continue; }
                            var destination = (JArray)room[kind + "_sources"];
                            bool duplicate = false;
                            foreach (JToken existing in destination) if ((int)existing["source_id"] == id) { duplicate = true; break; }
                            if (!duplicate) destination.Add(new JObject(new JProperty("source_id", id),
                                new JProperty("name", SetupName(source["name"], label(id))),
                                new JProperty("Source_ID$", id.ToString(CultureInfo.InvariantCulture)),
                                new JProperty("selection_mode", kind),
                                new JProperty("status", "advertised_by_controller_not_runtime_tested")));
                        }
                    }
                }
                catch (JsonReaderException) { sourceStatus = "unrecognized_format"; }
                catch (InvalidDataException) { sourceStatus = "unrecognized_format"; }
            }
            if (sourceStatus != "discovered") warnings.Add("Room source discovery unavailable or unrecognized; no source availability is inferred from inventory.");
            if (omitted != 0) warnings.Add("Some source entries were omitted or exceeded limits.");
            if (inventory["errors"] is JArray && ((JArray)inventory["errors"]).Count != 0) warnings.Add("Some inventory categories or source requests failed.");
            foreach (JToken mapping in mappings)
            {
                int id = SetupId(mapping["room_id"]);
                if (id == 0) continue;
                JObject room = roomFor(id);
                int watch = SetupId(mapping["watch_id"]), listen = SetupId(mapping["listen_id"]), output = SetupId(mapping["listen_output_id"]);
                room["endpoint_status"] = "configured_in_program_not_automatically_verified";
                room["watch_audio_endpoint"] = new JObject(new JProperty("name", watch == 0 ? "Not configured" : label(watch)), new JProperty("item_id", watch));
                room["listen_audio_endpoint"] = new JObject(new JProperty("name", listen == 0 ? "Not configured" : label(listen)),
                    new JProperty("item_id", listen), new JProperty("audio_output_id", output));
                room["audio_video_parameters"] = new JObject(new JProperty("Gateway_ID", gatewayId), new JProperty("Control4_Room_ID", id),
                    new JProperty("Watch_Feedback_ID", watch), new JProperty("Listen_Feedback_ID", listen), new JProperty("Listen_Output_ID", output));
            }
            text.AppendLine("CONTROL4 / CRESTRON SETUP REPORT");
            string generated = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture);
            text.AppendLine("Generated UTC: " + generated + " | Gateway_ID=" + gatewayId);
            text.AppendLine("PARTIAL: source lists are discovered; endpoint/output mappings below are configured values, not automatic verification.");
            text.AppendLine("Selection activates a source; it does not select content or guarantee playback.");
            text.AppendLine("Type 6 = protocol driver; type 7 = proxy. Numeric IDs below are parameter values.");
            foreach (JObject room in rooms.Values)
            {
                text.AppendLine();
                text.AppendLine("ROOM: " + (string)room["name"] + " | Control4_Room_ID=" + (int)room["room_id"]);
                text.AppendLine("Room module parameters: Gateway_ID=" + gatewayId + " Room_ID=" + (int)room["room_id"] + " (endpoint IDs not required)");
                text.AppendLine("Endpoint mapping: " + (string)room["endpoint_status"]);
                JObject parameters = room["audio_video_parameters"] as JObject;
                if (parameters != null)
                {
                    text.AppendLine("Watch audio: " + (string)room["watch_audio_endpoint"]["name"]);
                    text.AppendLine("Listen audio: " + (string)room["listen_audio_endpoint"]["name"] + " | room audio output=" + (int)parameters["Listen_Output_ID"]);
                    text.Append("Audio / Video module parameters: ");
                    foreach (JProperty p in parameters.Properties()) text.Append(p.Name + "=" + p.Value + "  ");
                    text.AppendLine();
                }
                else text.AppendLine("Audio/Video endpoints and matrix output unresolved. Identify Watch/Listen endpoints with the guided capture test; no values guessed.");
                foreach (string mode in new[] { "watch", "listen" })
                {
                    text.AppendLine(mode.ToUpperInvariant() + " SOURCES:");
                    foreach (JToken source in (JArray)room[mode + "_sources"])
                        text.AppendLine("  " + (string)source["name"] + " | Source_ID$=" + (string)source["Source_ID$"]);
                }
            }
            text.AppendLine();
            text.AppendLine("CATEGORY CANDIDATES (controller category membership; confirm device function before configuring)");
            JObject categories = inventory["categories"] as JObject ?? new JObject();
            var candidates = new JObject();
            foreach (JProperty category in categories.Properties())
            {
                var entries = new JArray();
                foreach (JToken item in SetupList(category.Value))
                {
                    int id = SetupId(item["item_id"]);
                    if (id == 0) continue;
                    var candidate = new JObject(new JProperty("item_id", id), new JProperty("name", label(id)),
                        new JProperty("status", "category_candidate_verify_device_function"));
                    int type = SetupId(item["type"]);
                    candidate["type"] = type;
                    if (type == 7 && category.Name == "lights")
                        candidate["candidate_module_parameters"] = new JObject(new JProperty("Gateway_ID", gatewayId), new JProperty("Item_ID", id));
                    entries.Add(candidate);
                    text.AppendLine("  " + category.Name + ": " + label(id) + " | item=" + id + " type=" + type +
                        (type == 7 && category.Name == "lights" ? " | Light: Gateway_ID=" + gatewayId + " Item_ID=" + id + " (candidate)" : ""));
                }
                candidates[category.Name] = entries;
            }
            text.AppendLine();
            text.AppendLine("ALL INVENTORY ITEMS (room membership may be unavailable)");
            foreach (JToken device in devices)
                text.AppendLine("  " + (string)device["name"] + " | item=" + device["item_id"] + " type=" + device["type"]);
            text.AppendLine();
            text.AppendLine("UNRESOLVED: automatic endpoint/output relationships, unclassified shade/proxy functions, exact active source and catalog browsing.");
            text.AppendLine("GUIDED IDENTIFICATION: start Capture_Audio_Feedback, change only the target room's Listen volume/mute, then Export_Inventory. Current diagnostic capture is scoped to Living Room; other rooms need configurable capture support before using this method.");
            foreach (JToken warning in warnings) text.AppendLine("WARNING: " + warning);
            var roomArray = new JArray();
            foreach (JObject room in rooms.Values) roomArray.Add(room);
            return new JObject(new JProperty("schema_version", 1), new JProperty("generated_utc", generated),
                new JProperty("gateway_id", gatewayId), new JProperty("completeness", "partial"),
                new JProperty("source_icon_fields", sourceIconFields), new JProperty("source_discovery_status", sourceStatus), new JProperty("omitted_source_entries", omitted),
                new JProperty("rooms", roomArray), new JProperty("category_candidates", candidates),
                new JProperty("devices", devices), new JProperty("warnings", warnings), new JProperty("readable_text", text.ToString()));
        }
    }
}


