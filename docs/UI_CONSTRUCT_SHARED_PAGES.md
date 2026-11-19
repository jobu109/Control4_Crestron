**Pressed feedback update:** Buttons brighten with a mint outline while held and show a short200ms highlight after activation. This is local press feedback and does not assert controller acceptance. Rebuild/transfer the Construct UI; no joins change.

**Layout update:** Now Playing is wider, artwork is taller and the footer has Volume Down/Up beside the slider. These use existing Transport digital41/40 and capability gating; no new joins. Reopen the workspace solution, build and transfer the UI to apply.

**255-character transport fix CP21a:** The browser received truncated source JSON. Shared UI now decodes bounded packets and compact Transport keys. No joins change. Updated C# library and UI must both be loaded; see [UI_SERIAL_TRANSPORT.md](UI_SERIAL_TRANSPORT.md) for the installation/test sequence. Editor canvas remains static; do not assess source loading there.

**Live mode enabled:** User confirms SIMPL wiring/program load. The shared source now uses enabled:true. Reopen/build/transfer the Construct project to update the deployed UI. The editor canvas remains a static preview; the runtime reads real joins and enables controls according to connection/validity. Initial live acceptance checks are Room_Name$ (serial7), Volume_FB (analog1) with Volume_Valid_FB (digital17), and Mute_FB (digital16) with Mute_Valid_FB from the current CSV. CP24a adds live metadata connections; see [UI_LIVE_NOW_PLAYING.md](UI_LIVE_NOW_PLAYING.md). Earlier preview-mode notes below describe prior versions.

**Current correction:** Room Off uses only Sources digital5; Transport's duplicate endpoint is intentionally unwired. Watch moves to30, and every digital assignment formerly30 or higher increases by1. Digital19/20 are unassigned. Highest joins are now Digital98 / Analog19 / Serial15; analog/serial assignments do not change. See the updated UI_JOIN_MAP.md, UI_TRANSPORT_JOINS.csv and UI_ALL_JOINS.csv. Both Room Off buttons dispatch5. Current mapping has134 bindings and11 intentionally module-only declarations. Reopen/build/transfer to apply this source update. Earlier dated summaries below describe prior versions.

**Complete room interface joins:** All remaining scalar Sources/Transport controls and feedback now have assignments (135 bindings including the existing gateway connection). See UI_JOIN_MAP.md and UI_ALL_JOINS.csv. Highest typed joins: Digital97 / Analog19 / Serial15. The bridge consumes source-selection index/validity and volume/mute validity; it exposes other scalar feedback through moduleFeedback for widgets/navigation. Array slot pins and four private manual-selector pins remain explicitly module-only. Existing assignments and preview mode are preserved. Native source/assets are regenerated; reopen/build/transfer is required to apply them.

**Complete SR-260 UI mapping:** All50 existing Transport keys now have joins and grouped buttons on Watch. Existing command joins are preserved; new commands use19,20,30–63. Digital feedback7–10,16–18 remains separate. The remote area scrolls within1280×800. Watch/Listen/List accept local_ui capability and remain local actions; all other keys require advertised metadata. See UI_JOIN_MAP.md and UI_TRANSPORT_JOINS.csv. Rebuild/transfer the updated Construct source to apply these changes.

**Current join policy:** Unrelated functions have separate input/output joins within each type. See UI_JOIN_MAP.md for the current tables. Only matching Volume_Set/Volume_FB share analog1. Digital feedback now uses7–10 and16–18; serial feedback uses3–8. The maximum UI join remains29. Regenerated source must be rebuilt/transferred before these assignments are active.

**Compact UI joins:** Configuration and UI_JOIN_MAP.md now use joins1–29 instead of1001–1029. Gateway Connected_FB uses digital feedback7 (old1000 cannot map to zero). Regenerated shared assets and native Div contents are updated; the currently built/deployed archive remains on its previous configuration until you reopen/build/transfer this source. Control4 IDs, IP ID and offline/live mode have not changed.

