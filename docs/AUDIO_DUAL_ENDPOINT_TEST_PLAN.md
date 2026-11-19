# Audio Watch/Listen readback checkpoint

Revision: `Stage1-CP8c-DualAudioReadback`. Supersedes the CP8a single-endpoint test plan. Audio remains read-only.

Update the Audio symbol from the synchronized Usrsplus source, then set:

| Parameter | Living Room value |
| --- | --- |
| Gateway_ID | 1 |
| Control4_Room_ID | 13 |
| Watch_Feedback_ID | 682 |
| Listen_Feedback_ID | 340 |
| Listen_Output_ID | 4003 |

There are five leading parameter skips on both signal lists. Query is digital input 6. Recheck existing wiring after updating the symbol. Output separators remain between digital/analog/serial types. Initialize returns registration success; it does not assert connection status. Listen endpoint/output must both be configured or both zero. Watch ID zero disables Watch event matching. Conflicting mappings for one room are rejected.

TV volume/mute/power events and matrix OutputStatus events for output 4003 with IsVideoBinding=false trigger measured room REST reads of POWER_STATE, CURRENT_VOLUME and IS_MUTED. Endpoint values are never assigned directly to room feedback. This handles either endpoint emitting while the other supplies room audio without guessing the active mode. Matrix output 4003 was correlated with Living Room Listen in the controlled capture; verify isolation from other rooms on hardware. There is no timer polling, source selection, volume/mute write or new power command.

1. Rebuild/load the SIMPL Windows program for RMC4 using the matching library and updated Audio symbol. All seven wrappers have been locally compiled for **4-Series only**.
2. Connect the shared gateway; check Initialized_FB and the individual Power_Valid_FB, Volume_Valid_FB and Mute_Valid_FB. Pulse Query if needed. Valid_FB requires all three measured fields.
3. Start Living Room Spotify through Control4. Change volume and mute in the app. Confirm Volume_FB and Mute_FB follow without pulsing Query.
4. Turn the room off, start DIRECTV Stream, and repeat volume/mute changes. Check measured Power_FB as well. Equipment power and matrix InputID=-1 are hints, never direct room-power values.
5. Change another room using the same matrix; confirm Living Room feedback does not change to that room's values. Inactive TV events may cause a refresh, but must not assign TV volume to Listen feedback.
6. Disconnect/reconnect. Validity must clear and then measured state must resynchronize. Last values may remain while invalid. Room power changes without a matching endpoint event can require Query; a universal room activity contract is not implemented.

Repeated events coalesce into one in-flight query per room with a follow-up when dirty. Field versions reject reads started before a newer endpoint hint; connection generation, epoch and registration identity reject stale results. Field failures invalidate only that field.

The capture filter now retains Boolean or numeric 0/1 MuteFlag values. Earlier capture showed the field name but omitted its value, and the subsequent RMC4 capture confirms Boolean true/false MuteFlag values. Production mute uses measured room IS_MUTED instead. InputID 3001 is observed routing metadata, not proof of a Spotify source-ID mapping.

Local validation: Release build passed with the existing generated CS0162 warning; audio/capture/socket/shade/inventory checks passed; all seven SIMPL+ modules compiled with series4, zero errors/warnings. Package and installed USP/USH/CLZ hashes match, and the CLZ DLL matches Release. The user subsequently confirmed Volume_FB and Mute_FB follow both Listen and Watch on RMC4. Another-room isolation, reconnect and room power checks remain pending.

