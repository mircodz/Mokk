using System;
using System.Collections.Generic;
using System.Linq;

namespace Mokk;

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
    MockInterceptor ICallSpec.Owner => _interceptor;

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