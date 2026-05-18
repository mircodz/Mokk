using Xunit;
using static Mokk.Wildcard;

namespace Mokk.Tests;

public class ResetTests
{
    [Fact]
    public void Reset_Clears_Setups()
    {
        var mock = new MockEmailService();
        mock.Send(Any, Any).Returns(true);
        Assert.True(mock.Instance.Send("a@b.com", "hi"));

        mock.Reset();

        Assert.False(mock.Instance.Send("a@b.com", "hi"));
    }

    [Fact]
    public void Reset_Clears_Call_History()
    {
        var mock = new MockEmailService();
        mock.Send(Any, Any).Returns(true);
        mock.Instance.Send("a@b.com", "hi");

        mock.Reset();

        mock.Send(Any, Any).Verify(Times.Never);
    }

    [Fact]
    public void Setups_Added_After_Reset_Work_Normally()
    {
        var mock = new MockEmailService();
        mock.Send(Any, Any).Returns(true);
        mock.Reset();

        mock.Send(Any, Any).Returns(false);

        Assert.False(mock.Instance.Send("a@b.com", "hi"));
    }
}
