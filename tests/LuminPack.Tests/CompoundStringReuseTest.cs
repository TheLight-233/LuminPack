using System.Buffers.Binary;
using LuminPack;
using LuminPack.Option;
using Xunit;

namespace LuminPackUnitTest;

/// <summary>Protects immutable string candidates in direct scalar/string compound records.</summary>
public class CompoundStringReuseTest
{
    /// <summary>Preserves every profile, matching string identity and reference-tuple construction.</summary>
    [Fact]
    public void ProfilesKeepMatchingImmutableStringsAndReplaceChangedText()
    {
        Check<LuminPackUtf8LengthOptions>();
        Check<LuminPackUtf8TokenOptions>();
        Check<LuminPackUtf16LengthOptions>();
        Check<LuminPackUtf16TokenOptions>();
    }

    /// <summary>Exercises concrete pair and tuple entries under the original binary profile.</summary>
    /// <typeparam name="TProfile">The selected stateless string profile.</typeparam>
    /// <summary>Exercises concrete compound entries with stable, changed, Unicode, null and empty strings.</summary>
    /// <typeparam name="TProfile">The original stateless string profile.</typeparam>
    private static void Check<TProfile>() where TProfile:struct,ILuminPackOptions
    {
        string text=new('x',17), old=new('x',17);
        var pair=new KeyValuePair<int,string>(137,text);
        var target=new KeyValuePair<int,string>(719,old);
        byte[] bytes=LuminPackSerializer.Serialize<TProfile>(pair);
        Assert.Equal(bytes.Length,LuminPackSerializer.Deserialize<TProfile>(bytes,ref target));
        Assert.Equal(137,target.Key);Assert.Same(old,target.Value);
        var tuple=(Number:137,Text:text,Enabled:true);
        var tupleTarget=(Number:719,Text:old,Enabled:false);
        bytes=LuminPackSerializer.Serialize<TProfile>(tuple);
        Assert.Equal(bytes.Length,LuminPackSerializer.Deserialize<TProfile>(bytes,ref tupleTarget));
        Assert.Equal(137,tupleTarget.Number);Assert.True(tupleTarget.Enabled);Assert.Same(old,tupleTarget.Text);
        var reference=Tuple.Create(137,text,true);
        var referenceTarget=Tuple.Create(719,old,false);
        var previous=referenceTarget;
        bytes=LuminPackSerializer.Serialize<TProfile>(reference);
        Assert.Equal(bytes.Length,LuminPackSerializer.Deserialize<TProfile>(bytes,ref referenceTarget));
        Assert.NotSame(previous,referenceTarget);Assert.Same(old,referenceTarget.Item2);
        pair=new(19,new string('y',17));bytes=LuminPackSerializer.Serialize<TProfile>(pair);
        LuminPackSerializer.Deserialize<TProfile>(bytes,ref target);
        Assert.NotSame(old,target.Value);Assert.Equal(pair,target);
        foreach(string? next in new string?[]{null,string.Empty,"\u00e9\u4e2d\ud83d\ude00","changed"})
        {
            var nullable=new KeyValuePair<int,string?>(137,next);
            var nullableTarget=new KeyValuePair<int,string?>(719,old);
            bytes=LuminPackSerializer.Serialize<TProfile>(nullable);
            LuminPackSerializer.Deserialize<TProfile>(bytes,ref nullableTarget);
            string? expected=next is null && default(TProfile).StringRecording==LuminPackStringRecording.Token?string.Empty:next;
            Assert.Equal(expected,nullableTarget.Value);Assert.Equal(137,nullableTarget.Key);
        }
    }

    /// <summary>Keeps the complete original value until all fields have parsed successfully.</summary>
    [Fact]
    public void IncompleteRecordsKeepTheOriginalStruct()
    {
        var source=(137,"incoming",true);
        byte[] bytes=LuminPackSerializer.Serialize(source);
        string old=new('z',7);
        for(int length=0;length<bytes.Length;length++)
        {
            var target=(719,old,false);
            Assert.ThrowsAny<Exception>(()=>LuminPackSerializer.Deserialize(bytes.AsSpan(0,length),ref target));
            Assert.Equal(719,target.Item1);Assert.Same(old,target.Item2);Assert.False(target.Item3);
        }
        bytes=LuminPackSerializer.Serialize(new KeyValuePair<int,string>(137,"incoming"));
        for(int length=0;length<bytes.Length;length++)
        {
            var target=new KeyValuePair<int,string>(719,old);
            Assert.ThrowsAny<Exception>(()=>LuminPackSerializer.Deserialize(bytes.AsSpan(0,length),ref target));
            Assert.Equal(719,target.Key);Assert.Same(old,target.Value);
        }
    }

    /// <summary>Rejects malformed records without accepting a candidate or publishing partial storage.</summary>
    [Fact]
    public void MalformedStringsCannotMatchAnExistingCandidate()
    {
        var source=new KeyValuePair<int,string>(137,"incoming");
        byte[] bytes=LuminPackSerializer.Serialize<LuminPackUtf8LengthOptions>(source);
        bytes[12]=255;
        var target=new KeyValuePair<int,string>(719,new string("incoming".ToCharArray()));
        var original=target;
        Assert.ThrowsAny<Exception>(()=>LuminPackSerializer.Deserialize<LuminPackUtf8LengthOptions>(bytes,ref target));
        Assert.Equal(original,target);Assert.Same(original.Value,target.Value);
        bytes=LuminPackSerializer.Serialize<LuminPackUtf16LengthOptions>(source);
        // Odd positive headers are compact Latin-1 records; a negative non-null header remains malformed.
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(4),-3);
        Assert.ThrowsAny<Exception>(()=>LuminPackSerializer.Deserialize<LuminPackUtf16LengthOptions>(bytes,ref target));
        Assert.Equal(original,target);Assert.Same(original.Value,target.Value);
    }

    /// <summary>Preserves per-position immutable strings when the array reader supplies an existing compound.</summary>
    [Fact]
    public void ExistingTupleAndPairArraysRetainEqualStringsByPosition()
    {
        string first=new('a',13),second=new('b',13);
        var source=new[]{new KeyValuePair<int,string>(1,new('a',13)),new KeyValuePair<int,string>(2,new('b',13))};
        var target=new[]{new KeyValuePair<int,string>(719,first),new KeyValuePair<int,string>(981,second)};
        byte[] bytes=LuminPackSerializer.Serialize(source);
        var original=target;
        LuminPackSerializer.Deserialize(bytes,ref target);
        Assert.Same(original,target);Assert.Same(first,target[0].Value);Assert.Same(second,target[1].Value);
        Assert.Equal(1,target[0].Key);Assert.Equal(2,target[1].Key);
        var tuples=new[]{(1,first,true),(2,second,false)};
        bytes=LuminPackSerializer.Serialize(tuples);
        var tupleTarget=new[]{(719,first,false),(981,second,true)};
        LuminPackSerializer.Deserialize(bytes,ref tupleTarget);
        Assert.Same(first,tupleTarget[0].Item2);Assert.Same(second,tupleTarget[1].Item2);
        Assert.Equal(1,tupleTarget[0].Item1);Assert.True(tupleTarget[0].Item3);
    }
}
