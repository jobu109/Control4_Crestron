**Current ready-to-open multi-page version:** follow [Shared Construct room pages](UI_CONSTRUCT_SHARED_PAGES.md). The workspace Floyd solution now contains visible Listen, Watch and RoomSelect pages with shared CSS/JavaScript. The earlier single-file instructions below remain reference material.

## Multi-page direction (user clarification)

The finished interface will have multiple pages. Prefer shared CSS/JavaScript assets plus visible per-page Div HTML, rather than pasting the complete application into each page. No npm step is required for plain shared scripts.

- Preserve the camera example's CSS/markup/initialization organization, the current dark/teal color scheme and parent/window CrComLib lookup.
- Load the shared implementation once per actual browser document/runtime. Pages in separate iframe documents need their own initialization; browser asset caching does not share JavaScript instances between documents.
- Use a common join/room configuration and a bridge with explicit mount/destroy lifecycle. Cached hidden pages should not resubscribe on every visit; destroyed pages must unsubscribe and release active button pulses.
- Keep service catalog state associated with room/source/revision. Selecting a room changes subscriptions/configuration, not SIMPL module parameters.
- Show visible HTML before runtime initialization. Shared script loading failure should leave an explicit status, rather than an empty root.
- The single-file [construct-room-div.html](../ui/room-media/construct-room-div.html) and current workspace native page are preview/reference artifacts. Do not duplicate their complete inline scripts across the finished project's pages.

This direction does not claim a completed multi-page controller; page lifecycle and asset loading must be verified in Construct.

**Native solution copy:** Open [Floyd.csln](../ui/construct/Floyd%20Room%20Media/Floyd.csln), then **Floyd Control 4 → Room Module**. This copy uses the user's saved native project format and includes the self-contained visual layout in its start page. Original Documents files are unchanged. Project references/page section structure were checked; actual Construct open/build remains unverified. Shared source assets have now been restored from the user's Floyd folder.

**For a visible Construct canvas layout:** use [construct-div-ready.html](../ui/room-media/construct-div-ready.html). Paste its complete contents into the Div Custom HTML property and size the Div around1280×820. All markup and styles are inline; it requires no JavaScript or external assets. This is a visual preview with disabled controls, not a connected application or native Construct solution. The older asset-based snippet starts empty and requires runtime initialization; do not use it to assess editor canvas appearance. The shared source assets were subsequently restored from the user-provided Floyd folder; live asset paths and initialization still require commissioning.

# Build the room interface in Crestron Construct

This is a step-by-step implementation guide, not a supplied `.c3s` solution or a claim of a successful Construct build. The supplied custom-area bundle is built locally; embedding and device behavior must be checked in your installed Construct version.

The recommended first implementation is a Construct project with one custom HTML media area. It reuses the exact vanilla station browser, source commands and room controls. You can then replace the surrounding controls with native Construct components without rewriting catalog logic. A fully native Widget List requires an extra binding adapter described below.

## 1. Prepare the custom Div files — no npm required

Construct can embed HTML, CSS and JavaScript directly in a Div's Custom HTML property. No Node.js, npm or separate JavaScript build is required for that approach. Construct still builds/packages its own solution as usual.

1. Use these files from `ui/room-media`: `styles.css`, `join-map.js`, `catalog.js`, `crestron-bridge.js`, `app.js` and `construct-plain.js`.
2. Add the CSS/JavaScript as project assets and reference them from the custom Div, or inline their contents in the corresponding `<style>` and `<script>` blocks. Load JavaScript in the order listed above, starting with join-map.js. Keep one copy of each script in the project.
3. Review `join-map.js` when you are ready to operate the system. Its joins must match the actual SIMPL wiring. Set the correct roomId and enabled flag. This configuration connects the controls to the processor; it is not a requirement merely to draw a Div or review the offline mockup.
4. Follow section 3 to add the root and mount the interface. Construct supplies the communication library and connection; do not initialize another Web XPanel inside the Div.

The original npm steps were development/package-maintenance steps for producing one combined JavaScript asset. They are optional for Construct:

- `npm ci`: installs the starter's development tools.
- `npm test`: runs local JavaScript checks.
- `npm run build:construct`: combines the shared scripts into `construct-assets/construct-room.js`.

If using that optional combined file, load it instead of the five separate JavaScript files. Do not load both. With the separate-script approach, editing join-map.js does not require an npm rebuild. With the combined approach, rebuild after changing source files. Neither approach replaces Construct's own solution build.

## 2. Create the Construct solution

