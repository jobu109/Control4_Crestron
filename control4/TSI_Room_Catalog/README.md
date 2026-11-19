# Live Control4 room catalog pilot

This pilot lets the RMC4 read configured light and blind proxies directly from an EA-5 DriverWorks helper. No Composer project backup or panel join changes are needed. Install one helper per Director and one **Control4 - Live Room Discovery** symbol per Crestron gateway. The existing room IDs determine which rooms the panel can select.

1. In PowerShell 7, generate an installation-specific key and keep it private.

   ```powershell
   [Convert]::ToHexString([Security.Cryptography.RandomNumberGenerator]::GetBytes(32)).ToLowerInvariant()
   ```

2. Import `TSI_Room_Catalog_v2.c4i` in Composer Pro and add **TSI Room Catalog v2** to the project. Set **Director LAN IP** to the EA-5 address, **Crestron LAN IP** to the RMC4 address, **Port** to `48124`, and **Discovery Key** to the generated key. The status should say it is listening for the configured Crestron host.
3. In SIMPL Windows, add **Control4 - Live Room Discovery**. Set `Gateway_ID=1`, `Director_IP$` to the EA-5 address, `Discovery_Port=48124`, and `Discovery_Key$` to the same key. Use `Diagnostic$` and `Refresh` in Debugger. The symbol attempts an initial fetch and can be refreshed explicitly after Director starts or the project changes.
4. Compile for 4-Series and load the RMC4. Once the diagnostic says **Discovered … devices**, switch to another room and back to the room you want to test. The router reads the latest accepted catalog on room selection. A failed fetch leaves the last accepted catalog in memory. Existing SIMPL device entries override discovered entries with the same room and ID.
5. Verify Kitchen contains Main Hall (437) and Living Room contains Middle Shade (226). Check room isolation and feedback. This pilot recognizes `light.c4i`, `light_v2.c4i`, and `blind.c4i`. Review other proxy types before expanding coverage.

Keep the catalog service on the installation LAN. The listener binds to the configured Director interface and checks the connecting Crestron IPv4 address. HMAC authenticates the request and response and binds the response to a fresh client nonce. The catalog is unencrypted on that LAN and contains only room/device IDs, labels, and device kind. The helper accepts no device control requests.

The helper passed 35 native validator checks and a Lua mock with 75 devices. A TCP loopback test exercised the Crestron reader and verified tampered-response rejection. Release and Series 4 builds passed. Composer import, the actual Director-to-RMC4 transfer, and hardware control with the discovered list remain to be verified.
