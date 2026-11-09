using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows.Forms;

namespace LabDisplaySwitch {
    internal sealed class GuardWindow:NativeWindow,IDisposable {
        private bool restoring;
        public GuardWindow() { CreateHandle(new CreateParams { Caption="LabDisplaySwitch recovery",Parent=new IntPtr(-3) });if(!Native.RegisterHotKey(Handle,1,0x4007,0x7B))Store.Log("Recovery hotkey registration failed."); }
        protected override void WndProc(ref Message m) {
            if(m.Msg==0x312&&!restoring){restoring=true;new Thread(delegate(){try{Controller.RestoreLab();}catch(Exception e){Store.Log("Hotkey restore failed: "+e.Message);}finally{restoring=false;}}){IsBackground=true}.Start();}
            base.WndProc(ref m);
        }
        public void Dispose(){Native.UnregisterHotKey(Handle,1);DestroyHandle();}
    }
    internal static class Guard {
        public static void Run() {
            bool created;using(var mutex=new Mutex(true,"Local\\LabDisplaySwitch.Guard."+Process.GetCurrentProcess().SessionId,out created)) {
                if(!created)return;
                using(var window=new GuardWindow())using(var ready=new EventWaitHandle(false,EventResetMode.ManualReset,"Local\\LabDisplaySwitch.GuardReady."+Process.GetCurrentProcess().SessionId)) {
                    string stopPath=Store.PathFor("guard-stop.request");if(File.Exists(stopPath))File.Delete(stopPath);
                    var stopTimer=new System.Windows.Forms.Timer { Interval=500 };stopTimer.Tick+=delegate{if(File.Exists(stopPath)){var state=Store.Read<Journal>("transaction.json");if(state==null||state.Phase=="lab"||state.Phase=="failed"){File.Delete(stopPath);Application.Exit();}}};stopTimer.Start();
                    new Thread(delegate(){
                        bool awake=false;int check=0;while(true){try{
                            Controller.RecoverPending();var j=Store.Read<Journal>("transaction.json");bool remote=j!=null&&((j.Phase=="remote"&&j.Confirmed)||j.Phase=="privacy-fault");
                            if(remote!=awake){Native.SetThreadExecutionState(remote?0x80000001u:0x80000000u);awake=remote;}
                            if(remote&&++check%4==0)Controller.CheckRemotePrivacy();
                        }catch(Exception e){Store.Log("Recovery check: "+e.Message);}Thread.Sleep(500);}
                    }){IsBackground=true}.Start();
                    ready.Set();Application.Run();ready.Reset();stopTimer.Dispose();
                }
            }
        }
    }
}
