# Media implementation audit and next test

2026-11-15. This audit reconciles the new research handoff with the current working project. No C#, SIMPL+ interface, SDK, framework or build output was changed for this audit. Research scripts were inspected, not executed; no AV commands were sent.

## Actual baseline

| Area | Current evidence and implementation |
| --- | --- |
| Project | Traditional Control4Gateway.csproj, class library, .NET Framework 4.7.2, used from SIMPL Windows/SIMPL+. Not SIMPL# Pro. |
| SDK | Thirteen DLL references use ../packages/Crestron.SimplSharp.SDK.Library.2.22.40/lib/net47. packages.config is 2.22.40. Targets import both project-local 2.21.274 and root 2.22.40. Preserve these existing paths/imports; the research handoff's single-version 2.21.274 description is not the current project configuration. |
| Authentication | Account-to-Director flow and on-demand token refresh implemented in Control4AccountClient/GatewayCore. Account authentication demonstrated on RMC4. No manual token input. Full-lifetime automatic renewal remains unverified. |
| Local trust | User-selected automatic local controller certificate acceptance for REST and WebSocket. Cloud HTTPS validation remains normal. No fingerprint configuration or process-wide TLS override. |
| Shared transport | GatewayRegistry shares one GatewayCore per Gateway_ID. Explicit Connect starts REST and one Socket.IO/Engine.IO listener. Constructors/Main do not initiate network requests. |
| Light | Proxy 437 command/readback, live and external-change feedback, manual reconnect demonstrated/confirmed. |
| Shade | Proxy 226 REST readback and Open followed by automatic position updates 0/3/100 demonstrated. Close, target level and Stop are implemented but their hardware tests remain pending. Shade events trigger measured REST Level reads; no unobserved position-event schema is assumed. |
| Inventory | SIMPL-triggered formatted JSON export implemented. User's downloaded JSON parsed successfully and identifies Middle Shade 226. Export currently omits UI experience/source mappings. |
| Audio/Video/Generic | Initialization-only adapters/wrappers. No C# media source selection, room volume/mute/power or media browser yet. Python media success must not be reported as C# success. |
| Build vs runtime | Prior Release build and 4-Series-only wrapper compiles passed; these do not prove future media runtime behavior. No audit rebuild was necessary. |

Current local CLZ SHA-256: EE06FB73B908C7275852AAE811C72AF1FC05956F33C1DDC0990EAD2860AB4EE8. Project SHA-256: 31EC7B343095533B8F0FFA8B6F7FF0199E9E368B39B3E5ACCD032F6EFFD98E51. Latest local source revision is Stage1-CP6d-ShadeControl. Exact deployed CLZ identity was not captured in the hardware traces.

The new handoff's historical statements that the gateway requires manual tokens and lacks REST/WebSocket are superseded by current source and user processor traces. Its later media observations remain useful research. Do not replace the current working package with the older archive or normalize its SDK to match that historical description.

## Inspected source and evidence

The user supplied media scripts and chronological notes under research/media_tests. Public pyControl4 implementation files were copied verbatim from the original Control4-Crestron.zip into research/reference/pyControl4 for reproducible line references. The installed Python package used in the later media tests has not been inspected on its original host; compare it if its behavior differs.

