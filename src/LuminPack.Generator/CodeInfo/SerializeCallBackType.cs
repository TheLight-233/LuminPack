namespace LuminPack.Code;

/// <summary>Provides serialize call back type functionality.</summary>
public enum SerializeCallBackType : byte
{
    /// <summary>Specifies on serializing.</summary>
    OnSerializing,
    /// <summary>Specifies on serialized.</summary>
    OnSerialized,
    /// <summary>Specifies on deserializing.</summary>
    OnDeserializing,
    /// <summary>Specifies on deserialized.</summary>
    OnDeserialized
}
