using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using UnityEngine;
internal static class NativeDump
{
 [DllImport("mono-2.0-bdwgc",CallingConvention=CallingConvention.Cdecl)] static extern IntPtr mono_domain_get();
 [DllImport("mono-2.0-bdwgc",CallingConvention=CallingConvention.Cdecl)] static extern IntPtr mono_jit_info_table_find(IntPtr domain,IntPtr address);
 [DllImport("mono-2.0-bdwgc",CallingConvention=CallingConvention.Cdecl)] static extern IntPtr mono_jit_info_get_code_start(IntPtr info);
 [DllImport("mono-2.0-bdwgc",CallingConvention=CallingConvention.Cdecl)] static extern int mono_jit_info_get_code_size(IntPtr info);
 public static void Run(string directory)
 {
  using var summary=new StreamWriter(Path.Combine(directory,"native.csv"));summary.WriteLine("image,type,operation,native_bytes,address");
  foreach(var image in new[]{typeof(LuminPack.UnityBaseline.LuminUnity),typeof(LuminPack.Unity.LuminUnity)})
  foreach(var value in new[]{typeof(Vector3),typeof(Matrix4x4),typeof(Keyframe)})
  foreach(string operation in new[]{"Read","Write"})
  {
   var method=image.GetMethod(operation,BindingFlags.Public|BindingFlags.Static,null,new[]{(operation=="Read"?typeof(LuminPack.Core.LuminPackReader):typeof(LuminPack.Core.LuminPackWriter)).MakeByRefType(),value.MakeByRefType()},null);
   IntPtr pointer=method.MethodHandle.GetFunctionPointer();IntPtr info=mono_jit_info_table_find(mono_domain_get(),pointer);
   if(info==IntPtr.Zero)throw new Exception("No JIT metadata for "+method);
   int size=mono_jit_info_get_code_size(info);IntPtr start=mono_jit_info_get_code_start(info);byte[] bytes=new byte[size];Marshal.Copy(start,bytes,0,size);
   File.WriteAllBytes(Path.Combine(directory,image.Namespace+"."+value.Name+"."+operation+".bin"),bytes);summary.WriteLine(image.Namespace+","+value.Name+","+operation+","+size+","+start.ToInt64());
  }
 }
}
