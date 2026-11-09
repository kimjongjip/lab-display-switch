using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace LabDisplaySwitch {
    internal static class DxgiDiagnostics {
        [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)] private struct AdapterDesc {
            [MarshalAs(UnmanagedType.ByValTStr,SizeConst=128)] public string Name;
            public uint Vendor,Device,Subsystem,Revision;public UIntPtr VideoMemory,SystemMemory,SharedMemory;public Native.Luid Luid;public uint Flags;
        }
        [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)] private struct OutputDesc {
            [MarshalAs(UnmanagedType.ByValTStr,SizeConst=32)] public string Name;
            public Native.Rect Bounds;public int Attached;public uint Rotation;public IntPtr Monitor;
        }
        [StructLayout(LayoutKind.Sequential)] internal struct FrameInfo {
            public long LastPresentTime,LastMouseUpdateTime;
            public uint AccumulatedFrames;public int RectsCoalesced,ProtectedContentMaskedOut;
            public int PointerX,PointerY,PointerVisible;public uint TotalMetadataBufferSize,PointerShapeBufferSize;
        }
        [StructLayout(LayoutKind.Sequential)] private struct CursorInfo { public uint Size,Flags;public IntPtr Cursor;public int X,Y; }
        [DllImport("user32.dll")] private static extern bool GetCursorInfo(ref CursorInfo info);
        [DllImport("dxgi.dll")] private static extern int CreateDXGIFactory1(ref Guid id,out IntPtr factory);
        [DllImport("d3d11.dll")] private static extern int D3D11CreateDevice(IntPtr adapter,uint type,IntPtr software,uint flags,IntPtr levels,uint count,uint version,out IntPtr device,out uint level,out IntPtr context);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int Enumerate(IntPtr self,uint index,out IntPtr item);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int AdapterDescription(IntPtr self,out AdapterDesc desc);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int OutputDescription(IntPtr self,out OutputDesc desc);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int Duplicate(IntPtr self,IntPtr device,out IntPtr duplication);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int AcquireFrame(IntPtr self,uint timeout,out FrameInfo info,out IntPtr resource);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int ReleaseFrame(IntPtr self);
        private static T Method<T>(IntPtr pointer,int slot) where T:class { return Marshal.GetDelegateForFunctionPointer(Marshal.ReadIntPtr(Marshal.ReadIntPtr(pointer),slot*IntPtr.Size),typeof(T)) as T; }
        private static string Hex(int value) { return "0x"+unchecked((uint)value).ToString("X8"); }
        private static object ReadCursorMetadata(IntPtr duplication,OutputDesc output) {
            int frames=0,pointerUpdates=0,separatePointerFrames=0,shapeUpdates=0;string error=null;
            for(int i=0;i<20;i++) {
                FrameInfo info;IntPtr resource=IntPtr.Zero;
                int hr=Method<AcquireFrame>(duplication,8)(duplication,75,out info,out resource);
                if(hr<0) {
                    if(resource!=IntPtr.Zero)Marshal.Release(resource);
                    if(unchecked((uint)hr)==0x887A0027u)continue;
                    error=Hex(hr);break;
                }
                try {
                    // Inspect frame metadata only. Do not map, copy, or save the
                    // desktop texture or the pointer image.
                    frames++;if(info.LastMouseUpdateTime!=0)pointerUpdates++;
                    if(info.PointerVisible!=0)separatePointerFrames++;
                    if(info.PointerShapeBufferSize>0)shapeUpdates++;
                }finally {
                    if(resource!=IntPtr.Zero)Marshal.Release(resource);
                    int released=Method<ReleaseFrame>(duplication,14)(duplication);if(released<0)error=Hex(released);
                }
                if(error!=null)break;
            }
            var cursor=new CursorInfo { Size=(uint)Marshal.SizeOf(typeof(CursorInfo)) };bool available=GetCursorInfo(ref cursor);
            return new { Frames=frames,PointerUpdates=pointerUpdates,SeparatePointerFrames=separatePointerFrames,ShapeUpdates=shapeUpdates,SystemCursorInfoAvailable=available,SystemCursorVisible=available&&(cursor.Flags&1)!=0,PointerWithinOutput=available&&cursor.X>=output.Bounds.Left&&cursor.X<output.Bounds.Right&&cursor.Y>=output.Bounds.Top&&cursor.Y<output.Bounds.Bottom,Error=error };
        }
        public static object Inspect(bool cursorMetadata=false) {
            Guid factoryId=new Guid("770aae78-f26f-4dba-a829-253c83d1b387");IntPtr factory;
            int result=CreateDXGIFactory1(ref factoryId,out factory);if(result<0)Marshal.ThrowExceptionForHR(result);
            var adapters=new List<object>();
            try {
                for(uint i=0;i<16;i++) {
                    IntPtr adapter;if(Method<Enumerate>(factory,12)(factory,i,out adapter)<0)break;
                    IntPtr device=IntPtr.Zero,context=IntPtr.Zero;
                    try {
                        AdapterDesc desc;Method<AdapterDescription>(adapter,10)(adapter,out desc);
                        uint level;int deviceResult=D3D11CreateDevice(adapter,0,IntPtr.Zero,0,IntPtr.Zero,0,7,out device,out level,out context);
                        var outputs=new List<object>();
                        for(uint n=0;n<16;n++) {
                            IntPtr output;if(Method<Enumerate>(adapter,7)(adapter,n,out output)<0)break;
                            IntPtr output1=IntPtr.Zero,duplication=IntPtr.Zero;
                            try {
                                OutputDesc info;Method<OutputDescription>(output,7)(output,out info);string duplicateResult="not attempted";object cursor=null;
                                if(info.Attached!=0&&deviceResult>=0) {
                                    Guid id=new Guid("00cddea8-939b-4b83-a340-a685226666cc");int hr=Marshal.QueryInterface(output,ref id,out output1);
                                    if(hr>=0)hr=Method<Duplicate>(output1,22)(output1,device,out duplication);duplicateResult=Hex(hr);
                                    if(hr>=0&&cursorMetadata)cursor=ReadCursorMetadata(duplication,info);
                                }
                                outputs.Add(new { Display=info.Name,Attached=info.Attached!=0,info.Bounds,DesktopDuplication=duplicateResult,Cursor=cursor });
                            }finally{if(duplication!=IntPtr.Zero)Marshal.Release(duplication);if(output1!=IntPtr.Zero)Marshal.Release(output1);Marshal.Release(output);}
                        }
                        adapters.Add(new { Adapter=desc.Name,Software=(desc.Flags&2)!=0,D3D11=Hex(deviceResult),Outputs=outputs });
                    }finally{if(context!=IntPtr.Zero)Marshal.Release(context);if(device!=IntPtr.Zero)Marshal.Release(device);Marshal.Release(adapter);}
                }
            }finally{Marshal.Release(factory);}
            return new { Adapters=adapters };
        }
    }
}
