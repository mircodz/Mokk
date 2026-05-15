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
    // Guards all mutable state below so a SUT may exercise the mock from
    // multiple threads concurrently. User delegates (Callback/Returns/event
    // handlers) are invoked outside the lock to avoid holding it across
    // arbitrary user code.
    private readonly object _gate = new();

    private volatile MockSession? _session;
    internal void UseSession(MockSession session) => _session = session;

    private readonly List<SetupEntry> _setups = [];
    private readonly List<(string Method, Type[]? TypeArgs, object?[] Args)> _calls = [];
    private readonly HashSet<int> _verifiedCallIndices = [];
    private readonly Dictionary<string, Delegate?> _eventHandlers = [];
    private readonly List<(string Event, Delegate? Handler)> _eventSubscribes = [];
    private readonly List<(string Event, Delegate? Handler)> _eventUnsubscribes = [];
    private readonly List<(string Event, Delegate Handler)> _eventInvocations = [];

    public void Reset()
    {
        lock (_gate)
        {
            _setups.Clear();
            _calls.Clear();
            _verifiedCallIndices.Clear();
            _eventHandlers.Clear();
            _eventSubscribes.Clear();
            _eventUnsubscribes.Clear();
            _eventInvocations.Clear();
        }
    }

    public void AddEventHandler(string eventName, Delegate? handler)
    {
        lock (_gate)
        {
            _eventSubscribes.Add((eventName, handler));
            _eventHandlers.TryGetValue(eventName, out var existing);
            _eventHandlers[eventName] = Delegate.Combine(existing, handler);
        }
    }

    public void RemoveEventHandler(string eventName, Delegate? handler)
    {
        lock (_gate)
        {
            _eventUnsubscribes.Add((eventName, handler));
            _eventHandlers.TryGetValue(eventName, out var existing);
            _eventHandlers[eventName] = Delegate.Remove(existing, handler);
        }
    }

    public void RaiseEvent(string eventName, object?[] args)
    {
        Delegate[] subscribers;
        lock (_gate)
        {
            _eventHandlers.TryGetValue(eventName, out var handler);
            if (handler is null)
                return;

            // Snapshot + record invocations in subscription order under the
            // lock; invoke the handlers outside it.
            subscribers = handler.GetInvocationList();
            foreach (var d in subscribers)
                _eventInvocations.Add((eventName, d));
        }

        foreach (var d in subscribers)
            d.DynamicInvoke(args);
    }

    public int EventSubscriberCount(string eventName)
    {
        lock (_gate)
            return _eventHandlers.TryGetValue(eventName, out var handler) && handler is not null
                ? handler.GetInvocationList().Length
                : 0;
    }

    public void VerifyEventSubscribed(string eventName, Delegate? handler, Times times)
    {
        lock (_gate)
            VerifyEventCount(eventName, handler, times, _eventSubscribes, "Subscribed");
    }

    public void VerifyEventUnsubscribed(string eventName, Delegate? handler, Times times)
    {
        lock (_gate)
            VerifyEventCount(eventName, handler, times, _eventUnsubscribes, "Unsubscribed");
    }

    public void VerifyEventHandlerInvoked(string eventName, Delegate? handler, Times times)
    {
        lock (_gate)
        {
            int count = _eventInvocations.Count(x => x.Event == eventName && (handler is null || Equals(x.Handler, handler)));
            if (!times.IsMatch(count))
                throw new VerificationException(
                    $"Verify failed: event {eventName} HandlerInvoked - {times.Describe(count)}.");
        }
    }

    private static void VerifyEventCount(
        string eventName, Delegate? handler, Times times,
        List<(string Event, Delegate? Handler)> log, string what)
    {
        int count = log.Count(x => x.Event == eventName && (handler is null || Equals(x.Handler, handler)));
        if (!times.IsMatch(count))
            throw new VerificationException(
                $"Verify failed: event {eventName} {what} - {times.Describe(count)}.");
    }

    public SetupEntry AddSetup(string methodName, Type[]? typeArgs, IMatcher[] matchers)
    {
        var entry = new SetupEntry(methodName, typeArgs, matchers);
        lock (_gate)
            _setups.Add(entry);
        return entry;
    }

    public void CheckUnusedSetups()
    {
        if (onUnusedSetup is null)
        {
            return;
        }

        List<string> unused;
        lock (_gate)
            unused = _setups
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
        SetupEntry? setup;
        Func<object?>? sequenceNext = null;

        lock (_gate)
        {
            _calls.Add((methodName, typeArgs, args));
            _session?.Record(this, methodName, typeArgs, args);

            setup = _setups.LastOrDefault(s => s.IsMatch(methodName, typeArgs, args));
            if (setup is not null)
            {
                setup.WasMatched = true;
                for (int i = 0; i < setup.Matchers.Length; i++)
                    setup.Matchers[i].OnMatched(args[i]);

                if (setup.SequenceQueue is { Count: > 0 })
                    sequenceNext = setup.SequenceQueue.Dequeue();
            }
        }

        // User code runs outside the lock.
        if (setup is null)
        {
            if (wrapping is not null)
                return InvokeOnWrapping<TReturn>(methodName, typeArgs, args);
            if (strict)
                throw new MissingSetupException(FormatSignature(methodName, typeArgs, []));
            return SmartDefaults.For<TReturn>();
        }

        setup.Callback?.Invoke(args);

        if (setup.ThrowException is not null)
            throw setup.ThrowException;

        if (sequenceNext is not null)
            return (TReturn)sequenceNext()!;

        return setup.ReturnFactory is not null
            ? (TReturn)setup.ReturnFactory(args)!
            : SmartDefaults.For<TReturn>();
    }

    public void InterceptVoid(string methodName, Type[]? typeArgs, object?[] args)
    {
        SetupEntry? setup;

        lock (_gate)
        {
            _calls.Add((methodName, typeArgs, args));
            _session?.Record(this, methodName, typeArgs, args);

            setup = _setups.LastOrDefault(s => s.IsMatch(methodName, typeArgs, args));
            if (setup is not null)
            {
                setup.WasMatched = true;
                for (int i = 0; i < setup.Matchers.Length; i++)
                    setup.Matchers[i].OnMatched(args[i]);
            }
        }

        if (setup is null)
        {
            if (wrapping is not null)
            {
                InvokeVoidOnWrapping(methodName, typeArgs, args);
                return;
            }
            if (strict)
                throw new MissingSetupException(FormatSignature(methodName, typeArgs, []));
            return;
        }

        setup.Callback?.Invoke(args);

        if (setup.ThrowException is not null)
            throw setup.ThrowException;
    }

    public void VerifyInOrder((string Method, Type[]? TypeArgs, IMatcher[] Matchers)[] steps)
    {
        lock (_gate)
        {
            int callIndex = 0;
            var matchedAt = new int[steps.Length];
            for (int step = 0; step < steps.Length; step++)
            {
                var (method, typeArgs, matchers) = steps[step];
                int hit = -1;
                for (int i = callIndex; i < _calls.Count; i++)
                    if (CallMatches(_calls[i], method, typeArgs, matchers)) { hit = i; break; }

                if (hit < 0)
                {
                    var stepLines = new List<(int, string, string)>();
                    for (int s = 0; s < steps.Length; s++)
                    {
                        var sig = FormatSignature(steps[s].Method, steps[s].TypeArgs, steps[s].Matchers);
                        string status =
                            s < step ? $"OK    @ call {matchedAt[s]}" :
                            s == step ? (step == 0 ? "FAIL  not found" : $"FAIL  no match after call {callIndex}") :
                            "-";
                        stepLines.Add((s + 1, sig, status));
                    }
                    var callLines = _calls.Select(c => FormatCall(c.Method, c.TypeArgs, c.Args)).ToList();
                    throw new VerificationException(RenderInOrderFailure(
                        $"VerifyInOrder on {MockedTypeName} failed at step {step + 1}/{steps.Length}.",
                        stepLines, callLines));
                }

                matchedAt[step] = hit + 1;
                callIndex = hit + 1;
            }
        }
    }

    public void Verify(string methodName, Type[]? typeArgs, IMatcher[] matchers, Times times)
    {
        lock (_gate)
        {
            int count = _calls.Count(c => CallMatches(c, methodName, typeArgs, matchers));
            if (!times.IsMatch(count))
            {
                var sb = new System.Text.StringBuilder(
                    $"Verify failed: {FormatSignature(methodName, typeArgs, matchers)} - {times.Describe(count)}.");
                sb.Append($"\n\nRecorded on {MockedTypeName}:");
                if (_calls.Count == 0)
                {
                    sb.Append("\n  (no calls recorded)");
                }
                else
                {
                    int shown = Math.Min(_calls.Count, CallLogCap);
                    for (int i = 0; i < shown; i++)
                    {
                        var c = _calls[i];
                        var mark = CallMatches(c, methodName, typeArgs, matchers) ? "   [matched]" : "";
                        sb.Append($"\n- {FormatCall(c.Method, c.TypeArgs, c.Args)}{mark}");
                    }
                    if (_calls.Count > shown)
                        sb.Append($"\n- … (+{_calls.Count - shown} more)");
                }
                throw new VerificationException(sb.ToString());
            }

            for (int i = 0; i < _calls.Count; i++)
                if (CallMatches(_calls[i], methodName, typeArgs, matchers))
                    _verifiedCallIndices.Add(i);
        }
    }

    public void VerifyNoOtherCalls()
    {
        List<string> unverified = [];
        lock (_gate)
        {
            for (int i = 0; i < _calls.Count; i++)
            {
                if (!_verifiedCallIndices.Contains(i))
                {
                    var c = _calls[i];
                    unverified.Add(FormatCall(c.Method, c.TypeArgs, c.Args));
                }
            }
        }

        if (unverified.Count > 0)
            throw new VerificationException(
                $"VerifyNoOtherCalls failed - unexpected call(s):\n{string.Join("\n", unverified.Select(s => $"  - {s}"))}");
    }

    public IReadOnlyList<(string Method, Type[]? TypeArgs, object?[] Args)> GetCalls(
        string methodName, Type[]? typeArgs, IMatcher[] matchers)
    {
        lock (_gate)
            return _calls
                .Where(c => CallMatches(c, methodName, typeArgs, matchers))
                .ToList();
    }

    private static bool CallMatches(
        (string Method, Type[]? TypeArgs, object?[] Args) call,
        string methodName, Type[]? typeArgs, IMatcher[] matchers)
        => CallMatches(call.Method, call.TypeArgs, call.Args, methodName, typeArgs, matchers);

    internal static bool CallMatches(
        string callMethod, Type[]? callTypeArgs, object?[] callArgs,
        string methodName, Type[]? typeArgs, IMatcher[] matchers)
        => callMethod == methodName
        && TypeArgsMatch(typeArgs, callTypeArgs)
        && matchers.Length == callArgs.Length
        && matchers.Zip(callArgs).All(p => p.First.Matches(p.Second));

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

    internal string MockedTypeName => wrappingType?.Name ?? "mock";

    private const int CallLogCap = 30;

    internal static string FormatArg(object? a) => a switch
    {
        null => "null",
        string s => $"\"{Truncate(s)}\"",
        char c => $"'{c}'",
        _ => Truncate(a.ToString() ?? ""),
    };

    private static string Truncate(string s) => s.Length > 60 ? s.Substring(0, 59) + "…" : s;

    internal static string FormatCall(string methodName, Type[]? typeArgs, object?[] args)
    {
        string typeArgStr = typeArgs?.Length > 0
            ? $"<{string.Join(", ", typeArgs.Select(t => t.Name))}>"
            : "";
        return $"{methodName}{typeArgStr}({string.Join(", ", args.Select(FormatArg))})";
    }

    internal static string FormatSignature(string methodName, Type[]? typeArgs, IMatcher[] matchers)
    {
        string typeArgStr = typeArgs?.Length > 0
            ? $"<{string.Join(", ", typeArgs.Select(t => t == typeof(AnyType) ? "*" : t.Name))}>"
            : "";
        return $"{methodName}{typeArgStr}({string.Join(", ", matchers.Select(m => m.Describe()))})";
    }

    // Shared "step N/M + call log" renderer for VerifyInOrder (single mock and
    // session). Steps are pre-formatted; callers supply the failing step's
    // header line and the call lines (already prefixed for sessions).
    internal static string RenderInOrderFailure(
        string header,
        IReadOnlyList<(int Num, string Sig, string Status)> steps,
        IReadOnlyList<string> calls)
    {
        int w = steps.Count == 0 ? 0 : steps.Max(s => s.Sig.Length);
        var sb = new System.Text.StringBuilder(header).Append('\n');
        foreach (var s in steps)
            sb.Append($"\n  step {s.Num}  {s.Sig.PadRight(w)}  {s.Status}");
        sb.Append('\n');
        AppendCallLog(sb, calls);
        return sb.ToString();
    }

    internal static void AppendCallLog(System.Text.StringBuilder sb, IReadOnlyList<string> calls)
    {
        if (calls.Count == 0)
        {
            sb.Append("\n  (no calls recorded)");
            return;
        }

        int shown = System.Math.Min(calls.Count, CallLogCap);
        for (int i = 0; i < shown; i++)
            sb.Append($"\n  call {i + 1}  {calls[i]}");
        if (calls.Count > shown)
            sb.Append($"\n  … (+{calls.Count - shown} more)");
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
