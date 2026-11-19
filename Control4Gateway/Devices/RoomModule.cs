using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Control4Crestron
{
    public sealed class Control4_Room
    {
        private readonly object _browserSync = new object();
        private GatewayCore _core;
        private ushort _roomId;
        private bool _registered, _sourcesValid;
        private string _roomName = "";
        private JArray _watch = new JArray(), _listen = new JArray();
        private ushort _audioPage = 1, _videoPage = 1;
        private ushort _audioSlots = 15, _videoSlots = 15;
        private int _slotPublishCapacity = 15;
        private RoomActiveSourceEventArgs _activeSource;
        private int _lastAutoServiceSource = -1;
        private JArray _mediaItems = new JArray();
        private JObject _mediaData;
        private string _mediaKind = "";
        private ushort _mediaPage = 1;
        private bool _mediaValid;
        private readonly UiSerialFrames _audioWire = new UiSerialFrames(), _videoWire = new UiSerialFrames(), _mediaWire = new UiSerialFrames();
        public event EventHandler<AudioStatusEventArgs> FeedbackChanged;
        public event EventHandler<RoomSourceListsEventArgs> SourceListsChanged;
        public event EventHandler<RoomBrowserEventArgs> BrowserChanged;
        public event EventHandler<GatewayMessageEventArgs> DiagnosticReceived;
        public event EventHandler<RoomSourceSlotEventArgs> SourceSlotChanged;
        public event EventHandler<RoomSourceSlotsStatusEventArgs> SourceSlotsStatusChanged;
        public event EventHandler<RoomSelectedSlotsEventArgs> SelectedSlotsChanged;
        public event EventHandler<RoomMediaPageEventArgs> MediaPageChanged;
        private readonly UiSerialFrames _playingWire = new UiSerialFrames();
        private ushort _playingPage = 1;
        private string _playingJson = "";
        private ushort _playingValid;
        public event EventHandler<RoomMediaPageEventArgs> NowPlayingChanged;
        public event EventHandler<RoomCatalogEventArgs> ProgressChanged;
        private void OnProgress(object sender, RoomCatalogEventArgs args)
        { if (_registered && args.RoomId == _roomId) { var handler = ProgressChanged; if (handler != null) handler(this, args); } }
        public void SetNowPlayingPage(ushort page)
        { lock (_browserSync) { _playingPage = page == 0 ? (ushort)1 : page; } PublishNowPlaying(); }
        private void OnNowPlaying(object sender, RoomCatalogEventArgs args)
        {
            if (!_registered || args.RoomId != _roomId) return;
            lock (_browserSync) { _playingJson = args.Json; _playingValid = args.Valid; }
            PublishNowPlaying();
        }
        private void PublishNowPlaying()
        {
            RoomMediaPageEventArgs args;
            lock (_browserSync)
            {
                string json = String.IsNullOrEmpty(_playingJson) ? new JObject(new JProperty("room_id", _roomId), new JProperty("valid", false)).ToString(Formatting.None) : _playingJson;
                if (_playingWire.Set(_roomId, "n", json)) _playingPage = 1;
                string frame = _playingWire.Read(ref _playingPage);
                args = new RoomMediaPageEventArgs { Json = frame, Valid = _playingValid, Page = _playingPage, Pages = (ushort)_playingWire.Count };
            }
            var handler = NowPlayingChanged; if (handler != null) handler(this, args);
        }
        public ushort ConfigureSourceSlots(ushort videoSources, ushort audioSources)
        {
            if (videoSources < 1 || audioSources < 1)
            { Diagnostic("Source slot counts must be nonzero"); return 0; }
            lock (_browserSync) { _videoSlots = videoSources; _audioSlots = audioSources; _slotPublishCapacity = Math.Max(_slotPublishCapacity, Math.Max((int)videoSources, (int)audioSources)); }
            PublishSourceSlots();
            PublishSelectedSlots();
            return 1;
        }
        public void SelectVideoSourceIndex(ushort index) { SelectSourceIndex(index, true); }
        public void SelectAudioSourceIndex(ushort index) { SelectSourceIndex(index, false); }
        private void SelectSourceIndex(ushort index, bool video)
        {
            int id;
            if (!TrySlotSource(index, video, out id)) { Diagnostic("Source slot is unavailable or outside the configured range"); return; }
            if (video) Send("SelectVideoSource", id); else OpenListenSource(id);
        }
        private bool TrySlotSource(ushort index, bool video, out int id)
        {
            id = 0;
            lock (_browserSync)
            {
                JArray sources = video ? _watch : _listen;
                if (!_sourcesValid || index == 0 || index > (video ? _videoSlots : _audioSlots) || index > sources.Count) return false;
                id = (int)sources[index - 1]["source_id"]; return true;
            }
        }
        public void SelectVideoSourceName(string name) { SelectSourceName(name, true); }
        public void SelectAudioSourceName(string name) { SelectSourceName(name, false); }
        private void SelectSourceName(string name, bool video)
        {
            int id;
            if (!TryNamedSource(name, video, out id)) { Diagnostic("Source name is unavailable or ambiguous in the configured slots"); return; }
            if (video) Send("SelectVideoSource", id); else OpenListenSource(id);
        }
        private bool TryNamedSource(string name, bool video, out int id)
        {
            id = 0;
            if (String.IsNullOrWhiteSpace(name)) return false;
            name = name.Trim();
            lock (_browserSync)
            {
                if (!_sourcesValid) return false;
                JArray sources = video ? _watch : _listen;
                int matches = 0;
                for (int i = 0; i < Math.Min(sources.Count, video ? _videoSlots : _audioSlots); i++)
                    if (String.Equals(((string)sources[i]["name"] ?? "").Trim(), name, StringComparison.OrdinalIgnoreCase))
                    { matches++; id = (int)sources[i]["source_id"]; }
                if (matches == 1) return true;
                id = 0; return false;
            }
        }
        private void PublishSourceSlots()
        {
            var slots = new List<RoomSourceSlotEventArgs>();
            RoomSourceSlotsStatusEventArgs status;
            lock (_browserSync)
            {
                for (int i = 1; i <= _slotPublishCapacity; i++)
                    foreach (bool video in new[] { true, false })
                    {
                        JArray sources = video ? _watch : _listen;
                        bool available = _sourcesValid && i <= (video ? _videoSlots : _audioSlots) && i <= sources.Count;
                        slots.Add(new RoomSourceSlotEventArgs { Video = (ushort)(video ? 1 : 0), Index = (ushort)i,
                            Available = (ushort)(available ? 1 : 0), Name = available ? (string)sources[i - 1]["name"] : "" });
                    }
                int audioExcess = _sourcesValid ? Math.Max(0, _listen.Count - _audioSlots) : 0;
                int videoExcess = _sourcesValid ? Math.Max(0, _watch.Count - _videoSlots) : 0;
                status = new RoomSourceSlotsStatusEventArgs { AudioOverflow = (ushort)(audioExcess > 0 ? 1 : 0),
                    VideoOverflow = (ushort)(videoExcess > 0 ? 1 : 0), AudioOverflowCount = (ushort)audioExcess, VideoOverflowCount = (ushort)videoExcess };
            }
            var slotHandler = SourceSlotChanged;
            if (slotHandler != null) foreach (var slot in slots) slotHandler(this, slot);
            var statusHandler = SourceSlotsStatusChanged; if (statusHandler != null) statusHandler(this, status);
        }
        internal bool AutomaticServiceBrowse = true;

        internal void Release()
        {
            if (_core == null) return;
            _core.RoomNowPlayingChanged -= OnNowPlaying;
            _core.RoomProgressChanged -= OnProgress;
            _core.RoomCatalogChanged -= OnRoomCatalog;
            _core.RoomActiveSourceChanged -= OnActiveSource;
            _core.AudioChanged -= OnFeedback;
            _core.RoomDiscovered -= OnDiscovery;
            _core.MessageReceived -= OnDiagnostic;
            if (_registered) _core.UnregisterRoomModule(_roomId);
            _registered = false;
            _core = null;
        }

        public ushort Initialize(ushort gatewayId, ushort roomId)
        {
            Release();
            _mediaKind = ""; _mediaValid = false; _mediaData = null; _mediaItems = new JArray(); _mediaPage = 1; _activeSource = null; _registered = false; _sourcesValid = false; _roomName = ""; _watch = new JArray(); _listen = new JArray();
            _core = GatewayRegistry.Get(gatewayId); _roomId = roomId;
            _playingJson = ""; _playingValid = 0; _playingPage = 1;
            _core.RoomNowPlayingChanged += OnNowPlaying;
            _core.RoomProgressChanged += OnProgress;
            _core.RoomCatalogChanged += OnRoomCatalog; _core.RoomActiveSourceChanged += OnActiveSource; _core.AudioChanged += OnFeedback; _core.RoomDiscovered += OnDiscovery; _core.MessageReceived += OnDiagnostic;
            _registered = _core.RegisterRoomModule(roomId, !AutomaticServiceBrowse);
            if (_registered) { _core.PublishCurrentAudio(roomId); _core.PublishRoomDiscovery(roomId); }
            else { PublishBrowser(); PublishSourceLists(); PublishSourceSlots(); PublishSelectedSlots(); }
            PublishMediaPage();
            PublishNowPlaying();
            if (_registered) _core.PublishCurrentPlaying(_roomId);
            return (ushort)(_registered ? 1 : 0);
        }
        public void Query() { if (_registered) _core.QueryAudio(_roomId); }
        public void InspectRoomState() { if (_registered) _core.InspectRoomState(_roomId); }
        public void InspectMediaCatalog() { if (_registered) _core.InspectRoomMediaCatalog(_roomId); }
        public void BrowsePlaylists() { BrowseMedia("playlists"); }
        public void BrowseAlbums() { BrowseMedia("albums"); }
        public void BrowseAudioStations() { BrowseMedia("audio_stations"); }
        public void BrowseMovies() { BrowseMedia("movies"); }
        public void BrowseVideoStations() { BrowseMedia("video_stations"); }
        public void BrowseServicePresets(ushort driverId, ushort sourceId)
        {
            if (sourceId == 0)
            {
                lock (_browserSync)
                {
                    if (_activeSource == null || _activeSource.AudioValid == 0 || _activeSource.AudioId <= 0 || _activeSource.AudioId > UInt16.MaxValue)
                    { Diagnostic("Wait for measured active audio source before automatic service browsing"); return; }
                    sourceId = (ushort)_activeSource.AudioId;
                }
            }
            lock (_browserSync) { _mediaKind = "service_presets"; _mediaValid = false; _mediaItems = new JArray(); _mediaData = null; _mediaPage = 1; }
            PublishMediaPage();
            if (_registered) _core.BrowseServicePresets(_roomId, driverId, sourceId);
        }
        private void BrowseMedia(string kind)
        {
            lock (_browserSync) { _mediaKind = kind; _mediaValid = false; _mediaItems = new JArray(); _mediaData = null; _mediaPage = 1; }
            PublishMediaPage();
            if (_registered) _core.BrowseRoomCatalog(_roomId, kind);
        }
        public void SetMediaPage(ushort page)
        { lock (_browserSync) { _mediaPage = page == 0 ? (ushort)1 : page; } PublishMediaPage(); }
        public void SelectMediaId(string mediaId)
        {
            string kind, revision, normalized;
            if (!TryMediaSelection(mediaId, null, null, out kind, out revision, out normalized))
            { Diagnostic("Media ID unavailable or not in the current selectable catalog"); return; }
            if (_registered) _core.SelectNativeRoomMedia(_roomId, kind, revision, normalized);
        }
        public void SelectServicePreset(ushort driverId, string preset)
        {
            if (_registered) _core.SelectServicePreset(_roomId, driverId, preset);
            else Diagnostic("Initialize Sources before selecting a service preset");
        }
        public void SetPandoraShuffle(ushort enabled)
        { if (_registered) _core.SetPandoraShuffle(_roomId, enabled != 0); }
        public void InspectServicePresets(ushort driverId, string reference)
        {
            if (_registered) _core.InspectServicePresets(_roomId, driverId, reference);
        }
        public void InspectNowPlaying()
        { if (_registered) _core.InspectNowPlaying(_roomId); }
        public void SelectMediaIndex(ushort index)
        {
            string id = null;
            lock (_browserSync)
            { if (_mediaValid && index > 0 && index <= _mediaItems.Count) id = (string)_mediaItems[index - 1]["id"]; }
            if (id == null) { Diagnostic("Native media index is unavailable"); return; }
            SelectMediaId(id);
        }
        public void SelectMediaRequest(string request)
        {
            try
            {
                if (String.IsNullOrEmpty(request) || request.Length > 512) { Diagnostic("Invalid media selection request size"); return; }
                var body = JObject.Parse(request);
                ushort id;
                if (body["room_id"] == null || body["room_id"].Type != JTokenType.Integer ||
                    !UInt16.TryParse(body["room_id"].ToString(), out id) || id != _roomId ||
                    body["kind"] == null || body["kind"].Type != JTokenType.String ||
                    body["revision"] == null || body["revision"].Type != JTokenType.String ||
                    body["id"] == null || body["id"].Type != JTokenType.String)
                { Diagnostic("Invalid media selection request fields"); return; }
                string kind, revision, normalized;
                if (!TryMediaSelection((string)body["id"], (string)body["kind"], (string)body["revision"], out kind, out revision, out normalized))
                { Diagnostic("Media selection request is stale or unavailable"); return; }
                if (_registered) _core.SelectNativeRoomMedia(_roomId, kind, revision, normalized);
            }
            catch (JsonReaderException) { Diagnostic("Invalid media selection JSON"); }
        }
        private bool TryMediaSelection(string mediaId, string expectedKind, string expectedRevision,
            out string kind, out string revision, out string normalized)
        {
            kind = revision = normalized = null;
            ulong number;
            if (!UInt64.TryParse(mediaId, NumberStyles.None, CultureInfo.InvariantCulture, out number) || number == 0) return false;
            normalized = number.ToString(CultureInfo.InvariantCulture);
            lock (_browserSync)
            {
                if (!_mediaValid || _mediaData == null || !(bool)_mediaData["selection_supported"]) return false;
                kind = _mediaKind; revision = (string)_mediaData["revision"];
                if ((expectedKind != null && expectedKind != kind) || (expectedRevision != null && expectedRevision != revision)) return false;
                foreach (JToken item in _mediaItems) if ((string)item["id"] == normalized) return true;
            }
            return false;
        }
        public void Discover() { if (_registered) _core.DiscoverRooms(); }
        public void SetVolume(ushort value) { Send("SetVolume", value); }
        public void MuteOn() { Send("MuteOn", 0); }
        public void MuteOff() { Send("MuteOff", 0); }
        public void RoomOff() { Send("RoomOff", 0); }
        public void SelectAudioSourceId(string sourceId) { SelectSourceId(sourceId, false); }
        public void SelectVideoSourceId(string sourceId) { SelectSourceId(sourceId, true); }
        private void SelectSourceId(string sourceId, bool video)
        {
            int id;
            if (!TryListedSourceId(sourceId, video, out id))
            { Diagnostic("Source ID is invalid or not in this room's valid source list"); return; }
            if (video) Send("SelectVideoSource", id); else OpenListenSource(id);
        }
        private bool TryListedSourceId(string sourceId, bool video, out int id)
        {
            id = 0;
            int parsed;
            if (!Int32.TryParse(sourceId, NumberStyles.None, CultureInfo.InvariantCulture, out parsed) || parsed <= 0) return false;
            lock (_browserSync)
            {
                if (!_sourcesValid) return false;
                foreach (JToken entry in video ? _watch : _listen)
                    if ((int)entry["source_id"] == parsed) { id = parsed; return true; }
            }
            return false;
        }
        public void SetAudioListPage(ushort page) { lock (_browserSync) { _audioPage = page == 0 ? (ushort)1 : page; } PublishSourceLists(); }
        public void SetVideoListPage(ushort page) { lock (_browserSync) { _videoPage = page == 0 ? (ushort)1 : page; } PublishSourceLists(); }
        private static List<JArray> SourcePages(JArray sources)
        { return WidgetPages(sources, "Source_ID$"); }
        private static List<JArray> WidgetPages(JArray sources, string idField)
        {
            var pages = new List<JArray>();
            var page = new JArray();
            foreach (JToken source in sources)
            {
                var item = new JObject(new JProperty("id", (string)source[idField]), new JProperty("name", (string)source["name"]));
                page.Add(item);
                if (page.Count > 1 && Encoding.UTF8.GetByteCount(page.ToString(Formatting.None)) > 3500)
                { page.RemoveAt(page.Count - 1); pages.Add(page); page = new JArray(item); }
            }
            pages.Add(page);
            return pages;
        }
        private static JArray FlattenPages(List<JArray> pages)
        {
            var items = new JArray();
            foreach (JArray page in pages) foreach (JToken item in page) items.Add(item.DeepClone());
            return items;
        }
        private string SourceDocument(List<JArray> pages, string mode, int total)
        {
            return new JObject(new JProperty("room_id", _roomId), new JProperty("mode", mode),
                new JProperty("valid", _sourcesValid), new JProperty("page", 1), new JProperty("pages", 1),
                new JProperty("total", total), new JProperty("items", FlattenPages(pages))).ToString(Formatting.None);
        }
        private void PublishSourceLists()
        {
            RoomSourceListsEventArgs args;
            lock (_browserSync)
            {
                if (_audioWire.Set(_roomId, "a", SourceDocument(SourcePages(_listen), "listen", _listen.Count))) _audioPage = 1;
                if (_videoWire.Set(_roomId, "v", SourceDocument(SourcePages(_watch), "watch", _watch.Count))) _videoPage = 1;
                string audio = _audioWire.Read(ref _audioPage), video = _videoWire.Read(ref _videoPage);
                args = new RoomSourceListsEventArgs { AudioPage = _audioPage, VideoPage = _videoPage,
                    AudioPages = (ushort)_audioWire.Count, VideoPages = (ushort)_videoWire.Count,
                    AudioJson = audio, VideoJson = video };
            }
            var h = SourceListsChanged; if (h != null) h(this, args);
        }
        private void OpenListenSource(int id)
        {
            lock (_browserSync) { _mediaKind = "service_presets"; _mediaValid = false; _mediaData = null; _mediaItems = new JArray(); _mediaPage = 1; }
            PublishMediaPage();
            if (_registered) _core.OpenListenSource(_roomId, id);
        }
        private void Send(string operation, int value) { if (_registered) _core.SendAudioCommand(_roomId, operation, value); }
        private void OnRoomCatalog(object sender, RoomCatalogEventArgs args)
        {
            if (!_registered || args.RoomId != _roomId || !Object.ReferenceEquals(sender, _core)) return;
            lock (_browserSync)
            {
                if (args.Kind != "" && args.Kind != _mediaKind) return;
                _mediaValid = args.Valid == 1;
                _mediaKind = args.Kind; _mediaPage = 1;
                if (String.IsNullOrEmpty(args.Kind)) _lastAutoServiceSource = -1;
                _mediaData = _mediaValid ? JObject.Parse(args.Json) : null;
                _mediaItems = _mediaData == null ? new JArray() : (JArray)_mediaData["items"];
            }
            PublishMediaPage();
        }
        private void PublishMediaPage()
        {
            RoomMediaPageEventArgs args;
            lock (_browserSync)
            {
                var pages = WidgetPages(_mediaItems, "id");
                
                bool partial = _mediaData != null && ((bool)_mediaData["truncated"] || (int)_mediaData["omitted"] > 0 || (int)_mediaData["unnamed"] > 0);
                bool selection = _mediaValid && _mediaData != null && (bool)_mediaData["selection_supported"] && _mediaItems.Count > 0;
                var packet = new JObject(new JProperty("room_id", _roomId), new JProperty("kind", _mediaKind),
                    new JProperty("scope", _mediaData == null ? "" : (string)_mediaData["scope"]), new JProperty("valid", _mediaValid),
                    new JProperty("revision", _mediaData == null ? "" : (string)_mediaData["revision"]),
                    new JProperty("page", 1), new JProperty("pages", 1), new JProperty("total", _mediaItems.Count),
                    new JProperty("partial", partial), new JProperty("items", FlattenPages(pages)), new JProperty("selection_supported", selection));
                if (_mediaKind == "service_presets" && _mediaData != null)
                {
                    packet["source_id"] = _mediaData["source_id"]; packet["driver_id"] = _mediaData["driver_id"];
                    if (_mediaData["selection_adapters"] != null) packet["selection_adapters"] = _mediaData["selection_adapters"];
                    if (_mediaData["pandora_shuffle"] != null) packet["pandora_shuffle"] = _mediaData["pandora_shuffle"];
                }
                if (_mediaWire.Set(_roomId, "m", packet.ToString(Formatting.None))) _mediaPage = 1;
                string wire = _mediaWire.Read(ref _mediaPage);
                args = new RoomMediaPageEventArgs { SelectionAvailable = (ushort)(selection ? 1 : 0), Valid = (ushort)(_mediaValid ? 1 : 0), Partial = (ushort)(partial ? 1 : 0),
                    Count = (ushort)_mediaItems.Count, Page = _mediaPage, Pages = (ushort)_mediaWire.Count, Kind = _mediaKind, Json = wire };
            }
            var handler = MediaPageChanged; if (handler != null) handler(this, args);
        }
        private void OnFeedback(object sender, AudioStatusEventArgs args)
        { if (_registered && args.RoomId == _roomId) { var h = FeedbackChanged; if (h != null) h(this, args); } }
        private void OnDiagnostic(object sender, GatewayMessageEventArgs args)
        { var h = DiagnosticReceived; if (h != null) h(this, args); }
        private void Diagnostic(string message) { OnDiagnostic(this, new GatewayMessageEventArgs { Message = message }); }
        private void OnDiscovery(object sender, RoomDiscoveryEventArgs args)
        {
            if (!_registered || args.RoomId != _roomId) return;
            lock (_browserSync) {
            _sourcesValid = false; _watch = new JArray(); _listen = new JArray();
            if (args.Valid == 1)
            {
                var room = JObject.Parse(args.Json);
                _roomName = (string)room["name"] ?? "";
                _watch = room["watch_sources"] as JArray ?? new JArray();
                _listen = room["listen_sources"] as JArray ?? new JArray();
                _sourcesValid = true;
            }
            _audioPage = _videoPage = 1; } PublishBrowser(); PublishSourceLists(); PublishSourceSlots(); PublishSelectedSlots();
        }
        private void OnActiveSource(object sender, RoomActiveSourceEventArgs args)
        {
            if (!_registered || args.RoomId != _roomId) return;
            bool changed = false;
            lock (_browserSync)
            {
                _activeSource = args;
                if (args.AudioValid == 1 && args.AudioId >= 0 && args.AudioId != _lastAutoServiceSource)
                { _lastAutoServiceSource = args.AudioId; changed = true; }
            }
            PublishSelectedSlots();
            if (!changed || !AutomaticServiceBrowse) return;
            lock (_browserSync)
            { _mediaKind = "service_presets"; _mediaValid = false; _mediaData = null; _mediaItems = new JArray(); _mediaPage = 1; }
            PublishMediaPage();
            _core.ClearRoomMediaForSourceChange(_roomId);
            if (args.AudioId > 0 && args.AudioId <= UInt16.MaxValue)
                BrowseServicePresets(0, (ushort)args.AudioId);
            else if (args.AudioId > UInt16.MaxValue)
                Diagnostic("Active audio source exceeds the service interface ID range");
        }
        private ushort ResolveSelectedSlot(int id, ushort valid, JArray list, ushort capacity, out ushort index)
        {
            index = 0;
            if (valid == 0) return 0;
            if (id == 0) return 1; // Measured none/off.
            if (!_sourcesValid) return 0;
            for (int i = 0; i < Math.Min(list.Count, capacity); i++)
                if ((int)list[i]["source_id"] == id) { index = (ushort)(i + 1); return 1; }
            return 0;
        }
        private void PublishSelectedSlots()
        {
            var args = new RoomSelectedSlotsEventArgs();
            lock (_browserSync)
            {
                if (_activeSource != null)
                {
                    ushort index;
                    args.AudioValid = ResolveSelectedSlot(_activeSource.AudioId, _activeSource.AudioValid, _listen, _audioSlots, out index);
                    args.AudioIndex = index;
                    args.VideoValid = ResolveSelectedSlot(_activeSource.VideoId, _activeSource.VideoValid, _watch, _videoSlots, out index);
                    args.VideoIndex = index;
                }
            }
            var handler = SelectedSlotsChanged; if (handler != null) handler(this, args);
        }
        private void PublishBrowser()
        {
            RoomBrowserEventArgs args;
            lock (_browserSync) {
            args = new RoomBrowserEventArgs {
                RoomName = _roomName, SourcesValid = (ushort)(_sourcesValid ? 1 : 0),
                HasWatch = (ushort)(_sourcesValid && _watch.Count > 0 ? 1 : 0), HasListen = (ushort)(_sourcesValid && _listen.Count > 0 ? 1 : 0),
                WatchCount = (ushort)_watch.Count, ListenCount = (ushort)_listen.Count,
            };
            }
            var h = BrowserChanged; if (h != null) h(this, args);
        }
    }
}
