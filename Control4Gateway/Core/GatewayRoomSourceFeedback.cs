using System;
using System.Globalization;
using System.IO;
using Newtonsoft.Json.Linq;

namespace Control4Crestron
{
    public sealed partial class GatewayCore
    {
        public event EventHandler<RoomActiveSourceEventArgs> RoomActiveSourceChanged;
        private static bool TrySourceId(JToken value, out int id)
        {
            id = 0;
            return value != null && (value.Type == JTokenType.Integer || value.Type == JTokenType.String) &&
                Int32.TryParse(value.ToString(), NumberStyles.None, CultureInfo.InvariantCulture, out id) && id >= 0;
        }
        private static bool SourceVariable(JArray entries, string name, out JToken value)
        {
            value = null; int matches = 0;
            foreach (JToken item in entries)
            {
                var entry = item as JObject;
                if (entry == null) continue;
                JToken field = MediaMetadataField(entry, "varname", "variableName", "variable_name", "var_name", "name");
                if (field == null || field.Type != JTokenType.String || !String.Equals((string)field, name, StringComparison.OrdinalIgnoreCase)) continue;
                matches++; value = MediaMetadataField(entry, "value");
            }
            return matches == 1 && value != null;
        }
        internal static RoomActiveSourceEventArgs ParseRoomSourceState(string json, ushort roomId)
        {
            if (String.IsNullOrEmpty(json) || json.Length > 32768) throw new InvalidDataException("Invalid source readback size");
            var entries = JToken.Parse(json) as JArray;
            if (entries == null || entries.Count > 64) throw new InvalidDataException("Invalid source readback schema");
            var result = new RoomActiveSourceEventArgs { RoomId = roomId };
            JToken raw; ushort power;
            if (!SourceVariable(entries, "POWER_STATE", out raw) || !TryAudioBoolean(raw, out power)) return result;
            if (power == 0) { result.AudioValid = result.VideoValid = 1; return result; }
            int video, audioDevice, playingAudio;
            if (SourceVariable(entries, "CURRENT_VIDEO_DEVICE", out raw) && TrySourceId(raw, out video))
            { result.VideoId = video; result.VideoValid = 1; }
            if (SourceVariable(entries, "CURRENT_AUDIO_DEVICE", out raw) && TrySourceId(raw, out audioDevice) &&
                SourceVariable(entries, "PLAYING_AUDIO_DEVICE", out raw) && TrySourceId(raw, out playingAudio))
            {
                // Spotify reports the generic renderer100002 as CURRENT_AUDIO_DEVICE,
                // while PLAYING_AUDIO_DEVICE is the actual listed service65.
                if (audioDevice > 0 && playingAudio > 0) { result.AudioId = playingAudio; result.AudioValid = 1; }
                else if (audioDevice == 0 && playingAudio == 0) result.AudioValid = 1;
            }
            return result;
        }
        private void PublishRoomSource(ushort id, RoomActiveSourceEventArgs state)
        {
            UpdatePlayingSource(id, state);
            var handler = RoomActiveSourceChanged;
            var args = state ?? new RoomActiveSourceEventArgs { RoomId = id };
            if (handler != null) SafeEvent(delegate { handler(this, args); });
        }
        internal void PublishCurrentRoomSource(ushort id)
        {
            lock (_sync) { AudioRoom room; if (_audioRooms.TryGetValue(id, out room)) PublishRoomSource(id, room.SourceState); }
        }
        private void ApplyRoomSourceFeedback(ushort id, AudioQueryWork work, RoomActiveSourceEventArgs state, int version)
        {
            lock (_sync)
            {
                if (!AudioWorkCurrent(id, work) || work.Configuration.SourceVersion != version) return;
                work.Configuration.SourceState = state;
                PublishRoomSource(id, state);
            }
        }
        private void InvalidateRoomSource(ushort id, AudioQueryWork work)
        {
            lock (_sync)
            {
                if (!AudioWorkCurrent(id, work)) return;
                work.Configuration.SourceVersion++;
                work.Configuration.SourceState = null;
                PublishRoomSource(id, null);
            }
        }
        private void ReadRoomSourceFeedback(ushort id, AudioQueryWork work, string host, string token)
        {
            int version;
            lock (_sync)
            {
                if (!AudioWorkCurrent(id, work) || !_roomModules.ContainsKey(id)) return;
                version = work.Configuration.SourceVersion;
            }
            try
            {
                string json = Request(host, token, "GET", "/api/v1/items/" + id +
                    "/variables?varnames=POWER_STATE,CURRENT_AUDIO_DEVICE,CURRENT_VIDEO_DEVICE,PLAYING_AUDIO_DEVICE", null);
                ApplyRoomSourceFeedback(id, work, ParseRoomSourceState(json, id), version);
            }
            catch (Exception ex)
            {
                lock (_sync) { if (!AudioWorkCurrent(id, work)) return; }
                InvalidateRejectedToken(ex);
                ApplyRoomSourceFeedback(id, work, null, version);
                EmitMessage("Room " + id + " active-source read failed: " + FailureKind(ex));
            }
        }
    }
}
