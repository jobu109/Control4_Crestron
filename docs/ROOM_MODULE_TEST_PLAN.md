**Historical first Room checkpoint.** Current Sources/Transport behavior and removal of the periodic fallback timer are documented in [EVENT_DRIVEN_ROOMS.md](EVENT_DRIVEN_ROOMS.md) and [ROOM_SOURCES_TRANSPORT.md](ROOM_SOURCES_TRANSPORT.md).

# Room module: first checkpoint

Revision `Stage1-CP11a-RoomModule`. New symbol **Control4 - Room**, class Control4_Room. Configure only Gateway_ID=1 and Room_ID=13 for the Living Room. No TV, matrix or output parameters are required. Preserve the existing shared Gateway symbol. Existing Audio/Video modules can remain during comparison testing and share the same room state; do not feed conflicting volume inputs into both interfaces.

The Room module is the preferred new installer interface. Existing Audio/Video symbols are retained while the replacement is hardware-tested. It does not yet provide transport execution, content catalogs or exact active-source feedback.

## RMC4 test

1. Add Control4_Room.usp from the synchronized Usrsplus folder. Set Gateway_ID=1, Room_ID=13. Two parameters reserve two leading skips on both signal lists. Input separator precedes Volume_Set; output separators precede analog outputs and serial outputs. Compile/load for **4-Series only**.
2. Connect the shared gateway. After WebSocket subscription, source discovery reads inventory and the verified UI configuration endpoint. Check Initialized_FB, Sources_Valid_FB, Room_Name$, Has_Video_Sources_FB/Has_Audio_Sources_FB and both source counts. Initialized_FB means registration succeeded; Sources_Valid_FB means that room was found in parsed source discovery. If necessary pulse Discover_Sources to refresh.
3. Pulse Browse_Audio_Sources. Listed_Source_Name$/Listed_Source_ID$ show the first Listen source. Next_Source/Previous_Source navigate without executing commands. Source_Position_FB is one-based (zero for no entry); Source_Count_FB is the current list length. Browse_Mode_FB=1 is Watch, 2 is Listen. Lists stop at their ends.
4. Navigate to Spotify, then pulse Select_Listed_Source. Confirm the Control4 room switches to Spotify. This activates the source; it does not choose a station/playlist or imply playback. Source IDs are handled internally, including IDs above65535.
5. Pulse Browse_Video_Sources, navigate to DIRECTV Stream, then Select_Listed_Source. Confirm the app/TV selection. The listed entry is a browser cursor, not feedback for the active source. Navigating to another entry must not switch anything.
6. Test Volume_Set (0–100), Mute_On_btn/Mute_Off_btn and Room_Off_btn. Confirm Power_FB, Volume_FB and Mute_FB with their individual validity outputs. Aggregate Valid_FB requires all three fields. Query remains available.
7. Test the Room module without the old Audio/Video modules to verify it operates independently of their endpoint parameters. Unmapped or partially mapped Room registrations use one shared two-second timer to request room-state readback, skipping rooms with a query already in flight. Fully configured legacy mappings continue event-triggered refreshes. Up to64 distinct Room IDs per gateway are accepted. Feedback latency and controller load across multiple rooms still require hardware validation.
8. Change another room and verify isolation. Disconnect must clear source validity/list entries and measured-state validity; reconnect should rediscover and synchronize. No timer or discovery starts in constructors/offline initialization. Automatic discovery/fallback start after WebSocket subscription; explicit Query/Discover remain available while REST is connected.

## Capabilities and limits

Has_Video_Sources_FB and Has_Audio_Sources_FB come from the controller's advertised Watch/Listen lists. They do not assert that every advertised device implements playback control. Missing/unsupported room discovery leaves Sources_Valid_FB low; empty lists are not guessed from inventory names. Discovery is shared/coalesced at gateway level and refreshed on subscription/reconnect or explicit Discover.

Transport_Available_FB and Content_Browse_Available_FB are currently low. Play/Pause/Stop/Next_Track/Previous_Track inputs report an unavailable diagnostic and send no guessed commands. Panel controls should honor those capability signals. Source-list browsing is implemented; station/playlist/album/track browsing needs a verified interface or separately scoped bridge. Exact active-source feedback and playback metadata are also unfinished.

The existing setup report now lists exact Room module parameters for discovered rooms, with no endpoint IDs required for this interface. Legacy endpoint relationships remain marked separately as configured/unresolved.

Local verification: Release build passed with existing generated CS0162 warning; Room startup/no-I/O, named Watch/Listen paging, large IDs, room filtering, offline rejection, disconnect invalidation and shared mapping upgrade checks passed. Existing setup/Audio/Video/capture/socket/shade/inventory checks passed. All eight wrappers compiled series4 with zero errors/warnings; installed/package USP/USH/CLZ hashes match. The new Room module, automatic discovery and fallback timing remain RMC4-unverified.

