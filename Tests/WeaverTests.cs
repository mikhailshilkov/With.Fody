using NUnit.Framework;
using System;
using System.IO;
using System.Linq;
using System.Reflection;

[TestFixture]
public class WeaverTests
{
    [TestCase(typeof(AssemblyToProcess.NoConstructor))]
    [TestCase(typeof(AssemblyToProcess.ConstructorWithSingleArgument))]
    [TestCase(typeof(AssemblyToProcess.NoMatchingProperty))]
    [TestCase(typeof(AssemblyToProcess.NoWithStub))]
    public void DoesNotSatisfyAllRules_NoWithIsInjected(Type type)
    {
        Assert.False(type.GetMethods().Any(m => m.Name.StartsWith("With") && !(m.IsGenericMethod && m.GetParameters().Length == 1)));
    }

    [TestCase(typeof(AssemblyToProcess.MultipleConstructors))]
    [TestCase(typeof(AssemblyToProcess.MultipleConstructors2))]
    public void MultipleConstructors_WithIsInjected(Type type)
    {
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

    [TestCase(typeof(AssemblyToProcess.Inheritance))]
    [TestCase(typeof(AssemblyToProcess.InheritanceFromAnotherAssembly))]
    public void Inheritance_WithIsInjected(Type type)
    {
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
        var type = typeof(AssemblyToProcess.MultipleConstructorsOnlyOneIsPublic);
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
        var type = typeof(AssemblyToProcess.NoMatchingParameter);
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
        var type = typeof(AssemblyToProcess.PrimitiveValues);
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

        var result4 = instance.With(123, "World");
        Assert.AreEqual(123, result4.Value1);
        Assert.AreEqual("World", result4.Value2);
        Assert.AreEqual(instance.Value3, result4.Value3);

        var result5 = instance.With(123, (long)31231);
        Assert.AreEqual(123, result5.Value1);
        Assert.AreEqual(instance.Value2, result5.Value2);
        Assert.AreEqual(31231, result5.Value3);

        var result6 = instance.WithSecondAndThird("World", (long)31231);
        Assert.AreEqual(instance.Value1, result6.Value1);
        Assert.AreEqual("World", result6.Value2);
        Assert.AreEqual(31231, result6.Value3);
    }

    [Test]
    public void PropertiesOfSameType_LongNamedWithIsInjected()
    {
        var type = typeof(AssemblyToProcess.PropertiesOfSameType);
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

        var result4 = instance.WithValue1Value2(111, 222);
        Assert.AreEqual(111, result4.Value1);
        Assert.AreEqual(222, result4.Value2);
        Assert.AreEqual(instance.Value3, result4.Value3);

        var result5 = instance.WithSecondAndThird(222, 333);
        Assert.AreEqual(instance.Value1, result5.Value1);
        Assert.AreEqual(222, result5.Value2);
        Assert.AreEqual(333, result5.Value3);
    }

    [Test]
    public void UnusualPropertyCasing_WithIsInjectedAnyway()
    {
        var type = typeof(AssemblyToProcess.PropertyCasing);
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
        var type = typeof(AssemblyToProcess.PrimitiveValues);
        var instance = (dynamic)Activator.CreateInstance(type, new object[] { 1, "Hello", (long)234234 });
        var runner = (dynamic)Activator.CreateInstance(typeof(AssemblyToProcess.InAssemblyUsage));
        var result = runner.ChangeIntTo3(instance);
        Assert.AreEqual(3, result.Value1);

        type = typeof(AssemblyToProcess.PropertiesOfSameType);
        instance = (dynamic)Activator.CreateInstance(type, new object[] { 1, 2, 3 });
        result = runner.ChangeValue1To33(instance);
        Assert.AreEqual(33, result.Value1);
    }

    [Test]
    public void UniquePropertyTypeButExplicitMethodName_WithIsEmittedWithLongName()
    {
        var type = typeof(AssemblyToProcess.ExplicitlyAskedForFullName);
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
        var type1 = typeof(AssemblyToProcess.PrimitiveValues);
        Assert.False(type1.GetMethods().Any(m => m.Name == "With" && m.IsGenericMethod));
    }
}