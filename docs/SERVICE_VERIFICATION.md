**Physical touchpanel acceptance passed (2026-11-18):** User confirmed the RMC4-based Construct Room Media checkpoint after the requested on-panel Pandora progress, Pause, Next, and Room Off checks plus Spotify artwork and Play/Pause. This is user-reported hardware acceptance. TuneIn, Amazon Music browsing, the Lua helper, and multi-room expansion remain on hold.

**Current scope:** User has placed TuneIn/Amazon browsing and the Lua helper on hold. Both additional Spotify/Pandora accounts are now user-confirmed through the existing preset/station workflows. Neither parked service has verified XPanel browsing/selection.

# Service verification

Multi-room expansion is on hold at the user's request until service support is reviewed. Availability and capabilities must come from the installed drivers and observed commands; unsupported selection commands must not be guessed.

| Service | Verified | Remaining |
|---|---|---|
| Michael's Spotify | Source/preset discovery, selection/playback, metadata/artwork, track updates, station label | Recheck reported Play/Pause action for this service. No elapsed events observed; native Control4 also has no progress. |
| Geannie's Spotify | User-confirmed presets, playback, metadata/artwork/Next, Pause/Play and Room Off | No elapsed feedback in the verified Spotify path. |
| Michael's Pandora | Stations, selection/playback, metadata/artwork/Next, reported Play/Pause action, station label; progress visible | Complete progress Pause/Next/Off acceptance if not already checked. |
| Geannies Pandora | User-confirmed stations, playback, metadata/artwork/Next, Pause/Play, progress and Room Off | Physical-panel acceptance remains separate. |
| TuneIn | Proxy20→protocol19 relationship; room catalog includes27 owned records | On hold. Browse results/IDs observed, but driver events are Lua logs rather than selection requests. Helper not accepted by Composer; installed Navigator request definitions remain unavailable. |
| Amazon Music | Source599→protocol598 verified; both command lists empty; saved catalog contains0matching/0omitted items | On hold. User logged in and reports both native browse/search and presets. Fresh saved catalog remains empty; XPanel browse/selection unverified. |
| Digital Media | Used as renderer in actual playback paths | Treat as a renderer rather than assume a saved-station provider. |

Both listed Spotify accounts and both listed Pandora accounts have now been tested by the user. Other unlisted accounts/drivers remain unverified. Physical-panel acceptance is separate from current Web XPanel results. Other room/provider availability should be established from inventory as testing proceeds.

