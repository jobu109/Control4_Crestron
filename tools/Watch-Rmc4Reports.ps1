[CmdletBinding()]
param(
    [string]$ProcessorAddress = '192.168.1.169',
    [string]$Username = 'tsiadmin',
    [ValidateRange(1,65535)][int]$GatewayId = 1,
    [ValidateRange(2,300)][int]$IntervalSeconds = 5,
    [switch]$Once,
    [switch]$CheckOnly
)
$ErrorActionPreference = 'Stop'
$taskAddress = $null
if (![Net.IPAddress]::TryParse($ProcessorAddress, [ref]$taskAddress)) { throw 'Use the RMC4 IP address.' }
$projectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$safeAddress = $taskAddress.ToString().Replace(':','_')
$reportDirectory = Join-Path $projectRoot ('reports/rmc4-' + $safeAddress)
$stateDirectory = Join-Path $projectRoot '.local'
$pinFile = Join-Path $stateDirectory ('rmc4-' + $safeAddress + '.sha256')

# Reuse the PC's installed Crestron tool dependency; do not add it to the RMC4 project.
$sshLibrary = 'C:/Program Files (x86)/Crestron/Installation Gathering Tool/Renci.SshNet.dll'
if (!(Test-Path -LiteralPath $sshLibrary)) { throw 'The installed Crestron SFTP library was not found.' }
[void][Reflection.Assembly]::LoadFrom($sshLibrary)
$references = @($sshLibrary)
if ($PSVersionTable.PSEdition -eq 'Core') {
    $references += Join-Path $PSHOME 'ref/mscorlib.dll'
    $references += Join-Path $PSHOME 'ref/System.Security.Cryptography.dll'
}
$asyncReference = Join-Path (Split-Path $sshLibrary) 'Microsoft.Bcl.AsyncInterfaces.dll'
if (Test-Path -LiteralPath $asyncReference) { $references += $asyncReference }
Add-Type -Path (Join-Path $PSScriptRoot 'Rmc4ReportSftp.cs') -ReferencedAssemblies $references
if ($CheckOnly) {
    $probeConnection = [Control4GatewayTools.ReportSftpConnection]::new($ProcessorAddress,$Username,'check-only-unused',$pinFile)
    $probeConnection.Dispose()
    Write-Output "Collector ready for $Username at $ProcessorAddress."
    Write-Output "Reports: $reportDirectory"
    Write-Output 'SFTP library and collector loaded. No authentication attempted.'
    return
}

New-Item -ItemType Directory -Path $reportDirectory,$stateDirectory -Force | Out-Null
Write-Host "RMC4 report collector: $Username at $ProcessorAddress"
Write-Host 'Read-only on the processor. Enter the password here once; it is kept in memory only.'
$securePassword = Read-Host 'RMC4 password' -AsSecureString
$taskCredential = [Management.Automation.PSCredential]::new($Username,$securePassword)
$connection = [Control4GatewayTools.ReportSftpConnection]::new($ProcessorAddress,$Username,$taskCredential.GetNetworkCredential().Password,$pinFile)
$stamps = @{}
$messages = @{}
$statusFile = Join-Path $reportDirectory 'collector_status.json'
$files = @('inventory','media_commands','media_capabilities') | ForEach-Object { 'Control4Gateway_' + $GatewayId + '_' + $_ + '.json' }
try {
    $connection.Connect()
    Write-Host ('Connected. SSH host key: ' + $connection.Fingerprint)
    Write-Host ('Watching reports in ' + $reportDirectory + '. Close this window to stop.')
    do {
        $cycle = [ordered]@{processor=$ProcessorAddress; checked_utc=[DateTime]::UtcNow.ToString('o'); connected=$connection.IsConnected; reports=@()}
        foreach ($file in $files) {
            $remote = '/user/' + $file
            $destination = Join-Path $reportDirectory $file
            $temporary = Join-Path $reportDirectory ('.' + $file + '.download')
            try {
                if (!$connection.IsConnected) { $connection.Connect() }
                $before = $connection.Stamp($remote)
                if ($null -eq $before) {
                    $cycle.reports += [ordered]@{file=$file;status='not_present'}
                    continue
                }
                if (!$stamps.ContainsKey($file) -or $stamps[$file] -ne $before -or !(Test-Path -LiteralPath $destination)) {
                    $connection.Download($remote,$temporary)
                    # Do not replace a good local report with an in-progress or malformed export.
                    [void](ConvertFrom-Json -InputObject ([IO.File]::ReadAllText($temporary)))
                    $after = $connection.Stamp($remote)
                    if ($before -ne $after) { throw 'Report changed during download; retrying next cycle.' }
                    Move-Item -LiteralPath $temporary -Destination $destination -Force
                    if ($file -eq ('Control4Gateway_' + $GatewayId + '_inventory.json')) {
                        $downloaded = Get-Content -LiteralPath $destination -Raw | ConvertFrom-Json
                        if ($downloaded.setup_report) {
                            & (Join-Path $PSScriptRoot 'Expand-SetupReport.ps1') -InventoryPath $destination | ForEach-Object { Write-Host $_ }
                        }
                    }
                    $stamps[$file] = $after
                    Write-Host ('Downloaded ' + $file + ' at ' + (Get-Date -Format 'HH:mm:ss'))
                }
                $cycle.reports += [ordered]@{file=$file;status='collected';local_path=$destination}
                $messages.Remove($file)
            } catch {
                $kind = $_.Exception.GetBaseException().GetType().Name
                $cycle.reports += [ordered]@{file=$file;status='retry_pending';error_type=$kind}
                if ($messages[$file] -ne $kind) { Write-Host ("Waiting to collect ${file}: $kind"); $messages[$file]=$kind }
                # Do not repeatedly retry rejected credentials or a changed SSH identity.
                if ($kind -eq 'SshAuthenticationException' -or $connection.HostKeyChanged) { throw }
            }
        }
        $cycle.connected = $connection.IsConnected
        $cycle | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $statusFile -Encoding utf8
        if (!$Once) { Start-Sleep -Seconds $IntervalSeconds }
    } while (!$Once)
} finally {
    $connection.Dispose()
    $taskCredential = $null
    $securePassword.Dispose()
}
