**CP15c update:** Explicit native item selection is implemented; see [ROOM_NATIVE_MEDIA_SELECTION.md](ROOM_NATIVE_MEDIA_SELECTION.md). Browse and paging remain read-only; playback awaits RMC4 confirmation.

# Native room media lists for HTML

Revision `Stage1-CP15b-NativeCatalogLists`. Adds read-only, named catalog pages to Sources. These are **native room catalogs**, not a proven Spotify-account library or service-specific artist/playlist tree. Media-item activation and child browsing are not implemented in this checkpoint.

## Confirmed REST inspection

Room13 inspection exported2026-11-16T16:17:50Z advertised five explicit GET paths. All returned HTTP200:

| Room media suffix | Entries |
| --- | ---: |
| albums/playlists | 2 |
| albums | 1 |
| broadcast_audio | 181 |
| movies | 0 |
| broadcast_video | 0 |

The initial report suppressed names and sampled numeric IDs. It establishes endpoint/list availability, not service ownership or playable IDs. New normalization reads only positive numeric mediaid/id and bounded name/title/label/mediaName strings. Other fields, account data, tokens, artwork and URLs are excluded. Labels are intentional catalog display data; they may appear in the local report. Render them as text, never unescaped HTML.

## Inputs

| Sources input | Action |
| --- | --- |
| Browse_Media_Playlists | Load the room's native playlist list |
| Browse_Media_Albums | Load albums |
| Browse_Media_Audio_Stations | Load broadcast-audio entries |
| Browse_Media_Movies | Load movies |
| Browse_Media_Video_Stations | Load broadcast-video entries |
| Media_List_Page_Set | Request one-based cached page; zero means1 |

Each Browse pulse rechecks the room's advertised GET paths through its command metadata, then fetches only the selected matching path. Paths are the exact observed routes; no arbitrary URI input exists. Maximum two GETs per Browse pulse, no POST or source/playback change. No startup catalog fetch or polling. The active list is replaced when switching kinds; a later request supersedes earlier work. Repeating Browse refreshes that list; page requests use cached items with no HTTP request.

## Outputs

| Signal | Meaning |
| --- | --- |
| Media_List_JSON$ | Current named media page |
| Media_Kind$ | playlists, albums, audio_stations, movies or video_stations |
| Media_Item_Count_FB | Number of normalized entries available for paging |
| Media_List_Page_FB / Media_List_Pages_FB | Current page / total pages |
| Media_List_Valid_FB | Loaded recognized catalog, including a valid empty list |
| Media_List_Partial_FB | Omitted/duplicate IDs, missing labels or truncation occurred |
| Content_Browse_Available_FB | Mirrors loaded native-list validity; initially0 until an explicit Browse succeeds |

Do not hide the initial Browse action merely because Content_Browse_Available_FB is0: this checkpoint discovers availability by that explicit request. This flag does not assert full service browsing or playback support. A valid empty catalog has count0, pages1 and items[]. Disconnect clears readiness and data. Reconnect requires a new explicit Browse for content; normal source/volume/selection feedback still synchronizes automatically.

Illustrative packet (labels are examples):

```json
{"room_id":13,"kind":"playlists","scope":"native_room_catalog_not_service_library","valid":true,"revision":"opaque-snapshot-id","page":1,"pages":1,"total":2,"partial":false,"items":[{"id":"1","name":"Playlist one"},{"id":"2","name":"Playlist two"}],"selection_supported":false}
```

Aggregate pages by room/kind/revision/page. A refresh changes revision; discard earlier pages even if total stays the same. IDs remain strings, preserving values beyond JavaScript's safe integer range. Treat a media ID together with its kind: playlist1 and album1 are different entries. **Do not send media IDs to Source_ID$**—that input selects room devices/services. Media selection remains disabled (`selection_supported=false`).

Pages keep items within3500 UTF-8 bytes and whole packets below4096 for bounded labels. Ensure your serial bridge preserves that size. The client requests additional pages using Media_List_Page_Set, confirms the returned page number and renders labels with textContent. Out-of-range pages clamp. At most2048 raw entries per response and1048576 response characters are processed. Missing labels use an explicit generic kind/ID label; partial feedback warns that labels or entries need review. Original source count, omissions and missing-label count are recorded in the exported normalized report.

## RMC4 test

1. Update Sources symbol, inspect changed wiring and compile/load for **4-Series only**. Parameters remain Gateway_ID=1 / Room_ID=13.
2. Connect and pulse Browse_Media_Playlists. Wait for the list-loaded diagnostic and Media_List_Valid_FB=1; expect the currently observed2 entries, subject to actual library changes. Inspect Media_List_JSON$ names/IDs and Media_List_Partial_FB.
3. Pulse Browse_Media_Audio_Stations. Expect the observed181 entries, subject to changes, with multiple pages as needed. Check item count and page outputs. No content should start playing.
4. Pulse gateway Export_Inventory and say exported. The existing collector receives the latest four sanitized native list snapshots under room_media_catalogs, allowing name/schema review without manual transfers. Reports are historical snapshots, not live readiness.

Local validation passed: labels/title fallback, duplicate/invalid IDs, U64 string IDs, privacy, valid empty/unknown schema handling, native read-only scope, widget revision/partial flags, lossless bounded paging, room/kind/stale-work rejection and disconnect cleanup. Existing Room/media/selected-source/event-driven/Transport/inventory checks passed. Release build passed with existing generated CS0162 warning; all nine wrappers compiled series4 with zero errors/warnings and signatures verified. New list output/name mapping remains RMC4-unverified.
