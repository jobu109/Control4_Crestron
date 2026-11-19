# Automatic RMC4 report collection

Processor: 192.168.1.169. Toolbox/SSH username supplied by the user: tsiadmin.

Run from the package root:

```powershell
pwsh -NoProfile -File .\tools\Watch-Rmc4Reports.ps1
```

Enter the processor password in the local PowerShell prompt. Leave that window open. The collector maintains an SFTP session and checks the three known gateway-1 JSON reports every five seconds. When a report changes it downloads and validates it, then replaces the local copy under:

`reports/rmc4-192.168.1.169/`

Inventory, media commands and media capabilities retain their original filenames. `collector_status.json` records connection/check status and which files were collected. The assistant can read these local copies; no Toolbox download or chat attachment is needed. Pulse the SIMPL export input as before, wait for its saved diagnostic, then say “exported.” The collector does not trigger exports or load/recompile the processor program.

Close the collector window to stop. Restart it and enter the password once per session when needed. No password, encrypted credential or private SSH key is saved. Only the server's public SSH fingerprint is stored in `.local/rmc4-192.168.1.169.sha256` after the first successful authenticated connection. Subsequent SSH identity changes stop collection rather than being accepted silently. This is the PC's SSH host check, separate from the module's selected local HTTPS certificate policy.

Remote operations are SFTP existence/stat/download only on the three named `/user/Control4Gateway_1_*.json` reports. No commands, uploads, deletes or program changes run on the RMC4. Local downloads use a temporary file and JSON validation; a malformed or changing remote export does not replace the previous local report. Connection failures are retried while rejected credentials/changed identity require attention.

The PC uses Renci.SshNet.dll already installed with Crestron's Installation Gathering Tool. This dependency is used only by the desktop collector; the RMC4 C# project, SDK configuration, CLZ and SIMPL+ interfaces are untouched.

Options: `-Once` downloads one snapshot, `-IntervalSeconds 10` changes the polling interval, and `-CheckOnly` validates the local dependency/helper without authentication or report writes. Other processor IP/username/gateway ID values can be supplied explicitly. Defaults match this session.

Verification at setup: TCP port 22 at 192.168.1.169 returned SSH-2.0-CrestronSSH. The local helper compiled/constructed successfully in CheckOnly mode. After the user completed local login, collector_status.json confirmed connected=true and both media reports were downloaded and parsed successfully. Inventory was not present remotely at that check.
