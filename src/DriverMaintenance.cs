using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management;
using System.Text.RegularExpressions;
using System.Threading;

namespace LabDisplaySwitch {
    internal static class DriverMaintenance {
        public static bool CanRestart(Profile saved,Profile active,Journal journal) {
            return saved!=null&&active!=null&&saved.Displays.Count==2&&active.Displays.Count==2
                &&saved.Displays.All(d=>!d.Virtual)&&active.Displays.All(d=>!d.Virtual)
                &&(journal==null||(journal.Phase=="lab"&&!journal.Confirmed))&&Displays.Matches(saved,active,true);
        }
        private static string DeviceInstance() {
            var found=new List<string>();
            using(var query=new ManagementObjectSearcher("SELECT PNPDeviceID, HardwareID FROM Win32_PnPEntity WHERE PNPClass = 'Display'"))
            using(var devices=query.Get())foreach(ManagementObject item in devices)using(item) {
                var ids=item["HardwareID"] as string[];
                if(ids!=null&&ids.Any(id=>String.Equals(id,"Root\\MttVDD",StringComparison.OrdinalIgnoreCase)))found.Add(item["PNPDeviceID"] as string);
            }
            if(found.Count!=1||found[0]==null||!Regex.IsMatch(found[0],@"\AROOT\\DISPLAY\\[0-9A-Fa-f]{4}\z",RegexOptions.IgnoreCase))throw new InvalidOperationException("이 앱의 가상 모니터 장치 하나를 식별하지 못했습니다.");
            return found[0];
        }
        public static object ApplyCursorFixInLab() {
            using(var mutex=new Mutex(false,Controller.MutexName)) {
                try{if(!mutex.WaitOne(0))throw new InvalidOperationException("다른 화면 전환이 진행 중입니다.");}catch(AbandonedMutexException){}
                try {
                    var before=Displays.Capture();var saved=Store.Read<Profile>("lab-profile.json");
                    if(!CanRestart(saved,before,Store.Read<Journal>("transaction.json")))throw new InvalidOperationException("커서 수정 적용은 저장된 실제 모니터 두 개가 켜진 연구실 모드에서만 가능합니다. 원격 숨김 중에는 드라이버를 재시작하지 않습니다.");
                    if(!Installer.IsAdmin)throw new InvalidOperationException("커서 설정과 가상 모니터 재시작에는 Windows 관리자 확인이 필요합니다.");
                    string device=DeviceInstance();var windows=Windows.Capture(before);
                    var settings=Store.Read<Settings>("settings.json");
                    if(settings==null||settings.VirtualKeys.Count!=2)throw new InvalidOperationException("저장된 가상 모니터 두 개를 확인하지 못했습니다.");
                    var keys=settings.VirtualKeys.ToArray();
                    var config=Installer.FixCursorConfiguration();
                    Store.Write("cursor-maintenance-backup.json",new { Time=DateTime.UtcNow.ToString("o"),Device=device,Before=before,Windows=windows,Configuration=config });
                    // UMDF teardown can take about a minute even when the device
                    // restarts successfully. Keep the recovery deadline beyond
                    // the command timeout and subsequent display verification.
                    Controller.EnsureGuard();var journal=Controller.Arm(before,180,"installing");
                    try {
                        int exitCode;
                        var start=new ProcessStartInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System),"pnputil.exe"),"/restart-device \""+device+"\"") { UseShellExecute=false,CreateNoWindow=true,WindowStyle=ProcessWindowStyle.Hidden,RedirectStandardOutput=true,RedirectStandardError=true };
                        using(var process=Process.Start(start)) {
                            var stdout=process.StandardOutput.ReadToEndAsync();var stderr=process.StandardError.ReadToEndAsync();
                            if(!process.WaitForExit(120000))throw new TimeoutException("가상 모니터 재시작 결과가 지연되고 있습니다. 재시작 명령이 아직 실행 중일 수 있습니다.");
                            exitCode=process.ExitCode;Store.Write("cursor-driver-restart.json",new { Device=device,ExitCode=exitCode,Output=stdout.Result,Error=stderr.Result });
                        }
                        if(exitCode!=0&&exitCode!=3010)throw new InvalidOperationException("가상 모니터 재시작 실패: "+exitCode);
                        bool found=false;
                        for(int i=0;i<30;i++) {
                            var inventory=Displays.Inventory(true);
                            if(keys.All(k=>inventory.Any(d=>d.Virtual&&String.Equals(d.Key,k,StringComparison.OrdinalIgnoreCase)))){found=true;break;}
                            Thread.Sleep(500);
                        }
                        if(!found)throw new InvalidOperationException("재시작 후 기존 가상 모니터 두 개를 확인하지 못했습니다.");
                        if(!Displays.Matches(before,Displays.Capture(),true))Displays.Apply(before,true,true);
                        int restored=Windows.Restore(windows,before,before);
                        journal.Phase="lab";Store.Write("transaction.json",journal);
                        return new { Success=true,Configuration=config,Device=device,RestartCommandSucceeded=exitCode==0,RebootRequired=exitCode==3010,RestoredWindows=restored,CursorBehaviorVerified=false };
                    }catch(Exception error) {
                        journal.LastError=error.Message;
                        try {
                            if(!Displays.Matches(before,Displays.Capture(),true))Displays.Apply(before,true,true);
                            Windows.Restore(windows,before,before);journal.Phase="lab";
                        }catch(Exception recovery){journal.Phase="failed";journal.LastError+=" / 화면 복구: "+recovery.Message;}
                        Store.Write("transaction.json",journal);throw;
                    }
                }finally{mutex.ReleaseMutex();}
            }
        }
    }
}
