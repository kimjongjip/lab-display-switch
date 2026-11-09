using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
namespace LabDisplaySwitch {
    internal static class Tests {
        private static int count;
        private static void Assert(bool condition,string name){if(!condition)throw new Exception(name);count++;Console.WriteLine("PASS "+name);}
        public static int Main(string[] args) {
            if(Array.IndexOf(args,"--window-integration")>=0)return WindowIntegration.Run();
            try {
                var missingCursor=new CursorRecovery.State { Available=true,MouseKeysEnabled=true };
                Assert(CursorRecovery.NeedsRepair(true,missingCursor),"recover missing cursor with enabled MouseKeys and no detected mouse");
                Assert(!CursorRecovery.NeedsRepair(false,missingCursor),"cursor recovery does not affect lab mode");
                missingCursor.MouseKeysEnabled=false;Assert(!CursorRecovery.NeedsRepair(true,missingCursor),"cursor recovery respects disabled accessibility setting");missingCursor.MouseKeysEnabled=true;
                missingCursor.MousePresent=true;Assert(!CursorRecovery.NeedsRepair(true,missingCursor),"cursor recovery does not override application hiding with a detected mouse");missingCursor.MousePresent=false;
                missingCursor.CursorFlags=2;Assert(!CursorRecovery.NeedsRepair(true,missingCursor),"cursor recovery respects explicitly suppressed touch cursor");missingCursor.CursorFlags=0;
                missingCursor.CursorHandle=1;Assert(!CursorRecovery.NeedsRepair(true,missingCursor),"cursor recovery ignores valid cursor handles");missingCursor.CursorHandle=0;
                missingCursor.Available=false;Assert(!CursorRecovery.NeedsRepair(true,missingCursor),"cursor recovery skips inaccessible desktop");
                Assert(Marshal.SizeOf(typeof(Native.DisplayPath))==72,"native display path layout");
                Assert(Marshal.SizeOf(typeof(Native.Mode))==64,"native mode union layout");
                Assert(Marshal.SizeOf(typeof(Native.TargetName))==420,"native target device name layout");
                Assert(Marshal.SizeOf(typeof(DxgiDiagnostics.FrameInfo))==48,"DXGI frame metadata layout");
                var now=DateTime.UtcNow;var j=new Journal { Phase="awaiting-confirmation",DeadlineUtc=now.AddSeconds(5).ToString("o") };
                Assert(!Controller.NeedsRollback(j,now,true),"live unconfirmed switch waits for deadline");
                Assert(Controller.NeedsRollback(j,now.AddSeconds(6),true),"missing confirmation rolls back");
                Assert(Controller.NeedsRollback(j,now,false),"crashed switching process rolls back");
                j.Phase="remote";j.Confirmed=true;
                Assert(!Controller.NeedsRollback(j,now.AddDays(1),false),"confirmed remote mode survives app exit and disconnect");
                j.Phase="lab";j.Confirmed=false;
                Assert(!Controller.NeedsRollback(j,now.AddDays(1),false),"completed rollback is not repeated");
                j.Phase="privacy-fault";Assert(!Controller.NeedsRollback(j,now.AddDays(1),false),"privacy fault never automatically exposes physical outputs");
                var target=new DisplaySpec { X=3840,Y=288,Width=2560,Height=1440,Scale=100,WorkX=3840,WorkY=288,WorkWidth=2560,WorkHeight=1392 };
                var w=new WindowSpec { OffsetX=100,OffsetY=60,Width=1200,Height=900,Scale=100 };
                var rect=Windows.Map(w,target);Assert(rect.Left==3940&&rect.Top==348&&rect.Width==1200,"mixed-resolution monitor origin preserved");
                w.Scale=150;rect=Windows.Map(w,target);Assert(rect.Width==800&&rect.Height==600,"DPI size mapping from 150 to 100 percent");
                w.OffsetX=99999;w.OffsetY=-99999;rect=Windows.Map(w,target);
                Assert(rect.Left>=3840&&rect.Right<=6400&&rect.Top>=288,"off-screen windows clamped to destination work area");
                var a=new Profile { Displays=new List<DisplaySpec>{new DisplaySpec{Key="a",Width=3840,Height=2160},new DisplaySpec{Key="b",X=3840,Y=288,Width=2560,Height=1440}} };
                var b=Store.Serializer().Deserialize<Profile>(Store.Serializer().Serialize(a));Assert(Displays.Matches(a,b,true),"profile JSON round trip preserves geometry");
                b.Displays.Add(new DisplaySpec{Key="physical-leak"});Assert(!Displays.Matches(a,b,true),"unexpected physical display rejects remote confirmation");
                Assert(Displays.IsVirtual("Virtual Display Driver","DISPLAY#MTT", "", ""),"virtual display identification");
                Assert(!Displays.IsVirtual("MSI MD271UL","DISPLAY#MSI", "", "NVIDIA TITAN V"),"physical display identification");
                Assert(Dpi.Relative(150,200,-4,0,3)==-2,"virtual 4K recommended 200 maps to requested 150");
                Assert(Dpi.Relative(100,125,-1,0,4)==-1,"virtual QHD recommended 125 maps to requested 100");
                bool rejected=false;try{Dpi.Relative(150,175,-4,0,3);}catch(InvalidOperationException){rejected=true;}Assert(rejected,"unexpected Windows DPI response rejected before changing settings");
                var full=new Profile { Displays=new List<DisplaySpec>{new DisplaySpec{Key="primary",Virtual=true,Width=3840,Height=2160,Scale=150},new DisplaySpec{Key="secondary",Virtual=true,X=3840,Y=288,Width=2560,Height=1440,Scale=100}} };
                var compact=Displays.CompactProfile(full);
                Assert(compact.Displays[0].Width==2560&&compact.Displays[0].Height==1440&&compact.Displays[0].Scale==100,"compact 4K/150 becomes QHD/100 with the same logical workspace");
                Assert(compact.Displays[1].X==2560&&compact.Displays[1].Y==192&&compact.Displays[1].Width==2560,"compact mode keeps the second monitor separate and aligned");
                Assert(full.Displays[0].Width==3840&&full.Displays[0].Scale==150&&full.Displays[1].X==3840,"compact mode does not mutate the saved original profile");
                full.Displays[1].X=-2560;compact=Displays.CompactProfile(full);
                Assert(compact.Displays[1].X==-2560&&compact.Displays[1].X+compact.Displays[1].Width==0,"left-side monitor does not overlap the compact primary");
                var cursorXml=new System.Xml.XmlDocument();cursorXml.LoadXml(CursorConfiguration.Complete("<vdd_settings><monitors><count>2</count></monitors><gpu><friendlyname>NVIDIA TITAN V</friendlyname></gpu><cursor><HardwareCursor>true</HardwareCursor></cursor></vdd_settings>"));
                Assert(cursorXml.SelectSingleNode("/vdd_settings/cursor/CursorMaxX").InnerText=="128"&&cursorXml.SelectSingleNode("/vdd_settings/cursor/CursorMaxY").InnerText=="128"&&cursorXml.SelectSingleNode("/vdd_settings/cursor/AlphaCursorSupport").InnerText=="true","hardware cursor config includes finite dimensions and alpha support");
                Assert(cursorXml.SelectSingleNode("/vdd_settings/monitors/count").InnerText=="2"&&cursorXml.SelectSingleNode("/vdd_settings/gpu/friendlyname").InnerText=="NVIDIA TITAN V","cursor correction preserves monitor count and rendering GPU");
                string customCursor="<vdd_settings>\n  <cursor><HardwareCursor>false</HardwareCursor><CursorMaxX>64</CursorMaxX><CursorMaxY>64</CursorMaxY><AlphaCursorSupport>false</AlphaCursorSupport></cursor>\n</vdd_settings>";
                Assert(CursorConfiguration.Complete(customCursor)==customCursor,"reapplying cursor correction preserves explicit choices and formatting");
                Assert(DriverMaintenance.CanRestart(a,a,new Journal{Phase="lab"}),"driver maintenance allowed on saved physical lab displays");
                Assert(!DriverMaintenance.CanRestart(a,full,new Journal{Phase="remote",Confirmed=true}),"driver maintenance blocked while virtual screens carry the session");
                Assert(!DriverMaintenance.CanRestart(a,a,new Journal{Phase="remote",Confirmed=true}),"driver maintenance blocked when physical outputs unexpectedly appear during remote mode");
                Assert(!DriverMaintenance.CanRestart(a,b,new Journal{Phase="lab"}),"driver maintenance rejects mixed or unexpected display configurations");
                Assert(!DriverMaintenance.CanRestart(a,a,new Journal{Phase="awaiting-confirmation"}),"driver maintenance cannot interrupt an unconfirmed display switch");
                var hotkeySettings=new Settings { Tested=true,VirtualKeys=new List<string>{"virtual-a","virtual-b"} };
                Assert(Controller.CanHideFromHotkey(hotkeySettings,a,a,new Journal{Phase="lab"}),"departure shortcut accepts a previously tested physical lab setup");
                hotkeySettings.Tested=false;
                Assert(!Controller.CanHideFromHotkey(hotkeySettings,a,a,new Journal{Phase="lab"}),"departure shortcut rejects an untested remote setup");
                hotkeySettings.Tested=true;
                Assert(!Controller.CanHideFromHotkey(hotkeySettings,a,b,new Journal{Phase="lab"}),"departure shortcut rejects changed monitor layout");
                Assert(!Controller.CanHideFromHotkey(hotkeySettings,a,a,new Journal{Phase="awaiting-confirmation"}),"departure shortcut cannot confirm an unrelated pending trial");
                var scaled=Store.Serializer().Deserialize<Profile>(Store.Serializer().Serialize(a));scaled.Displays[0].Scale=150;
                Assert(!Controller.CanHideFromHotkey(hotkeySettings,a,scaled,new Journal{Phase="lab"}),"departure shortcut rejects an untested physical DPI change");
                Assert(Dpi.Relative(125,100,-1,-4,4)==0,"resolution change accepts a stale lower DPI offset only at clamped 100 percent");
                bool badClamp=false;try{Dpi.Relative(125,150,-1,-4,4);}catch(InvalidOperationException){badClamp=true;}Assert(badClamp,"stale DPI offset cannot bypass observed-scale verification");
                var portrait=Store.Serializer().Deserialize<Profile>(Store.Serializer().Serialize(full));portrait.Displays[1].Rotation=2;portrait.Displays[1].X=3840;portrait.Displays[1].Y=-885;
                var landscape=Displays.LandscapeProfile(portrait,true);
                Assert(landscape.Displays[0].Width==2560&&landscape.Displays[1].Width==2560&&landscape.Displays[1].Height==1440&&landscape.Displays[1].Rotation==1&&landscape.Displays[1].Scale==100,"portrait lab layout produces two QHD landscape remote displays at 100 percent");
                Assert(landscape.Displays[1].X==2560&&landscape.Displays[1].Y==0&&portrait.Displays[1].Rotation==2&&portrait.Displays[1].Y==-885,"remote landscape joins screens without mutating the physical portrait profile");
                var rotated=Store.Serializer().Deserialize<Profile>(Store.Serializer().Serialize(landscape));rotated.Displays[1].Rotation=2;
                Assert(!Displays.Matches(landscape,rotated,true),"remote orientation mismatch rejects confirmation");
                Console.WriteLine("All "+count+" tests passed.");return 0;
            }catch(Exception e){Console.Error.WriteLine(e);return 1;}
        }
    }
}
