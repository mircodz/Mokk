using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Mokk;

public class SetupEntry(string methodName, Type[]? typeArgs, IMatcher[] matchers)
{
    public string MethodName { get; } = methodName;
    public Type[]? TypeArgs { get; } = typeArgs;
    public IMatcher[] Matchers { get; } = matchers;
    public Func<object?[], object?>? ReturnFactory { get; set; }
    public Exception? ThrowException { get; set; }
    public Action<object?[]>? Callback { get; set; }
    public Queue<Func<object?>>? SequenceQueue { get; set; }
    public bool WasMatched { get; internal set; }

    public bool IsMatch(string method, Type[]? typeArgs, object?[] args)
        => method == MethodName
        && MockInterceptor.TypeArgsMatch(TypeArgs, typeArgs)
        && Matchers.Length == args.Length
        && Matchers.Zip(args).All(p => p.First.Matches(p.Second));
}

public class MockInterceptor(bool strict = false, object? wrapping = null, Type? wrappingType = null, Action<string>? onUnusedSetup = null)
{
    private readonly List<SetupEntry> _setups = [];
    private readonly List<(string Method, Type[]? TypeArgs, object?[] Args)> _calls = [];
    private readonly HashSet<int> _verifiedCallIndices = [];
    private readonly Dictionary<string, Delegate?> _eventHandlers = [];

    public void Reset()
    {
        _setups.Clear();
        _calls.Clear();
        _verifiedCallIndices.Clear();
        _eventHandlers.Clear();
    }

    public void AddEventHandler(string eventName, Delegate? handler)
    {
        _eventHandlers.TryGetValue(eventName, out var existing);
        _eventHandlers[eventName] = Delegate.Combine(existing, handler);
    }

    public void RemoveEventHandler(string eventName, Delegate? handler)
    {
        _eventHandlers.TryGetValue(eventName, out var existing);
        _eventHandlers[eventName] = Delegate.Remove(existing, handler);
    }

    public void RaiseEvent(string eventName, object?[] args)
    {
        _eventHandlers.TryGetValue(eventName, out var handler);
        handler?.DynamicInvoke(args);
    }

    public int EventSubscriberCount(string eventName)
        => _eventHandlers.TryGetValue(eventName, out var handler) && handler is not null
            ? handler.GetInvocationList().Length
            : 0;

    public SetupEntry AddSetup(string methodName, Type[]? typeArgs, IMatcher[] matchers)
    {
        var entry = new SetupEntry(methodName, typeArgs, matchers);
        _setups.Add(entry);
        return entry;
    }

    public void CheckUnusedSetups()
    {
        if (onUnusedSetup is null)
        {
            return;
        }

        var unused = _setups
            .Where(e => !e.WasMatched)
            .Select(e => FormatSignature(e.MethodName, e.TypeArgs, e.Matchers))
            .ToList();

        if (unused.Count > 0)
        {
            onUnusedSetup($"Unused setup(s) - configured but never matched:\n{string.Join("\n", unused.Select(s => $"  - {s}"))}");
        }
    }

    public TReturn Intercept<TReturn>(string methodName, Type[]? typeArgs, object?[] args)
    {
        _calls.Add((methodName, typeArgs, args));

        var setup = _setups.LastOrDefault(s => s.IsMatch(methodName, typeArgs, args));

        if (setup is null)
        {
            if (wrapping is not null)
            {
                return InvokeOnWrapping<TReturn>(methodName, typeArgs, args);
            }

            if (strict)
            {
                throw new MissingSetupException(FormatSignature(methodName, typeArgs, []));
            }

            return SmartDefaults.For<TReturn>();
        }

        setup.WasMatched = true;
        for (int i = 0; i < setup.Matchers.Length; i++)
        {
            setup.Matchers[i].OnMatched(args[i]);
        }

        setup.Callback?.Invoke(args);

        if (setup.ThrowException is not null)
        {
            throw setup.ThrowException;
        }

        if (setup.SequenceQueue is not null && setup.SequenceQueue.Count > 0)
        {
            return (TReturn)setup.SequenceQueue.Dequeue()()!;
        }

        return setup.ReturnFactory is not null
            ? (TReturn)setup.ReturnFactory(args)!
            : SmartDefaults.For<TReturn>();
    }

    public void InterceptVoid(string methodName, Type[]? typeArgs, object?[] args)
    {
        _calls.Add((methodName, typeArgs, args));

        var setup = _setups.LastOrDefault(s => s.IsMatch(methodName, typeArgs, args));

        if (setup is null)
        {
            if (wrapping is not null)
            {
                InvokeVoidOnWrapping(methodName, typeArgs, args);
                return;
            }

            if (strict)
            {
                throw new MissingSetupException(FormatSignature(methodName, typeArgs, []));
            }

            return;
        }

        setup.WasMatched = true;
        for (int i = 0; i < setup.Matchers.Length; i++)
        {
            setup.Matchers[i].OnMatched(args[i]);
        }

        setup.Callback?.Invoke(args);

        if (setup.ThrowException is not null)
        {
            throw setup.ThrowException;
        }
    }

