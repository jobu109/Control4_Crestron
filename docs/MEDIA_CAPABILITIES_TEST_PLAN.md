# TuneIn read-only media capability export

**Current behavior:** CP7c extends this input to four sources and writes a schema-version-2 comparison report. Follow [the current comparison test plan](MEDIA_COMPARISON_TEST_PLAN.md). The single-source description below records CP7a.

Revision: `Stage1-CP7a-MediaCapabilities`. Implements the Step4C proposal approved by the user on 2026-11-15. Local build, filtering checks and 4-Series-only gateway/light/shade compilation passed. RMC4 export results are still required.

1. Keep the working program backup. Build the SIMPL program against the current CLZ in SIMPLPlus and update the gateway symbol from its newly compiled wrapper. Target **4-Series only**. The existing four parameters, leading skips, digital-to-serial separator and prior signals remain; the new input/output are appended.
2. Use Gateway_ID=1 and the existing account parameters. Load the program, check revision `Stage1-CP7a-MediaCapabilities`, and pulse Connect. No media request runs at startup.
3. Pulse **Export_Media_Capabilities** once. This issues only `GET /api/v1/items/20` through the existing local REST transport. It does not select TuneIn, start playback, change power/volume/mute, or follow returned links. Account refresh may run through the existing gateway if needed.
4. Busy_FB covers the operation. Expect `Media capabilities saved: /user/Control4Gateway_1_media_capabilities.json; browsing not yet established`. **Media_Capabilities_File$** also provides the file path. Export failure reports a nonsecret error type/status instead.
5. Download that JSON from the RMC4 user directory with Toolbox File Manager, as with the inventory export. Open it in VS Code and provide the sanitized report for inspection. Re-export replaces the same gateway-specific report.

The report includes actual HTTP status, item count, URIs presence and bounded structural summaries. It lists sanitized explicitly advertised GET paths separately from local paths whose method is unknown. Unknown or sensitive keys/values are omitted; same-host HTTPS paths must use recognized API structure/numeric ID segments, and query strings/fragments are removed. This conservative filter can omit legitimate unrecognized paths; absence of a path does not prove the endpoint is unavailable. A traversal limit is indicated by inspection_truncated, and shape depth limits by children_omitted.

`browse_endpoint_status` remains **not_established** for this metadata test even if a browse-looking path is advertised. The next step would be reviewing that specific route and, only when its semantics are established, testing one actual read-only browse request. A successful item-detail export is not proof of catalog browsing.

No raw response, account/token field, user profile, media title or listening history is written into this report. Strings appear only as conservative normalized local paths; structural summaries report types/counts and allowlisted key names. Unrecognized URI representations are summarized without interpreting bare strings as links.

Code and checks: GatewayMediaCapabilities.cs, the gateway adapter/wrapper additions, and tests/MediaCapabilitiesChecks.ps1. The existing Request helper now also has a private status-returning overload so this report records the observed status rather than assuming 200. Framework and SDK configuration are unchanged.
