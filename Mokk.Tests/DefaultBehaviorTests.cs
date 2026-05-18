using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;
using static Mokk.Wildcard;

namespace Mokk.Tests;

public class DefaultBehaviorTests
{
    [Fact]
    public void Smart_Default_For_String_Is_Empty()
    {
        var mock = new MockEmailService();
        Assert.Equal("", mock.Instance.GetTemplate("x", 1));
    }

    [Fact]
    public void Smart_Default_For_Bool_Is_False()
    {
        var mock = new MockEmailService();
        Assert.False(mock.Instance.Send("a@b.com", "hi"));
    }

    [Fact]
    public async Task Smart_Default_For_Task_Of_T_Is_A_Completed_Task()
    {
        var mock = new MockUserRepository();
        var task = mock.Instance.GetUserAsync(1);

        Assert.NotNull(task);
        Assert.Null(await task); // completes; inner value is default(string)
    }

    [Fact]
    public async Task Smart_Default_For_ValueTask_Of_T_Is_Default()
    {
        var mock = new MockUserRepository();
        Assert.Equal(0, await mock.Instance.CountAsync());
    }

    [Fact]
    public void Strict_Throws_When_No_Setup_Matches()
    {
        var mock = new MockEmailService(strict: true);

        Assert.Throws<MissingSetupException>(() => mock.Instance.Send("a@b.com", "hi"));
    }

    [Fact]
    public void Strict_Does_Not_Throw_When_Setup_Matches()
    {
        var mock = new MockEmailService(strict: true);
        mock.Send(Any, Any).Returns(true);

        Assert.True(mock.Instance.Send("a@b.com", "hi"));
    }

    [Fact]
    public void Unused_Setup_Reported_When_Never_Matched()
    {
        var warnings = new List<string>();
        var mock = new MockEmailService(onUnusedSetup: warnings.Add);
        mock.Send(Any, Any).Returns(true);

        mock.CheckUnusedSetups();

        Assert.Single(warnings);
        Assert.Contains("Send", warnings[0]);
    }

    [Fact]
    public void Unused_Setup_Not_Reported_When_All_Matched()
    {
        var warnings = new List<string>();
        var mock = new MockEmailService(onUnusedSetup: warnings.Add);
        mock.Send(Any, Any).Returns(true);

        mock.Instance.Send("a@b.com", "hi");
        mock.CheckUnusedSetups();

        Assert.Empty(warnings);
    }

    [Fact]
    public void Unused_Setup_Single_Message_Lists_All_Unused()
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
    public void Unused_Setup_Only_Reports_Unmatched()
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
    public void Unused_Setup_Check_Is_Disabled_Without_A_Callback()
    {
        var mock = new MockEmailService();
        mock.Send(Any, Any).Returns(true);

        mock.CheckUnusedSetups(); // no callback => no-op, must not throw
    }

    [Fact]
    public void Members_Colliding_With_Mokk_Surface_Use_Handle_Suffix()
    {
        var mock = new MockReservedNames();
        mock.InstanceHandle.Getter().Returns(7);
        mock.Work(Any).Returns(1);

        Assert.Equal(7, mock.Instance.Instance);
        Assert.Equal(1, mock.Instance.Work(0));

        mock.Instance.Reset();
        mock.ResetHandle().Verify(Times.Once); // distinct from Mokk's own Reset()
    }
}
