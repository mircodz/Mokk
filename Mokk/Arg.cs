using System;
using System.Linq;

namespace Mokk;

public static class Arg
{
    public static Matcher<T> Any<T>() => Matcher<T>.Any;

    public static Matcher<T> Is<T>(Func<T, bool> predicate)
        => Matcher<T>.Is(predicate);

    public static Matcher<T> Is<T>(Func<T, bool> predicate, string label)
        => Matcher<T>.Is(predicate, label);

    public static Matcher<T> Null<T>() => Matcher<T>.From(new NullMatcher());

    public static Matcher<T> NotNull<T>() => Matcher<T>.From(new NotNullMatcher());

    /// <summary>Matches the same reference (<see cref="object.ReferenceEquals"/>),
    /// bypassing any <c>Equals</c> override.</summary>
    public static Matcher<T> Same<T>(T instance) where T : class
        => Matcher<T>.From(new SameMatcher(instance));

    /// <summary>Wildcard match: <c>*</c> = any run, <c>?</c> = one char.</summary>
    public static Matcher<string> Like(string wildcard)
        => Matcher<string>.From(new WildcardMatcher(wildcard));

    public static Matcher<string> Regex(string pattern)
        => Matcher<string>.From(new RegexMatcher(pattern));

    public static Matcher<T> In<T>(params T[] values)
        => Matcher<T>.Is(values.Contains, $"in[{string.Join(", ", values)}]");

    public static Matcher<T> InRange<T>(T min, T max) where T : IComparable<T>
        => Matcher<T>.Is(v => v.CompareTo(min) >= 0 && v.CompareTo(max) <= 0, $"in [{min}, {max}]");
}

/// <summary>Generic-friendly alias so <c>Arg&lt;int&gt;.Any()</c> reads naturally.</summary>
public static class Arg<T>
{
    public static Matcher<T> Any() => Matcher<T>.Any;
    public static Matcher<T> Is(Func<T, bool> predicate) => Matcher<T>.Is(predicate);
    public static Matcher<T> Is(Func<T, bool> predicate, string label) => Matcher<T>.Is(predicate, label);
    public static Matcher<T> Null() => Matcher<T>.From(new NullMatcher());
    public static Matcher<T> NotNull() => Matcher<T>.From(new NotNullMatcher());
}