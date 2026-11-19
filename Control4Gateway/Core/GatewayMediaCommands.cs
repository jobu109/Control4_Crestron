using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;

namespace Control4Crestron
{
    public sealed partial class GatewayCore
    {
        // Metadata inspection only: no advertised command is ever executed here.
        private static JObject BuildMediaCommandsReport(string json, int status)
        {
            return BuildMediaCommandsForSource(json, status, 20);
        }

        private static JObject BuildMediaCommandsForSource(string json, int status, int sourceId)
        {
            MediaSourceLabel(sourceId);
            JObject report = BuildCommandMetadataReport(json, status, sourceId);
            if (sourceId == 65)
            {
                JToken root = JToken.Parse(json);
                JArray commands = root as JArray;
                var wrapper = root as JObject;
                if (commands == null && wrapper != null) commands = MediaMetadataField(wrapper, "commands") as JArray;
                var schemas = new JArray();
                int budget = 256;
                if (commands != null)
                    foreach (JToken item in commands)
                    {
                        var definition = item as JObject;
                        if (definition == null || MediaMetadataName(MediaMetadataField(definition, "command", "commandName", "command_name", "name")) != "SelectPreset") continue;
                        schemas.Add(SpotifyPresetSchema(MediaMetadataField(definition, "parameters", "params", "tParams"), 0, ref budget));
                        if (schemas.Count >= 4 || budget <= 0) break;
                    }
                report["spotify_preset_parameter_schemas"] = schemas;
                report["spotify_preset_schema_truncated"] = budget <= 0;
                report["spotify_preset_note"] = "Read-only parameter structure and explicit choice fields only; choices are not validated playback selectors. No preset executed.";
            }
            return report;
        }

        private static JObject SpotifyPresetSchema(JToken value, int depth, ref int budget)
        {
            var result = new JObject(new JProperty("type", value == null ? "missing" : value.Type.ToString()));
            if (--budget <= 0 || depth >= 6) { result["truncated"] = true; return result; }
            var array = value as JArray;
            if (array != null)
            {
                result["count"] = array.Count;
                var children = new JArray();
                foreach (JToken child in array)
                {
                    if (children.Count >= 32 || budget <= 0) break;
                    children.Add(SpotifyPresetSchema(child, depth + 1, ref budget));
                }
                result["children"] = children;
                result["omitted"] = array.Count - children.Count;
            }
            var obj = value as JObject;
            if (obj != null)
            {
                var fields = new JObject();
                var otherFields = new JArray();
                int omitted = 0;
                foreach (JProperty field in obj.Properties())
                {
                    string key = field.Name.ToLowerInvariant();
                    if (budget <= 0 || fields.Count >= 32 || SensitiveMediaKey(key) ||
                        !Regex.IsMatch(key, "^(name|parameter|parametername|preset|room|type|required|min|max|options|choices|values|enum|items|id|value|label)$"))
                    {
                        if (otherFields.Count < 32 && !SensitiveMediaKey(key) && Regex.IsMatch(field.Name, "^[A-Za-z][A-Za-z0-9_.:-]{0,95}$"))
                            otherFields.Add(new JObject(new JProperty("name", field.Name), new JProperty("type", field.Value.Type.ToString())));
                        omitted++; continue;
                    }
                    JObject shape = SpotifyPresetSchema(field.Value, depth + 1, ref budget);
                    // Only schema identifiers/types/bounds are exported as scalar values.
                    // Choice/default values remain suppressed until their actual schema is established.
                    if ((key == "name" || key == "parameter" || key == "parametername") &&
                        field.Value.Type == JTokenType.String && (field.Value.ToString() == "Preset" || field.Value.ToString() == "Room"))
                        shape["schema_value"] = field.Value.ToString();
                    if (key == "type" && field.Value.Type == JTokenType.String &&
                        Regex.IsMatch(field.Value.ToString(), "^[A-Za-z][A-Za-z0-9 _.:/-]{0,95}$"))
                        shape["schema_value"] = field.Value.ToString();
                    if (key == "required" && field.Value.Type == JTokenType.Boolean) shape["schema_value"] = field.Value.DeepClone();
                    fields[field.Name] = shape;
                }
                result["fields"] = fields;
                result["omitted_fields"] = omitted;
                result["other_field_shapes"] = otherFields;
            }
            return result;
        }

