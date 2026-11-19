# Add Kitchen to the RMC4 room interface

The EA-5 inventory identifies **Kitchen** as room **212** (type 8). Living Room remains room **13**.

1. In the active RMC4 SIMPL program, add another **Control4 - Room Module - Sources** and another **Control4 - Room Module - Transport**. Set both to **Gateway_ID=1** and **Room_ID=212**.
2. Confirm the Kitchen join ranges are free on the active panel symbol. Wire [UI_KITCHEN_JOINS.csv](UI_KITCHEN_JOINS.csv). The CSV direction is from the UI: send goes from the panel to a module input, receive goes from a module output to the panel. Gateway Connected_FB on digital 7 already serves both rooms. Use unique SIMPL signal names for Kitchen and preserve matching opposite-direction analog pairs within each room.
3. Compile the active SIMPL program for **4-Series** and load it to the RMC4. The existing module binaries provide this interface.
4. Reopen the **Floyd Room Media** solution in Crestron Construct, build a fresh archive, and transfer it to the touchpanel. The packaged archive in the handoff predates Kitchen. The solution's shared JavaScript source assets contain the new room.
5. Select Kitchen. Confirm its Listen and Watch lists, then test an advertised source, volume, mute, Room Off, and applicable metadata using the Kitchen zone. Switch to Living Room and confirm it retains its own source and feedback. Repeat in reverse and make an external change in each room to verify isolation.

The bridge fixture verified that Kitchen consumes its reserved joins and sends volume on analog 101 and Room Off on digital 105. Existing Living Room join coverage and bridge tests passed. Live Kitchen discovery, wiring, and hardware acceptance are pending.
