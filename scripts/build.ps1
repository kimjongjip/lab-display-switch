param([switch]$Test,[switch]$WindowIntegration)
$ErrorActionPreference='Stop'
$projectRoot=Split-Path $PSScriptRoot -Parent
$compiler=Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$outDir=Join-Path $projectRoot 'dist'
New-Item -ItemType Directory -Path $outDir -Force | Out-Null
New-Item -ItemType Directory -Path (Join-Path $projectRoot 'artifacts') -Force | Out-Null
$iconFile=Join-Path $outDir 'app.ico'
if(-not (Test-Path -LiteralPath $iconFile)){
 Add-Type -AssemblyName System.Drawing
 $bitmap=New-Object Drawing.Bitmap 32,32
 $graphics=[Drawing.Graphics]::FromImage($bitmap)
 $graphics.Clear([Drawing.Color]::Transparent)
 $blue=New-Object Drawing.SolidBrush ([Drawing.Color]::FromArgb(42,111,232))
 $green=New-Object Drawing.SolidBrush ([Drawing.Color]::FromArgb(106,223,190))
 $graphics.FillRectangle($blue,1,5,18,15)
 $graphics.FillRectangle($green,14,10,17,14)
 $graphics.FillRectangle($blue,7,21,8,3)
 $graphics.FillRectangle($green,19,25,8,3)
 $icon=[Drawing.Icon]::FromHandle($bitmap.GetHicon())
 $stream=[IO.File]::Create($iconFile)
 $icon.Save($stream)
 $stream.Dispose();$icon.Dispose();$graphics.Dispose();$bitmap.Dispose();$blue.Dispose();$green.Dispose()
}
$sources=@(Get-ChildItem -LiteralPath (Join-Path $projectRoot 'src') -Filter '*.cs' | ForEach-Object { $_.FullName })
& $compiler /nologo /target:winexe /platform:x64 /optimize+ /codepage:65001 /warn:4 /warnaserror+ /r:System.Windows.Forms.dll /r:System.Drawing.dll /r:System.Web.Extensions.dll /r:System.Core.dll /r:System.Management.dll ('/win32manifest:'+(Join-Path $projectRoot 'app.manifest')) ('/win32icon:'+$iconFile) ('/out:'+(Join-Path $outDir 'LabDisplaySwitch.exe')) $sources
if($LASTEXITCODE -ne 0){throw 'Application build failed'}
Copy-Item -LiteralPath (Join-Path $projectRoot 'vendor\vdd') -Destination $outDir -Recurse -Force
if($Test -or $WindowIntegration){
 $testSources=@(Get-ChildItem -LiteralPath (Join-Path $projectRoot 'tests') -Filter '*.cs' | ForEach-Object { $_.FullName })
 & $compiler /nologo /target:exe /platform:x64 /codepage:65001 /warnaserror+ /r:System.Windows.Forms.dll /r:System.Drawing.dll /r:System.Web.Extensions.dll /r:System.Core.dll /r:System.Management.dll /main:LabDisplaySwitch.Tests ('/out:'+(Join-Path $outDir 'LabDisplaySwitch.Tests.exe')) $sources $testSources
 if($LASTEXITCODE -ne 0){throw 'Test build failed'}
 & (Join-Path $outDir 'LabDisplaySwitch.Tests.exe')
 if($LASTEXITCODE -ne 0){throw 'Tests failed'}
 if($WindowIntegration){
  & $compiler /nologo /target:winexe /platform:x64 /codepage:65001 /warnaserror+ /r:System.Windows.Forms.dll /r:System.Drawing.dll /r:System.Web.Extensions.dll /r:System.Core.dll /r:System.Management.dll /main:LabDisplaySwitch.WindowFixture ('/win32manifest:'+(Join-Path $projectRoot 'app.manifest')) ('/out:'+(Join-Path $outDir 'LabDisplaySwitch.TestWindows.exe')) $sources $testSources
  if($LASTEXITCODE -ne 0){throw 'Window fixture build failed'}
  & (Join-Path $outDir 'LabDisplaySwitch.Tests.exe') --window-integration
  if($LASTEXITCODE -ne 0){throw 'Real-window integration test failed'}
 }
}
Write-Output ('Built: '+(Join-Path $outDir 'LabDisplaySwitch.exe'))
