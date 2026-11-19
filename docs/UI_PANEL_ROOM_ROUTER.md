# Reusable RMC4 panel room interface

Use this interface for the Living Room (13) and Kitchen (212). The deployed Living Room program and existing archive are the reference for rollback. The older per-room Kitchen wire list is historical.

## Live room picker

Configure the allowed rooms in the Panel Room Router's Room_IDs$ parameter. In live mode the XPanel requests this list from its own router over serial join 101. The router returns the ordered IDs and Director names as paged frames over serial join 102. The XPanel requests the list again when the gateway reconnects. A valid ID missing from Director inventory is labeled Room followed by its ID. The embedded list is used only in preview mode.

For the first deployment, compile the 4-Series program with the updated gateway library and deploy the matching FloydControl4 CH5 archive. After that, adding a room to Room_IDs$ requires a SIMPL program update only. The light and shade catalogs are discovered through the existing Director connection.

## Wire once per panel

In the active SIMPL program, use one **Control4 - Panel Room Router** per touchpanel or independently wired XPanel symbol. Set Gateway_ID to **1** and Room_IDs$ to **13,212**. Assign distinct SIMPL signal names per panel even though their join numbers match. Each router selects and subscribes to only its panel's selected room. Commands carry that room ID and the router rejects commands for a different room.

| UI direction | Type | Join | SIMPL signal |
| --- | --- | --- | --- |
| Send | Serial | 101 | Request$ |
| Receive | Serial | 102 | State$ |
| Receive | Serial | 103 | Audio$ |
| Receive | Serial | 104 | Video$ |
| Receive | Serial | 105 | Catalog$ |
| Receive | Serial | 106 | Playing$ |
| Receive | Serial | 107 | Progress$ |
| Receive | Serial | 108 | Keys$ |
| Receive | Digital | 7 | Existing Gateway Connected_FB |

The updated CLZ, USP and USH were built for 4-Series and synchronized to the package and installed Usrsplus folder. Previous installed versions were backed up. Reopen SIMPL Windows, confirm these joins are free on the actual panel symbol, wire the table, compile and load the active program for 4-Series. Keep the Living Room modules and wiring as the reference. Reopen Floyd Room Media in Construct, rebuild, and transfer the new archive. The archived binary already in the handoff is the earlier build.

## Acceptance

1. Select Living Room on one independently wired panel/client and Kitchen on the other. Verify each has its own sources and volume. Make a volume change on each and independently confirm the physical room and the other panel's unchanged state.
2. Browse a supported service, select a station or preset, and check metadata, remote keys, mute and Room Off. Switch rooms mid-use and confirm the selected room owns subsequent commands and feedback. In this first router build, open a service source to load its saved-item list.
3. Interrupt and restore the EA-5 network with the RMC4 and panels powered. Verify feedback is restored and a normal control command remains responsive. Measure this during rapid changes before extending the number of zones.

Local validation includes two independent simulated clients, wrong-room feedback rejection, reconnect re-selection, the existing Living Room join regression, and 4-Series signature checks. Initial room selection reuses current gateway discovery data when available. Qualifying refresh bursts at whole-house scale remains separate rollout work.

## Lighting and shade configuration

Add **Control4 - Room Devices** in SIMPL Windows for each room with lighting and shades. This symbol registers devices with the existing Gateway_ID; it needs no panel wires. Wire Diagnostic$ only if you want the setup result in SIMPL Debugger.

For the original pilot use Gateway_ID **1** and Room_ID **13**. Enter Lights$ as `437=Living Room Light` and Shades$ as `226=Middle Shade` if you want the original labels. Use semicolons between entries, for example `437=Main Hall;438=Counter Light` (438 is an example only). Supply confirmed IDs from your project. Each field accepts 255 characters. If a room needs more room in the parameters, add another Room Devices symbol for the same room with distinct IDs. The router supports up to 128 configured devices per room.

Construct receives the selected room's device list and initial measured values in paged frames and renders one row per device. Subsequent feedback is keyed by device ID. Commands and feedback still use serial 101/102. Existing router parameters and panel wiring carry over. A room with no configured devices hides the device controls.

The EA-5 inventory export provides IDs and names but the saved export for this system does not include per-device room IDs. Control4 documents the project XML API C4:GetProjectItems for on-controller DriverWorks code. An import from a Composer backup or automated discovery is separate and has not been verified on this system.

Recompile and load SIMPL with the installed CLZ and the new Room Devices symbol. Rebuild and transfer Floyd Room Media from the updated Construct source. Verify two lights and two shades in one room, then switch rooms and check isolation. Local browser tests verified multiple rows, paged delivery, device commands and feedback filtering. Runtime validation of this version is pending.
