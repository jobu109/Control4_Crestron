**Current interface work:** [Touchpanel/Web XPanel architecture and metadata capture](ROOM_MEDIA_INTERFACE.md). TuneIn is on hold. Existing Spotify/Pandora browsing and playback remain the baseline; live now-playing metadata is being investigated.

**CP18b automatic service browsing:** Actual audio-source changes now trigger mapping and preset-list loading; repeated feedback does not poll or autoplay. Spotify event-trigger test and Pandora capability validation are pending. [Current behavior](ROOM_SERVICE_PRESETS.md).

**Verified 2026-11-17:** CP17c discovers five named Spotify saved presets and plays HEAVY through its cached selector on RMC4. [Current service interface and evidence](ROOM_SERVICE_PRESETS.md).

**Dynamic service presets:** [ROOM_SERVICE_PRESETS.md](ROOM_SERVICE_PRESETS.md) documents named Spotify saved-preset discovery and cached selection using the verified driver command. Initial driver/source configuration remains required; end-to-end discovery testing is pending.

**Spotify playback verified:** CP17a SelectPreset plays HEAVY through protocol driver64 in Room13. [Evidence and interface](SPOTIFY_SERVICE_PRESETS.md). Automatic service-preset discovery remains unfinished.

**Current service priority:** [Spotify saved presets](SPOTIFY_SERVICE_PRESETS.md). User confirmed the Control4 Spotify screen exposes presets. Native room catalogs below are separate; their playback test is deferred.

**Native media list update:** [ROOM_NATIVE_MEDIA_LISTS.md](ROOM_NATIVE_MEDIA_LISTS.md) documents read-only named catalog pages confirmed by the room's advertised REST routes. Media-item activation and service-account library browsing remain separate.

**Next media research checkpoint:** [Native room catalog inspection](ROOM_MEDIA_CATALOG_INSPECTION.md), using explicit read-only requests; content browsing is not yet enabled.

**Current Room modules:** [Sources / Transport](ROOM_SOURCES_TRANSPORT.md), [HTML widget lists](ROOM_WIDGET_LISTS.md), [numbered source slots](ROOM_SOURCE_SLOTS.md), and [event-driven feedback without polling](EVENT_DRIVEN_ROOMS.md). Follow these and the main handoff; the material below is historical.

# Control4 ↔ Crestron modular gateway — conservative stage-1 source

> **Historical snapshot.** This page describes an earlier Stage 1 source package. For the current .NET Framework **4.7.2** project, use [the main handoff](../CONTROL4_CRESTRON_VS2026_HANDOFF.md). The folder was the user-confirmed working baseline; it now contains a locally built account-auth change awaiting SIMPL+ and RMC4 validation. Preserve its SDK configuration unless a separate compatibility test supports changing it.

**Historical status of this earlier prototype: source only; it had not yet been validated on an RMC4.**

## Based on your Newline DSX sample
- .NET Framework 4.7 **C# Class Library**, not SIMPL# Pro.
- SDK package `Crestron.SimplSharp.SDK.Library` **2.21.274** (same as sample).
- Public C# classes, `EventHandler<TEventArgs>` and SIMPL+ `RegisterEvent`.
- One `#USER_SIMPLSHARP_LIBRARY "Control4Gateway"` referenced by both wrappers.
- This project deliberately **does not** include `.ush`; SIMPL+ compiles that signature from source.

## Architecture
- `GatewayRegistry`: static shared gateway by `Gateway_ID`. A single gateway is shared by all device modules using the same ID.
- `GatewayCore`: Director token + REST transport + shared state/event dispatch. Does no work in constructor.
- `Control4_Gateway`: main SIMPL+ gateway adapter.
- `Control4_Light`: per-item lighting adapter. e.g. `Gateway_ID=1`, `Item_ID=437`.
- `Control4_Shade`, `Control4_Audio`, `Control4_Video`, `Control4_Generic`: **explicit placeholders**; no unverified commands.

## Critical limitations — read before installing
1. **Do not upload or run this on the live RMC4 yet.** The C# build and actual SIMPL+ compiler are unavailable here, so no processor-runtime or compilation claim is made.
2. Authentication is **not** yet implemented inside the gateway. The historical prototype used token injection; the current gateway has removed that input and uses account authentication only. Your Control4 Director token typically expires in ~24 hours. **Do not hardcode credentials, permanent passwords, or token values in SIMPL files**.
3. Local Controller HTTPS certificate validation **remains enabled** in this source. Your EA-5 may use a self-signed certificate, and connection may fail until a narrowly scoped, verified trust strategy is designed. No global `ServicePointManager.ServerCertificateValidationCallback = ...` override is used.
4. Socket.IO v4 / WebSocket subscription, reauthorization, retry/backoff and event-to-feedback parsing are **not implemented**. `Valid_FB` will remain low until a verified parser feeds `AcceptLightEvent`.
5. REST command JSON is a best-effort provisional shape (see pyControl4 source). It must be verified from actual request payloads before controlling physical devices. REST response JSON for queries has not been captured; query parser remains disabled.
6. SIMPL+ symbols require compilation in your installed Crestron tools; signature/structure is modeled after your Newline wrapper, not separately verified with an S+ compiler.
7. This is **not** a compiled `.clz` and should not be represented as one.

## Safe next validation sequence
1. Open `Control4Gateway.sln` in Visual Studio 2022 on Windows; Restore NuGet packages (same 2.21.274 as Newline).
2. Resolve any C# build issues, build `.clz` through Crestron package targets.
3. Compile **gateway wrapper only** in SIMPL+, without network Connect.
4. Load on a spare/test RMC4 and confirm idle stability. Do not enter credentials/tokens in persistent SIMPL code.
5. Add one light wrapper (ID 437), no commands or connection; validate boot.
6. Implement verified OAuth + safe certificate policy + token refresh in GatewayCore, then validate REST and Socket.IO incrementally with diagnostic logging.
7. Add remaining device adapters only after commands and feedback are captured and validated.

## Previous proven EA-5 test
- Controller IP `192.168.1.160`; Control4 OS 3.x.
- 491 total items; 129 lighting-related entries.
- Main Hall light ID `437`: REST SET_LEVEL 50 gave actual level 50, status ON.
- Socket.IO WebSocket produced OnDataToUI with `data.light_level` 100,79,16,0 for ID 437.
- These tests were conducted using pyControl4 from Windows, **not using this Crestron library**.

## Security
Store Control4 passwords and tokens only in suitable secured runtime configuration, not in source, logs, solution, `.usp`, `.smw` or persisted project artifacts. Do not share complete diagnostic responses that could include account data.



