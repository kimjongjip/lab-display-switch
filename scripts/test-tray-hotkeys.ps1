param([Parameter(Mandatory=$true)][string]$AppPath,[switch]$Rescue,[string]$Marker)
$ErrorActionPreference='Stop'
$projectRoot=Split-Path $PSScriptRoot -Parent
$app=(Resolve-Path -LiteralPath $AppPath).Path
if([IO.Path]::GetFileName($app) -ne 'LabDisplaySwitch.exe'){throw 'Expected the installed LabDisplaySwitch.exe path.'}
$stateRoot=Join-Path $env:LOCALAPPDATA 'LabDisplaySwitch'
if($Rescue){
    Start-Sleep -Seconds 45
    if(-not (Test-Path -LiteralPath $Marker)){
        $output=Join-Path $projectRoot 'artifacts\tray-hotkeys-rescue.json'
        Start-Process -FilePath $app -ArgumentList ('--restore --output "'+$output+'"') -WindowStyle Hidden -Wait
    }
    exit
}
Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;
public static class HotkeyTrial {
 [StructLayout(LayoutKind.Sequential)] public struct MouseInput { public int x,y;public uint data,flags,time;public UIntPtr extra; }
 [StructLayout(LayoutKind.Sequential)] public struct KeyInput { public ushort key,scan;public uint flags,time;public UIntPtr extra; }
 [StructLayout(LayoutKind.Explicit)] public struct InputUnion { [FieldOffset(0)] public MouseInput mouse;[FieldOffset(0)] public KeyInput keyboard; }
 [StructLayout(LayoutKind.Sequential)] public struct Input { public uint type;public InputUnion value; }
 [DllImport("user32.dll",SetLastError=true)] static extern uint SendInput(uint count,Input[] inputs,int size);
 [DllImport("user32.dll")] static extern short GetAsyncKeyState(int key);
 [DllImport("user32.dll",CharSet=CharSet.Unicode)] public static extern IntPtr FindWindow(string cls,string title);
 public static IntPtr FindApp() { return FindWindow(null,"Lab Display Switch"); }
 [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr hwnd);
 [DllImport("user32.dll")] public static extern IntPtr SendMessage(IntPtr hwnd,uint msg,IntPtr w,IntPtr l);
 [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr hwnd,out uint pid);
 public static void Press(ushort key) {
  foreach(int modifier in new[]{0x10,0x11,0x12,0x5b,0x5c})if(GetAsyncKeyState(modifier)<0)throw new Exception("A modifier key is already held; test stopped.");
  ushort[] keys={0x11,0x12,key,key,0x12,0x11};uint[] flags={0,0,1,3,2,2};
  var inputs=new Input[keys.Length];
  for(int i=0;i<inputs.Length;i++){inputs[i].type=1;inputs[i].value.keyboard.key=keys[i];inputs[i].value.keyboard.flags=flags[i];}
  if(SendInput((uint)inputs.Length,inputs,Marshal.SizeOf(typeof(Input)))!=inputs.Length)throw new Exception("Keyboard injection failed: "+Marshal.GetLastWin32Error());
 }
}
'@
$keys=Get-Content (Join-Path $stateRoot 'tray-hotkeys.json') -Raw -Encoding UTF8 | ConvertFrom-Json
$owner=Get-Process -Id $keys.Pid
if($owner.Path -ne $app -or $owner.StartTime.ToUniversalTime().Ticks -ne $keys.ProcessStart -or -not $keys.RestoreRegistered -or -not $keys.HideRegistered){throw 'Tray shortcut ownership check failed.'}
$hwnd=[HotkeyTrial]::FindApp()
[uint32]$windowOwner=0
$null=[HotkeyTrial]::GetWindowThreadProcessId($hwnd,[ref]$windowOwner)
if($hwnd -eq [IntPtr]::Zero -or $windowOwner -ne $keys.Pid){throw 'Tray app window not found.'}
$null=[HotkeyTrial]::SendMessage($hwnd,0x112,[IntPtr]0xf060,[IntPtr]::Zero)
if([HotkeyTrial]::IsWindowVisible($hwnd)){throw 'Closing the app did not hide it to the tray.'}
if((Get-Process -Id $keys.Pid).HasExited){throw 'The app exited instead of remaining in the tray.'}
$assembly=[Reflection.Assembly]::LoadFile($app)
$displayType=$assembly.GetType('LabDisplaySwitch.Displays')
$windowType=$assembly.GetType('LabDisplaySwitch.Windows')
$native=$assembly.GetType('LabDisplaySwitch.Native')
$previousDpi=$native.GetMethod('SetThreadDpiAwarenessContext').Invoke($null,@([IntPtr](-4)))
function Read-State { Get-Content (Join-Path $stateRoot 'transaction.json') -Raw -Encoding UTF8 | ConvertFrom-Json }
function Wait-Phase([string]$phase,[string]$oldId) {
    $watch=[Diagnostics.Stopwatch]::StartNew()
    do {
        $state=Read-State
        if($state.Phase -eq $phase -and ($phase -ne 'remote' -or ($state.Confirmed -and $state.Id -ne $oldId))){return $watch.ElapsedMilliseconds}
        Start-Sleep -Milliseconds 100
    }while($watch.ElapsedMilliseconds -lt 15000)
    throw ('Timed out waiting for '+$phase+'; current phase='+$state.Phase)
}
$rescueStarted=$false
try {
    $before=$displayType.GetMethod('Capture').Invoke($null,@())
    if($before.Displays.Count -ne 2 -or @($before.Displays | Where-Object Virtual).Count -ne 0 -or (Read-State).Phase -ne 'lab'){throw 'This live test must start in lab mode.'}
    $windows=$windowType.GetMethod('Capture').Invoke($null,@($before))
    $oldId=(Read-State).Id
    $Marker=Join-Path $projectRoot ('artifacts\hotkey-test-'+[Guid]::NewGuid().ToString('N')+'.done')
    $arguments='-NoProfile -ExecutionPolicy Bypass -File "'+$PSCommandPath+'" -AppPath "'+$app+'" -Rescue -Marker "'+$Marker+'"'
    $rescueProcess=Start-Process -FilePath (Join-Path $PSHOME 'powershell.exe') -ArgumentList $arguments -WindowStyle Hidden -PassThru
    $rescueStarted=$true
    [HotkeyTrial]::Press(0x23)
    $hideMs=Wait-Phase 'remote' $oldId
    $remote=$displayType.GetMethod('Capture').Invoke($null,@())
    $remoteChecks=$windowType.GetMethod('Inspect').Invoke($null,@($windows,$before,$remote))
    if($remote.Displays.Count -ne 2 -or @($remote.Displays | Where-Object { -not $_.Virtual }).Count -ne 0){throw 'Physical outputs were not fully hidden.'}
    [HotkeyTrial]::Press(0x24)
    $restoreMs=Wait-Phase 'lab' ''
    $after=$displayType.GetMethod('Capture').Invoke($null,@())
    $checks=$windowType.GetMethod('Inspect').Invoke($null,@($windows,$before,$after))
    $same=$displayType.GetMethod('Matches').Invoke($null,@($before,$after,$true))
    if(-not $same -or @($after.Displays | Where-Object Virtual).Count -ne 0){throw 'Lab topology was not restored.'}
    $report=[ordered]@{
        TimeUtc=[DateTime]::UtcNow.ToString('o');AppPath=$app;Success=$true
        AppHiddenAfterClose=(-not [HotkeyTrial]::IsWindowVisible($hwnd));TrayPid=$keys.Pid
        ActualSendInput=$true;HideMilliseconds=$hideMs;RestoreMilliseconds=$restoreMs
        RemotePhysicalOutputs=@($remote.Displays | Where-Object { -not $_.Virtual }).Count
        RemoteVirtualOutputs=$remote.Displays.Count;FinalPhase=(Read-State).Phase
        LabTopologyRestored=$same;Before=$before;Remote=$remote;After=$after
        RemoteWindows=$remoteChecks;RestoredWindows=$checks
    }
    $report | ConvertTo-Json -Depth 12 | Set-Content (Join-Path $projectRoot 'artifacts\tray-hotkeys-roundtrip.json') -Encoding UTF8
    [PSCustomObject]@{Success=$true;HideMs=$hideMs;RestoreMs=$restoreMs;Windows=$checks.Count;RemoteMatching=@($remoteChecks | Where-Object Matches).Count;LabMatching=@($checks | Where-Object Matches).Count;InTray=$report.AppHiddenAfterClose;FinalPhase=$report.FinalPhase} | ConvertTo-Json
} finally {
    if($rescueStarted){
        try {
            $live=$displayType.GetMethod('Capture').Invoke($null,@())
            if((Read-State).Phase -ne 'lab' -or @($live.Displays | Where-Object Virtual).Count -gt 0){
                $result=Join-Path $projectRoot 'artifacts\tray-hotkeys-finally-restore.json'
                $restoreProcess=Start-Process -FilePath $app -ArgumentList ('--restore --output "'+$result+'"') -WindowStyle Hidden -PassThru
                if(-not $restoreProcess.WaitForExit(25000) -or $restoreProcess.ExitCode -ne 0){throw 'Fallback restore did not finish; independent rescue remains armed.'}
            }
            [IO.File]::WriteAllText($Marker,'Lab mode restored.')
        }catch{Write-Warning $_}
    }
    $null=$native.GetMethod('SetThreadDpiAwarenessContext').Invoke($null,@($previousDpi))
}
