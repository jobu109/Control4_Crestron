using System;
using System.Globalization;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Control4Crestron
{
    public sealed partial class GatewayCore
    {
        private DateTime _audioCaptureStartUtc = DateTime.MinValue;
        private DateTime _audioCaptureEndUtc;
        private int _audioCaptureGeneration, _audioCaptureBytes, _audioCaptureDropped;
        private readonly JArray _audioCaptureEvents = new JArray();

        // Explicit, bounded diagnostic for the currently researched Living Room setup.
        // These probe IDs do not configure production room/endpoint routing.
        public void StartAudioFeedbackCapture()
        {
            lock (_sync)
            {
                if (!_online) { EmitMessage("Connect before starting audio capture"); return; }
                _audioCaptureStartUtc = DateTime.UtcNow;
                _audioCaptureEndUtc = _audioCaptureStartUtc.AddSeconds(60);
                _audioCaptureGeneration = _generation;
                _audioCaptureBytes = _audioCaptureDropped = 0;
                _audioCaptureEvents.Clear();
            }
            EmitMessage("Audio capture started (60s): room 13, TV 682, matrix 339/340; then Export_Inventory");
        }

        private void CaptureAudioFeedbackEvent(JToken message, int gen)
        {
            lock (_sync)
            {
                if (!_online || _audioCaptureStartUtc == DateTime.MinValue || DateTime.UtcNow > _audioCaptureEndUtc ||
                    gen != _generation || gen != _audioCaptureGeneration) return;
                if (message == null || message.Type != JTokenType.Object) return;
                JToken id = message["iddevice"], eventName = message["evtName"], data = message["data"];
                int device;
                if (id == null || (id.Type != JTokenType.Integer && id.Type != JTokenType.String) ||
                    !Int32.TryParse(id.ToString(), NumberStyles.None, CultureInfo.InvariantCulture, out device) ||
                    (device != 339 && device != 340 && device != 682 && device != 7)) return;
                string kind = eventName != null && eventName.Type == JTokenType.String ? (string)eventName : "other";
                if (kind != "OnDataToUI" && kind != "devicecommand") kind = "other";
                JObject sanitized;
                if (device == 7)
                {
                    JToken activity = data != null && data.Type == JTokenType.Object ? data["active_state_changed"] : null;
                    if (activity == null || activity.Type != JTokenType.Object) return;
                    int roomId;
                    JToken room = activity["room_id"], type = activity["type"], active = activity["active"];
                    if (room == null || (room.Type != JTokenType.Integer && room.Type != JTokenType.String) ||
                        !Int32.TryParse(room.ToString(), out roomId) || roomId != 13 ||
                        type == null || type.Type != JTokenType.String || ((string)type != "watch" && (string)type != "listen") ||
                        active == null || active.Type != JTokenType.Boolean) return;
                    sanitized = new JObject(new JProperty("active_state_changed", new JObject(
                        new JProperty("room_id", 13), new JProperty("type", (string)type), new JProperty("active", (bool)active))));
                }
                else sanitized = AudioCaptureShape(data, 0);
                var record = new JObject(new JProperty("received_utc", DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture)),
                    new JProperty("iddevice", device), new JProperty("event_type", kind), new JProperty("data", sanitized));
                int length = record.ToString(Formatting.None).Length;
                if (_audioCaptureEvents.Count >= 256 || _audioCaptureBytes + length > 131072)
                { _audioCaptureDropped++; return; }
                _audioCaptureEvents.Add(record);
                _audioCaptureBytes += length;
            }
        }

        private static JObject AudioCaptureShape(JToken data, int depth)
        {
            var result = new JObject(new JProperty("type", data == null ? "missing" : data.Type.ToString()));
            var obj = data as JObject;
            if (obj == null || depth > 2) return result; // Never export scalar/string payload content.
            var keys = new JArray();
            var values = new JObject();
            int omitted = 0, visited = 0;
            foreach (JProperty field in obj.Properties())
            {
                if (visited++ >= 48) { result["fields_truncated"] = true; break; }
                string key = field.Name;
                if (!Regex.IsMatch(key, "^[A-Za-z_][A-Za-z0-9_]{0,47}$") || SensitiveMediaKey(key)) { omitted++; continue; }
                // Structural key names only; unknown string values and media metadata stay omitted.
                keys.Add(key);
                string normalized = key.Replace("_", "").ToLowerInvariant();
                bool numeric = false, boolean = false;
                switch (normalized)
                {
                    case "volume": case "volumelevel": case "level": case "output": case "outputid": case "outputindex":
                    case "input": case "inputid": case "channel": case "channelid": case "zone": case "zoneid": case "id":
                    case "roomid": case "bindingid": numeric = true; break;
                    case "mute": case "muted": case "muteflag": case "ismuted": case "powerstate": case "isvideobinding": boolean = true; break;
                }
                if (numeric && field.Value.Type == JTokenType.Integer)
                {
                    int number;
                    if (Int32.TryParse(field.Value.ToString(), out number) && number >= -1 && number <= 1000000) values[key] = number;
                }
                else if (boolean && field.Value.Type == JTokenType.Boolean) values[key] = field.Value.DeepClone();
                else if (boolean && field.Value.Type == JTokenType.Integer && (field.Value.ToString() == "0" || field.Value.ToString() == "1"))
                    values[key] = field.Value.DeepClone(); // Capture encoding without assigning production state.
                else if (normalized == "outputstatus" || normalized == "outputs" || normalized == "channels" || normalized == "zones")
                {
                    var array = field.Value as JArray;
                    if (array == null) values[key] = AudioCaptureShape(field.Value, depth + 1);
                    else
                    {
                        var entries = new JArray();
                        foreach (JToken item in array) { if (entries.Count == 8) break; entries.Add(AudioCaptureShape(item, depth + 1)); }
                        values[key] = new JObject(new JProperty("count", array.Count), new JProperty("sample", entries));
                    }
                }
            }
            result["field_keys"] = keys;
            result["unreported_key_count"] = omitted;
            result["values"] = values;
            return result;
        }

        private JObject AudioFeedbackCaptureReport()
        {
            lock (_sync)
            {
                if (_audioCaptureStartUtc == DateTime.MinValue) return null;
                return new JObject(new JProperty("diagnostic_scope", "Living Room endpoint mapping; not production routing configuration"),
                    new JProperty("room_id", 13), new JProperty("tv_endpoint", 682), new JProperty("matrix_driver", 339),
                    new JProperty("matrix_proxy", 340), new JProperty("activity_agent", 7),
                    new JProperty("started_utc", _audioCaptureStartUtc.ToString("o", CultureInfo.InvariantCulture)),
                    new JProperty("window_end_utc", _audioCaptureEndUtc.ToString("o", CultureInfo.InvariantCulture)),
                    new JProperty("capture_state", _audioCaptureGeneration != _generation ? "cancelled_connection_changed" :
                        DateTime.UtcNow <= _audioCaptureEndUtc ? "active" : "complete"),
                    new JProperty("event_count", _audioCaptureEvents.Count), new JProperty("dropped_event_count", _audioCaptureDropped),
                    new JProperty("events", _audioCaptureEvents.DeepClone()));
            }
        }
    }
}
