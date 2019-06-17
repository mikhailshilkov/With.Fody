using Fody;
using Mono.Cecil;
using Mono.Cecil.Cil;
using System;
using System.Collections.Generic;
using System.Linq;

public class ModuleWeaver : BaseModuleWeaver
{
    public override IEnumerable<string> GetAssembliesForScanning() => Enumerable.Empty<string>();

    public override void Execute()
    {
        foreach (var type in ModuleDefinition.GetTypes()
            .Where(type => 
                type.Name != "<Module>" && 
                type.Methods.Any(m => m.IsPublic && m.Name.StartsWith("With"))))
        {
            try
            {
                var ctor = GetValidConstructor(type);
                if (ctor is object)
                {
                    AddWith(type, ctor);
                    RemoveGenericWith(type);
                    LogInfo($"Added method 'With' to type '{type.Name}'.");
                }
            }
            catch (AssemblyResolutionException ex) {
                LogInfo($"Method 'With' not added to type '{type.Name}'. Failed to resolve assembly '{ex.AssemblyReference.FullName}'.");
            }
        }
    }

    private static bool IsPair(PropertyDefinition pro, ParameterDefinition par)
    {
        return 
            par.ParameterType.FullName == pro.PropertyType.FullName &&
            String.Compare(par.Name, pro.Name, StringComparison.InvariantCultureIgnoreCase) == 0;
    }

    private MethodDefinition GetValidConstructor(TypeDefinition type)
    {
        return type.Methods
            .Where(ctor => ctor.IsConstructor && ctor.Parameters.Count >= 2 && ctor.Parameters.All(par => GetAllProperties(type).Any(pro => IsPair(pro, par))))
            .Aggregate((MethodDefinition)null, (max, next) => next.Parameters.Count > (max?.Parameters.Count ?? -1) ? next : max);
    }

    private void RemoveGenericWith(TypeDefinition type)
    {
        if (type.HasMethods)
        {
            var count = type.Methods.Count;
            for (var index = 0; index < count; index++)
            {
                var method = type.Methods[index];
                if (method.IsPublic && method.HasGenericParameters && method.Name == "With")
                {
                    type.Methods.RemoveAt(index);
                    break;
                }
            }
        }
    }

    private void AddWith(TypeDefinition type, MethodDefinition ctor)
    {
        foreach (var withMethod in type.Methods.Where(m => m.IsPublic && m.Name.StartsWith("With")).ToArray())
        {
            if (withMethod.HasGenericParameters)
            {
                if (withMethod.Parameters.Count == 1)
                {
                    // create one 'with' method for each contructor parameter
                    foreach (var parameter in ctor.Parameters)
                    {
                        // step over if type contains explicit 'with' method for this parameter
                        var propertyName = ToPropertyName(parameter.Name);
                        var explicitName = $"With{propertyName}";
                        if (!type.Methods.Any(m => m.Name == explicitName))
                        {
                            var methodName = "With";
                            // append property name if another parameter with same type
                            if(ctor.Parameters
                                .Except(new[] { parameter })
                                .Any(p => p.ParameterType.FullName == parameter.ParameterType.FullName))
                            {
                                methodName += propertyName;
                            }

                            var method = new MethodDefinition(methodName, MethodAttributes.Public, type)
                            {
                                AggressiveInlining = true,
                                CustomAttributes =
                                {
                                    GeneratedCodeAttribute(),
                                },
                                Parameters =
                                {
                                    new ParameterDefinition(parameter.Name, ParameterAttributes.None, parameter.ParameterType),
                                },
                            };
                            type.Methods.Add(method);
                            AddWith(type, ctor, method);

                            this.ReplaceCalls(type, method, parameter.ParameterType);
                        }
                    }
                }
                else
                {
                    // do nothing
                    // any use case for a generic multi-parameter 'with' method?
                }
            }
            else
            {
                var parameterName = (string)null;
                if (withMethod.Parameters.Count == 1 && IsExplicitName(type, withMethod.Name, out parameterName))
                {
                    AddWith(type, ctor, withMethod, parameterName);
                }
                else
                {
                    AddWith(type, ctor, withMethod);
                }
            }
        }
    }

    public void AddWith(TypeDefinition type, MethodDefinition ctor, MethodDefinition withMethod, string parameterName)
    { 
        var processor = withMethod.Body.GetILProcessor();
        processor.Body.Instructions.Clear();
        foreach (var ctorParameter in ctor.Parameters)
        {
            if (String.Compare(parameterName, ctorParameter.Name, StringComparison.InvariantCultureIgnoreCase) == 0)
            {
                processor.Emit(OpCodes.Ldarg_1);
            }
            else
            {
                var getter = GetPropertyGetter(type, ctorParameter.Name);
                processor.Emit(OpCodes.Ldarg_0);
                processor.Emit(OpCodes.Call, getter);
            }
        }
        processor.Emit(OpCodes.Newobj, ctor);
        processor.Emit(OpCodes.Ret);
    }

