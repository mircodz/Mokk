using System;
using System.Collections.Generic;

namespace Mokk;

public interface IMatcher
{
    bool Matches(object? actual);
    string Describe();
    void OnMatched(object? actual) { }
}

public class AnyMatcher : IMatcher
{
    public bool Matches(object? _) => true;
    public string Describe() => "_";
}

// Null checks need their own matcher: a PredicateMatcher<T> never sees null
// (its `actual is T` test fails first).
public class NullMatcher : IMatcher
{
    public bool Matches(object? actual) => actual is null;
    public string Describe() => "null";
}

public class NotNullMatcher : IMatcher
{
    public bool Matches(object? actual) => actual is not null;
    public string Describe() => "not null";
}

// FluentAssertions-style string wildcards: `*` = any run, `?` = one char.
public class WildcardMatcher(string pattern) : IMatcher
{
    private readonly System.Text.RegularExpressions.Regex _regex = new(
        "^" + System.Text.RegularExpressions.Regex.Escape(pattern)
            .Replace("\\*", ".*").Replace("\\?", ".") + "$",
        System.Text.RegularExpressions.RegexOptions.Singleline);

    public bool Matches(object? actual) => actual is string s && _regex.IsMatch(s);
    public string Describe() => $"like(\"{pattern}\")";
}

public class RegexMatcher(string pattern) : IMatcher
{
    private readonly System.Text.RegularExpressions.Regex _regex = new(pattern);
    public bool Matches(object? actual) => actual is string s && _regex.IsMatch(s);
    public string Describe() => $"regex(\"{pattern}\")";
}

public class EqualityMatcher<T>(T expected) : IMatcher
{
    public bool Matches(object? actual)
    {
        if (actual is T t)
        {
            return EqualityComparer<T>.Default.Equals(t, expected);
        }

        return false;
    }

    public string Describe() => $"{expected}";
}

public class PredicateMatcher<T>(Func<T, bool> pred, string label = "predicate") : IMatcher
{
    public bool Matches(object? actual) => actual is T t && pred(t);
    public string Describe() => $"Is({label})";
}

public class Matcher<T>
{
    public IMatcher Inner { get; }
    private Matcher(IMatcher inner) => Inner = inner;

    public static readonly Matcher<T> Any = new(new AnyMatcher());

    public static Matcher<T> From(IMatcher inner) => new(inner);

    public static Matcher<T> Is(Func<T, bool> pred, string label = "predicate")
        => new(new PredicateMatcher<T>(pred, label));

    public static implicit operator Matcher<T>(Wildcard _) => Any;

    public static implicit operator Matcher<T>(T value)
        => new(new EqualityMatcher<T>(value));

    public bool Matches(object? value) => Inner.Matches(value);
    public override string ToString() => Inner.Describe();
}
