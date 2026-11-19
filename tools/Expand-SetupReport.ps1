[CmdletBinding()]
param([string]$InventoryPath=(Join-Path $PSScriptRoot '../reports/rmc4-192.168.1.169/Control4Gateway_1_inventory.json'))
$ErrorActionPreference='Stop'
$inventory=Get-Content -LiteralPath $InventoryPath -Raw | ConvertFrom-Json
$report=$inventory.setup_report
if(!$report -or !$report.readable_text){throw 'No setup report in this inventory. Pulse Export_Setup_Report on the gateway.'}
$id=0
if(![int]::TryParse([string]$report.gateway_id,[ref]$id) -or $id -lt 1 -or $id -gt 65535){throw 'Invalid gateway ID'}
$directory=Split-Path ([IO.Path]::GetFullPath($InventoryPath))
$base=Join-Path $directory ('Control4Gateway_'+$id+'_setup')
$report | ConvertTo-Json -Depth 30 | Set-Content -LiteralPath ($base+'.json') -Encoding utf8
[IO.File]::WriteAllText(($base+'.txt'),[string]$report.readable_text,[Text.UTF8Encoding]::new($false))
Write-Output ($base+'.txt')
Write-Output ($base+'.json')