**Actual Construct build inspected:** The user successfully opened/built the native solution. `output/FloydControl4.ch5z` contains the shared files at `app/project/assets/css/room-shared.css` and `app/project/assets/javascript/room-shared.js`. Its index already loads the shared implementation in the parent document; each Div is a srcdoc iframe. The loader is now corrected to prefer those actual paths and reuse the host runtime across frames. Reopen/rebuild the updated source to include this correction; the existing archive was inspected but not modified. Browser tests against an extracted test copy verify the actual asset path layout, one host bridge across two Div frames, synchronized room selection and iframe-removal cleanup. Live Crestron communication/deployment remain pending.

**Construct editor asset warning corrected:** The installed editor renders scripted Divs in a `*_iframe` with `pointer-events:none`. The page loader now recognizes that editor-only container, keeps the visible static layout and labels it “Layout preview · Construct editor” without requesting runtime assets. In an ordinary runtime frame it still loads shared CSS/JavaScript and still reports genuine missing-asset failures. Tests simulate both paths; they do not establish the generated BuildOutput asset URLs. Close stale buffers without saving, reopen the solution, then separately Build/test the runtime project.

**1280×800 fit update:** The native Div, static canvas layout and initialized runtime now use the panel height without a whole-page scrollbar. Header, paging, transport, volume, mute and Room Off fit inside the viewport; only content regions can scroll. `construct-fit.css` is the source for these sizing rules and is included in both the shared stylesheet and the static baseline. Browser tests check all three pages with JavaScript enabled/disabled at exactly1280×800, including control bounds. Close stale Construct pages without saving and reopen the updated solution to load this change.

**Blank-page correction:** The earlier generated native pages omitted Construct's `[[Elements]]` records. They are now corrected: each page has one `html-div` element with its complete `customHtmlContent` property and 1280×800 CSS bounds, matching the installed editor component definition. Close any stale editor copy without saving, then reopen the solution below. Native component fields and embedded layout rendering are checked; actual Construct opening/building remains unverified.

# Shared Construct room pages

This is the current implementation direction. The original dark/teal theme is retained. CSS/JavaScript live in shared assets; each page contains visible HTML and a small common asset loader. No npm is needed to use the prepared files.

## Open the prepared solution

1. Close the previous copy in Construct before reopening, so an old editor buffer does not overwrite the updated files.
2. Open `ui/construct/Floyd Room Media/Floyd.csln` from this package. The original Documents/Floyd solution is unchanged.
3. Under **Floyd Control 4**, open:
   - **Room Module**: Listen source and station browser, plus room controls.
   - **WatchRemot**: Watch source/navigation screen (existing spelling retained).
   - **RoomSelect**: configured room selection.
4. Each canvas has static HTML with inline baseline styling. It remains visible even when JavaScript does not execute. Static artwork and metadata are labeled samples.
5. Build the solution in Construct. This has not been performed by the assistant. Native file section/reference checks and browser tests do not prove editor serialization or a successful Construct build.
6. In runtime preview, the shared assets initialize interactive offline mode. The files deliberately keep `enabled:false`; they do not send joins in preview mode. Merely opening the canvas does not initialize live control.

A separate [browser preview](../ui/room-media/construct-pages/index.html) includes Listen/Watch/Rooms navigation so you can check the shared runtime without native page-transition wiring.

## Files in the prepared project

| File | Purpose |
|---|---|
| assets/css/room-shared.css | Shared dark/teal stylesheet |
| assets/javascript/room-shared.js | Shared configuration, catalog collector, bridge, UI, sample data and page lifecycle |
| Room Module.cuig | Native Listen page with visible fallback HTML |
| WatchRemot.cuig | Native Watch page with visible fallback HTML |
| RoomSelect.cuig | Native configured-room page |

The shared assets also exist at the solution root for output-path compatibility. Old imported assets remain in the copied project, but these pages do not execute their individual scripts as well as the shared script. Do not add a second copy of CrComLib or initialize another Web XPanel connection.

The page loader first tries `app/project/assets/css/room-shared.css` and `app/project/assets/javascript/room-shared.js`, as verified in the actual archive, then older standalone-preview paths. When the parent already has RoomMediaPages, the frame loads only its CSS and registers its root with the host runtime. Construct's generated output paths must still be verified after Build; if neither convention matches, edit the loader's `paths` values to the actual paths. Failure leaves visible fallback HTML and an explicit status. `IncludeUnusedAsset=True` is set in the project copy to retain dynamically referenced assets; confirm inclusion in BuildOutput.

