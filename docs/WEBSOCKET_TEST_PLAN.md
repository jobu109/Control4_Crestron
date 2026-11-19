# RMC4 WebSocket checkpoint

Current revision: `Stage1-CP5-WebSocket-Diag2`. The user has confirmed RMC4 subscription startup, initial synchronization, SET_LEVEL with live feedback, external keypad/app feedback and explicit Disconnect/Connect recovery. The locally packaged CLZ hash is recorded in the main handoff; the processor trace did not include its deployed hash. Network outage recovery and full-lifetime token renewal are the remaining reliability tests.

1. Back up the working processor program. Use the CLZ beside the wrappers in `SIMPLPlus`, compile for **4-Series only**, and update the gateway/light symbols in SIMPL Windows. Check signal wiring after the leading parameter skips were added.
2. Set gateway `Gateway_ID=1`, `IP_Address$=192.168.1.160`, and the account parameters. Set light `Gateway_ID=1`, `Proxy_ID=437`. No manual Director token or certificate configuration is needed.
3. Load the test program. Confirm the startup diagnostic revision. No network requests occur automatically at startup.
4. Pulse `Connect` (it acquires a token automatically). Expect `REST connected; starting WebSocket`, followed by `WebSocket subscription started`. A one-time REST query synchronizes registered lights when the subscription starts.
5. Change Main Hall from the Control4 app/keypad. Expect diagnostics such as `Light 437 level 79% (WebSocket)`, with matching `Level_FB`, `On_FB`, and `Valid_FB=1`. Test On, Off and Level_Set from SIMPL too, and confirm actual light behavior. SET_LEVEL does not trigger delayed polling or infer feedback from the requested level.
6. Pulse `Disconnect`. Valid_FB should clear and further external changes should not update feedback. Pulse `Connect` again and verify subscription and synchronization return.
7. If practical, temporarily interrupt connectivity to the EA-5. WebSocket loss invalidates light feedback; retries back off to a maximum 30 seconds. Recovery should resubscribe and synchronize. Initial connection, disconnect/reconnect and external-event tests take priority over extended outage or token-lifetime testing.

Record the nonsecret `Diagnostic$` text and light/gateway outputs. Do not share account parameters or raw protocol payloads. `Connected_FB` continues to indicate successful REST connectivity, not a separate live WebSocket health signal; use the subscription and disconnect diagnostics when assessing the live channel. Busy_FB covers queued REST/auth operations, not the persistent background listener or its retries.

The local certificate policy is the user-selected automatic acceptance policy, scoped to local EA-5 REST and TLS/WebSocket sessions. Cloud sign-in keeps normal certificate validation. No fixed certificate fingerprint is embedded.

Implementation follows `pyControl4/websocket.py` and its bundled `socketio_v4` / `engineio_v3` from the original test archive: Engine.IO 3 at `/socket.io/`, JWT handshake header, clientId, 2probe event, datatoui subscription GET, startSubscription, subscription status acknowledgement and OnDataToUI routing. Token renewal causes the live subscription to reconnect using refreshed authorization; renewal across a full lifetime still needs processor validation.

Local checks: `pwsh -NoProfile -File tests/SocketProtocolChecks.ps1` and `pwsh -NoProfile -File tests/WebSocketTransportChecks.ps1`. The TLS fixture uses a temporary local certificate and loopback server; it does not connect to the EA-5 or install a trusted certificate. Windows Schannel may require running the TLS fixture outside the restricted sandbox.

## Next: automatic outage recovery

Use the current working program without a rebuild. Start with live feedback working. Temporarily disconnect only the EA-5's network connection, leaving the RMC4 reachable in Toolbox. Wait for the WebSocket failure/retry diagnostic and Valid_FB=0; a dropped connection can take roughly a minute to detect. Restore the EA-5 network without pulsing Authenticate or Connect. Capture the next subscription-start diagnostic and automatic current-level synchronization, then change the light externally to verify live events resume. Retries back off to a maximum 30 seconds, with connection attempts taking additional time. Record the entire nonsecret trace if it does not recover.

## Later: token-lifetime renewal

Leave the current program connected beyond the Director token's reported lifetime (the original Python test reported 86400 seconds). Do not pulse Authenticate/Connect during the test. Verify the live subscription recovers after automatic token refresh and still follows external light changes. The cloud service must be reachable for renewal. Keep this pending until observed; manual reconnect does not prove renewal.
