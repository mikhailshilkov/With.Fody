using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Mono.Cecil;
using NUnit.Framework;

[TestFixture]
public class WeaverTests
{
#if (DEBUG)
    const string Configuration = "Debug";
#else
    const string Configuration = "Release";
#endif

    Assembly assembly;
    string newAssemblyPath;
    string assemblyPath;

    [TestFixtureSetUp]
    public void Setup()
    {
        var localPath = new Uri(Path.GetDirectoryName(Assembly.GetExecutingAssembly().CodeBase)).LocalPath;
        var assemblyProcessBinPath = Path.GetFullPath(Path.Combine(localPath, @"..\..\..\AssemblyToProcess\bin\" + Configuration));
        assemblyPath = Path.Combine(assemblyProcessBinPath, "AssemblyToProcess.dll");

        newAssemblyPath = assemblyPath.Replace(".dll", "2.dll");
        File.Copy(assemblyPath, newAssemblyPath, true);

        var assemblyResolver = new DefaultAssemblyResolver();
        assemblyResolver.AddSearchDirectory(assemblyProcessBinPath);

        var moduleDefinition = ModuleDefinition.ReadModule(newAssemblyPath);

        var weavingTask = new ModuleWeaver
        {
            ModuleDefinition = moduleDefinition,
            AssemblyResolver = assemblyResolver,
        };

        weavingTask.Execute();
        moduleDefinition.Write(newAssemblyPath);

        assembly = Assembly.LoadFile(newAssemblyPath);

        AppDomain.CurrentDomain.AssemblyResolve += (sender, e) =>
        {
            return Assembly.LoadFrom(Path.Combine(assemblyProcessBinPath, @"ReferencedAssembly.dll"));
        };
    }

    [TestCase("NoConstructor")]
    [TestCase("ConstructorWithSingleArgument")]
    [TestCase("NoMatchingProperty")]
    [TestCase("NoWithStub")]
    public void DoesNotSatisfyAllRules_NoWithIsInjected(string typeName)
    {
        var type = assembly.GetType($"AssemblyToProcess.{typeName}");
        Assert.False(type.GetMethods().Any(m => m.Name.StartsWith("With") && !(m.IsGenericMethod && m.GetParameters().Length == 1)));
    }

    [TestCase("MultipleConstructors")]
    [TestCase("MultipleConstructors2")]
    public void MultipleConstructors_WithIsInjected(string typeName)
    {
        var type = assembly.GetType($"AssemblyToProcess.{typeName}");
        var instance = (dynamic)Activator.CreateInstance(type, new object[] { 1, "Hello", (long)234234 });

        var result1 = instance.With(123);
        Assert.AreEqual(123, result1.Value1);
        Assert.AreEqual(instance.Value2, result1.Value2);
        Assert.AreEqual(instance.Value3, result1.Value3);

        var result2 = instance.With("World");
        Assert.AreEqual(instance.Value1, result2.Value1);
        Assert.AreEqual("World", result2.Value2);
        Assert.AreEqual(instance.Value3, result1.Value3);

        var result3 = instance.With((long)31231);
        Assert.AreEqual(instance.Value1, result3.Value1);
        Assert.AreEqual(instance.Value2, result3.Value2);
        Assert.AreEqual(31231, result3.Value3);
    }

    [TestCase("Inheritance")]
    [TestCase("InheritanceFromAnotherAssembly")]
    public void Inheritance_WithIsInjected(string typeName)
    {
        var type = assembly.GetType($"AssemblyToProcess.{typeName}");
        var instance = (dynamic)Activator.CreateInstance(type, new object[] { 1, "Hello", 234234L });

        var result1 = instance.With(123);
        Assert.AreEqual(123, result1.Value1);
        Assert.AreEqual(instance.Value2, result1.Value2);
        Assert.AreEqual(instance.Value3, result1.Value3);

        var result2 = instance.With("World");
        Assert.AreEqual(instance.Value1, result2.Value1);
        Assert.AreEqual("World", result2.Value2);
        Assert.AreEqual(instance.Value3, result1.Value3);

        var result3 = instance.With(31231L);
        Assert.AreEqual(instance.Value1, result3.Value1);
        Assert.AreEqual(instance.Value2, result3.Value2);
        Assert.AreEqual(31231L, result3.Value3);
    }

    [Test]
    public void MultipleConstructorsOnlyOneIsPublic_WithIsInjected()
    {
        var type = assembly.GetType("AssemblyToProcess.MultipleConstructorsOnlyOneIsPublic");
        var instance = (dynamic)Activator.CreateInstance(type, new object[] { 1, "Hello" });

        var result1 = instance.With(123);
        Assert.AreEqual(123, result1.Value1);
        Assert.AreEqual(instance.Value2, result1.Value2);

        var result2 = instance.With("World");
        Assert.AreEqual(instance.Value1, result2.Value1);
        Assert.AreEqual("World", result2.Value2);
    }

    [Test]
    public void NoMatchingParameter_WithIsInjectedForOtherParameters()
    {
        var type = assembly.GetType("AssemblyToProcess.NoMatchingParameter");
        var instance = (dynamic)Activator.CreateInstance(type, new object[] { 1, 2 });

        var result1 = instance.WithValue1(111);
        Assert.AreEqual(111, result1.Value1);
        Assert.AreEqual(instance.Value2, result1.Value2);
        Assert.AreEqual(result1.Value1 + result1.Value2, result1.Sum);

        var result2 = instance.WithValue2(222);
        Assert.AreEqual(instance.Value1, result2.Value1);
        Assert.AreEqual(222, result2.Value2);
        Assert.AreEqual(result2.Value1 + result2.Value2, result2.Sum);

        Assert.False(type.GetMethods().Any(m => m.Name == "WithSum"));
    }

    [Test]
    public void PrimitiveValues_ShortWithIsInjected()
    {
        var type = assembly.GetType("AssemblyToProcess.PrimitiveValues");
        var instance = (dynamic)Activator.CreateInstance(type, new object[] { 1, "Hello", (long)234234 });

        var result1 = instance.With(123);
        Assert.AreEqual(123, result1.Value1);
        Assert.AreEqual(instance.Value2, result1.Value2);
        Assert.AreEqual(instance.Value3, result1.Value3);

        var result2 = instance.With("World");
        Assert.AreEqual(instance.Value1, result2.Value1);
        Assert.AreEqual("World", result2.Value2);
        Assert.AreEqual(instance.Value3, result2.Value3);

        var result3 = instance.With((long)31231);
        Assert.AreEqual(instance.Value1, result3.Value1);
        Assert.AreEqual(instance.Value2, result3.Value2);
        Assert.AreEqual(31231, result3.Value3);
    }

    [Test]
    public void PropertiesOfSameType_LongNamedWithIsInjected()
    {
        var type = assembly.GetType("AssemblyToProcess.PropertiesOfSameType");
        var instance = (dynamic)Activator.CreateInstance(type, new object[] { 1, 2, 3 });

        var result1 = instance.WithValue1(111);
        Assert.AreEqual(111, result1.Value1);
        Assert.AreEqual(instance.Value2, result1.Value2);
        Assert.AreEqual(instance.Value3, result1.Value3);

        var result2 = instance.WithValue2(222);
        Assert.AreEqual(instance.Value1, result2.Value1);
        Assert.AreEqual(222, result2.Value2);
        Assert.AreEqual(instance.Value3, result2.Value3);

        var result3 = instance.WithValue3(333);
        Assert.AreEqual(instance.Value1, result3.Value1);
        Assert.AreEqual(instance.Value2, result3.Value2);
        Assert.AreEqual(333, result3.Value3);
    }

    [Test]
    public void UnusualPropertyCasing_WithIsInjectedAnyway()
    {
        var type = assembly.GetType("AssemblyToProcess.PropertyCasing");
        var instance = (dynamic)Activator.CreateInstance(type, new object[] { 1, "Hello" });

        var result1 = instance.With(123);
        Assert.AreEqual(123, result1.VALUE1);
        Assert.AreEqual(instance.vaLue2, result1.vaLue2);

        var result2 = instance.With("World");
        Assert.AreEqual(instance.VALUE1, result2.VALUE1);
        Assert.AreEqual("World", result2.vaLue2);
    }

    [Test]
    public void InAssemblyUsage_Works()
    {
        var type = assembly.GetType("AssemblyToProcess.PrimitiveValues");
        var instance = (dynamic)Activator.CreateInstance(type, new object[] { 1, "Hello", (long)234234 });
        var runner = (dynamic)Activator.CreateInstance(assembly.GetType("AssemblyToProcess.InAssemblyUsage"));
        var result = runner.ChangeIntTo3(instance);
        Assert.AreEqual(3, result.Value1);

        type = assembly.GetType("AssemblyToProcess.PropertiesOfSameType");
        instance = (dynamic)Activator.CreateInstance(type, new object[] { 1, 2, 3 });
        result = runner.ChangeValue1To33(instance);
        Assert.AreEqual(33, result.Value1);
    }

    [Test]
    public void UniquePropertyTypeButExplicitMethodName_WithIsEmittedWithLongName()
    {
        var type = assembly.GetType("AssemblyToProcess.ExplicitlyAskedForFullName");
        var instance = (dynamic)Activator.CreateInstance(type, new object[] { 1, "Hello" });

        var result1 = instance.WithValue1(123);
        Assert.AreEqual(123, result1.Value1);
        Assert.AreEqual(instance.Value2, result1.Value2);

        var result2 = instance.WithValue2("World");
        Assert.AreEqual(instance.Value1, result2.Value1);
        Assert.AreEqual("World", result2.Value2);
    }

    [Test]
    public void OriginalWithMethodIsRemoved()
    {
        var type1 = assembly.GetType("AssemblyToProcess.PrimitiveValues");
        Assert.False(type1.GetMethods().Any(m => m.Name == "With" && m.IsGenericMethod));
    }

#if(DEBUG)
    [Test]
    public void PeVerify()
    {
        Verifier.Verify(assemblyPath,newAssemblyPath);
    }
#endif
}