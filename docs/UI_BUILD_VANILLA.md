# Build with vanilla HTML5, CSS and JavaScript

This path uses no React, Angular or Vue. The supplied app uses ordinary DOM elements, Crestron's communication libraries and a small Vite packaging step. One build targets an HTML5-capable Crestron touchpanel and a browser through Web XPanel. Actual compatibility must be checked against the selected panel model/firmware.

## 1. Preview before connecting anything

1. Open `ui/room-media/index.html` directly in Edge/Chrome. It is the offline demonstration, including captured sample metadata.
2. Select Spotify; select HEAVY. Switch to Pandora, search Instrumental Radio and navigate pages. The two identically named stations remain distinct by ID.
3. Try Watch, volume, mute and Room Off. None of these preview actions operate your system.
4. Review the screenshots under `previews`. The desktop and portrait layouts are CSS adaptations of the same application.

## 2. Install the development prerequisites

1. Install a Node.js/npm version supported by the current Crestron CH5 utilities. The local build used Node24.19.0 and npm11.17.0; Crestron's current archive guide specifies minimum Node24.18.0/npm11.16.0. Confirm requirements before installing on another PC. See [CH5 archives and deployment](https://sdkcon78221.crestron.com/sdk/Crestron_HTML5UI/Content/Topics/UI-CH5-Archives.htm).
2. Open PowerShell:

```powershell
Set-Location 'C:\Users\mfloyd\Desktop\Control4_Crestron_VS2026_Handoff_Package\ui\room-media'
node --version
npm --version
npm ci
npm test
```

3. Keep `package-lock.json`. Dependencies are pinned: CrComLib2.20.0, theme2.20.0, Web XPanel2.8.0, utilities CLI2.19.0. Do not upgrade these casually while commissioning.
4. `npm run test:preview` uses installed Microsoft Edge through Playwright and creates screenshots. On a different PC, install Edge or deliberately adjust the test browser setting; there is no automatic browser download in these instructions.

## 3. Understand which files do what

| File | Role |
|---|---|
| index.html, preview-data.js | Offline demo only; never a live data source |
| live.html, live-main.js | Real CH5 entry and platform connection initialization |
| app.js, styles.css | Shared framework-free layout and interactions |
| join-map.js | Reviewed room, host, IP ID and typed joins |
| catalog.js | Current-revision catalog/page collector |
| crestron-bridge.js | CrComLib subscriptions and commands |
| construct-main.js | Separate integration entry for a Construct-owned connection |
| dist/index.html | Generated live entry after production build |

`CrComLib.subscribeState` receives processor state; `publishEvent` sends typed UI events. The bridge uses `b` for digital, `n` for analog and `s` for serial, with join numbers passed as strings. See Crestron's [receiving joins](https://sdkcon78221.crestron.com/sdk/Crestron_HTML5UI/Content/Topics/Advanced/Joins-Received-CS.htm) and [sending events](https://sdkcon78221.crestron.com/sdk/Crestron_HTML5UI/Content/Topics/Advanced/Events-Joins-CS.htm).

## 4. Wire SIMPL before enabling live commands

1. Follow [UI_JOIN_MAP.md](UI_JOIN_MAP.md). Add/configure the panel/Web XPanel device, choose an unused IP ID, and wire each command and feedback by type.
2. Preserve the shared gateway and Room13 Sources/Transport pair. No Control4 password/token belongs in this web project.
3. Edit `join-map.js`: set the reviewed joins, host and IP ID, keep roomId13 for Living Room. Set `enabled:true` when the program and client mapping are ready.
4. CP24a uses `receive.nowPlaying:"16"` with digital99 validity and analog20 packet requests/feedback (analog21 packet count). Wire the five connections in [UI_LIVE_NOW_PLAYING.md](UI_LIVE_NOW_PLAYING.md). The bridge reassembles the live document automatically.
5. The starter uses a serial source ID followed by an Audio/Video selection pulse. Station requests instead select directly on the change of Media_Select_Request$ and carry the current catalog revision. Do not wire that serial request to Media_ID$.
6. Confirm supported command metadata is discovered so Transport capability feedback can enable controls. Do not force unavailable buttons on just to make them appear responsive.

## 5. Run the development server

```powershell
npm run dev
```

Open the printed local URL. `/index.html` is the offline demo; `/live.html` is the live entry. Live mode requires the real processor and the Crestron browser connection/authentication setup. Serving a file on localhost does not bypass processor authentication or guarantee cross-origin access. Use the packaged processor-hosted build for definitive Web XPanel testing.

If the page says live mode is disabled, review join-map.js; do not edit a generated minified file. Stop the server with Ctrl+C when finished.

## 6. Use the correct connection on each target

`live-main.js` uses Crestron's `getWebXPanel(!runsInContainerApp())` pattern. In an ordinary browser it initializes Web XPanel with the configured processor host and IP ID. In a native Crestron container it relies on the container's communication path. Do not initialize a second browser connection on the touchpanel.

