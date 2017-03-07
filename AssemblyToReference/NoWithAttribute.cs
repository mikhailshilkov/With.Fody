using System;

/// <summary>
/// Do not generate With method for the property.
/// </summary>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
public sealed class NoWithAttribute : Attribute
{
}