1. Create a new Construct solution in a separate UI project folder. Select the installed CH5 version supported by your target panel. Use the target panel's real resolution; use 1280×800 as a starting landscape preview only if appropriate.
2. Select a dark theme. Set the page background near `#101416`; use teal `#68d6be` for active actions, text near `#f4f5f6` and muted text near `#99a6ab`.
3. Add a page named `RoomMedia` and make it the start page. Later add `RoomSelect` and `WatchRemote` as separate pages rather than nesting many widgets.
4. Open Join Manager and reserve the reviewed digital, analog and serial ranges. Keep UI send and receive directions clear. Do not assume a module pin's ordinal is its join number.
5. Add the separate JavaScript files from section 1 and `styles.css` to project assets (or use the optional combined bundle). Build once and inspect the actual BuildOutput paths before writing asset references. Construct makes assets available in output; adding a non-image asset alone does not prove it is being executed. See Crestron's [solution/assets/build guide](https://help.crestron.com/construct/Content/Topics/UI%20Editor/Create%20A%20Solution.htm).

## 3. Add the shared HTML media area

Complete copy/paste snippet: [construct-div.html](../ui/room-media/construct-div.html). It includes the root, styles, ordered asset references and mounting code; it does not require construct-plain.js. Replace the example asset paths with your actual Construct output paths. This is an asset-based snippet, not a self-contained copy of all application code.

