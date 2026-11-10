#nullable disable
using System;
using System.Collections.Generic;

namespace LuminPack.SourceGenerator.Next.Rendering.Emitters;

internal static partial class BinaryEmitter
{
    // The version-tolerant schema analysis needs the revision metadata encoder, former names and constructor facts that the
    // Next model does not carry yet. The model factory leaves model.Version null, so these entry points are unreachable until
    // the version migration task fills the schema. They return empty bodies instead of throwing so a diagnostic type never
    // crashes generation.

    /// <summary>Builds the version-tolerant record write body.</summary>
    /// <param name="model">The render model.</param>
    /// <param name="w">The writer.</param>
    /// <returns>The write statements, which are empty until the schema is modeled.</returns>
    private static List<string> VersionWriteBody(PayloadRenderModel model, CodeWriter w) => new List<string>();

    /// <summary>Whether a version-tolerant write inlines its member stores.</summary>
    /// <param name="model">The render model.</param>
    /// <returns>False until the schema is modeled.</returns>
    internal static bool VersionWriteInline(PayloadRenderModel model) => false;

    /// <summary>Builds the version-tolerant record read body.</summary>
    /// <param name="model">The render model.</param>
    /// <param name="w">The writer.</param>
    /// <returns>The read statements, which are empty until the schema is modeled.</returns>
    private static List<string> VersionReadBody(PayloadRenderModel model, CodeWriter w) => new List<string>();

    /// <summary>Emits the version schema class.</summary>
    /// <param name="model">The render model.</param>
    /// <param name="w">The writer.</param>
    private static void VersionSchemaClass(PayloadRenderModel model, CodeWriter w)
    {
    }

    /// <summary>Emits the version-tolerant struct roots.</summary>
    /// <param name="model">The render model.</param>
    /// <param name="w">The writer.</param>
    /// <param name="operations">The operations the project calls.</param>
    internal static void EmitVersionStruct(PayloadRenderModel model, CodeWriter w, GeneratedOperations operations)
    {
    }
}
