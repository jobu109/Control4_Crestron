**CP22a current UI workflow:** Click the named Listen service to browse its saved presets, then select a preset to start playback. Services with catalog-owned content are opened without an initial room-source routing command; this preserves the native launch sequence. Explicit driver relationships and validated command metadata remain required for selection. Physical audio and Watch routing remain available. Actual selected-source feedback does not change merely because a service list was opened. No UI rebuild or join changes are required for this library update. The user verified the equivalent manual browse-first HEAVY sequence restores normal Control4 metadata/artwork updates; automatic UI flow awaits reload/testing. Crestron now-playing text/artwork output is a separate pending implementation. Empty services without a verified preset command contract are not established by this change.
**Current Pandora shuffle control (CP19c):** Pandora_Shuffle_On is a digital level input:0=Off (default),1=On. Each manual or discovered station-selection pulse captures the current setting. Changing it alone sends no playback request. Widget pandora_shuffle reports the configured next-selection mode, not measured shuffle state. This supersedes historical fixed-On notes below.

# Dynamic Spotify saved presets

Revision Stage1-CP17c-DynamicServicePresets. CP17a playback of HEAVY through protocol driver64 is user-verified. CP17b raw catalog inspection found its exact copied selector in record171.location, with device_id65. This establishes the extraction path for that record; discovery and selection of the resulting full list still need RMC4 verification.

## Configuration and read-only browse

Update the Sources symbol and compile/load the matching library for 4-Series only. Keep Room_ID=13. Set Service_Preset_Driver_ID=64 and Service_Preset_Source_ID=65, then pulse Browse_Service_Presets. No selector text, Composer action copy or Service_Preset$ value is needed for this browse/selection path.

The two IDs have separate roles: source/proxy65 filters catalog ownership; protocol driver64 receives SelectPreset. They are configurable inputs, not hardcoded relationships. Automatic discovery of protocol-to-proxy relationships is not implemented. The widget can obtain the source ID from the room's existing source list; the corresponding driver ID currently requires setup.

Each explicit browse reads room command metadata, requires the room broadcast-audio GET to be advertised, reads driver command metadata to validate SelectPreset(Preset,Room), then fetches the catalog. Three GETs, no POST, autoplay, startup fetch or periodic polling.

Entries must have the requested device_id, positive numeric id, display name and bounded even-length hex location. Names/IDs populate the public list; selectors remain in the gateway's private snapshot. Unknown formats are omitted and partial status is raised. Ambiguous duplicate IDs are removed. This supports the observed Spotify saved-preset format, not every service's catalog or a full Spotify library.

## Existing media-list outputs

Media_Kind$ becomes service_presets. Media_List_JSON$ carries kind, scope=service_saved_presets, room_id, driver_id, source_id, revision, page/pages, total, partial, items[{id,name,label_available}] and selection_supported. Media_Item_Count_FB, Media_List_Valid_FB, Media_List_Partial_FB and Media_Select_Available_FB report the loaded list. Media_List_Page_Set uses cached pages. A valid empty list has no selectable items.

The public JSON and inventory catalog history do not contain opaque selectors. Render names as text. Source ownership is filtered by the actual catalog device_id, not a name match or assumed adjacent ID.

## Selection

With the service list loaded, use any existing media selection method:

- Set Media_ID$ to an item's ID and pulse Select_Media_ID_btn.
- Change Media_Item_Select to a one-based whole-list index.
- Change Media_Select_Request$ using the list's exact room, kind and revision:

```json
{"room_id":13,"kind":"service_presets","revision":"COPY_CURRENT_REVISION","id":"171"}
```

The gateway resolves the ID to the cached selector and uses the verified SelectPreset command with Room as a string. It does not call the failing native SELECT_AUDIO_MEDIA endpoint. IDs are meaningful only within the loaded kind/revision. List replacement, switching to native browsing or disconnect clears the private mapping. Queued stale selections are checked again before POST; already transmitted requests may complete. Offline requests are not replayed. Acceptance refreshes measured room state and is not an assertion of playback.

The manual Service_Preset$/Select_Service_Preset_btn interface remains available for diagnostics, but is unnecessary for selecting discovered items.

## RMC4 test

