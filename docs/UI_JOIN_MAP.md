**CP28a read-only progress:** Add Sources `Now_Playing_Progress_JSON$` → panel serial input17. No other joins change. The compact packet supplies measured position/duration in confirmed seconds and a track revision; no page request, timer or seeking. Update the Sources symbol, compile/load SIMPL, rebuild/transfer Construct. See [UI_LIVE_NOW_PLAYING.md](UI_LIVE_NOW_PLAYING.md).

**CP21a wire update:** Source/media serial outputs now carry bounded packets <=255 bytes, reassembled by the UI. Capability serial8 uses compact50-key state JSON. Existing joins remain unchanged; page request/feedback numbers refer to transport packets, while widget pages remain six-row local pages. Install the matching updated CLZ and UI; see [UI_SERIAL_TRANSPORT.md](UI_SERIAL_TRANSPORT.md). The logical JSON contract described below applies after reassembly.

**Complete scalar mapping:** 140 bindings now cover the room UI and remaining Sources/Transport scalar signals. The highest allocated joins are Digital99, Analog21 and Serial17. Existing assignments remain intact. Fixed-slot arrays and private manual-selector pins are explicitly module-only below. Join assignment does not change the existing SIMPL program automatically.

# Shared SIMPL / Crestron join wiring

These are proposed joins for the new UI. They do not describe assignments already present in the user's SIMPL program. Review for collisions before using. Within each signal type, unrelated commands and feedback use different join numbers. Opposite directions share a number only for the same function: analog1 is Volume_Set/Volume_FB. Signal types remain separate namespaces.

**Join allocation:** Room Off stays on digital5 (Sources). Watch is now digital30. All previously assigned digital joins30 and above move up1, including the later controls and feedback. Digital19 and20 are unassigned. Existing analog/serial assignments are unchanged; CP24a adds the metadata joins listed below. The exact current assignments are in the tables and CSV files below.

## 1. Establish the room and client

1. Preserve the existing shared Control4 gateway, account authentication and WebSocket behavior.
2. For Living Room, use one Sources instance and one Transport instance with Gateway_ID=1, Room_ID=13. Keep room parameters fixed.
3. Add/configure the appropriate Crestron HTML5 panel or Web XPanel device in SIMPL Windows for the selected hardware. Choose an unused IP ID. The starter's `0x03` is only an example. Set the client to the same IP ID and processor host `192.168.1.169`.
4. Use a separate appropriate IP ID/device entry for a concurrently connected independent client. Wire each client's joins to the same room logic as required; route module feedback to both devices. Do not reuse a busy IP ID blindly.
5. Connect UI commands from the touchpanel symbol's outputs to module inputs. Connect module feedback to the touchpanel symbol's inputs. The tables below use the UI's point of view.
6. Check signal types and join strings carefully. Keep signal lengths sufficient for the complete module JSON. Do not use a short intermediate serial buffer that truncates a page.

## 2. UI → processor commands

Sources inputs unless marked Transport. Pulse inputs must return low; the JavaScript bridge uses about 100 ms pulses.