    public void AddWith(TypeDefinition type, MethodDefinition ctor, MethodDefinition withMethod)
    {
        var processor = withMethod.Body.GetILProcessor();
        processor.Body.Instructions.Clear();
        foreach (var ctorParameter in ctor.Parameters)
        {
            var withParameter = withMethod.Parameters
                .Select((par, index) => new { Parameter = par, Index = index })
                .FirstOrDefault(item => 
                    item.Parameter.ParameterType.FullName == ctorParameter.ParameterType.FullName && 
                    String.Compare(item.Parameter.Name, ctorParameter.Name, StringComparison.InvariantCultureIgnoreCase) == 0);

            if (withParameter is object)
            {
                processor.Emit(OpCodes.Ldarg, withParameter.Index + 1);
            }
            else
            {
                var getter = GetPropertyGetter(type, ctorParameter.Name);
                processor.Emit(OpCodes.Ldarg_0);
                processor.Emit(OpCodes.Call, getter);
            }
        }
        processor.Emit(OpCodes.Newobj, ctor);
        processor.Emit(OpCodes.Ret);
    }

    private void ReplaceCalls(TypeDefinition withType, MethodDefinition newMethod, TypeReference argumentType)
    {
        foreach (var call in ModuleDefinition.GetTypes()
            .SelectMany(type => type.Methods.Where(method => method.HasBody))
            .SelectMany(method => method.Body.Instructions.Where(instruction => instruction.OpCode == OpCodes.Callvirt)))
        {
            var originalMethodReference = (MethodReference)call.Operand;
            if (originalMethodReference.IsGenericInstance)
            {
                var genericMethodReference = originalMethodReference as GenericInstanceMethod;
                var originalMethodDefinition = originalMethodReference.Resolve();
                var declaringTypeReference = originalMethodReference.DeclaringType;
                var declaringTypeDefinition = declaringTypeReference.Resolve();

                if (declaringTypeDefinition.FullName == withType.FullName
                    && originalMethodDefinition.Name == newMethod.Name
                    && genericMethodReference.GenericArguments[0] == argumentType)
                {
                    call.Operand = ModuleDefinition.ImportReference(newMethod);
                    LogInfo($"Found a call");
                }
            }
        }
    }

    private static string ToPropertyName(string fieldName)
    {
        return Char.ToUpperInvariant(fieldName[0]) + fieldName.Substring(1);
    }

    private bool IsExplicitName(TypeDefinition type, string methodName, out string parameterName)
    {
        if (!(methodName.Length > 4 && methodName.StartsWith("With")))
        {
            parameterName = null;
            return false;
        }

        parameterName = methodName.Substring(4);
        var name = parameterName; // required for lambda
        return GetAllProperties(type)
            .Any(pro => String.Compare(pro.Name, name, StringComparison.InvariantCultureIgnoreCase) == 0);
    }

    private IEnumerable<PropertyDefinition> GetAllProperties(TypeDefinition type)
    {
        // get recursively through the hierachy all the properties with a public getter
        return type.Properties
            .Where(pro => pro.GetMethod.IsPublic)
            .Concat(type.BaseType is null ?
                Enumerable.Empty<PropertyDefinition>() :
                GetAllProperties(type.BaseType));
    }

    private IEnumerable<PropertyDefinition> GetAllProperties(TypeReference type)
    {
        // import type if it's in a referenced assembly
        if (type.Scope is AssemblyNameReference assemblyName)
        {
            var assembly = ResolveAssembly(assemblyName.Name);
            type = assembly.MainModule.GetType(type.FullName);
            ModuleDefinition.ImportReference(type);
        }

        return GetAllProperties(type.Resolve());
    }

    private MethodReference GetPropertyGetter(TypeDefinition type, string name)
    {
        // get the getter for the property anywhere in the hierachy with the given name
        var getter = GetAllProperties(type)
            .First(pro => String.Compare(pro.Name, name, StringComparison.InvariantCultureIgnoreCase) == 0)
            .GetMethod;
        return ModuleDefinition.ImportReference(getter);
    }

    private CustomAttribute GeneratedCodeAttribute()
    {
        var assembly = GetType().Assembly;
        var assemblyName = assembly.GetName().Name;
        var assemblyVersion = ((System.Reflection.AssemblyFileVersionAttribute)Attribute.GetCustomAttribute(assembly, typeof(System.Reflection.AssemblyFileVersionAttribute), false)).Version;
        return new CustomAttribute(
            ModuleDefinition.ImportReference(typeof(System.CodeDom.Compiler.GeneratedCodeAttribute).GetConstructor(new[] { typeof(string), typeof(string) })))
            {
                ConstructorArguments = {
                    new CustomAttributeArgument(ModuleDefinition.TypeSystem.String, assemblyName),
                    new CustomAttributeArgument(ModuleDefinition.TypeSystem.String, assemblyVersion),
                }
            };
    }
}