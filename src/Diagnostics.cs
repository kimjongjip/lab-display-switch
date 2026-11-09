using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;

namespace LabDisplaySwitch {
    internal static class Diagnostics {
        public static object CaptureTiming() {
            var profile=Displays.Capture();var results=new List<object>();
            foreach(var screen in profile.Displays) {
                var samples=new List<double>();
                // Benchmark readback only. Pixel data stays in memory and is never saved.
                using(var bitmap=new Bitmap(screen.Width,screen.Height,PixelFormat.Format32bppRgb))
                using(var graphics=Graphics.FromImage(bitmap)) {
                    for(int i=0;i<9;i++) {
                        var watch=Stopwatch.StartNew();graphics.CopyFromScreen(screen.X,screen.Y,0,0,new Size(screen.Width,screen.Height),CopyPixelOperation.SourceCopy);watch.Stop();
                        if(i>0)samples.Add(watch.Elapsed.TotalMilliseconds);
                    }
                }
                samples.Sort();results.Add(new { Display=screen.GdiName,screen.Width,screen.Height,MedianMilliseconds=Math.Round(samples[samples.Count/2],2),MaxMilliseconds=Math.Round(samples.Max(),2) });
            }
            return new { Kind="GDI readback only; not end-to-end CRD latency",Displays=results };
        }
    }
}
