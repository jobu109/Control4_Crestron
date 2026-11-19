**Historical CP8a plan. Superseded by [CP8c dual-endpoint testing](AUDIO_DUAL_ENDPOINT_TEST_PLAN.md); use its five-parameter configuration with the current build.**

# Audio room readback checkpoint

Revision: `Stage1-CP8a-AudioReadback`. Read-only C# room state and volume/mute event mapping. Source selection, volume/mute writes, room off and content browsing are not added in this checkpoint.

Set the Audio symbol parameters:

| Parameter | Living Room test value |
| --- | --- |
| Gateway_ID | 1 |
| Control4_Room_ID | 13 |
| Feedback_Device_ID | 682 |

Room ID 13 is the REST target. Feedback ID 682 is the separately verified Living Room AV endpoint; it must not be assigned to other rooms without evidence. Feedback_Device_ID=0 disables direct event matching but permits REST queries. A room with conflicting endpoint mappings is rejected instead of overwriting another adapter's mapping.

## Test on RMC4

1. Keep the working processor-program backup. Compile/load against the current CLZ for **4-Series only**, then update the Audio symbol. Changed Audio source, matching CLZ and generated signatures are synchronized in both the package and `C:\Users\Public\Documents\Crestron\SIMPL\Usrsplus`.
2. Recheck wiring: Audio now has **three** parameters and three leading skips in each direction. Query is digital input 4. Output type separators appear before analog Volume_FB and serial Diagnostic$. Initialized_FB confirms successful room registration, not online state.
3. Configure 1/13/682 as above. Connect the shared gateway. Audio registers for initial/reconnect synchronization; it does not create an independent session or token. If needed, pulse the Audio Query input.
4. Observe Power_FB, Volume_FB (0..100), Mute_FB and their separate Power_Valid_FB, Volume_Valid_FB and Mute_Valid_FB. Valid_FB is high only when all three have valid measured state. Diagnostic$ identifies a field failure without printing raw responses.
5. Change Living Room volume and mute through Control4. The configured endpoint's OnDataToUI volume_level and Boolean mute fields should update Volume_FB and Mute_FB without Query. Verify another room's changes do not affect this module.
6. If testing room power through the Control4 app, observe Power_FB from measured POWER_STATE REST readback. A configured endpoint powerstate event only triggers a requery: it is **not** directly assigned as room power. Manual Query remains available if no correlated endpoint event arrives. This checkpoint adds no universal room-power event contract, agent-7 activity signal or exact source feedback.
7. Disconnect/Connect and verify all validity flags clear and state resynchronizes. Values can retain the last measurement while validity is low; assess each Valid output rather than interpreting a retained zero/one as current state.

Queries use verified pyControl4 variables POWER_STATE, CURRENT_VOLUME and IS_MUTED at `/api/v1/items/{room}/variables?varnames=...`. Each field is read separately so an unavailable field does not invalidate a successful unrelated field. Invalid/missing/undefined values are rejected. Boolean REST state accepts Boolean, 0/1 and string Boolean/0/1 forms; volume accepts integral 0..100 values. Unrecognized formats produce invalid state, not guessed truthiness.

One in-flight query per room combines repeated triggers. Live volume/mute events increment field versions so an older REST response cannot overwrite a newer event. Generation, feedback epoch and registration identity checks reject results after disconnect/reconfiguration. Registrations are shared/ref-counted; room 13 and endpoint 682 are test configuration values, not constants in the transport.

The Audio adapter's previously placeholder Initialize contract now takes three arguments and returns successful registration as an integer flag; its matching wrapper is updated in this checkpoint. Do not mix the old two-parameter placeholder wrapper/signature with this build. Other device interfaces are unchanged.

Local verification: Release build with existing generated CS0162 warning; audio parsing/endpoint mapping/independent validity/stale read/power-hint/conflicting registration/disconnect checks; existing lighting/shade/media/inventory regressions; all seven wrappers compiled for series4 only with zero errors/warnings. No Audio query or live event result has yet been demonstrated on RMC4. The supplied Python research is protocol evidence, not proof this C# module runs end-to-end.