    public void VerifyInOrder((string Method, Type[]? TypeArgs, IMatcher[] Matchers)[] steps)
    {
        int callIndex = 0;
        for (int step = 0; step < steps.Length; step++)
        {
            var (method, typeArgs, matchers) = steps[step];
            bool found = false;

            for (int i = callIndex; i < _calls.Count; i++)
            {
                if (CallMatches(_calls[i], method, typeArgs, matchers))
                {
                    callIndex = i + 1;
                    found = true;
                    break;
                }
            }

            if (!found)
            {
                var sig = FormatSignature(method, typeArgs, matchers);
                var message = step == 0
                    ? $"VerifyInOrder failed: expected call to {sig} was not found."
                    : $"VerifyInOrder failed: expected call to {sig} after {FormatSignature(steps[step - 1].Method, steps[step - 1].TypeArgs, steps[step - 1].Matchers)}, but it was not found.";
                throw new VerificationException(message);
            }
        }
    }

    public void Verify(string methodName, Type[]? typeArgs, IMatcher[] matchers, Times times)
    {
        int count = CountCalls(methodName, typeArgs, matchers);
        if (!times.IsMatch(count))
            throw new VerificationException($"Verify failed: {FormatSignature(methodName, typeArgs, matchers)} - {times.Describe(count)}.");

        for (int i = 0; i < _calls.Count; i++)
        {
            if (CallMatches(_calls[i], methodName, typeArgs, matchers))
            {
                _verifiedCallIndices.Add(i);
            }
        }
    }

    public void VerifyNoOtherCalls()
    {
        var unverified = new List<string>();
        for (int i = 0; i < _calls.Count; i++)
        {
            if (!_verifiedCallIndices.Contains(i))
            {
                var c = _calls[i];
                unverified.Add(FormatCall(c.Method, c.TypeArgs, c.Args));
            }
        }

        if (unverified.Count > 0)
            throw new VerificationException(
                $"VerifyNoOtherCalls failed - unexpected call(s):\n{string.Join("\n", unverified.Select(s => $"  - {s}"))}");
    }

    private int CountCalls(string methodName, Type[]? typeArgs, IMatcher[] matchers)
        => _calls.Count(c => CallMatches(c, methodName, typeArgs, matchers));

    public IReadOnlyList<(string Method, Type[]? TypeArgs, object?[] Args)> GetCalls(
        string methodName, Type[]? typeArgs, IMatcher[] matchers)
        => _calls
            .Where(c => CallMatches(c, methodName, typeArgs, matchers))
            .ToList();

    private static bool CallMatches(
        (string Method, Type[]? TypeArgs, object?[] Args) call,
        string methodName, Type[]? typeArgs, IMatcher[] matchers)
        => call.Method == methodName
        && TypeArgsMatch(typeArgs, call.TypeArgs)
        && matchers.Length == call.Args.Length
        && matchers.Zip(call.Args).All(p => p.First.Matches(p.Second));

    private TReturn InvokeOnWrapping<TReturn>(string methodName, Type[]? typeArgs, object?[] args)
        => (TReturn)FindWrappingMethod(methodName, typeArgs, args).Invoke(wrapping, args)!;

    private void InvokeVoidOnWrapping(string methodName, Type[]? typeArgs, object?[] args)
        => FindWrappingMethod(methodName, typeArgs, args).Invoke(wrapping, args);

    private MethodInfo FindWrappingMethod(string methodName, Type[]? typeArgs, object?[] args)
    {
        var type = wrappingType!;

        var method = type.GetMethods()
            .FirstOrDefault(m => m.Name == methodName && m.GetParameters().Length == args.Length)
            ?? throw new InvalidOperationException(
                $"No method '{methodName}' with {args.Length} parameter(s) found on {type.Name}.");

        return typeArgs?.Length > 0 ? method.MakeGenericMethod(typeArgs) : method;
    }

    private static string FormatCall(string methodName, Type[]? typeArgs, object?[] args)
    {
        string typeArgStr = typeArgs?.Length > 0
            ? $"<{string.Join(", ", typeArgs.Select(t => t.Name))}>"
            : "";
        return $"{methodName}{typeArgStr}({string.Join(", ", args.Select(a => a?.ToString() ?? "null"))})";
    }

    private static string FormatSignature(string methodName, Type[]? typeArgs, IMatcher[] matchers)
    {
        string typeArgStr = typeArgs?.Length > 0
            ? $"<{string.Join(", ", typeArgs.Select(t => t == typeof(AnyType) ? "*" : t.Name))}>"
            : "";
        return $"{methodName}{typeArgStr}({string.Join(", ", matchers.Select(m => m.Describe()))})";
    }

    internal static bool TypeArgsMatch(Type[]? expected, Type[]? actual)
    {
        if (expected is null && actual is null)
        {
            return true;
        }

        if (expected is null || actual is null)
        {
            return false;
        }

        if (expected.Length != actual.Length)
        {
            return false;
        }

        return expected.Zip(actual).All(p => p.First == typeof(AnyType) || p.First == p.Second);
    }
}

public class VerificationException(string message) : Exception(message);

public class MissingSetupException(string signature) : Exception($"Strict mock: unexpected call to {signature} - no setup matched.");
