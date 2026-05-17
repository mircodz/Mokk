using System;

namespace Mokk;

// An indexer is a get_/set_ method pair at the IL level (named "Item" by
// default, or whatever [IndexerName] specifies). The index arguments are
// folded into the call signature as matchers, exactly like method args.
public sealed class IndexerHandle<T>
{
    private readonly MockInterceptor _interceptor;
    private readonly string _name;
    private readonly IMatcher[] _indexMatchers;

    public IndexerHandle(MockInterceptor interceptor, string name, IMatcher[] indexMatchers)
    {
        _interceptor = interceptor;
        _name = name;
        _indexMatchers = indexMatchers;
    }

    public MethodHandle<T> Getter()
        => new(_interceptor, $"get_{_name}", null, _indexMatchers);

    public VoidMethodHandle Setter(Matcher<T> value)
    {
        // The setter's value is the trailing argument: set_Item(index..., value).
        var matchers = new IMatcher[_indexMatchers.Length + 1];
        Array.Copy(_indexMatchers, matchers, _indexMatchers.Length);
        matchers[_indexMatchers.Length] = value.Inner;
        return new(_interceptor, $"set_{_name}", null, matchers);
    }
}