## This is an add-in for [Fody](https://github.com/Fody/Fody/) 

![Icon](https://raw.github.com/mikhailshilkov/With.Fody/master/Icons/package_icon.png)

A Fody addin to extend immutable C# classes with `With()` methods to return a copy of an object with one field modified.

[Introduction to Fody](http://github.com/Fody/Fody/wiki/SampleUsage).

## The nuget package  [![NuGet Status](http://img.shields.io/nuget/v/With.Fody.svg?style=flat)](https://www.nuget.org/packages/With.Fody/)

https://nuget.org/packages/With.Fody/

    PM> Install-Package With.Fody
    
## Your Code

    public class MyClass
    {
        public MyClass(int intValue, string stringValue, OtherClass c1, OtherClass c2)
        {
            this.IntValue = intValue;
            this.StringValue = stringValue;
            this.C1 = c1;
            this.C2 = c2;
        }

        public int IntValue { get; }

        public string StringValue { get; }

        public OtherClass C1 { get; }

        public OtherClass C2 { get; }

        // Needed for IntelliSense/Resharper support
        public MyClass With<T>(T value) => this;
        // If two properties have same type, we need to append the property name to With
        public MyClass WithC1(OtherClass value) => this;
        public MyClass WithC2(OtherClass value) => this;
    }

## What gets compiled

    public class MyClass
    {
        public MyClass(int intValue, string stringValue, OtherClass c1, OtherClass c2)
        {
            this.IntValue = intValue;
            this.StringValue = stringValue;
            this.C1 = c1;
            this.C2 = c2;
        }

        public int IntValue { get; }

        public string StringValue { get; }

        public OtherClass C1 { get; }

        public OtherClass C2 { get; }

        public MyClass With(int value)
        {
            return new MyClass(value, this.StringValue, this.C1, this.C2);
        }

        public MyClass With(string value)
        {
            return new MyClass(this.IntValue, value, this.C1, this.C2);
        }

        // If two properties have same type, we need to append the property name to With
        public MyClass WithC1(OtherClass value)
        {
            return new MyClass(this.IntValue, this.StringValue, value, this.C2);
        }

        public MyClass WithC2(OtherClass value)
        {
            return new MyClass(this.IntValue, this.StringValue, this.C1, value);
        }
    }


## Icon

Icon created by Yazmin Alanis from [Noun Project](http://thenounproject.com)