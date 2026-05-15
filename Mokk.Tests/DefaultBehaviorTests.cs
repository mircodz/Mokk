using static Mokk.Wildcard;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;

namespace Mokk.Tests;

public class SmartDefaultsTests
{
    [Fact]
    public void String_returns_empty_string()
    {
        var mock = new MockEmailService();
        Assert.Equal("", mock.Instance.GetTemplate("x", 1));
    }

    [Fact]
    public void Bool_returns_false()
    {
        var mock = new MockEmailService();
        Assert.False(mock.Instance.Send("a@b.com", "hi"));
    }

    [Fact]
    public async Task Task_of_T_returns_completed_task_with_default()
    {
        var mock = new MockUserRepository();
        // Task<string> completes without throwing - inner value is default(string) = null
        var task = mock.Instance.GetUserAsync(1);
        Assert.NotNull(task);
        var result = await task;
        Assert.Null(result);
    }

    [Fact]
    public async Task ValueTask_of_T_returns_default()
    {
        var mock = new MockUserRepository();
        var result = await mock.Instance.CountAsync();
        Assert.Equal(0, result);
    }

    [Fact]
    public async Task Task_returns_completed_task()
    {
        // Task (non-generic) should not throw on await
        var mock = new MockUserRepository();
        var task = mock.Instance.GetUserAsync(1);
        Assert.NotNull(task);
        await task; // should complete without exception
    }
}

public class StrictModeTests
{
    [Fact]
    public void Throws_when_no_setup_matches()
    {
        var mock = new MockEmailService(strict: true);

        Assert.Throws<MissingSetupException>(() => mock.Instance.Send("a@b.com", "hi"));
    }

    [Fact]
    public void Does_not_throw_when_setup_matches()
    {
        var mock = new MockEmailService(strict: true);
        mock.Send(Any, Any).Returns(true);

        Assert.True(mock.Instance.Send("a@b.com", "hi"));
    }

    [Fact]
    public void Non_strict_returns_default_when_no_setup()
    {
        var mock = new MockEmailService();

        Assert.False(mock.Instance.Send("a@b.com", "hi"));
    }
}

public class UnusedSetupTests
{
    [Fact]
    public void Calls_callback_when_setup_never_matched()
    {
        var warnings = new List<string>();
        var mock = new MockEmailService(onUnusedSetup: warnings.Add);
        mock.Send(Any, Any).Returns(true);

        mock.CheckUnusedSetups();

        Assert.Single(warnings);
        Assert.Contains("Send", warnings[0]);
    }

    [Fact]
    public void No_callback_when_all_setups_matched()
    {
        var warnings = new List<string>();
        var mock = new MockEmailService(onUnusedSetup: warnings.Add);
        mock.Send(Any, Any).Returns(true);

        mock.Instance.Send("a@b.com", "hi");
        mock.CheckUnusedSetups();

        Assert.Empty(warnings);
    }

    [Fact]
    public void Single_callback_lists_all_unused_setups()
    {
        var warnings = new List<string>();
        var mock = new MockEmailService(onUnusedSetup: warnings.Add);
        mock.Send(Any, Any).Returns(true);
        mock.GetTemplate(Any, Any).Returns("hi");

        mock.CheckUnusedSetups();

        Assert.Single(warnings); // one message, two entries
        Assert.Contains("Send", warnings[0]);
        Assert.Contains("GetTemplate", warnings[0]);
    }

    [Fact]
    public void Only_unmatched_setups_reported()
    {
        var warnings = new List<string>();
        var mock = new MockEmailService(onUnusedSetup: warnings.Add);
        mock.Send(Any, Any).Returns(true);
        mock.GetTemplate(Any, Any).Returns("hi");

        mock.Instance.Send("a@b.com", "hi");
        mock.CheckUnusedSetups();

        Assert.Single(warnings);
        Assert.DoesNotContain("Send", warnings[0]);
        Assert.Contains("GetTemplate", warnings[0]);
    }

    [Fact]
    public void Disabled_when_no_callback_provided()
    {
        var mock = new MockEmailService();
        mock.Send(Any, Any).Returns(true);

        // Should not throw - no callback = no check
        mock.CheckUnusedSetups();
    }
}
