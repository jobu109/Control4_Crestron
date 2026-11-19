**CP13c correction:** Controller entries contain both varName and generic name. The parser now prefers varName; earlier versions preferred the generic label and omitted every entry. Recompile/load the updated library and repeat Off / Spotify / DIRECTV snapshots. Confirm the recorded-snapshot diagnostic reports a nonzero name count before exporting.

**CP13b update:** The first three RMC4 snapshots each contained57 entries but CP13a exported0 because it did not recognize their names/schema. Load the updated library and repeat the three-state test. The actual entry shape is not yet established; CP13b reports a safe first-entry structure, accepts uppercase variable-name lists and additional name fields, and explicitly reads missing source/state values through the filtered endpoint. No pins change.

# Actual active-source feedback: read-only discovery

Revision `Stage1-CP13a-RoomStateInspection`. Adds Sources input Inspect_Room_State. It performs one explicit GET `/api/v1/items/{Room_ID}/variables` using the existing shared authenticated transport. No timer, source command or startup inspection is added. Actual selected-index outputs remain0/invalid until the reported fields are correlated with real source changes.

## Three snapshots on RMC4

1. Update the Sources symbol, inspect wiring and compile/load for **4-Series only**. Parameters remain Gateway_ID=1 / Room_ID=13. Connect.
2. Turn Living Room off, wait for the app to show off, then pulse Inspect_Room_State. Wait for the recorded-snapshot diagnostic.
3. Select Spotify (source65) through the app or Sources module. Once selection settles, pulse Inspect_Room_State again and wait for its diagnostic. Playback content is unnecessary for this state test.
4. Select DIRECTV Stream (source2061), wait for the app/TV selection to settle, then pulse Inspect_Room_State and wait for its diagnostic.
5. Pulse Export_Inventory on the gateway and say exported. The existing collector retrieves the snapshots under room_state_inspections. No Desktop transfer or new login is needed. Please identify any different test order/source IDs used.

The module retains the latest12 explicit snapshots per gateway in memory. They are historical observations, not live source feedback, and include room ID/time. A repeated pulse while the same room inspection is running is ignored. Offline/unregistered rooms are rejected before queueing. Generation/feedback epoch/registration identity prevent stale results after reconnect/reconfiguration.

The report lists safe variable identifiers and value types; only source/device/selection candidate names can carry numeric values (including source100002 or the sentinel-1). POWER_STATE, CURRENT_VOLUME and IS_MUTED can carry independently validated state values. Arbitrary strings, titles, credentials, defaults, account payloads and nested data are suppressed. Source-candidate matching is a diagnostic name heuristic, not proof that a numeric value is a Control4 source ID. At most512 entries and262144 response characters are inspected; omissions and truncation are explicit. Unknown response shapes fail rather than asserting source feedback.

Next, compare all snapshots with the actual room Watch/Listen source IDs. Only after identifying the correct fields and off semantics should production readback and selected-slot validity be enabled. Routing InputID and TV current_input are not substitutes for verified UI source IDs. No WebSocket active-source contract has yet been established.

Local verification: Release build passed (existing generated CS0162 warning); privacy/schema/numeric-ID/sentinel/bounds/offline tests passed alongside Room/event-driven/Transport/inventory checks. All nine wrappers compiled series4 with zero errors/warnings and generated signatures were checked. Package/installed USP/USH/CLZ hashes match. New all-variable response schema and snapshots remain RMC4-unverified.


CP13b inspection behavior: when the variable list supplies names without values, the pulse can perform up to16 distinct source-candidate value GETs plus the three measured-state GETs, sequentially. Duplicate names are not queried repeatedly. Additional candidates are marked truncated, and per-field failures report exception types without raw responses. Metadata keys/types are included for the first entry without property values, so unsupported shapes are visible rather than silently yielding an unexplained empty list. This remains an explicit one-shot diagnostic, not polling.

