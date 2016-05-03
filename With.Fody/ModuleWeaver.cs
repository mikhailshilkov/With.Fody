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

    TypeSystem typeSystem;

    // Init logging delegates to make testing easier
    public ModuleWeaver()
    {
        LogInfo = m => { };
    }

    public void Execute()
    {
        typeSystem = ModuleDefinition.TypeSystem;

        foreach (var type in ModuleDefinition.Types.Where(CanHaveWith))
        {
            AddWith(type);
            LogInfo($"Added method 'With' to type '{type.Name}'.");
        }
    }

    private bool CanHaveWith(TypeDefinition type)
    {
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

        return parameters.All(par => type.Properties.Any(pro => ToPropertyName(par.Name) == pro.Name));
    }

    private void AddWith(TypeDefinition type)
    {
        var ctor = type.GetConstructors().First();
        foreach (var property in ctor.Parameters)
        {
            var parameterName = property.Name;
            string propertyName = ToPropertyName(property.Name);
            var getter = type.GetMethods().Where(m => m.Name == $"get_{propertyName}").First();

            var methodName = ctor.Parameters.Except(new[] { property }).Any(p => p.ParameterType == property.ParameterType) 
                ? $"With{propertyName}" : "With";
            var method = new MethodDefinition(methodName, MethodAttributes.Public, type);
            method.Parameters.Add(new ParameterDefinition(parameterName, ParameterAttributes.None, getter.ReturnType));

            var processor = method.Body.GetILProcessor();
            foreach (var parameter in ctor.Parameters)
            {
                if (parameter.Name == parameterName)
                {
                    processor.Emit(OpCodes.Ldarg_1);
                }
                else
                {
                    var getterParameter = type.GetMethods().Where(m => m.Name == $"get_{ToPropertyName(parameter.Name)}").First();
                    processor.Emit(OpCodes.Ldarg_0);
                    processor.Emit(OpCodes.Call, getterParameter);
                }
            }
            processor.Emit(OpCodes.Newobj, ctor);
            processor.Emit(OpCodes.Ret);
            type.Methods.Add(method);
        }
    }

    private string ToPropertyName(string fieldName)
    {
        return Char.ToUpperInvariant(fieldName[0]) + fieldName.Substring(1);
    }
}