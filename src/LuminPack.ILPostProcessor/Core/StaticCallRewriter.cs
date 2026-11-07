using System;
using System.Collections.Generic;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace LuminPack.Build
{
    /// <summary>Replaces mapped local static calls without modifying evaluation or method bodies.</summary>
    public static class StaticCallRewriter
    {
        private const string AttributeName = "LuminPack.Generated.StaticCallAttribute";

        /// <summary>Applies generator-owned mappings to one consumer module.</summary>
        /// <param name="module">The compiled consumer module, optionally with symbols.</param>
        /// <returns>The number of rewritten call instructions.</returns>
        public static int Rewrite(ModuleDefinition module)
        {
            var mappings = new Dictionary<string, Mapping>(StringComparer.Ordinal);
            var marked = new List<MethodDefinition>();
            foreach (var method in module.GetTypes().SelectMany(type => type.Methods))
            {
                foreach (var attribute in method.CustomAttributes.Where(value => value.AttributeType.FullName == AttributeName))
                {
                    if (!method.IsStatic || attribute.ConstructorArguments.Count != 2)
                        LuminPackExceptionHelper.ThrowInvalidOperationException("Invalid static call mapping on " + method.FullName);
                    string key = (string)attribute.ConstructorArguments[0].Value;
                    int profile = (int)attribute.ConstructorArguments[1].Value;
                    if (mappings.ContainsKey(key))
                        LuminPackExceptionHelper.ThrowInvalidOperationException("Ambiguous static call mapping: " + key);
                    mappings.Add(key, new Mapping(method, profile));
                    marked.Add(method);
                }
            }
            if (mappings.Count == 0)
                return 0;
            ValidateDefinitions(module);
            int changed = 0;
            foreach (var method in module.GetTypes().SelectMany(type => type.Methods).Where(method => method.HasBody))
            {
                foreach (var instruction in method.Body.Instructions)
                {
                    if (instruction.OpCode != OpCodes.Call || instruction.Operand is not MethodReference original || original.HasThis)
                        continue;
                    if (!mappings.TryGetValue(Key(original, -1), out var mapping) &&
                        (!(original is GenericInstanceMethod generic) || generic.GenericArguments.Count == 0 ||
                         !mappings.TryGetValue(Key(original, generic.GenericArguments.Count - 1), out mapping)))
                        continue;
                    var definition = original.Resolve();
                    if (definition == null || definition.Module != module || !definition.IsStatic)
                        continue;
                    MethodReference target = mapping.Method;
                    if (mapping.Profile >= 0)
                    {
                        var construction = original as GenericInstanceMethod;
                        if (construction == null || mapping.Profile >= construction.GenericArguments.Count ||
                            target.GenericParameters.Count != construction.GenericArguments.Count)
                            LuminPackExceptionHelper.ThrowInvalidOperationException("Invalid options forwarding for " + original.FullName);
                        var closed = new GenericInstanceMethod(target);
                        foreach (var argument in construction.GenericArguments)
                            closed.GenericArguments.Add(argument);
                        target = closed;
                    }
                    ValidateStack(original, target);
                    instruction.Operand = target;
                    changed++;
                }
            }
            foreach (var method in marked)
                for (int index = method.CustomAttributes.Count - 1; index >= 0; index--)
                    if (method.CustomAttributes[index].AttributeType.FullName == AttributeName)
                        method.CustomAttributes.RemoveAt(index);
            return changed;
        }

        /// <summary>Rejects metadata-name ambiguities rather than choosing an arbitrary overload.</summary>
        /// <param name="module">The module whose local definitions can be rewritten.</param>
        private static void ValidateDefinitions(ModuleDefinition module)
        {
            foreach (var type in module.GetTypes())
            {
                var keys = new HashSet<string>(StringComparer.Ordinal);
                foreach (var method in type.Methods.Where(method => method.IsStatic))
                    if (!keys.Add(Key(method, -1)))
                        LuminPackExceptionHelper.ThrowInvalidOperationException("Ambiguous local metadata signature on " + type.FullName);
            }
        }

        /// <summary>Checks exact stack types, including assembly scopes, before replacing an operand.</summary>
        /// <param name="original">The original static call.</param>
        /// <param name="target">The generated static entry.</param>
        private static void ValidateStack(MethodReference original, MethodReference target)
        {
            if (original.Parameters.Count != target.Parameters.Count ||
                StackType(original.ReturnType, original) != StackType(target.ReturnType, target))
                LuminPackExceptionHelper.ThrowInvalidOperationException("Static call return or argument count mismatch: " + original.FullName);
            for (int index = 0; index < original.Parameters.Count; index++)
                if (StackType(original.Parameters[index].ParameterType, original) != StackType(target.Parameters[index].ParameterType, target))
                    LuminPackExceptionHelper.ThrowInvalidOperationException("Static call argument mismatch: " + original.FullName);
        }

        /// <summary>Substitutes method and owner arguments while retaining by-reference stack contracts.</summary>
        /// <param name="type">The signature type.</param>
        /// <param name="method">The call's complete generic construction.</param>
        /// <returns>A scoped stack type identity.</returns>
        private static string StackType(TypeReference type, MethodReference method)
        {
            if (type is GenericParameter parameter)
            {
                TypeReference actual = parameter.Type == GenericParameterType.Method && method is GenericInstanceMethod generic
                    ? generic.GenericArguments[parameter.Position]
                    : parameter.Type == GenericParameterType.Type && method.DeclaringType is GenericInstanceType owner
                        ? owner.GenericArguments[parameter.Position] : parameter;
                return TypeKey(actual, true);
            }
            if (type is GenericInstanceType instance)
                return TypeKey(instance.ElementType, true) + "<" + string.Join(",", instance.GenericArguments.Select(argument => StackType(argument, method))) + ">";
            if (type is ByReferenceType byReference)
                return StackType(byReference.ElementType, method) + "&";
            if (type is ArrayType array)
                return StackType(array.ElementType, method) + "[" + new string(',', array.Rank - 1) + "]";
            if (type is PointerType pointer)
                return StackType(pointer.ElementType, method) + "*";
            if (type is IModifierType modifier)
                return StackType(modifier.ElementType, method);
            return TypeKey(type, true);
        }

        /// <summary>Builds the same definition-and-construction key as the source generator.</summary>
        /// <param name="method">The compiled call or local definition.</param>
        /// <param name="profile">The wildcard options argument, or minus one.</param>
        /// <returns>The generator mapping key.</returns>
        private static string Key(MethodReference method, int profile)
        {
            var definition = method is GenericInstanceMethod instance ? instance.ElementMethod : method;
            var arguments = method is GenericInstanceMethod construction ? construction.GenericArguments.Cast<TypeReference>() : Enumerable.Empty<TypeReference>();
            return TypeKey(method.DeclaringType) + "::" + method.Name + "`" + definition.GenericParameters.Count + "(" +
                string.Join(",", definition.Parameters.Select(parameter => TypeKey(parameter.ParameterType))) + ")->" + TypeKey(definition.ReturnType) + "|" +
                string.Join(",", arguments.Select((argument, index) => index == profile ? "*" : TypeKey(argument)));
        }

        /// <summary>Formats an ECMA signature independently of source spelling and debug information.</summary>
        /// <param name="type">The metadata type.</param>
        /// <param name="scoped">Whether to include the assembly identity for stack verification.</param>
        /// <returns>The canonical signature shape.</returns>
        private static string TypeKey(TypeReference type, bool scoped = false)
        {
            if (type is GenericParameter parameter)
                return (parameter.Type == GenericParameterType.Method ? "!!" : "!") + parameter.Position;
            if (type is GenericInstanceType instance)
                return TypeKey(instance.ElementType, scoped) + "<" + string.Join(",", instance.GenericArguments.Select(argument => TypeKey(argument, scoped))) + ">";
            if (type is ByReferenceType byReference)
                return TypeKey(byReference.ElementType, scoped) + "&";
            if (type is ArrayType array)
                return TypeKey(array.ElementType, scoped) + "[" + new string(',', array.Rank - 1) + "]";
            if (type is PointerType pointer)
                return TypeKey(pointer.ElementType, scoped) + "*";
            if (type is IModifierType modifier)
                return TypeKey(modifier.ElementType, scoped);
            return (scoped ? "[" + (type.Scope is ModuleDefinition module ? module.Assembly.Name.Name : type.Scope.Name.Replace(".dll", "")) + "]" : "") + type.FullName;
        }

        /// <summary>A generated target and its sole optional profile argument.</summary>
        private sealed class Mapping
        {
            internal readonly MethodDefinition Method;
            internal readonly int Profile;

            /// <summary>Captures a validated generated mapping.</summary>
            /// <param name="method">The concrete static target.</param>
            /// <param name="profile">The original profile position, or minus one.</param>
            internal Mapping(MethodDefinition method, int profile)
            {
                Method = method;
                Profile = profile;
            }
        }
    }
}
