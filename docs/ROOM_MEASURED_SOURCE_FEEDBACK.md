# Measured selected-source feedback

Revision `Stage1-CP14a-MeasuredSourceFeedback`. Uses the existing Sources selected-index and validity pins; no new interface pins or polling timer.

## Evidence from the three RMC4 snapshots

Inventory generated2026-11-16T03:07:05Z contains three Room13 snapshots, each57 entries with55 safe identifiers exported and2 omitted. Values were inline; no extra filtered reads were needed.

| State | POWER_STATE | CURRENT_AUDIO_DEVICE | PLAYING_AUDIO_DEVICE | CURRENT_VIDEO_DEVICE |
| --- | ---: | ---: | ---: | ---: |
| Room Off | 0 | 0 | 0 | 0 |
| Spotify | 1 | 100002 | 65 | 0 |
| DIRECTV Stream | 1 | 0 | 0 | 2061 |

Spotify's CURRENT_AUDIO_DEVICE is the generic Digital Media renderer, not the actual service. PLAYING_AUDIO_DEVICE identifies the Spotify65 selection. CURRENT_SELECTED_DEVICE also reports renderer100002 for Spotify, so it is not used as an exact service source. CURRENT_VIDEO_DEVICE supplies the DIRECTV2061 source. No IDs13/65/100002/2061 are hardcoded into production source mapping; these are observed test values only.

## Output semantics

Audio_Source_Selected_FB and Video_Source_Selected_FB are one-based positions in the room's current discovered lists, matching the numbered availability/name pins. They are not Control4 item IDs. Each has independent *_Selected_Valid_FB.

- Measured Room Off: both indices0, both validity flags1 (known none/off).
- Measured Spotify: Audio is the index whose name/ID represents Spotify; Video0 with validity1.
- Measured DIRECTV: Video is the matching DIRECTV slot; Audio0 with validity1 because no Listen service is selected. Its video audio remains part of the Watch path.
- Missing/undefined/duplicate/unresolved fields, an unknown source ID or a selection beyond the defined source capacity: relevant index0/validity0. A generic renderer without a resolved playing service is not reported as Spotify or another service.
- Disconnect or a source-changing command invalidates selected feedback until a fresh measured read. Commands do not echo their requested index into feedback.

On source-list refresh/capacity changes, cached measured IDs are remapped to the current list order. Exact-slot validity requires a matching entry within the compile-time capacity. A valid empty/off selection needs no nonzero slot. Other fields' validity remains independent.

## Readback and event behavior

Existing Room readback adds one GET with varnames=POWER_STATE,CURRENT_AUDIO_DEVICE,CURRENT_VIDEO_DEVICE,PLAYING_AUDIO_DEVICE. This uses the established comma-separated filtered-variable query mechanism. Actual multi-variable response shape/behavior still needs RMC4 testing. It runs for registered Sources modules only, alongside initial/reconnect, command, Query and recognized WebSocket-triggered room reads; there is no scheduled polling.

Source field versions reject reads overtaken by new source/AV hints or commands; generation/epoch/registration identity reject stale sessions. Failure invalidates source feedback without invalidating successful volume/mute/power readings. The raw endpoint values and command acknowledgements are never treated as selected-source IDs.

A source change through the Control4 app must produce a recognized event to trigger readback. Complete source-change notification coverage is not yet established. If a selected output stays stale after an app change, pulse Query and report whether it then updates; that distinguishes event coverage from variable parsing. No guessed WebSocket field or polling fallback is added.

## RMC4 test

1. Recompile/load the matching updated Sources wrapper and CLZ for **4-Series only**. Existing pins/parameters remain unchanged. Connect and wait for initial readback/discovery.
2. Turn room off: both selected indices should become0 with validity high.
3. Select Spotify through a digital slot, analog index or name: Audio_Source_Selected_FB should match Spotify's slot, Audio validity high; Video should be0/valid.
4. Select DIRECTV: Video_Source_Selected_FB should match its slot with validity high; Audio should be0/valid. Compare with the named source outputs, not hardcoded slot assumptions.
5. Repeat source changes through the Control4 app, then Disconnect/Reconnect. Confirm measured values recover without requested-source echo or a recurring idle query cycle. If an app change is missed, note whether Query corrects it.

Local checks passed using the actual sanitized snapshot fixture: Off/service/renderer/video distinctions, unknown/duplicate fields, independent validity, slot/capacity mapping, stale version/epoch rejection, no command echo and disconnect clearing. Room/event-driven/Audio/Transport/inspection regressions passed. Release build passed with existing generated CS0162 warning; all nine wrappers compile series4 with zero errors/warnings and signatures checked. Installed/package USP/USH/CLZ match. New selected-source runtime behavior remains pending.

Runtime update (2026-11-15): user confirms the requested Off/Spotify/DIRECTV selected-index and validity test, including source changes. No missed-change/Query fallback was reported. Other services/rooms and untested edge cases remain separate.
