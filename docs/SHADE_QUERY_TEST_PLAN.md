# Shade position query checkpoint

Current revision: `Stage1-CP6d-ShadeControl`. Middle Shade proxy 226 readback was verified on the RMC4 in CP6c. Open, Close, Stop, target level and event-triggered shade refresh are now built and 4-Series compiled; their processor tests remain pending. All operations share the existing gateway, authentication and REST transport. The lighting WebSocket path remains in place.

## Current control and automatic-feedback test

1. Use the current matching CLZ and gateway/light/shade wrappers, compiled for **4-Series only**. Update the shade symbol and check wiring: two leading skips, Query, Open_btn, Close_btn, Stop_btn; analog Level_Set. Existing output positions remain unchanged. Gateway_ID=1 and Proxy_ID=226.
2. Connect. Registered shades are queried on subscription startup/reconnect, without pressing Query. No network activity is added to program startup.
3. Change Middle Shade through the Control4 app. A registered shade OnDataToUI event triggers a read of its already-verified Level variable. Expect `Shade 226 position <n>% (WebSocket-triggered REST readback)` and updated Position_FB/endpoint flags. Bursts are combined into one active read plus a pending refresh; this is event-driven readback rather than direct interpretation of an unknown event position field.
4. Pulse Open_btn and verify physical opening, then Close_btn and verify closing. Set Level_Set to 45 and verify the target position. Scale: 0=closed, 100=open. Test Stop_btn while moving. A command acknowledgement alone is not proof of movement and does not set feedback to the requested target.
5. Disconnect/Connect and check shade validity and resynchronization. Keep the verified lighting checkpoint archive for rollback.

The command payloads follow pyControl4 C4Blind: Open uses SET_LEVEL_TARGET:LEVEL_TARGET_OPEN, Close uses SET_LEVEL_TARGET:LEVEL_TARGET_CLOSED, level uses SET_LEVEL_TARGET with tParams.LEVEL_TARGET, and Stop uses STOP. All use the existing async command envelope. No post-command timer or periodic polling was added. Query remains available manually. Actual command acceptance and event behavior on Middle Shade require the test above; no direct motor-moving output is inferred.

## Earlier read-only test reference

The protocol reference is the `C4Blind.get_level()` implementation in the original Python archive and [pyControl4 blind.py](https://github.com/lawtancool/pyControl4/blob/master/pyControl4/blind.py). It requests `/api/v1/items/{proxy}/variables?varnames=Level`. Position is 0% closed and 100% open. This reference has not yet been validated against a shade in this EA-5 project.

1. Keep the lighting checkpoint backup. Use an isolated test program and the current CLZ with the matching wrappers, compiled for **4-Series only**.
2. Configure the gateway account parameters and Gateway_ID as before. Add Control4_Shade with the same Gateway_ID and the actual shade **Proxy_ID** from Composer. No shade ID is preselected or hardcoded.
3. Update the shade symbol after compilation and check its wiring. Its two parameters require two leading skips in both directions. Outputs are Initialized_FB, Valid_FB, Fully_Open_FB, Fully_Closed_FB and Position_FB. Initialized_FB only means initialization returned; it is not device connectivity.
4. Connect the shared gateway, then pulse the shade Query input. Expect `Shade <id> position <n>% (REST readback)`, Position_FB matching the actual position, and Valid_FB=1.
5. Change the shade using Control4, then press Query again. Check open (100), closed (0), and an intermediate position if supported. Fully_Open_FB/Fully_Closed_FB are derived from the validated percentage. They do not indicate motor movement.
6. Disconnect the gateway and verify Valid_FB clears. Invalid data, missing/undefined Level, and REST failures do not produce valid position feedback.

Capture only the nonsecret diagnostic and outputs. A successful light test does not establish shade protocol compatibility. If the shade query fails, record the diagnostic and shade model/proxy ID before implementing movement commands or guessing variable names.

Local verification: Release build, `tests/ShadeQueryChecks.ps1`, lighting/socket regression checks, and gateway/light/shade 4-Series compilation. Processor verification is pending.

Compiler cache note: SPlusCC used a stale installed/cache DLL even when given the current local CLZ and /usersplusfolder. The old installed CLZ is backed up at `_baseline/installed_Control4Gateway_before_CP6a.clz`, with the previous compiler cache in `_baseline/compiler_cache_before_CP6a`. The installed CLZ and cached DLL were updated to the CP6a assembly before successful compilation. When rolling back, restore matching library/cache inputs as well as the processor program; rebuilding wrappers from stale inputs can yield misleading compile results.
