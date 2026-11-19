**2026-11-17 verified:** Automatic Spotify saved-preset list discovery (five named entries) and HEAVY playback through the cached selector are user-confirmed on RMC4. See [ROOM_SERVICE_PRESETS.md](ROOM_SERVICE_PRESETS.md). Driver/source IDs remain configured; full Spotify library browsing is separate.

# Spotify service presets

## Current target

The user wants streaming-service content, starting with Spotify, rather than the native local media catalogs. On 2026-11-16 the user confirmed that Listen → Spotify in the Control4 app shows **saved presets**, not full browsing/search. The earlier missing broadcast-audio result is explained: only playlists were loaded. It was not a station test failure.

The next runtime test is Spotify preset discovery/recall. The native Blues / Blues Win!!! entries must not be presented as Spotify presets or used as their IDs without a verified relationship. CP15c native selection is retained but its playback test is no longer the priority.

## Captured controller evidence

The existing media_commands report for item65 (Spotify) contains one command:

```json
{"command_name":"SelectPreset","parameter_definitions":[{"name":"Preset"},{"name":"Room"}]}
```

The complete normalized command list has no omitted commands or parameters. It has no browse/search command, parameter types, preset choices or parameter bounds. The item capability report provides no established browse GET route. This does not prove that the controller has no other interface; it establishes the limit of the captured evidence.

The metadata exporter intentionally discards default/enum/description values. Therefore absence of choices in this normalized report does not establish absence of choices in the original response. Before implementing preset writes, inspect the actual SelectPreset definition for a bounded preset list or typed selector. Do not guess whether Preset is a display name, slot number or opaque identifier. Likewise, verify the Room selector encoding rather than assuming it from its label.

## Product behavior

Control4's official [Spotify guide](https://www.control4.com/docs/product/spotify/user-guide/english/latest/) describes Spotify Connect and saved preset recall from Listen → Spotify. This supports targeting the installed preset interface. It is not documentation of the external REST SelectPreset argument encoding.

Once discovery is established, expose a service-specific named list to the HTML widget, with identifiers stored as strings, room/source identity and a revision. A selection must resolve against that list and revalidate current controller command metadata. Browsing must not start playback. Acceptance and actual playback are separate states. No invented twenty-slot availability, local-media ID substitution or automatic preset execution during diagnostics.

The user supplied the exact displayed Spotify preset name HEAVY on 2026-11-16. A search of the collected reports, research JSON/text and fixtures found no HEAVY entry or associated selector. Do not ask for its name again. The remaining missing evidence is the exact selector format and discovery route. Obtain controller metadata through the module/automatic report collector, without manual token entry or Python on the processor. A display name alone does not establish the wire value.

## Full Spotify library browsing is separate

