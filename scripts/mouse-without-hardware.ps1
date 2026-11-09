param(
    [ValidateSet('Inspect','Enable','Restore')][string]$Mode='Inspect',
    [string]$BackupPath
)
$ErrorActionPreference='Stop'
Add-Type -TypeDefinition @'
using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
public static class MouseKeysSetting {
 [StructLayout(LayoutKind.Sequential)] public struct MouseKeys {
  public uint cbSize,dwFlags,iMaxSpeed,iTimeToMaxSpeed,iCtrlSpeed,dwReserved1,dwReserved2;
 }
 [DllImport("user32.dll",EntryPoint="SystemParametersInfoW",SetLastError=true)]
 static extern bool SystemParametersInfo(uint action,uint size,ref MouseKeys value,uint flags);
 [DllImport("user32.dll")] static extern short GetKeyState(int key);
 public static bool NumLockOn() { return (GetKeyState(0x90)&1)!=0; }
 public static MouseKeys Read() {
  var value=new MouseKeys();value.cbSize=(uint)Marshal.SizeOf(typeof(MouseKeys));
  if(!SystemParametersInfo(0x36,value.cbSize,ref value,0))throw new Win32Exception(Marshal.GetLastWin32Error());
  return value;
 }
 public static void Write(MouseKeys value) {
  value.cbSize=(uint)Marshal.SizeOf(typeof(MouseKeys));value.dwReserved1=value.dwReserved2=0;
  if(!SystemParametersInfo(0x37,value.cbSize,ref value,3))throw new Win32Exception(Marshal.GetLastWin32Error());
 }
}
'@
$before=[MouseKeysSetting]::Read()
if($Mode -eq 'Enable'){
    $stateRoot=Join-Path $env:LOCALAPPDATA 'LabDisplaySwitch'
    $BackupPath=Join-Path $stateRoot ('mouse-keys-before-'+[DateTime]::UtcNow.ToString('yyyyMMddTHHmmssfff')+'.json')
    [PSCustomObject]@{TimeUtc=[DateTime]::UtcNow.ToString('o');MouseKeys=$before} | ConvertTo-Json | Set-Content -LiteralPath $BackupPath -Encoding UTF8
    $desired=$before
    # Enable MouseKeys, but keep Num Lock ON available for ordinary number entry.
    $desired.dwFlags=($desired.dwFlags -bor 1) -band [uint32]4294967167
    try {
        [MouseKeysSetting]::Write($desired)
        $check=[MouseKeysSetting]::Read()
        if(($check.dwFlags -band 1) -eq 0 -or ($check.dwFlags -band 128) -ne 0){throw 'MouseKeys setting verification failed.'}
    }catch{[MouseKeysSetting]::Write($before);throw}
}elseif($Mode -eq 'Restore'){
    if(-not $BackupPath){throw 'Restore requires the exact BackupPath returned by Enable.'}
    $saved=(Get-Content -LiteralPath $BackupPath -Raw -Encoding UTF8 | ConvertFrom-Json).MouseKeys
    if($null -eq $saved -or $saved.cbSize -ne 28){throw 'Invalid MouseKeys backup.'}
    $desired=New-Object MouseKeysSetting+MouseKeys
    foreach($field in 'cbSize','dwFlags','iMaxSpeed','iTimeToMaxSpeed','iCtrlSpeed','dwReserved1','dwReserved2'){$desired.$field=[uint32]$saved.$field}
    [MouseKeysSetting]::Write($desired)
    if((([MouseKeysSetting]::Read()).dwFlags -band 255) -ne ($desired.dwFlags -band 255)){throw 'MouseKeys restore verification failed.'}
}
$after=[MouseKeysSetting]::Read()
[PSCustomObject]@{
    Mode=$Mode;MouseKeysEnabled=(($after.dwFlags -band 1) -ne 0)
    NumbersWhenNumLockOn=(($after.dwFlags -band 128) -eq 0)
    CurrentNumLockOn=[MouseKeysSetting]::NumLockOn()
    Before=$before;After=$after;BackupPath=$BackupPath
} | ConvertTo-Json -Depth 4
