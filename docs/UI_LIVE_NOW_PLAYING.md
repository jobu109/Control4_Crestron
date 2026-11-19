**Spotify elapsed-time limitation:** User confirms native Control4 shows no advancing Spotify progress; the verified REST/WebSocket capture likewise contains track durations but no elapsed-time events. Spotify progress remains hidden. Pandora uses measured progress events. No local timer is synthesized; this finding is scoped to the verified Control4 integration.

**CP28a read-only progress:** User confirms Pandora ProgressChanged length/offset units are seconds. Added a compact `Now_Playing_Progress_JSON$` output, wired to panel serial input17. Update the Sources symbol to expose this pin, connect it, compile/load SIMPL, then rebuild/transfer Construct and hard-refresh. Existing joins remain unchanged. The same production UI and vanilla assets display elapsed/total time and a noninteractive progress bar. Each JSON packet is <=255 ASCII characters; it is not framed and needs no page request. Fields: room_id, source_id, track_revision, valid, position_seconds, duration_seconds. Metadata now also includes track_revision, which the UI uses to reject previous-track progress. Missing/invalid/range-exceeding/live-duration0 progress stays hidden. Source/track change, Room Off and disconnect clear progress. Identical samples are suppressed. Updates come only from measured WebSocket events, with no interpolation, polling or simulated elapsed timer. canSeek is intentionally not used to enable seeking. Pandora is the observed format; other service progress remains unverified unless it emits the same contract.

Test: play Pandora, verify elapsed/total against the current track, pause and ensure no simulated advance, resume and verify actual updates, Next and verify old progress clears, then Room Off. Also verify metadata and Play/Pause behavior remain correct. Physical-panel runtime remains a separate acceptance target.

**CP27a station/preset label:** The document includes station, populated only from the current Spotify preset header or unique Pandora Playing From section. The UI already displays this as Playing from. Missing/ambiguous labels remain empty without clearing a valid song. Initial room text can arrive before the queue label. Reload the updated SIMPL library/program; no UI rebuild or wiring changes. Verify HEAVY and the actual Pandora station label, Next preservation, and clearing on Room Off. CP26b reported-action behavior is user-confirmed.

**CP26b reported transport action:** The metadata document may include playback_action=play, pause or unknown. This describes the action reported by the service dashboard; it is not an exact paused/stopped state. Controlled Pandora captures show both Pause and Stop expose Play. The main UI button follows that reported action. Missing/ambiguous feedback leaves separate Play/Pause controls. The field uses the existing metadata connection; no new joins. Recompile/load SIMPL and rebuild/transfer Construct for this update; test Pause/Play/Stop and app-initiated changes. Until the first dashboard update after source activation, action remains unknown.

**CP25b Next-update correction:** A complete Pandora queue can be followed by an index-only QueueChanged notification. The library preserves valid metadata when that notification matches the last complete index. A changed index without a full list still clears validity, as do malformed/ambiguous full queues. Reload the CP25b SIMPL library/program and verify repeated Next changes text/artwork without blanking. Existing joins remain unchanged.

**CP25a Pandora extension:** The same metadata joins/UI now support the observed Pandora QueueChanged section format. Its index1 points to the station; the parser instead requires one explicit “On Now” header and exactly one following non-header row before the next header. Uses that row's title/artist/album/image, never Playing From or You Just Heard. Empty/ambiguous sections clear validity. English section labels are the captured contract; other/localized formats remain unavailable. Artwork uses the existing HTTPS URL validation. No new joins, UI rebuild, polling or command changes. Compile/load the synchronized library and test Pandora playback, Next text/artwork changes and Room Off. Runtime verification of this extension is pending.

# Live Control4 metadata — CP24a

The Sources module supplies current-track metadata to both the shared Construct pages and vanilla HTML5. The transport module is unchanged. Spotify is the first supported, captured schema; other services require equivalent verified events. This version has local build/browser verification; RMC4 delivery and actual touchpanel artwork retrieval remain to be tested.

## Install and wire

1. Save your current SIMPL program. The package and `C:\Users\Public\Documents\Crestron\SIMPL\Usrsplus` contain the matching library and Sources module. The previous installed files are backed up under `_baseline/installed_before_CP24a_LiveNowPlaying`.
2. Refresh/update **Control4 - Room Module - Sources** in SIMPL Windows so the five new pins appear. Keep Gateway_ID=1 and Room_ID=13. Verify existing wiring after updating the symbol; new scalar declarations must precede fixed arrays in SIMPL+.
3. Add the five connections below to the existing HTML5 panel/Web XPanel symbol. The direction is from the UI's perspective. Existing join assignments remain unchanged.

