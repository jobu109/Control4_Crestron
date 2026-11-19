**Current interface work:** [Touchpanel/Web XPanel architecture and metadata capture](ROOM_MEDIA_INTERFACE.md). TuneIn is on hold. Existing Spotify/Pandora browsing and playback remain the baseline; live now-playing metadata is being investigated.

**CP18b automatic service browsing:** Actual audio-source changes now trigger mapping and preset-list loading; repeated feedback does not poll or autoplay. Spotify event-trigger test and Pandora capability validation are pending. [Current behavior](ROOM_SERVICE_PRESETS.md).

**Dynamic service presets:** [ROOM_SERVICE_PRESETS.md](ROOM_SERVICE_PRESETS.md) documents named Spotify saved-preset discovery and cached selection using the verified driver command. Initial driver/source configuration remains required; end-to-end discovery testing is pending.

**Current service priority:** [Spotify saved presets](SPOTIFY_SERVICE_PRESETS.md). User confirmed the Control4 Spotify screen exposes presets. Native room catalogs below are separate; their playback test is deferred.

**CP15c update:** Explicit native item selection is implemented; see [ROOM_NATIVE_MEDIA_SELECTION.md](ROOM_NATIVE_MEDIA_SELECTION.md). Browse and paging remain read-only; playback awaits RMC4 confirmation.

**Native media list update:** [ROOM_NATIVE_MEDIA_LISTS.md](ROOM_NATIVE_MEDIA_LISTS.md) documents read-only named catalog pages confirmed by the room's advertised REST routes. Media-item activation and service-account library browsing remain separate.

**Current module split:** [Room Sources / Transport](ROOM_SOURCES_TRANSPORT.md). Source lists and selection stay in Sources; remote controls are in Transport.

**Current extension:** [Numbered source slots](ROOM_SOURCE_SLOTS.md) adds digital/analog/name selection, per-slot availability/name outputs and editable compile-time capacities (15 each). The JSON interface below remains available.

# Room source lists for an HTML widget

Revision `Stage1-CP11b-RoomWidgetLists`. Update the Room symbol and compile/load the matching library for RMC4, **4-Series only**. Parameters remain Gateway_ID and Room_ID. Input analog-to-serial separator was added before Source_ID$; recheck wiring after symbol update. All USP sources retain CRLF and generated signature cues were checked.

## Outputs

| Signal | Meaning |
| --- | --- |
| Audio_Source_Count_FB | Total discovered Listen entries for the room |
| Video_Source_Count_FB | Total discovered Watch entries for the room |
| Audio_Source_List_JSON$ | Current page of named audio sources |
| Video_Source_List_JSON$ | Current page of named video sources |
| Audio_List_Page_FB / Video_List_Page_FB | Current page, starting at 1 |
| Audio_List_Pages_FB / Video_List_Pages_FB | Total pages for each list |
| Sources_Valid_FB | Discovery validity; clear the widget when low |

## Building a dynamic widget list

The HTML widget can resize at runtime to the full discovered source list. It is **not limited by VideoSources/AudioSources or the 15 numbered slots**. Those compile-time constants only size the individual SIMPL selection/availability/name pins. The JSON outputs, total counts and direct-ID selection use the full room source lists. No additional SIMPL pins or recompilation are needed when more sources are discovered through this interface.

