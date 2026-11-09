using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace LabDisplaySwitch {
    internal sealed class WindowCheck {
        public long Handle;public uint Pid,ExpectedShow,ActualShow;public string Class,ExpectedDisplay,ActualDisplay;
        public bool Exists,SameMonitor,SameBounds,SameShow;
        public int DeltaX,DeltaY,DeltaWidth,DeltaHeight;
        public bool Matches { get { return Exists&&SameMonitor&&SameBounds&&SameShow; } }
    }
    internal static class Windows {
        public static List<WindowCheck> Inspect(List<WindowSpec> windows,Profile source,Profile destination) {
            var live=Displays.Capture();var actual=Capture(live);
            var src=source.Displays.OrderBy(d=>d.X).ThenBy(d=>d.Y).ToList();
            var dst=destination.Displays.OrderBy(d=>d.X).ThenBy(d=>d.Y).ToList();
            var checks=new List<WindowCheck>();
            foreach(var saved in windows) {
                int index=src.FindIndex(d=>String.Equals(d.Key,saved.DisplayKey,StringComparison.OrdinalIgnoreCase));if(index<0||index>=dst.Count)continue;
                var target=live.Displays.FirstOrDefault(d=>String.Equals(d.Key,dst[index].Key,StringComparison.OrdinalIgnoreCase));if(target==null)continue;
                var current=actual.FirstOrDefault(w=>w.Handle==saved.Handle&&w.Pid==saved.Pid&&(saved.ProcessStart==0||w.ProcessStart==saved.ProcessStart)&&w.Class==saved.Class);
                var check=new WindowCheck { Handle=saved.Handle,Pid=saved.Pid,Class=saved.Class,ExpectedShow=saved.Show,ExpectedDisplay=target.GdiName,Exists=current!=null };checks.Add(check);
                if(current==null)continue;
                var expected=Map(saved,target);check.ActualDisplay=live.Displays.First(d=>d.Key==current.DisplayKey).GdiName;
                check.ActualShow=current.Show;check.SameShow=current.Show==saved.Show;check.SameMonitor=String.Equals(current.DisplayKey,target.Key,StringComparison.OrdinalIgnoreCase);
                check.DeltaX=current.X-expected.Left;check.DeltaY=current.Y-expected.Top;check.DeltaWidth=current.Width-expected.Width;check.DeltaHeight=current.Height-expected.Height;
                check.SameBounds=Math.Abs(check.DeltaX)<=3&&Math.Abs(check.DeltaY)<=3&&Math.Abs(check.DeltaWidth)<=3&&Math.Abs(check.DeltaHeight)<=3;
            }
            return checks;
        }
        public static List<WindowSpec> Capture(Profile profile) {
            var result=new List<WindowSpec>(); uint own=(uint)Process.GetCurrentProcess().Id;
            Native.EnumWindows(delegate(IntPtr hwnd,IntPtr data) {
                try {
                    if(!Native.IsWindowVisible(hwnd)||Native.GetWindow(hwnd,4)!=IntPtr.Zero)return true;
                    uint pid;Native.GetWindowThreadProcessId(hwnd,out pid);if(pid==own)return true;
                    int cloaked;if(Native.DwmGetWindowAttribute(hwnd,14,out cloaked,4)==0&&cloaked!=0)return true;
                    var cls=new StringBuilder(256);Native.GetClassName(hwnd,cls,cls.Capacity);
                    if(cls.ToString()=="Progman"||cls.ToString()=="WorkerW"||cls.ToString().StartsWith("Shell_"))return true;
                    if((Native.GetWindowLongPtr(hwnd,-20).ToInt64()&0x80)!=0)return true;
                    var placement=new Native.Placement { Length=Marshal.SizeOf(typeof(Native.Placement)) };
                    if(!Native.GetWindowPlacement(hwnd,ref placement))return true;
                    var monitor=new Native.MonitorInfo { Size=Marshal.SizeOf(typeof(Native.MonitorInfo)) };
                    if(!Native.GetMonitorInfo(Native.MonitorFromWindow(hwnd,2),ref monitor))return true;
                    DisplaySpec display=profile.Displays.FirstOrDefault(d=>d.GdiName==monitor.Device);if(display==null)return true;
                    Native.Rect rect=placement.Normal;
                    // WINDOWPLACEMENT uses workspace coordinates; convert to screen coordinates.
                    int ox=monitor.Work.Left-monitor.Bounds.Left,oy=monitor.Work.Top-monitor.Bounds.Top;
                    if(rect.Width<40||rect.Height<30)return true;
                    long started=0;try{started=Process.GetProcessById((int)pid).StartTime.ToUniversalTime().Ticks;}catch{}
                    result.Add(new WindowSpec { Handle=hwnd.ToInt64(),Pid=pid,ProcessStart=started,Class=cls.ToString(),DisplayKey=display.Key,Show=placement.Show,PlacementFlags=placement.Flags,X=rect.Left+ox,Y=rect.Top+oy,Width=rect.Width,Height=rect.Height,OffsetX=rect.Left+ox-display.X,OffsetY=rect.Top+oy-display.Y,Scale=display.Scale,Dpi=Native.GetDpiForWindow(hwnd) });
                }catch{ }
                return true;
            },IntPtr.Zero);return result;
        }
        public static Native.Rect Map(WindowSpec window,DisplaySpec target) {
            double factor=(double)(target.Scale>0?target.Scale:100)/(window.Scale>0?window.Scale:100);
            int width=Math.Max(80,(int)Math.Round(window.Width*factor)),height=Math.Max(50,(int)Math.Round(window.Height*factor));
            int workWidth=target.WorkWidth>0?target.WorkWidth:target.Width,workHeight=target.WorkHeight>0?target.WorkHeight:target.Height;
            int left=target.WorkWidth>0?target.WorkX:target.X,top=target.WorkHeight>0?target.WorkY:target.Y;
            width=Math.Min(width,workWidth);height=Math.Min(height,workHeight);
            int x=target.X+(int)Math.Round(window.OffsetX*factor),y=target.Y+(int)Math.Round(window.OffsetY*factor);
            x=Math.Max(left,Math.Min(x,left+workWidth-width));y=Math.Max(top,Math.Min(y,top+workHeight-height));
            return new Native.Rect { Left=x,Top=y,Right=x+width,Bottom=y+height };
        }
        public static int Restore(List<WindowSpec> windows,Profile source,Profile destination) {
            var watch=Stopwatch.StartNew();int stable=0;
            for(int attempt=0;attempt<8;attempt++) {
                var checks=Inspect(windows,source,destination);
                var pending=windows.Where(w=>checks.Any(c=>c.Handle==w.Handle&&c.Exists&&!c.Matches)).ToList();
                if(pending.Count==0){stable++;if(stable>=3&&watch.ElapsedMilliseconds>=900)break;}
                else {stable=0;RestorePass(pending,source,destination);}
                Thread.Sleep(250);
            }
            var final=Inspect(windows,source,destination);
            Store.Write("window-restore-latest.json",new { Time=DateTime.UtcNow.ToString("o"),Total=final.Count,Restored=final.Count(c=>c.Matches),Missing=final.Count(c=>!c.Exists),Unresolved=final.Where(c=>c.Exists&&!c.Matches).ToArray() });
            Store.Log("Window restore verified: "+final.Count(c=>c.Matches)+"/"+final.Count(c=>c.Exists)+" surviving windows.");
            return final.Count(c=>c.Matches);
        }
        private static int RestorePass(List<WindowSpec> windows,Profile source,Profile destination) {
            int restored=0;var live=Displays.Capture();
            var src=source.Displays.OrderBy(d=>d.X).ThenBy(d=>d.Y).ToList();
            var dst=destination.Displays.OrderBy(d=>d.X).ThenBy(d=>d.Y).ToList();
            // Place a maximized window in its restored state on the destination
            // monitor first. Otherwise Windows may keep maximizing it on the old
            // monitor, or apply a late WM_DPICHANGED resize over the saved bounds.
            for(int pass=0;pass<2;pass++) {
            foreach(var saved in windows) {
                try {
                    IntPtr hwnd=new IntPtr(saved.Handle);if(!Native.IsWindow(hwnd))continue;
                    uint pid;Native.GetWindowThreadProcessId(hwnd,out pid);if(pid!=saved.Pid)continue;
                    if(saved.ProcessStart!=0&&Process.GetProcessById((int)pid).StartTime.ToUniversalTime().Ticks!=saved.ProcessStart)continue;
                    var cls=new StringBuilder(256);Native.GetClassName(hwnd,cls,cls.Capacity);if(cls.ToString()!=saved.Class)continue;
                    int index=src.FindIndex(d=>String.Equals(d.Key,saved.DisplayKey,StringComparison.OrdinalIgnoreCase));if(index<0||index>=dst.Count)continue;
                    var target=live.Displays.FirstOrDefault(d=>String.Equals(d.Key,dst[index].Key,StringComparison.OrdinalIgnoreCase));if(target==null)continue;
                    Native.Rect mapped=Map(saved,target);
                    var p=new Native.Placement { Length=Marshal.SizeOf(typeof(Native.Placement)) };if(!Native.GetWindowPlacement(hwnd,ref p))continue;
                    int ox=target.WorkX-target.X,oy=target.WorkY-target.Y;
                    mapped.Left-=ox;mapped.Right-=ox;mapped.Top-=oy;mapped.Bottom-=oy;
                    p.Normal=mapped;p.Show=pass==0&&saved.Show==3?9u:saved.Show;p.Flags=4|(saved.Show==2?saved.PlacementFlags&2:0); // async, preserve restore-to-maximized for minimized windows
                    if(Native.SetWindowPlacement(hwnd,ref p))restored++;
                }catch(Exception e){Store.Log("Window restore skipped: "+e.GetType().Name);}
            }
            if(pass==0)Thread.Sleep(180);
            }
            return restored;
        }
    }
}
