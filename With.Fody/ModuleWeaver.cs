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

    // An instance of Mono.Cecil.ModuleDefinition for processing
    public ModuleDefinition ModuleDefinition { get; set; }

    // Init logging delegates to make testing easier
    public ModuleWeaver()
    {
        LogInfo = m => { };
    }

    public void Execute()
    {
        foreach (var type in ModuleDefinition.Types)
        {
            if (type.GetMethods().Any(m => m.IsPublic && m.Name.StartsWith("With")))
            {
                var ctor = GetValidConstructor(type);
                if (ctor != null)
                {
                    AddWith(type, ctor);
                    RemoveGenericWith(type);
                    LogInfo($"Added method 'With' to type '{type.Name}'.");
                }
            }
        }
    }

    private static MethodDefinition GetValidConstructor(TypeDefinition type)
    {
        return type.GetConstructors()
            .Where(ctor =>
                ctor.Parameters.Count >= 2 &&
                ctor.Parameters.All(par =>
                    type.Properties.Any(pro =>
                        string.Compare(par.Name, pro.Name, StringComparison.InvariantCultureIgnoreCase) == 0
                    )
                )
            )
            .FirstOrDefault();
    }

    private void RemoveGenericWith(TypeDefinition type)
    {
        var method = type.GetMethods()
            .Where(m =>
                m.IsPublic &&
                m.Name == "With" &&
                m.Parameters.Count == 1 &&
                m.Parameters[0].ParameterType.FullName == "System.Object")
            .FirstOrDefault();

        if(method != null)
        {
            type.Methods.Remove(method);
        }
    }

    private void AddWith(TypeDefinition type, MethodDefinition ctor)
    {
        foreach (var property in ctor.Parameters)
        {
            var parameterName = property.Name;
            var getter = type.Methods.First(m => m.IsGetter && string.Compare(m.Name, $"get_{property.Name}", StringComparison.InvariantCultureIgnoreCase) == 0);

            string propertyName = ToPropertyName(property.Name);
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
                    var getterParameter = type.Methods.First(m => m.IsGetter && string.Compare(m.Name, $"get_{parameter.Name}", StringComparison.InvariantCultureIgnoreCase) == 0);
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


    private string ToPropertyName(string fieldName)
    {
        return Char.ToUpperInvariant(fieldName[0]) + fieldName.Substring(1);
    }
}