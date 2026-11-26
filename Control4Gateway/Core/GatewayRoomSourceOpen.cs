using System;
using System.Globalization;
using System.IO;
using Newtonsoft.Json.Linq;

namespace Control4Crestron
{
    public sealed partial class GatewayCore
    {
        private readonly System.Collections.Generic.Dictionary<ushort, object> _listenOpenWork = new System.Collections.Generic.Dictionary<ushort, object>();
        internal static bool HasSourceOwnedMedia(string json, int sourceId)
        {
            if (sourceId <= 0 || String.IsNullOrEmpty(json) || json.Length > 1048576) throw new InvalidDataException("Invalid source catalog probe");
            JToken root = JToken.Parse(json); JArray items = root as JArray;
            if (items == null && root is JObject) items = MediaMetadataField((JObject)root, "items", "stations", "results", "media", "data") as JArray;
            if (items == null || items.Count > 2048) throw new InvalidDataException("Incomplete source catalog probe");
            foreach (JObject item in items)
            {
                int owner;
                if (Int32.TryParse((item["device_id"] ?? new JValue("")).ToString(), NumberStyles.None, CultureInfo.InvariantCulture, out owner) && owner == sourceId) return true;
            }
            return false;
        }
        private bool ListenOpenCurrent(ushort roomId, AudioRoom registration, int gen, int epoch, object work)
        {
            object current;
            return InspectionCurrent(roomId, registration, gen, epoch) && _listenOpenWork.TryGetValue(roomId, out current) && Object.ReferenceEquals(current, work);
        }
        internal void OpenListenSource(ushort roomId, int sourceId)
        {
            AudioRoom registration; int gen, epoch; object work = new object();
            lock (_sync)
            {
                if (!_online || !_roomModules.ContainsKey(roomId) || !_audioRooms.TryGetValue(roomId, out registration))
                { EmitMessage("Connect and initialize Sources before opening a Listen source"); return; }
                gen = _generation; epoch = _feedbackEpoch;
                _listenOpenWork[roomId] = work;
                _roomCatalogWork.Remove(roomId); _servicePresetLists.Remove(roomId); _nativeCatalogSelections.Remove(roomId);
                PublishRoomCatalog(roomId, "service_presets", null);
            }
            Queue(delegate
            {
                try
                {
                    lock (_sync) { if (!ListenOpenCurrent(roomId, registration, gen, epoch, work)) return; }
                    string host, token; if (!GetConnection(out host, out token)) return;
                    string metadata = Request(host, token, "GET", "/api/v1/items/" + roomId + "/commands", null);
                    string path = RoomCatalogPath(roomId, "audio_stations"); bool allowed = false;
                    foreach (JToken link in (JArray)BuildRoomMediaPlan(metadata, host, roomId)["advertised_get_paths"]) if ((string)link == path) allowed = true;
                    if (!allowed) throw new InvalidDataException("Source catalog GET is not advertised");
                    string catalog = Request(host, token, "GET", path, null);
                    bool service = HasSourceOwnedMedia(catalog, sourceId);
                    // No saved items does not turn an advertised Spotify-style service
                    // into a physical source. Validate its existing preset contract too.
                    if (!service && sourceId <= UInt16.MaxValue)
                    {
                        string detail = Request(host, token, "GET", "/api/v1/items/" + sourceId, null);
                        service = IsTuneInSource(ServiceDetail(detail, (ushort)sourceId));
                        JObject mapping; ushort driver = ServiceDriverCandidate(detail, (ushort)sourceId, out mapping);
                        if (!service && driver > 0)
                        {
                            string driverDetail = Request(host, token, "GET", "/api/v1/items/" + driver, null);
                            if (!ServiceDriverIdentity(driverDetail, driver)) throw new InvalidDataException("Candidate is not a protocol driver");
                            string commands = Request(host, token, "GET", "/api/v1/items/" + driver + "/commands", null);
                            try { BuildServicePresetSelection(commands, roomId, driver, "00"); service = true; }
                            catch (InvalidDataException) { }
                        }
                    }
                    lock (_sync)
                    {
                        if (!ListenOpenCurrent(roomId, registration, gen, epoch, work)) return;
                        if (service)
                        {
                            if (sourceId > UInt16.MaxValue) throw new InvalidDataException("Service source ID exceeds interface range");
                            BrowseServicePresets(roomId, 0, (ushort)sourceId);
                            EmitMessage("Room " + roomId + " browsing service " + sourceId + "; select an item to start playback");
                        }
                        else SendAudioCommand(roomId, "SelectAudioSource", sourceId);
                    }
                }
                catch (Exception ex)
                {
                    lock (_sync) { if (!ListenOpenCurrent(roomId, registration, gen, epoch, work)) return; }
                    InvalidateRejectedToken(ex); EmitMessage("Listen source open failed: " + FailureKind(ex));
                }
                finally
                {
                    lock (_sync) { object current; if (_listenOpenWork.TryGetValue(roomId, out current) && Object.ReferenceEquals(current, work)) _listenOpenWork.Remove(roomId); }
                }
            });
        }
    }
}
