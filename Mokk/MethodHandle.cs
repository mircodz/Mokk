using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Mokk;

public interface ICallSpec
{
    string Method { get; }
    Type[]? TypeArgs { get; }
    IMatcher[] Matchers { get; }
}

public sealed class MethodHandle<TReturn> : ICallSpec
{
    private readonly MockInterceptor _interceptor;
    private readonly string _method;
    private readonly Type[]? _typeArgs;
    private readonly IMatcher[] _matchers;
    private SetupEntry? _entry;

    public MethodHandle(MockInterceptor interceptor, string method, Type[]? typeArgs, IMatcher[] matchers)
    {
        _interceptor = interceptor;
        _method = method;
        _typeArgs = typeArgs;
        _matchers = matchers;
    }

    string ICallSpec.Method => _method;
    Type[]? ICallSpec.TypeArgs => _typeArgs;
    IMatcher[] ICallSpec.Matchers => _matchers;

    private SetupEntry Entry => _entry ??= _interceptor.AddSetup(_method, _typeArgs, _matchers);

    public MethodHandle<TReturn> Returns(TReturn value) { Entry.ReturnFactory = _ => value; return this; }
    public MethodHandle<TReturn> Returns(Func<TReturn> factory) { Entry.ReturnFactory = _ => factory(); return this; }

    public MethodHandle<TReturn> Returns<T1>(Func<T1, TReturn> factory)
    { Entry.ReturnFactory = args => factory((T1)args[0]!); return this; }

    public MethodHandle<TReturn> Returns<T1, T2>(Func<T1, T2, TReturn> factory)
    { Entry.ReturnFactory = args => factory((T1)args[0]!, (T2)args[1]!); return this; }

    public MethodHandle<TReturn> Returns<T1, T2, T3>(Func<T1, T2, T3, TReturn> factory)
    { Entry.ReturnFactory = args => factory((T1)args[0]!, (T2)args[1]!, (T3)args[2]!); return this; }

    public MethodHandle<TReturn> Returns<T1, T2, T3, T4>(Func<T1, T2, T3, T4, TReturn> factory)
    { Entry.ReturnFactory = args => factory((T1)args[0]!, (T2)args[1]!, (T3)args[2]!, (T4)args[3]!); return this; }

    public MethodHandle<TReturn> Callback(Action callback) { Entry.Callback = _ => callback(); return this; }
    public MethodHandle<TReturn> Callback(Action<object?[]> callback) { Entry.Callback = callback; return this; }

    public void Throws<TException>() where TException : Exception, new() => Entry.ThrowException = new TException();
    public void Throws(Exception ex) => Entry.ThrowException = ex;

    public SequenceSetupBuilder<TReturn> Sequence() => new(Entry);

    public void Verify(Times times) => _interceptor.Verify(_method, _typeArgs, _matchers, times);

    public IReadOnlyList<CallRecord> ReceivedCalls()
        => _interceptor.GetCalls(_method, _typeArgs, _matchers)
            .Select(c => new CallRecord(c.Args)).ToList();
}

public sealed class VoidMethodHandle : ICallSpec
{
    private readonly MockInterceptor _interceptor;
    private readonly string _method;
    private readonly Type[]? _typeArgs;
    private readonly IMatcher[] _matchers;
    private SetupEntry? _entry;

    public VoidMethodHandle(MockInterceptor interceptor, string method, Type[]? typeArgs, IMatcher[] matchers)
    {
        _interceptor = interceptor;
        _method = method;
        _typeArgs = typeArgs;
        _matchers = matchers;
    }

    string ICallSpec.Method => _method;
    Type[]? ICallSpec.TypeArgs => _typeArgs;
    IMatcher[] ICallSpec.Matchers => _matchers;

    private SetupEntry Entry => _entry ??= _interceptor.AddSetup(_method, _typeArgs, _matchers);

    public VoidMethodHandle Callback(Action callback) { Entry.Callback = _ => callback(); return this; }
    public VoidMethodHandle Callback(Action<object?[]> callback) { Entry.Callback = callback; return this; }

    public void Throws<TException>() where TException : Exception, new() => Entry.ThrowException = new TException();
    public void Throws(Exception ex) => Entry.ThrowException = ex;

    public void Verify(Times times) => _interceptor.Verify(_method, _typeArgs, _matchers, times);

    public IReadOnlyList<CallRecord> ReceivedCalls()
        => _interceptor.GetCalls(_method, _typeArgs, _matchers)
            .Select(c => new CallRecord(c.Args)).ToList();
}

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

public sealed class EventHandle
{
    private readonly MockInterceptor _interceptor;
    private readonly string _eventName;

    public EventHandle(MockInterceptor interceptor, string eventName)
    {
        _interceptor = interceptor;
        _eventName = eventName;
    }

    public void Raise(params object?[] args) => _interceptor.RaiseEvent(_eventName, args);

    public void Raise(object? sender, EventArgs e) => _interceptor.RaiseEvent(_eventName, new[] { sender, e });

    public int SubscriberCount => _interceptor.EventSubscriberCount(_eventName);
}

public static class MethodHandleExtensions
{
    public static MethodHandle<Task<T>> ReturnsAsync<T>(this MethodHandle<Task<T>> handle, T value)
        => handle.Returns(Task.FromResult(value));

    public static MethodHandle<ValueTask<T>> ReturnsAsync<T>(this MethodHandle<ValueTask<T>> handle, T value)
        => handle.Returns(new ValueTask<T>(value));
}