Crestron documents that non-image assets are available in BuildOutput, but it does not imply that adding an asset automatically executes it. See [Construct assets/build instructions](https://help.crestron.com/construct/Content/Topics/UI%20Editor/Create%20A%20Solution.htm). The small page loader performs that initialization explicitly.

## Lifecycle and efficiency

Within one browser document, all loaded room pages use one shared script/style load promise. Live Listen/Watch views share one bridge for the selected room and receive cached current feedback when attached. Adding a page or calling start repeatedly does not create another room subscription set.

Removing the final media view unsubscribes the bridge and releases any active digital pulse. Cached hidden pages keep their view and local search/page position while attached. A room change destroys old views, clears their catalog state and mounts the new room's join map. A DOM observer handles insertion/removal, rather than a polling timer. A bounded local initialization retry can wait briefly for CrComLib; it does not query Control4.

The inspected Construct build hosts the Divs in separate srcdoc frames. These frames now reuse the same-origin host runtime and subscriptions when available; standalone frames can initialize an independent runtime as a fallback. The bridge finds CrComLib in the current, parent or top window where accessible. Verify the actual page lifecycle on your panel/Web XPanel before calling it commissioned.

The preview's Watch/Listen buttons can change the current view locally. They are not native Construct page flips. Use Construct's own page navigation/visibility configuration to move between the actual pages. The RoomSelect page changes the active room context in its document; it does not automatically open a different native page. Same-origin Div frames using the host runtime share room selection. Cross-origin or independent host documents still need an explicit synchronization contract. This distinction must be tested in the actual Construct runtime.

## Live setup

1. Complete the existing [join map](UI_JOIN_MAP.md), using one Sources/Transport pair per controlled room and one shared gateway.
2. Edit `ui/room-media/join-map.js` to match the actual SIMPL panel joins. Set enabled true only after wiring is ready. Keep actual module Room_ID parameters fixed.
3. `ui/room-media/rooms.js` defines the room manifest. It currently contains only real Room13, Living Room. Add a room only after creating its module pair and assigning a separate send/receive join map. Do not reuse Room13 joins for a different room.
4. Reassemble the shared plain files with the included helper, then reopen/build the prepared Construct solution:

```powershell
python -X utf8 ui/room-media/tools/build-construct-pages.py
```

This is a convenience concatenation/copy step, not a Node/npm build requirement. Alternatively, edit the generated project `room-shared.js` asset directly, but keep its root/asset copies synchronized and remember a later helper run replaces generated edits. The script also regenerates the native page fragments; keep hand-edited layouts in source/templates or back them up first.

5. Review the generated `room-shared.js` configuration at its beginning. No Control4 password or Director token belongs in these assets.
6. Build and verify asset paths, client connection, Sources validity, source lists, station lists and transport capability feedback before testing commands.
7. Run the acceptance sequence in UI_JOIN_MAP.md on one client, then test reconnect and another client. Browser mock tests do not establish actual RMC4/panel communication.

## Current limits

- The configured room manifest has one room; room switching/isolation is tested using a synthetic second room in browser tests, not another live Control4 room.
- Live metadata normalization/output pins, artwork and playback timing remain unfinished. Live mode shows waiting metadata; offline mode labels captured samples.
- The Watch page includes all50 keys; actual runtime availability comes from room capability metadata. Commands use momentary pulses; hold-to-repeat is not implemented.
- Source-list generation handling, same-value analog page request refresh and timeout/retry remain starter limitations documented in UI_BUILD_START_HERE.md.
- Native page transitions and real host/iframe lifecycle still need runtime verification; cross-frame room synchronization is implemented and browser-tested through the shared host.
- No C#/SIMPL+ changes, native Construct build, UI deployment or processor load occurred for this work.

## Local checks

From `ui/room-media`, `npm run test:pages` runs browser checks with an installed Edge. Tests cover offline page views, one script/style load, one shared live bridge, repeated start, removal/remount, room-change subscription replacement, wrong-room packet isolation and final cleanup. Existing catalog, bridge and offline UI tests also pass. npm is used for these developer tests, not for pasting/using the prepared Construct pages.