1. Subscribe to Sources_Valid_FB, both source-count outputs and both JSON outputs through your existing HTML-to-SIMPL signal bridge. Maintain separate Audio and Video lists for each room.
2. After Connect/discovery, accept a JSON packet only for the intended room and mode and with valid=true. Parse it with JSON.parse. When validity goes low, clear/disable the displayed lists and discard cached pages.
3. Cache each packet's items by page number. If pages is greater than1, request missing pages sequentially through Audio_List_Page_Set or Video_List_Page_Set. Confirm each response using the JSON page field; count/page feedback updates alone are not a complete list payload. Avoid repeatedly writing an unchanged analog page value, which does not trigger a CHANGE event.
4. Once all pages are present, concatenate their items in page order and verify the resulting length equals total. The count outputs are total source counts, not the number of rows in the current packet. Create one widget row per item; store its id as a string and display its name as text. A valid total0 produces an empty list.
5. On a clicked row, send its ID through Source_ID$ and then pulse the matching Audio/Video ID-selection input as described below. Use the ID, not the row index or the 15-slot selection inputs. This also selects sources beyond slot15.
6. On an explicit Discover_Sources refresh, start a fresh page collection rather than merging new discovery with old pages. Fetch all pages again even if the total count is unchanged: source names/order can change without a count change. Serialize refresh/page requests through your bridge; the current packet format has no discovery revision token.

Your widget supplies the rendering and signal-bridge bindings; this package supplies the counts, JSON and selection inputs. The module does not generate HTML or add runtime SIMPL signal pins. Available-source rows are not actual active-source feedback; selected-index validity is separate; [measured-source feedback](ROOM_MEASURED_SOURCE_FEEDBACK.md) now implements it, pending RMC4 verification.

Example audio packet (illustrative names/count):

```json
{"room_id":13,"mode":"listen","valid":true,"page":1,"pages":1,"total":2,"items":[{"id":"65","name":"Spotify"},{"id":"100002","name":"Digital Media"}]}
```

Parse the JSON and use items for the widget. IDs are strings, including large IDs. Names are JSON-escaped; render them with textContent or the widget's text API rather than inserting unescaped HTML. Each packet identifies its room and list, validity, page and total. Do not use item position as a Control4 ID. Source lists describe available source selection, not currently selected source or available playlists.

Payloads are paged automatically: the items array stays within3500 UTF-8 bytes, leaving envelope space below4096 bytes with the bounded discovery names. Make sure the panel's serial transport preserves that size. No entries are silently dropped. Write Audio_List_Page_Set or Video_List_Page_Set to request another page; zero selects page1 and values above the last page clamp to the last page. Consume the corresponding page feedback/JSON packet to confirm the requested page arrived. Aggregate all pages by room/mode/page if the widget needs the entire list; discard the old aggregate when discovery changes or validity clears. A valid empty list has total0, items[] and one empty page. Invalid discovery/disconnect sends valid=false, empty items, and count0.

## Selection from the widget

1. When the user clicks an audio item, write its id string to Source_ID$.
2. After setting that value, pulse Select_Audio_ID_btn. For a video item pulse Select_Video_ID_btn.
3. The module validates the ID against the appropriate current room list, then sends the existing verified room source command. Other-room IDs, wrong-mode IDs and malformed values are rejected without a POST. Merely changing Source_ID$ sends nothing.

Keep the string update and pulse in order through the panel/SIMPL bridge. Next_Source, Previous_Source and Select_Listed_Source were removed in the Sources/Transport split; direct widget selection replaces that cursor interface. Selecting a source can turn the room on; it does not choose content or guarantee playback. No new content-browsing or transport capability is claimed.

After load, Connect and wait for discovery (or pulse Discover_Sources). Check total counts and parse each JSON output. Test choosing Spotify via audio ID65 and DIRECTV Stream via video ID2061 in Living Room. Confirm each routes correctly, then test wrong-list rejection, paging and reconnect clearing. A second room must use its own discovered list; no fixed source mapping is imposed.

Local checks passed for JSON list contract, ID100002, direct-ID membership/mode validation, escaping and lossless bounded paging, page clamping and disconnect invalidation, alongside Room/setup/Audio/Video/socket regressions. Release build passed with existing generated CS0162 warning. All eight modules compile series4 with zero errors/warnings; signature checks verify all51 Room cues. Package and installed USP/USH/CLZ hashes match. New widget outputs and direct-ID selection remain RMC4-unverified.




