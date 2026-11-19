using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Control4Crestron
{
    public sealed partial class GatewayCore
    {
        public event EventHandler<RoomCatalogEventArgs> RoomProgressChanged;
        internal static JObject ParseProgress(JObject args)
        {
            int length, offset;
            if (args == null || !TrySourceId(args["length"], out length) || !TrySourceId(args["offset"], out offset) ||
                length <= 0 || length > 172800 || offset > length) return null;
            return new JObject(new JProperty("valid", true), new JProperty("position_seconds", offset), new JProperty("duration_seconds", length));
        }
        private void PublishProgress(ushort room, PlayingRoom state)
        {
            JObject data = state == null || state.Progress == null ? new JObject(new JProperty("valid", false)) : (JObject)state.Progress.DeepClone();
            data["room_id"] = room;
            if (state != null) { data["source_id"] = state.Source; data["track_revision"] = state.TrackRevision; }
            string json = data.ToString(Formatting.None);
            if (json.Length > 255) throw new InvalidOperationException("Progress packet exceeds serial limit");
            var handler = RoomProgressChanged;
            if (handler != null) SafeEvent(delegate { handler(this, new RoomCatalogEventArgs { RoomId = room, Kind = "progress", Valid = (ushort)((bool)data["valid"] ? 1 : 0), Json = json }); });
        }
        private void UpdateProgressTrack(ushort room, PlayingRoom state)
        {
            if (state == null) { PublishProgress(room, null); return; }
            string identity = state.Data == null || !(bool)state.Data["valid"] ? "" :
                (string)state.Data["title"] + "\0" + (string)state.Data["artist"] + "\0" + (string)state.Data["album"] + "\0" + (string)state.Data["station"];
            if (identity == state.TrackIdentity) return;
            state.TrackIdentity = identity; state.TrackRevision = Guid.NewGuid().ToString("N").Substring(0, 16);
            state.Progress = null; PublishProgress(room, state);
        }
        private void RouteProgress(JToken message, JObject evt, int generation)
        {
            int source;
            if (!TrySourceId(message["iddevice"], out source)) return;
            JObject data = ParseProgress(evt["ARGS"] as JObject);
            lock (_sync)
            {
                if (!_online || _generation != generation) return;
                foreach (var entry in _playingRooms)
                {
                    PlayingRoom state = entry.Value;
                    if (state.Source != source || !PlayingCurrent(entry.Key, state) || state.TrackIdentity.Length == 0) continue;
                    if (JToken.DeepEquals(state.Progress, data)) continue;
                    state.Progress = data; PublishProgress(entry.Key, state);
                }
            }
        }
    }
}
