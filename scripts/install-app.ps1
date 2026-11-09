param([switch]$StartOnLogin,[switch]$Launch)
$ErrorActionPreference='Stop'
$projectRoot=Split-Path $PSScriptRoot -Parent
$manifest=[xml](Get-Content -LiteralPath (Join-Path $projectRoot 'app.manifest') -Raw)
$version=([version]$manifest.assembly.assemblyIdentity.version).ToString(3)
$appDir=Join-Path $env:LOCALAPPDATA ('LabDisplaySwitch\app\'+$version)
$sourceExe=Join-Path $projectRoot 'dist\LabDisplaySwitch.exe'
if(-not (Test-Path -LiteralPath $sourceExe)){throw 'Run scripts/build.ps1 first'}
$running=Get-Process -Name LabDisplaySwitch -ErrorAction SilentlyContinue | Where-Object { $_.Path -eq (Join-Path $appDir 'LabDisplaySwitch.exe') }
if($running){throw 'Close the tray app and stop its recovery process in lab mode before updating the installed executable.'}
New-Item -ItemType Directory -Path $appDir -Force | Out-Null
Copy-Item -LiteralPath $sourceExe -Destination (Join-Path $appDir 'LabDisplaySwitch.exe') -Force
Copy-Item -LiteralPath (Join-Path $projectRoot 'vendor\vdd') -Destination $appDir -Recurse -Force
Copy-Item -LiteralPath (Join-Path $projectRoot 'README.md') -Destination (Join-Path $appDir 'README.md') -Force
$shell=New-Object -ComObject WScript.Shell
foreach($folder in @([Environment]::GetFolderPath('Desktop'),[Environment]::GetFolderPath('Programs'))){
 $shortcut=$shell.CreateShortcut((Join-Path $folder 'Lab Display Switch.lnk'))
 $shortcut.TargetPath=Join-Path $appDir 'LabDisplaySwitch.exe'
 $shortcut.WorkingDirectory=$appDir
 $shortcut.Description='Switch between lab monitors and private virtual displays'
 $shortcut.IconLocation=(Join-Path $appDir 'LabDisplaySwitch.exe')+',0'
 $shortcut.Save()
}
if($StartOnLogin){New-ItemProperty -Path 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run' -Name 'LabDisplaySwitch' -Value ('"'+(Join-Path $appDir 'LabDisplaySwitch.exe')+'" --tray') -PropertyType String -Force | Out-Null}
if($Launch){Start-Process -FilePath (Join-Path $appDir 'LabDisplaySwitch.exe') -ArgumentList '--tray' -WindowStyle Hidden}
Write-Output ('Installed: '+(Join-Path $appDir 'LabDisplaySwitch.exe'))
