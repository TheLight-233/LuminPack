using System;
using System.Collections.Generic;
using System.Linq;
using LuminPack.SourceGenerator.Next.Diagnostics;
using Microsoft.CodeAnalysis;

namespace LuminPack.SourceGenerator.Next.Analysis;

internal static partial class TypeModelFactory
{
    private sealed partial class Session
    {
        /// <summary>Selects the same public explicit constructor used by model creation and diagnostics.</summary>
        /// <param name="type">The payload declaration.</param>
        /// <param name="ambiguous">Whether several constructors remain possible.</param>
        /// <returns>The selected constructor, or null for implicit value construction or an invalid selection.</returns>
        private static IMethodSymbol? SelectConstructor(INamedTypeSymbol type, out bool ambiguous)
        {
            IMethodSymbol? single = null, marked = null;
            int count = 0, markedCount = 0;
            foreach (IMethodSymbol constructor in type.InstanceConstructors)
            {
                if (constructor.DeclaredAccessibility != Accessibility.Public || constructor.IsImplicitlyDeclared)
                    continue;
                single = constructor;
                count++;
                if (HasAttribute(constructor, ConstructorAttribute))
                {
                    marked = constructor;
                    markedCount++;
                }
            }
            ambiguous = markedCount > 1 || markedCount == 0 && count > 1;
            return ambiguous ? null : marked ?? single;
        }

        /// <summary>Checks constructor selection and whether each parameter can receive its matching serialized member.</summary>
        /// <param name="type">The payload declaration.</param>
        /// <param name="members">The filtered serialized storage.</param>
        /// <param name="diagnostics">Receives constructor diagnostics.</param>
        private void ValidateConstructors(INamedTypeSymbol type, List<ISymbol> members, ref DeclarationDiagnosticBag diagnostics)
        {
            IMethodSymbol? selected = SelectConstructor(type, out bool ambiguous);
            if (ambiguous)
            {
                diagnostics.Add(DiagnosticDescriptors.MultipleConstructorsRequireAttribute, type, type.Name,
                    additional: type.OriginalDefinition.Locations);
                return;
            }
            if (selected == null)
            {
                if (type.TypeKind == TypeKind.Class && type.InstanceConstructors.Any(constructor => !constructor.IsImplicitlyDeclared))
                    diagnostics.Add(DiagnosticDescriptors.NoPublicConstructor, type, type.Name, additional: type.OriginalDefinition.Locations);
                return;
            }
            foreach (IParameterSymbol parameter in selected.Parameters)
            {
                ISymbol? member = members.FirstOrDefault(item => string.Equals(item.Name, parameter.Name, StringComparison.OrdinalIgnoreCase));
                if (member == null)
                    diagnostics.Add(DiagnosticDescriptors.ConstructorParameterNameMismatch, type, parameter.Name, type.Name);
                else if (parameter.RefKind is RefKind.Ref or RefKind.Out ||
                    !_compilation.ClassifyCommonConversion(StorageType(member), parameter.Type).IsImplicit)
                    diagnostics.Add(DiagnosticDescriptors.ConstructorParameterCannotReceiveMember, parameter, parameter.Name, type.Name, member.Name);
            }
        }

        /// <summary>Checks each callback using the legacy rule precedence.</summary>
        /// <param name="type">The attributed payload.</param>
        /// <param name="diagnostics">Receives callback diagnostics.</param>
        private static void ValidateCallbacks(INamedTypeSymbol type, ref DeclarationDiagnosticBag diagnostics)
        {
            foreach (IMethodSymbol method in type.GetMembers().OfType<IMethodSymbol>())
            foreach (AttributeData attribute in method.GetAttributes())
            {
                if (attribute.AttributeClass?.ToDisplayString() is not (
                    "LuminPack.Attribute.LuminPackOnSerializingAttribute" or "LuminPack.Attribute.LuminPackOnSerializedAttribute" or
                    "LuminPack.Attribute.LuminPackOnDeserializingAttribute" or "LuminPack.Attribute.LuminPackOnDeserializedAttribute"))
                    continue;
                if (type.TypeKind == TypeKind.Struct && type.IsUnmanagedType)
                    diagnostics.Add(DiagnosticDescriptors.OnMethodInUnmanagedType, method, type.Name, method.Name);
                else if (method.DeclaredAccessibility is Accessibility.NotApplicable or Accessibility.Private or Accessibility.Protected or Accessibility.ProtectedAndInternal)
                    diagnostics.Add(DiagnosticDescriptors.OnMethodIsPrivate, method, type.Name, method.Name);
                else if (method.Parameters.Length != 0 || method.IsGenericMethod)
                    diagnostics.Add(DiagnosticDescriptors.OnMethodHasParameter, method, type.Name, method.Name);
            }
        }

