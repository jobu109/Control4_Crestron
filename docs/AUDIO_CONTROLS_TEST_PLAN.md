# Audio controls checkpoint

Revision `Stage1-CP8d-AudioControls`. Adds explicit room controls while preserving verified Watch/Listen feedback. No command runs at initialization. Existing five parameters remain 1 / 13 / 682 / 340 / 4003 for Living Room; five leading skips remain on both lists. Input separators now also precede analog and serial inputs. Update the Audio symbol and inspect wiring before compiling/loading the RMC4 program.

| Input | Action |
| --- | --- |
| Volume_Set | Analog change sends room volume, integer 0–100; higher values rejected |
| Mute_On_btn | Pulse to mute |
| Mute_Off_btn | Pulse to unmute |
| Room_Off_btn | Pulse to turn the room off |
| Source_ID$ | Decimal source item ID string, such as `65`; changing it sends nothing |
| Select_Audio_Source_btn | Pulse to select Source_ID$ for Listen |
| Select_Video_Source_btn | Pulse to select Source_ID$ for Watch (video and audio) |
| Query | Existing measured room readback |

Source_ID$ is serial so large IDs such as Digital Media 100002 are preserved. Empty, zero, signed, nonnumeric or overflowing IDs are rejected. A positive ID is not proof the source is available to the room; Director can reject unsupported selections. Source selection can turn the room on, following the supplied Python contract. No generic On button or guessed last-source restore is added.

1. Load the updated symbol/library/program for **4-Series only**. Keep current feedback parameters and connect the shared gateway.
2. With an active Listen source, change Volume_Set to a modest level such as 20, then 25. Confirm actual volume and Volume_FB. Repeat on Watch.
3. Pulse Mute_On_btn and Mute_Off_btn for both paths; confirm actual mute and Mute_FB. A POST acknowledgement is not measured state.
4. Set Source_ID$ to `65`, then pulse Select_Audio_Source_btn to test Spotify. Confirm the selected room/source in Control4. Merely changing Source_ID$ must not select it. Use a separately verified video source (Python research used SHIELD 542) for Select_Video_Source_btn; do not infer a DIRECTV source ID from its friendly name.
5. Pulse Room_Off_btn; confirm room off physically/in Control4 and measured Power_FB when available. Selecting a verified source can turn the room back on.
6. Recheck another-room isolation and reconnect feedback, which remain pending from CP8c. Offline commands are rejected rather than stored for replay after reconnect.

Transport uses existing account authentication and shared gateway queue. Commands match supplied pyControl4 room.py: SET_VOLUME_LEVEL with tParams.LEVEL, MUTE_ON/OFF, ROOM_OFF, SELECT_AUDIO_DEVICE/SELECT_VIDEO_DEVICE with tParams.deviceid, sent to the configured room commands endpoint. Session generation, feedback epoch and configuration identity guard queued work. Accepted commands request room readback; subsequent endpoint events trigger further reads as before. No optimistic feedback, retry/replay, periodic polling, catalog browse or exact-source feedback is added. Immediate readback may precede physical completion; assess subsequent feedback rather than treating acknowledgement as completion.

Local validation passed: Release build (existing generated CS0162 warning), audio payload/range/large-ID/offline checks and audio/capture/socket/shade/inventory regressions; all seven wrappers compiled series4 with zero errors/warnings. Package/installed source/signature/CLZ hashes match and embedded DLL matches Release. New command behavior remains RMC4-unverified.

Runtime update (2026-11-15): user confirms volume control, mute/unmute and Room_Off_btn work on RMC4. Explicit audio/video source selection, another-room isolation/reconnect and measured room-power feedback remain pending.

Further runtime update: user confirms selecting source65 switches the Control4 app to Spotify. No content was selected, so this verifies source activation only; it does not establish autoplay or catalog browsing.
