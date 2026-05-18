namespace Mokk;

public sealed class PropertyHandle<T>(MockInterceptor interceptor, string propertyName)
{
    public MethodHandle<T> Getter()
        => new(interceptor, $"get_{propertyName}", null, []);

    public VoidMethodHandle Setter(Matcher<T> value)
        => new(interceptor, $"set_{propertyName}", null, [value.Inner]);
}