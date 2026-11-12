using System;

namespace LuminPack.SourceGenerator.Next.Rendering.Facade;

internal static partial class DispatchOptionsSource
{
    /// <summary>Initializes the disabled fallback only for assemblies that request it.</summary>
    private static class DisabledRegistration
    {
        internal static readonly string Source = WithoutRegistration();
    }

    /// <summary>Retains all four fallback signatures while replacing their bodies with the original missing-registration failure.</summary>
    /// <returns>The immutable disabled-registration template.</returns>
    private static string WithoutRegistration()
    {
        int failureStart = Template.IndexOf("LuminPackExceptionHelper.ThrowMessage(", StringComparison.Ordinal);
        int failureEnd = Template.IndexOf(';', failureStart) + 1;
        string failure = Template.Substring(failureStart, failureEnd - failureStart);
        using var output = new PooledTextBuffer(Template.Length);
        int copied = 0;
        foreach (GeneratedText.Method method in GeneratedText.ReadMethods(Template))
        {
            if (!method.Name.EndsWith("Registered", StringComparison.Ordinal))
                continue;
            int open = Template.IndexOf('{', method.SignatureStart + method.Signature.Length);
            output.Append(Template, copied, open + 1 - copied);
            output.Append((char)10).Append("                ").Append(failure).Append((char)10).Append("            ");
            copied = method.End - 1;
        }
        output.Append(Template, copied, Template.Length - copied);
        return output.ToString();
    }
}
