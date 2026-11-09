using System;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;

namespace LabDisplaySwitch {
    internal static class Dpi {
        private static readonly int[] Scales={100,125,150,175,200,225,250,300,350,400,450,500};
        // Windows has no documented public per-monitor DPI setter. These guarded
        // device-info requests are also used by DisplayConfig/SetDPI. Restrict all
        // writes to our virtual monitors and verify both before and after writing.
        public static int Relative(int desired,int actual,int minimum,int current,int maximum) {
            if(minimum>0||minimum<=-Scales.Length||maximum<0||maximum>=Scales.Length+minimum||current<=-Scales.Length||current>maximum)throw new InvalidOperationException("Windows 배율 범위 응답이 올바르지 않습니다.");
            // A resolution change can leave the previous relative setting below
            // the new minimum. Accept it only if Windows actually reports the
            // clamped 100% scale; writes still use the new supported range.
            int recommended=-minimum,index=Array.IndexOf(Scales,desired),currentIndex=recommended+Math.Max(current,minimum);
            if(minimum>0||recommended>=Scales.Length||index<0||currentIndex<0||currentIndex>=Scales.Length||Scales[currentIndex]!=actual)throw new InvalidOperationException("Windows 배율 응답을 안전하게 해석할 수 없습니다. 디스플레이 설정에서 가상 화면 배율을 맞춰주세요.");
            int relative=index-recommended;if(relative<minimum||relative>maximum)throw new InvalidOperationException("요청한 가상 화면 배율을 지원하지 않습니다.");return relative;
        }
        public static void MatchVirtual(Profile desired) {
            foreach(var target in desired.Displays) {
                var actual=Displays.Capture().Displays.FirstOrDefault(d=>String.Equals(d.Key,target.Key,StringComparison.OrdinalIgnoreCase));
                if(actual==null||!actual.Virtual||!target.Virtual)throw new InvalidOperationException("배율 자동 조정은 가상 화면에만 적용할 수 있습니다.");
                if(actual.Scale==target.Scale)continue;
                var topology=Displays.Query(false);
                var path=topology.Paths.First(p=>String.Equals(Displays.Describe(p,topology.Modes).Key,target.Key,StringComparison.OrdinalIgnoreCase));
                var get=new Native.DpiGet { Header=new Native.Header { Type=unchecked((uint)-3),Size=(uint)Marshal.SizeOf(typeof(Native.DpiGet)),Adapter=path.Source.Adapter,Id=path.Source.Id } };
                Displays.Check(Native.GetDpiScale(ref get),"Read virtual display DPI");
                Store.Log("DPI request: actual="+actual.Scale+", desired="+target.Scale+", minimum="+get.Minimum+", current="+get.Current+", maximum="+get.Maximum);
                int relative=Relative(target.Scale,actual.Scale,get.Minimum,get.Current,get.Maximum);
                var set=new Native.DpiSet { Header=new Native.Header { Type=unchecked((uint)-4),Size=(uint)Marshal.SizeOf(typeof(Native.DpiSet)),Adapter=path.Source.Adapter,Id=path.Source.Id },Relative=relative };
                Displays.Check(Native.SetDpiScale(ref set),"Set virtual display DPI");
                bool matched=false;for(int i=0;i<20;i++){Thread.Sleep(100);var now=Displays.Capture().Displays.FirstOrDefault(d=>d.Key==target.Key);if(now!=null&&now.Scale==target.Scale){matched=true;break;}}
                if(!matched)throw new InvalidOperationException("가상 화면 배율 확인에 실패했습니다. 원래 화면으로 복귀합니다.");
                Store.Log("Virtual display scale matched: "+actual.Scale+" -> "+target.Scale);
            }
        }
    }
}
