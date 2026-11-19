# Control4 ↔ Crestron — Codex Development Handoff

**Status:** Python API research through Media Step 4B completed; transfer implementation to Codex in Visual Studio 2026.
**Date:** 2026-11-15
**Purpose:** Build a robust, modular Crestron SIMPL Windows integration to a Control4 EA-5, including lighting/shades/AV and, where feasible, complete media browsing. Do not infer unverified protocol behavior.

## 1. NON-NEGOTIABLE WORKING BASELINE

- **Crestron processor:** RMC4; SIMPL Windows program with C# class library referenced from SIMPL+; **NOT SIMPL# Pro**.
- **Known-loading project archive:** `Control4_Crestron_WorkingBaseline_Merged_NET472.zip` (provided separately). **User confirmed this .NET Framework 4.7.2 project loads on RMC4.** Preserve this as a backed-up working baseline.
- **Framework:** `.NET Framework 4.7.2` (`v4.7.2`, `net472`). Do not upgrade to net48, modern .NET, or change toolchain-generated output without explicit separate load tests.
- **Crestron SDK:** `Crestron.SimplSharp.SDK.Library` **2.21.274**. Do not change SDK to 2.21.237 or pull in new binary dependencies without startup compatibility testing.
- **IDE:** Visual Studio 2026. Building with a new IDE is NOT proof of RMC4 runtime compatibility; rebuild fresh and test library loading.
- **Architecture:** One central Control4 Processor/Gateway handles authentication, access tokens, token refresh, HTTPS REST, WebSocket/Socket.IO, reconnection and logging. Lightweight, independently configurable Lighting, Shades, Audio, Video, Generic device modules share this gateway by Gateway_ID. Retain existing SIMPL+ digital, analog, and serial signals and existing namespace/class contracts unless migration is explicitly documented.
- **Crash history:** An earlier distinct `Control4-Crestron` project using different SDK, SDK-style project and event/startup logic caused `startSPlus.sh` to abort (core dumped); RMC4 Toolbox remained accessible, but SIMPL Program failed to load (`Could not create Initialize SIMPL+ interface class`, return `-39`). Root cause not isolated. Do not import that implementation wholesale. Avoid network work in constructors, static initializers, SIMPL+ Main or during interface startup. Connect only upon explicit signal; exceptions must not propagate through startup callbacks.
- **Current Stage 1 gateway:** Basic module framework starts, but expects an externally supplied Director bearer token. Automated account OAuth, token renewal, full REST/Socket.IO gateway, and individual device protocols are NOT yet implemented in known-loading baseline. Do not claim the C# integration already functions end-to-end.
- **Do not hardcode account credentials or bearer tokens.** Avoid logging credentials, token values, arbitrary media/history metadata. Local HTTPS diagnostics previously used disabled certificate validation; do not silently adopt insecure global validation in production.

## 2. VERIFIED CONTROL4 SYSTEM AND PYTHON TOOLING

- **Control4:** EA-5 / OS 3.x / controller name `HDMStereoone`, local IP `192.168.1.160`.
- **Python prototype:** `pyControl4==2.0.2`, tested with authenticated account. Windows project folder `C:\Control4-Crestron`; virtual environment `C:\Control4-Crestron\.venv`. Another Windows installation has Python 3.15 and 3.10 rather than original 3.12; use a functioning venv and validate compatibility rather than assuming fixed version.
- Cloud account OAuth succeeds; Director token valid **86400 seconds**; authenticated GET `/api/v1/items` returned **491 items**. Never store tokens in output/logs.
- `get_all_items_by_category('lights')` yielded **129 lighting-related items**.
- **Main Hall light** item ID **437**, associated driver ID **436**. REST: `get_state()` False / `get_level()` 0 initially; `set_level(50)` physically changed light and REST returned True / 50. WebSocket `OnDataToUI` on **iddevice=437** with `data.light_level` observed 100, 79, 16, 0 and corresponding REST readback.

## 3. VERIFIED MEDIA DISCOVERY (STEP 1, 2A)

- `/api/v1/agents/ui_configuration` with authenticated REST / `get_ui_configuration()` returned **156 UI experiences**, **16 unique room IDs**, **242 Watch/Listen source entries** (these are entries, not 242 unique devices).
- **Living Room**: room ID **13**.
- **Living Room Watch sources:** DIRECTV Stream **2061**, Paramount+ **2217**, Netflix **545**, Disney Plus **547**, Max **2033**, Apple TV+ **749**, Hulu **549**, YouTube **555**, Cameras **1717**, SHIELD TV **542**, Living RM TV **682**, Apple TV **1713**, Google TV **2204**. Watch source names initially blank in UI config; resolved through GET item-inventory matching. Not all IDs necessarily represent equivalent transport devices.
- **Common Listen services:** Michael's Spotify **65**, Geannie's Spotify **609**, Patio Spotify **2157**, Michael's Pandora **1648**, Geannies Pandora **261**, TuneIn **20**, Amazon Music **599**, Digital Media **100002**, Chromecast Audio **2211**, Falcon Player FPP **2229**. Available sources vary by room.