| Direction | Type / join | Sources module pin |
|---|---|---|
| UI → module | Analog 20 | Now_Playing_Page_Set |
| Module → UI | Analog 20 | Now_Playing_Page_FB |
| Module → UI | Analog 21 | Now_Playing_Pages_FB |
| Module → UI | Digital 99 | Now_Playing_Valid_FB |
| Module → UI | Serial 16 | Now_Playing_JSON$ |

4. Compile and load the SIMPL program for **4-Series only**. No account/token/certificate settings change.
5. Close the currently opened Construct solution, then open the workspace copy at `ui/construct/Floyd Room Media/Floyd.csln`. Its shared JavaScript/CSS and native pages have been regenerated. Build and transfer the new project using your existing Toolbox workflow. The existing `.ch5z` archive is not rebuilt automatically by the local generators.
6. Hard-refresh Web XPanel (Ctrl+Shift+R). For a physical panel, load the rebuilt project and verify it connects using its assigned device/IP ID. Use the same five connections on each client that should receive this room.
7. For vanilla HTML5, `ui/room-media/dist` contains the updated production build. Its join map matches the table. Deploy through your established CH5 packaging workflow; do not start an additional Web XPanel connection inside Construct.

## Verify

1. Select Michael's Spotify, then HEAVY from the UI. Confirm actual audio.
2. Confirm title, artist and album appear in the UI and match Control4. Artwork should appear when a valid Spotify queue event supplies its HTTPS image reference.
3. Press Next twice, waiting for each change. Confirm text and artwork change together without repeated Query/Browse pulses. If initial artwork is absent, check whether Next supplies it; that distinguishes an initial-event timing issue from image retrieval.
4. Browse another source's presets without starting one. The actual playing track should remain visible while browsing.
5. Press Room Off. Text/artwork should clear. Disconnect/reconnect the gateway and verify stale metadata clears, then returns from fresh source feedback/events.
6. Repeat on the physical panel if available. Browser tests used a mocked image response; they do not establish that the panel can reach Spotify's image host.

If it fails, report the Sources Now_Playing_Valid_FB state, whether Now_Playing_JSON$ carries stream `n` packets, and whether panel analog20 requests successive packets. The UI handles reassembly automatically; SIMPL Debugger may show only one <=255-character packet. Export_Inventory still supplies the existing diagnostic captures, not raw artwork URLs.

## Data and lifecycle

After reassembly, the snapshot contains `room_id`, `source_id`, `valid`, `title`, `artist`, `album` and `artwork`. Empty/invalid snapshots may contain only room identity and validity. Empty artwork means unavailable, independently of text validity. It is not a playback-state, position, progress or station-name feed.

Observed Spotify events use `OnDataToUI → EVENT.NAME=QueueChanged → ARGS.NowPlayingIndex` and `ARGS.List.item`. The index includes the header row: the capture moves from1 (Machine Head / Circle The Drain) to2 (Okilly Dokilly / White Wine Spritzer). The parser checks bounds, rejects header rows and conflicting/multiple explicit current markers, and never chooses an arbitrary queue title. The event's source must match measured room audio-source feedback before publication.

Initial text is read once on measured source activation from the room's `CURRENT MEDIA INFO` variable, accepting `value.mediainfo` only when `medSrcDev` matches that source. A newer queue event supersedes an in-flight initial read. A bounded, short-lived queue cache handles events arriving just before measured source activation. No periodic polling or new playback commands are added.

Artwork comes from the selected row's `album_cover_url`. The observed host is `i.scdn.co`. Production accepts bounded HTTPS DNS URLs without credentials, queries or fragments; unsupported references stay blank. The room's `img` format is still unrecognized and is not converted into a guessed URL. The browser renders an image element with no referrer and clears its old source when artwork is missing; fetch errors leave the placeholder visible. No Director token is delivered to the UI.

Metadata uses framed stream `n` and the existing <=255-character ASCII envelope format. The UI requests missing packets using analog20, rejects other-room/retired snapshots and waits for the complete document before replacing text/artwork. Source invalidation, Room Off, disconnect and incomplete queue changes clear validity. Source-list browsing alone does not assert playback or replace actual metadata.
