# Video room controls checkpoint

Revision `Stage1-CP9a-VideoRoomControls`. Replaces the Video placeholder with Watch source selection, Room Off and measured room-power feedback. It composes the existing Audio room adapter, sharing registration, room readback, commands and connection rather than adding another transport. No device transport/playback commands, exact-source feedback or catalog browsing are claimed.

Update the Video symbol and set the same room mapping as Audio:

| Parameter | Living Room value |
| --- | --- |
| Gateway_ID | 1 |
| Control4_Room_ID | 13 |
| Watch_Feedback_ID | 682 |
| Listen_Feedback_ID | 340 |
| Listen_Output_ID | 4003 |

Both modules for one room must use identical mappings; conflicts are rejected. Parameters increased from two to five, with five leading skips on each signal list. Input/output digital-to-serial separators are present. Recheck wiring after symbol update. Initialized_FB confirms successful registration, not connection or device power.

1. Compile/load the updated program for **4-Series only**. Connect the shared gateway; verify Audio still works with Video present.
2. Set Video Source_ID$ to `542` (SHIELD TV from the verified Python test), or `2061` (DIRECTV Stream from the researched Living Room Watch list). Changing the string alone must do nothing. Pulse Select_Source_btn; check Control4 and physical TV source selection. Selection may power the room on.
3. Observe Power_FB with Power_Valid_FB high. It comes from room POWER_STATE, never directly from TV powerstate, agent7 activity, or routing InputID. Pulse Query if a room transition produces no matched endpoint event. A universal room-activity contract is not implemented.
4. Pulse Video Room_Off_btn; confirm the Living Room turns off, then check measured room power. Repeat source selection to turn it back on.
5. Check another room cannot change this module's feedback. Disconnect/reconnect should clear validity and resynchronize. These hardware checks remain pending.

Source IDs use the same positive decimal Int32 parser as Audio, preserving IDs above65535. SELECT_VIDEO_DEVICE and ROOM_OFF use the existing room command path and session/configuration guards. No command runs during startup, and offline commands are rejected rather than queued for replay. Power_FB may retain its last value when Power_Valid_FB is low. Diagnostics currently share the Audio room service's wording.

Local validation: Release build passed with existing generated CS0162 warning. Video registration/refcount/reinitialization/power forwarding/room isolation/conflict/offline/disconnect checks and Audio/capture/socket/shade/inventory regressions passed. All seven modules compiled /target series4 with zero errors/warnings; package and installed USP/USH/CLZ hashes match. RMC4 Video behavior remains unverified.

Runtime update (2026-11-15): user explicitly confirms DIRECTV Stream selection, Video Room_Off_btn and Power_FB following on/off with Power_Valid_FB high. Another-room isolation and reconnect checks remain pending. The inventory export itself contained no output trace; this result is based on user confirmation.
