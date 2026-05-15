using System;
using System.Collections.Generic;

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
    private readonly List<(MockInterceptor Owner, string Method, Type[]? TypeArgs, object?[] Args)> _timeline = [];

    public MockSession(params IMockObject[] mocks) => Track(mocks);

    public MockSession Track(params IMockObject[] mocks)
    {
        foreach (var m in mocks)
            m.Interceptor.UseSession(this);
        return this;
    }

    public void Reset() => _timeline.Clear();

    internal void Record(MockInterceptor owner, string method, Type[]? typeArgs, object?[] args)
        => _timeline.Add((owner, method, typeArgs, args));

    /// <summary>
    /// Asserts the steps occurred in this relative order across the tracked
    /// mocks. Unrelated calls between steps are ignored; wrong order throws.
    /// </summary>
    public void VerifyInOrder(params ICallSpec[] steps)
    {
        int from = 0;
        for (int s = 0; s < steps.Length; s++)
        {
            var step = steps[s];
            bool found = false;

            for (int i = from; i < _timeline.Count; i++)
            {
                var e = _timeline[i];
                if (ReferenceEquals(e.Owner, step.Owner)
                    && MockInterceptor.CallMatches(e.Method, e.TypeArgs, e.Args, step.Method, step.TypeArgs, step.Matchers))
                {
                    from = i + 1;
                    found = true;
                    break;
                }
            }

            if (!found)
                throw new VerificationException(
                    s == 0
                        ? $"Session.VerifyInOrder failed: expected call to {step.Method} was not found."
                        : $"Session.VerifyInOrder failed: expected call to {step.Method} after {steps[s - 1].Method}, but it was not found.");
        }
    }
}
