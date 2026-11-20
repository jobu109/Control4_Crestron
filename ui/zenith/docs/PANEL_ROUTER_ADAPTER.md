# Zenith UI on the Panel Room Router

Status (2026-11-19): built and tested locally. Not yet loaded on the RMC4 or a panel.

The Crestron build of the Zenith UI talks to the existing **Control4 - Panel Room Router** symbol through the joins it already uses. No SIMPL+, C# or CLZ change is needed. The draft house-wide `ZenithUI` contract in [CONTRACT.md](CONTRACT.md) is phase 2 and is still reachable with `?adapter=contract`.

Source: [src/adapters/panel-router.ts](../src/adapters/panel-router.ts). Tests: [tests/panel-router.test.ts](../tests/panel-router.test.ts) and [tests/csharp-frames.test.ts](../tests/csharp-frames.test.ts).

## Joins

These match [docs/UI_PANEL_ROOM_ROUTER.md](../../../docs/UI_PANEL_ROOM_ROUTER.md). Directions are from the panel's point of view.

| Panel direction | Type | Join | Router signal |
| --- | --- | --- | --- |
| Send | Serial | 101 | Request$ |
| Receive | Serial | 102 | State$ |
| Receive | Serial | 103 | Audio$ |
| Receive | Serial | 104 | Video$ |
| Receive | Serial | 105 | Catalog$ |
| Receive | Serial | 106 | Playing$ |
| Receive | Serial | 107 | Progress$ |
| Receive | Serial | 108 | Keys$ |
| Receive | Digital | 7 | Gateway Connected_FB |

## Deploy

1. In `ui/zenith`, run `npm ci`, then `npm run typecheck`, `npm test` and `npm run archive`. The archive is `ui/zenith/archives/ZenithUI.ch5z`.
2. Pick one of these:
   - **Side by side (recommended for the first test).** In SIMPL Windows, add a second Web XPanel device (for example IPID 0x04) with its own Panel Room Router: Gateway_ID 1 and the same Room_IDs$ as the existing panel. Wire the table above to the new XPanel's joins and Gateway Connected_FB to digital 7. Compile for 4-Series and load. The existing panel and FloydControl4 archive stay as they are.
   - **Replace on the existing IPID 0x03.** No SIMPL change. Loading ZenithUI.ch5z replaces FloydControl4 on that IPID. To roll back, reload the FloydControl4 archive.
3. Transfer ZenithUI.ch5z with Crestron Toolbox (Web Pages and Mobility Projects) and open the Web XPanel URL. Add `?ipId=0x04` when you used the side-by-side option. The default is 0x03.

On a TSW panel, the container app provides the connection and the URL parameters are ignored.

If the processor requires sign-in, the browser must hold a processor session before Web XPanel can connect. Otherwise WebXPanel reports `NOT_AUTHORIZED` and the panel shows "Sign in to the processor", with a Sign in button for the processor login page and a Reconnect button. If no connection is made within 15 seconds, it shows "Not connected to the processor". Add `?debug=1` to log every router join in the browser console.

## What the adapter does

- On Connected_FB it requests `rooms`, `scenes` and `audio_sessions`. The `audio_sessions` request only checks whether Whole House Audio is available. It is closed once read and opened again while the Audio page is showing.
- The router keeps one room session per panel. Rooms load on demand: when the Room or Media page shows a room, the adapter sends `{"op":"room","room_id":…}`. That room's devices, sources, presets, now playing, progress and remote keys then arrive. When the user switches rooms, the previous room's live state is cleared, because the router stops reporting it.
- Paged documents use the `w:1` framing from `UiSerialFrames.cs`. The router publishes page 1 and the adapter requests the remaining pages with `device_page`, `audio_page`, `video_page`, `media_page`, `playing_page`, `rooms_page`, `scenes_page` and `audio_sessions_page`.
- Every request carries an increasing `q` and is at most 255 characters. Requests are sent at least 60 ms apart (REQUEST_GAP_MS), because Request$ is one SIMPL+ string input that only holds its latest value. A queued request for the same target is replaced by the newer one. `house_devices` is sent once the room list arrives, and rooms, scenes or site documents that haven't completed after 6 seconds are requested again, up to 3 times.

| UI action | Router request |
| --- | --- |
| Light level, on, off, toggle | `device` with `action:"level"` and 0–100 |
| Room lights On / Off | one `device` level per light in the room |
| Shade open, close, stop, position | `device` with `open`, `close`, `stop` or `level` |
| Watch source | `video` |
| Listen source | `audio` (service sources open their saved presets) |
| Preset | `item` with the catalog `kind` and `revision` |
| Volume, mute, unmute, room off | `volume`, `mute_on`, `mute_off`, `off` |
| Play, pause, next, previous, volume ± | `key` with `Play`, `Pause`, `Skip_Forward`, `Skip_Back`, `Volume_Up`, `Volume_Down`, only when Keys$ advertises the key |
| Watch remote (Navigation, Keypad, Playback pads) | `key` with the SR-260 key name. Offered only while a Watch source is active, and only for `advertised` keys. Room_Off is the power button. `local_ui` keys (Watch, Listen, List) are panel-side and never sent. |
| Pandora shuffle | `shuffle` with 0 or 1. Shown only on the Audio tab when the playing source's catalog reports `pandora_shuffle`. |
| Scene | `scene_activate` with the current catalog `revision` and `slot` |
| Zone volume, mute | `audio_session_volume`, `audio_session_mute` |
| Zone on / off | `audio_session_add` / `audio_session_remove` |