1. Connect; set driver64/source65.
2. Pulse Browse_Service_Presets. Wait for the loaded diagnostic and Media_Select_Available_FB=1; inspect the count and named list.
3. Set Media_ID$=171 and pulse Select_Media_ID_btn once. Confirm HEAVY plays in Living Room.
4. Export_Inventory to capture the public list. Test other discovered presets separately before claiming them verified.

Local Release build and source-filtering/private-map/privacy/duplicate/stale/disconnect tests pass, along with verified command serialization and native catalog/selection regressions. End-to-end dynamic discovery/playback remains pending on RMC4. Native media selection remains a separate unresolved controller path.

## RMC4 verified 2026-11-17

User confirms HEAVY played after Browse_Service_Presets and Media_ID/Select_Media_ID_btn, without selector text. Collected inventory generated2026-11-17T14:38:12Z contains list generated14:36:48.843066Z for Room13/driver64/source65: five entries, zero omitted/unnamed, no truncation, selection_supported=true. Names and IDs: 172=90's Music;168=Classic Rock Favs;170=Emma's Favs;169=FAVS;171=HEAVY. This verifies read-only automatic named-list/selector discovery and HEAVY playback through the cached mapping. Other listed presets have discovery evidence only, not confirmed playback. Saved fixture: tests/fixtures/Room13ServicePresets_CP17c_2026-11-17.json. Dynamic-list-specific analog/widget selection and refresh/reconnect runtime cases remain separate tests; driver/source association still requires configuration.

## Additional RMC4 verification 2026-11-17

User confirms Media_Item_Select=2 plays Classic Rock Favs from the discovered service list. User also confirms the complete requested Disconnect/Connect/rebrowse test: Media_List_Valid_FB and Media_Select_Available_FB clear on Disconnect; after Connect/WebSocket readiness and explicit Browse_Service_Presets, all five entries return and both flags assert. This verifies the tested manual reconnect/rebrowse recovery; it does not establish automatic catalog fetch on reconnect (none is implemented), unexpected-outage behavior or full token lifetime. Service-list JSON widget selection remains a separate unverified runtime path.

## Widget JSON selection verified 2026-11-17

User explicitly confirms HEAVY played after sending current-room/current-revision service_presets JSON with id171 to Media_Select_Request$, without a digital button pulse. This verifies the serial-change widget selection path for this room/list/preset. Together with prior confirmations: automatic five-item discovery, Media_ID$ plus digital pulse, analog position2/Classic Rock Favs, and manual disconnect/connect/explicit rebrowse recovery are verified. Wrong/stale-revision rejection remains locally tested rather than user-demonstrated; other services, automatic driver/source association, unexpected outage and token-lifetime behavior remain separate.

## CP18a automatic ID mode (hardware validation pending)

Existing inputs now accept0 as automatic mode. Service_Preset_Source_ID0 uses the measured active AudioId only when valid and positive; it never substitutes a last-requested or generic renderer ID. Select Spotify through the normal room source controls and wait for measured selection before Browse_Service_Presets. Source IDs beyond the current ushort interface are rejected rather than truncated.

Service_Preset_Driver_ID0 reads the selected source item detail and accepts only an explicit driver_id/driverId/protocol_id/protocolId, or a parent_id/parentId/parent relationship if no explicit driver field is present. Source identity must match and type must be proxy7; exactly one nonself candidate is required. Candidate detail must match and be protocol type6; its fresh commands must support SelectPreset(Room,Preset). No source-minus-one, same-name, nearest-ID or inventory-order fallback exists. This is an implementation against explicitly checked schema variants, not a claim that this EA-5 has already returned those fields. Missing/ambiguous/unrecognized data fails without posting playback. A bounded source field/type/relationship report is saved in room_media_inspections with inspection_kind=service_driver_mapping. Ordinary explicit driver64/source65 setup remains supported.

An automatic browse uses up to5 GETs: source detail, driver detail, driver commands, room commands and advertised broadcast catalog. No POST or periodic discovery. Current driver metadata is reused within that browse. Successful mapping and list public IDs can be checked in Export_Inventory; report status verified_relationship_and_command means controller identity/command checks passed, not that a new preset played. Generation/epoch/registration guards prevent stale publication.

