# Control4–Crestron: verified-load baseline / conservative module merge

> **Historical merge notes.** This document describes an earlier .NET Framework **4.7** snapshot. The current project targets **4.7.2** and has SDK **2.21.274/2.22.40** paths and imports. The folder was the user-confirmed working baseline; it now contains a locally built account-auth change awaiting SIMPL+ and RMC4 validation. Follow [the main handoff](../CONTROL4_CRESTRON_VS2026_HANDOFF.md) for current development.

## What this archive is

This is the **working Stage 1 .NET Framework 4.7 solution** with **no C# source changes**. It is NOT the newer `Control4Crestron.sln` implementation with a different registry / events / SDK. The four individual device families from the newer project's architecture are represented using the matching existing Stage 1 C# adapter types, with added minimal SIMPL+ wrappers for loading/binding tests.

**Preserved exactly:** original `Control4Gateway.sln`, `Control4Gateway.csproj`, all ten C# source files in `Core/` and `Devices/`, `Control4_Gateway.usp`, `Control4_Light.usp`, and SDK 2.21.274. Target remains `v4.7` (the version confirmed to load on the RMC4). The previous `IPAddress` / `Interlocked` disambiguation fixes remain.

## What's new

- `SIMPLPlus/Control4_Shade.usp` calls `Control4_Shade.Initialize(Gateway_ID, Control4_Item_ID)`.
- `SIMPLPlus/Control4_Audio.usp` calls `Control4_Audio.Initialize(Gateway_ID, Control4_Room_ID)`.
- `SIMPLPlus/Control4_Video.usp` calls `Control4_Video.Initialize(Gateway_ID, Control4_Room_ID)`.
- `SIMPLPlus/Control4_Generic.usp` calls `Control4_Generic.Initialize(Gateway_ID, Control4_Item_ID)`.
- `SIMPLPlus/Control4_Library_Load_Probe.usp` references the library but intentionally calls no C# method.

`Initialized_FB` means only that the corresponding adapter's `Initialize` call returned. **It does not indicate Control4 connectivity or operational commands.**

## Suggested controlled deployment order

1. Keep a backup of your known-working SIMPL program and previous known-working `.clz`.
2. In Visual Studio, open `Control4Gateway.sln` and rebuild **Release**. Check the actual target shown in project properties is .NET Framework 4.7. Use its freshly built `Control4Gateway.clz` rather than an older one.
3. Compile `Control4_Library_Load_Probe.usp` against the same newly built library; load a test SIMPL program containing only this wrapper, and verify the program starts.
4. Compile and test `Control4_Gateway.usp` alone, **Connect held LOW, token empty**. Gateway_ID=1, IP_Address$=192.168.1.160. Verify program starts without `SplusManagerApp` crash.
5. Add `Control4_Light.usp` with Gateway_ID=1, Item_ID=437, without sending commands. Verify startup.
6. Add one of the new module wrappers at a time, with Gateway_ID=1, using a real matching device / room ID. Check program starts and `Initialized_FB` rises. Do not add all modules at once before confirming each.
7. Stop and collect Toolbox `ERRLOG` on first failed startup. Restore known-working program as needed.

## Not yet functional

- Automatic Control4 account authentication and token renewal
- CA/certificate trust strategy for the EA-5's local HTTPS API
- Socket.IO / WebSocket feedback
- Real-time light feedback parsing
- Shade, audio, video and generic device commands / feedback

The current gateway has removed manual token entry and uses account authentication only. **Never place real tokens or account passwords into SIMPL source or share them in logs.**

## Compatibility caveat

Although the baseline already started successfully on your RMC4, the newly added wrappers have **not** been compiled using Crestron's SIMPL+ editor or tested on your processor here. This bundle contains editable source and Crestron SDK files, **not a verified compiled `.clz`**.