        private static JObject BuildCommandMetadataReport(string json, int status, int sourceId)
        {
            if (sourceId <= 0) throw new ArgumentOutOfRangeException("sourceId");
            if (status != 200 || string.IsNullOrEmpty(json) || json.Length > 262144)
                throw new InvalidDataException("Invalid command metadata response");
            JToken root = JToken.Parse(json);
            var list = root as JArray;
            var wrapper = root as JObject;
            if (list == null && wrapper != null) list = MediaMetadataField(wrapper, "commands") as JArray;
            if (root.Type != JTokenType.Array && root.Type != JTokenType.Object)
                throw new InvalidDataException("Invalid command metadata shape");
            var commands = new JArray();
            var browseMatches = new SortedSet<string>(StringComparer.Ordinal);
            int omitted = 0;
            bool truncated = list != null && list.Count > 256;
            if (list != null)
            {
                int count = 0;
                foreach (JToken item in list)
                {
                    if (count++ >= 256) break;
                    var definition = item as JObject;
                    string name = definition == null ? null : MediaMetadataName(MediaMetadataField(definition, "command", "commandName", "command_name", "name"));
                    if (name == null) { omitted++; continue; }
                    int omittedParameters;
                    bool parametersTruncated;
                    JToken parameters = MediaMetadataField(definition, "parameters", "params", "tParams");
                    JArray normalized = MediaParameterDefinitions(parameters, out omittedParameters, out parametersTruncated);
                    commands.Add(new JObject(new JProperty("command_name", name),
                        new JProperty("parameter_container_type", parameters == null ? "missing" : parameters.Type.ToString()),
                        new JProperty("parameter_definitions", normalized),
                        new JProperty("unreported_parameter_count", omittedParameters),
                        new JProperty("parameters_truncated", parametersTruncated)));
                    string lower = name.ToLowerInvariant();
                    foreach (string term in new[] { "browse", "search", "list", "catalog", "menu", "playlist", "album", "artist", "track", "favorite", "queue" })
                        if (lower.Contains(term)) { browseMatches.Add(name); break; }
                }
            }
            return new JObject(new JProperty("source_id", sourceId), new JProperty("http_status", status),
                new JProperty("request_method", "GET"), new JProperty("request_path", "/api/v1/items/" + sourceId.ToString(System.Globalization.CultureInfo.InvariantCulture) + "/commands"),
                new JProperty("response_type", root.Type.ToString()), new JProperty("command_list_recognized", list != null),
                new JProperty("command_count", list == null ? (object)null : list.Count),
                new JProperty("exported_command_count", commands.Count), new JProperty("unreported_command_count", omitted),
                new JProperty("inspection_truncated", truncated), new JProperty("commands", commands),
                new JProperty("browse_name_matches", StringArray(browseMatches)),
                new JProperty("match_note", "Name matches are heuristic metadata candidates, not verified browse operations. No command was executed."),
                new JProperty("unrecognized_response_shape", list == null ? MediaCommandShape(root) : null),
                new JProperty("first_entry_shape", list != null && list.Count > 0 ? MediaCommandShape(list[0]) : null),
                new JProperty("browse_endpoint_status", "not_established"));
        }

        private static JToken MediaMetadataField(JObject item, params string[] names)
        {
            foreach (string name in names)
                foreach (JProperty field in item.Properties())
                    if (field.Name.Equals(name, StringComparison.OrdinalIgnoreCase)) return field.Value;
            return null;
        }

        private static string MediaMetadataName(JToken value)
        {
            if (value == null || value.Type != JTokenType.String) return null;
            string name = (string)value;
            // Names only; no labels, descriptions, URLs, default values or enum content.
            if (!Regex.IsMatch(name, "^[A-Za-z][A-Za-z0-9_.:-]{0,95}$") || SensitiveMediaKey(name)) return null;
            return name;
        }

        private static JObject MediaCommandShape(JToken item)
        {
            var result = new JObject(new JProperty("type", item == null ? "missing" : item.Type.ToString()));
            var obj = item as JObject;
            if (obj == null) return result;
            var keys = new JArray();
            int unknown = 0;
            foreach (JProperty property in obj.Properties())
            {
                switch (property.Name.ToLowerInvariant())
                {
                    case "commands": case "command": case "commandname": case "command_name": case "name":
                    case "parameters": case "params": case "tparams": case "type": case "required": case "min": case "max":
                        keys.Add(property.Name.ToLowerInvariant()); break;
                    default: unknown++; break;
                }
            }
            result["known_keys"] = keys;
            result["unreported_key_count"] = unknown;
            return result;
        }

        private static JArray MediaParameterDefinitions(JToken source, out int omitted, out bool truncated)
        {
            omitted = 0;
            truncated = false;
            var result = new JArray();
            var list = source as JArray;
            var map = source as JObject;
            int visited = 0;
            if (list != null)
            {
                truncated = list.Count > 32;
                foreach (JToken value in list)
                {
                    if (visited++ >= 32) break;
                    var definition = value as JObject;
                    string name = definition == null ? null : MediaMetadataName(MediaMetadataField(definition, "name", "parameter", "parameterName"));
                    JObject parameter = MediaParameterDefinition(name, definition);
                    if (parameter == null) omitted++; else result.Add(parameter);
                }
            }
            else if (map != null)
            {
                truncated = map.Count > 32;
                foreach (JProperty field in map.Properties())
                {
                    if (visited++ >= 32) break;
                    string name = MediaMetadataName(new JValue(field.Name));
                    JObject parameter = MediaParameterDefinition(name, field.Value as JObject);
                    if (parameter == null) omitted++; else result.Add(parameter);
                }
            }
            // Other containers are reported by type, without interpreting payload values.
            return result;
        }

        private static JObject MediaParameterDefinition(string name, JObject definition)
        {
            if (name == null) return null;
            var result = new JObject(new JProperty("name", name));
            if (definition == null) return result;
            JToken type = MediaMetadataField(definition, "type");
            if (type != null && type.Type == JTokenType.String)
            {
                string normalized = type.ToString().ToLowerInvariant();
                switch (normalized)
                {
                    case "string": case "str": case "integer": case "int": case "uint": case "number":
                    case "float": case "double": case "boolean": case "bool": case "enum": case "array": case "list": case "object":
                        result["type"] = normalized; break;
                }
            }
            JToken required = MediaMetadataField(definition, "required");
            if (required != null && required.Type == JTokenType.Boolean) result["required"] = required.DeepClone();
            foreach (string bound in new[] { "min", "max" })
            {
                JToken value = MediaMetadataField(definition, bound);
                if (value != null && (value.Type == JTokenType.Integer || value.Type == JTokenType.Float)) result[bound] = value.DeepClone();
            }
            return result;
        }
    }
}