        /// <summary>Checks pool method count, declaration, accessibility, arguments and exact return identity.</summary>
        /// <param name="type">The attributed payload.</param>
        /// <param name="diagnostics">Receives rental diagnostics.</param>
        private void ValidatePoolRent(INamedTypeSymbol type, ref DeclarationDiagnosticBag diagnostics)
        {
            IMethodSymbol? selected = null;
            foreach (IMethodSymbol method in type.GetMembers().OfType<IMethodSymbol>())
            {
                if (!HasAttribute(method, PoolRentAttribute))
                    continue;
                if (selected != null)
                {
                    diagnostics.Add(DiagnosticDescriptors.MultipleRentPoolMethods, type, type.Name);
                    return;
                }
                selected = method;
            }
            if (selected == null)
                return;
            if (!selected.IsStatic)
                diagnostics.Add(DiagnosticDescriptors.RentPoolMethodMustBeStatic, selected, selected.Name, type.Name);
            else if (selected.Parameters.Length != 0 || selected.IsGenericMethod)
                diagnostics.Add(DiagnosticDescriptors.RentPoolMethodHasParameters, selected, selected.Name, type.Name);
            else if (!SymbolEqualityComparer.Default.Equals(selected.ReturnType, type))
                diagnostics.Add(DiagnosticDescriptors.RentPoolMethodReturnTypeMismatch, selected, selected.Name, type.Name,
                    type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat));
            else if (!_compilation.IsSymbolAccessibleWithin(selected, _compilation.Assembly))
                diagnostics.Add(DiagnosticDescriptors.InaccessiblePoolRent, selected, selected.Name, type.Name);
        }

        /// <summary>Checks the required binary pair and optional complete JSON pair on a Custom declaration.</summary>
        /// <param name="type">The user-owned formatter contract.</param>
        /// <param name="diagnostics">Receives Custom diagnostics.</param>
        private void ValidateCustom(INamedTypeSymbol type, ref DeclarationDiagnosticBag diagnostics)
        {
            string display = type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);
            if (type.IsGenericType)
                diagnostics.Add(DiagnosticDescriptors.CustomTypeMustNotBeGeneric, type, display);
            if (type.IsAbstract || HasAttribute(type, UnionAttribute))
                diagnostics.Add(DiagnosticDescriptors.CustomUnionNotSupported, type, display);
            ValidateCustomMethod(type, "Serialize", "LuminPackWriter", RefKind.In, true, ref diagnostics);
            ValidateCustomMethod(type, "Deserialize", "LuminPackReader", RefKind.Ref, true, ref diagnostics);
            bool writeJson = CustomMethod(type, "SerializeJson") != null, readJson = CustomMethod(type, "DeserializeJson") != null;
            if (writeJson != readJson)
                diagnostics.Add(DiagnosticDescriptors.CustomJsonMethodsIncomplete, type, display,
                    writeJson ? "SerializeJson" : "DeserializeJson", writeJson ? "DeserializeJson" : "SerializeJson");
            ValidateCustomMethod(type, "SerializeJson", "LuminPackJsonWriter", RefKind.In, false, ref diagnostics);
            ValidateCustomMethod(type, "DeserializeJson", "LuminPackJsonReader", RefKind.Ref, false, ref diagnostics);
            if (Options.RegisterMode == Model.RegisterMode.Disabled)
                diagnostics.Add(DiagnosticDescriptors.CustomRegistrationDisabled, type, display);
        }

        /// <summary>Finds the first ordinary Custom method, including inaccessible declarations for actionable diagnostics.</summary>
        /// <param name="type">The Custom payload.</param>
        /// <param name="name">The operation name.</param>
        /// <returns>The declared operation, or null.</returns>
        private static IMethodSymbol? CustomMethod(INamedTypeSymbol type, string name)
            => type.GetMembers(name).OfType<IMethodSymbol>().FirstOrDefault(method => method.MethodKind == MethodKind.Ordinary);

        /// <summary>Checks a Custom operation's exact typed-delegate signature.</summary>
        /// <param name="type">The Custom payload.</param>
        /// <param name="name">The operation name.</param>
        /// <param name="state">The state type name.</param>
        /// <param name="passing">The payload passing convention.</param>
        /// <param name="required">Whether a missing operation is an error.</param>
        /// <param name="diagnostics">Receives signature diagnostics.</param>
        private static void ValidateCustomMethod(INamedTypeSymbol type, string name, string state, RefKind passing, bool required,
            ref DeclarationDiagnosticBag diagnostics)
        {
            IMethodSymbol? method = CustomMethod(type, name);
            if (method == null && !required)
                return;
            string display = type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);
            string qualified = type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            string signature = "static void " + name + "(ref global::LuminPack.Core." + state + " " +
                (passing == RefKind.In ? "writer, in " : "reader, ref ") + qualified + " value)";
            if (method == null)
            {
                diagnostics.Add(DiagnosticDescriptors.CustomMissingMethod, type, display, name, signature);
                return;
            }
            if (!method.IsStatic)
                diagnostics.Add(DiagnosticDescriptors.CustomMethodMustBeStatic, method, display, name);
            if (method.DeclaredAccessibility is not (Accessibility.Public or Accessibility.Internal))
                diagnostics.Add(DiagnosticDescriptors.CustomMethodMustBeAccessible, method, display, name);
            if (method.IsGenericMethod || !method.ReturnsVoid || method.Parameters.Length != 2 ||
                method.Parameters[0].RefKind != RefKind.Ref ||
                method.Parameters[0].Type.ToDisplayString() != "LuminPack.Core." + state ||
                method.Parameters[1].RefKind != passing || !SymbolEqualityComparer.Default.Equals(method.Parameters[1].Type, type))
                diagnostics.Add(DiagnosticDescriptors.CustomMethodSignatureMismatch, method, display, name, signature);
        }
    }
}
