#if !NET5_0_OR_GREATER
namespace System.Runtime.CompilerServices;

/// <summary>Enables method-local compiler initialization control on portable targets.</summary>
[AttributeUsage(AttributeTargets.Method, Inherited = false)]
internal sealed class SkipLocalsInitAttribute : Attribute
{
}
#endif
