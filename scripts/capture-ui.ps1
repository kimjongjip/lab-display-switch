param([string]$OutputPath)
$ErrorActionPreference='Stop'
if(-not $OutputPath){$OutputPath=Join-Path (Split-Path $PSScriptRoot -Parent) 'artifacts\ui-final.png'}
Add-Type -TypeDefinition @'
using System;
using System.Drawing;
using System.Runtime.InteropServices;
public static class AppWindowCapture {
 [StructLayout(LayoutKind.Sequential)] public struct Rect { public int Left,Top,Right,Bottom; }
 [DllImport("user32.dll")] static extern IntPtr SetThreadDpiAwarenessContext(IntPtr context);
 [DllImport("user32.dll")] static extern IntPtr FindWindow(string cls,string title);
 [DllImport("user32.dll")] static extern bool ShowWindow(IntPtr hwnd,int cmd);
 [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr hwnd,out Rect rect);
 [DllImport("user32.dll")] static extern bool PrintWindow(IntPtr hwnd,IntPtr hdc,uint flags);
 public static void Capture(string path) {
  var previous=SetThreadDpiAwarenessContext(new IntPtr(-4));
  try { var hwnd=FindWindow(null,"Lab Display Switch");if(hwnd==IntPtr.Zero)throw new Exception("App window not found");
   ShowWindow(hwnd,1);Rect r;if(!GetWindowRect(hwnd,out r))throw new Exception("Window bounds unavailable");
   using(var b=new Bitmap(r.Right-r.Left,r.Bottom-r.Top))using(var g=Graphics.FromImage(b)) {
    var dc=g.GetHdc();try{if(!PrintWindow(hwnd,dc,2))throw new Exception("Capture failed");}finally{g.ReleaseHdc(dc);}
    b.Save(path,System.Drawing.Imaging.ImageFormat.Png);
   }
  } finally { SetThreadDpiAwarenessContext(previous); }
 }
}
'@ -ReferencedAssemblies System.Drawing
[AppWindowCapture]::Capture($OutputPath)
Write-Output $OutputPath
