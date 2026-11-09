using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Management;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;
using System.Threading;
using Microsoft.Win32;

namespace LabDisplaySwitch {
    internal static class Installer {
        [StructLayout(LayoutKind.Sequential)] private struct DeviceInfo { public uint Size; public Guid Class; public uint Device; public UIntPtr Reserved; }
        [DllImport("setupapi.dll",SetLastError=true)] private static extern IntPtr SetupDiCreateDeviceInfoList(ref Guid cls,IntPtr parent);
        [DllImport("setupapi.dll",CharSet=CharSet.Unicode,SetLastError=true)] private static extern bool SetupDiCreateDeviceInfo(IntPtr set,string name,ref Guid cls,string description,IntPtr parent,uint flags,ref DeviceInfo info);
        [DllImport("setupapi.dll",CharSet=CharSet.Unicode,SetLastError=true)] private static extern bool SetupDiSetDeviceRegistryProperty(IntPtr set,ref DeviceInfo info,uint property,byte[] value,uint size);
        [DllImport("setupapi.dll",SetLastError=true)] private static extern bool SetupDiCallClassInstaller(uint action,IntPtr set,ref DeviceInfo info);
        [DllImport("setupapi.dll")] private static extern bool SetupDiDestroyDeviceInfoList(IntPtr set);
        [DllImport("newdev.dll",CharSet=CharSet.Unicode,SetLastError=true)] private static extern bool UpdateDriverForPlugAndPlayDevices(IntPtr parent,string hardwareId,string inf,uint flags,out bool reboot);
        public static bool IsAdmin { get { return new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator); } }
        public static string Package { get { return Path.Combine(Path.GetDirectoryName(Store.Exe),"vdd"); } }
        public static void VerifyPackage() {
            string[] files={"MttVDD.inf","MttVDD.dll","mttvdd.cat"};
            string[] hashes={"6C3DC69C34D968001F97414FB9DBC035C82566B0AD41F7E51557F91EF1D9252A","347DE404BB09E025DBA9C069237446462B8644989FF14AEB51565213241F682B","2797CE1D6633DC9F94752AAA85A0FB3D4A2697CA6F3BF0777E22999E91ECC9AA"};
            for(int i=0;i<files.Length;i++)using(var stream=File.OpenRead(Path.Combine(Package,files[i])))using(var sha=SHA256.Create()){
                if(BitConverter.ToString(sha.ComputeHash(stream)).Replace("-","")!=hashes[i])throw new InvalidOperationException("검증된 드라이버 파일과 다릅니다: "+files[i]);
            }
        }
        public static bool AlreadyInstalled() {
            using(var query=new ManagementObjectSearcher("SELECT HardwareID FROM Win32_PnPEntity WHERE PNPClass = 'Display'"))
            foreach(ManagementObject item in query.Get()) { var ids=item["HardwareID"] as string[];if(ids!=null&&ids.Any(s=>s.Equals("Root\\MttVDD",StringComparison.OrdinalIgnoreCase)))return true; }
            return false;
        }
        public static string CursorSettingsPath {
            get {
                string expected=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),"LabDisplaySwitch","VDD");
                using(var key=Registry.LocalMachine.OpenSubKey("SOFTWARE\\MikeTheTech\\VirtualDisplayDriver")) {
                    string configured=key==null?null:key.GetValue("VDDPATH") as string;
                    if(String.IsNullOrEmpty(configured)||!String.Equals(Path.GetFullPath(configured).TrimEnd('\\'),expected,StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("이 앱이 설치한 드라이버 설정 경로가 아닙니다.");
                }
                return Path.Combine(expected,"vdd_settings.xml");
            }
        }
        public static object PreviewCursorFix() {
            string original=File.ReadAllText(CursorSettingsPath),updated=CursorConfiguration.Complete(original),preview=Store.PathFor("cursor-settings-proposed.xml");
            File.WriteAllText(preview,updated,new UTF8Encoding(false));return new { Changed=original!=updated,OriginalPath=CursorSettingsPath,PreviewPath=preview,DriverRestarted=false };
        }
        public static object FixCursorConfiguration() {
            if(!IsAdmin)throw new InvalidOperationException("보호된 드라이버 설정 파일을 수정하려면 Windows 관리자 확인이 필요합니다.");
            string path=CursorSettingsPath,original=File.ReadAllText(path),updated=CursorConfiguration.Complete(original);
            if(original==updated)return new { Success=true,Changed=false,SettingsPath=path,BackupPath=(string)null,DriverRestarted=false,NeedsDriverRestart=true };
            string backup=path+".before-cursor-"+DateTime.UtcNow.ToString("yyyyMMddHHmmssfff")+".bak",temp=path+".cursor-"+Guid.NewGuid().ToString("N")+".tmp";
            try{File.WriteAllText(temp,updated,new UTF8Encoding(false));File.Replace(temp,path,backup);}finally{if(File.Exists(temp))File.Delete(temp);}
            return new { Success=true,Changed=true,SettingsPath=path,BackupPath=backup,DriverRestarted=false,NeedsDriverRestart=true };
        }
        public static void Install() {
            if(!IsAdmin)throw new InvalidOperationException("가상 화면 드라이버 설치에는 Windows 관리자 권한이 필요합니다.");
            VerifyPackage();if(AlreadyInstalled())throw new InvalidOperationException("Virtual Display Driver가 이미 설치되어 있습니다. 기존 설정을 자동으로 덮어쓰지 않았습니다.");
            var lab=Store.Read<Profile>("lab-profile.json");if(lab==null)throw new InvalidOperationException("연구실 화면을 먼저 저장하세요.");
            var settings=Store.Read<Settings>("settings.json")??new Settings();
            string gpu=settings.RenderingGpu;
            bool gpuFound=false;
            using(var query=new ManagementObjectSearcher("SELECT Name, PNPDeviceID FROM Win32_VideoController"))
            using(var devices=query.Get())foreach(ManagementObject gpuDevice in devices)using(gpuDevice) {
                string id=gpuDevice["PNPDeviceID"] as string;
                if(!String.IsNullOrWhiteSpace(gpu)&&String.Equals(gpuDevice["Name"] as string,gpu,StringComparison.OrdinalIgnoreCase)&&id!=null&&id.StartsWith("PCI\\",StringComparison.OrdinalIgnoreCase))gpuFound=true;
            }
            if(!gpuFound)throw new InvalidOperationException("대상 PC의 실제 GPU를 먼저 선택하세요: scripts/configure-machine.ps1 -GpuName 'GPU 이름'. 기존 PC의 GPU 이름을 복사하지 마세요.");
            string settingsDirectory=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),"LabDisplaySwitch","VDD");
            Directory.CreateDirectory(settingsDirectory);
            File.WriteAllText(Path.Combine(settingsDirectory,"vdd_settings.xml"),"<?xml version=\"1.0\"?><vdd_settings><monitors><count>2</count></monitors><gpu><friendlyname>"+System.Security.SecurityElement.Escape(gpu)+"</friendlyname></gpu><global><g_refresh_rate>60</g_refresh_rate></global><resolutions><resolution><width>1920</width><height>1080</height><refresh_rate>60</refresh_rate></resolution><resolution><width>2560</width><height>1440</height><refresh_rate>60</refresh_rate></resolution><resolution><width>3840</width><height>2160</height><refresh_rate>60</refresh_rate></resolution></resolutions><logging><logging>false</logging><debuglogging>false</debuglogging></logging><colour><SDR10bit>false</SDR10bit><HDRPlus>false</HDRPlus><ColourFormat>RGB</ColourFormat></colour><cursor><HardwareCursor>true</HardwareCursor></cursor></vdd_settings>",new UTF8Encoding(false));
            string settingsPath=Path.Combine(settingsDirectory,"vdd_settings.xml");File.WriteAllText(settingsPath,CursorConfiguration.Complete(File.ReadAllText(settingsPath)),new UTF8Encoding(false));
            using(var key=Registry.LocalMachine.CreateSubKey("SOFTWARE\\MikeTheTech\\VirtualDisplayDriver")) { if(key.GetValue("VDDPATH")!=null)Store.Write("previous-vdd-path.json",new { Path=key.GetValue("VDDPATH") }); key.SetValue("VDDPATH",settingsDirectory,RegistryValueKind.String); }
            Controller.EnsureGuard();Journal journal=Controller.Arm(lab,120,"installing");
            Guid cls=new Guid("4D36E968-E325-11CE-BFC1-08002BE10318");IntPtr set=SetupDiCreateDeviceInfoList(ref cls,IntPtr.Zero);if(set==new IntPtr(-1))throw new Win32Exception();
            var device=new DeviceInfo { Size=(uint)Marshal.SizeOf(typeof(DeviceInfo)) };bool registered=false;
            try {
                if(!SetupDiCreateDeviceInfo(set,"Display",ref cls,"LabDisplaySwitch virtual displays",IntPtr.Zero,1,ref device))throw new Win32Exception();
                byte[] ids=Encoding.Unicode.GetBytes("Root\\MttVDD\0\0");
                if(!SetupDiSetDeviceRegistryProperty(set,ref device,1,ids,(uint)ids.Length))throw new Win32Exception();
                if(!SetupDiCallClassInstaller(0x19,set,ref device))throw new Win32Exception();registered=true;
                bool reboot;
                if(!UpdateDriverForPlugAndPlayDevices(IntPtr.Zero,"Root\\MttVDD",Path.Combine(Package,"MttVDD.inf"),1,out reboot))throw new Win32Exception();
                bool found=false;for(int i=0;i<40;i++){Thread.Sleep(500);if(Displays.Inventory(true).Count(d=>d.Virtual&&d.Name.IndexOf("Parsec",StringComparison.OrdinalIgnoreCase)<0)>=2){found=true;break;}}
                if(!found)throw new InvalidOperationException("드라이버 설치 후 가상 화면 두 개를 감지하지 못했습니다. 재부팅 필요: "+reboot);
                Controller.RestoreLab();Store.Write("driver-install-result.json",new { Success=true,RebootRequired=reboot,Time=DateTime.UtcNow.ToString("o") });
            }catch(Exception e) {
                if(registered)SetupDiCallClassInstaller(5,set,ref device);
                try{Controller.RestoreLab();}catch{}
                journal.LastError=e.Message;Store.Write("driver-install-result.json",new { Success=false,Error=e.Message,Time=DateTime.UtcNow.ToString("o") });throw;
            }finally{SetupDiDestroyDeviceInfoList(set);}
        }
    }
}
