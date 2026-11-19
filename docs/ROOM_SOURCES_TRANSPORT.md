# Room Sources / Transport split

Split introduced in `Stage1-CP12a-RoomSourcesTransport`. Current update: [CP12b event-driven rooms and actual REST mappings](EVENT_DRIVEN_ROOMS.md). Both modules use Gateway_ID and Room_ID, with two leading skips on both directions and the existing shared gateway. Preserve the .NET4.7.2 class-library architecture and **4-Series-only** compilation.

## Sources

Control4_Room.usp now displays as **Control4 - Room Module - Sources**. It retains source discovery, compile-time15 Audio/Video slots, availability/name outputs, digital/index/name/ID selection, full JSON source lists, paging, overflow, volume/mute, Room Off and measured feedback.

Next_Source, Previous_Source, Select_Listed_Source and the old Browse_Video_Sources/Browse_Audio_Sources cursor inputs are removed. Cursor-only outputs Browse_Mode_FB, Source_Position_FB, Source_Count_FB and Listed_Source_Name$/Listed_Source_ID$ are removed. Use numbered/index/name/ID selection or JSON pages instead. Audio_Source_Count_FB and Video_Source_Count_FB remain total counts. Transport inputs and Transport_Available_FB are removed from Sources. Selected-source indices now use measured room fields in [CP14a](ROOM_MEASURED_SOURCE_FEEDBACK.md), pending runtime verification.

## Transport

New Control4_Room_Transport.usp displays as **Control4 - Room Module - Transport**. The50 remote inputs match the [official SR-260 user guide](https://docs.control4.com/docs/product/system-remote-control-sr-260/user-guide/english/latest/system-remote-control-sr-260-user-guide-rev-c.pdf). This is a button interface, not a claim that all50 functions work over REST.

| Group | Inputs (all suffixed _btn) |
| --- | --- |
| Room/system | Room_Off, Watch, Control4, Listen, List |
| Programmable | Custom_1, Custom_2, Custom_3, Red, Green, Yellow, Blue |
| Navigation | Guide, Page_Up, Page_Down, Previous, Up, Down, Left, Right, Select, Info, Menu, Back |
| Volume/channel | Volume_Up, Volume_Down, Channel_Up, Channel_Down, Mute |
| Playback | DVR, Rewind, Fast_Forward, Skip_Back, Play, Skip_Forward, Record, Pause, Stop |
| Keypad | Digit_0 through Digit_9, Star, Pound |

Volume/mute and Room Off remain in Sources. Transport also includes their remote-key counterparts for the full remote surface, gated like other REST keys. A button press sends at most one command; hardware hold/repeat/programming semantics are not emulated.

### Follow REST metadata

Discover_Commands issues only GET `/api/v1/items/{Room_ID}/commands`. It runs automatically after subscription/reconnect for registered Transport rooms, or on an explicit pulse while connected. No constructor/offline initialization performs network work. Request results use the existing bounded metadata/privacy parser, now generalized to arbitrary positive item IDs. No raw command/media/account payload is exported.

Discovery_Valid_FB indicates a recognized command-list response. Advertised_Command_Count_FB counts normalized definitions; Mapped_Key_Count_FB counts executable mappings. Key_Capabilities_JSON$ identifies all50 keys with state advertised, local_ui or unavailable. Play_Available_FB/Pause_Available_FB/Stop_Available_FB expose the corresponding metadata gates directly. Advertised does not mean physically tested or supported by every selected source.

The actual Room13 REST response advertises95 commands and supports43 mapped keys; see the CP12b document for mappings. A key is enabled only when its mapped name is advertised with no parameter field (the observed zero-argument schema) or an explicitly empty parameter array/object, no omitted definitions and no truncation. Nonempty or unrecognized parameter definitions stay disabled. The four color-key correspondences remain unresolved. Empty/incomplete metadata does not prove that no other interface exists.

Watch/Listen/List are local UI requests: Local_Action$ reports the key and Watch_UI_Request/Listen_UI_Request/List_UI_Request pulse. Wire these to open the appropriate HTML screens. They do not request last-source restore or emulate the physical remote's display. All other unmapped keys report an unavailable diagnostic and send no POST. No free-form command passthrough, new account authentication or playback-state assertion is added. Disconnect clears discovered command capabilities; queued work checks registration identity, connection generation/epoch and current advertisement before POST, with no offline replay.

### First RMC4 test / automatic export

1. Refresh the module database, update Sources and add Transport. Recheck Sources wiring after removed pins. Set both to Gateway_ID=1, Room_ID=13; compile/load for RMC4.
2. Connect the existing shared gateway. Pulse Discover_Commands on Transport if needed. Wait for its discovery diagnostic/Busy_FB to settle.
3. Pulse Export_Inventory on the gateway, then say exported. The report now contains room_transport_commands with normalized room command names/parameter definitions; the existing collector downloads it without another transfer/login. Read this actual response before adding additional SR-260 mappings.
4. Check Sources selection/volume/mute/Room Off still work. Check Watch/Listen/List local request outputs. Do not expect unavailable remote inputs to control a device. Test an advertised mapping only after inspecting the actual report and selecting suitable content/device; acknowledgement alone is not playback feedback.

Local validation: Release build passed with existing generated CS0162 warning. New tests verify metadata/parameter gating, unique50-key catalog, bounded capability JSON, no startup I/O/polling, local UI events, offline rejection, immutable inventory snapshots, disconnect invalidation and refcounts. Sources/media/setup/Audio/Video/socket/inventory regressions passed. All nine wrappers compiled series4 with zero errors/warnings; signatures verify138 Sources cues and66 Transport cues. Package/installed USP/USH/CLZ hashes match. New room command metadata and execution remain RMC4-unverified.

Runtime update (2026-11-15): user confirms the requested Transport test set—navigation arrows, Select, Back, Guide and supported playback controls—works on RMC4. This is not blanket verification of all mapped keys on all devices. Watch/Listen/List remain optional local HTML navigation requests.

