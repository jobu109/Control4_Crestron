**CP24a extension:** Live now-playing uses the same bounded framing as stream `n`, with serial16, analog20 request/feedback, analog21 page count and digital99 validity. Existing source/media/capability assignments remain unchanged. See [UI_LIVE_NOW_PLAYING.md](UI_LIVE_NOW_PLAYING.md). The historical unfinished-metadata notes below describe the CP21a stage.

**Runtime result:** User confirms Web XPanel Spotify browsing and HEAVY selection/playback after the startup correction. The framed source/media path is verified for this flow. A new report of missing metadata inside the Control4 app is being investigated separately; Crestron live metadata outputs are still unfinished.

**UI startup follow-up:** User confirms serial3 now carries the new audio packet1/5. A browser replay-order defect was reproduced with that exact packet: lists arriving before Connected feedback lost their next-packet request. The bridge now resumes pending audio/video/media requests once on a disconnected→connected transition. Repeated Connected events do not restart transfers. This correction only requires rebuilding/transferring Construct UI; CP21a processor library/joins stay unchanged. Hardware validation of the startup correction remains pending.

# 255-character UI serial transport

## Verified problem

The user reports Web XPanel volume/mute commands and Room Off work, but sources are absent. Panel serial3 contains a JSON source-list prefix cut at255 characters; a browser CrComLib.getState('s','3').length check also returns255. No Make String Permanent or serial-processing symbol is on this route. This confirms truncation on this deployment's serial path; the exact layer imposing the limit is not established. Do not generalize it to all RMC4 string storage or all CH5 projects.

## CP21a fix

No joins or module pins change. The .NET4.7.2 class-library/4-Series-only architecture, account authentication, local certificate policy and public station IDs are retained.

- Sources/audio JSON stays on serial3; video on4; media on5.
- Complete logical JSON documents are UTF8/base64 encoded and divided into ASCII JSON frames of at most255 bytes. Names and IDs are not shortened.
- The frame includes wire version `w:1`, room `r`, stream `k` (a/v/m), snapshot `e`, packet `p`, packet count `n` and base64 fragment `d`.
- The UI requests missing frames through the existing analog Audio_List_Page_Set3, Video_List_Page_Set4 and Media_List_Page_Set2 inputs. Those page/Pages feedback signals now refer to transport packets. The widget still shows its own six-row pages after the whole list is assembled.
- A new document resets its packet index to1. Repeated requests reuse the same snapshot and packets. The UI rejects wrong-room, superseded, malformed or conflicting frames and enables media selection only after complete validated assembly.
- A deliberate analog0 then requested page, separated by25ms, is used on first/repeated-value requests so SIMPL+ CHANGE sees a transition. This is an acknowledgement-driven local transfer, not a periodic Control4 query or REST poll.
- Snapshots are bounded to256KiB; larger documents return a wire_error instead of silent truncation. No payload is rendered from an incomplete document.
- Transport capabilities stay on serial8 using compact `wire:keys1` with50 advertised/local/unavailable state codes in the existing Sr260Keys order. The UI expands them into key objects before applying the existing capability gate.
- Inventory exports retain their readable full data. The wire frame format is specifically for module-to-UI list outputs.

## Install the matching pair

1. Updated CLZ and matching cache/module signatures have been synchronized to Public Documents/Crestron/SIMPL/Usrsplus with backups. All9 wrappers compile for series4 only; existing signatures remain unchanged.
2. Close/reopen SIMPL Windows as needed to pick up the updated library, compile the program using it and load the RMC4.
3. Close stale Construct page buffers without saving, reopen the workspace Floyd.csln, build and transfer the new archive.
4. Hard-refresh Web XPanel, authenticate/connect the gateway as usual, then wait for the source cards. Both processor library and UI need this update; old and new wire protocols are not a supported deployment pair.
5. Select Michael's Spotify (the actual exported name), wait for5 presets, then HEAVY. Confirm actual sound before Pause/Play tests. Do not expect a literal source called Spotify if the room's list uses an account-specific name.
6. Retest app feedback and UI volume/mute/Room Off. No join rewiring is needed.

The editor canvas remains a labeled static preview. Runtime displays real joins. Metadata still has no normalized live output; a waiting track-information state is expected.

## Local validation and limits

Release build passes with the existing generated CS0162 warning. C# tests check stable packets <=255 ASCII bytes and lossless UTF8 round trips with162 long/quoted/Unicode names. Generated C# packet vectors are consumed by JavaScript tests, including missing/wrong-room/stale/conflicting frames, compact50-key decoding, complete source/media assembly and repeated-request transitions. Room/media/selection/Transport tests now decode the wire transport before checking their original semantic contracts. Automatic service mapping/browse, dynamic selectors, measured source and event-driven feedback regressions pass. All9 series4 compiler logs report zero errors/warnings and signature checks pass.

Actual RMC4/Web XPanel behavior with CP21a remains unverified until the user loads both components. Packet-loss/reconnect recovery and large concurrent-client transfers require hardware testing; local simulations do not establish those outcomes. There is no automatic retry/poll loop for indefinitely stalled packets in this revision.
