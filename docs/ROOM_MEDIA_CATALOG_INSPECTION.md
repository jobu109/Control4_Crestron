**Native media list update:** [ROOM_NATIVE_MEDIA_LISTS.md](ROOM_NATIVE_MEDIA_LISTS.md) documents read-only named catalog pages confirmed by the room's advertised REST routes. Media-item activation and service-account library browsing remain separate.

# Native room media catalog: read-only inspection

Revision `Stage1-CP15a-RoomMediaInspection`. Adds Sources input Inspect_Media_Catalog. This is research toward content browsing; it does not enable a playable catalog or Spotify playlists yet. Existing measured selection and event-driven feedback remain unchanged.

## Evidence and scope

The actual Room13 command list advertises SELECT_AUDIO_MEDIA:PLAYLIST/ALBUM/BROADCAST_AUDIO/MOVIE and SELECT_VIDEO_MEDIA:MOVIE/BROADCAST_VIDEO with mediaid/deselect parameters. Their mediaid definitions are typed as lists. That establishes media-selection command metadata, not where to obtain valid IDs or whether a list represents a streaming service's library.

An official [Control4 OS3.4 release note](https://docs.control4.com/docs/product/control4-software/dealer-release-notes/english/revision/AQ/control4-software-release-notes-rev-aq.pdf), issue C4OS-8048, names GET /api/v1/locations/rooms/[roomId]/media/movies and an empty-list defect. This supplies a documented native read endpoint to test, not a guarantee of data on this EA-5. DriverWorks internal media APIs are not assumed accessible over external REST. Third-party API-client claims were research leads only; no unverified generic /media, Spotify, playlist, browse or search endpoint is probed.

## What the pulse does

1. GET the configured room's commands and inspect only the six established media-selection commands' mediaid parameter structures. Export safe key/type/count summaries and eligible advertised paths; omit defaults, arbitrary strings, titles, credentials, account/profile/history content and artwork.
2. GET the documented room media/movies path.
3. Follow at most six additional **explicitly GET-advertised**, same-controller paths inside this exact room's /media subtree. Query strings/fragments are removed; unrecognized segments, other hosts and other rooms are rejected. Unknown-method paths are reported but never followed automatically. No arbitrary URI input is exposed.
4. Record HTTP/format errors separately from recognized empty lists and unknown schemas. Response summaries include types, counts, bounded structural samples and up to eight positive numeric IDs, preserving large IDs as strings. These IDs are observations, not verified playable service media. No POST/source switch/content playback is performed.

At most eight GETs per pulse including the commands request. Metadata limit524288 characters; catalog response limit1048576; traversal/entry samples bounded. The latest four inspection reports are retained in memory. Offline/unregistered rooms reject before queueing; per-room pending guard and generation/epoch/registration checks prevent duplicate or stale publication. Authentication remains shared and unchanged. There is no startup inspection or polling timer.

## RMC4 test

1. Update/reload Sources symbol and inspect wiring for the added input; compile/load for **4-Series only**, Gateway_ID=1 / Room_ID=13.
2. Connect, then pulse Inspect_Media_Catalog once. Wait for the recorded-media-inspection diagnostic. It does not need to change the room source or interrupt playback.
3. Pulse gateway Export_Inventory and say exported. The existing collector downloads room_media_inspections without another transfer/login.
4. Compare command_parameter_inspection definitions/GET links/unknown-method links and catalog_responses. An empty movie library does not establish absence of service playlists. A populated numeric-ID list still needs schema/selection validation before building the HTML content list. Content_Browse_Available_FB stays low in this checkpoint.

Local validation: Release build passed with existing generated CS0162 warning; new tests cover mediaid definitions, GET/unknown-method separation, same-room/host restrictions, privacy, recognized/empty/unknown catalog shapes, numeric IDs and offline no-I/O. Media capability/command/comparison, measured-source/event-driven and inventory regressions passed. All nine wrappers compiled series4 with zero errors/warnings and signatures checked. Package/installed USP/USH/CLZ hashes match. Actual room media responses remain pending.

