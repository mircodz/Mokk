using System;
using System.Collections.Generic;
using System.Linq;

namespace Mokk;

public sealed class VoidMethodHandle(MockInterceptor interceptor, string method, Type[]? typeArgs, IMatcher[] matchers)
    : ICallSpec
{
    private SetupEntry? _entry;

    string ICallSpec.Method => method;
    Type[]? ICallSpec.TypeArgs => typeArgs;
    IMatcher[] ICallSpec.Matchers => matchers;
    MockInterceptor ICallSpec.Owner => interceptor;

    private SetupEntry Entry => _entry ??= interceptor.AddSetup(method, typeArgs, matchers);

    public VoidMethodHandle Callback(Action callback) { Entry.Callback = _ => callback(); return this; }
    public VoidMethodHandle Callback(Action<object?[]> callback) { Entry.Callback = callback; return this; }

    public void Throws<TException>() where TException : Exception, new() => Entry.ThrowException = new TException();
    public void Throws(Exception ex) => Entry.ThrowException = ex;

    public void Verify(Times times) => interceptor.Verify(method, typeArgs, matchers, times);

    public IReadOnlyList<CallRecord> ReceivedCalls()
        => interceptor.GetCalls(method, typeArgs, matchers)
            .Select(c => new CallRecord(c.Args)).ToList();
}