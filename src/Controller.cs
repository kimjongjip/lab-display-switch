using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;

namespace LabDisplaySwitch {
    internal static class Controller {
        internal static string MutexName { get { return "Local\\LabDisplaySwitch.Operation."+Process.GetCurrentProcess().SessionId; } }
        public static bool NeedsRollback(Journal j,DateTime now,bool ownerAlive) {
            if(j==null||j.Confirmed||!(j.Phase=="preparing"||j.Phase=="awaiting-confirmation"||j.Phase=="restoring"||j.Phase=="installing"))return false;
            DateTime deadline;if(!DateTime.TryParse(j.DeadlineUtc,null,System.Globalization.DateTimeStyles.RoundtripKind,out deadline))return true;
            return now>=deadline||!ownerAlive;
        }
        public static void EnsureGuard() {
            using(var ready=new EventWaitHandle(false,EventResetMode.ManualReset,"Local\\LabDisplaySwitch.GuardReady."+Process.GetCurrentProcess().SessionId)) {
                if(ready.WaitOne(0))return;
                Process.Start(new ProcessStartInfo(Store.Exe,"--guard") { UseShellExecute=false,CreateNoWindow=true,WindowStyle=ProcessWindowStyle.Hidden });
                if(!ready.WaitOne(7000))throw new InvalidOperationException("독립 복구 프로그램을 시작하지 못했습니다. 화면은 변경하지 않았습니다.");
            }
        }
        public static Journal Arm(Profile after,int seconds,string phase) {
            Profile before=Displays.Capture();var process=Process.GetCurrentProcess();
            var j=new Journal { Id=Guid.NewGuid().ToString("N"),Phase=phase,StartedUtc=DateTime.UtcNow.ToString("o"),DeadlineUtc=DateTime.UtcNow.AddSeconds(seconds).ToString("o"),OwnerPid=process.Id,OwnerStart=process.StartTime.ToUniversalTime().Ticks,Before=before,After=after,Windows=Windows.Capture(before) };
            Store.Write("transaction.json",j);return j;
        }
        public static Journal EnterRemote(int seconds,bool? compact=null) {
            using(var mutex=new Mutex(false,MutexName)) {
                if(!mutex.WaitOne(0))throw new InvalidOperationException("다른 화면 전환이 진행 중입니다.");
                try {
                    var current=Displays.Capture();if(current.Displays.Any(d=>d.Virtual))throw new InvalidOperationException("연구실 모드로 복귀한 후 전환하세요.");
                    bool useCompact=compact??(Store.Read<Settings>("settings.json")??new Settings()).Compact;
                    Profile after=Displays.RemoteProfile(useCompact);Displays.Check(Displays.Validate(after),"Validate remote profile");
                    EnsureGuard();Journal j=Arm(after,Math.Max(15,seconds),"preparing");
                    j.Compact=useCompact;Store.Write("transaction.json",j);
                    try {
                        Displays.Apply(after,false);Dpi.MatchVirtual(after);Thread.Sleep(500);
                        Windows.Restore(j.Windows,j.Before,after);
                        j.Phase="awaiting-confirmation";Store.Write("transaction.json",j);
                        Store.Log("Remote trial applied, rollback armed: "+j.Id);return j;
                    }catch(Exception error) {
                        j.LastError=error.Message;Store.Write("transaction.json",j);
                        RestoreJournal(j);throw;
                    }
                }finally{mutex.ReleaseMutex();}
            }
        }
        public static void Confirm(string id) {
            using(var mutex=new Mutex(false,MutexName)) {
                if(!mutex.WaitOne(5000))throw new InvalidOperationException("화면 전환이 진행 중입니다.");
                try {
                    var j=Store.Read<Journal>("transaction.json");if(j==null||j.Id!=id||j.Phase!="awaiting-confirmation")throw new InvalidOperationException("확인 시간이 만료되었습니다.");
                    if(DateTime.UtcNow>=DateTime.Parse(j.DeadlineUtc,null,System.Globalization.DateTimeStyles.RoundtripKind))throw new InvalidOperationException("확인 시간이 만료되었습니다.");
                    if(!Displays.Matches(j.After,Displays.Capture(),true))throw new InvalidOperationException("가상 화면 구성이 달라져 확정하지 않았습니다.");
                    if(Displays.Capture().Displays.Any(d=>j.After.Displays.Any(a=>a.Key==d.Key&&a.Scale!=d.Scale)))throw new InvalidOperationException("배율이 연구실 구성과 다릅니다. 가상 화면 배율을 확인하세요.");
                    j.Confirmed=true;j.Phase="remote";Store.Write("transaction.json",j);
                    var settings=Store.Read<Settings>("settings.json")??new Settings();settings.Tested=true;settings.LastMode="remote";settings.Compact=j.Compact;Store.Write("settings.json",settings);
                    Store.Log("Remote mode confirmed; disconnects do not restore physical outputs.");
                }finally{mutex.ReleaseMutex();}
            }
        }
        public static bool CanHideFromHotkey(Settings settings,Profile saved,Profile current,Journal journal) {
            return settings!=null&&settings.Tested&&settings.VirtualKeys.Count==2
                &&saved!=null&&current!=null&&saved.Displays.Count==2&&current.Displays.All(d=>!d.Virtual)
                &&(journal==null||(journal.Phase=="lab"&&!journal.Confirmed))
                &&Displays.Matches(saved,current,true)&&current.Displays.All(d=>saved.Displays.Any(s=>s.Key==d.Key&&s.Scale==d.Scale));
        }
        public static void HideFromHotkey() {
            using(var mutex=new Mutex(false,MutexName)) {
                try{if(!mutex.WaitOne(0))throw new InvalidOperationException("다른 화면 전환이 진행 중입니다.");}catch(AbandonedMutexException){}
                try {
                    var current=Displays.Capture();var state=Store.Read<Journal>("transaction.json");
                    if(state!=null&&state.Phase=="remote"&&state.Confirmed&&current.Displays.Count>0&&current.Displays.All(d=>d.Virtual))return;
                    var settings=Store.Read<Settings>("settings.json");
                    if(!CanHideFromHotkey(settings,Store.Read<Profile>("lab-profile.json"),current,state))throw new InvalidOperationException("단축키로 숨기기 전에 앱에서 연구실 배치를 저장하고, Chrome 원격에서 화면 숨김을 한 번 확인해 주세요. 현재 모니터 해상도와 배율도 저장된 구성과 같아야 합니다.");
                    // The user has already verified this PC's remote setup. The
                    // departure shortcut intentionally keeps it hidden without a
                    // confirmation dialog on the now-inactive physical displays.
                    var journal=EnterRemote(60,settings.Compact);
                    try{Confirm(journal.Id);}catch{RestoreJournal(journal);throw;}
                    Store.Log("Departure shortcut completed; remote mode kept hidden.");
                }finally{mutex.ReleaseMutex();}
            }
        }
        public static void RestoreLab() {
            using(var mutex=new Mutex(false,MutexName)) {
                try { if(!mutex.WaitOne(20000))throw new InvalidOperationException("다른 화면 전환이 끝나지 않았습니다."); }catch(AbandonedMutexException){}
                try {
                    var saved=Store.Read<Profile>("lab-profile.json");if(saved==null)throw new InvalidOperationException("저장한 연구실 구성이 없습니다.");
                    var before=Displays.Capture();var windows=Windows.Capture(before);
                    var j=Store.Read<Journal>("transaction.json");if(j!=null){j.Phase="restoring";j.Confirmed=false;j.OwnerPid=Process.GetCurrentProcess().Id;j.OwnerStart=Process.GetCurrentProcess().StartTime.ToUniversalTime().Ticks;j.DeadlineUtc=DateTime.UtcNow.AddSeconds(30).ToString("o");Store.Write("transaction.json",j);}
                    Displays.Apply(saved,true,true);Thread.Sleep(400);Windows.Restore(windows,before,saved);
                    if(j!=null){j.Phase="lab";j.Confirmed=false;Store.Write("transaction.json",j);}
                    var settings=Store.Read<Settings>("settings.json")??new Settings();settings.LastMode="lab";Store.Write("settings.json",settings);Store.Log("Lab profile restored.");
                }finally{mutex.ReleaseMutex();}
            }
        }
        public static object RepairRemoteLayout(bool allWindows) {
            using(var mutex=new Mutex(false,MutexName)) {
                try{if(!mutex.WaitOne(5000))throw new InvalidOperationException("다른 화면 전환이 진행 중입니다.");}catch(AbandonedMutexException){}
                try {
                    var j=Store.Read<Journal>("transaction.json");var current=Displays.Capture();
                    if(j==null||j.After==null||!Displays.Matches(j.After,current,true)||current.Displays.Any(d=>!d.Virtual))throw new InvalidOperationException("저장한 가상 화면 구성에서만 창 배치를 복구할 수 있습니다.");
                    var checks=Windows.Inspect(j.Windows,j.Before,j.After);
                    var selected=allWindows?j.Windows:j.Windows.Where(w=>checks.Any(c=>c.Handle==w.Handle&&c.Exists&&!c.SameMonitor)).ToList();
                    // A repair changes window placement only; the confirmed remote
                    // journal and physical-output privacy state stay untouched.
                    int restored=Windows.Restore(selected,j.Before,j.After);
                    return new { Selected=selected.Count,Restored=restored,Windows=Windows.Inspect(selected,j.Before,j.After) };
                }finally{mutex.ReleaseMutex();}
            }
        }
        public static void UpdateRemoteQuality(bool compact) {
            using(var mutex=new Mutex(false,MutexName)) {
                try{if(!mutex.WaitOne(5000))throw new InvalidOperationException("다른 화면 전환이 진행 중입니다.");}catch(AbandonedMutexException){}
                try {
                    var current=Displays.Capture();var j=Store.Read<Journal>("transaction.json");
                    if(j==null||j.Phase!="remote"||!j.Confirmed||current.Displays.Any(d=>!d.Virtual)||!Displays.Matches(j.After,current,true))throw new InvalidOperationException("확정된 원격 모드에서만 해상도를 조정할 수 있습니다.");
                    var after=Displays.RemoteProfile(compact);
                    if(after.Displays.Any(d=>!d.Virtual)||after.Displays.Count!=current.Displays.Count||after.Displays.Any(d=>!current.Displays.Any(c=>c.Key==d.Key)))throw new InvalidOperationException("현재 가상 화면만 조정할 수 있습니다.");
                    Displays.Check(Displays.Validate(after),"Validate remote quality profile");var windows=Windows.Capture(current);
                    var originalJournal=Store.Serializer().Deserialize<Journal>(Store.Serializer().Serialize(j));var originalSettings=Store.Read<Settings>("settings.json")??new Settings();
                    Store.Write("remote-adjustment-backup.json",new { Time=DateTime.UtcNow.ToString("o"),Before=current,Windows=windows,Journal=j });
                    try {
                        // Keep the monitor's chosen scale stable while changing
                        // resolution; Windows may change its recommended DPI after
                        // a resolution switch before its APIs agree on that value.
                        Dpi.MatchVirtual(after);Displays.Apply(after,false);Dpi.MatchVirtual(after);Windows.Restore(windows,current,after);
                        j.After=after;j.Compact=compact;Store.Write("transaction.json",j);
                        var settings=Store.Read<Settings>("settings.json")??new Settings();settings.Compact=compact;Store.Write("settings.json",settings);
                        Store.Log("Remote quality changed; physical outputs remain disabled. Compact="+compact);
                    }catch {
                        Displays.Apply(current,false);Dpi.MatchVirtual(current);Windows.Restore(windows,current,current);Store.Write("transaction.json",originalJournal);Store.Write("settings.json",originalSettings);throw;
                    }
                }finally{mutex.ReleaseMutex();}
            }
        }
        public static void RecoverPending() {
            using(var mutex=new Mutex(false,MutexName)) {
                try{if(!mutex.WaitOne(0)){
                    var pending=Store.Read<Journal>("transaction.json");
                    if(pending!=null&&!pending.Confirmed&&(pending.Phase=="preparing"||pending.Phase=="restoring")&&DateTime.UtcNow>DateTime.Parse(pending.DeadlineUtc,null,System.Globalization.DateTimeStyles.RoundtripKind).AddSeconds(5)&&Store.OwnerAlive(pending)){
                        var owner=Process.GetProcessById(pending.OwnerPid);
                        if(owner.ProcessName=="LabDisplaySwitch"&&Path.GetFileName(owner.MainModule.FileName)=="LabDisplaySwitch.exe"){Store.Log("Stopping stalled switch process after recovery deadline.");owner.Kill();}
                    }
                    return;
                }}catch(AbandonedMutexException){}
                try {
                    var j=Store.Read<Journal>("transaction.json");if(NeedsRollback(j,DateTime.UtcNow, j!=null&&Store.OwnerAlive(j)))RestoreJournal(j);
                }finally{mutex.ReleaseMutex();}
            }
        }
        private static void RestoreJournal(Journal j) {
            try {
                Displays.Apply(j.Before,true,!j.Before.Displays.Any(d=>d.Virtual));Thread.Sleep(350);Windows.Restore(j.Windows,j.Before,j.Before);
                j.Phase="lab";j.Confirmed=false;Store.Write("transaction.json",j);Store.Log("Automatic rollback completed: "+j.Id);
            }catch(Exception e){j.Phase="failed";j.LastError=e.Message;Store.Write("transaction.json",j);Store.Log("Automatic rollback failed: "+e.Message);throw;}
        }
        public static void ReconcileStartup() {
            var j=Store.Read<Journal>("transaction.json");if(j==null||j.Phase=="lab")return;
            DateTime started;if(DateTime.TryParse(j.StartedUtc,null,System.Globalization.DateTimeStyles.RoundtripKind,out started)&&started<DateTime.UtcNow.AddMilliseconds(-(double)Native.GetTickCount64()).AddSeconds(-5))RestoreLab();
        }
        public static void CheckRemotePrivacy() {
            using(var mutex=new Mutex(false,MutexName)) {
                try{if(!mutex.WaitOne(0))return;}catch(AbandonedMutexException){}
                try {
                    var j=Store.Read<Journal>("transaction.json");if(j==null||j.Phase!="remote"||!j.Confirmed)return;
                    if(Displays.Capture().Displays.Any(d=>!d.Virtual)) {
                        Store.Log("Unexpected physical output in remote mode; requesting workstation lock.");
                        if(Native.LockWorkStation()){j.Phase="privacy-fault";j.Confirmed=false;j.LastError="화면 구성이 외부에서 변경되어 Windows 잠금을 요청했습니다. 원격 모드를 다시 적용하세요.";Store.Write("transaction.json",j);}
                    }
                }finally{mutex.ReleaseMutex();}
            }
        }
    }
}
