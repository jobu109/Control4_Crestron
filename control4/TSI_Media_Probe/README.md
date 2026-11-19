**Current test build: TSI_Media_Probe_v4.c4i — Composer3.4.3**

User confirms v3 opens in Driver Editor but Composer fails when adding the search result to Rack, with Unable to add to Rack. This is a project/controller-load failure, not evidence of an XML/project-open failure. Found and corrected a real lifecycle defect: earlier Lua called C4:UpdateProperty in OnDriverInit, prohibited by the documented API contract. v4 performs no such API work until OnDriverLateInit and ignores early property/actions. Contract-enforcing mock test reproduces the old violation and passes the fix. Single-file v4 passes all35 native validator tests. These checks do not prove this was the only cause of Composer failure.

1. Composer Driver > Add Driver: select TSI_Media_Probe_v4.c4i.
2. Search the local database for the exact new name TSI Media Probe 4 (manufacturer TSI, version4), avoiding cached v1-v3 entries.
3. Add that result to Rack.
4. If it adds, verify Protocol ID19/Proxy ID20, run Actions > Inspect TuneIn and report Status/Report Pages/Report Text page1.
5. If it still says Unable to add to Rack, obtain the controller/Composer load error for that attempt before another package revision. Composer version is3.4.3; exact Director version remains unverified.

No Crestron program/joins change. The diagnostic still sends no media/control/network commands. Native validation log: reports/MediaProbe-native-validation-v4-c4i.log.

**Current install alternative: single-file v3**

If Composer rejects the archive, use TSI_Media_Probe_v3.c4i. It contains the Lua and documentation inline, with no ZIP/external file dependencies. Composer: Driver > Add Driver, select the .c4i, then search Local for TSI Media Probe/version3. Driver Editor: use File > Import Driver for the .c4i. Open Driver Project expects a .c4zproj XML manifest, not a packaged driver. Do not use an archive as a project.

Verification: v3 passes all35 applicable native DriverValidator tests; its embedded Lua parses/initializes in the isolated test runtime. The installed Driver Editor's actual DriverExplorer.LoadSolution method successfully loads the extracted v2 .c4zproj through a32bit STA harness (reports/MediaProbe-actual-editor-loader.log). That verifies the project-loader path, not Composer/controller installation. User reports packaged v2 still rejected by Composer and root-level error in Driver Editor; exact selected file/Composer failure cause remains unresolved. No claim of deployment success.

**Version2 correction:** The original archive failed native Control4 validation (XML declaration and missing proxy). Those were real packaging defects, now fixed. Use TSI_Media_Probe_v2.c4z. A standard contact proxy and matching internal binding make the diagnostic compatible with the installed legacy toolchain; no contact state is published and no physical contact is bound. The original package filename is also refreshed with the corrected version.

## Open in Driver Editor

1. Copy TSI_Media_Probe_Editor_v2.zip to the Driver Editor PC.
2. Extract the entire ZIP into one folder; keep driver.xml, driver.lua and www alongside the project file.
3. In Driver Editor choose File > Open and select TSI_Media_Probe.c4zproj from that extracted folder.
4. For Composer installation, use the separately built TSI_Media_Probe_v2.c4z, not the .c4zproj or source ZIP.

# TSI Media Probe — first bridge diagnostic

This custom DriverWorks driver reads selected public metadata from installed devices19/20. It does not start/stop media, send service commands, modify existing drivers, open network connections or require a SIMPL/Construct update. This is a diagnostic package, not the complete browse/search bridge.

## Install on the Composer PC

1. Copy TSI_Media_Probe_v2.c4z from this directory to the PC running Composer Pro. Keep the package intact; do not unzip it for installation.
2. Connect Composer Pro to the existing EA-5 system.
3. Choose Driver > Add Driver. Select TSI_Media_Probe_v2.c4z and open it to add it to the local driver database.
4. In System Design, select the Rack room. On Search, enable the local database and search for TSI Media Probe (manufacturer TSI).
5. Add that driver to the project. The helper has an internal compatibility contact proxy, but no user Connections wiring is required; leave TuneIn unchanged.
6. Select TSI Media Probe. In Properties verify Protocol ID=19 and Proxy ID=20.
7. Open Actions and run Inspect TuneIn once.
8. Return to Properties. Send the Status, Report Pages and Report Text for Report Page1. To read more, edit Report Page to2,3,etc. Each page is limited to240characters.
9. If the helper has a Lua Output pane available, Print Report prints the complete sanitized report. You can save/copy that output into a text file and place it under research/drivers on the development PC. A Lua script execution pane is not needed for either action.

If Composer cannot find or add the package, report the exact message. The native Control4 validator now passes all41checks; actual Composer/controller acceptance is still pending. Removing this helper from the project reverses its installation; it makes no playback/configuration changes.

## What it checks

- C4:GetDeviceData reads selected devicedata tags; documentation notes two-level limitations. Available from2.10.
- C4:GetDriverConfigInfoById reads selected root configuration tags only when the function is exposed; documented from4.2. Missing API or error is reported, not bypassed.
- Reports only bounded tag names and selected public Name/Command/ScreenId identifiers. It does not dump Lua, properties/default credentials, URL values or arbitrary XML content.
- API presence does not guarantee the TuneIn UI definitions are exposed. That is the point of the first probe. No browse/search handler is assumed.

Reference: https://snap-one.github.io/docs-driverworks-api/

## Verification

Version2 was built with Control4 DriverPackager and passes all41 native DriverValidator tests, plus the mock Lua checks in tests/Control4MediaProbeChecks.py: action dispatch, old/new API capability paths, no automatic inspection, paging bounds, invalid IDs and private-value suppression. It has not yet been installed in Composer or run on the EA-5. No Crestron library, target, authentication or certificate policy changed.
