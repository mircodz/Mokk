using System;
using System.Collections.Generic;
using System.Linq;

namespace Mokk;

public interface ICallSpec
{
    string Method { get; }
    Type[]? TypeArgs { get; }
    IMatcher[] Matchers { get; }
    MockInterceptor Owner { get; }
}

public sealed class MethodHandle<TReturn>(
    MockInterceptor interceptor,
    string method,
    Type[]? typeArgs,
    IMatcher[] matchers)
    : ICallSpec
{
    private SetupEntry? _entry;

    string ICallSpec.Method => method;
    Type[]? ICallSpec.TypeArgs => typeArgs;
    IMatcher[] ICallSpec.Matchers => matchers;
    MockInterceptor ICallSpec.Owner => interceptor;

    private SetupEntry Entry => _entry ??= interceptor.AddSetup(method, typeArgs, matchers);

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

    public void Verify(Times times) => interceptor.Verify(method, typeArgs, matchers, times);

    public IReadOnlyList<CallRecord> ReceivedCalls()
        => interceptor.GetCalls(method, typeArgs, matchers)
            .Select(c => new CallRecord(c.Args)).ToList();
}