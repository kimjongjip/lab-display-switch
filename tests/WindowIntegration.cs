using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;

namespace LabDisplaySwitch {
    internal static class WindowIntegration {
        public static int Run() {
            Native.SetProcessDpiAwarenessContext(new IntPtr(-4));
            Process fixture=null;
            try {
                var profile=Displays.Capture();if(profile.Displays.Count<2)throw new Exception("Two active monitors are needed for this integration test.");
                fixture=Process.Start(new ProcessStartInfo(Path.Combine(Path.GetDirectoryName(Store.Exe),"LabDisplaySwitch.TestWindows.exe")) { UseShellExecute=false,CreateNoWindow=true });
                fixture.WaitForInputIdle(4000);Thread.Sleep(900);
                var saved=Windows.Capture(profile).Where(w=>w.Pid==(uint)fixture.Id).ToList();
                if(saved.Count!=3||!saved.Any(w=>w.Show==3)||!saved.Any(w=>w.Show==2))throw new Exception("Test windows did not reach normal/maximized/minimized states.");
                using(var move=EventWaitHandle.OpenExisting("Local\\LabDisplaySwitch.WindowTest."+fixture.Id))move.Set();
                Thread.Sleep(800);
                var before=Windows.Inspect(saved,profile,profile);if(before.Count(c=>c.Exists&&!c.SameMonitor)<2){Console.WriteLine(Store.Serializer().Serialize(new { Saved=saved,Before=before }));throw new Exception("Test did not reproduce windows moving to the wrong monitor.");}
                int restored=Windows.Restore(saved,profile,profile);var after=Windows.Inspect(saved,profile,profile);
                Console.WriteLine(Store.Serializer().Serialize(new { Before=before,After=after,Restored=restored }));
                if(restored!=3||after.Any(c=>!c.Matches))throw new Exception("Real-window restoration failed.");
                var actual=Windows.Capture(profile);foreach(var w in saved.Where(w=>w.Show==2))if((actual.First(a=>a.Handle==w.Handle).PlacementFlags&2)!=(w.PlacementFlags&2))throw new Exception("Minimized window lost its restore-to-maximized flag.");
                Console.WriteLine("PASS real normal, maximized, minimized windows return to the correct monitor, size, position and state across 150%/100% DPI.");return 0;
            }catch(Exception e){Console.Error.WriteLine(e);return 1;}
            finally{if(fixture!=null&&!fixture.HasExited){fixture.Kill();fixture.WaitForExit(3000);}}
        }
    }
}