| Native request | Python method / exact implementation location | Test source and evidence |
| --- | --- | --- |
| GET /api/v1/items | C4Director.get_all_item_info; [director.py](../research/reference/pyControl4/director.py#L121) lines 121–125 | Step1_Discovery.py:49 and Step2A_SourceNames.py:34. Names join resolves blank Watch source names. |
| GET /api/v1/agents/ui_configuration | C4Director.get_ui_configuration; director.py:251,333 | Step1_Discovery.py:39; Step2A_SourceNames.py:33. Research notes report 156 experiences, 16 rooms, 242 Watch/Listen entries, not unique devices. |
| GET /api/v1/items/13/variables?varnames=POWER_STATE | C4Room.is_on; room.py:23–28 | Step3C_RoomPowerTest.py:23 and Step3D_RoomFeedback.py:26. Room state readback. |
| GET /api/v1/items/13/variables?varnames=CURRENT_VOLUME | C4Room.get_volume; room.py:57–64 | Step3B_VolumeTest.py:32,52,60. Reported test 41→30→41. |
| GET /api/v1/items/13/variables?varnames=IS_MUTED | C4Room.is_muted; room.py:66–71 | Step3A_MuteTest.py:20. Reported mute readback. |
| POST /api/v1/items/13/commands: SELECT_VIDEO_DEVICE, tParams.deviceid | C4Room.set_video_and_audio_source/_set_source; room.py:38–55 | Step2B_SelectSource.py:74; Step3C_RoomPowerTest.py:61. Source 542/SHIELD selection physically confirmed in research notes. |
| Same POST: SELECT_AUDIO_DEVICE, tParams.deviceid | C4Room.set_audio_source/_set_source; room.py:38–51 | Step2B_SelectSource.py:76. TuneIn 20 selected; no demonstrated catalog or playback start. |
| Same POST: MUTE_ON / MUTE_OFF | C4Room.set_mute_on/off; room.py:73–87 | Step3A_MuteTest.py:51,53. Volume unchanged during the reported unmute test. |
| Same POST: SET_VOLUME_LEVEL, tParams.LEVEL | C4Room.set_volume; room.py:97–103 | Step3B_VolumeTest.py:48,58 calls set_volume(). Its printed SET_VOLUME label is not the wire command. |
| Same POST: ROOM_OFF | C4Room.set_room_off; room.py:30–36 | Step3C_RoomPowerTest.py:51. Physical power-off confirmed in notes; selecting SHIELD is a specific new selection, not general prior-state restoration. |
| GET /api/v1/items/{20,65,599,100002} | C4Director.send_get_request/get_item_info; director.py:61,127–135 | Step4A_BrowseCapability.py:44–49; Step4B_ItemStructure.py:53–58. Length-one metadata lists, not a catalog tree. |
| GET /api/v1/items/{id}/commands | C4Director.get_item_commands; director.py:221–229 | Available in inspected library; not established as tested or a browse operation in the supplied Step1–4 scripts. Do not claim otherwise. |

All POSTs use C4Director.send_post_request, director.py:78–105, with async, command and tParams. The room ID is configurable in production; 13 is the specific research test room.

## Feedback mapping requirements

- Living Room: REST room 13; separately configurable feedback endpoint 682 for volume_level and mute. Do not subscribe only to 13 or hardcode 682 across rooms.
- Endpoint 682/2061 powerstate is device power, not universal room power. Query room POWER_STATE for a general room power signal until a broader event contract is proven.
- Agent 7 active_state_changed must match room_id=13 and type=watch. This is Watch activity, not generic power, Listen activity, or exact selected source.
- Routing endpoint 2165 InputID 1001/1002 and endpoint 682 current_input 3002/1002 have no verified mapping to source IDs. Preserve unknown source state rather than invent that mapping.
- Initial/reconnect reconciliation must read measured state. Any bounded fallback reconciliation should be explicit, separate from live endpoint mappings, and tested before enabling background traffic.

Evidence: media scripts Step3D–3H and supplied CONTROL4_CRESTRON_MEDIA_CODEX_NOTES.md. No media traffic was executed during this audit.

## Browse investigation boundary

[Official MSP resources](https://github.com/snap-one/docs-driverworks/tree/master/media_service_proxy) describe driver/proxy UI browsing and provide the MSP By Numbers examples. [BrowsingLists](https://github.com/snap-one/docs-driverworks/tree/master/media_service_proxy/MSP%20By%20Numbers/3-BrowsingLists) and BrowsingRealData are the relevant next references; the repository flags some older samples/schema tools as outdated. These resources describe DriverWorks integration inside Control4, not a verified external REST catalog endpoint.

The [proxy protocol reference](https://snap-one.github.io/docs-driverworks-proxyprotocol/#get_list) describes Generic Media Player list commands. Sending an internal GET_LIST name to an arbitrary REST URL is not justified by that documentation. No catalog/search URL has been established by the supplied scripts.

Service status: TuneIn 20, Spotify 65, Amazon Music 599 and Digital Media 100002 have successful item-detail research only. Pandora source IDs are discovered, but no Pandora catalog test is supplied. Artist/album/playlist/station browsing, search, queue, favorites and artwork remain unproven over the external API for all these services.

If a native external browse route cannot be established, evaluate an opt-in DriverWorks bridge separately. First prove a supported request/response mechanism for the actual service; a custom driver does not automatically grant access to another proprietary driver's catalog. Per-service APIs are a separate option with their own authentication and capability constraints. No bridge deployment or service changes are part of this proposal.

## Proposed single next read-only test: Step 4C, TuneIn URI capability inspection

Use the already-working authenticated gateway to repeat the already-verified **GET /api/v1/items/20**, but inspect its **URIs** metadata specifically. Step4B listed structure only and did not preserve the advertised routes. This makes the next test narrower than guessing browse URLs or invoking internal proxy commands.

Proposed SIMPL implementation after review: append one Export_Media_Capabilities input and expose a JSON file path through a dedicated serial output, retaining parameter skips and signal-type separators. One explicit pulse performs the GET and creates a small sanitized report. No startup work, source selection, playback, volume/mute/power changes or background browsing. Reuse account authentication, local TLS policy and the current central gateway. No Python runtime is required on the processor.

Report contract:

```json
{
  "source_id": 20,
  "http_status": 200,
  "item_count": 1,
  "uris_present": true,
  "uri_structure": {},
  "advertised_local_get_paths": [],
  "browse_endpoint_status": "not_established"
}
```

This is the expected report format, not a fabricated test result. Values depend on the real response. Export only known structural metadata and same-controller API paths with query strings/fragments stripped; omit credentials, token-like fields, user profiles, raw values and media/history titles. Do not request any advertised path automatically. Unrecognized URI schemas are reported by structure, not interpreted as links.

Interpretation:

1. An explicitly advertised local GET browse/list route is a candidate for a subsequent documented GET test. Its existence alone is not proof of usable catalog data.
2. A successful request returning an actual typed/paginated catalog list would establish that one native browse operation for that source, not universal multi-service browsing.
3. Missing URIs, no eligible links, or only generic control/metadata routes means **no native browse route established by this test**. It does not prove REST has no browse interface; continue official documentation/sample research before a separate bridge decision.
4. HTTP/auth/format failure is inconclusive. Report only nonsecret status/type, preserve existing functioning transport, and diagnose that isolated layer.

There is currently no evidence-backed, actionable catalog URL to prove/disprove directly. This limited capability test is the honest preceding step. Do not label a metadata dictionary as a browse tree or silently probe guessed /browse, /search or /media endpoints.

## Staged implementation following review

1. Save the current source/CLZ and wrapper evidence; preserve the mixed SDK references and net472. Do not re-run already-proven startup work by migrating to an older baseline.
2. Implement the single Step4C capability inspection through SIMPL, locally check metadata filtering and JSON output, compile 4-Series only, then validate idle startup/export on RMC4.
3. Implement read-only room/source discovery via ui_configuration plus item-name joins. Keep Watch/Listen catalogs distinct from content catalogs. Validate Living Room 13 mapping before writes.
4. Add Audio room readback and configurable feedback endpoint, with independent valid states for volume/mute/room power. Route measured 682 fields and filtered agent-7 Watch activity with their proper semantics. Reconcile on reconnect.
5. Add individually pulsed source selection, mute, volume and room off using the exact verified Python commands, with readback and operator-led physical testing. Do not send AV commands during audit, initialization or diagnostics.
6. Implement one evidence-backed native browse operation if Step4C/documentation yields a valid route. Otherwise present a separate optional bridge/per-service design with bounded pages, request IDs, explicit selection and artwork policy.
7. Complete pending shade commands, outage recovery, program reboot and full token-lifetime tests independently. Maintain 4-Series-only wrappers, account-only auth and current local certificate acceptance.

## Review gate

The newly supplied handoff section 8 explicitly says: **“DO NOT modify the working C# library until the user approves the initial audit/test proposal.”** This document supplies that audit/proposal. Approval is needed before implementing Step4C; this is the handoff's explicit workflow requirement, not a tool or sandbox denial. Existing working source, wrappers, project and CLZ are unchanged by this audit.