Next test after recompile/load: Connect, select Spotify normally, wait for measured audio selection; set both service-ID inputs0; pulse Browse_Service_Presets; wait for loaded/failure diagnostic; Export_Inventory either way. No selector copying and no playback retry are requested. The module still uses an explicit Browse pulse; automatically triggering a browse on every source change is not implemented in this checkpoint.

Local explicit-parent/direct-driver/ambiguity/self/type/identity/privacy tests pass using clearly synthetic relationships, plus dynamic-list and verified command-serialization regressions. Actual automatic mapping remains pending on RMC4. No SIMPL+ signal/parameter changes.

## CP18a automatic relationship verified on RMC4

Export generated2026-11-17T14:59:11Z contains service_driver_mapping at14:58:48.120801Z: measured source65 returned explicit protocolId64, candidate identity and SelectPreset command checks passed (status verified_relationship_and_command). The service catalog at14:58:48.587916Z has Room13,driver64,source65,total5. This verifies both-zero automatic ID mode through the tested app-selected Spotify source, without adjacent-ID inference. Fixture: tests/fixtures/ServiceDriverMapping_CP18a_2026-11-17.json. Playback using the newly automatically mapped list is the remaining immediate runtime check; earlier manual-mapping playback tests remain verified.

The attempt to select by Audio_Source_Name$=Spotify reported unavailable/ambiguous in configured slots. User selected through the app for this mapping test. Do not claim that source-name selection issue is resolved or assume its cause; inspect discovered slot names/validity if investigating it. Automatic driver mapping itself used protocolId and succeeded independently.

## CP18a automatically mapped playback confirmed

User confirms HEAVY played via Media_ID and Select_Media_ID_btn from the list loaded with both service-ID inputs0. This verifies the complete tested automatic workflow: measured app-selected Spotify source65, explicit protocolId64 resolution, five-preset discovery/private-selector caching, and playback through SelectPreset in Room13. No manual source/driver IDs or per-preset selector copying are required in this tested mode. Explicit Browse_Service_Presets is still required; automatic browsing on each source change and the earlier unavailable/ambiguous source-name input remain separate work.

## CP18b browse automatically on measured source changes

The Sources module now initiates automatic service mapping/browsing after each distinct valid measured active audio-source change. This applies to selections made through the app or existing module controls. Initial/reconnect measured selection re-arms browsing. Repeated feedback for the same service and temporary invalid source feedback do not retrigger GETs; there is no timer or polling. A failed/unsupported service is not retried on each unchanged readback; manual Browse_Service_Presets remains an explicit refresh/retry.

Changing source immediately invalidates the old media list/private selector mapping and cancels its pending catalog publication. A measured no-audio-source/Room Off clears without browsing. Delayed old catalog responses cannot repopulate the changed room. Starting a preset in the same service does not unnecessarily replace the widget revision.

Automatic browsing always uses measured source identity and controller protocol relationship, independently of the manual service-ID override inputs used by the explicit Browse pulse. No preset is selected automatically. A user must still select a discovered item to start content.

Spotify's mapping/list/selection path is already verified; this new event trigger needs the RMC4 test below. Other services such as Pandora are attempted using their actual reported driver relationship and command metadata. Only the established SelectPreset/hex-location saved-preset adapter is implemented. Unsupported services keep no stale list/readiness; mapping reports now include filtered driver command metadata and a capability-failure reason for the next adapter investigation. This does not claim that all streaming services expose the Spotify format or a full library.

After matching recompile/load: Connect, Room Off, select Spotify through the app, and wait for service presets loaded without pressing Browse_Service_Presets. Confirm count5/readiness and select HEAVY normally. Then select Pandora and Export_Inventory after its loaded/failure diagnostic. No selector copying or numeric service IDs are needed for the automatic path.

Local Release build and measured-event/duplicate/transient/other-room/Off/reconnect tests pass, together with mapping/list/privacy/source-feedback/event-driven regressions. The automatic event trigger and Pandora capabilities remain pending hardware validation. No SIMPL+ signal or parameter changes.
## CP18b Spotify source-change trigger verified 2026-11-17

