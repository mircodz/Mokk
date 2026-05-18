using Xunit;
using static Mokk.Wildcard;

namespace Mokk.Tests;

public class MatchingTests
{
    [Fact]
    public void Wildcard_Matches_Any_Argument()
    {
        var mock = new MockEmailService();
        mock.Send(Any, Any).Returns(true);

        Assert.True(mock.Instance.Send("anyone@test.com", "Hello"));
        Assert.True(mock.Instance.Send("other@test.com", "World"));
    }

    [Fact]
    public void Exact_Value_Match_Via_Implicit_Conversion()
    {
        var mock = new MockEmailService();
        mock.Send("admin@site.com", Any).Returns(true);

        Assert.True(mock.Instance.Send("admin@site.com", "anything"));
        Assert.False(mock.Instance.Send("other@site.com", "anything"));
    }

    [Fact]
    public void Last_Setup_Wins_Over_Earlier()
    {
        var mock = new MockEmailService();
        mock.Send(Any, Any).Returns(true);
        mock.Send("blocked@evil.com", Any).Returns(false);

        Assert.False(mock.Instance.Send("blocked@evil.com", "anything"));
        Assert.True(mock.Instance.Send("legit@good.com", "anything"));
    }

    [Fact]
    public void Mixed_Typed_And_Wildcard_Arguments()
    {
        var mock = new MockEmailService();
        mock.GetTemplate(Any, 2).Returns("v2-template");

        Assert.Equal("v2-template", mock.Instance.GetTemplate("welcome", 2));
        Assert.Equal("v2-template", mock.Instance.GetTemplate("any-name", 2));
        Assert.Equal("", mock.Instance.GetTemplate("welcome", 3));
    }

    [Fact]
    public void Predicate_Matcher_Via_Matcher_Is()
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
    public void Predicate_Matcher_Via_Arg_Is()
    {
        var mock = new MockEmailService();
        mock.Send(Arg.Is<string>(s => s.Contains("@")), Any).Returns(true);

        Assert.True(mock.Instance.Send("test@example.com", "subject"));
        Assert.False(mock.Instance.Send("no-at-sign", "subject"));
    }

    [Fact]
    public void Arg_Any_Matches_Any_Value()
    {
        var mock = new MockEmailService();
        mock.Send(Arg.Any<string>(), Arg.Any<string>()).Returns(true);

        Assert.True(mock.Instance.Send("any@any.com", "any"));
    }

    [Fact]
    public void Zero_Parameter_Method_Shortcut()
    {
        var mock = new MockExtendedService();
        mock.GetName().Returns("Test");
        mock.GetCount().Returns(7);

        Assert.Equal("Test", mock.Instance.GetName());
        Assert.Equal(7, mock.Instance.GetCount());
    }

    [Fact]
    public void Independent_Setups_On_Different_Methods()
    {
        var mock = new MockEmailService();
        mock.Send(Any, Any).Returns(true);
        mock.GetTemplate("v2", Any).Returns("v2!");

        Assert.True(mock.Instance.Send("a@b.com", "hi"));
        Assert.Equal("v2!", mock.Instance.GetTemplate("v2", 1));
    }

    [Fact]
    public void Wildcard_And_Regex_String_Matchers()
    {
        var mock = new MockEmailService();
        mock.Send(Arg.Like("*@example.com"), Any).Returns(true);
        mock.Send(Arg.Regex(@"^\d{3}$"), Any).Returns(true);

        Assert.True(mock.Instance.Send("bob@example.com", "x"));
        Assert.True(mock.Instance.Send("123", "x"));
        Assert.False(mock.Instance.Send("nope", "x"));
    }

    [Fact]
    public void Null_NotNull_InRange_And_Generic_Arg_Alias()
    {
        var mock = new MockEmailService();
        mock.Send(Arg.NotNull<string>(), Any).Returns(true);
        mock.GetTemplate(Arg.Like("tmpl-*"), Arg.InRange(1, 3)).Returns("ok");
        mock.GetTemplate("alias", Arg<int>.Any()).Returns("g");

        Assert.True(mock.Instance.Send("a", "b"));
        Assert.False(mock.Instance.Send(null!, "b"));      // NotNull rejects null
        Assert.Equal("ok", mock.Instance.GetTemplate("tmpl-x", 2));
        Assert.Equal("", mock.Instance.GetTemplate("tmpl-x", 9)); // out of range
        Assert.Equal("g", mock.Instance.GetTemplate("alias", 99));
    }
}
