using System;
using System.Collections.Generic;
using System.Linq;

namespace Mokk;

/// <summary>Implemented by every generated mock so a session can observe its calls.</summary>
public interface IMockObject
{
    MockInterceptor Interceptor { get; }
}

/// <summary>
/// Records calls across multiple mocks on one shared timeline so call order can
/// be verified across mock boundaries (not just within a single mock).
/// </summary>
public sealed class MockSession
{
    private readonly object _gate = new();
    private readonly List<(MockInterceptor Owner, string Method, Type[]? TypeArgs, object?[] Args)> _timeline = [];

    public MockSession(params IMockObject[] mocks) => Track(mocks);

    public MockSession Track(params IMockObject[] mocks)
    {
        foreach (var m in mocks)
            m.Interceptor.UseSession(this);
        return this;
    }

    public void Reset()
    {
        lock (_gate)
            _timeline.Clear();
    }

    internal void Record(MockInterceptor owner, string method, Type[]? typeArgs, object?[] args)
    {
        lock (_gate)
            _timeline.Add((owner, method, typeArgs, args));
    }

    /// <summary>
    /// Asserts the steps occurred in this relative order across the tracked
    /// mocks. Unrelated calls between steps are ignored; wrong order throws.
    /// </summary>
    public void VerifyInOrder(params ICallSpec[] steps)
    {
        lock (_gate)
        {
            int from = 0;
            var matchedAt = new int[steps.Length];
            for (int s = 0; s < steps.Length; s++)
            {
                var step = steps[s];
                int hit = -1;
                for (int i = from; i < _timeline.Count; i++)
                {
                    var e = _timeline[i];
                    if (ReferenceEquals(e.Owner, step.Owner)
                        && MockInterceptor.CallMatches(e.Method, e.TypeArgs, e.Args, step.Method, step.TypeArgs, step.Matchers))
                    {
                        hit = i;
                        break;
                    }
                }

                if (hit < 0)
                {
                    var stepLines = new List<(int, string, string)>();
                    for (int k = 0; k < steps.Length; k++)
                    {
                        var sig = $"{steps[k].Owner.MockedTypeName}.{MockInterceptor.FormatSignature(steps[k].Method, steps[k].TypeArgs, steps[k].Matchers)}";
                        string status =
                            k < s ? $"OK    @ call {matchedAt[k]}" :
                            k == s ? (s == 0 ? "FAIL  not found" : $"FAIL  no match after call {from}") :
                            "-";
                        stepLines.Add((k + 1, sig, status));
                    }
                    var callLines = _timeline
                        .Select(e => $"{e.Owner.MockedTypeName}  {MockInterceptor.FormatCall(e.Method, e.TypeArgs, e.Args)}")
                        .ToList();
                    throw new VerificationException(MockInterceptor.RenderInOrderFailure(
                        $"Session.VerifyInOrder failed at step {s + 1}/{steps.Length}.",
                        stepLines, callLines));
                }

                matchedAt[s] = hit + 1;
                from = hit + 1;
            }
        }
    }
}
