using System;
namespace LuminPack.Burst.Tests
{
/// <summary>Validates native layout prefixes and cipher rounds independently of managed Core implementation selection.</summary>
public static unsafe class LayoutCryptoContract
{
/// <summary>Checks cached plans against the original descriptor-based entry for every tested geometry.</summary><param name="legacy">Original native prefix.</param><param name="planned">Precomputed native prefix.</param><returns>The checked case count.</returns>
public static int VerifyPlanned(BurstBackend.LayoutFunction legacy, BurstBackend.LayoutPlannedFunction planned)
{
int cases=0;var random=new Random(311);
foreach(int a in new[]{1,3,5,8,12,16,17,24,31,32,33})foreach(int b in new[]{1,5,8,16,17,24,32,33})foreach(int count in new[]{0,1,2,3,7,16,32,129,511,512,513,1024})
{
int n=Math.Min(a,b)+3;int[] from=new int[n],to=new int[n];for(int i=0;i<n;i++){from[i]=random.Next(a);to[i]=random.Next(b);}
int segments=n==0?0:1;for(int i=1;i<n;i++)if(from[i]!=from[i-1]+1||to[i]!=to[i-1]+1)segments++;
var plan=global::LuminPack.Utility.LuminLayoutPlan.Create(from,a,to,b,segments);
byte[] input=new byte[count*a+16],expected=new byte[count*b+16],output=new byte[expected.Length];random.NextBytes(input);random.NextBytes(expected);Array.Copy(expected,output,output.Length);
fixed(byte* p=input)fixed(byte* x=expected)fixed(byte* y=output)fixed(int* f=from)fixed(int* t=to)
{
int oldDone=global::LuminPack.Code.LuminKernelPolicy.UseNativeLayout(count)?legacy(p+3,x+3,count,a,b,f,t,n):0,done=planned(p+3,y+3,count,&plan);
Require(done==oldDone,"Cached plan prefix differs.");Require(output.AsSpan().SequenceEqual(expected),"Cached plan bytes/padding/canaries differ.");
}
cases++;
}
return cases;
}
/// <summary>Checks random valid descriptors, duplicate destinations, tails, canaries, counters and read-only state.</summary><param name="layout">Native layout prefix.</param><param name="cipher">Native cipher blocks.</param><returns>The checked case count.</returns>
public static int Verify(BurstBackend.LayoutFunction layout,BurstBackend.ChaCha20Function cipher)
{
int checks=0;var random=new Random(20261026);
foreach(int a in new[]{1,2,3,4,7,8,12,15,16,17,20,24,31,32,33})
foreach(int b in new[]{1,3,4,7,8,12,16,17,20,24,31,32,33})
foreach(int count in new[]{0,1,2,3,7,8,15,16,31,32,63})
{
int n=Math.Min(a,b)+3;int[] from=new int[n],to=new int[n];
for(int i=0;i<n;i++){from[i]=random.Next(a);to[i]=random.Next(b);}
byte[] input=new byte[a*count+16],output=new byte[b*count+16];random.NextBytes(input);for(int i=0;i<output.Length;i++)output[i]=0xA5;
byte[] inputBefore=(byte[])input.Clone(),expected=(byte[])output.Clone();
for(int element=0;element<count;element++)for(int i=0;i<n;i++)expected[3+element*b+to[i]]=input[3+element*a+from[i]];
fixed(byte* p=input)fixed(byte* q=output)fixed(int* x=from)fixed(int* y=to){
int done=layout(p+3,q+3,count,a,b,x,y,n);Require(done>=0&&done<=count,"Layout count");
for(int element=done;element<count;element++)for(int i=0;i<n;i++)output[3+element*b+to[i]]=input[3+element*a+from[i]];
}
for(int element=0;element<count;element++)for(int i=0;i<n;i++)Require(output[3+element*b+to[i]]==expected[3+element*b+to[i]],"Layout data/order");
for(int i=0;i<output.Length;i++)if(i<3||i>=3+b*count)Require(output[i]==0xA5,"Layout canary");
Require(input.AsSpan().SequenceEqual(inputBefore),"Layout changed input");checks++;
}
uint[] rfc={0x61707865,0x3320646e,0x79622d32,0x6b206574,0x03020100,0x07060504,0x0b0a0908,0x0f0e0d0c,0x13121110,0x17161514,0x1b1a1918,0x1f1e1d1c,1,0x09000000,0x4a000000,0};
byte[] known=Hex("10f1e7e4d13b5915500fdd1fa32071c4c7d1f4c733c068030422aa9ac3d46c4ed2826446079faa0914c2d705d98b02a2b5129cd1de164eb9cbd083e8a2503c4e");
fixed(uint* state=rfc){byte[] output=new byte[64];fixed(byte* q=output)cipher(state,q,1);Require(output.AsSpan().SequenceEqual(known),"RFC 8439 block");checks++;}
for(int sample=0;sample<128;sample++)foreach(int blocks in new[]{1,2,4}){
uint[] state=new uint[16];for(int i=0;i<16;i++)state[i]=(uint)random.Next()^((uint)random.Next()<<1);if(sample<4)state[12]=uint.MaxValue-(uint)sample;
uint[] before=(uint[])state.Clone();byte[] output=new byte[blocks*64+13],expected=new byte[blocks*64];for(int i=0;i<output.Length;i++)output[i]=0xA5;
for(int block=0;block<blocks;block++){state[12]=unchecked(before[12]+(uint)block);ReferenceBlock(state,expected,block*64);}state[12]=before[12];
fixed(uint* p=state)fixed(byte* q=output)cipher(p,q+3,blocks);
Require(output.AsSpan(3,blocks*64).SequenceEqual(expected),"Cipher differential");Require(state.AsSpan().SequenceEqual(before),"Cipher changed state");
for(int i=0;i<output.Length;i++)if(i<3||i>=3+blocks*64)Require(output[i]==0xA5,"Cipher canary");checks++;
}
return checks;
}
/// <summary>Computes an independent test oracle from RFC 8439 quarter rounds.</summary><param name="state">Input words.</param><param name="output">Output.</param><param name="offset">Output offset.</param>
private static void ReferenceBlock(uint[] state,byte[] output,int offset)
{
uint[] x=(uint[])state.Clone();for(int i=0;i<10;i++){Round(x,0,4,8,12);Round(x,1,5,9,13);Round(x,2,6,10,14);Round(x,3,7,11,15);Round(x,0,5,10,15);Round(x,1,6,11,12);Round(x,2,7,8,13);Round(x,3,4,9,14);}
for(int i=0;i<16;i++){uint word=unchecked(x[i]+state[i]);for(int j=0;j<4;j++)output[offset+i*4+j]=(byte)(word>>(8*j));}
}
/// <summary>Applies the independent RFC quarter round.</summary><param name="x">Words.</param><param name="a">First index.</param><param name="b">Second index.</param><param name="c">Third index.</param><param name="d">Fourth index.</param>
private static void Round(uint[] x,int a,int b,int c,int d){unchecked{x[a]+=x[b];x[d]=Rotate(x[d]^x[a],16);x[c]+=x[d];x[b]=Rotate(x[b]^x[c],12);x[a]+=x[b];x[d]=Rotate(x[d]^x[a],8);x[c]+=x[d];x[b]=Rotate(x[b]^x[c],7);}}
/// <summary>Rotates an oracle word.</summary><param name="value">Word.</param><param name="count">Bits.</param><returns>The rotated word.</returns>
private static uint Rotate(uint value,int count)=>(value<<count)|(value>>(32-count));
/// <summary>Reads an RFC hexadecimal vector.</summary><param name="text">Hexadecimal input.</param><returns>The bytes.</returns>
private static byte[] Hex(string text){var bytes=new byte[text.Length/2];for(int i=0;i<bytes.Length;i++)bytes[i]=Convert.ToByte(text.Substring(i*2,2),16);return bytes;}
/// <summary>Reports an independent native contract failure.</summary><param name="condition">Required condition.</param><param name="message">Failure.</param>
private static void Require(bool condition,string message){if(!condition)global::LuminPack.Code.LuminPackExceptionHelper.ThrowInvalidOperationException(message);}
}
}