A direct Spotify adapter could use [Spotify authorization](https://developer.spotify.com/documentation/web-api/concepts/authorization) and [PKCE](https://developer.spotify.com/documentation/web-api/tutorials/code-pkce-flow) for a separate account connection. [Playback control](https://developer.spotify.com/documentation/web-api/reference/start-a-users-playback) requires Premium and an eligible Spotify playback device. Control4 source IDs are not Spotify device IDs; EA-5 device eligibility must be measured before promising direct playback. This alternative is not implemented or required merely to investigate the current Control4 presets.

No binary or SIMPL+ interface changes accompany this investigation. Existing 4-Series-only/net472 architecture, account-only authentication, local TLS policy and event-driven room feedback remain in place.

## CP16a module inspection

Revision Stage1-CP16a-SpotifyPresetSchema adds targeted structure inspection to the existing gateway Export_Media_Commands pulse. No new SIMPL+ pins or parameters are required. Load the matching updated library/program, Connect, pulse Export_Media_Commands and wait for the saved-report diagnostic. The automatic collector retrieves the file; no Desktop transfer is needed.

The Spotify entry adds spotify_preset_parameter_schemas, showing bounded parameter/choice structure and recognized parameter names, schema types and required flags. Defaults, descriptions and scalar choice values are suppressed. This step determines whether the original response contains choices or a typed selector; it does not yet resolve HEAVY or promise a usable preset catalog. No POST, playback, polling, additional request endpoint or authentication change is introduced.

Local Release build, Spotify schema/privacy/bounds checks, media-command regressions and native-selection regressions pass. Hardware export of this new schema remains pending.
## CP16a runtime result and CP16b correction

Export generated 2026-11-16T21:46:48Z confirms the new schema runs on RMC4. Preset and Room both have String type definitions outside the original scalar-type allowlist. Preset has one omitted field. Room has one omitted field and items=[String] (one entry); this is structure, not proof of a catalog or valid selector. No HEAVY mapping is established. Fixture: tests/fixtures/SpotifySchema_CP16a_2026-11-16.json.

Stage1-CP16b-SpotifySchemaTypes retains bounded vendor type strings and nonsensitive omitted field names/types, while still suppressing their values. Rebuild/load, then repeat Export_Media_Commands. No new pins or preset execution. Local build and schema/privacy/media-command checks pass; runtime export of the correction is pending.

## CP16b runtime result

Export generated 2026-11-16T23:56:13Z confirms Preset type CUSTOM_SELECT:ListPresetsForProgramming, with additional search:String field; Room type DEVICE_SELECTOR, items containing one string, and multiselect:Boolean. No preset entries/IDs were returned. Fixture: tests/fixtures/SpotifySchema_CP16b_2026-11-16.json.

Official DriverWorks CUSTOM SELECT documentation describes the suffix as a global driver Lua function invoked by Composer to generate a selectable list. Display labels can differ from underlying values. Therefore HEAVY cannot safely be substituted for an unknown selector. ListPresetsForProgramming is not an advertised REST command or a verified REST URL. Public documentation reviewed establishes the internal mechanism but no external REST invocation route. More schema-only exports will not resolve that gap; the next investigation must establish an accessible list invocation interface (driver/Navigator or documented REST), before any custom bridge or direct Spotify integration can be promised. No new binary changes or additional RMC4 export requested for this result.

Reference: https://snap-one.github.io/docs-driverworks-fundamentals/#using-the-custom-select-property

## Read-only driver-context diagnostic

Prepared tools/Inspect-SpotifyHeavy.lua. In Composer Pro, select the Spotify Connect driver associated with source65, open its Lua tab/command area and run the script. It invokes only ListPresetsForProgramming with the empty current value and a deferred completion callback. It does not call SelectPreset, ExecuteCommand or UIRequest, select any source, or change properties. It prints only HEAVY matches and their bounded string selectors; unrelated preset values and errors are suppressed. Supply the printed result for the next adapter step. If unavailable in that context, retain the diagnostic message rather than trying arbitrary commands. This temporary investigation step is not the final user/installer workflow.

Official CUSTOM SELECT documentation supports synchronous list returns and a second-argument callback for deferred results. A list entry may be a string or a text/value table. The script handles those documented forms. Installed Control4 DriverEditor301 Lua compiler syntax check and offline tests passed (sync/deferred completion, duplicate callback, exact match and output filtering). Actual invocation on the controller is pending. No C#/SIMPL+ changes were made; current installed revision remains CP16b.

The official SendUIRequest API requires a matching UIRequest handler in the receiving driver. It is not a general way to call arbitrary global Lua functions; a bridge alone cannot be assumed to expose ListPresetsForProgramming. Reference: https://snap-one.github.io/docs-driverworks-api/#senduirequest

## Composer-free next check

The user does not have Composer Pro available and requests external list access. Defer the Lua diagnostic. No verified external CUSTOM_SELECT invocation endpoint was found in the public documentation reviewed; do not claim impossibility or add guessed URLs.

A previously verified explicit GET room route, /api/v1/locations/rooms/13/media/broadcast_audio, returned181 entries in CP15a inspection. Only the playlist list was subsequently exported with names. Use the already installed Sources Browse_Media_Audio_Stations pulse, then Export_Inventory, to inspect that existing named list for HEAVY. This is a read-only test of whether a known controller catalog exposes the saved service item; no new binary, Composer, credentials or manual file transfer is needed. Do not select an entry or infer source ownership merely from a name match. If absent, this rules out that normalized list for this preset, not all external interfaces.

The Rithum integration vendor documents source-specific Control4 Media Database favourites, as distinct from full service browsing. This is a first-party account of its own driver and an investigation lead, not proof that Spotify HEAVY appears in this EA-5 broadcast_audio route: https://support.rithumhome.com/hc/en-gb/articles/36369019973521-Control4-User-Guide

## Composer-free HEAVY match found

User export generated 2026-11-17T00:04:56Z (Nov16 local) contains a Room13 audio_stations catalog fetched at 2026-11-17T00:04:39.536164Z. All181 entries have names, with zero omitted and no truncation. Exactly one entry matches HEAVY: native broadcast-audio media ID171. selection_supported=true from CP15c command metadata eligibility. Saved fixture: tests/fixtures/Room13AudioStations_2026-11-16.json.

This is the first external named-list match for the user's known Spotify preset. The normalized list does not retain source ownership, so exact Spotify association and actual playback still require user confirmation. ID171 is a native media ID for SELECT_AUDIO_MEDIA:BROADCAST_AUDIO, not an established SelectPreset(Preset) value and not a source proxy ID. Do not substitute171 into SelectPreset.

No new code or rebuild needed: with the audio_stations list still loaded and Media_Select_Available_FB high, set Media_ID$ to171 and pulse Select_Media_ID_btn. If the list was replaced or the gateway reconnected, Browse_Media_Audio_Stations first and wait for readiness. Confirm HEAVY actually plays from Spotify; accepted-command diagnostics alone do not prove this. This is explicit selection through the already implemented CP15c path and measured readback, not a guessed custom-list endpoint. Keep the Lua diagnostic deferred.

## HEAVY selection HTTP400; CP16c diagnostics

User attempted media ID171 through Select_Media_ID_btn with readiness high. The controller returned HTTP400; playback has not been verified. Current payload is SELECT_AUDIO_MEDIA:BROADCAST_AUDIO with numeric tParams.mediaid171 and async=true. Metadata also lists deselect, which was omitted; no evidence yet identifies this as the cause. Do not mark native preset playback successful or automatically retry with guessed payload variants.

Stage1-CP16c-MediaValidationDetail reads at most4097 response characters on an HTTP400 from explicit native media selection and emits only recognized validation keywords. It never echoes the response, tokens or arbitrary server values. This can distinguish command, parameter/mediaid/deselect, required/type errors when the server includes those terms. Non-JSON/empty/unrecognized responses get a fixed diagnostic. No write/polling/selection-contract changes were introduced. Release build, classification/privacy/bounds checks, native selection regression, all nine series4 compilations and signature checks passed; installed Usrsplus hashes match.

Next: rebuild/load matching library/program, Connect, Browse_Media_Audio_Stations, wait for Media_Select_Available_FB=1, set Media_ID and pulse Select_Media_ID_btn once. Provide the extended diagnostic. The original source remains unchanged; command acceptance and measured playback are still separate.

## CP16d response-lifetime fix

The CP16c hardware attempt still returned HTTP400 with server detail unavailable. Inspection found an avoidable diagnostic defect: FailureKind closes HttpWebResponse, and CP16c called it before NativeMediaFailureDetail. Stage1-CP16d-MediaErrorResponseOrder routes HTTP400 through a single formatter that captures status and reads/classifies the body before disposing the response. General failure handling remains unchanged. An actual loopback HTTP400 test verifies the complete WebException-to-diagnostic path with required/deselect terms and sensitive-value suppression; build and native-selection regressions pass. This fixes diagnostics only; it does not establish why the controller rejects selection171.

## CP16e structured controller errors

CP16d runtime returned HTTP400 with JSON but no recognized validation terms. The keyword classifier was too restrictive to identify this server failure. CP16e replaces it with bounded recognized error fields (including nested error/code/message/error_description), up to420 diagnostic characters, with URL/bearer/JWT/credential-assignment redaction. Unrelated fields are excluded. This reports numeric codes and messages even without generic validation words. The actual local HTTP400 regression, structured code/message/redaction tests, Release build and native selection regressions pass. The playback request itself is unchanged; its rejection cause and Spotify playback remain unverified.

## CP16f media-ID list correction

Controller error now captured: error=cond.params.push is not a function. This indicates array/list handling failure, but does not independently prove which internal value caused it. A request mismatch was found: mediaid is advertised as type list, while the module serialized a scalar. CP16f changes only tParams.mediaid to a one-element numeric array, [171] for HEAVY. Command name, explicit selection/membership checks, omitted deselect and async=true remain unchanged. Tests now require array shape for all five supported mappings and the exact [171] example. Local Release build, selection regressions and actual-loopback error diagnostics pass. Runtime acceptance/playback of the corrected payload is pending; do not claim the server error is resolved until measured.

## CP16f unchanged server error; isolate request phase

User reports the same HTTP400/error=cond.params.push is not a function after the array change. This disproves scalar-to-array as a sufficient fix; do not declare list encoding resolved. SelectNativeRoomMedia performs fresh command-metadata GET before the POST, and its catch currently labels failures from either phase identically. The reported error does not yet prove a playback POST was reached. Next use existing same-room Transport Discover_Commands (GET /api/v1/items/13/commands only). Success yields Room13 REST command metadata collected; failure yields Room transport discovery failed. Capture that diagnostic before any further payload change. No rebuild or playback attempt required.

## CP16g expanded media-command contract

User independently pulsed same-room Transport Discover_Commands: metadata GET succeeded,95 commands/43 mappings. This narrows the failure toward the selection POST, but does not prove the prior selection attempt reached its POST. CP16g explicitly labels future failures metadata GET or selection POST. It also extends the existing Inspect_Media_Catalog report with command_contract: bounded structure of each complete recognized media command, including deselect and filter/condition structure; schema type/name/default primitives are retained selectively, other scalar strings suppressed. This avoids another speculative selection-payload change. Existing same-controller/room GET rules remain intact; inspection does not select playback.

Next after matching rebuild/load: Connect, pulse Sources Inspect_Media_Catalog, wait for recorded-media-inspection diagnostic, then gateway Export_Inventory. Collector retrieves room_media_inspections[].command_parameter_inspection.definitions[].command_contract. No Select_Media_ID pulse is requested. Release build, metadata-contract/privacy checks (condition params shape and deselect BOOLEAN_SELECTOR/default false), selection and error/loopback regressions pass. Actual new contract export remains pending.

## CP16h declared wire values

Expanded export generated2026-11-17T00:34:16Z shows mediaid has type LIST, valueType, valueSrc(method/path), valueField and valueDisplay. deselect has valueType and a fixed value. Earlier exports suppressed valueType/value strings. The array assumption was not justified by the UI list type. CP16h builds parameters from the fresh raw command definition: mediaid uses its declared scalar valueType; deselect uses its declared fixed value converted according to valueType. Missing, duplicate, unknown or unsupported declarations reject before POST. Readiness now requires successful typed payload construction for an item; list browsing remains usable when selection is unavailable. No fallback/automatic retry is added.

Supported declared scalar types: string/str, integer/int/uint/number, bool/boolean. Tests use explicitly synthetic declarations to verify both numeric and string IDs plus fixed Boolean/string values; the historical normalized fixture is not misrepresented as containing these values. Contract inspection now preserves valueType/valueField/valueDisplay/method and Boolean-like fixed literals for diagnosis. Saved runtime evidence: tests/fixtures/Room13MediaContract_CP16g_2026-11-16.json.

Next after matching rebuild/load: Connect, Browse_Media_Audio_Stations and wait. If Media_Select_Available_FB=1, set Media_ID and pulse Select_Media_ID_btn once. If readiness stays0, do not force selection: pulse Inspect_Media_Catalog, wait for recorded diagnostic, then Export_Inventory for the exact now-visible scalar declarations. Actual corrected request acceptance/Spotify playback remains pending.

## CP16h runtime: POST confirmed, same error

User diagnostic explicitly reports Native media selection POST failed: HTTP400/error=cond.params.push is not a function. Fresh metadata/typed-payload construction passed far enough to reach POST, but the declared-type/fixed-value correction did not resolve rejection. Do not claim this proves a controller defect independent of request format. Collector still holds the CP16g inspection generated2026-11-17T00:34:16Z, with scalar valueType/value fields suppressed. Next use the already installed CP16h Inspect_Media_Catalog then Export_Inventory to capture those fields. No rebuild or further playback pulse needed for this check. Compare exact scalar declarations and fixed deselect value before changing payload again.

## CP16h exact contract confirmed

Latest export generated2026-11-17T00:41:29Z establishes mediaid.valueType=INTEGER, valueField=id, valueSrc.method=GET and path /api/v1/locations/rooms/13/media/broadcast_audio. deselect.valueType=BOOL with literal string value1. Captured contract saved as tests/fixtures/Room13BroadcastContract_CP16h_2026-11-16.json. Reconstructing metadata from that real fixture and invoking the actual Release request builder locally produces async=true, command=SELECT_AUDIO_MEDIA:BROADCAST_AUDIO, tParams={mediaid:171,deselect:true}; saved reports/CP16h-HEAVY-request.json. No new binary changes were made.

This verifies parameter serialization against the advertised metadata, but not the entire endpoint execution contract or controller implementation. The same selection POST is rejected with cond.params.push is not a function. Do not keep trying array/scalar/Boolean variants without new evidence, and do not claim a confirmed controller defect independent of request framing. Preset171 discovery works; Spotify playback remains unverified. Next investigation needs the actual command-execution envelope/handler or a service-specific preset selector mapping, rather than another identical user playback retry. No additional user test requested at this checkpoint.

## Execution-envelope comparison result

Reviewed local pyControl4.director.send_post_request and the hardware-confirmed GatewayAudio/room transport implementations. Their shared envelope is async/command/tParams, matching the native request envelope; this verifies a generic working convention, not native-media compatibility. No known-working SELECT_AUDIO_MEDIA:BROADCAST_AUDIO POST example was found in public primary sources searched. Official DriverWorks docs cover internal Lua commands and media device context, not this REST execution contract.

Reviewed source https://github.com/New-Forest-Technology-Services/Control4-MCP (src/tools/media.ts and src/client.ts). Its media helper uses PLAY_MEDIA with params/MEDIA_ID and generic /media paths; it does not implement our advertised SELECT_AUDIO_MEDIA command, and provides no captured runtime result proving these routes on this EA-5. Treat it as an unverified implementation lead, not a known-working replacement. No foreign command names/endpoints or dependency changes were adopted.

Alternative SelectPreset route still lacks the service selector mapping. A native Media Database ID is not proven to be the driver's Preset string. CUSTOM_SELECT callback documentation explicitly permits separate labels/values; SendUIRequest requires a receiver handler and cannot be assumed to invoke that callback. A separate bridge is not yet proven to solve this.

Current evidence cannot distinguish an unsupported native execution envelope from a defect in controller handling. Next required evidence is an actual successful native-media request, Control4 REST execution documentation covering these commands, or driver-context selector output. Composer diagnostic remains prepared for when available. No further user playback retry or rebuild requested; do not claim a fix or silently switch to speculative params-array framing.

## CP17a copied Composer service command

User supplied Composer programming XML for SpotifyConnect driver64, command SelectPreset, Room STRING13 and Preset STRING containing the copied opaque hex selector for HEAVY. Earlier Room was empty because the user omitted it in programming; do not treat empty Room as desired/default behavior. Evidence saved in research/media_tests/Spotify_HEAVY_ComposerAction.json and selector-only text file Spotify_HEAVY_Selector.txt. Protocol driver64 is distinct from previously discovered source/proxy65. Native media171 is not the preset selector.

CP17a adds Sources Select_Service_Preset_btn (digital), Service_Preset_Driver_ID (analog), Service_Preset$[2048] (serial). No new parameters; leading skips remain2 per direction and type separators unchanged. Explicit pulse sends only SelectPreset; no serial-change execution, arbitrary commands, auto retry or hardcoded selector. The selected driver's fresh command metadata must advertise exactly Room/Preset for SelectPreset. Hex selectors must be nonempty/even-length<=2048. Module Room_ID is sent as a decimal STRING, and Preset preserved exactly. Offline/uninitialized/stale sessions reject; one pending action per room; acceptance refreshes measured room state and does not assert playback. Selector is neither logged in diagnostics nor embedded in the CLZ.

After updating Sources symbol and recompiling/loading: Connect; set Service_Preset_Driver_ID=64; paste selector text only (no quote marks or newline) into Service_Preset$; pulse Select_Service_Preset_btn once. No Browse_Media or native ID171 selection required for this path. Confirm HEAVY actually plays in Living Room or supply the phase-specific diagnostic. Driver64 metadata GET and SelectPreset REST runtime remain unverified. This is an explicit replay of the copied service action, not automatic preset discovery or a completed dynamic service list.

Local Release build, exact Composer string-value tests, invalid selector/metadata and offline rejection checks, native selection regressions and HTTP400 response tests pass. RMC4 playback remains pending.

## CP17a RMC4 playback verified (2026-11-16 local)

User confirms the requested module test worked: Service_Preset_Driver_ID64, Room_ID13, copied HEAVY opaque selector in Service_Preset$, explicit Select_Service_Preset_btn. This verifies the driver's REST SelectPreset command path and HEAVY playback for this test. Room is sent as STRING13; preset as exact copied STRING. The native SELECT_AUDIO_MEDIA:BROADCAST_AUDIO path remains rejected with HTTP400 and is not the verified playback path. Other presets/drivers/rooms, complete transport behavior and reconnect playback edge cases are not established by this one success.

Next service implementation requirement: discover service preset names and their exact opaque selectors for dynamic widget lists and selection without per-preset manual copying. Native catalog media ID171 alone does not establish that mapping; do not silently map it to the copied selector or hardcode HEAVY into the library. Preserve the explicit pulse-driven verified path while investigating read-only list/selector acquisition.

## CP17b automatic discovery investigation

User authorized automatic service-preset names/selector discovery. CP17b adds Sources Inspect_Service_Presets digital input, using the existing Service_Preset_Driver_ID and Service_Preset$ as known reference data. It reads the room command metadata, requires the exact previously established broadcast_audio GET to remain advertised, then fetches that catalog. No POST, guessed endpoint, selection, startup fetch or periodic polling. Driver ID is report context only; driver ownership is not inferred from the input.

The raw catalog is inspected for exact case-insensitive matches to the known hex selector. Reports retain matching record paths, bounded record/key/type shapes and numeric identity fields; selector text and unrelated string values are suppressed. At most2048 records/1MB, depth5,256 traversal nodes per record,16 matching records and3 nonmatching sample shapes. Truncation markers/scan_incomplete limit negative-result interpretation. Standard room generation/epoch/registration checks guard publication. Report stored in the existing room_media_inspections ring with inspection_kind=service_preset_catalog_mapping, available via Export_Inventory.

After symbol update/rebuild/load: Connect; set Service_Preset_Driver_ID64; paste the verified selector into Service_Preset$; pulse Inspect_Service_Presets (not Select_Service_Preset_btn); wait for inspection-recorded diagnostic; pulse gateway Export_Inventory. Automatic collector retrieves the evidence. An exact location match could establish a basis for list extraction; a nonmatch is limited to inspected fields/depth and does not prove all APIs lack selector data. This checkpoint does not yet claim automatic discovery is implemented end-to-end.

Local Release build, exact/partial/case comparison and sensitive-value suppression tests, numeric-identity retention, traversal bound/empty/offline checks, verified playback serializer and room media-inspection regression checks pass. RMC4 discovery evidence is pending. CP17a verified playback is retained.
