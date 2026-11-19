# TuneIn command metadata: read-only Step4D

**Current behavior:** CP7c extends this input to four sources and writes a schema-version-2 comparison report. Follow [the current comparison test plan](MEDIA_COMPARISON_TEST_PLAN.md). The single-source description below records CP7b.

Revision: `Stage1-CP7b-MediaCommands`. The preceding TuneIn item-detail export succeeded on RMC4 and advertised `/api/v1/items/20/commands`. This checkpoint inspects that known endpoint using **GET**, following pyControl4 C4Director.get_item_commands. No POST is performed and no command is executed.

1. Preserve the working program. Load the matching new CLZ/wrappers compiled for **4-Series only** and update the gateway symbol in SIMPL Windows. Existing parameter skips/type separators are retained; Export_Media_Commands and Media_Commands_File$ are appended.
2. Pulse Connect, then **Export_Media_Commands**. No request is made at startup. The explicit export shares existing authentication, local TLS policy and Busy_FB handling.
3. Expect `Media commands saved: /user/Control4Gateway_1_media_commands.json; browsing not yet established`. **Media_Commands_File$** provides that file path for Gateway_ID=1. Download it with Toolbox File Manager and open/send the JSON report.
4. If the endpoint rejects GET, reports malformed data or cannot be reached, capture the nonsecret diagnostic. Do not fall back to POST or execute a candidate command just because its name sounds read-only.

The prior Export_Media_Capabilities input/file continues to inspect item detail separately. The new export does not overwrite that earlier evidence.

Report fields include actual HTTP status, request method/path, response type, command-list recognition and counts, recognized command names and parameter definitions, and explicit traversal/omission flags. Supported list schemas are an array of definition objects or an object containing a commands array. Name aliases are matched case-insensitively. Unrecognized schemas produce a bounded structural summary instead of being mislabeled an empty command list.

Recognized parameter metadata includes identifier-like names, allowlisted types, Boolean required flags and numeric min/max bounds. Parameter defaults, enumeration contents, labels, descriptions, arbitrary fields, credentials, profiles and media/history content are not exported. Command counts are bounded at 256 and parameter definitions at 32 per command; flags indicate truncation. Filtered/unknown entries are counted.

`browse_name_matches` is only a name-based heuristic list. A match such as GET_LIST or SEARCH does not prove a callable native browse contract, correct arguments, session setup, response paging, or a content catalog. `browse_endpoint_status` remains not_established. Review the actual metadata and official proxy documentation before proposing any next operation. There is no speculative URL probing or automatic request to an advertised link.

Local verification passed: media command parsing/filtering/bounds, existing media capability filtering, socket/routing, shade and inventory checks; Release build with the existing generated CS0162 warning; gateway/light/shade SIMPL+ compilation with zero errors/warnings for series4 only. Actual command endpoint response compatibility on the RMC4 remains pending.
