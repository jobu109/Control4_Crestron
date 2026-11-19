# Automatic scene and macro catalog

The Panel Room Router discovers Advanced Lighting scenes and Macros through the existing authenticated Director connection. **Gateway_ID** and **Room_IDs$** remain the router parameters. The catalog is whole-home and independent of the selected room. It exposes the actions advertised by Director, including macros that affect multiple rooms.

## Serial API

Use the existing **Request$** input (serial 101) and **State$** output (serial 102).

| Request | Behavior |
| --- | --- |
| `{"op":"scenes","q":1}` | Read Director's current scene and macro lists and return page 1 asynchronously. |
| `{"op":"scenes_page","value":2,"q":2}` | Fetch page 2 of the current catalog. |
| `{"op":"scene_activate","revision":"<revision>","slot":1,"q":3}` | Activate slot 1 from the specified catalog revision. |

Increment `q` so repeated requests change the SIMPL serial signal. Reassemble the existing paged frames with room `0` and kind `scenes`. Each decoded catalog has `room_id`, `revision`, and `actions`. Each action has a `slot`, `kind` (`light` or `macro`), and `name`. For example:

```json
{"room_id":0,"revision":"<revision>","actions":[{"slot":1,"kind":"light","name":"House Off"},{"slot":2,"kind":"macro","name":"Main Level Off"}]}
```

The example slots are illustrative. The GUI uses the current returned slots, requests a fresh catalog after reconnect, and sends the active revision with each action. The gateway resolves command IDs and their advertised types internally. It rejects stale revisions and out-of-range slots. HTTP request status appears on the gateway's **Diagnostic$** output. Physical feedback establishes whether the intended devices changed.

Compile and load the matched 4-Series program. In SIMPL Debugger, request the catalog before trying activation. Expect House Off and Main Level Off alongside the other actions advertised by Director. A macro's membership and its effect remain defined in Control4.