User supplies debugger trace after selecting Spotify through the Control4 app, without Browse pulse. Measured selected-source feedback arrives485ms; old media list is invalid/empty during transition. At1.907s Content_Browse_Available_FB, Media_List_Valid_FB and Media_Select_Available_FB assert, count5; list JSON at1.922s has kind service_presets, scope service_saved_presets, valid true, partial false and five items. Diagnostic confirms service presets loaded. Busy clears2.000s. This verifies automatic source-change mapping/list load on RMC4 for Spotify. Playback via this exact newly event-loaded revision and Pandora remain separate checks. The transient repeated empty JSON emissions are observed; they do not by themselves establish repeated HTTP browsing or periodic polling.

## Pandora mapping and CP18c proxy capability inspection

Export generated2026-11-17T15:15:52Z now captures actual Pandora source change: source1648 explicitly reports protocolId1647. Candidate protocol identity succeeded; GET /api/v1/items/1647/commands returned HTTP200 with an empty recognized array. Mapping report remains status unresolved/reason SelectPreset_contract_not_verified because the existing adapter does not apply. Automatic source-change triggering and explicit relationship discovery work; Pandora preset/station playback is not implemented or verified. The earlier15:14:34Z export had only Spotify history; after starting a Pandora station the new source was observed. Fixture: tests/fixtures/PandoraDriverMapping_CP18b_2026-11-17.json.

CP18c adds one read-only fallback inspection when a mapped protocol driver lacks the SelectPreset contract: GET the selected source/proxy commands (for this test /api/v1/items/1648/commands) and retain sanitized source_commands metadata in the same mapping report. This generic item-commands route is already established; no speculative command is posted. Empty protocol metadata does not prove the source/proxy or all external interfaces lack controls. Spotify behavior is unchanged. No signal changes.

Next after recompile/load: Connect, turn Living Room off, select Pandora in the app and start a station; wait for loaded/failure diagnostic, Export_Inventory. A failure is expected if no supported adapter exists; the export now includes proxy capabilities for the next implementation decision. Local Release build and mapping/event/verified-service regressions pass. Proxy response and Pandora support remain pending hardware evidence.

## CP18c Pandora proxy result

Inventory generated2026-11-17T15:21:08Z includes Pandora mapping inspection at15:20:43.813912Z. Both protocol1647 and source/proxy1648 commands returned HTTP200 recognized empty arrays, with no truncation or omitted commands. Therefore no Pandora selection command is established through either inspected commands endpoint. This does not prove all Navigator/internal/other external interfaces lack controls. The existing Spotify SelectPreset/hex adapter must not be applied to Pandora or a native media ID substituted as a service selector. Saved fixture: tests/fixtures/PandoraProxyCapabilities_CP18c_2026-11-17.json.

Next evidence route: inspect Composer's ordinary Pandora programming Actions for a station/favourite selection action, if available. Copy a working action for one station and Living Room exactly as was done for Spotify, without Lua execution or changing driver protection. Do not assume that Composer offers such an action. A copied command would establish exact target/selector syntax; REST execution would still require its own validation if not advertised. If no action exists, further vendor/Navigator API documentation is needed before promising Pandora selection. No new binary changes or repeated identical export requested.

## CP19a copied Pandora action test

User supplied Composer action: proxy pandora-premium, DeviceID1647, command Play Item (space preserved), Room STRING13, Item STRING ST:0:4574543691136306420, Shuffle STRING On, Instrumental Radio. Saved exact values in research/media_tests/Pandora_Instrumental_ComposerAction.json and selector-only text companion. These IDs/selector are test data, not compiled constants.

CP19a reuses Select_Service_Preset_btn and Service_Preset$ for a narrowly recognized Pandora ST:0:positive-decimal station selector. This builds the exact copied Play Item/Room/Item/Shuffle strings with Shuffle On. Driver ID remains an input. The fresh metadata endpoint must return the captured recognized empty list; authorization for this specific command shape comes from the copied Composer action, not a false advertised-command claim. No arbitrary command passthrough, auto fallback or guessed selector conversion. Existing Spotify hex selection retains its advertised SelectPreset validation. Offline/session/pending/stale guards and measured refresh remain unchanged. No new SIMPL+ pins.

After matching rebuild/load: Connect, set Service_Preset_Driver_ID1647, set Service_Preset$ to ST:0:4574543691136306420, pulse Select_Service_Preset_btn once. Confirm Instrumental Radio plays in Living Room or provide phase-specific Diagnostic$. No native media ID or Browse pulse is needed. This REST playback remains pending until the user verifies it. Existing automatic Pandora list browsing still lacks an adapter and can report a capability failure independently.

