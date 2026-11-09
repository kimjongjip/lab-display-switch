using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Web.Script.Serialization;

namespace LabDisplaySwitch {
    public sealed class DisplaySpec {
        public string Key, Name, GdiName, Adapter, AdapterPath;
        public uint TargetId, SourceId, Rotation;
        public bool Active, Virtual;
        public int X, Y, Width, Height, Scale, WorkX, WorkY, WorkWidth, WorkHeight;
        public uint RefreshNumerator, RefreshDenominator;
    }
    public sealed class WindowSpec {
        public long Handle, ProcessStart; public uint Pid, Show,PlacementFlags; public string Class, DisplayKey;
        public int X, Y, Width, Height, OffsetX, OffsetY, Scale; public uint Dpi;
    }
    public sealed class Profile {
        public int Version=1; public string CreatedUtc; public List<DisplaySpec> Displays=new List<DisplaySpec>();
    }
    public sealed class Journal {
        public int Version=1; public string Id, Phase, StartedUtc, DeadlineUtc, LastError;
        public int OwnerPid; public long OwnerStart; public bool Confirmed,Compact;
        public Profile Before; public Profile After; public List<WindowSpec> Windows=new List<WindowSpec>();
    }
    public sealed class Settings {
        public int Version=1; public bool Tested,Compact,RemoteLandscape; public string LastMode="lab";
        public string RenderingGpu;
        public List<string> VirtualKeys=new List<string>();
    }
    internal static class Store {
        public static readonly string Root=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"LabDisplaySwitch");
        public static string PathFor(string name) { Directory.CreateDirectory(Root); return Path.Combine(Root,name); }
        public static JavaScriptSerializer Serializer() { return new JavaScriptSerializer { MaxJsonLength=16*1024*1024, RecursionLimit=100 }; }
        public static T Read<T>(string name) where T:class { string p=PathFor(name); if(!File.Exists(p))return null; using(var stream=new FileStream(p,FileMode.Open,FileAccess.Read,FileShare.ReadWrite|FileShare.Delete))using(var reader=new StreamReader(stream,Encoding.UTF8))return Serializer().Deserialize<T>(reader.ReadToEnd()); }
        public static void Write(string name, object value) {
            string path=PathFor(name), temp=path+"."+Guid.NewGuid().ToString("N")+".tmp";
            byte[] bytes=new UTF8Encoding(false).GetBytes(Serializer().Serialize(value));
            using(var stream=new FileStream(temp,FileMode.CreateNew,FileAccess.Write,FileShare.None)) { stream.Write(bytes,0,bytes.Length); stream.Flush(true); }
            if(File.Exists(path)) File.Replace(temp,path,path+".bak",true); else File.Move(temp,path);
        }
        public static void Log(string text) {
            try { string path=PathFor("events.log"); if(File.Exists(path)&&new FileInfo(path).Length>2*1024*1024)File.Move(path,PathFor("events-"+DateTime.UtcNow.ToString("yyyyMMddHHmmssfff")+".log")); File.AppendAllText(path,DateTime.UtcNow.ToString("o")+" "+text+Environment.NewLine,Encoding.UTF8); } catch { }
        }
        public static bool OwnerAlive(Journal j) { try { var p=Process.GetProcessById(j.OwnerPid); return !p.HasExited&&p.StartTime.ToUniversalTime().Ticks==j.OwnerStart; } catch{return false;} }
        public static string Exe { get { return Process.GetCurrentProcess().MainModule.FileName; } }
    }
}
