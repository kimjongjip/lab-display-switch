using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;

namespace LabDisplaySwitch {
    internal sealed class Topology { public Native.DisplayPath[] Paths; public Native.Mode[] Modes; }
    internal static class Displays {
        public static Topology Query(bool all) {
            for(int attempt=0;attempt<5;attempt++) {
                uint np,nm; int result=Native.GetDisplayConfigBufferSizes(all?1u:2u,out np,out nm); Check(result,"GetDisplayConfigBufferSizes");
                var paths=new Native.DisplayPath[np]; var modes=new Native.Mode[nm];
                result=Native.QueryDisplayConfig(all?1u:2u,ref np,paths,ref nm,modes,IntPtr.Zero);
                if(result==122)continue; Check(result,"QueryDisplayConfig");
                Array.Resize(ref paths,(int)np);Array.Resize(ref modes,(int)nm);return new Topology { Paths=paths,Modes=modes };
            }
            throw new InvalidOperationException("화면 구성이 계속 변경되고 있습니다. 잠시 후 다시 시도하세요.");
        }
        public static void Check(int result,string operation) { if(result!=0)throw new Win32Exception(result,operation+" failed ("+result+")"); }
        private static Native.Header Header(uint type,Type shape,Native.Luid adapter,uint id) { return new Native.Header { Type=type,Size=(uint)Marshal.SizeOf(shape),Adapter=adapter,Id=id }; }
        public static DisplaySpec Describe(Native.DisplayPath path,Native.Mode[] modes) {
            var target=new Native.TargetName { Header=Header(2,typeof(Native.TargetName),path.Target.Adapter,path.Target.Id) };
            var source=new Native.SourceName { Header=Header(1,typeof(Native.SourceName),path.Source.Adapter,path.Source.Id) };
            var adapter=new Native.AdapterName { Header=Header(4,typeof(Native.AdapterName),path.Target.Adapter,0) };
            Native.GetTargetName(ref target);Native.GetSourceName(ref source);Native.GetAdapterName(ref adapter);
            string name=target.FriendlyName??"Unknown display",key=target.DevicePath??"";
            var d=new DisplaySpec { Key=key,Name=name,GdiName=source.Name??"",Adapter=path.Target.Adapter.ToString(),AdapterPath=adapter.DevicePath??"",TargetId=path.Target.Id,SourceId=path.Source.Id,Active=(path.Flags&1)!=0,Scale=100,Rotation=path.Target.Rotation,RefreshNumerator=path.Target.Refresh.Numerator,RefreshDenominator=path.Target.Refresh.Denominator };
            d.Virtual=IsVirtual(name,key,d.AdapterPath,AdapterDescription(d.GdiName));
            if(d.Active&&path.Source.ModeIndex<modes.Length) {
                var mode=modes[path.Source.ModeIndex];if(mode.Type==1){d.X=mode.X;d.Y=mode.Y;d.Width=(int)mode.Width;d.Height=(int)mode.Height;}
            }
            return d;
        }
        public static bool IsVirtual(string name,string key,string adapterPath,string adapterDescription) {
            string s=(name+" "+key+" "+adapterDescription).ToLowerInvariant();
            return s.Contains("virtual")||s.Contains("parsec")||s.Contains("mttvdd")||s.Contains("iddsample")||s.Contains("display\\mtt")||s.Contains("display#mtt");
        }
        private static string AdapterDescription(string gdi) {
            for(uint i=0;i<64;i++){var d=new Native.DisplayDevice { Size=Marshal.SizeOf(typeof(Native.DisplayDevice)) };if(!Native.EnumDisplayDevices(null,i,ref d,0))break;if(d.Name==gdi)return d.Description??"";}return "";
        }
        public static List<DisplaySpec> Inventory(bool all) {
            Topology t=Query(all); var result=new List<DisplaySpec>();
            foreach(var path in t.Paths.OrderByDescending(p=>(p.Flags&1)!=0)) {
                if(path.Target.Available==0)continue;
                DisplaySpec d=Describe(path,t.Modes); if(String.IsNullOrEmpty(d.Key)||result.Any(x=>String.Equals(x.Key,d.Key,StringComparison.OrdinalIgnoreCase)))continue;result.Add(d);
            }
            Native.EnumDisplayMonitors(IntPtr.Zero,IntPtr.Zero,delegate(IntPtr monitor,IntPtr hdc,ref Native.Rect rect,IntPtr data) {
                var info=new Native.MonitorInfo { Size=Marshal.SizeOf(typeof(Native.MonitorInfo)) };
                if(Native.GetMonitorInfo(monitor,ref info))foreach(var d in result.Where(x=>x.Active&&x.GdiName==info.Device)) {
                    int scale; if(Native.GetScaleFactorForMonitor(monitor,out scale)==0)d.Scale=scale;
                    d.WorkX=info.Work.Left;d.WorkY=info.Work.Top;d.WorkWidth=info.Work.Width;d.WorkHeight=info.Work.Height;
                }
                return true;
            },IntPtr.Zero);
            return result;
        }
        public static Profile Capture() { return new Profile { CreatedUtc=DateTime.UtcNow.ToString("o"),Displays=Inventory(false).Where(d=>d.Active).ToList() }; }
        public static void SaveLab() {
            var p=Capture();if(p.Displays.Count!=2||p.Displays.Any(d=>d.Virtual))throw new InvalidOperationException("실제 모니터 두 개만 활성화된 연구실 상태에서 저장하세요.");
            if(!p.Displays.Any(d=>d.X==0&&d.Y==0))throw new InvalidOperationException("주 모니터를 확인할 수 없습니다.");
            var previous=Store.Read<Profile>("lab-profile.json");
            if(previous==null||!Matches(previous,p,true)||p.Displays.Any(d=>!previous.Displays.Any(s=>s.Key==d.Key&&s.Scale==d.Scale))) {
                var settings=Store.Read<Settings>("settings.json")??new Settings();settings.Tested=false;Store.Write("settings.json",settings);
            }
            Store.Write("lab-profile.json",p);
        }
        public static Profile RemoteProfile(bool? compact=null) {
            Profile lab=Store.Read<Profile>("lab-profile.json");if(lab==null)throw new InvalidOperationException("연구실 화면 구성을 먼저 저장하세요.");
            Settings settings=Store.Read<Settings>("settings.json")??new Settings();
            var virtuals=Inventory(true).Where(d=>d.Virtual&&d.Name.IndexOf("Parsec",StringComparison.OrdinalIgnoreCase)<0&&d.Key.IndexOf("PSR",StringComparison.OrdinalIgnoreCase)<0).OrderBy(d=>d.Key,StringComparer.OrdinalIgnoreCase).ToList();
            if(settings.VirtualKeys.Count==2)virtuals=settings.VirtualKeys.Select(k=>virtuals.FirstOrDefault(d=>String.Equals(k,d.Key,StringComparison.OrdinalIgnoreCase))).Where(d=>d!=null).ToList();
            if(virtuals.Count!=2)throw new InvalidOperationException("앱에서 사용할 가상 모니터 두 개가 필요합니다. 먼저 가상 화면 준비를 실행하세요. 감지: "+virtuals.Count);
            var result=new Profile { CreatedUtc=DateTime.UtcNow.ToString("o") };
            for(int i=0;i<2;i++) {
                DisplaySpec original=lab.Displays.OrderBy(d=>d.X).ThenBy(d=>d.Y).ElementAt(i),v=virtuals[i];
                v.X=original.X;v.Y=original.Y;v.Width=original.Width;v.Height=original.Height;v.Scale=original.Scale;
                v.WorkX=original.WorkX;v.WorkY=original.WorkY;v.WorkWidth=original.WorkWidth;v.WorkHeight=original.WorkHeight;
                v.Rotation=original.Rotation;v.RefreshNumerator=60;v.RefreshDenominator=1;result.Displays.Add(v);
            }
            if(settings.VirtualKeys.Count==0){settings.VirtualKeys=virtuals.Select(d=>d.Key).ToList();Store.Write("settings.json",settings);}
            bool small=compact??settings.Compact;
            return settings.RemoteLandscape?LandscapeProfile(result,small):small?CompactProfile(result):result;
        }
        public static Profile LandscapeProfile(Profile original,bool compact) {
            var result=Store.Serializer().Deserialize<Profile>(Store.Serializer().Serialize(original));
            int x=0;
            foreach(var d in result.Displays.OrderBy(d=>d.X).ThenBy(d=>d.Y)) {
                d.Rotation=1;d.Width=compact?2560:3840;d.Height=compact?1440:2160;d.Scale=compact?100:150;
                d.X=x;d.Y=0;d.WorkX=x;d.WorkY=0;d.WorkWidth=d.Width;d.WorkHeight=d.Height;x+=d.Width;
            }
            return result;
        }
        public static Profile CompactProfile(Profile original) {
            var result=Store.Serializer().Deserialize<Profile>(Store.Serializer().Serialize(original));
            var primary=original.Displays.First(d=>d.X==0&&d.Y==0);
            foreach(var d in result.Displays) {
                if(d.Width==3840&&d.Height==2160){d.Width=2560;d.Height=1440;d.Scale=Math.Max(100,(int)Math.Round(d.Scale*(2.0/3)/25)*25);}
                else if(d.Width==2160&&d.Height==3840){d.Width=1440;d.Height=2560;d.Scale=Math.Max(100,(int)Math.Round(d.Scale*(2.0/3)/25)*25);}
            }
            var newPrimary=result.Displays.First(d=>d.Key==primary.Key);double ratio=(double)newPrimary.Width/primary.Width;
            foreach(var d in result.Displays) {
                var source=original.Displays.First(s=>s.Key==d.Key);
                d.X=(int)Math.Round(source.X*ratio);d.Y=(int)Math.Round(source.Y*ratio);
                if(source.X>=primary.Width)d.X=newPrimary.Width+(int)Math.Round((source.X-primary.Width)*ratio);
                else if(source.X+source.Width<=0)d.X=-d.Width+(int)Math.Round((source.X+source.Width)*ratio);
                if(source.Y>=primary.Height)d.Y=newPrimary.Height+(int)Math.Round((source.Y-primary.Height)*ratio);
                else if(source.Y+source.Height<=0)d.Y=-d.Height+(int)Math.Round((source.Y+source.Height)*ratio);
                d.WorkX=d.X;d.WorkY=d.Y;d.WorkWidth=d.Width;d.WorkHeight=d.Height;
            }
            return result;
        }
        public static Topology Build(Profile profile,bool allowMissing) {
            if(profile==null||profile.Displays.Count==0)throw new InvalidOperationException("복구할 화면 구성이 없습니다.");
            Topology all=Query(true);var paths=new List<Native.DisplayPath>();var modes=new List<Native.Mode>();var used=new HashSet<string>();
            foreach(DisplaySpec desired in profile.Displays) {
                var candidates=all.Paths.Where(p=>p.Target.Available!=0&&String.Equals(Describe(p,all.Modes).Key,desired.Key,StringComparison.OrdinalIgnoreCase)).OrderByDescending(p=>(p.Flags&1)!=0).ToList();
                bool found=false;
                foreach(var candidate in candidates) {
                    string sourceKey=candidate.Source.Adapter+":"+candidate.Source.Id;if(used.Contains(sourceKey))continue;
                    var path=candidate;path.Flags=1;path.Source.ModeIndex=(uint)modes.Count;path.Target.ModeIndex=UInt32.MaxValue;
                    path.Target.Rotation=desired.Rotation==0?1:desired.Rotation;path.Target.Refresh=new Native.Rational { Numerator=desired.RefreshNumerator,Denominator=desired.RefreshDenominator };
                    path.Target.Scanline=desired.RefreshNumerator==0?0u:1u;
                    modes.Add(new Native.Mode { Type=1,Id=path.Source.Id,Adapter=path.Source.Adapter,Width=(uint)desired.Width,Height=(uint)desired.Height,PixelFormat=4,X=desired.X,Y=desired.Y });
                    paths.Add(path);used.Add(sourceKey);found=true;break;
                }
                if(!found&&!allowMissing)throw new InvalidOperationException("화면을 찾거나 독립 출력 경로를 만들 수 없습니다: "+desired.Name);
            }
            if(paths.Count==0)throw new InvalidOperationException("복구 가능한 모니터가 연결되어 있지 않습니다.");
            if(allowMissing&&!modes.Any(m=>m.X==0&&m.Y==0)){int x=modes[0].X,y=modes[0].Y;for(int i=0;i<modes.Count;i++){var m=modes[i];m.X-=x;m.Y-=y;modes[i]=m;}}
            return new Topology { Paths=paths.ToArray(),Modes=modes.ToArray() };
        }
        public static int Validate(Profile profile) { var t=Build(profile,false);return Native.SetDisplayConfig((uint)t.Paths.Length,t.Paths,(uint)t.Modes.Length,t.Modes,0x40|0x20|0x400); }
        public static void Apply(Profile profile,bool allowMissing,bool persistLab=false) {
            if(persistLab&&profile.Displays.Any(d=>d.Virtual))throw new InvalidOperationException("가상 화면 구성은 부팅 기본값으로 저장하지 않습니다.");
            var t=Build(profile,allowMissing);
            Check(Native.SetDisplayConfig((uint)t.Paths.Length,t.Paths,(uint)t.Modes.Length,t.Modes,0x40|0x20|0x400),"Validate display profile");
            Check(Native.SetDisplayConfig((uint)t.Paths.Length,t.Paths,(uint)t.Modes.Length,t.Modes,0x80|0x20|0x400|(persistLab?0x200u:0u)),"Apply display profile");
            for(int i=0;i<30;i++){Thread.Sleep(150);if(Matches(profile,Capture(),!allowMissing))return;}
            throw new InvalidOperationException("변경 후 화면 구성이 요청한 값과 일치하지 않습니다.");
        }
        public static bool Matches(Profile desired,Profile actual,bool exact) {
            if(actual.Displays.Count==0)return false;
            if(exact&&desired.Displays.Count!=actual.Displays.Count)return false;
            foreach(var d in actual.Displays) {
                var p=desired.Displays.FirstOrDefault(x=>String.Equals(x.Key,d.Key,StringComparison.OrdinalIgnoreCase));if(p==null)return false;
                if(exact&&(d.Width!=p.Width||d.Height!=p.Height||d.X!=p.X||d.Y!=p.Y))return false;
                if(exact&&d.Rotation!=(p.Rotation==0?1:p.Rotation)&&d.Rotation!=0)return false;
            }
            return true;
        }
    }
}