If playback succeeds, pulse Inspect_Service_Presets using the same selector/driver inputs, wait for recorded diagnostic, then Export_Inventory. The read-only inspection now recognizes both supported copied-selector formats and can determine whether Pandora locations directly match the ST item. Automatic Pandora station-list extraction is not yet enabled. Local exact action/string/large-ID preservation, invalid selector, inspection privacy, Spotify regressions and automatic event tests pass.

## CP19a Pandora playback and selector match verified

User explicitly confirms Instrumental Radio played through the copied Play Item adapter on RMC4. Export generated2026-11-17T15:36:13Z contains inspection at15:36:01.394491Z:196 records visited, no scan truncation, exact Item selector match at record92.location, catalog ID357, device_id1648. This establishes the same source-owned catalog location extraction path for the tested Pandora station. Fixture: tests/fixtures/PandoraSelectorMatch_CP19a_2026-11-17.json.

## CP19b automatic Pandora saved-station lists

Automatic browsing now proceeds from verified source/protocol identity to the advertised room catalog even when the driver has no SelectPreset command. Source-owned catalog locations may be the existing Spotify hex format or Pandora's observed ST:0:positive-decimal station format. Each private selector is validated against its specific adapter: Spotify requires advertised SelectPreset metadata; Pandora uses the exact copied, RMC4-verified Play Item contract with the observed empty command list. Unsupported selectors/contracts are omitted, never converted or sent through speculative commands. Mapping reports distinguish verified relationship/catalog inspection and record adapter names/selectable counts. Empty metadata alone does not enable playback; a recognized selector plus its validated adapter is required.

Public list kind remains service_presets and reuses Media_List_JSON$/count/page/validity, Media_ID$+pulse, analog index and revision-bound JSON selection. Pandora list scope is service_saved_items; selection_adapters includes Play Item and pandora_shuffle=On. Selectors stay private. Selecting ID357 from the current Pandora list resolves its cached ST string and posts Play Item to the automatically resolved driver. No hardcoded driver/source/media/station IDs are introduced; those numbers here are evidence/test values. Mixed/other-source records remain filtered; stale/disconnected list selections remain rejected.

No new signals. After recompile/load: Connect; turn Living Room off; select Pandora and start a station in the app to establish measured source feedback. Wait for automatic list/readiness, set Media_ID and pulse Select_Media_ID_btn once. Confirm Instrumental Radio plays, then Export_Inventory for named station-count validation. Do not paste Service_Preset$ or pulse Browse manually. Copied Play Item uses Shuffle On; configurable shuffle and full Pandora search/library browsing remain separate. Automatic Pandora list/selection still needs this RMC4 test. Local build, station/source/private-map/copy-shape tests and Spotify/mapping/event/native-list regressions pass.

## CP19c configurable Pandora shuffle

User requested On/Off with Off default before CP19b hardware testing. Added one digital input Pandora_Shuffle_On to Sources, no parameter additions. Leading skips remain2/direction; transition separators remain intact; USP retains CRLF. Main explicitly applies the initial input level after Initialize, including an already-high input. Core default is false for an unset room; settings are per-room and persist across connection resets while the input is unchanged. Selection snapshots the setting at its explicit pulse, so delayed metadata GETs do not change the requested mode mid-command.

Play Item always sends Shuffle STRING Off or On. Setting the flag updates an already-loaded Pandora list's public configured pandora_shuffle field without changing its item revision or posting a command. No optimistic actual-shuffle feedback is added. Spotify's stored opaque selector and SelectPreset parameters are unaffected.

Update Sources symbol/recompile/load. Connect, Room Off/select Pandora/start a station in the app; wait for automatic list/readiness. Leave Pandora_Shuffle_On0, set Media_ID and pulse Select_Media_ID_btn. Confirm Instrumental Radio plays; then set Pandora_Shuffle_On1 and repeat the explicit selection to test On. Export_Inventory for discovered station counts. Copied manual Pandora playback with On is already user-verified; default-Off playback and automatic Pandora discovery/selection remain pending. Local On/Off serializer/default-state/no-I/O tests, private station mapping and Spotify regressions pass.

