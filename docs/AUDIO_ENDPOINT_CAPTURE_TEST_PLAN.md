# Living Room Watch/Listen endpoint capture

Revision: `Stage1-CP8b-AudioEndpointCapture`. The inventory identifies matrix driver 339, matrix proxy 340 and TV proxy 682. The user's room has different audio endpoints for Watch and Listen. CP8a's single-endpoint mapping is not yet a complete production solution for this room.

The supplied RMC4 Query trace confirms room 13 REST readback completed with Volume_FB=18. The trace does not separately display the power/mute validity values. Neither that query nor inventory establishes a matrix output/channel or a live Listen event contract.

## Capture without transferring another file

1. Load the current matching library/program compiled for **4-Series only** and update the gateway symbol. The new digital input **Capture_Audio_Feedback** is appended. Existing parameters/skips/type separators are unchanged. Updated source/signatures/CLZ are synchronized in the package and installed Usrsplus folder.
2. Connect and wait for `WebSocket subscription started`. Pulse **Capture_Audio_Feedback** once. Expect `Audio capture started (60s): room 13, TV 682, matrix 339/340; then Export_Inventory`.
3. During the next minute, use the Control4 app to select a Living Room **Listen** source and make small volume changes and a mute/unmute change. Optionally compare a Watch source in the same window; note the order. The gateway sends no source/volume/power command for this test.
4. After the minute, pulse **Export_Inventory**. Wait for its saved diagnostic, then say “exported.” The inventory now contains **audio_feedback_capture**, and the existing SFTP collector downloads that same known inventory file automatically. No collector restart, extra password prompt or Toolbox transfer is needed.

Capture remains off until explicitly started. It passively examines events already delivered by the single shared subscription. The diagnostic scope is deliberately fixed to the researched room 13, TV 682, matrix driver/proxy 339/340 and activity agent 7; these constants do not configure production endpoint routing.

Recorded data is limited to timestamps, endpoint IDs, a small event-type allowlist, structural key names and selected numeric/Boolean volume/mute/output/channel/binding fields. Known output/channel containers are summarized with bounded samples. String payloads, command contents, credentials, profile fields, arbitrary media strings and values of unknown fields are not exported. Agent-7 activity is included only for room 13 and Watch/Listen activity with a Boolean active field. Other rooms' matrix hardware channel events may be recorded to determine discrimination; no room names/history are included.

The buffer is bounded to 256 events/131072 serialized characters and 60 seconds. The exported section includes state/count/drop indicators and cloned snapshots. An early inventory export gives a partial active snapshot; export again after completion. Disconnect/reauthentication changes mark the capture cancelled; reconnect and start a fresh capture for a coherent test.

This checkpoint does not route matrix 340 volume/mute directly into room 13, infer a matrix output from its ID, or select between TV and matrix based on whichever event arrived last. The goal is to establish the correct output discriminator and Watch/Listen activity evidence before changing production mappings. Existing Audio Query remains available for measured room state.

Local verification: explicit-start/routing/privacy/field filtering, other-room/stale-session rejection, expiry, immutable snapshots and buffer-bound checks; existing Audio, socket, inventory and shade regressions; Release build with existing generated CS0162 warning; all seven wrappers compiled for series4 only with zero errors/warnings. Capture behavior and matrix event schema require the processor test above.
