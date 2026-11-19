# JSON inventory from SIMPL

Use the gateway module's **Export_Inventory** digital input after connecting. The shared gateway reads Director's items and the lights, motorization, audio_video, comfort, sensors and cameras categories, then writes an indented UTF-8 JSON file:

`/user/Control4Gateway_1_inventory.json` for Gateway_ID=1.

On completion, Inventory_File$ provides the path and Diagnostic$ reports `Inventory JSON saved: ...`. Busy_FB stays active during the export. Use Crestron Toolbox File Manager to download the file from the RMC4's user directory, then open it in VS Code or a text editor. No Python, Composer Pro, manual token or certificate entry is needed.

The file contains generated time, gateway/controller address, all_items and categories. Each item includes item_id and available name/type/room metadata. Check categories.motorization for shades and categories.lights for lighting. Unsupported categories are empty with a nonsecret entry in errors. A category missing from Director does not prevent the other categories being exported.

item_id is the REST identifier returned by Director. All-items results can include both protocol drivers and proxies; do not treat every entry as a Proxy ID. For a light or shade module, select the appropriate proxy item from the relevant category, then verify it with Query. The exporter does not infer a proxy solely by adding one to the driver ID or rename every item_id to proxy_id.

Only selected inventory metadata is exported; arbitrary API fields, account passwords, bearer tokens and authentication responses are excluded. Re-export replaces the previous file for that Gateway_ID. No devices are changed and no discovery runs at program startup.

Recompile the updated gateway against the current CLZ for **4-Series only**, update the symbol in SIMPL Windows, and verify wiring. Existing parameters and four leading skips remain unchanged; Export_Inventory is appended after Disconnect and Inventory_File$ is appended after Diagnostic$.

This checkpoint has local build/parser checks; actual RMC4 filesystem writing and Director inventory response compatibility require processor validation. If export fails, share Diagnostic$ rather than account parameters or raw authentication data.
