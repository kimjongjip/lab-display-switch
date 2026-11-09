using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Threading;

namespace LabDisplaySwitch {
    internal sealed class CursorRecovery {
        [StructLayout(LayoutKind.Sequential)] private struct CursorInfo { public uint Size,Flags;public IntPtr Handle;public int X,Y; }
        [StructLayout(LayoutKind.Sequential)] private struct MouseKeys { public uint Size,Flags,MaxSpeed,TimeToMaxSpeed,CtrlSpeed,Reserved1,Reserved2; }
        internal sealed class State {
            public bool Available,MousePresent,MouseKeysEnabled;
            public uint CursorFlags,MouseKeysFlags;
            public long CursorHandle;
        }
        [DllImport("user32.dll",SetLastError=true)] private static extern bool GetCursorInfo(ref CursorInfo info);
        [DllImport("user32.dll")] private static extern int GetSystemMetrics(int index);
        [DllImport("user32.dll",EntryPoint="SystemParametersInfoW",SetLastError=true)] private static extern bool SystemParametersInfo(uint action,uint size,ref MouseKeys value,uint flags);
        private int missingSamples;
        private DateTime nextAttempt=DateTime.MinValue;
        private static MouseKeys ReadKeys() {
            var keys=new MouseKeys { Size=(uint)Marshal.SizeOf(typeof(MouseKeys)) };
            if(!SystemParametersInfo(0x36,keys.Size,ref keys,0))throw new Win32Exception(Marshal.GetLastWin32Error());
            return keys;
        }
        private static void WriteKeys(MouseKeys keys,uint flags) {
            keys.Reserved1=keys.Reserved2=0;
            if(!SystemParametersInfo(0x37,keys.Size,ref keys,flags))throw new Win32Exception(Marshal.GetLastWin32Error());
        }
        public static State Inspect() {
            var cursor=new CursorInfo { Size=(uint)Marshal.SizeOf(typeof(CursorInfo)) };
            bool available=GetCursorInfo(ref cursor);var keys=ReadKeys();
            return new State { Available=available,MousePresent=GetSystemMetrics(19)!=0,MouseKeysEnabled=(keys.Flags&1)!=0,MouseKeysFlags=keys.Flags,CursorFlags=cursor.Flags,CursorHandle=cursor.Handle.ToInt64() };
        }
        public static bool NeedsRepair(bool remote,State state) {
            return remote&&state.Available&&state.MouseKeysEnabled&&!state.MousePresent&&state.CursorFlags==0&&state.CursorHandle==0;
        }
        public void Poll(bool remote) {
            if(!remote){missingSamples=0;return;}
            if(DateTime.UtcNow<nextAttempt)return;
            try {
                var before=Inspect();
                if(!NeedsRepair(remote,before)){missingSamples=0;return;}
                // Require two consecutive observations; avoid toggling during a
                // brief device transition, and rate-limit failed repairs.
                if(++missingSamples<2)return;
                missingSamples=0;nextAttempt=DateTime.UtcNow.AddSeconds(30);
                var original=ReadKeys();
                if((original.Flags&1)==0)return;
                var disabled=original;disabled.Flags&=~1u;
                try { WriteKeys(disabled,0);Thread.Sleep(100); }
                finally {
                    // Never persist the temporary OFF state. Restore every
                    // preference, including Num Lock and keypad behavior.
                    try{WriteKeys(original,2);}catch{WriteKeys(original,2);}
                }
                var after=Inspect();
                Store.Write("cursor-recovery-latest.json",new { TimeUtc=DateTime.UtcNow.ToString("o"),Before=before,After=after,Recovered=after.Available&&(after.CursorFlags&1)!=0 });
                Store.Log("Cursor recovery refreshed MouseKeys; visible="+(after.Available&&(after.CursorFlags&1)!=0));
            }catch(Exception e){missingSamples=0;nextAttempt=DateTime.UtcNow.AddSeconds(60);Store.Log("Cursor recovery: "+e.Message);}
        }
    }
}
