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

    // Will log an error message to MSBuild. 
    public Action<string> LogError { get; set; }

    // An instance of Mono.Cecil.IAssemblyResolver for resolving assembly references. 
    public IAssemblyResolver AssemblyResolver { get; set; }

    // An instance of Mono.Cecil.ModuleDefinition for processing
    public ModuleDefinition ModuleDefinition { get; set; }

    // Init logging delegates to make testing easier
    public ModuleWeaver()
    {
        LogInfo = m => { };
        LogError = m => { };
    }

    public void Execute()
    {
        foreach (var type in ModuleDefinition.Types
            .Where(type => type.GetMethods().Any(m => m.IsPublic && m.Name.StartsWith("With"))))
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
                LogError($"Failed to resolve assembly '{ex.AssemblyReference.FullName}'.");
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
        foreach (var property in ctor.Parameters)
        {
            var parameterName = property.Name;
            var getter = GetPropertyGetter(type, parameterName);

            var propertyName = ToPropertyName(property.Name);
            MethodDefinition method;
            var explicitName = $"With{propertyName}";
            if (type.Methods.Any(m => m.Name == explicitName)
                || ctor.Parameters.Except(new[] { property }).Any(p => p.ParameterType.FullName == property.ParameterType.FullName))
            {
                method = type.Methods.FirstOrDefault(m => m.Name == explicitName);
                if (method == null)
                    continue;
            }
            else
            {
                method = new MethodDefinition("With", MethodAttributes.Public, type);
                method.Parameters.Add(new ParameterDefinition(parameterName, ParameterAttributes.None, getter.ReturnType));
                type.Methods.Add(method);
            }

            var processor = method.Body.GetILProcessor();
            foreach (var i in processor.Body.Instructions.ToArray())
            {
                processor.Remove(i);
            }
            foreach (var parameter in ctor.Parameters)
            {
                if (parameter.Name == parameterName)
                {
                    processor.Emit(OpCodes.Ldarg_1);
                }
                else
                {
                    var getterParameter = GetPropertyGetter(type, parameter.Name);
                    processor.Emit(OpCodes.Ldarg_0);
                    processor.Emit(OpCodes.Call, getterParameter);
                }
            }
            processor.Emit(OpCodes.Newobj, ctor);
            processor.Emit(OpCodes.Ret);
            method.Body.OptimizeMacros();

            this.ReplaceCalls(type, method, getter.ReturnType);
        }
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

    private TypeDefinition ResolveType(TypeReference type)
    {
        //var scope = type.Scope;
        //if(scope is ModuleDefinition)
        //{
        //    var moduleDefinition = (ModuleDefinition)scope;
        //    var baseTypeAssembly = AssemblyResolver.Resolve(moduleDefinition.Assembly.FullName);
        //    return baseTypeAssembly.MainModule.GetType(type.FullName);
        //}

        return type.Resolve();
    }


    private IEnumerable<PropertyDefinition> GetAllProperties(TypeDefinition type)
    {
        // get recursively through the hierachy all the properties with a public getter
        return type.Properties.Where(pro => pro.GetMethod.IsPublic)
            .Concat(type.BaseType == null ?
                Enumerable.Empty<PropertyDefinition>() :
                GetAllProperties(ResolveType(type.BaseType)));
    }

    private MethodDefinition GetPropertyGetter(TypeDefinition type, string name)
    {
        // get the getter for the property anywhere in the hierachy with the given name
        return GetAllProperties(type)
            .First(pro => String.Compare(pro.Name, name, StringComparison.InvariantCultureIgnoreCase) == 0)
            .GetMethod;
    }

}