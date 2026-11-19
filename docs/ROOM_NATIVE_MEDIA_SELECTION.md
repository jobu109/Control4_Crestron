# Native room media selection

Revision `Stage1-CP15c-NativeMediaSelection`. Browse and paging remain read-only. Explicit selection can start room playback. Native catalogs are not established Spotify-account libraries.

## RMC4 test

Update the Sources symbol and load the matching library. Pulse Browse_Media_Playlists and wait for Media_Select_Available_FB=1. Set Media_ID$ to `1`, then pulse Select_Media_ID_btn. Confirm whether **Blues** actually plays. The exported Room13 list contained `1` = Blues and `2` = Blues Win!!!. Command acceptance alone does not establish playback.

## Selection interface

| Signal | Meaning |
| --- | --- |
| Media_ID$ + Select_Media_ID_btn | Select a media ID from the currently loaded list |
| Media_Item_Select | Select on analog change using the one-based position in the whole catalog |
| Media_Select_Request$ | Select on serial change using the request below |
| Media_Select_Available_FB | Current nonempty list has eligible command metadata |

Widget request using the exact revision from Media_List_JSON$:

```json
{"room_id":13,"kind":"playlists","revision":"COPY_CURRENT_LIST_REVISION","id":"1"}
```

ID is a string representing native media, distinct from a source proxy ID. Add a changing `request_id` field to repeat an identical serial selection, or use ID plus digital pulse.

Selection requires the current room, kind, revision and item membership. Refresh/switch invalidates older requests. Disconnect clears readiness; offline selections are not queued or replayed. Browsing and paging never select automatically.

Before posting, the gateway reads fresh command metadata and requires the exact matching SELECT_AUDIO_MEDIA or SELECT_VIDEO_MEDIA command with its mediaid list parameter. Unknown additional parameters, truncated metadata and explicitly required deselect parameters disable selection. Optional deselect is omitted. Requests send mediaid and async=true; actual behavior needs the first RMC4 playback test.

Queued older selections are superseded; already transmitted requests can complete. Acceptance triggers measured room-state readback, without optimistic playback feedback. Child browsing and service-specific libraries remain unfinished.

## Verification

Release build and selection/catalog regression checks passed. All nine SIMPL+ modules compiled with `/target series4`, zero errors/warnings. Sources has158 verified signature cues. Installed Usrsplus USP/USH/CLZ hashes match the package; embedded CLZ DLL matches Release.

Installed backup: `_baseline/installed_before_native_media_selection_20261116_104456`.
CLZ SHA256: `D239F51CDDF71DA06BDB17B1D03924F454B2EF8B43D6E1E63D058E8009B8E4AB`.

Playback remains unverified on RMC4. Latest normalized export contains two playlists and no audio-station catalog result.
