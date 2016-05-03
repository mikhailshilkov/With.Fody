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
            RemoveWith(type);
            AddWith(type);
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

    private void RemoveWith(TypeDefinition type)
    {
        foreach (var method in type.GetMethods().Where(m => m.Name.StartsWith("With")).ToArray())
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
            var getter = type.GetMethods().First(m => string.Compare(m.Name, $"get_{property.Name}", StringComparison.InvariantCultureIgnoreCase) == 0);

            string propertyName = ToPropertyName(property.Name);
            var methodName = ctor.Parameters.Except(new[] { property }).Any(p => p.ParameterType.FullName == property.ParameterType.FullName) 
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
                    var getterParameter = type.GetMethods().First(m => string.Compare(m.Name, $"get_{parameter.Name}", StringComparison.InvariantCultureIgnoreCase) == 0);
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