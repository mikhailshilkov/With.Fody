using System;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Rocks;
using Mono.Cecil.Cil;

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
        foreach (var type in ModuleDefinition.Types.Where(CanHaveWith))
        {
            AddWith(type);
            RemoveGenericWith(type);
            LogInfo($"Added method 'With' to type '{type.Name}'.");
        }
    }

    private bool CanHaveWith(TypeDefinition type)
    {
        if (!type.GetMethods().Any(m => m.Name.StartsWith("With")))
        {
            return false;
        }

        var ctors = type.GetConstructors().ToArray();
        if (ctors.Length != 1)
        {
            return false;
        }

        var ctor = ctors[0];
        var parameters = ctor.Parameters;
        if (parameters.Count < 2)
        {
            return false;
        }

        return parameters.All(par => type.Properties.Any(pro => string.Compare(par.Name, pro.Name, StringComparison.InvariantCultureIgnoreCase) == 0));
    }

    private void RemoveGenericWith(TypeDefinition type)
    {
        foreach (var method in type.GetMethods().Where(m => m.Name == "With" && m.HasGenericParameters).ToArray())
        {
            type.Methods.Remove(method);
        }
    }

    private void AddWith(TypeDefinition type)
    {
        var ctor = type.GetConstructors().First();
        foreach (var property in ctor.Parameters)
        {
            var parameterName = property.Name;
            var getter = type.Methods.First(m => string.Compare(m.Name, $"get_{property.Name}", StringComparison.InvariantCultureIgnoreCase) == 0);

            string propertyName = ToPropertyName(property.Name);
            MethodDefinition method;
            bool existing = false;
            if (ctor.Parameters.Except(new[] { property }).Any(p => p.ParameterType.FullName == property.ParameterType.FullName))
            {
                var methodName = $"With{propertyName}";
                method = type.Methods.FirstOrDefault(m => m.Name == methodName);
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
                    var getterParameter = type.GetMethods().First(m => string.Compare(m.Name, $"get_{parameter.Name}", StringComparison.InvariantCultureIgnoreCase) == 0);
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