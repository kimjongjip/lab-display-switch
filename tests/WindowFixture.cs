using System;
using System.Drawing;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Windows.Forms;

namespace LabDisplaySwitch {
    internal static class WindowFixture {
        [STAThread] public static void Main() {
            Native.SetProcessDpiAwarenessContext(new IntPtr(-4));
            Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
            var screens=Screen.AllScreens.OrderBy(s=>s.Bounds.Left).ThenBy(s=>s.Bounds.Top).ToArray();
            if(screens.Length<2)return;var area=screens[1].WorkingArea;var windows=new List<Form>();
            for(int i=0;i<3;i++) {
                int state=i;var form=new Form { Text="Lab Display Switch repair test "+(i+1),StartPosition=FormStartPosition.Manual,Bounds=new Rectangle(area.X+120+i*90,area.Y+100+i*60,600,420),BackColor=Color.FromArgb(29,37,52),ForeColor=Color.White };
                form.Controls.Add(new Label { Text="Window restoration test\nThis temporary test window closes automatically.",AutoSize=true,Location=new Point(20,25) });
                form.Shown+=delegate{
                    Native.SetWindowPos(form.Handle,IntPtr.Zero,area.X+120+state*90,area.Y+100+state*60,600,420,0x14);
                    var settle=new System.Windows.Forms.Timer { Interval=250 };settle.Tick+=delegate{settle.Stop();settle.Dispose();if(state>0)form.WindowState=FormWindowState.Maximized;if(state==2)form.WindowState=FormWindowState.Minimized;};settle.Start();
                };form.Show();windows.Add(form);
            }
            using(var move=new EventWaitHandle(false,EventResetMode.AutoReset,"Local\\LabDisplaySwitch.WindowTest."+Process.GetCurrentProcess().Id)) {
            var poll=new System.Windows.Forms.Timer { Interval=100 };poll.Tick+=delegate{if(move.WaitOne(0))for(int i=0;i<windows.Count;i++){
                var form=windows[i];form.WindowState=FormWindowState.Normal;form.Bounds=new Rectangle(screens[0].WorkingArea.X+100+i*40,screens[0].WorkingArea.Y+100+i*40,760,560);
                if(i>0)form.WindowState=FormWindowState.Maximized;if(i==2)form.WindowState=FormWindowState.Minimized;
            }};poll.Start();
            var timer=new System.Windows.Forms.Timer { Interval=20000 };timer.Tick+=delegate{Application.Exit();};timer.Start();Application.Run();timer.Dispose();
            poll.Dispose();}
        }
    }
}
