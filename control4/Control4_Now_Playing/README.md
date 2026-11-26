# Now Watching bench archive

The Roku, Shield, Apple TV, and Google TV playback metadata work is on hold. These files preserve bench tools and the experimental Composer package. The production Gateway and Sytrohs build do not use this package.

Historical bench notes follow.

# Control4 Now Playing v3

The EA-5 reads Roku status directly over the LAN and publishes NOW_PLAYING_SNAPSHOT. The RMC4 reads it from Director using the existing gateway, and Sytrohs displays feedback for the selected Watch source. Deployment uses only the Control4 and Crestron controllers. The Python files in this folder are retained as bench-test references from the previous design.

## Install and verify Roku

1. Import control4_now_playing.c4z in Composer Pro. Add or update one Control4 Now Playing instance to version 3. No physical bindings are used.
2. Set Roku IP to the television or player's LAN address. Set Watch Source IDs to the Control4 Watch item ID. For a physical Roku exposed by several Watch entries, enter those IDs separated by commas. This release supports one Roku per gateway.
3. In Roku settings, enable Control by mobile apps network access. Start a video. Composer Status should show Roku playback received. Roku app received means the Roku provided its active app but no recognized playback state.
4. Reconnect the Crestron gateway to Director so its inventory picks up the driver. Use the gateway and Sytrohs archive from the Control4 Now Playing build. Use the gateway library that recognizes the combo package. The existing Sytrohs archive supports its snapshot.
5. Open the room in Sytrohs Media Video and pause from the Roku remote. Verify the state updates. Check Sessions Video and switch back to Roku Home to verify the last playback clears.

The reader uses only Roku ECP GET active-app and media-player. It forwards the app and reports playback state, title, position, and duration when the device provides them. Google TV, DIRECTV, Shield, and Apple TV still need controller-side readers.
