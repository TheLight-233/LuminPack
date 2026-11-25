#nullable disable
using System.Collections.Generic;
using System.Globalization;

namespace LuminPack.SourceGenerator.Next.Rendering.Emitters;

internal static partial class BinaryEmitter
{
    /// <summary>Groups two consecutive wire string reads with direct field destinations and one compiled profile.</summary>
    /// <param name="body">The receiving method statements.</param>
    /// <param name="model">The enclosing payload facts.</param>
    /// <param name="owner">The local holding the target instance.</param>
    /// <param name="w">The source writer.</param>
    /// <param name="index">The first member's index, advanced past the second member on success.</param>
    /// <returns>Whether both members were emitted.</returns>
    private static bool TryAddStringPairRead(List<string> body, PayloadRenderModel model, string owner, CodeWriter w, ref int index)
    {
        const string utf16Length = "LuminPackUtf16LengthOptions";
        if (!model.StringPairs || model.Redirect != null || model.Limits?.Bounded == true || index + 1 == model.Members.Length)
            return false;
        var first = model.Members[index];
        var second = model.Members[index + 1];
        if (!DirectText(first) || !DirectText(second))
            return false;
        string declared = first.ScopeProfile ?? model.DeclaredProfile;
        if (declared != (second.ScopeProfile ?? model.DeclaredProfile) || declared != null && declared != utf16Length)
            return false;
        body.Add("reader.ReadStringPair<" + ReadProfile(model, w, first) + ">(ref " + owner + "." + first.Name +
            ", ref " + owner + "." + second.Name + ");");
        index++;
        return true;

        static bool DirectText(RenderMember member) => member.Kind == RenderMemberKind.Text && !member.Mirrored && !member.Property;
    }

    /// <summary>Reads a staged or reused member through its existing scalar or concrete static owner contract.</summary>
    /// <param name="body">The receiving method statements.</param>
    /// <param name="model">The enclosing payload facts.</param>
    /// <param name="member">The member's compiled operation.</param>
    /// <param name="w">The source writer.</param>
    /// <param name="state">The reader expression.</param>
    /// <param name="target">The addressable destination expression.</param>
    /// <param name="fresh">Whether the destination is a default-initialized local.</param>
    internal static void AddMemberRead(List<string> body, PayloadRenderModel model, RenderMember member, CodeWriter w,
        string state, string target, bool fresh = false)
    {
        string profile = ReadProfile(model, w, member);
        if (member.Kind == RenderMemberKind.Text)
        {
            body.Add(state + ".Read<" + profile + ">(ref " + target + ");");
            return;
        }
        if (member.IsFixed)
        {
            body.Add(target + " = " + (member.IsBoolean && !model.Trusted
                ? w.A("LuminPackInputPolicy") + ".ReadBoolean(ref " + state + ")"
                : state + ".Read<" + member.Scalar + ">()") + ";");
            return;
        }
        if (member.Kind == RenderMemberKind.Parameter)
        {
            if (!fresh)
                AddParameterReset(body, member, w, state, target);
            body.Add(w.A("LuminPackSerializer") + ".FormatterDispatch.Deserialize" + MemberArguments(member, profile) + "(ref " + state + ", ref " + target + "!);");
            return;
        }
        if (member.Kind == RenderMemberKind.Array && member.Scalar == "bool" && !model.Trusted)
        {
            body.Add(w.A("LuminPackInputPolicy") + ".ReadBooleans(ref " + state + ", ref " + target + "!);");
            AddCollectionCheck(body, member, target);
            return;
        }
        if (!fresh && member.FixedPayloadMemberCount != 0 && member.ScopeProfile == null &&
            model.Redirect == null && model.Limits?.Bounded != true)
        {
            string root = w.A("LuminPackSerializer") + ".LuminRoots_" + ReadableTypeName.Of(GeneratedNames.PayloadKey(member.TypeName));
            body.Add("if (" + w.A("LuminPackInputPolicy") + ".ReadNullMarker(ref " + state + ", " +
                member.FixedPayloadMemberCount.ToString(CultureInfo.InvariantCulture) + "))");
            body.Add("\t" + target + " = default!;");
            body.Add("else");
            body.Add("\t" + root + ".RootReadUnion(ref " + state + ", ref " + target + "!);");
            return;
        }
        // Clear a reused destination before its reader raises deserializing callbacks and consumes the marker.
        if (!fresh && (member.Kind == RenderMemberKind.Object || member.NullObjectGuard))
        {
            body.Add("if (" + state + ".PeekIsNullObject())");
            body.Add("\t" + target + " = default!;");
        }
        body.Add(member.BinaryFormatter + ".Read<" + profile + ">(ref " + state + ", ref " + target + "!);");
        AddCollectionCheck(body, member, target);
    }

    /// <summary>Identifies a checked fixed case whose payload read keeps the object read's storage proof and fallback.</summary>
    /// <param name="child">The nested record's render facts.</param>
    /// <returns>True when only the checked object marker distinguishes the two reads.</returns>
    internal static bool HasEquivalentFixedPayloadRead(PayloadRenderModel child)
    {
        if (!child.ManagedFixedUnionCase || child.Trusted || child.HasCallbacks || child.Rent != null ||
            child.Mirror != null || child.ConstructorArguments != null || child.Version != null ||
            child.TypeParameters.Length != 0 || child.DeclaredProfile != null || child.Recursive || child.Redirect != null ||
            child.Rejection != null || child.Limits?.Bounded == true || child.Members.Length < 2 ||
            child.StreamRecord || child.MixedRecord || !RenderModelFactory.Covered(child))
            return false;

        foreach (var field in child.Members)
            if (!field.IsFixed || field.Mirrored || field.Property || field.Volatile || field.IsBoolean)
                return false;

        return RunEnd(child.Members, 0) == child.Members.Length - 1;
    }
}