| Type / example join | Target pin | Behavior |
|---|---|---|
| Serial 1 | Source_ID$ | String item ID; set before the selection pulse |
| Digital 1 | Select_Audio_ID_btn | Pulse Listen source selection |
| Digital 2 | Select_Video_ID_btn | Pulse Watch source selection |
| Serial 2 | Media_Select_Request$ | Current room/kind/revision/id JSON; selects on change |
| Analog 1 | Volume_Set | 0–100; commit on slider release |
| Analog 2 | Media_List_Page_Set | 1-based module JSON page request |
| Analog 3 | Audio_List_Page_Set | 1-based source JSON page request |
| Analog 4 | Video_List_Page_Set | 1-based source JSON page request |
| Digital 3 | Mute_On_btn | Pulse |
| Digital 4 | Mute_Off_btn | Pulse |
| Digital 5 | Room_Off_btn | Pulse |
| Digital 6 | Pandora_Shuffle_On | Maintained level; low is default Off; next selection only |
| Digital 30 | Transport Watch_btn | Local UI action; no REST command |
| Digital 31 | Transport Control4_btn | Pulse; enabled only when advertised |
| Digital 32 | Transport Listen_btn | Local UI action; no REST command |
| Digital 33 | Transport List_btn | Local UI action; no REST command |
| Digital 34 | Transport Custom_1_btn | Pulse; enabled only when advertised |
| Digital 35 | Transport Custom_2_btn | Pulse; enabled only when advertised |
| Digital 36 | Transport Custom_3_btn | Pulse; enabled only when advertised |
| Digital 21 | Transport Guide_btn | Pulse; enabled only when advertised |
| Digital 37 | Transport Page_Up_btn | Pulse; enabled only when advertised |
| Digital 38 | Transport Page_Down_btn | Pulse; enabled only when advertised |
| Digital 39 | Transport Previous_btn | Pulse; enabled only when advertised |
| Digital 40 | Transport Volume_Up_btn | Pulse; enabled only when advertised |
| Digital 41 | Transport Volume_Down_btn | Pulse; enabled only when advertised |
| Digital 42 | Transport Channel_Up_btn | Pulse; enabled only when advertised |
| Digital 43 | Transport Channel_Down_btn | Pulse; enabled only when advertised |
| Digital 22 | Transport Up_btn | Pulse; enabled only when advertised |
| Digital 28 | Transport Down_btn | Pulse; enabled only when advertised |
| Digital 24 | Transport Left_btn | Pulse; enabled only when advertised |
| Digital 26 | Transport Right_btn | Pulse; enabled only when advertised |
| Digital 25 | Transport Select_btn | Pulse; enabled only when advertised |
| Digital 44 | Transport Mute_btn | Pulse; enabled only when advertised |
| Digital 23 | Transport Info_btn | Pulse; enabled only when advertised |
| Digital 29 | Transport Menu_btn | Pulse; enabled only when advertised |
| Digital 27 | Transport Back_btn | Pulse; enabled only when advertised |
| Digital 45 | Transport DVR_btn | Pulse; enabled only when advertised |
| Digital 46 | Transport Rewind_btn | Pulse; enabled only when advertised |
| Digital 47 | Transport Fast_Forward_btn | Pulse; enabled only when advertised |
| Digital 14 | Transport Skip_Back_btn | Pulse; enabled only when advertised |
| Digital 11 | Transport Play_btn | Pulse; enabled only when advertised |
| Digital 15 | Transport Skip_Forward_btn | Pulse; enabled only when advertised |
| Digital 48 | Transport Record_btn | Pulse; enabled only when advertised |
| Digital 12 | Transport Pause_btn | Pulse; enabled only when advertised |
| Digital 13 | Transport Stop_btn | Pulse; enabled only when advertised |
| Digital 49 | Transport Red_btn | Pulse; enabled only when advertised |
| Digital 50 | Transport Green_btn | Pulse; enabled only when advertised |
| Digital 51 | Transport Yellow_btn | Pulse; enabled only when advertised |
| Digital 52 | Transport Blue_btn | Pulse; enabled only when advertised |
| Digital 53 | Transport Digit_1_btn | Pulse; enabled only when advertised |
| Digital 54 | Transport Digit_2_btn | Pulse; enabled only when advertised |
| Digital 55 | Transport Digit_3_btn | Pulse; enabled only when advertised |
| Digital 56 | Transport Digit_4_btn | Pulse; enabled only when advertised |
| Digital 57 | Transport Digit_5_btn | Pulse; enabled only when advertised |
| Digital 58 | Transport Digit_6_btn | Pulse; enabled only when advertised |
| Digital 59 | Transport Digit_7_btn | Pulse; enabled only when advertised |
| Digital 60 | Transport Digit_8_btn | Pulse; enabled only when advertised |
| Digital 61 | Transport Digit_9_btn | Pulse; enabled only when advertised |
| Digital 62 | Transport Star_btn | Pulse; enabled only when advertised |
| Digital 63 | Transport Digit_0_btn | Pulse; enabled only when advertised |
| Digital 64 | Transport Pound_btn | Pulse; enabled only when advertised |