For the RMC4, do not set Web XPanel's `roomId` to Control4 room13. That Web XPanel setting is for VC-4 rooms. The starter's own roomId is used only in our catalog/selection validation. Preserve the SDK defaults for connection ports/token endpoints unless an actual deployment requirement says otherwise. See [custom Web XPanel projects](https://sdkcon78221.crestron.com/sdk/Crestron_HTML5UI/Content/Topics/Platforms/X-Custom.htm).

Browser controls are gated by both CIP connection and gateway connection. Native connection-loss recovery and state replay still require panel testing. Never treat a visible page or an open WebSocket as proof that the complete control path is ready.

## 7. Build and package

```powershell
npm test
npm run test:preview
npm run build
npm run archive
```

1. Build uses the live entry. A post-build step copies `dist/live.html` to `dist/index.html`, so the archive's default page is live, not the offline mockup.
2. Confirm `dist/index.html` and `dist/assets/` exist. Preview sample data is not imported into the live build.
3. The archive command creates the RoomMedia CH5 archive under `archives`. If a contract is added later, include the appropriate contract in packaging rather than assuming it is automatically present.
4. Crestron's libraries produce bundle-size and eval warnings in the current build. The build succeeds; actual panel memory/startup performance remains a commissioning check. Do not patch vendor library internals to silence warnings.
5. No deployment occurs as part of build/archive. Keep a copy of the previous working UI for rollback.

Crestron requires its communication library and theme assets for CH5 projects; Web XPanel adds the browser transport. These dependencies are already included here. See [required project files](https://sdkcon78221.crestron.com/sdk/crestron_html5ui/Content/Topics/Reference/Required-Project-Files.htm).

## 8. Load the touchpanel

1. Identify the exact panel model, resolution and HTML5 support. Set its processor host/IP ID to match the configured SIMPL device.
2. Use Toolbox's supported HTML5 project transfer workflow to load `archives/RoomMedia.ch5z` onto that panel.
3. Confirm it opens the live page and receives room name/connection state. If controls show no lists, inspect the actual Sources outputs and serial joins before changing UI code.
4. Follow the acceptance sequence in UI_JOIN_MAP.md. Confirm actual playback, not just a highlight or a diagnostic saying the command was accepted.
5. Test long names, portrait/landscape where supported, slider touch response and page scrolling on the device.

The CLI also supports deployment, but it is a separate explicit action. For example, after verifying your target address and credentials:

```powershell
npx ch5-cli deploy -p -H PANEL_IP -t touchscreen archives/RoomMedia.ch5z
```

Do not substitute the RMC4 address for PANEL_IP in a touchscreen deployment. Let the utility prompt for credentials; do not store passwords in scripts.

## 9. Load the browser/Web XPanel project

1. Confirm processor HTTPS/authentication settings and the dedicated Web XPanel SIMPL device/IP ID.
2. Use Toolbox's web-project transfer workflow to publish the archive on the processor. Open the resulting HTTPS project URL and complete normal Crestron authentication.
3. For an explicit CLI web deployment after reviewing the target:

```powershell
npx ch5-cli deploy -p -H 192.168.1.169 -t web archives/RoomMedia.ch5z
```

4. Check browser console and processor IP table for connection errors. Test CIP connected before testing the Control4 gateway. Do not disable HTTPS validation globally to resolve a UI issue; the gateway's local-Control4 certificate policy is unrelated to browser authentication.
5. Run playback/volume/source tests, then refresh the browser and test disconnect/reconnect. Use a second independently configured client to test shared-room feedback.

See Crestron's [Web XPanel deployment](https://sdkcon78221.crestron.com/sdk/Crestron_HTML5UI/Content/Topics/Platforms/X-Deploy.htm). No UI has been transferred by this task.

## 10. Finish metadata and multi-room support

1. Add verified metadata normalization in C# from the inspection evidence. Expose a real room-scoped output through Sources and assign a serial join. This is the next implementation stage, not accomplished by configuring the existing null join.
2. Extend the bridge to validate that packet's room/source and emit `nowPlaying` with title/artist/album/station. Clear absent fields on every update. Keep station context separate from track title.
3. Resolve artwork only after the actual URL/reference and access path are verified. Do not expose Control4 credentials or opaque selectors to the browser.
4. Add measured play state and duration/position only after units and events are established. Until then use separate commands with no fake progress bar.
5. Wire and hardware-verify the completed measured selected-source and validity mapping described in the shared guide.
6. Replace the one-entry room selector with a manifest. Destroy old subscriptions, clear state, then create the new bridge for that room's join map. Keep one Sources/Transport pair per room in SIMPL and one shared gateway.
7. Verify the completed50-key Watch remote and add page timeout/retry handling before production acceptance. The delivered starter is one-room scaffolding with an interactive visual reference, not a finished multi-room product.
