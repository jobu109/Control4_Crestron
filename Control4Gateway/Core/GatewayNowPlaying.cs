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
        private sealed class PlayingRoom
        {
            internal AudioRoom Registration;
            internal int Source, Generation, Epoch, Version;
            internal JObject Data;
            internal bool TuneInReadPending;
            internal string Action = "unknown";
            internal JObject Progress;
            internal string TrackIdentity = "", TrackRevision = "";
        }
        private sealed class PlayingQueue
        {
            internal DateTime Received;
            internal JObject Data;
            internal int? Index;
            internal bool TuneIn;
        }
        private readonly Dictionary<ushort, PlayingRoom> _playingRooms = new Dictionary<ushort, PlayingRoom>();
        private readonly Dictionary<int, PlayingQueue> _playingQueues = new Dictionary<int, PlayingQueue>();
        private readonly Dictionary<string, string> _tuneInLogos = new Dictionary<string, string>(StringComparer.Ordinal);
        private static string TuneInLogoKey(int source, string guide)
        { return source.ToString(CultureInfo.InvariantCulture) + ":" + guide; }

        internal static void ApplyTuneInLogo(JObject station, string logo)
        {
            if (String.IsNullOrEmpty(logo)) return;
            if (String.IsNullOrEmpty((string)station["artwork"])) station["artwork"] = logo;
            else if ((string)station["artwork"] != logo) station["station_artwork"] = logo;
        }

        public event EventHandler<RoomCatalogEventArgs> RoomNowPlayingChanged;
        private static string PlayingText(JToken token)
        {
            if (token == null || token.Type != JTokenType.String) return "";
            string text = Regex.Replace(token.ToString(), @"[\x00-\x1f]", " ").Trim();
            if (Regex.IsMatch(text, @"https?://|\bBearer\s|\beyJ[A-Za-z0-9_-]+\.", RegexOptions.IgnoreCase)) return "";
            return text.Length <= 512 ? text : text.Substring(0, 512);
        }
        internal static string PlayingArtwork(JToken token)
        {
            if (token == null || token.Type != JTokenType.String) return "";
            string text = token.ToString(); Uri uri;
            if (text.Length > 2048 || !Uri.TryCreate(text, UriKind.Absolute, out uri) || uri.Scheme != "https" ||
                uri.HostNameType != UriHostNameType.Dns || uri.UserInfo.Length > 0 || uri.Query.Length > 0 || uri.Fragment.Length > 0 ||
                Regex.IsMatch(text, @"[\s\x00-\x1f]") || SensitiveMediaKey(uri.AbsolutePath)) return "";
            return uri.AbsoluteUri;
        }
        private static JObject PlayingData(JObject item, string titleKey, int source, string artworkKey)
        {
            string title = PlayingText(item[titleKey]);
            return new JObject(new JProperty("source_id", source), new JProperty("valid", title.Length > 0),
                new JProperty("title", title), new JProperty("artist", PlayingText(item["artist"])),
                new JProperty("album", PlayingText(item["album"])),
                new JProperty("station", ""),
                new JProperty("artwork", artworkKey == null ? "" : PlayingArtwork(item[artworkKey])));
        }
        private static JObject PlayingSectionItem(JArray items, string label, out bool recognized)
        {
            recognized = false; int start = -1;
            for (int i = 0; i < items.Count; i++)
            {
                JObject item = items[i] as JObject;
                if (item == null || (string)item["title"] != label) continue;
                recognized = true;
                JToken header = item["isHeader"];
                if (start >= 0 || header == null || header.Type != JTokenType.Boolean || !(bool)header) return null;
                start = i;
            }
            if (!recognized) return null;
            JObject selected = null;
            for (int i = start + 1; i < items.Count; i++)
            {
                JObject item = items[i] as JObject; if (item == null) return null;
                JToken header = item["isHeader"];
                if (header != null)
                {
                    if (header.Type != JTokenType.Boolean) return null;
                    if ((bool)header) break;
                }
                if (selected != null) return null;
                selected = item;
            }
            return selected;
        }
        private static JObject PlayingSection(JArray items, int source, out bool recognized)
        {
            JObject selected = PlayingSectionItem(items, "On Now", out recognized);
            if (selected == null) return null;
            JObject result = PlayingData(selected, "title", source, "image");
            bool found; JObject station = PlayingSectionItem(items, "Playing From", out found);
            if (station != null) result["station"] = PlayingText(station["title"]);
            return result;
        }
        private static string TuneInArtwork(JToken token)
        {
            string safe = PlayingArtwork(token);
            if (safe.Length != 0) return safe;
            Uri uri;
            if (token?.Type != JTokenType.String || !Uri.TryCreate(token.ToString(), UriKind.Absolute, out uri) ||
                uri.Scheme != "http" || !uri.IsDefaultPort ||
                (uri.Host != "cdn-radiotime-logos.tunein.com" && uri.Host != "cdn-albums.tunein.com")) return "";
            return PlayingArtwork(new JValue(new UriBuilder(uri) { Scheme = "https", Port = -1 }.Uri.AbsoluteUri));
        }

        private static JObject PlayingTuneInStation(JObject current, JObject selected, int source)
        {
            string guide = PlayingText(current["guide_id"]);
            if (!Regex.IsMatch(guide, @"\As[0-9]{1,16}\z") || selected == null ||
                PlayingText(selected["guide_id"]) != guide || PlayingText(selected["itemType"]) != "station") return null;
            JObject result = PlayingData(current, "Title", source, null);
            result["station"] = result["title"];
            string artwork = TuneInArtwork(current["ImageUrl"]);
            if (artwork.Length == 0) artwork = TuneInArtwork(selected["ImageUrl"]);
            result["artwork"] = artwork;
            return result;
        }

        // Observed QueueChanged index includes the header row. Never flatten or
        // select the first title in a queue; verify any explicit current marker.
        internal static JObject ParsePlayingQueue(JToken message, out int source)
        {
            source = 0;
            if (!(message is JObject) || (string)message["evtName"] != "OnDataToUI" || !TrySourceId(message["iddevice"], out source) || source <= 0) return null;
            JObject evt = message["data"] is JObject ? message["data"]["EVENT"] as JObject : null;
            if (evt == null || (string)evt["NAME"] != "QueueChanged") return null;
            JObject args = evt["ARGS"] as JObject; if (args == null) return null;
            JToken rows = (args["List"] as JObject)?["item"];
            JObject currentStation = args["NowPlaying"] as JObject;
            if (currentStation != null && PlayingText(currentStation["itemType"]) == "station")
            {
                int stationIndex;
                if (!TrySourceId(args["NowPlayingIndex"], out stationIndex)) return null;
                var stationRows = rows as JArray;
                JObject stationRow = stationRows != null && stationRows.Count <= 256 && stationIndex < stationRows.Count
                    ? stationRows[stationIndex] as JObject : stationIndex == 0 ? rows as JObject : null;
                return PlayingTuneInStation(currentStation, stationRow, source);
            }
            JArray items = rows as JArray;
            if (items == null || items.Count > 256) return null;
            // Pandora's index1 identifies the station, not the song. Its explicit
            // On Now section has one current item, followed by You Just Heard.
            bool sectional; JObject section = PlayingSection(items, source, out sectional);
            if (sectional) return section;
            int index;
            if (!TrySourceId(args["NowPlayingIndex"], out index) || index >= items.Count) return null;
            JObject selected = items[index] as JObject;
            if (selected == null) return null;
            JToken header = selected["isHeader"];
            if (header != null && (header.Type != JTokenType.Boolean || (bool)header)) return null;
            int marked = -1;
            for (int i = 0; i < items.Count; i++)
            {
                JObject item = items[i] as JObject;
                JToken current = item == null ? null : item["current"];
                if (current != null && current.Type != JTokenType.Boolean) return null;
                if (current != null && (bool)current) { if (marked >= 0) return null; marked = i; }
            }
            if (marked >= 0 && marked != index) return null;
            JObject data = PlayingData(selected, "track", source, "album_cover_url");
            JObject first = items.Count > 0 ? items[0] as JObject : null;
            JToken firstHeader = first == null ? null : first["isHeader"];
            if (firstHeader != null && firstHeader.Type == JTokenType.Boolean && (bool)firstHeader)
                data["station"] = PlayingText(first["track"]);
            return data;
        }
        internal static JObject ParsePlayingRoom(string json, int source)
        {
            JObject media = ReadRoomMediaInfo(json, source);
            // Room img addressing is still unrecognized for other services.
            return media == null ? null : PlayingData(media, "title", source, null);
        }

        private static JObject ReadRoomMediaInfo(string json, int source)
        {
            if (String.IsNullOrEmpty(json) || json.Length > 262144) throw new InvalidDataException("Invalid now-playing response");
            JArray entries = JToken.Parse(json) as JArray; JToken value;
            if (entries == null || entries.Count > 128 || !SourceVariable(entries, "CURRENT MEDIA INFO", out value)) return null;
            if (value.Type == JTokenType.String) value = JToken.Parse(value.ToString());
            JObject media = value is JObject ? value["mediainfo"] as JObject : null;
            int owner;
            if (media == null || !TrySourceId(media["medSrcDev"], out owner) || owner != source) return null;
            return media;
        }
        // The room's REST response confirms the current track but does not provide
        // a usable image URL. Restore only an image from the same measured track.
        internal static void RestorePlayingArtwork(JObject measured, JObject queued)
        {
            if (measured == null || queued == null || (bool?)measured["valid"] != true ||
                (bool?)queued["valid"] != true || (int?)measured["source_id"] != (int?)queued["source_id"] ||
                !String.IsNullOrEmpty((string)measured["artwork"])) return;
            foreach (string key in new[] { "title", "artist" })
            {
                string value = (string)measured[key];
                if (String.IsNullOrWhiteSpace(value) ||
                    !String.Equals(value, (string)queued[key], StringComparison.OrdinalIgnoreCase)) return;
            }
            string album = (string)measured["album"], queueAlbum = (string)queued["album"];
            if (!String.IsNullOrEmpty(album) && !String.IsNullOrEmpty(queueAlbum) &&
                !String.Equals(album, queueAlbum, StringComparison.OrdinalIgnoreCase)) return;
            string artwork = PlayingArtwork(queued["artwork"]);
            if (artwork.Length != 0) measured["artwork"] = artwork;
        }

        internal static JObject ReadTuneInTrack(string json, int source, JObject station)
        {
            JObject media = ReadRoomMediaInfo(json, source);
            string name = PlayingText(station["station"]);
            // The sampled TuneIn room reports its station name in artist.
            // Require that name and measured source to match the queue before merging.
            if (media == null || name.Length == 0 ||
                !String.Equals(PlayingText(media["artist"]), name, StringComparison.OrdinalIgnoreCase)) return null;
            JObject track = PlayingData(media, "title", source, null);
            if ((bool?)track["valid"] != true) return null;
            track["station"] = name;
            track["artist"] = "";
            string artwork = TuneInArtwork(media["img"]);
            track["artwork"] = artwork.Length == 0 ? station["artwork"] : new JValue(artwork);
            ApplyTuneInLogo(track, (string)(station["station_artwork"] ?? station["artwork"]));
            return track;
        }

        private void RefreshTuneInTrack(ushort roomId, PlayingRoom state)
        {
            if (state.TuneInReadPending) return;
            state.TuneInReadPending = true;
            Queue(delegate
            {
                int version = -1;
                try
                {
                    string host, token;
                    if (!GetConnection(out host, out token)) return;
                    JObject station;
                    lock (_sync)
                    {
                        if (!PlayingCurrent(roomId, state)) return;
                        PlayingQueue snapshot;
                        if (!_playingQueues.TryGetValue(state.Source, out snapshot) || !snapshot.TuneIn) return;
                        station = snapshot.Data;
                        version = state.Version;
                    }
                    string json = Request(host, token, "GET", "/api/v1/items/" + roomId + "/variables?varnames=CURRENT%20MEDIA%20INFO", null);
                    JObject track = ReadTuneInTrack(json, state.Source, station);
                    lock (_sync)
                    {
                        if (track == null || !PlayingCurrent(roomId, state) || state.Version != version) return;
                        state.Data = track;
                        PublishPlaying(roomId, state);
                    }
                }
                catch (Exception ex)
                {
                    lock (_sync) { if (!PlayingCurrent(roomId, state)) return; }
                    InvalidateRejectedToken(ex);
                    EmitMessage("Room " + roomId + " TuneIn track read failed: " + FailureKind(ex));
                }
                finally
                {
                    lock (_sync)
                    {
                        state.TuneInReadPending = false;
                        PlayingQueue latest;
                        if (version >= 0 && state.Version != version && PlayingCurrent(roomId, state) &&
                            _playingQueues.TryGetValue(state.Source, out latest) && latest.TuneIn)
                            RefreshTuneInTrack(roomId, state);
                    }
                }
            });
        }

        private void PublishPlaying(ushort roomId, PlayingRoom state)
        {
            UpdateProgressTrack(roomId, state);
            JObject data = state == null || state.Data == null ? new JObject(new JProperty("valid", false)) : (JObject)state.Data.DeepClone();
            data["room_id"] = roomId;
            data["playback_action"] = state == null ? "unknown" : state.Action;
            data["track_revision"] = state == null ? "" : state.TrackRevision;
            var handler = RoomNowPlayingChanged;
            if (handler != null) SafeEvent(delegate { handler(this, new RoomCatalogEventArgs { RoomId = roomId, Kind = "now_playing", Valid = (ushort)((bool)data["valid"] ? 1 : 0), Json = data.ToString(Formatting.None) }); });
        }
        internal void PublishCurrentPlaying(ushort roomId)
        { lock (_sync) { PlayingRoom state; _playingRooms.TryGetValue(roomId, out state); PublishPlaying(roomId, state); PublishProgress(roomId, state); } }
        private bool PlayingCurrent(ushort room, PlayingRoom state)
        {
            PlayingRoom current;
            return InspectionCurrent(room, state.Registration, state.Generation, state.Epoch) && _playingRooms.TryGetValue(room, out current) && Object.ReferenceEquals(current, state);
        }
        private void UpdatePlayingSource(ushort roomId, RoomActiveSourceEventArgs source)
        {
            // Caller holds _sync. Called only from measured source publication.
            PlayingRoom current;
            if (source == null || source.AudioValid == 0 || source.AudioId <= 0)
            { _playingRooms.Remove(roomId); PublishPlaying(roomId, null); return; }
            if (_playingRooms.TryGetValue(roomId, out current) && current.Source == source.AudioId && PlayingCurrent(roomId, current)) return;
            AudioRoom registration;
            if (!_online || !_roomModules.ContainsKey(roomId) || !_audioRooms.TryGetValue(roomId, out registration)) return;
            var state = new PlayingRoom { Registration = registration, Source = source.AudioId, Generation = _generation, Epoch = _feedbackEpoch };
            _playingRooms[roomId] = state;
            PlayingQueue cached;
            if (_playingQueues.TryGetValue(state.Source, out cached) && (DateTime.UtcNow - cached.Received).TotalSeconds <= 10)
            { state.Data = cached.Data; state.Version++; }
            PublishPlaying(roomId, state);
            if (state.Data != null)
            {
                if (cached.TuneIn) RefreshTuneInTrack(roomId, state);
                return;
            }
            Queue(delegate
            {
                try
                {
                    string host, token; if (!GetConnection(out host, out token)) return;
                    lock (_sync) { if (!PlayingCurrent(roomId, state) || state.Version != 0) return; }
                    string json = Request(host, token, "GET", "/api/v1/items/" + roomId + "/variables?varnames=CURRENT%20MEDIA%20INFO", null);
                    JObject parsed = ParsePlayingRoom(json, state.Source);
                    lock (_sync)
                    {
                        if (!PlayingCurrent(roomId, state) || state.Version != 0) return;
                        PlayingQueue lastQueue;
                        if (_playingQueues.TryGetValue(state.Source, out lastQueue))
                            RestorePlayingArtwork(parsed, lastQueue.Data);
                        state.Data = parsed;
                        PublishPlaying(roomId, state);
                    }
                }
                catch (Exception ex) { lock (_sync) { if (!PlayingCurrent(roomId, state)) return; } InvalidateRejectedToken(ex); EmitMessage("Room " + roomId + " initial metadata read failed: " + FailureKind(ex)); }
            });
        }
        private void RoutePlayingEvent(JToken message, int generation)
        {
            if (!(message is JObject) || (string)message["evtName"] != "OnDataToUI" || !(message["data"] is JObject)) return;
            JObject incoming = message["data"]["EVENT"] as JObject;
            if (incoming != null && (string)incoming["NAME"] == "ProgressChanged")
            { RouteProgress(message, incoming, generation); return; }
            if (incoming != null && (string)incoming["NAME"] == "DashboardChanged")
            { RoutePlayingDashboard(message, incoming, generation); return; }
            if (incoming == null || (string)incoming["NAME"] != "QueueChanged" || message.ToString(Formatting.None).Length > 262144) return;
            JObject args = incoming["ARGS"] as JObject;
            int index;
            int? queueIndex = null;
            if (args != null && TrySourceId(args["NowPlayingIndex"], out index)) queueIndex = index;
            bool indexOnly = args != null && args["List"] == null && queueIndex.HasValue;
            if (indexOnly) foreach (JProperty property in args.Properties())
                if (property.Name != "NowPlayingIndex" && property.Name != "NowPlaying") indexOnly = false;
            int source; JObject data;
            try { data = ParsePlayingQueue(message, out source); } catch (Exception) { return; }
            var station = args?["NowPlaying"] as JObject;
            bool tuneIn = data != null && (bool?)data["valid"] == true && PlayingText(station?["itemType"]) == "station";
            if (data == null)
            {
                JObject evt = message["data"] is JObject ? message["data"]["EVENT"] as JObject : null;
                if (source <= 0 || evt == null || (string)evt["NAME"] != "QueueChanged" || (string)message["evtName"] != "OnDataToUI") return;
                // An incomplete queue change must not leave the preceding song visible.
                data = new JObject(new JProperty("source_id", source), new JProperty("valid", false));
            }
            lock (_sync)
            {
                if (!_online || _generation != generation) return;
                PlayingQueue previous;
                // Observed Pandora sequence: complete queue, then same-index
                // notification without List. It does not revoke the full snapshot.
                if (indexOnly && _playingQueues.TryGetValue(source, out previous) && previous.Index == queueIndex &&
                    previous.Data != null && (bool)previous.Data["valid"]) return;
                string logo;
                if (tuneIn && _tuneInLogos.TryGetValue(TuneInLogoKey(source, TuneInGuideId(station)), out logo))
                    ApplyTuneInLogo(data, logo);
                if (!_playingQueues.ContainsKey(source) && _playingQueues.Count >= 32) _playingQueues.Clear();
                _playingQueues[source] = new PlayingQueue { Received = DateTime.UtcNow, Data = data, Index = queueIndex, TuneIn = tuneIn };
                foreach (var entry in _playingRooms)
                {
                    PlayingRoom state = entry.Value;
                    if (state.Source != source || !PlayingCurrent(entry.Key, state)) continue;
                    // Keep the current song visible during a duplicate station notification.
                    bool sameStation = tuneIn && state.Data != null &&
                        (string)state.Data["station"] == (string)data["station"];
                    state.Version++;
                    if (!sameStation) { state.Data = data; PublishPlaying(entry.Key, state); }
                    else if (data["artwork"] != null && (string)data["artwork"] != "")
                    {
                        var artworkUpdate = (JObject)state.Data.DeepClone();
                        ApplyTuneInLogo(artworkUpdate, (string)(data["station_artwork"] ?? data["artwork"]));
                        if (!JToken.DeepEquals(state.Data, artworkUpdate))
                        { state.Data = artworkUpdate; PublishPlaying(entry.Key, state); }
                    }
                    if (tuneIn) RefreshTuneInTrack(entry.Key, state);
                }
            }
        }
        internal static string ParsePlayingAction(JToken items)
        {
            if (items == null || items.Type != JTokenType.String || items.ToString().Length > 65536) return "unknown";
            string text = System.Net.WebUtility.HtmlDecode(items.ToString());
            bool play = Regex.IsMatch(text, @"\bplay\b", RegexOptions.IgnoreCase);
            bool pause = Regex.IsMatch(text, @"\bpause\b", RegexOptions.IgnoreCase);
            return play == pause ? "unknown" : play ? "play" : "pause";
        }
        private void RoutePlayingDashboard(JToken message, JObject evt, int generation)
        {
            int source; JObject args = evt["ARGS"] as JObject;
            if (!TrySourceId(message["iddevice"], out source) || args == null || args.Property("Items") == null) return;
            string action = ParsePlayingAction(args["Items"]);
            lock (_sync)
            {
                if (!_online || _generation != generation) return;
                foreach (var entry in _playingRooms)
                {
                    PlayingRoom state = entry.Value;
                    if (state.Source != source || !PlayingCurrent(entry.Key, state) || state.Action == action) continue;
                    state.Action = action; PublishPlaying(entry.Key, state);
                }
            }
        }
        private void InvalidatePlaying()
        {
            _playingQueues.Clear(); _playingRooms.Clear(); _tuneInLogos.Clear();
            foreach (ushort room in _roomModules.Keys) PublishPlaying(room, null);
        }
    }
}
