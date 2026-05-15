using Xunit;
using static Mokk.Wildcard;

namespace Mokk.Tests;

// Argument matching: wildcard, exact (implicit conversion), predicate, precedence.
public class MatchingTests
{
    [Fact]
    public void Wildcard_matches_any_argument()
    {
        var mock = new MockEmailService();
        mock.Send(Any, Any).Returns(true);

        Assert.True(mock.Instance.Send("anyone@test.com", "Hello"));
        Assert.True(mock.Instance.Send("other@test.com", "World"));
    }

    [Fact]
    public void Exact_value_match_via_implicit_conversion()
    {
        var mock = new MockEmailService();
        mock.Send("admin@site.com", Any).Returns(true);

        Assert.True(mock.Instance.Send("admin@site.com", "anything"));
        Assert.False(mock.Instance.Send("other@site.com", "anything"));
    }

    [Fact]
    public void Last_setup_wins_over_earlier()
    {
        var mock = new MockEmailService();
        mock.Send(Any, Any).Returns(true);
        mock.Send("blocked@evil.com", Any).Returns(false);

        Assert.False(mock.Instance.Send("blocked@evil.com", "anything"));
        Assert.True(mock.Instance.Send("legit@good.com", "anything"));
    }

    [Fact]
    public void Mixed_typed_and_wildcard_arguments()
    {
        var mock = new MockEmailService();
        mock.GetTemplate(Any, 2).Returns("v2-template");

        Assert.Equal("v2-template", mock.Instance.GetTemplate("welcome", 2));
        Assert.Equal("v2-template", mock.Instance.GetTemplate("any-name", 2));
        Assert.Equal("", mock.Instance.GetTemplate("welcome", 3));
    }

    [Fact]
    public void Predicate_matcher_via_Matcher_Is()
    {
        var mock = new MockEmailService();
        mock.Send(
            Matcher<string>.Is(s => s.EndsWith("@internal.com"), "@internal.com"),
            Any
        ).Returns(true);

        Assert.True(mock.Instance.Send("alice@internal.com", "hi"));
        Assert.False(mock.Instance.Send("alice@external.com", "hi"));
    }

    [Fact]
    public void Predicate_matcher_via_Arg_Is()
    {
        var mock = new MockEmailService();
        mock.Send(Arg.Is<string>(s => s.Contains("@")), Any).Returns(true);

        Assert.True(mock.Instance.Send("test@example.com", "subject"));
        Assert.False(mock.Instance.Send("no-at-sign", "subject"));
    }

    [Fact]
    public void Arg_Any_matches_any_value()
    {
        var mock = new MockEmailService();
        mock.Send(Arg.Any<string>(), Arg.Any<string>()).Returns(true);

        Assert.True(mock.Instance.Send("any@any.com", "any"));
    }

    [Fact]
    public void Zero_parameter_method_shortcut()
    {
        var mock = new MockExtendedService();
        mock.GetName().Returns("Test");
        mock.GetCount().Returns(7);

        Assert.Equal("Test", mock.Instance.GetName());
        Assert.Equal(7, mock.Instance.GetCount());
    }

    [Fact]
    public void Independent_setups_on_different_methods()
    {
        var mock = new MockEmailService();
        mock.Send(Any, Any).Returns(true);
        mock.GetTemplate("v2", Any).Returns("v2!");

        Assert.True(mock.Instance.Send("a@b.com", "hi"));
        Assert.Equal("v2!", mock.Instance.GetTemplate("v2", 1));
    }
}
