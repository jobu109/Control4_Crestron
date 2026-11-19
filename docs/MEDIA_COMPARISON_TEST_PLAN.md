# Four-source read-only media comparison

Revision: `Stage1-CP7c-MediaComparison`. Extends the existing two exports without changing their SIMPL inputs, outputs or parameters. The earlier TuneIn command result was an empty list; it does not establish what the other services expose.

| Source ID | Generic label |
| --- | --- |
| 20 | TuneIn |
| 65 | Spotify |
| 599 | Amazon Music |
| 100002 | Digital Media |

These IDs come from the supplied Python research. Digital Media uses an integer ID greater than 65535; no ushort truncation is used for media source IDs. Account-specific source names are not exported.

1. Preserve the working program. Use the current CLZ and matching gateway/light/shade wrappers compiled for **4-Series only**, then compile/load the SIMPL program. Existing gateway symbols/signals remain the same.
2. Connect, then pulse **Export_Media_Commands**. It sequentially issues GET `/api/v1/items/{id}/commands` for the four sources and saves the combined report at `/user/Control4Gateway_1_media_commands.json` for Gateway_ID=1.
3. Wait for the saved diagnostic and Busy_FB=0. Then pulse **Export_Media_Capabilities**. It sequentially issues GET `/api/v1/items/{id}` for the same four sources and saves `/user/Control4Gateway_1_media_capabilities.json`.
4. Download both files using Toolbox File Manager and provide them for inspection. The existing Media_Commands_File$ / Media_Capabilities_File$ outputs report their paths. Re-export overwrites the corresponding processor file; preserve earlier downloaded evidence if desired.

Each report now has `schema_version: 2`, a `sources` array, source_count, successful_source_count, error_count and browse_endpoint_status. Per-source entries retain their source_id, generic label and the existing filtered metadata schema. This is a different report envelope from the prior single-source file; look inside sources rather than for a top-level source_id.

Unsupported endpoints and malformed source responses become per-source nonsecret errors, so other results are retained. An authorization rejection (HTTP 401/403) aborts the export through the existing gateway token handling. Disconnect cancels stale results. Network failures and parsing failures are not reported as empty command lists; a successful HTTP 200 response containing [] is separately represented as command_count=0.

Each export performs at most four local GETs. The two buttons share one export worker guard; wait for the first to complete before pulsing the other. There are no POSTs, playback/power/volume changes, arbitrary link requests, background discovery or startup traffic. Existing account/token handling and local TLS policy remain unchanged.

All privacy/path filters from CP7a/b remain. Browse-like names or advertised paths are candidates only; neither proves a usable content catalog. browse_endpoint_status remains not_established. Empty results across these four endpoints also do not prove that every external or internal Control4 browsing interface is unavailable.

Local checks passed: per-source IDs/paths and large IDs, name/path redaction, combined and partial-failure summaries, invalid/duplicate batch rejection, existing media schema filters, lighting/socket, shade and inventory regressions. Release build passed with the existing generated warning. Gateway, Light and Shade compiled for series4 only with zero errors and warnings. Actual four-source RMC4 results remain pending.
