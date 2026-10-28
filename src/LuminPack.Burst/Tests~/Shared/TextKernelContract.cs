using System;
using System.Buffers;
using System.Text;
using LuminPack.Utility;
namespace LuminPack.Burst.Tests
{
/// <summary>Checks native text kernels against the prebuilt Core codec with complete output canaries.</summary>
public static unsafe class TextKernelContract
{
/// <summary>Checks complete ordinary equality across every mismatch position and unaligned range.</summary><param name="equal">Native byte comparator.</param><returns>The checked operation count.</returns>
public static int VerifyBytes(BurstBackend.EqualFunction equal)
{
 int checks=0;var random=new Random(137);
 for(int length=0;length<=513;length++)for(int alignment=0;alignment<4;alignment++)
 {
  byte[] left=new byte[length+8],right=new byte[length+8];random.NextBytes(left);Array.Copy(left,right,left.Length);
  fixed(byte* a=left)fixed(byte* b=right)
  {
   if(equal(a+alignment,b+alignment,(uint)length)!=1)global::LuminPack.Code.LuminPackExceptionHelper.ThrowInvalidOperationException("Equal bytes rejected.");checks++;
   for(int i=0;i<length;i++){right[alignment+i]^=1;if(equal(a+alignment,b+alignment,(uint)length)!=0)global::LuminPack.Code.LuminPackExceptionHelper.ThrowInvalidOperationException("Byte mismatch accepted.");right[alignment+i]^=1;checks++;}
  }
 }
 if(equal(null,null,0)!=1)global::LuminPack.Code.LuminPackExceptionHelper.ThrowInvalidOperationException("Empty comparison failed.");
 return checks;
}
/// <summary>Verifies flags, partial buffers, malformed input, all short lengths and vector tails.</summary><param name="encode">Native encoder.</param><param name="decode">Native decoder.</param><param name="equal">Strict equality.</param><param name="ascii">No-write ASCII encoder.</param><returns>The checked operation count.</returns>
public static int Verify(BurstBackend.Utf8EncodeFunction encode,BurstBackend.Utf8DecodeFunction decode,BurstBackend.Utf8EqualFunction equal,BurstBackend.AsciiEncodeFunction ascii)
{
 int checks=0;
 string[] units={"A","é","\u4E2D","😀","éABÇD","A\u4E2D😀é","\uD800","\uDC00","\uD800A","\uDC00\uD800","\uD800\uD800","\0\"\\"};
 int[] lengths={0,1,2,3,4,5,7,8,9,15,16,17,24,31,32,33,47,48,63,64,65,127,128,129,255,256,257};
 foreach(var unit in units)foreach(int length in lengths)
 {
  var builder=new StringBuilder();while(builder.Length<length)builder.Append(unit);
  string text=builder.ToString(0,length);
  int max=Math.Max(36,length*3+4);
  int[] capacities={0,1,2,3,7,15,16,31,32,length,Math.Max(0,length-1),max};
  foreach(int capacity in capacities)for(uint flags=0;flags<4;flags++)
  {
   byte[] expected=new byte[max+16],actual=new byte[max+16];Fill(expected,(byte)0xA5);Fill(actual,(byte)0xA5);
   var status=StringSerializer.Serialize(text.AsSpan(),expected.AsSpan(3,capacity),out int read,out int written,SerializeMode.Utf8,(flags&1)!=0,(flags&2)!=0);
   fixed(char* input=text)fixed(byte* output=actual)
   {int n=-1,m=-1;int native=encode((ushort*)input,length,output+3,capacity,&n,&m,flags);Require(native==(int)status&&n==read&&m==written,"Encode status/count",text,length,capacity,flags); }
   Require(expected.AsSpan(3,written).SequenceEqual(actual.AsSpan(3,written)),"Encode bytes",text,length,capacity,flags);
   for(int i=0;i<actual.Length;i++)if(i<3||i>=3+written)Require(actual[i]==0xA5,"Encode canary",text,length,capacity,flags);
   checks++;
  }
  byte[] bytes=Encoding.UTF8.GetBytes(text);
  checks+=VerifyDecode(bytes,decode);
  fixed(byte* p=bytes)fixed(char* t=text)
  {
   bool valid=HasValidUtf16(text);
   Require((equal(p,bytes.Length,(ushort*)t,text.Length)!=0)==valid,"Text equality",text,length,0,0);checks++;
   string other=text+"x";fixed(char* o=other)Require(equal(p,bytes.Length,(ushort*)o,other.Length)==0,"Length mismatch",text,length,0,0);
  }
  for(uint flags=0;flags<2;flags++){
   byte[] output=new byte[length+8];Fill(output,0xA5);bool valid=true;
   for(int i=0;i<length;i++)if(text[i]>127||((flags&1)!=0&&(text[i]<32||text[i]=='"'||text[i]=='\\')))valid=false;
   fixed(char* input=text)fixed(byte* p=output){int result=ascii((ushort*)input,length,p+3,flags);if(result!=0){Require(valid,"ASCII accepted invalid",text,length,0,flags);for(int i=0;i<length;i++)Require(output[i+3]==text[i],"ASCII bytes",text,length,0,flags);}else for(int i=0;i<output.Length;i++)Require(output[i]==0xA5,"ASCII refusal wrote",text,length,0,flags);Require(output[0]==0xA5&&output[output.Length-1]==0xA5,"ASCII canary",text,length,0,flags);}checks++;
  }
 }
 byte[][] invalid={new byte[]{0x80},new byte[]{0xC0,0xAF},new byte[]{0xC2},new byte[]{0xE0,0x80,0xAF},new byte[]{0xED,0xA0,0x80},new byte[]{0xE1,0x80},new byte[]{0xE1,0x80,0x41},new byte[]{0xF0,0x80,0x80,0xAF},new byte[]{0xF4,0x90,0x80,0x80},new byte[]{0xF0,0x90,0x80},new byte[]{0xFF,0xFE}};
 foreach(var suffix in invalid)foreach(int prefix in new[]{0,1,15,31,32,33,64,129}){
 var bytes=new byte[prefix+suffix.Length];for(int i=0;i<prefix;i++)bytes[i]=65;Array.Copy(suffix,0,bytes,prefix,suffix.Length);checks+=VerifyDecode(bytes,decode);
 string replaced=Encoding.UTF8.GetString(bytes);fixed(byte* p=bytes)fixed(char* t=replaced)Require(equal(p,bytes.Length,(ushort*)t,replaced.Length)==0,"Invalid text compared equal",replaced,bytes.Length,0,0);
 }
 for(int mask=0;mask<256;mask++)
 {
  char[] text=new char[66];text[0]='\u00E9';text[1]='A';
  for(int i=2;i<text.Length;i++)text[i]=(mask&(1<<(i&7)))==0?(char)('A'+i%26):(char)(128+i%128);
  byte[] expected=Encoding.UTF8.GetBytes(text),output=new byte[text.Length*3+8];Fill(output,0xA5);
  fixed(char* p=text)fixed(byte* q=output){int read=-1,written=-1;int status=encode((ushort*)p,text.Length,q+3,output.Length-6,&read,&written,3);Require(status==0&&read==text.Length&&written==expected.Length,"Latin1 mask status","",mask,output.Length,3);Require(output.AsSpan(3,written).SequenceEqual(expected),"Latin1 mask bytes","",mask,output.Length,3);for(int i=0;i<output.Length;i++)if(i<3||i>=3+written)Require(output[i]==0xA5,"Latin1 mask canary","",mask,output.Length,3);}
  checks++;
 }
 return checks;
}
/// <summary>Checks bounded decoding and every policy bit.</summary><param name="bytes">Input.</param><param name="decode">Native decoder.</param><returns>The checked operation count.</returns>
private static int VerifyDecode(byte[] bytes,BurstBackend.Utf8DecodeFunction decode)
{
 int checks=0;
 foreach(int capacity in new[]{0,1,2,3,7,15,16,31,32,33,Math.Max(0,bytes.Length-1),bytes.Length,bytes.Length+2})for(uint flags=0;flags<4;flags++){
 char[] expected=new char[capacity+8],actual=new char[capacity+8];for(int i=0;i<actual.Length;i++)actual[i]=expected[i]='\uA5A5';
 var status=StringSerializer.Deserialize(bytes,expected.AsSpan(3,capacity),out int read,out int written,SerializeMode.Utf8,(flags&1)!=0,(flags&2)!=0);
 fixed(byte* p=bytes)fixed(char* q=actual){int n=-1,m=-1;int native=decode(p,bytes.Length,(ushort*)(q+3),capacity,&n,&m,flags);Require(native==(int)status&&n==read&&m==written,"Decode status/count","",bytes.Length,capacity,flags);}
 Require(expected.AsSpan(3,written).SequenceEqual(actual.AsSpan(3,written)),"Decode characters","",bytes.Length,capacity,flags);
 for(int i=0;i<actual.Length;i++)if(i<3||i>=3+written)Require(actual[i]=='\uA5A5',"Decode canary","",bytes.Length,capacity,flags);checks++;
 }
 return checks;
}
/// <summary>Checks that equality is only expected for valid UTF-16.</summary><param name="text">Input.</param><returns>Whether all surrogates are paired.</returns>
private static bool HasValidUtf16(string text){for(int i=0;i<text.Length;i++){if(char.IsHighSurrogate(text[i])){if(++i>=text.Length||!char.IsLowSurrogate(text[i]))return false;}else if(char.IsLowSurrogate(text[i]))return false;}return true;}
/// <summary>Fills an output canary.</summary><param name="bytes">Output.</param><param name="value">Canary.</param>
private static void Fill(byte[] bytes,byte value){for(int i=0;i<bytes.Length;i++)bytes[i]=value;}
/// <summary>Reports an exact differential failure.</summary><param name="value">Required condition.</param><param name="operation">Failed operation.</param><param name="text">Fixture.</param><param name="length">Input length.</param><param name="capacity">Output capacity.</param><param name="flags">Codec policies.</param>
private static void Require(bool value,string operation,string text,int length,int capacity,uint flags){if(!value)global::LuminPack.Code.LuminPackExceptionHelper.ThrowInvalidOperationException(operation+" length="+length+" capacity="+capacity+" flags="+flags+" text="+BitConverter.ToString(Encoding.Unicode.GetBytes(text)));}
}
}


