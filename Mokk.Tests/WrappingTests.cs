using Xunit;
using static Mokk.Wildcard;

namespace Mokk.Tests;

public class WrappingTests
{
    [Fact]
    public void Unconfigured_Calls_Delegate_To_Real_Object()
    {
        var mock = new MockEmailService(wrapping: new RealEmailService());

        // No setup - should call through to RealEmailService
        Assert.Equal("real:welcome-v1", mock.Instance.GetTemplate("welcome", 1));
        Assert.True(mock.Instance.Send("a@b.com", "hi"));
    }

    [Fact]
    public void Setup_Takes_Priority_Over_Wrapped_Object()
    {
        var mock = new MockEmailService(wrapping: new RealEmailService());
        mock.GetTemplate("welcome", 1).Returns("mocked");

        Assert.Equal("mocked", mock.Instance.GetTemplate("welcome", 1));
        // Unmatched call still delegates to real
        Assert.Equal("real:reset-v2", mock.Instance.GetTemplate("reset", 2));
    }

    [Fact]
    public void Calls_To_Real_Object_Are_Still_Recorded()
    {
        var mock = new MockEmailService(wrapping: new RealEmailService());

        mock.Instance.Send("a@b.com", "hi");

        mock.Send(Any, Any).Verify(Times.Once);
    }

    [Fact]
    public void Callbacks_Still_Fire_On_Matched_Setups()
    {
        var mock = new MockEmailService(wrapping: new RealEmailService());
        var called = false;
        mock.Send(Any, Any).Callback(() => called = true);

        mock.Instance.Send("a@b.com", "hi");

        Assert.True(called);
    }
}