## CP19c automatic Pandora/default-Off playback verified 2026-11-17

User confirms the requested automatic Pandora workflow played Instrumental Radio through mediaID357/Select_Media_ID_btn with Pandora_Shuffle_On left0. Collected inventory generated2026-11-17T15:49:27Z contains list generated15:46:58.665218Z:Room13,source1648,driver1647,162 named selectable saved items, zero omitted/unnamed, no truncation, Play Item adapter, configured pandora_shuffle=Off. Fixture: tests/fixtures/PandoraStations_CP19c_2026-11-17.json. This verifies automatic Pandora discovery/private-selector selection and playback with Shuffle Off requested. Exported shuffle is configured request state, not measured actual playback shuffle feedback. Explicit new input-high test and large-list paging/JSON selection on Pandora remain separate checks; earlier copied-action On playback is verified.

## CP19c input-high selection test confirmed

User confirms the requested test: Pandora_Shuffle_On1, Media_ID, explicit Select_Media_ID_btn. Pandora station selection/playback now has user-confirmed tests with the input both0 (default Off request) and1 (On request). The module still has no measured actual-shuffle-state feedback; the JSON field reports requested configuration. No binary changes. Remaining station-list validation includes multi-page widget output and Pandora JSON item selection; automatic discovery/mapping and cached digital selection are already verified.

## Pandora paging test confirmed 2026-11-17

User confirms the requested paging check: Media_List_Pages_FB exceeds1; setting Media_List_Page_Set2 changes the reported page and station entries; returning to1 restores the first page; paging does not change playback. Exact page count was not supplied. This verifies the tested Pandora multi-page navigation on RMC4. Pandora revision-bound JSON selection remains the next widget-specific runtime check; no binary changes.

## Pandora widget JSON selection confirmed 2026-11-17

Initial Media_Select_Request$ attempt returned stale or unavailable; the exact failing request was not captured, so do not assert its specific mismatch. After instructions to use the current loaded list revision and readiness, user confirms JSON selection worked for mediaID357/Instrumental Radio without a button pulse. This verifies Pandora serial-change widget selection with a current revision. Earlier Spotify JSON selection is independently verified. No new binary changes.

## Service-switch isolation verified 2026-11-17

User supplies a Spotify transition trace: old list invalid/empty around484-515ms, five valid/selectable presets loaded1.734s, valid service_saved_presets JSON1.750s, Busy clears1.843s. User then explicitly confirms the entire requested sequence: Spotify5 -> Pandora162 -> Spotify5, automatic list replacement without Browse pulses, followed by HEAVY playback through the module. This verifies service-list isolation and cached selection after switching for the tested Room13 services. No new binary changes.

## Automatic manual-reconnect recovery verified 2026-11-17

User confirms the complete requested test passed: with Pandora playing, gateway Disconnect clears Media_List_Valid_FB and Media_Select_Available_FB; Connect restores all162 stations and both flags without a Browse pulse; subsequent Instrumental Radio selection succeeds. This verifies automatic measured-source/mapping/list reinitialization and cached station selection after an intentional Disconnect/Connect on RMC4. It is distinct from an unexpected network outage, processor reboot or full token-lifetime renewal, which remain separate validation. No new binary changes.

## TuneIn initial runtime investigation 2026-11-17

Export generated2026-11-17T16:06:18Z captures source20 explicitly mapped to protocol19. Protocol commands GET returned HTTP200 recognized empty list. Advertised room catalog loaded196 total records; source20 normalization omitted27 entries, emitted0 selectable items, no truncation, selection_adapters empty. This does not establish missing TuneIn content: the current adapter filters out unknown/invalid selector formats. Exact rejection causes and raw source20 location values are not exported in this snapshot. Automatic source-change/mapping/catalog-fetch works, while TuneIn selection is not implemented. Fixture: tests/fixtures/TuneInCapabilities_CP19c_2026-11-17.json.

Next obtain one ordinary Composer TuneIn station-selection action for Living Room, if offered, using the same copy/XML procedure as Pandora/Spotify. Preserve exact command name, target protocol/proxy, selector and Room types. Do not assume TuneIn uses Play Item/Pandora station prefix, SelectPreset/Spotify hex, or a native media ID. No new binary changes or repeated export requested until that evidence is available.
