using System;

namespace Mokk;

// An indexer is a get_/set_ method pair at the IL level (named "Item" by
// default, or whatever [IndexerName] specifies). The index arguments are
// folded into the call signature as matchers, exactly like method args.
public sealed class IndexerHandle<T>(MockInterceptor interceptor, string name, IMatcher[] indexMatchers)
{
    public MethodHandle<T> Getter()
        => new(interceptor, $"get_{name}", null, indexMatchers);

    public VoidMethodHandle Setter(Matcher<T> value)
    {
        // The setter's value is the trailing argument: set_Item(index..., value).
        var matchers = new IMatcher[indexMatchers.Length + 1];
        Array.Copy(indexMatchers, matchers, indexMatchers.Length);
        matchers[indexMatchers.Length] = value.Inner;
        return new(interceptor, $"set_{name}", null, matchers);
    }
}

// Returned by the `mock[matcher]` bracket form on single-index interface mocks.
// Knows the index type, so the setter callback is strongly typed: (index, value).
public sealed class IndexerHandle<TIndex, TValue>(MockInterceptor interceptor, string name, IMatcher index)
{
    public MethodHandle<TValue> Getter()
        => new(interceptor, $"get_{name}", null, new[] { index });

    public IndexerSetterHandle<TIndex, TValue> Setter()
        => Setter(Matcher<TValue>.Any);

    public IndexerSetterHandle<TIndex, TValue> Setter(Matcher<TValue> value)
        => new(new VoidMethodHandle(interceptor, $"set_{name}", null, new[] { index, value.Inner }));
}

public sealed class IndexerSetterHandle<TIndex, TValue>
{
    private readonly VoidMethodHandle _inner;
    internal IndexerSetterHandle(VoidMethodHandle inner) => _inner = inner;

    // set_Item is recorded as (index, value).
    public IndexerSetterHandle<TIndex, TValue> Callback(Action<TIndex, TValue> callback)
    {
        _inner.Callback(args => callback((TIndex)args[0]!, (TValue)args[1]!));
        return this;
    }

    public IndexerSetterHandle<TIndex, TValue> Callback(Action callback)
    {
        _inner.Callback(callback);
        return this;
    }

    public void Throws<TException>() where TException : Exception, new() => _inner.Throws<TException>();
    public void Throws(Exception ex) => _inner.Throws(ex);
    public void Verify(Times times) => _inner.Verify(times);
}