using static Mokk.Wildcard;
using Xunit;

namespace Mokk.Tests;

public class WildcardMatchingTests
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
    public void Last_setup_wins_over_earlier_wildcard()
    {
        var mock = new MockEmailService();
        mock.Send(Any, Any).Returns(true);
        mock.Send("blocked@evil.com", Any).Returns(false);

        Assert.False(mock.Instance.Send("blocked@evil.com", "anything"));
        Assert.True(mock.Instance.Send("legit@good.com", "anything"));
    }

    [Fact]
    public void Mixed_types_int_version_with_wildcard_string()
    {
        var mock = new MockEmailService();
        mock.GetTemplate(Any, 2).Returns("v2-template");

        Assert.Equal("v2-template", mock.Instance.GetTemplate("welcome", 2));
        Assert.Equal("v2-template", mock.Instance.GetTemplate("any-name", 2));
        Assert.Equal("", mock.Instance.GetTemplate("welcome", 3));
    }
}

public class ResetTests
{
    [Fact]
    public void Reset_clears_setups()
    {
        var mock = new MockEmailService();
        mock.Send(Any, Any).Returns(true);
        Assert.True(mock.Instance.Send("a@b.com", "hi"));

        mock.Reset();

        Assert.False(mock.Instance.Send("a@b.com", "hi"));
    }

    [Fact]
    public void Reset_clears_call_history()
    {
        var mock = new MockEmailService();
        mock.Send(Any, Any).Returns(true);
        mock.Instance.Send("a@b.com", "hi");

        mock.Reset();

        mock.Send(Any, Any).Verify(Times.Never);
    }

    [Fact]
    public void Setups_added_after_reset_work_normally()
    {
        var mock = new MockEmailService();
        mock.Send(Any, Any).Returns(true);
        mock.Reset();

        mock.Send(Any, Any).Returns(false);

        Assert.False(mock.Instance.Send("a@b.com", "hi"));
    }
}
