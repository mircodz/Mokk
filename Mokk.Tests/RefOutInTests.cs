using Xunit;
using static Mokk.Wildcard;

namespace Mokk.Tests;

public class RefOutInTests
{
    [Fact]
    public void Out_parameter_is_written_back_from_callback()
    {
        var mock = new MockParser();
        mock.TryParse("42").Callback(args => args[1] = 42).Returns(true);

        var ok = mock.Instance.TryParse("42", out int value);

        Assert.True(ok);
        Assert.Equal(42, value);
    }

    [Fact]
    public void Out_parameter_defaults_when_unset()
    {
        var mock = new MockParser();
        mock.TryParse(Any).Returns(false);

        var ok = mock.Instance.TryParse("x", out int value);

        Assert.False(ok);
        Assert.Equal(0, value);
    }

    [Fact]
    public void Ref_parameter_is_written_back()
    {
        var mock = new MockParser();
        mock.Increment(Any).Callback(args => args[0] = (int)args[0]! + 1);

        int counter = 10;
        mock.Instance.Increment(ref counter);

        Assert.Equal(11, counter);
    }

    [Fact]
    public void Ref_parameter_input_value_is_matched()
    {
        var mock = new MockParser();
        mock.Increment(10).Callback(args => args[0] = 99);

        int matched = 10;
        mock.Instance.Increment(ref matched);
        Assert.Equal(99, matched);

        int unmatched = 5;
        mock.Instance.Increment(ref unmatched); // no setup matches input 5
        Assert.Equal(5, unmatched);
    }

    [Fact]
    public void In_parameter_is_matched()
    {
        var mock = new MockParser();
        mock.Sum(2, 3).Returns(5);

        Assert.Equal(5, mock.Instance.Sum(2, 3));
    }

    [Fact]
    public void Verify_and_ReceivedCalls_work_with_out_param()
    {
        var mock = new MockParser();
        mock.TryParse(Any).Returns(true);

        mock.Instance.TryParse("a", out int _);
        mock.Instance.TryParse("b", out int _);

        mock.TryParse(Any).Verify(Times.Exactly(2));
        mock.TryParse("a").Verify(Times.Once);
    }

    [Fact]
    public void Abstract_class_out_parameter_is_written_back()
    {
        var mock = new MockRefSink();
        mock.TryTake().Callback(args => args[0] = 7).Returns(true);

        var ok = mock.Instance.TryTake(out int v);

        Assert.True(ok);
        Assert.Equal(7, v);
    }
}
