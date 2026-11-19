**CP24a live metadata update:** Existing playback is verified. The updated Sources module/shared UI add title, artist, album and Spotify artwork. Follow [the five new connections and reload procedure](UI_LIVE_NOW_PLAYING.md); this new metadata delivery still needs RMC4 verification.

# Room media interface: start here

Prepared 2026-11-17 for the RMC4 package. TuneIn remains on hold.

## Open the mockup

Double-click `ui/room-media/index.html`. It works offline. Select Spotify or Pandora, search stations, change pages, select a station, try Watch, volume, mute and Room Off. These controls simulate a UI; they do not contact Crestron or Control4.

Screenshots: [landscape](../ui/room-media/previews/room-media-desktop.png), [portrait](../ui/room-media/previews/room-media-portrait.png). The preview contains the five exported Spotify presets and 162 Pandora stations. The room dropdown is a one-room layout placeholder, not a completed room-switching implementation. Watch now contains all50 SR-260 keys in grouped, scrollable sections; availability is metadata-gated in live mode.

## Choose a build path

| Path | What you build | Appropriate when |
|---|---|---|
| [Construct](UI_BUILD_CONSTRUCT.md) | A Construct solution with a custom HTML media area, optionally native surrounding pages and controls | You want Construct to own the project, deployment and other house pages |
| [Vanilla HTML5](UI_BUILD_VANILLA.md) | The supplied HTML/CSS/JavaScript application using CrComLib and Web XPanel | You want full control of dynamic lists and layout without a UI framework |

Both use the same existing Sources and Transport modules through typed Crestron joins. The browser never authenticates directly to Control4. A native Construct-only list requires an additional row-binding adapter; raw JSON does not automatically populate a Widget List.

Start with [shared SIMPL wiring](UI_JOIN_MAP.md), then follow your chosen build guide. Join numbers are examples, not assignments already made in your program.

## What is established

User-verified Web XPanel behavior includes Listen/Watch source selection, Spotify/Pandora saved-item selection, volume/mute/Room Off, live title/artist/album/artwork, track changes and visible pressed-button feedback. Pandora's full queue followed by its same-index notification is handled without clearing the new song. The existing join map has140 scalar bindings; metadata uses serial16, digital99 and analog20/21. See [UI_LIVE_NOW_PLAYING.md](UI_LIVE_NOW_PLAYING.md).

The offline mockup uses sample data. The shared Construct runtime and vanilla live build consume actual module feedback. A physical touchpanel remains a separate acceptance target; Web XPanel confirmation does not establish its image/network behavior.

## Remaining production work

1. Reported Play/Pause action feedback is implemented and user-confirmed (CP26b). Exact Paused vs Stopped remains unknown; do not assert it from an available Play control.
2. Pandora seconds-based read-only progress is implemented and user reports it appears. Spotify has no elapsed updates in the verified feed or native Control4 app, so its bar stays hidden. Seeking remains unimplemented; do not simulate progress.
3. Playing station/preset labels are implemented and user-confirmed (CP27a).
4. Verify physical-panel operation and multi-room/multi-client isolation with real module instances and independent client configuration.
5. Continue service-specific support (TuneIn remains on hold) using advertised commands and observed formats.

## Local validation

The package includes catalog tests for page assembly, room isolation, invalidation, duplicate IDs and malformed IDs, connection/selection/capability gating checks, plus Edge preview interaction checks and screenshots. `npm run build` builds the vanilla live entry; `npm run build:construct` builds the custom-area JavaScript. Crestron dependencies emit bundle-size/eval warnings; no dependency source was modified. These are starter artifacts, not a Construct-compiled project or hardware acceptance result. No SIMPL+/C# changes or processor loads are required merely to view the mockup.

Local results: catalog/bridge tests passed; Edge preview interactions passed; vanilla build, Construct custom-area bundle and RoomMedia.ch5z creation passed. Built live page loaded with no JavaScript page errors in Edge while disabled; Construct bundle exposed its mount entry. This smoke check did not connect to a processor.
