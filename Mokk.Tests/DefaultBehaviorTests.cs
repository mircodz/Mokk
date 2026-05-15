using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;
using static Mokk.Wildcard;

namespace Mokk.Tests;

// Behaviour when a call has no matching setup: smart defaults, strict mode,
// and unused-setup reporting.
public class DefaultBehaviorTests
{
    [Fact]
    public void Smart_default_for_string_is_empty()
    {
        var mock = new MockEmailService();
        Assert.Equal("", mock.Instance.GetTemplate("x", 1));
    }

    [Fact]
    public void Smart_default_for_bool_is_false()
    {
        var mock = new MockEmailService();
        Assert.False(mock.Instance.Send("a@b.com", "hi"));
    }

    [Fact]
    public async Task Smart_default_for_Task_of_T_is_a_completed_task()
    {
        var mock = new MockUserRepository();
        var task = mock.Instance.GetUserAsync(1);

        Assert.NotNull(task);
        Assert.Null(await task); // completes; inner value is default(string)
    }

    [Fact]
    public async Task Smart_default_for_ValueTask_of_T_is_default()
    {
        var mock = new MockUserRepository();
        Assert.Equal(0, await mock.Instance.CountAsync());
    }

    [Fact]
    public void Strict_throws_when_no_setup_matches()
    {
        var mock = new MockEmailService(strict: true);

        Assert.Throws<MissingSetupException>(() => mock.Instance.Send("a@b.com", "hi"));
    }

    [Fact]
    public void Strict_does_not_throw_when_setup_matches()
    {
        var mock = new MockEmailService(strict: true);
        mock.Send(Any, Any).Returns(true);

        Assert.True(mock.Instance.Send("a@b.com", "hi"));
    }

    [Fact]
    public void Unused_setup_reported_when_never_matched()
    {
        var warnings = new List<string>();
        var mock = new MockEmailService(onUnusedSetup: warnings.Add);
        mock.Send(Any, Any).Returns(true);

        mock.CheckUnusedSetups();

        Assert.Single(warnings);
        Assert.Contains("Send", warnings[0]);
    }

    [Fact]
    public void Unused_setup_not_reported_when_all_matched()
    {
        var warnings = new List<string>();
        var mock = new MockEmailService(onUnusedSetup: warnings.Add);
        mock.Send(Any, Any).Returns(true);

        mock.Instance.Send("a@b.com", "hi");
        mock.CheckUnusedSetups();

        Assert.Empty(warnings);
    }

    [Fact]
    public void Unused_setup_single_message_lists_all_unused()
    {
        var warnings = new List<string>();
        var mock = new MockEmailService(onUnusedSetup: warnings.Add);
        mock.Send(Any, Any).Returns(true);
        mock.GetTemplate(Any, Any).Returns("hi");

        mock.CheckUnusedSetups();

        Assert.Single(warnings);
        Assert.Contains("Send", warnings[0]);
        Assert.Contains("GetTemplate", warnings[0]);
    }

    [Fact]
    public void Unused_setup_only_reports_unmatched()
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
    public void Unused_setup_check_is_disabled_without_a_callback()
    {
        var mock = new MockEmailService();
        mock.Send(Any, Any).Returns(true);

        mock.CheckUnusedSetups(); // no callback => no-op, must not throw
    }
}