All50 UI remote keys are covered:49 use Transport inputs, while Room Off uses only the existing Sources Room_Off_btn on digital5. Do not wire Transport Room_Off_btn for this UI; digital19 is no longer assigned. Both visible Room Off controls now send digital5.

Watch, Listen and List are local UI actions supported by the existing module. Their UI buttons also change the local media view; they do not guess or send a REST service-selection command. Optionally wire Watch_UI_Request, Listen_UI_Request and List_UI_Request to your native Construct navigation logic. Native page flips remain a separate project navigation setup.

The shared Watch screen groups all50 keys into Navigation, Playback, Room, Volume/channel, Keypad and Functions. Its key area scrolls inside the fixed panel viewport. In live mode, remote commands require `advertised` capability feedback; the three local keys accept `local_ui`. Missing/unavailable keys stay disabled. The JSON capability subscription remains serial8. All buttons are momentary pulses, not held/repeating commands; hold-to-repeat is not implemented.

Machine-readable mapping: [UI_TRANSPORT_JOINS.csv](UI_TRANSPORT_JOINS.csv). No new Transport module pins or REST command mappings were introduced.

### Additional controls and commissioning joins

These are assigned for widgets or commissioning logic, not automatically pulsed by the page. Query and discovery are deliberate actions; do not add a timer. Existing automatic source/service discovery remains the normal path. Inspect controls produce exported evidence, not live metadata outputs. Name selection occurs on serial change; analog selection uses the module's index semantics.

| Type / join | Module input |
|---|---|
| Digital 65 | Sources Query |
| Digital 66 | Sources Discover_Sources |
| Digital 67 | Sources Inspect_Room_State |
| Digital 68 | Sources Inspect_Now_Playing |
| Digital 69 | Sources Inspect_Media_Catalog |
| Digital 70 | Sources Browse_Media_Playlists |
| Digital 71 | Sources Browse_Media_Albums |
| Digital 72 | Sources Browse_Media_Audio_Stations |
| Digital 73 | Sources Browse_Media_Movies |
| Digital 74 | Sources Browse_Media_Video_Stations |
| Digital 75 | Sources Select_Media_ID_btn |
| Digital 76 | Sources Inspect_Service_Presets |
| Digital 77 | Sources Browse_Service_Presets |
| Analog 5 | Sources Video_Source_Select |
| Analog 6 | Sources Audio_Source_Select |
| Analog 7 | Sources Media_Item_Select |
| Serial 9 | Sources Video_Source_Name$ |
| Serial 10 | Sources Audio_Source_Name$ |
| Serial 11 | Sources Media_ID$ |
| Digital 90 | Transport Discover_Commands |

## 3. Processor → UI feedback

| Type / example join | Source pin | Purpose |
|---|---|---|
| Serial 3 | Sources Audio_Source_List_JSON$ | Listen source pages |
| Serial 4 | Sources Video_Source_List_JSON$ | Watch source pages |
| Serial 5 | Sources Media_List_JSON$ | Current stations/presets, revision and selection support |
| Serial 6 | Sources Diagnostic$ | Commissioning log; keep out of ordinary product screen |
| Serial 7 | Sources Room_Name$ | Friendly room name |
| Serial 8 | Transport Key_Capabilities_JSON$ | Enable supported remote buttons |
| Digital 7 | Gateway Connected_FB | Control4 connection feedback; not a substitute for client CIP status |
| Digital 8 | Sources Sources_Valid_FB | Source-discovery validity |
| Digital 9 | Sources Media_List_Valid_FB | Catalog validity; false clears cached media |
| Digital 10 | Sources Media_Select_Available_FB | Selection readiness |
| Digital 16 | Sources Mute_FB | Measured mute |
| Digital 17 | Sources Volume_Valid_FB | Whether volume is meaningful |
| Digital 18 | Sources Power_FB | Measured room power |
| Analog 1 | Sources Volume_FB | Measured 0–100 volume |

