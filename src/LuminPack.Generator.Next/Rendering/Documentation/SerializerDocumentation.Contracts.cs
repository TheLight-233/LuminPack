using System.Globalization;
using LuminPack.SourceGenerator.Next.Model;

namespace LuminPack.SourceGenerator.Next.Rendering;

internal static partial class SerializerDocumentation
{
    /// <summary>Describes the exact ordered contract without exposing implementation helpers as documented APIs.</summary>
    /// <param name="xml">The transient documentation buffer.</param>
    /// <param name="indent">The declaration indentation.</param>
    /// <param name="facts">The compiler-free declaration facts.</param>
    private static void Contract(PooledTextBuffer xml, string indent, PayloadDocumentation facts)
    {
        TypeModel model = facts.Model;
        string mode = model is UnionModel ? "Union" : model is CircleReferenceModel ? "CircleReference" :
            model is RecordModel { Versioned: true } or StructModel { Versioned: true } ? "VersionTolerant" : "Object";
        Line(xml, indent, "<para>Generation mode: <c>" + mode + "</c>. " + (model is StructModel ? "Value type." : "Reference type.") + "</para>");
        if (model.Identity.IsGeneric)
            Line(xml, indent, "<para>The member table describes the generic declaration; closed entries substitute their actual type arguments.</para>");
        xml.Append(indent).Append("/// <para>CLR memory layout: <c>");
        Escape(xml, facts.Layout);
        xml.Append("</c>. CLR offsets and padding are distinct from serialized member order; JSON uses member names.</para>").Append((char)10);
        if (model is UnionModel union)
        {
            Line(xml, indent, "<para>The Union tag selects a concrete case payload through the existing virtual dispatch contract.</para>");
            Line(xml, indent, "<list type='table'><listheader><term>Tag</term><description>Concrete case</description></listheader>");
            foreach (UnionCaseModel item in union.Cases)
            {
                xml.Append(indent).Append("/// <item><term>").Append(item.Tag.ToString(CultureInfo.InvariantCulture)).Append("</term><description><c>");
                Escape(xml, item.Type.GlobalName);
                xml.Append("</c></description></item>").Append((char)10);
            }
            Line(xml, indent, "</list>");
            return;
        }
        if (model is StructModel { Storage: StructStorage.RawUnmanaged or StructStorage.PortableUnmanaged } structure)
        {
            Line(xml, indent, structure.StoragePlan?.Tagged == true ?
                "<para>Binary unmanaged storage uses the tagged layout contract. Its native-layout tag identifies the validated storage model; packed wire offsets and CLR padding remain distinct.</para>" :
                structure.Storage == StructStorage.PortableUnmanaged ?
                "<para>Binary unmanaged storage follows the packed field contract without CLR padding. Architecture-specific transfers preserve the declared storage representation.</para>" :
                "<para>Binary unmanaged storage follows its declared native or packed layout contract. Native copies can include CLR padding; ignored named members can still occupy serialized storage.</para>");
            Line(xml, indent, "<list type='table'><listheader><term>Storage member</term><description>Type and CLR offset</description></listheader>");
            for (int index = 0; index < facts.StorageNames.Length; index++)
            {
                xml.Append(indent).Append("/// <item><term><c>");
                Escape(xml, facts.StorageNames[index]);
                xml.Append("</c></term><description><c>");
                Escape(xml, facts.StorageTypes[index]);
                xml.Append("</c>; CLR offset: ");
                Escape(xml, facts.StorageOffsets[index]);
                xml.Append(".</description></item>").Append((char)10);
            }
            Line(xml, indent, "</list>");
            Line(xml, indent, "<para>JSON serializes the filtered named members below; it does not copy CLR storage or padding.</para>");
        }
        else
            Line(xml, indent, mode == "VersionTolerant" ?
                "<para>Binary members use stable UTF-8 names and the version-tolerant width table. Former names preserve their established wire identities; preceding payloads determine variable offsets.</para>" :
                mode == "CircleReference" ?
                "<para>Binary members follow their declared slots with explicit reference identities and slot widths. Reused targets retain their documented publication behavior.</para>" :
                "<para>Binary members follow the positional member header in the order below. Variable payloads prevent a single fixed byte-offset table.</para>");
        Line(xml, indent, "<list type='table'><listheader><term>Serialized member</term><description>Type and order</description></listheader>");
        for (int index = 0; index < model.Members.Length; index++)
        {
            MemberModel member = model.Members[index];
            xml.Append(indent).Append("/// <item><term>");
            if (facts.MemberIds[index].Length != 0)
            {
                xml.Append("<see cref='");
                Escape(xml, facts.MemberIds[index]);
                xml.Append("'/>");
            }
            else
            {
                xml.Append("<c>");
                Escape(xml, member.Name);
                xml.Append("</c>");
            }
            xml.Append("</term><description><c>");
            Escape(xml, facts.MemberTypes[index]);
            xml.Append("</c>; binary order: ").Append((index + 1).ToString(CultureInfo.InvariantCulture));
            xml.Append(member.Storage == MemberStorage.AutoProperty ? "; auto-property storage" :
                member.Storage == MemberStorage.IncludedField ? "; included non-public storage" : "; field");
            if (member.Compressed)
                xml.Append("; compressed");
            if (model is CircleReferenceModel tracked && index < tracked.Slots.Length)
                xml.Append("; slot: ").Append(tracked.Slots[index].ToString(CultureInfo.InvariantCulture));
            VersionSchemaModel? version = model is RecordModel recordVersion ? recordVersion.Version :
                model is StructModel structVersion ? structVersion.Version : null;
            if (version != null && index < version.OldNames.Length)
                foreach (string oldName in version.OldNames[index])
                {
                    xml.Append("; former name: <c>");
                    Escape(xml, oldName);
                    xml.Append("</c>");
                }
            xml.Append(".</description></item>").Append((char)10);
        }
        Line(xml, indent, "</list>");
        CallbackModel? callbacks = model switch
        {
            RecordModel record => record.Callbacks,
            StructModel value => value.SerializationCallbacks,
            CircleReferenceModel circle => circle.SerializationCallbacks,
            _ => null,
        };
        if (callbacks != null)
            Line(xml, indent, "<para>Serialization callbacks run at their established before/after read and write boundaries.</para>");
        bool rent = model is RecordModel { Rent: not null } or StructModel { RentCall: not null } or CircleReferenceModel { RentCall: not null };
        if (rent)
            Line(xml, indent, "<para>Fresh reads use the declared pool rental. Existing instances follow the same reuse and failure-publication contract.</para>");
    }
}
