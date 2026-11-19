**Measured selection update:** [ROOM_MEASURED_SOURCE_FEEDBACK.md](ROOM_MEASURED_SOURCE_FEEDBACK.md) supersedes the earlier placeholder selected-index behavior below. These outputs now read actual room fields and map them to slots; CP14a runtime verification is pending.

**Current module name:** Control4 - Room Module - Sources. See [Sources / Transport split](ROOM_SOURCES_TRANSPORT.md) for removed cursor pins and the new SR-260 module.

# Room numbered source slots

Revision `Stage1-CP11c-RoomSourceSlots`. The latest user choice replaces the proposed 64-slot/runtime-parameter scheme with two editable compile-time capacities:

```c
#DEFINE_CONSTANT VideoSources 15
#DEFINE_CONSTANT AudioSources 15
```

These are one-dimensional arrays: `Video_Source[VideoSources]`, not a two-dimensional declaration. Increase either constant in Control4_Room.usp and recompile the SIMPL+ module/program to expose more pins. No C# capacity edit is needed. Keep the source/signature copies synchronized in package SIMPLPlus and installed Usrsplus. Parameters remain Gateway_ID and Room_ID, with two leading skips per direction.

| Signal | Behavior |
| --- | --- |
| Video_Source[n] / Audio_Source[n] | Digital pulse selects source slot n from that list |
| Video_Source_Select / Audio_Source_Select | Analog change selects one-based slot n; zero/out-of-range is rejected |
| Video_Source_Name$ / Audio_Source_Name$ | Serial change selects by trimmed, case-insensitive exact name among defined slots |
| Video_Source_Available[n] / Audio_Source_Available[n] | High when current valid discovery has that source slot; low otherwise |
| Video_Source_Name_FB$[n] / Audio_Source_Name_FB$[n] | Friendly name for slot n; empty when unavailable |
| Video_Source_Overflow_FB / Audio_Source_Overflow_FB | More sources discovered than the respective defined capacity |
| Video_Source_Overflow_Count_FB / Audio_Source_Overflow_Count_FB | Number of additional discovered sources beyond that capacity |
| Source_Overflow_Message$[1] | Readable capacity warning; empty when no overflow |
| Video_Source_Selected_FB / Audio_Source_Selected_FB | Actual selected slot; currently zero with corresponding validity low |
| Video_Source_Selected_Valid_FB / Audio_Source_Selected_Valid_FB | Currently low: exact actual Control4 active-source feedback is not established |

There are15 fixed slots for each list in this build. After Connect/discovery, populated slots go available=1 and names fill in. Remaining slots stay available=0/name empty. Disconnect, invalid discovery or a shrinking list clears availability/names. Overflow reports do not silently substitute a different source into a numbered slot. Full discovered totals remain in the existing source-count outputs; existing JSON lists retain all discovered sources and paging.

Slots follow the controller's source-list order and may change when discovery is refreshed. Use the accompanying names/availability rather than assuming slot1 always means a specific service. Audio slot1 and Video slot1 belong to different lists. Duplicate names within the enabled list are rejected as ambiguous; digital/analog index selection remains unambiguous. Repeating an unchanged analog or serial value does not produce a CHANGE event; pulse the corresponding digital selection to repeat it.

Selected-index feedback must never mirror a requested command or browser cursor. At the user's request it requires actual Control4 source feedback. Until the exact active-source contract is established, those outputs stay0/invalid even when selection succeeds. Power/volume/mute feedback remain independent and measured. Content catalogs and transport execution remain unavailable.

The overflow message is a single-element terminal serial array so both preceding source-name arrays generate all fixed pins. Do not remove or reorder that terminal array without checking the signature: the compiler otherwise makes the final name array expandable.

## Test

Update/reload the Room symbol, inspect wiring, compile/load for RMC4 with **4-Series only**, then Connect. Check each available source has a name. Test slot1 via digital pulse, analog value1 and the matching name; verify the app selects the intended source. Test both modes, unavailable slots, duplicate-name rejection where applicable, overflow, discovery refresh and disconnect clearing. The actual-selected validity outputs must remain low.

Local verification passed: Release build (existing generated CS0162 warning), Room index/name/bounds/ambiguity/overflow/availability/disconnect checks and setup/Audio/Video/socket regressions; all eight wrappers compile series4 with zero errors/warnings. Signature checks verify154 Room cues including every numbered pin. An isolated17-video/19-audio prototype also compiled and exposed all six selection/availability/name endpoints at those higher indices. Installed/package USP/USH/CLZ hashes match and embedded DLL matches Release. New slot behavior awaits RMC4 verification.


Runtime update (2026-11-15): user explicitly confirms digital slot-pulse, analog-index and serial-name selection work on RMC4. Actual selected-source feedback and other untested edge cases remain separate.