### Remaining room and Transport feedback

Wire these outputs to the listed panel receive joins. The bridge subscribes to them as room-scoped `moduleFeedback`; counts, diagnostics and local-action requests are available for further widgets/host navigation. Mapping a local-action output does not itself program a native page flip. Source-selected index/validity now drives actual source-card highlights; volume and mute controls use their validity feedback. Other status/count fields are available without implying every field has a visible indicator.

| Type / join | Module output |
|---|---|
| Digital 78 | Sources Initialized_FB |
| Digital 79 | Sources Valid_FB |
| Digital 80 | Sources Power_Valid_FB |
| Digital 81 | Sources Mute_Valid_FB |
| Digital 82 | Sources Has_Video_Sources_FB |
| Digital 83 | Sources Has_Audio_Sources_FB |
| Digital 84 | Sources Content_Browse_Available_FB |
| Digital 85 | Sources Media_List_Partial_FB |
| Digital 86 | Sources Video_Source_Selected_Valid_FB |
| Digital 87 | Sources Audio_Source_Selected_Valid_FB |
| Digital 88 | Sources Video_Source_Overflow_FB |
| Digital 89 | Sources Audio_Source_Overflow_FB |
| Analog 8 | Sources Video_Source_Count_FB |
| Analog 9 | Sources Audio_Source_Count_FB |
| Analog 3 | Sources Audio_List_Page_FB |
| Analog 4 | Sources Video_List_Page_FB |
| Analog 10 | Sources Audio_List_Pages_FB |
| Analog 11 | Sources Video_List_Pages_FB |
| Analog 12 | Sources Video_Source_Selected_FB |
| Analog 13 | Sources Audio_Source_Selected_FB |
| Analog 14 | Sources Video_Source_Overflow_Count_FB |
| Analog 15 | Sources Audio_Source_Overflow_Count_FB |
| Analog 16 | Sources Media_Item_Count_FB |
| Analog 2 | Sources Media_List_Page_FB |
| Analog 17 | Sources Media_List_Pages_FB |
| Serial 12 | Sources Media_Kind$ |
| Serial 13 | Sources Source_Overflow_Message$ [element1] |
| Digital 91 | Transport Initialized_FB |
| Digital 92 | Transport Discovery_Valid_FB |
| Digital 93 | Transport Play_Available_FB |
| Digital 94 | Transport Pause_Available_FB |
| Digital 95 | Transport Stop_Available_FB |
| Digital 96 | Transport Watch_UI_Request |
| Digital 97 | Transport Listen_UI_Request |
| Digital 98 | Transport List_UI_Request |
| Analog 18 | Transport Advertised_Command_Count_FB |
| Analog 19 | Transport Mapped_Key_Count_FB |
| Serial 14 | Transport Local_Action$ |
| Serial 15 | Transport Diagnostic$ |

The selected-source analogs are indices in the corresponding source list, not item IDs or the last requested selection. A valid index is resolved against the matching list. No highlight is inferred merely from a button press or catalog context. Wire Mute_Valid_FB and Volume_Valid_FB to enable their controls.

Matching page-set/page-feedback analogs share2(Media),3(Audio sources),4(Video sources), as well as Volume1. All unrelated same-type directions remain separate.

### Pins intentionally kept module-only

The two fixed-slot source selection arrays, two availability arrays and two source-name output arrays are replaced by the dynamic source JSON/ID path in this UI. They do not need individual panel joins. This saves60 feedback and30 selection joins at the current15 slots per mode.

The manual Select_Service_Preset_btn, Service_Preset_Driver_ID, Service_Preset_Source_ID and Service_Preset$ path remains commissioning-only inside SIMPL; opaque service selectors are not sent to the browser. Automatic station selection uses the existing catalog IDs/revision instead. `_skip_` entries and module parameters are not panel joins.