1. Put a Div on RoomMedia sized to the available page. Open its Custom HTML editor. This component permits custom HTML/CSS/JavaScript; syntax and runtime behavior remain your responsibility. See [Construct Div](https://help.crestron.com/construct/Content/Topics/UI%20Editor/Ch5%20Components/Div.htm).
2. Add a dedicated root:

```html
<div id="room-media-construct"></div>
```

3. Load `styles.css` and the separate JavaScript files from their actual packaged asset locations. Use project-relative paths that exist in BuildOutput, not `C:\...` paths. For example, **if** your output places them under `assets/room-media/`, the references are:

```html
<link rel="stylesheet" href="assets/room-media/styles.css">
<script src="assets/room-media/join-map.js"></script>
<script src="assets/room-media/catalog.js"></script>
<script src="assets/room-media/crestron-bridge.js"></script>
<script src="assets/room-media/app.js"></script>
<script src="assets/room-media/construct-plain.js"></script>
```

These paths are examples; verify the generated project. Ensure the scripts load once in order, after Construct's CrComLib is available. Do not add `live-main.js`, `live.html` or `preview-data.js` to the live Construct page.

4. In your page's custom JavaScript initialization, after the root and scripts exist, mount the area:

```javascript
const root = document.getElementById('room-media-construct');
window.roomMediaView = window.RoomMediaConstruct.mount(root);
```

5. Verify in the browser console that `window.CrComLib`, `window.RoomMediaConstruct` and the root exist before mounting. If your Construct version inserts custom HTML without executing script tags, load the scripts through its project custom-JavaScript initialization and call mount after page insertion. Do not duplicate subscriptions while retrying. This lifecycle integration needs verification in the actual Construct project.
6. Before destroying/replacing the page, call `window.roomMediaView?.destroy()` and clear the reference. For a page that only hides, keep one mounted view; do not mount again each time it becomes visible.
7. The supplied `.rm` container uses viewport height. When embedded inside a native header/footer, add a project style such as `#room-media-construct.rm { min-height:0; height:100%; overflow:auto; }` and give the parent a real height. Check clipping and touch scrolling at the actual panel size.
8. Build and open preview. Live mode starts empty until valid processor feedback arrives. A static preview without a processor cannot demonstrate station discovery. Use the separate offline mockup to evaluate appearance.

## 4. Make native Construct controls where desired

The first custom area already includes these controls. When replacing any with native components, remove/hide its duplicate in the custom area and keep one owner for each action.

1. Add a native room heading bound to Room_Name$ through the reviewed serial receive join.
2. Add Listen and Watch page/tab buttons. These change the UI view. They do not select an arbitrary source or start playback.
3. For the first fixed source slots, create a reusable source-button widget with a native button and name label. Bind label text to Audio_Source_Name_FB$[n] or Video_Source_Name_FB$[n]. Bind enable/visibility to the matching Source_Available[n] feedback. Bind the press join to Audio_Source[n] or Video_Source[n]. Allocate those additional joins explicitly; they are not in the starter's ID-based source map.
4. For source highlight, compare the measured Audio_Source_Selected_FB or Video_Source_Selected_FB index with n in SIMPL logic and send a selected-state digital to that button, gated by the corresponding validity output. Do not latch the highlight merely from a press.
5. Add a volume slider with an explicit 0–100 range. Its send analog goes to Volume_Set; receive analog comes from Volume_FB. Gate controls with Volume_Valid_FB and connection status. If the component scales differently, add an explicit scaler rather than sending 65535 as a percent.
6. Add Mute and Room Off buttons. For a toggle-style mute UI, route based on measured Mute_FB to Mute_On_btn or Mute_Off_btn; alternatively use separate On/Off buttons. Display mute only when Mute_Valid_FB is true. Room Off is a pulse.
7. Add Play, Pause, Stop and Skip buttons, then a separate WatchRemote page for navigation/channel/keypad/color keys. Route to the exact Transport pins. Bind availability to its discovered capabilities. Keep Play and Pause separate until measured playback state is established.
8. For Pandora, a maintained toggle sends Pandora_Shuffle_On. Initialize low. Label it as applying to the next station selection; it is not measured current shuffle state.
9. CP24a supplies live title/artist/album/artwork through the shared runtime. Wire the five metadata connections in [UI_LIVE_NOW_PLAYING.md](UI_LIVE_NOW_PLAYING.md), rebuild and transfer. Inspection JSON is separate and must not be bound directly as a title.

## 5. Dynamic stations: choose the appropriate approach

**Recommended now:** keep the station browser in custom HTML. The supplied collector understands catalog revisions, IDs, invalidation and JSON pages. It can show all 162 stations without 162 fixed serial labels. You can extract the station portion from `app.js` later while keeping `catalog.js` and the bridge shared.

**Fully native alternative:** create a processor-side UI adapter and a Construct Widget List contract. This adapter is additional work; it is not already provided by this package. Its responsibilities are:

1. Assemble the complete current catalog in C# without exposing private selectors.
2. Publish a native list item count and a per-row name/enable binding through the selected contract.
3. Keep the catalog ID and revision associated with each row internally.
4. Receive the clicked row's index and translate it into a current-revision media-selection request.
5. Clear row enable/count on source change, disconnect or invalid catalog; prevent stale clicks and maintain independent client/page context as needed.
6. Bind your Construct Widget List to that contract and its item-count signal. A serial containing JSON cannot simply be assigned as its data source.

Crestron documents Widget List as contract-based and describes component/nesting restrictions in [Widget List](https://help.crestron.com/construct/Content/Topics/UI%20Editor/Ch5%20Components/Widget-List.htm). Build a small row widget with a button and label first; avoid putting another list inside each row. Keep the layout shallow and split larger remote screens into pages, following [Construct design guidance](https://help.crestron.com/construct/Content/Topics/Best-Practices/Design%20Best%20Practices.htm).

## 6. Build and load the touchpanel

1. Build the Construct solution and resolve project errors. Inspect BuildOutput to confirm the custom assets are present and referenced correctly.
2. Transfer the generated HTML5 project/archive to the actual panel using the supported Construct/Toolbox workflow for that panel. Set processor host and IP ID to match SIMPL.
3. Verify connection feedback first, then run the acceptance sequence in UI_JOIN_MAP.md. Inspect the panel browser console/diagnostics for missing assets and JavaScript errors.
4. Confirm text sizes, 44px-or-larger touch targets, long station names and scrolling. Test the actual device; desktop preview does not establish touchpanel performance.

## 7. Enable the browser/Web XPanel version

1. Use Construct's Web XPanel project settings for the processor host and matching client IP ID. Let Construct own its Web XPanel initialization; do not start the standalone starter's connection code inside it.
2. Build/package and transfer the browser project using the supported processor web-project workflow. Open its HTTPS URL and follow the normal Crestron authentication flow.
3. Check Web XPanel/CIP connection, then gateway connection. If communication fails, verify IP ID, project build, HTTPS/authentication configuration and browser console. See [Construct Web XPanel communication](https://help.crestron.com/construct/Content/Topics/UI%20Editor/Troubleshooting/WebXPanel%20Communication.htm).
4. Test browser refresh, reconnect and two simultaneous clients. Do not infer success solely from seeing the page.

## 8. Expand to more rooms after Room13 works

Create one Sources/Transport pair per room and assign a join group to each. Store a room manifest of friendly name, Control4 room ID and joins. Before changing rooms, destroy the old view/bridge, clear its catalog and mount with the next room's config. Avoid changing Room_ID parameters at runtime. The supplied starter's room selector has one entry; multi-room switching remains to be implemented.
