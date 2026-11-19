# Room feedback without periodic polling

Revision `Stage1-CP12b-EventDrivenRooms`. Removes the two-second Room fallback timer completely. Sources/Transport signal interfaces and parameters are unchanged.

Room state is refreshed on initial connection/reconnection, module commands, explicit Query, and recognized WebSocket state changes. Fully mapped legacy rooms retain their existing endpoint-triggered readback. For Room modules without complete endpoint mappings, changed TV-style volume/mute/power or matrix audio OutputStatus hints request coalesced REST readback for the unmapped registered rooms. Agent7 Watch/Listen activity is scoped to its explicit room_id. No endpoint volume, mute, routing input or equipment power value is directly assigned to another room's state.

This removes the mapping prerequisite for event-triggered measured feedback; it does **not** establish automatic endpoint/output mapping. One changed AV endpoint may refresh several unmapped rooms because its room ownership is unknown. Each receives only its own measured room variables. There are no scheduled reads when no recognized changes arrive. Identical TV/matrix state snapshots are suppressed for unmapped refreshes; partial snapshots merge into a bounded256-entry scalar-only hint cache. Disconnect clears that cache. Other rooms' light events, non-state devicecommand packets, malformed data, video matrix bindings and stale sessions do not trigger these fallback reads. Known mapped routes retain their previous behavior.

## RMC4 check

1. Recompile/load against the updated CLZ for **4-Series only**; no signal rewiring is required from CP12a.
2. Connect and allow initial discovery/readback to finish. With no relevant AV state changes, verify the repeated two-second Audio query/Busy_FB cycle is gone. Unsolicited changed AV states may still cause a readback; those are event-driven.
3. Change Listen volume/mute in Control4, then repeat on Watch. Confirm Sources measured feedback updates without a Query pulse.
4. Test Room Off/on, an unrelated room, and disconnect/reconnect. Ensure no other room's raw endpoint values appear in this room's outputs. Query remains available if a device supplies no recognized change event.

## Actual REST command export inspected

Inventory generated 2026-11-16T02:14:04Z contains Room13 HTTP200 command metadata:95 recognized/exported definitions, zero omitted and no truncation. Its zero-argument commands omit the parameter field. The transport gate now accepts that observed schema as no advertised arguments while retaining checks against nonempty, omitted or truncated parameter definitions.

The response supports43 mapped SR-260 keys, subject to controller advertisement and subsequent runtime testing. Added mappings include digits→NUMBER_n, Select→ENTER, Previous→RECALL, channel→PULSE_CH_UP/DOWN, DVR→PVR, rewind/fast-forward→SCAN_REV/FWD, skip→SKIP_REV/FWD, navigation/menu/guide/info/back, Control4 and CUSTOM_1..3. These are controller-advertised command names with semantic button mappings; acknowledgement and metadata are not proof of the selected source's physical response.

Watch/Listen/List remain local UI requests. Four color inputs remain unavailable: PROGRAM_A/B/C/D are advertised, but their exact color correspondence has not been established. START/STOP variants for held keys also appear in metadata; hold/repeat behavior is not implemented in this checkpoint. SELECT_AUDIO_MEDIA:* entries advertise mediaid/deselect, but valid media IDs/catalog discovery remain unestablished; they do not make Spotify playlist browsing available automatically.

The sanitized command report is preserved as tests/fixtures/Room13Commands_2026-11-15.json (local test date November15). New tests verify timer absence, event-only readback/coalescing, repeated-state suppression, room-scoped activity, privacy/filtering, no raw cross-room assignment and disconnect cleanup. Transport tests consume the actual95-definition fixture and verify43 mappings. Room/Audio/socket regressions passed. Release build passed with existing generated CS0162 warning; all nine wrappers compile series4 with zero errors/warnings, signatures checked and installed/package files matched. CP12b runtime behavior remains pending.

Runtime update (2026-11-15): user confirms the idle two-second query cycle is gone and app volume/mute feedback still updates on RMC4. Transport execution and the remaining cross-room/reconnect checks are separate and still pending.

Cross-room runtime clarification (2026-11-15): user reports other-room changes do not alter Living Room values; the18→19→20 trace occurred while also changing Living Room volume. Value isolation passes this user test. Extra Living Room REST reads on other-room power changes remain an acknowledged consequence of broad AV-event refresh until endpoint ownership is resolved.

Reconnect runtime update (2026-11-15): user confirms explicit Disconnect→Reconnect restores source availability and measured feedback without manual Query. Unplanned outage/reboot recovery and full token-lifetime renewal remain separate tests.
