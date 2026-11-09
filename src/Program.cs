using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows.Forms;

namespace LabDisplaySwitch {
    internal static class Program {
        [STAThread] private static int Main(string[] args) {
            Native.SetProcessDpiAwarenessContext(new IntPtr(-4));
            string output=Value(args,"--output");
            try {
                if(args.Contains("--guard")){Guard.Run();return 0;}
                if(args.Contains("--cursor-status")){Report(output,CursorRecovery.Inspect());return 0;}
                if(args.Contains("--install-driver")){Installer.Install();return 0;}
                if(args.Contains("--preview-cursor-fix")){Report(output,Installer.PreviewCursorFix());return 0;}
                if(args.Contains("--fix-cursor-config")){Report(output,Installer.FixCursorConfiguration());return 0;}
                if(args.Contains("--apply-cursor-fix-in-lab")){Report(output,DriverMaintenance.ApplyCursorFixInLab());return 0;}
                if(args.Contains("--stop-guard")){var state=Store.Read<Journal>("transaction.json");if(state!=null&&state.Phase!="lab"&&state.Phase!="failed")throw new InvalidOperationException("연구실 모드에서만 복구 프로그램을 종료할 수 있습니다.");File.WriteAllText(Store.PathFor("guard-stop.request"),DateTime.UtcNow.ToString("o"));return 0;}
                if(args.Contains("--retire-old-guard")){
                    if(!Installer.IsAdmin)throw new InvalidOperationException("관리자 권한이 필요합니다.");
                    if(Displays.Capture().Displays.Any(d=>d.Virtual))throw new InvalidOperationException("연구실 모드에서 업데이트하세요.");
                    int pid=Int32.Parse(Value(args,"--retire-old-guard"));var old=Process.GetProcessById(pid);
                    if(!String.Equals(old.MainModule.FileName,Path.Combine(Store.Root,"app","LabDisplaySwitch.exe"),StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("예상한 이전 앱 경로가 아닙니다.");
                    old.Kill();old.WaitForExit(5000);Report(output,new{Success=true});return 0;
                }
                if(args.Contains("--verify-rollback")){
                    var j=Store.Read<Journal>("transaction.json");if(j==null)throw new InvalidOperationException("No transaction available");
                    var now=Windows.Capture(Displays.Capture());var matched=j.Windows.Where(w=>now.Any(n=>n.Handle==w.Handle&&n.Pid==w.Pid)).ToList();
                    var differences=matched.Where(w=>{var n=now.First(x=>x.Handle==w.Handle&&x.Pid==w.Pid);return w.Show!=n.Show||w.DisplayKey!=n.DisplayKey||Math.Abs(w.X-n.X)>2||Math.Abs(w.Y-n.Y)>2||Math.Abs(w.Width-n.Width)>2||Math.Abs(w.Height-n.Height)>2;}).Select(w=>{var n=now.First(x=>x.Handle==w.Handle&&x.Pid==w.Pid);return new{w.Class,w.Show,CurrentShow=n.Show,SameDisplay=w.DisplayKey==n.DisplayKey,DeltaX=n.X-w.X,DeltaY=n.Y-w.Y,DeltaWidth=n.Width-w.Width,DeltaHeight=n.Height-w.Height};}).ToArray();
                    Report(output,new{Phase=j.Phase,SurvivingWindows=matched.Count,MissingWindows=j.Windows.Count-matched.Count,Differences=differences});return 0;
                }
                if(args.Contains("--verify-remote-windows")){
                    var j=Store.Read<Journal>("transaction.json");if(j==null||j.After==null||Displays.Capture().Displays.Any(d=>!d.Virtual))throw new InvalidOperationException("가상 화면 모드에서만 확인할 수 있습니다.");
                    var checks=Windows.Inspect(j.Windows,j.Before,j.After);Report(output,new{Phase=j.Phase,Total=checks.Count,Found=checks.Count(c=>c.Exists),Correct=checks.Count(c=>c.Matches),WrongMonitor=checks.Count(c=>c.Exists&&!c.SameMonitor),Windows=checks});return 0;
                }
                if(args.Contains("--repair-remote-windows")){Report(output,Controller.RepairRemoteLayout(args.Contains("--all-windows")));return 0;}
                if(args.Contains("--remote-quality")){
                    string quality=Value(args,"--remote-quality");if(quality!="compact"&&quality!="native")throw new InvalidOperationException("Quality must be compact or native.");
                    Controller.UpdateRemoteQuality(quality=="compact");Report(output,new { Success=true,Quality=quality,Displays=Displays.Capture() });return 0;
                }
                if(args.Contains("--diagnose")){
                    var active=Displays.Capture();var native=Displays.Query(false);var built=Displays.Build(active,false);Report(output,new { Success=true,Displays=Displays.Inventory(true),CurrentConfigurationValidation=Displays.Validate(active),ExactValidation=Native.SetDisplayConfig((uint)native.Paths.Length,native.Paths,(uint)native.Modes.Length,native.Modes,0x60),NativeTopology=native,BuiltTopology=built,Elevated=Installer.IsAdmin,StateDirectory=Store.Root,LabProfile=Store.Read<Profile>("lab-profile.json"),Settings=Store.Read<Settings>("settings.json") });return 0;
                }
                if(args.Contains("--capture-benchmark")){Report(output,Diagnostics.CaptureTiming());return 0;}
                if(args.Contains("--dxgi-diagnose")){Report(output,DxgiDiagnostics.Inspect());return 0;}
                if(args.Contains("--dxgi-cursor-diagnose")){Report(output,DxgiDiagnostics.Inspect(true));return 0;}
                if(args.Contains("--save-lab")){Displays.SaveLab();Report(output,new{Success=true,Profile=Store.Read<Profile>("lab-profile.json")});return 0;}
                if(args.Contains("--restore")){Controller.RestoreLab();Report(output,new{Success=true,Displays=Displays.Capture()});return 0;}
                if(args.Contains("--remote-profile")){string quality=Value(args,"--quality");var profile=Displays.RemoteProfile(quality=="compact"?(bool?)true:quality=="native"?(bool?)false:null);Report(output,new{Success=true,Profile=profile,Validation=Displays.Validate(profile)});return 0;}
                if(args.Contains("--trial")){
                    int seconds=30;Int32.TryParse(Value(args,"--trial"),out seconds);seconds=Math.Max(15,Math.Min(90,seconds));
                    var before=Displays.Capture();var j=Controller.EnterRemote(seconds);
                    Report(output,new{Success=true,Phase="trial",Transaction=j,Actual=Displays.Capture()});
                    while(DateTime.UtcNow<DateTime.Parse(j.DeadlineUtc,null,System.Globalization.DateTimeStyles.RoundtripKind).AddSeconds(12)){
                        Thread.Sleep(500);var state=Store.Read<Journal>("transaction.json");if(state!=null&&(state.Phase=="lab"||state.Phase=="failed")){bool ok=state.Phase=="lab"&&Displays.Matches(before,Displays.Capture(),true);Report(output,new{Success=ok,Phase=state.Phase,RemoteProfile=j.After,Restored=Displays.Capture(),Error=state.LastError});return ok?0:1;}
                    }
                    Controller.RestoreLab();throw new InvalidOperationException("자동 복구 기한이 지나 직접 복구했습니다.");
                }
                Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
                bool created;using(var single=new Mutex(true,"Local\\LabDisplaySwitch.UI."+Process.GetCurrentProcess().SessionId,out created)){
                    if(!created){MessageBox.Show("Lab Display Switch가 이미 실행 중입니다. 작업 표시줄의 모니터 아이콘을 열어주세요.");return 0;}
                    Application.Run(new MainForm(args.Contains("--tray")));
                }return 0;
            }catch(Exception e){Store.Log(e.ToString());Report(output,new{Success=false,Error=e.Message});if(output==null)MessageBox.Show(e.Message,"Lab Display Switch",MessageBoxButtons.OK,MessageBoxIcon.Error);return 1;}
        }
        private static string Value(string[] args,string key){int i=Array.IndexOf(args,key);return i>=0&&i+1<args.Length?args[i+1]:null;}
        private static void Report(string path,object value){string text=Store.Serializer().Serialize(value);if(path!=null){Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)));File.WriteAllText(path,text,new System.Text.UTF8Encoding(false));}}
    }
}