## Shown as unavailable

The adapter does not invent state. Controls are disabled or hidden when the router cannot carry them out:

- **Floors.** The router has no floor data, so every scoped room is listed under one "Rooms" group.
- **Lighting and Shades overview pages** use the router's whole-house view. `house_devices` (sent on connect) registers every scoped room's discovered lights and shades and returns a framed `house_devices` catalog (room 0) with current levels. Later level changes arrive on State$ as `{"h":1,"u":<id>,"v":<level>}`. Commands for a room that isn't open use `house_device` with `room_id`, `device_id` and `action`/`value`, checked against the catalog. `house_devices_close` releases the registrations, which are reference-counted alongside the open room's session. The tiles appear only when the catalog reports devices, and an empty catalog is re-requested up to 6 times, 10 seconds apart, in case Director discovery is still running.
- **Room status in the Rooms list** is shown only for the room that was last opened.
- **Ambient lighting** is hidden because the router has no room lighting scene. Room lights On and Off send per-light levels.
- **Lights** all get a level slider, because the router only accepts level commands. Switch-only loads may simply go to 0 or 100.
- **Transport keys** that the room does not advertise are disabled.
- **Zone power.** Removing the queue owner is not offered, because the router rejects it. "On" joins a room to the only active session in scope. With no session, or more than one, it is disabled because the target would be ambiguous. Zone volume ± steps the measured volume by 5.
- **Climate, security, cameras and the other designed pages** stay hidden. The router does not report them.
- **Home page Location, Current Weather and Sunrise/Sunset** come from the router's `site` request (re-read every 15 minutes). Its `site` document carries:
  - `latitude` and `longitude`, from the Control4 project root item's `Latitude`/`Longitude` variables. Sunrise and sunset are calculated on the panel ([src/model/sun.ts](../src/model/sun.ts)).
  - `weather`, with `temperature` and today's `low`/`high`, from an agent whose name contains "weather" and that has a `CURRENT_TEMPERATURE` variable (the OpenWeather driver). It is included only when `weather_status` is `reporting`. When the driver reports 0 for humidity and pressure, the status is `not_reporting` and the panel shows Unavailable.
  - `temperature_unit` (`F` or `C`), worked out from the driver's own readings. Temperature, humidity and dew point only agree under one scale. Very humid readings can't tell the two apart, so the gateway keeps the last scale it identified. With no dew point, temperatures show without a unit letter.
  - The place name is a Composer project setting that Director does not expose: the root item's `/setup` returned 404 (probe revision 3). Probe revision 4 found that the root item exposes only variables and commands, while the OpenWeather agent's `/api/v1/items/{id}/properties` returns its Composer properties (timezone, sunrise, sunset, current readings). Revision 5 lists every property name so a location property can be identified. None of them carries it, so the gateway reads the place name from the weather driver's first non-empty variable among `LOCATION_NAME`, `CURRENT_LOCATION`, `LOCATION`, `CITY_NAME` and `CITY`. If the driver has none, the same names are read from Composer custom variables (Programming → Variables, Director's `variables` agent) and then from the project root. If none of them is set, the panel names the location itself: [src/model/places.ts](../src/model/places.ts) finds the nearest town to the project coordinates in a bundled GeoNames table ([src/data/README.md](../src/data/README.md), CC BY 4.0). It needs no network access, and the coordinates never leave the panel. The name is left out when the nearest listed town is more than 25 km away. The table is a separate 4.3 MB chunk, loaded only once coordinates arrive.

## Preview without a processor

`npm run dev`, then open `http://127.0.0.1:5173/?adapter=sim`. This runs the real adapter against a simulated router ([src/adapters/router-sim.ts](../src/adapters/router-sim.ts)) with a generic four-room house. Lights, shades, sources, presets, the remote, shuffle, progress, scenes and Whole House Audio all respond. The simulator exists only in the dev server. Production builds and the CH5 archive leave it out, so a deployed panel can never run a simulated house.

## Not verified yet

- Loading on the RMC4, a Web XPanel and a TSW.
- Room switching and recovery after a reconnect on real hardware.
- Whole House Audio membership changes from the Multi Audio page.
- Multi-page scene catalogs. The 17-page hardware catalog is exercised in tests only.

## C# framing fixture

`tests/fixtures/csharp-frames.json` holds frames produced by the Release `Control4Gateway.dll` `UiSerialFrames` class, loaded by reflection the same way as [tests/UiSerialFramesChecks.ps1](../../../tests/UiSerialFramesChecks.ps1). If the C# framing changes, rebuild Release and regenerate it with [tools/gen-csharp-frames.ps1](../tools/gen-csharp-frames.ps1).
