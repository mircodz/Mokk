using System;

namespace Mokk;

public sealed class PropertyHandle<T>
{
    private readonly MockInterceptor _interceptor;
    private readonly string _propertyName;

    public PropertyHandle(MockInterceptor interceptor, string propertyName)
    {
        _interceptor = interceptor;
        _propertyName = propertyName;
    }

    public MethodHandle<T> Getter()
        => new(_interceptor, $"get_{_propertyName}", null, Array.Empty<IMatcher>());

    public VoidMethodHandle Setter(Matcher<T> value)
        => new(_interceptor, $"set_{_propertyName}", null, new IMatcher[] { value.Inner });
}