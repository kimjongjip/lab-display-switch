using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace LabDisplaySwitch {
    internal sealed class TrayHotkeys:NativeWindow,IDisposable {
        public const string RestoreShortcut="Ctrl + Alt + Home",HideShortcut="Ctrl + Alt + End";
        private readonly Action<bool> activate;
        public bool RestoreRegistered { get; private set; }
        public bool HideRegistered { get; private set; }
        public TrayHotkeys(Action<bool> action) {
            activate=action;CreateHandle(new CreateParams { Caption="LabDisplaySwitch tray hotkeys",Parent=new IntPtr(-3) });
            RestoreRegistered=Native.RegisterHotKey(Handle,1,0x4003,0x24);
            int restoreError=RestoreRegistered?0:Marshal.GetLastWin32Error();
            // Never enable immediate hiding without the matching return shortcut.
            HideRegistered=RestoreRegistered&&Native.RegisterHotKey(Handle,2,0x4003,0x23);
            int hideError=HideRegistered?0:RestoreRegistered?Marshal.GetLastWin32Error():0;
            var owner=Process.GetCurrentProcess();
            Store.Write("tray-hotkeys.json",new { Pid=owner.Id,ProcessStart=owner.StartTime.ToUniversalTime().Ticks,Window=Handle.ToInt64(),RestoreRegistered=RestoreRegistered,HideRegistered=HideRegistered,RestoreError=restoreError,HideError=hideError });
            Store.Log("Tray hotkeys registered: restore="+RestoreRegistered+", hide="+HideRegistered);
        }
        protected override void WndProc(ref Message message) {
            if(message.Msg==0x312) {
                if(message.WParam.ToInt32()==1&&RestoreRegistered)activate(false);
                else if(message.WParam.ToInt32()==2&&HideRegistered)activate(true);
            }
            base.WndProc(ref message);
        }
        public void Dispose() {
            if(HideRegistered)Native.UnregisterHotKey(Handle,2);
            if(RestoreRegistered)Native.UnregisterHotKey(Handle,1);
            DestroyHandle();
        }
    }
}
