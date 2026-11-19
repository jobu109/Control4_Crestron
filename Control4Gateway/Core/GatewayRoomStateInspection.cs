using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;

namespace Control4Crestron
{
    public sealed partial class GatewayCore
    {
        private readonly HashSet<ushort> _roomInspectionPending = new HashSet<ushort>();
        private readonly Queue<JObject> _roomStateInspections = new Queue<JObject>();
        public void InspectRoomState(ushort roomId)
        {
            int gen, epoch;
            AudioRoom registration;
            lock (_sync)
            {
                if (!_online || !_roomModules.ContainsKey(roomId) || !_audioRooms.TryGetValue(roomId, out registration))
                { EmitMessage("Connect and initialize Sources before inspecting room state"); return; }
                if (!_roomInspectionPending.Add(roomId)) { EmitMessage("Room state inspection already running"); return; }
                gen = _generation; epoch = _feedbackEpoch;
            }
            Queue(delegate
            {
                try
                {
                    string host, token;
                    if (!GetConnection(out host, out token)) return;
                    lock (_sync) { if (!InspectionCurrent(roomId, registration, gen, epoch)) return; }
                    string json = Request(host, token, "GET", "/api/v1/items/" + roomId + "/variables", null);
                    JObject inspection = BuildRoomStateInspection(json, roomId);
                    int readCount = 0, missingCandidates = 0;
                    var pendingValues = new List<JObject>();
                    var scheduledNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    foreach (JObject field in (JArray)inspection["variables"])
                    {
                        string name = (string)field["name"];
                        string upper = name.ToUpperInvariant();
                        bool state = upper == "POWER_STATE" || upper == "CURRENT_VOLUME" || upper == "IS_MUTED";
                        if ((string)field["value_type"] != "missing" || (!(bool)field["source_candidate"] && !state)) continue;
                        if (!scheduledNames.Add(name)) continue;
                        if (!state && missingCandidates++ >= 16) continue;
                        pendingValues.Add(field);
                    }
                    foreach (JObject field in pendingValues)
                    {
                        lock (_sync) { if (!InspectionCurrent(roomId, registration, gen, epoch)) return; }
                        string name = (string)field["name"];
                        try
                        {
                            readCount++;
                            string response = Request(host, token, "GET", "/api/v1/items/" + roomId + "/variables?varnames=" + Uri.EscapeDataString(name), null);
                            JObject observation = BuildFilteredRoomVariable(response, name);
                            field["value_type"] = observation["value_type"].DeepClone();
                            if (observation["numeric_candidate"] != null) field["numeric_candidate"] = observation["numeric_candidate"].DeepClone();
                            if (observation["measured_value"] != null) field["measured_value"] = observation["measured_value"].DeepClone();
                        }
                        catch (Exception ex)
                        {
                            InvalidateRejectedToken(ex);
                            lock (_sync) { if (!InspectionCurrent(roomId, registration, gen, epoch)) throw; }
                            field["read_error"] = FailureKind(ex);
                        }
                    }
                    inspection["filtered_value_read_count"] = readCount;
                    inspection["filtered_value_reads_truncated"] = missingCandidates > 16;
                    lock (_sync)
                    {
                        if (!InspectionCurrent(roomId, registration, gen, epoch)) return;
                        _roomStateInspections.Enqueue(inspection);
                        while (_roomStateInspections.Count > 12) _roomStateInspections.Dequeue();
                    }
                    EmitMessage("Room " + roomId + " state snapshot recorded (" + inspection["exported_variable_count"] + " names); Export_Inventory includes room_state_inspections");
                }
                catch (Exception ex)
                {
                    lock (_sync) { if (!InspectionCurrent(roomId, registration, gen, epoch)) return; }
                    InvalidateRejectedToken(ex);
                    EmitMessage("Room state inspection failed: " + FailureKind(ex));
                }
                finally { lock (_sync) { _roomInspectionPending.Remove(roomId); } }
            });
        }
        private bool InspectionCurrent(ushort id, AudioRoom registration, int gen, int epoch)
        {
            AudioRoom current;
            return _online && gen == _generation && epoch == _feedbackEpoch && _roomModules.ContainsKey(id) &&
                _audioRooms.TryGetValue(id, out current) && Object.ReferenceEquals(current, registration);
        }
        private static JObject BuildRoomStateInspection(string json, ushort roomId)
        {
            if (roomId == 0 || String.IsNullOrEmpty(json) || json.Length > 262144) throw new InvalidDataException("Invalid room variable inspection");
            JArray source = JToken.Parse(json) as JArray;
            if (source == null) throw new InvalidDataException("Expected room variable array");
            var fields = new JArray();
            int omitted = 0, visited = 0;
            foreach (JToken item in source)
            {
                if (++visited > 512) break;
                JObject entry = item as JObject;
                JToken rawName = item.Type == JTokenType.String ? item : entry == null ? null :
                    MediaMetadataField(entry, "varname", "variableName", "variable_name", "var_name", "variable", "key", "name");
                if (rawName == null || rawName.Type != JTokenType.String) { omitted++; continue; }
                string name = (string)rawName;
                if (!Regex.IsMatch(name, "^[A-Za-z_][A-Za-z0-9_]{0,63}$") || SensitiveMediaKey(name)) { omitted++; continue; }
                if (item.Type == JTokenType.String && !Regex.IsMatch(name, "^[A-Z_][A-Z0-9_]{0,63}$")) { omitted++; continue; }
                fields.Add(RoomVariableObservation(name, entry == null ? null : MediaMetadataField(entry, "value")));
            }
            return new JObject(new JProperty("room_id", roomId), new JProperty("generated_utc", DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture)),
                new JProperty("request_path", "/api/v1/items/" + roomId + "/variables"), new JProperty("variable_count", source.Count),
                new JProperty("exported_variable_count", fields.Count), new JProperty("omitted_variable_count", omitted),
                new JProperty("inspection_truncated", source.Count > 512), new JProperty("variables", fields),
                new JProperty("first_entry_shape", RoomVariableShape(source.Count == 0 ? null : source[0])),
                new JProperty("interpretation", "Numeric source candidates are observations, not verified active-source IDs. No selected feedback is asserted."));
        }
        private static JObject RoomVariableObservation(string name, JToken value)
        {
            string upper = name.ToUpperInvariant();
            bool candidate = upper.Contains("SOURCE") || upper.Contains("DEVICE") || upper.Contains("SELECTION");
            var field = new JObject(new JProperty("name", name), new JProperty("value_type", value == null ? "missing" : value.Type.ToString()), new JProperty("source_candidate", candidate));
            int number;
            if (candidate && value != null && (value.Type == JTokenType.Integer || value.Type == JTokenType.String) &&
                Int32.TryParse(value.ToString(), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out number) && number >= -1)
                field["numeric_candidate"] = number;
            else if (upper == "POWER_STATE" || upper == "IS_MUTED" || upper == "CURRENT_VOLUME")
            {
                ushort state;
                bool valid = upper == "CURRENT_VOLUME" ? TryAudioVolume(value, out state) : TryAudioBoolean(value, out state);
                if (valid) field["measured_value"] = state;
            }
            return field;
        }
        private static JObject BuildFilteredRoomVariable(string json, string name)
        {
            if (String.IsNullOrEmpty(json) || json.Length > 16384) throw new InvalidDataException("Invalid filtered variable size");
            var entries = JToken.Parse(json) as JArray;
            if (entries == null || entries.Count != 1 || entries[0].Type != JTokenType.Object) throw new InvalidDataException("Invalid filtered variable schema");
            return RoomVariableObservation(name, MediaMetadataField((JObject)entries[0], "value"));
        }
        private static JObject RoomVariableShape(JToken item)
        {
            var shape = new JObject(new JProperty("type", item == null ? "missing" : item.Type.ToString()));
            var properties = new JArray(); int skipped = 0;
            var obj = item as JObject;
            if (obj != null) foreach (JProperty property in obj.Properties())
            {
                if (properties.Count >= 16 || !Regex.IsMatch(property.Name, "^[A-Za-z_][A-Za-z0-9_]{0,63}$") || SensitiveMediaKey(property.Name)) { skipped++; continue; }
                properties.Add(new JObject(new JProperty("key", property.Name), new JProperty("type", property.Value.Type.ToString())));
            }
            shape["properties"] = properties; shape["omitted_key_count"] = skipped;
            return shape;
        }
        private JArray RoomStateInspectionReports()
        {
            var result = new JArray();
            lock (_sync) foreach (JObject report in _roomStateInspections) result.Add(report.DeepClone());
            return result;
        }
    }
}

