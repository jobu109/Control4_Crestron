# Panel Room Router configuration

In SIMPL Windows, update the Control4 - Panel Room Router symbol and add one Control4 - Room Config symbol for each room with exclusions or lighting scenes. Enter the room's Director ID in Room_ID and positive comma-separated Director item IDs in Exclude_IDs$. For example, Room_ID 212 and Exclude_IDs$ 524,602 hides those two items only in Kitchen.

Wire the module's Room_Config$ output to an unused Room_Config$ input slot on each router that should apply it. Give each configuration module its own serial input slot on a router. The same output can fan out to multiple routers. Multiple slots for the same room combine their exclusion lists. Use the proxy or source ID displayed by discovery. There are 64 configuration slots per router and 249 characters per CSV parameter.

The filter applies to room and whole-house discovery of lights and shades, room fans, relays and experience buttons, and the room's Listen and Watch sources. Room summary counts follow the visible device catalog. Whole-house scenes and macros remain under the house actions catalog. This change uses the current panel joins and requires the updated Sytrohs CH5 archive.

Enter the Control4 Advanced Lighting scene names in Light_Scenes$, separated by commas. Existing semicolon-separated lists also work. Matching uses the full name, ignores letter case and trims surrounding spaces. Only unique lighting scene names are assigned. Names containing a comma, semicolon, or vertical bar are unsupported by this parameter. Leave the field blank to omit the Light Scenes drawer for that room. The drawer appears on the right side of the room Lighting page. Tap a scene once to activate it.

Update existing Room Exclusions instances from the Room Config symbol. The symbol keeps its original library filename, existing room ID and exclusion parameters, and Room_Config$ wiring. Each scene list supports 249 characters. Multiple slots for the same room combine their scene assignments.

Compile for 4-Series, deploy, then check Kitchen's room view and whole-house Lighting. Verify a second panel without the configuration still displays the excluded items. To test sources, add a discovered Listen or Watch source ID to Kitchen's list and confirm that source disappears from Kitchen while remaining in a different room where it is available.

If configured scene buttons are missing, watch the existing Room_Config$ serial net in SIMPL Debugger during startup. A scene assignment for room 13 with no exclusions should have the shape 13||Scene One, Scene Two. The processor Error Log reports out-of-scope room IDs, unmatched Advanced Lighting names, and duplicate matches after scene discovery.
