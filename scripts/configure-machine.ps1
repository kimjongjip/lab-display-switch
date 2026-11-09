param(
    [switch]$ListGpus,
    [string]$GpuName,
    [ValidateSet('landscape','match-lab')][string]$RemoteLayout='landscape',
    [switch]$NativeResolution
)
$ErrorActionPreference='Stop'
$projectRoot=Split-Path $PSScriptRoot -Parent
$gpus=@(Get-CimInstance Win32_VideoController | Where-Object { $_.PNPDeviceID -like 'PCI\*' } | Select-Object Name,DriverVersion)
if($ListGpus){$gpus;return}
if([string]::IsNullOrWhiteSpace($GpuName) -or -not ($gpus.Name -contains $GpuName)){throw 'Use -ListGpus, then supply one exact physical GPU name with -GpuName.'}
$app=Join-Path $projectRoot 'dist\LabDisplaySwitch.exe'
if(-not (Test-Path -LiteralPath $app)){throw 'Run scripts/build.ps1 -Test first.'}
$root=Join-Path $env:LOCALAPPDATA 'LabDisplaySwitch'
$stateFile=Join-Path $root 'transaction.json'
if(Test-Path -LiteralPath $stateFile){$state=Get-Content -LiteralPath $stateFile -Raw -Encoding UTF8|ConvertFrom-Json;if($state.Phase -ne 'lab'){throw 'Configure only in lab mode.'}}
if(Get-Process LabDisplaySwitch -ErrorAction SilentlyContinue){throw 'Close the tray app and stop its guard in lab mode before configuring.'}
$assembly=[Reflection.Assembly]::LoadFile($app)
$store=$assembly.GetType('LabDisplaySwitch.Store')
$type=$assembly.GetType('LabDisplaySwitch.Settings')
$settings=$store.GetMethod('Read').MakeGenericMethod(@($type)).Invoke($null,@('settings.json'))
if($null -eq $settings){$settings=[Activator]::CreateInstance($type)}
$settingsPath=Join-Path $root 'settings.json'
if(Test-Path -LiteralPath $settingsPath){Copy-Item -LiteralPath $settingsPath -Destination ($settingsPath+'.before-configure-'+[DateTime]::UtcNow.ToString('yyyyMMddHHmmssfff')+'.bak')}
$settings.RenderingGpu=$GpuName
$settings.RemoteLandscape=($RemoteLayout -eq 'landscape')
$settings.Compact=(-not $NativeResolution)
$settings.Tested=$false
$store.GetMethod('Write').Invoke($null,@('settings.json',$settings))
Write-Output ('Saved local configuration: '+$settingsPath)
Write-Output 'No driver or monitor changes were made. RenderingGpu applies to NEW driver installs only.'
