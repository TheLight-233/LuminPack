using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using LuminPack.Core;
using LuminPack.Utility;
using UnityEngine;
using Unity.Profiling;
using MemoryPack;
using Before=LuminPack.UnityBaseline.LuminUnity;
using After=LuminPack.Unity.LuminUnity;

public static unsafe class UnityOwnerAudit
{
 public static void Run(string directory)
 {
  Directory.CreateDirectory(directory);
  var control=StartAllocation();GC.KeepAlive(new byte[64]);if(FinishAllocation(ref control)==0)throw new Exception("Allocation counter positive control failed");
  MemoryPackFormatterProvider.Register(new AnimationCurveFormatter());MemoryPackFormatterProvider.Register(new GradientReference());MemoryPackFormatterProvider.Register(new RectOffsetFormatter());
  using var timing=new StreamWriter(Path.Combine(directory,"timing.csv"));using var alloc=new StreamWriter(Path.Combine(directory,"allocation.csv"));using var il=new StreamWriter(Path.Combine(directory,"il.csv"));
  timing.WriteLine("family,count,operation,sample,before_ns,after_ns,reference_ns,before_ratio,after_ratio");alloc.WriteLine("family,count,operation,before_allocations,after_allocations,reference_allocations");il.WriteLine("image,method,il_bytes,locals");
  foreach(var type in new[]{typeof(Before),typeof(After)})foreach(var method in type.GetMethods(System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.Static)){var body=method.GetMethodBody();if(body!=null)il.WriteLine(type.Namespace+",\""+method+"\","+body.GetILAsByteArray().Length+","+body.LocalVariables.Count);}
  foreach(int count in new[]{0,1,2,16,17,257})
  {
   var keys=new Keyframe[count];for(int i=0;i<count;i++)keys[i]=new Keyframe(i,i*0.25f,-2,3,0.125f,0.875f);
   var value=new AnimationCurve(keys){preWrapMode=WrapMode.Loop,postWrapMode=WrapMode.PingPong};
   new Case<AnimationCurve>("curve",count,value,new AnimationCurve(),new AnimationCurve(),&Before.Write,&After.Write,&Before.Read,&After.Read,&Before.Size,&After.Size,()=>new AnimationCurve()).Run(timing,alloc);
  }
  foreach(int count in new[]{2,4,8})
  {
   var c=new GradientColorKey[count];var a=new GradientAlphaKey[count];for(int i=0;i<count;i++){float t=(float)i/(count-1);c[i]=new GradientColorKey(new Color(t,0.25f,0.75f,1),t);a[i]=new GradientAlphaKey(t,t);}
   var value=new Gradient{mode=GradientMode.Fixed,colorSpace=ColorSpace.Linear};value.SetKeys(c,a);
   new Case<Gradient>("gradient",count,value,new Gradient(),new Gradient(),&Before.Write,&After.Write,&Before.Read,&After.Read,&Before.Size,&After.Size,()=>new Gradient()).Run(timing,alloc);
  }
  new Case<RectOffset>("offset",4,new RectOffset(-1,2,3,-4),new RectOffset(),new RectOffset(),&Before.Write,&After.Write,&Before.Read,&After.Read,&Before.Size,&After.Size,()=>new RectOffset()).Run(timing,alloc);
  Value<Vector2>("vector2",new Vector2(1.25f, -2.5f),&Before.Write,&After.Write,&Before.Read,&After.Read,timing,alloc);
  Value<Vector3>("vector3",new Vector3(1.25f, -2.5f, 3.75f),&Before.Write,&After.Write,&Before.Read,&After.Read,timing,alloc);
  Value<Vector4>("vector4",new Vector4(1.25f, -2.5f, 3.75f, -0f),&Before.Write,&After.Write,&Before.Read,&After.Read,timing,alloc);
  Value<Vector2Int>("vector2int",new Vector2Int(int.MinValue, int.MaxValue),&Before.Write,&After.Write,&Before.Read,&After.Read,timing,alloc);
  Value<Vector3Int>("vector3int",new Vector3Int(-1, 0, 1234567),&Before.Write,&After.Write,&Before.Read,&After.Read,timing,alloc);
  Value<Quaternion>("quaternion",new Quaternion(0.125f, -0.25f, 0.375f, 0.875f),&Before.Write,&After.Write,&Before.Read,&After.Read,timing,alloc);
  Value<Color>("color",new Color(0.125f, -1f, 2f, 0.75f),&Before.Write,&After.Write,&Before.Read,&After.Read,timing,alloc);
  Value<Color32>("color32",new Color32(0, 1, 127, 255),&Before.Write,&After.Write,&Before.Read,&After.Read,timing,alloc);
  Value<Rect>("rect",new Rect(-1.25f, 2.5f, -3.75f, 4.125f),&Before.Write,&After.Write,&Before.Read,&After.Read,timing,alloc);
  Value<RectInt>("rectint",new RectInt(-1, 2, -3, 4),&Before.Write,&After.Write,&Before.Read,&After.Read,timing,alloc);
  Value<Bounds>("bounds",new Bounds(new Vector3(1,2,3), new Vector3(5,7,9)),&Before.Write,&After.Write,&Before.Read,&After.Read,timing,alloc);
  Value<BoundsInt>("boundsint",new BoundsInt(new Vector3Int(1,2,3), new Vector3Int(-5,7,9)),&Before.Write,&After.Write,&Before.Read,&After.Read,timing,alloc);
  Value<Ray>("ray",new Ray(new Vector3(1,2,3), new Vector3(0.125f,0.375f,0.875f)),&Before.Write,&After.Write,&Before.Read,&After.Read,timing,alloc);
  Value<Ray2D>("ray2d",new Ray2D(new Vector2(1,2), new Vector2(0.125f,0.875f)),&Before.Write,&After.Write,&Before.Read,&After.Read,timing,alloc);
  Value<Plane>("plane",new Plane(new Vector3(0.125f,0.375f,0.875f), -1.25f),&Before.Write,&After.Write,&Before.Read,&After.Read,timing,alloc);
  Value<Matrix4x4>("matrix4x4",new Matrix4x4(new Vector4(1,2,3,4),new Vector4(5,6,7,8),new Vector4(9,10,11,12),new Vector4(13,14,15,16)),&Before.Write,&After.Write,&Before.Read,&After.Read,timing,alloc);
  Value<LayerMask>("layermask",(LayerMask)(-2147483647),&Before.Write,&After.Write,&Before.Read,&After.Read,timing,alloc);
  Value<Keyframe>("keyframe",new Keyframe(0.25f, -2f, 3f, -4f, 0.125f, 0.875f) { weightedMode = WeightedMode.Both },&Before.Write,&After.Write,&Before.Read,&After.Read,timing,alloc);
  Value<GradientColorKey>("gradientcolorkey",new GradientColorKey(new Color(1, 0.5f, 0.125f, 1), 0.375f),&Before.Write,&After.Write,&Before.Read,&After.Read,timing,alloc);
  Value<GradientAlphaKey>("gradientalphakey",new GradientAlphaKey(0.125f, 0.75f),&Before.Write,&After.Write,&Before.Read,&After.Read,timing,alloc);
  Value<RangeInt>("rangeint",new RangeInt(-3, 17),&Before.Write,&After.Write,&Before.Read,&After.Read,timing,alloc);
  NativeDump.Run(directory);
 }
 static void Value<T>(string name,T value,delegate*<ref LuminPackWriter,in T,void> write,delegate*<ref LuminPackWriter,in T,void> writeAfter,delegate*<ref LuminPackReader,ref T,void> before,delegate*<ref LuminPackReader,ref T,void> after,StreamWriter timing,StreamWriter allocation)where T:unmanaged
 {
  var output=new LuminBufferWriter(true);var writer=new LuminPackWriter(output);write(ref writer,value);var binary=writer.GetSpan().ToArray();var reference=MemoryPackSerializer.Serialize(value);var referenceOutput=new RetainedBuffer(1024);T sink=default;
  Func<int>[] actions={ReadBefore,ReadAfter,ReadReference};Measure(name,1,"value-read",actions,timing,allocation,true);
  int ReadBefore(){fixed(byte* address=binary){ReadOnlySpan<byte> input=new ReadOnlySpan<byte>(address,binary.Length);var reader=new LuminPackReader(ref input);before(ref reader,ref sink);return reader.Position;}}
  int ReadAfter(){fixed(byte* address=binary){ReadOnlySpan<byte> input=new ReadOnlySpan<byte>(address,binary.Length);var reader=new LuminPackReader(ref input);after(ref reader,ref sink);return reader.Position;}}
  int ReadReference(){return MemoryPackSerializer.Deserialize<T>(reference,ref sink);}
  Measure(name,1,"value-write",new Func<int>[]{WriteBefore,WriteAfter,WriteReference},timing,allocation,true);
  int WriteBefore(){output.ResetCore();var w=new LuminPackWriter(output);write(ref w,value);return w.Position;}
  int WriteAfter(){output.ResetCore();var w=new LuminPackWriter(output);writeAfter(ref w,value);return w.Position;}
  int WriteReference(){referenceOutput.Reset();MemoryPackSerializer.Serialize(referenceOutput,value);return referenceOutput.WrittenCount;}
  output.Dispose();
 }
 static void Measure(string name,int count,string operation,Func<int>[] actions,StreamWriter timing,StreamWriter allocation,bool timed)
 {
  int sink=0;foreach(var a in actions)for(int n=0;n<(timed?512:64);n++)sink^=a();
  var bytes=new double[3];for(int k=0;k<3;k++){for(int n=0;n<64;n++)sink^=actions[k]();var recording=StartAllocation();for(int n=0;n<128;n++)sink^=actions[k]();bytes[k]=FinishAllocation(ref recording)/128.0;}
  allocation.WriteLine($"{name},{count},{operation},{bytes[0]},{bytes[1]},{bytes[2]}");allocation.Flush();
  if(timed){for(int sample=0;sample<24;sample++){var ns=new double[3];for(int order=0;order<3;order++){int k=(sample+order)%3;long start=Stopwatch.GetTimestamp();for(int n=0;n<4096;n++)sink^=actions[k]();ns[k]=(Stopwatch.GetTimestamp()-start)*1e9/Stopwatch.Frequency/4096;}timing.WriteLine($"{name},{count},{operation},{sample},{ns[0]:F4},{ns[1]:F4},{ns[2]:F4},{ns[0]/ns[2]:F6},{ns[1]/ns[2]:F6}");}timing.Flush();}
  GC.KeepAlive(sink);
 }
 sealed class Case<T>where T:class
 {
  readonly string name;readonly int count;readonly T value;T beforeValue,afterValue,referenceValue;readonly Func<T> create;
  readonly delegate*<ref LuminPackWriter,in T,void> writeBefore,writeAfter;readonly delegate*<ref LuminPackReader,ref T,void> readBefore,readAfter;readonly delegate*<ref LuminPackEvaluator,in T,void> sizeBefore,sizeAfter;
  readonly LuminBufferWriter output=new LuminBufferWriter(true);readonly RetainedBuffer referenceOutput=new RetainedBuffer(65536);byte[] binary,reference;
  public Case(string name,int count,T value,T beforeValue,T afterValue,delegate*<ref LuminPackWriter,in T,void> writeBefore,delegate*<ref LuminPackWriter,in T,void> writeAfter,delegate*<ref LuminPackReader,ref T,void> readBefore,delegate*<ref LuminPackReader,ref T,void> readAfter,delegate*<ref LuminPackEvaluator,in T,void> sizeBefore,delegate*<ref LuminPackEvaluator,in T,void> sizeAfter,Func<T> create)
  {this.name=name;this.count=count;this.value=value;this.beforeValue=beforeValue;this.afterValue=afterValue;referenceValue=create();this.writeBefore=writeBefore;this.writeAfter=writeAfter;this.readBefore=readBefore;this.readAfter=readAfter;this.sizeBefore=sizeBefore;this.sizeAfter=sizeAfter;this.create=create;}
  public void Run(StreamWriter timing,StreamWriter allocation)
  {
   int length=WriteBefore();binary=output.GetFullSpan().Slice(0,length).ToArray();int afterLength=WriteAfter();if(!output.GetFullSpan().Slice(0,afterLength).SequenceEqual(binary))throw new Exception("Wire mismatch");WriteReference();reference=referenceOutput.WrittenSpan.ToArray();ReadBefore();ReadAfter();ReadReference();Check(beforeValue);Check(afterValue);Check(referenceValue);
   Measure(name,count,"write",new Func<int>[]{WriteBefore,WriteAfter,WriteReference},timing,allocation,true);
   Measure(name,count,"read-reuse",new Func<int>[]{ReadBefore,ReadAfter,ReadReference},timing,allocation,true);
   Measure(name,count,"read-fresh",new Func<int>[]{ReadFreshBefore,ReadFreshAfter,ReadFreshReference},timing,allocation,false);
   Measure(name,count,"size",new Func<int>[]{SizeBefore,SizeAfter,Construct},timing,allocation,false);
   output.Dispose();
  }
  void Check(T result){output.ResetCore();var writer=new LuminPackWriter(output);writeBefore(ref writer,result);if(!writer.GetSpan().SequenceEqual(binary))throw new Exception(name+" reference or candidate values differ");}
  int WriteBefore(){output.ResetCore();var writer=new LuminPackWriter(output);writeBefore(ref writer,value);return writer.Position;}
  int WriteAfter(){output.ResetCore();var writer=new LuminPackWriter(output);writeAfter(ref writer,value);return writer.Position;}
  int WriteReference(){referenceOutput.Reset();MemoryPackSerializer.Serialize(referenceOutput,value);return referenceOutput.WrittenCount;}
  int ReadBefore(){fixed(byte* address=binary){ReadOnlySpan<byte> input=new ReadOnlySpan<byte>(address,binary.Length);var reader=new LuminPackReader(ref input);readBefore(ref reader,ref beforeValue);return reader.Position;}}
  int ReadAfter(){fixed(byte* address=binary){ReadOnlySpan<byte> input=new ReadOnlySpan<byte>(address,binary.Length);var reader=new LuminPackReader(ref input);readAfter(ref reader,ref afterValue);return reader.Position;}}
  int ReadReference(){return MemoryPackSerializer.Deserialize(reference,ref referenceValue);}
  int ReadFreshBefore(){fixed(byte* address=binary){T result=null;ReadOnlySpan<byte> input=new ReadOnlySpan<byte>(address,binary.Length);var reader=new LuminPackReader(ref input);readBefore(ref reader,ref result);GC.KeepAlive(result);return reader.Position;}}
  int ReadFreshAfter(){fixed(byte* address=binary){T result=null;ReadOnlySpan<byte> input=new ReadOnlySpan<byte>(address,binary.Length);var reader=new LuminPackReader(ref input);readAfter(ref reader,ref result);GC.KeepAlive(result);return reader.Position;}}
  int ReadFreshReference(){T result=null;int bytes=MemoryPackSerializer.Deserialize(reference,ref result);GC.KeepAlive(result);return bytes;}
  int SizeBefore(){var evaluator=new LuminPackEvaluator();sizeBefore(ref evaluator,value);return evaluator.Value;}
  int SizeAfter(){var evaluator=new LuminPackEvaluator();sizeAfter(ref evaluator,value);return evaluator.Value;}
  int Construct(){GC.KeepAlive(create());return 1;}
 }
 static ProfilerRecorder StartAllocation(){var r=ProfilerRecorder.StartNew(ProfilerCategory.Internal,"GC.Alloc",8192,ProfilerRecorderOptions.CollectOnlyOnCurrentThread);if(!r.Valid)throw new Exception("No allocation recorder");return r;}
 static long FinishAllocation(ref ProfilerRecorder r){r.Stop();try{if(r.WrappedAround)throw new Exception("Allocation samples overflowed");return r.Count;}finally{r.Dispose();}}
 sealed class RetainedBuffer:IBufferWriter<byte>
 {
  byte[] buffer;int count;
  public RetainedBuffer(int capacity){buffer=new byte[capacity];}
  public int WrittenCount=>count;
  public ReadOnlySpan<byte> WrittenSpan=>buffer.AsSpan(0,count);
  public void Reset(){count=0;}
  public void Advance(int length){count+=length;}
  public Memory<byte> GetMemory(int sizeHint=0){Ensure(sizeHint);return buffer.AsMemory(count);}
  public Span<byte> GetSpan(int sizeHint=0){Ensure(sizeHint);return buffer.AsSpan(count);}
  void Ensure(int sizeHint){if(buffer.Length-count<Math.Max(1,sizeHint))Array.Resize(ref buffer,Math.Max(buffer.Length*2,count+sizeHint));}
 }
 sealed class GradientReference:MemoryPackFormatter<Gradient>
 {
  readonly GradientFormatter inner=new GradientFormatter();
  public override void Serialize<TBufferWriter>(ref MemoryPackWriter<TBufferWriter> writer,ref Gradient value){inner.Serialize(ref writer,ref value);if(value!=null)writer.WriteUnmanaged(value.colorSpace);}
  public override void Deserialize(ref MemoryPackReader reader,ref Gradient value){inner.Deserialize(ref reader,ref value);if(value!=null){reader.ReadUnmanaged(out ColorSpace space);value.colorSpace=space;}}
 }
}
