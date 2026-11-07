using LuminPack.Core;

namespace LuminPack.Code;

/// <summary>Writes a registered payload using the caller's binary state.</summary>
/// <typeparam name="T">The payload type.</typeparam>
/// <param name="writer">The current writer.</param>
/// <param name="value">The payload.</param>
public delegate void LuminPackWriteDelegate<T>(ref LuminPackWriter writer, in T value);

/// <summary>Reads a registered payload using the caller's binary state.</summary>
/// <typeparam name="T">The payload type.</typeparam>
/// <param name="reader">The current reader.</param>
/// <param name="value">The payload storage to initialize or reuse.</param>
public delegate void LuminPackReadDelegate<T>(ref LuminPackReader reader, ref T value);

/// <summary>Writes a registered payload using the caller's JSON state.</summary>
/// <typeparam name="T">The payload type.</typeparam>
/// <param name="writer">The current writer.</param>
/// <param name="value">The payload.</param>
public delegate void LuminPackJsonWriteDelegate<T>(ref LuminPackJsonWriter writer, in T value);

/// <summary>Reads a registered payload using the caller's JSON state.</summary>
/// <typeparam name="T">The payload type.</typeparam>
/// <param name="reader">The current reader.</param>
/// <param name="value">The payload storage to initialize or reuse.</param>
public delegate void LuminPackJsonReadDelegate<T>(ref LuminPackJsonReader reader, ref T value);

/// <summary>Writes a registered Union case through its root storage.</summary>
/// <typeparam name="T">The Union root type.</typeparam>
/// <param name="writer">The current writer.</param>
/// <param name="value">The root storage.</param>
public delegate void LuminPackUnionWriteDelegate<T>(ref LuminPackWriter writer, ref T value);

/// <summary>Writes a registered JSON Union case through its root storage.</summary>
/// <typeparam name="T">The Union root type.</typeparam>
/// <param name="writer">The current writer.</param>
/// <param name="value">The root storage.</param>
public delegate void LuminPackUnionJsonWriteDelegate<T>(ref LuminPackJsonWriter writer, ref T value);
