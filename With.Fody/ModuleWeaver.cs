using System;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Rocks;
using Mono.Cecil.Cil;
using System.Collections.Generic;

public class ModuleWeaver
{
    // Will log an informational message to MSBuild
    public Action<string> LogInfo { get; set; } 

    // An instance of Mono.Cecil.IAssemblyResolver for resolving assembly references. 
    public IAssemblyResolver AssemblyResolver { get; set; }

    // An instance of Mono.Cecil.ModuleDefinition for processing
    public ModuleDefinition ModuleDefinition { get; set; }

    // Init logging delegates to make testing easier
    public ModuleWeaver()
    {
        LogInfo = m => { };
    }

    public void Execute()
    {
        foreach (var type in ModuleDefinition.Types
            .Where(type => 
                type.Name != "<Module>" && 
                type.GetMethods().Any(m => m.IsPublic && m.Name.StartsWith("With"))))
        {
            try
            {
                var ctor = GetValidConstructor(type);
                if (ctor != null)
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
        return type.GetConstructors()
            .Where(ctor => ctor.Parameters.Count >= 2 && ctor.Parameters.All(par => GetAllProperties(type).Any(pro => IsPair(pro, par))))
            .Aggregate((MethodDefinition)null, (max, next) => next.Parameters.Count > (max?.Parameters.Count ?? -1) ? next : max);
    }

    private void RemoveGenericWith(TypeDefinition type)
    {
        foreach (var method in type.GetMethods().Where(m => m.IsPublic && m.Name == "With" && m.HasGenericParameters).ToArray())
        {
            type.Methods.Remove(method);
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

                            var method = new MethodDefinition(methodName, MethodAttributes.Public, type);
                            method.Parameters.Add(new ParameterDefinition(parameter.Name, ParameterAttributes.None, parameter.ParameterType));
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
                if (withMethod.Parameters.Count == 1 && IsExplicitName(withMethod.Name, out parameterName))
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
        foreach (var i in processor.Body.Instructions.ToArray())
        {
            processor.Remove(i);
        }
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
        withMethod.Body.OptimizeMacros();
    }

    public void AddWith(TypeDefinition type, MethodDefinition ctor, MethodDefinition withMethod)
    {
        var processor = withMethod.Body.GetILProcessor();
        foreach (var i in processor.Body.Instructions.ToArray())
        {
            processor.Remove(i);
        }
        foreach (var ctorParameter in ctor.Parameters)
        {
            var withParameter = withMethod.Parameters
                .Select((par, index) => new { Parameter = par, Index = index })
                .FirstOrDefault(item => 
                    item.Parameter.ParameterType.FullName == ctorParameter.ParameterType.FullName && 
                    String.Compare(item.Parameter.Name, ctorParameter.Name, StringComparison.InvariantCultureIgnoreCase) == 0);

            if (withParameter != null)
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
        withMethod.Body.OptimizeMacros();
    }

    private void ReplaceCalls(TypeDefinition withType, MethodDefinition newMethod, TypeReference argumentType)
    {
        foreach (var type in ModuleDefinition.Types)
        {
            foreach (var method in type.Methods.Where(x => x.Body != null))
            {
                var calls = method.Body.Instructions.Where(i => i.OpCode == OpCodes.Callvirt);
                foreach (var call in calls)
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
        }
    }

    private static string ToPropertyName(string fieldName)
    {
        return Char.ToUpperInvariant(fieldName[0]) + fieldName.Substring(1);
    }

    private static bool IsExplicitName(string methodName, out string parameterName)
    {
        if (!(methodName.Length > 4 && methodName.StartsWith("With")))
        {
            parameterName = null;
            return false;
        }

        parameterName = methodName.Substring(4);
        return true;
    }

    private IEnumerable<PropertyDefinition> GetAllProperties(TypeDefinition type)
    {
        // get recursively through the hierachy all the properties with a public getter
        return type.Properties
            .Where(pro => pro.GetMethod.IsPublic)
            .Concat(type.BaseType == null ?
                Enumerable.Empty<PropertyDefinition>() :
                GetAllProperties(type.BaseType));
    }

    private IEnumerable<PropertyDefinition> GetAllProperties(TypeReference type)
    {
        // import type if it's in a referenced assembly
        var assemblyName = type.Scope as AssemblyNameReference;
        if (assemblyName != null)
        {
            var assembly = AssemblyResolver.Resolve(assemblyName);
            type = assembly.MainModule.GetType(type.FullName);
            ModuleDefinition.ImportReference(type);
        }

        return GetAllProperties(type.Resolve());
    }

    private MethodReference GetPropertyGetter(TypeDefinition type, string name)
    {
        // get the getter for the property anywhere in the hierachy with the given name
        var property = GetAllProperties(type)
            .FirstOrDefault(pro => String.Compare(pro.Name, name, StringComparison.InvariantCultureIgnoreCase) == 0);
        if (property == null)
        {
            return null;
        }
        return ModuleDefinition.ImportReference(property.GetMethod);
    }
}