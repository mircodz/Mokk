using System;

namespace Mokk;

[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)]
public class GenerateMockAttribute(Type targetType) : Attribute
{
    public Type TargetType { get; } = targetType;
}