## 4. VERIFIED MEDIA REST COMMANDS (STEPS 2B–3C)

Using pyControl4 `C4Room` for Living Room (ID 13):

| Operation | Verified outcome |
|---|---|
| `set_video_and_audio_source(542)` | SHIELD TV selected; user confirmed physical TV switched. Also used to bring room ON after ROOM_OFF. |
| `set_audio_source(20)` | TuneIn selected in Navigator. No station auto-started. Could be no selected content; separate mute test showed room had been muted. **No proof of catalog browsing via REST.** |
| `set_mute_off()` / `is_muted()` | Before True; after False. Volume unchanged at 41. |
| Set room volume 30 (script's verified `SET_VOLUME` operation) | Original 41 -> 30 -> restored 41; room power and mute unchanged. Read exact method in included Python script; don't guess API signature. |
| `set_room_off()` / `is_on()` | Room changed ON -> OFF, user confirmed physical devices powered off. |
| Restore via `set_video_and_audio_source(542)` | Room turned back ON with SHIELD TV selected; user confirmed physically. This does NOT restore arbitrary prior media. |

Use explicit operator confirmation for commands that interrupt AV, and readback + physical confirmation for all changes. Source selection may change volume/mute due to Control4 room logic.

## 5. VERIFIED REAL-TIME EVENTS (STEPS 3D–3H)

- **Important WebSocket behavior:** callback dispatch uses event `iddevice` (not always Control4 room ID). Subscribing solely to **room 13** produced **zero callbacks**, even while REST showed changes. A raw (pre-filter) diagnostic then captured **325 events**. Do NOT interpret zero room-ID callbacks as lack of WebSocket support.
- **Living Room volume/mute feedback** on `iddevice=682` (Living RM TV / VIDEO_SELECTION): `OnDataToUI`, `data.volume_level` observed 19, 22, 24; `data.mute` observed True/False. Verified event-driven feedback route.
- **Power feedback:** `iddevice=682` `data.powerstate=False/True`, and `iddevice=2061` (DIRECTV Stream) `powerstate=False/True`. Correlates with room power REST OFF/ON but is equipment-level feedback, not generic room-power semantics.
- **Watch activity:** `iddevice=7`, `data.active_state_changed` object; examples `{type:'watch', room_id:13, active:False}` on room off and `{type:'watch', room_id:13, active:True}` on return. **Filter by `room_id`**; ID 7 also emitted a different room (`room_id:214`). This is Watch activity, NOT necessarily global room power or the selected source.
- **Routing changes:** `iddevice=2165`, `data.OutputStatus` = `{OutputID:2000, InputID:1001 or 1002 or -1, IsVideoBinding:True}`. `-1` correlated with room off. **No mapping of InputID 1001/1002 to Navigator source ID 2061/545 is proven.** Do not represent them as exact selected sources without independent verification.
- **Other input reports:** `iddevice=682` `data.current_input` alternated 3002 and 1002. These are NOT verified as Control4 Watch source IDs.
- Large numbers of `devicecommand` messages occur on IDs such as 699, 2203; do NOT interpret them as feedback state or print their payloads indiscriminately.
- **Production design:** Subscribe to appropriate per-room endpoint/agent IDs, normalize event data into stable SIMPL+ callbacks, requery REST on startup/reconnect and after writes; provide bounded REST fallback polling for unverified state (notably exact active source). Maintain explicit unknown-state semantics rather than inventing source feedback.

## 6. MEDIA BROWSING RECON (STEPS 4A–4B — LATEST STATUS)

- User wants BOTH (a) Watch/Listen service/source selection, and (b) browsing artist, album, playlist, track, station, search/favorites and metadata/artwork directly on the Crestron panel wherever feasible.
- `pyControl4 2.0.2` public `C4Director` method-name inspection found **no named browse/search/playlist/album/artist/track/media/queue/favorite/metadata methods**. This is NOT proof REST lacks such capabilities.
- Authenticated GET `/api/v1/items/{id}` succeeded for TuneIn **20**, Spotify **65**, Amazon Music **599**, Digital Media **100002**. Each returned a **list of length 1**, first element a **device metadata dictionary** with keys including `URIs`, `capabilities`, `categories`, `proxy`, `proxyMeta`, `control`, `protocolControl`, `id`, `name`, `roomId`, `typeName`, etc. `proxyMeta` and `categories` each were lists of length 1. No actual catalog tree was demonstrated by these device-detail calls.
- **Next engineering task:** inspect the specific vendor/DriverWorks `media_service`/digital-audio proxy documentation and what validated APIs expose. Distinguish official SDK driver's media browsing support from externally accessible REST; do not invent endpoints or equate internal proxy media browser features with REST access. Explore safe, documented, read-only service browse calls if established, otherwise design an opt-in custom Control4 DriverWorks Lua bridge (or supported per-service APIs) for browse/search/metadata. Source switching and basic AV control already work via REST; don't replace them needlessly.
- If inspecting deeper REST item metadata, use allowlisted types/keys and avoid exporting account IDs, access tokens, personally identifying profile or listening history.

## 7. IMPLEMENTATION ROADMAP FOR CODEX (DO IN SMALL LOAD-TESTED INCREMENTS)

1. **Safeguard baseline:** extract verified `Control4_Crestron_WorkingBaseline_Merged_NET472.zip` into version control; inspect all `.csproj`, `.cs`, `.usp`, `.ush`. Record hashes, compile as-is in VS 2026, ensure clean CLZ, SIMPL+ compilation and RMC4 startup. Do not overwrite working processor program. Build a minimal load probe / explicit Connect test. No authentication integration before startup is proven.
2. **Authentication in central C# gateway:** port verified pyControl4 account -> controller -> Director token OAuth flow. Secure handling; token expiry/refresh; cancellation, bounded timeouts; never block SIMPL+ Main, static constructor, event callback or processor logic thread. Catch and surface errors to diagnostics. Confirm .NET 4.7.2 / RMC4 TLS and certificate requirements.
3. **REST gateway APIs:** room query and source discovery; individual device methods; no per-module independent connections or authentication. Add verified lighting, room source, mute, level, off and command readback.
4. **WebSocket/Socket.IO:** reproduce verified pyControl4 2.0.2 protocol with a Crestron-compatible dependency strategy (or minimal hand-written compatible transport) subject to load testing. Parse per-device event IDs 437, 682 and filtered room activity from 7; build reconnect/resync logic; don't assume vanilla WebSocket equals Socket.IO.
5. **Modules:** Lighting, Shades, Audio, Video, Generic wrappers with Gateway_ID registry; per-item configurable Device_ID and per-room endpoint mapping. Keep SIMPL+ ABI stable, validate independent module initialization each build. Separate driver functionality from transport.
6. **Media browsing investigation:** document tested/untested capabilities by service (TuneIn, Spotify, Pandora, Amazon Music, Digital Media); if REST only selects services, implement a clearly bounded optional C4 DriverWorks media browser bridge. Distinguish service restrictions, authorization and artwork handling.
7. **Reliability/security:** startup and reconnect tests on RMC4, processor program reboots, 24h token expiry, no crashes, no global TLS bypass, backpressure/event dispatch safety, configurable polling and diagnostic counters.

## 8. FIRST TASK FOR CODEX IN VISUAL STUDIO 2026

Read this file and the existing `CONTROL4_CRESTRON_VS2026_HANDOFF.md` / `CONTROL4_CRESTRON_MEDIA_CODEX_NOTES.md`. Inspect working source and Python research scripts. **Do not start by implementing the whole integration.** First output:
- A short baseline audit of actual solution and SDK references, showing what is working vs placeholder.
- A table of verified native REST endpoints and pyControl4 methods used by scripts; cite exact source code locations.
- A media browse capability investigation plan distinguishing public REST versus DriverWorks internal media-service APIs.
- A proposal for the **next single safe test** to prove or disprove an actionable browsing endpoint, with no hardcoded credentials, read-only first, and an exact expected output/interpretation.
- A staged implementation plan that preserves RMC4 program startup, .NET Framework 4.7.2 and Crestron SDK 2.21.274.

**DO NOT** modify the working C# library until the user approves the initial audit/test proposal. When writing code, always give complete files and do not remove or replace pre-existing handoff instructions.

## 9. REFERENCE FILES IN HANDOFF PACKAGE

- Verified solution archive: `Control4_Crestron_WorkingBaseline_Merged_NET472.zip` (download from chat; not automatically part of smaller research ZIP).
- Python tests: `Control4_Media_Step1_Discovery.py`, `Step2A_SourceNames.py`, `Step2B_SelectSource.py`, `Step3A_MuteTest.py`, `Step3B_VolumeTest.py`, `Step3C_RoomPowerTest.py`, `Step3D_RoomFeedback.py`, `Step3E_RawEventDiagnostic.py`, `Step3F_PowerSourceFeedback.py`, `Step3G_StateFieldDiagnostic.py`, `Step3H_RoutingState.py`, `Step4A_BrowseCapability.py`, `Step4B_ItemStructure.py`.
- Prior chronological notes: `CONTROL4_CRESTRON_MEDIA_CODEX_NOTES.md` (contains step-by-step observations) and `CONTROL4_CRESTRON_VS2026_HANDOFF.md`.

## 10. DO NOT LOSE THESE DISTINCTIONS

- Proven physical control vs command merely submitted.
- Room 13 commands vs AV feedback from device 682.
- Watch activity event vs room general power vs exact active source.
- Routing InputID vs Control4 UI source ID.
- Control4 Navigator browse abilities vs pyControl4 REST browsing abilities (still unproven).
- Successful VS compile vs successful RMC4 SIMPL program startup.
