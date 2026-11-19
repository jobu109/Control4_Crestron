**CP24a supersedes this inspection checkpoint:** The CP23a capture established current-item identity and Spotify HTTPS artwork addressing. Live Sources/UI implementation is ready for runtime verification; see [UI_LIVE_NOW_PLAYING.md](UI_LIVE_NOW_PLAYING.md). Historical capture procedure follows.

# Live now-playing implementation checkpoint

The browse-first launch fix (CP22a) is RMC4 verified: selecting Spotify and HEAVY from Web XPanel preserves normal Control4 metadata, artwork and Next updates. The Crestron bridge still emits nowPlaying:null; no production metadata joins are allocated yet.

CP23a improves the existing Inspect_Now_Playing capture, without adding inputs or polling. Previous captures contain room variable value.mediainfo and wallmediainfo arrays, and service OnDataToUI EVENT.ARGS.List.item arrays with NowPlayingIndex. Picking an arbitrary title from either array could display another room or a queued track. Existing inspection discarded the index/device numbers and variable labels containing spaces, so it cannot establish current-item identity. It also treated artist_uri as artwork and omitted img references.

The updated capture records bounded variable labels (including spaces), EVENT.NAME identifiers, track text, NowPlayingIndex/deviceid/notifyDevId/medSrcDev/queueid/streamid numeric observations, and explicit artwork-field address descriptors. Artwork descriptors include scheme, host, port, path segment count, common image extension and presence of query/fragment/userinfo. They omit path, query values, fragments and credentials. Relative paths are identified without exporting their contents. Numeric index semantics remain unverified; no zero/one-based assumption has been added. Existing capture time, size and session/room/source guards remain.

## RMC4 capture

1. Compile and load the SIMPL program with the synchronized CP23a library. No Construct rebuild, rewiring or symbol replacement is required.
2. In Web XPanel select Michael's Spotify and HEAVY. Wait for playback and actual source feedback.
3. Pulse Sources Inspect_Now_Playing (existing digital join68, or the module input directly).
4. Press Next once, wait for the Control4 title/artwork to change, then press Next once more. Finish within60 seconds of step3.
5. Pulse Gateway Export_Inventory. The existing collector retrieves the report; no Desktop transfer is needed.

Then compare room-scoped metadata, measured service/renderer IDs and the service current-item index over those track changes. Implement one normalized room now-playing snapshot with explicit validity/source identity and clearing on Off/source change/disconnect. Deliver bounded frames to the Crestron UI, retain title/artist/album/artwork validity, reject stale snapshots, and use verified artwork addressing rather than guessing controller routes. Allocate/document new compact joins only when the production contract is established. Both Construct shared assets and vanilla HTML5 must consume the same contract.