The duplicate Transport Room_Off_btn is intentionally left unwired; the UI uses Sources Room_Off_btn on5. All other scalar Sources/Transport interface signals, except the four private selector pins noted above, are assigned. The complete list is [UI_ALL_JOINS.csv](UI_ALL_JOINS.csv); the machine-checked coverage report is `reports/ui-join-coverage.json`. Gateway authentication/account configuration remains processor setup; it is not part of the room widget contract.

## 4. List/selection contract

Sources JSON has `room_id`, `mode` (`listen`/`watch`), `valid`, `page`, `pages`, `total`, and `items:[{id,name}]`. IDs remain strings. Request missing pages before searching the entire list. The current source format has no revision, so source rediscovery during collection needs additional refresh-generation handling.

Media JSON additionally has `kind`, `revision`, `selection_supported`, `partial` and service context when available. On invalidation discard the old list immediately. Accumulate only one room/kind/revision; reject malformed pages and duplicate IDs. Module JSON pages and the UI's six-row pages are different: the UI first assembles the catalog and then paginates locally.

When a user taps an item, send this serial request with the exact current revision and catalog ID:

```json
{"room_id":13,"kind":"service_presets","revision":"COPY_CURRENT_REVISION","id":"357","request_id":"unique-per-tap"}
```

Do not send the row number, station name, copied private selector, or an earlier revision. The changing request_id ensures two taps on the same station still cause a serial change. A UI tap is a request, not confirmation of playback.

Spotify/Pandora source changes already trigger automatic service discovery in the gateway. Do not add periodic Browse/Query pulses or a polling timer. After connection, use the established module initialization/discovery workflow. If source discovery or transport discovery is not yet valid, pulse Discover_Sources / Discover_Commands once from commissioning logic, rather than repeatedly from a UI timer.

## 5. Live metadata (CP24a)

The Sources module now provides a framed `Now_Playing_JSON$` feed. Wire these additions; all previous assignments stay intact. Both the shared Construct UI and vanilla HTML5 use this contract.

| UI direction | Type / join | Sources pin |
|---|---|---|
| UI to module | Analog 20 | Now_Playing_Page_Set |
| Module to UI | Analog 20 | Now_Playing_Page_FB |
| Module to UI | Analog 21 | Now_Playing_Pages_FB |
| Module to UI | Digital 99 | Now_Playing_Valid_FB |
| Module to UI | Serial 16 | Now_Playing_JSON$ |

Analog20 is a matching page request/feedback pair. The bridge requests remaining packets automatically; never add a timer. The module sends room/source identity, validity, title, artist, album and an optional HTTPS artwork URL using stream `n`, with <=255-character packets. Spotify QueueChanged events identify the current row; initial room text is accepted only when its medSrcDev matches the measured service. Unsupported queue/artwork formats remain unavailable. No playback-state/progress/station field is guessed. Native RMC4 metadata delivery and actual panel image retrieval still need runtime verification. See [UI_LIVE_NOW_PLAYING.md](UI_LIVE_NOW_PLAYING.md) for loading and testing.

## 6. Acceptance sequence

1. Back up the existing program and verify your new device/IP ID and typed joins.
2. Build/load the reviewed SIMPL program using the existing 4-Series-only modules and library. This guide does not require changes to their target or architecture.
3. Connect one UI client; confirm gateway connection/name/source feedback before enabling commands.
4. Select an actual discovered Spotify source; wait for valid five-item list; select HEAVY. Confirm actual sound and module diagnostics.
5. Select Pandora; confirm the catalog replaces Spotify; search Instrumental Radio, select the desired entry and verify sound. Duplicate display names must retain separate IDs.
6. Test Shuffle Off then On on the next station selection; verify request mode, not an assumed current shuffle feedback state.
7. Test volume, mute, supported transport, Room Off, app-initiated source changes, disconnect/reconnect and a second client. Old media must not remain selectable after invalidation.
8. Verify Spotify title/artist/album/artwork and Next updates on the UI; Room Off/disconnect must clear them. Unsupported/incomplete queue changes clear validity. Pandora-specific live metadata remains unverified.
