using System;
using System.Threading.Tasks;
using Xunit;
using static Mokk.Wildcard;

namespace Mokk.Tests;

// Configuring a setup's behaviour: Returns (constant/typed factory), Callback,
// Throws, and Sequence — for both value-returning and void methods.
public class ReturnsTests
{
    [Fact]
    public void Returns_with_typed_args()
    {
        var mock = new MockEmailService();
        mock.GetTemplate(Any, Any).Returns((string name, int version) => $"{name}-v{version}");

        Assert.Equal("welcome-v2", mock.Instance.GetTemplate("welcome", 2));
        Assert.Equal("reset-v1", mock.Instance.GetTemplate("reset", 1));
    }

    [Fact]
    public void Returns_factory_with_single_arg()
    {
        var mock = new MockUserRepository();
        mock.GetUserAsync(Any).Returns((int id) => Task.FromResult($"User#{id}"));

        Assert.Equal("User#42", mock.Instance.GetUserAsync(42).Result);
        Assert.Equal("User#99", mock.Instance.GetUserAsync(99).Result);
    }

    [Fact]
    public void Callback_executes_on_each_matched_call()
    {
        var mock = new MockEmailService();
        var count = 0;

        mock.Send(Any, Any).Callback(() => count++).Returns(true);

        mock.Instance.Send("a", "b");
        mock.Instance.Send("c", "d");
        Assert.Equal(2, count);
    }

    [Fact]
    public void Callback_captures_arguments()
    {
        var mock = new MockEmailService();
        var capturedTo = "";

        mock.Send(Any, Any).Callback(args => capturedTo = (string)args[0]!).Returns(true);

        mock.Instance.Send("target@test.com", "subject");
        Assert.Equal("target@test.com", capturedTo);
    }

    [Fact]
    public void Throws_a_specific_exception_instance()
    {
        var mock = new MockEmailService();
        mock.Send("bad@evil.com", Any).Throws(new InvalidOperationException("Blocked!"));

        var ex = Assert.Throws<InvalidOperationException>(() =>
            mock.Instance.Send("bad@evil.com", "test"));
        Assert.Equal("Blocked!", ex.Message);
    }

    [Fact]
    public void Throws_a_generic_exception_type()
    {
        var mock = new MockEmailService();
        mock.Send(Any, Any).Throws<ArgumentException>();

        Assert.Throws<ArgumentException>(() => mock.Instance.Send("a", "b"));
    }

    [Fact]
    public void Sequence_returns_values_in_order()
    {
        var mock = new MockEmailService();
        mock.Send(Any, Any).Sequence()
            .Returns(true)
            .Returns(false)
            .Returns(true);

        Assert.True(mock.Instance.Send("a", "b"));
        Assert.False(mock.Instance.Send("c", "d"));
        Assert.True(mock.Instance.Send("e", "f"));
    }

    [Fact]
    public void Sequence_falls_back_to_default_after_exhausted()
    {
        var mock = new MockEmailService();
        mock.GetTemplate(Any, Any).Sequence()
            .Returns("first")
            .Returns("second");

        Assert.Equal("first", mock.Instance.GetTemplate("a", 1));
        Assert.Equal("second", mock.Instance.GetTemplate("b", 2));
        Assert.Equal("", mock.Instance.GetTemplate("c", 3));
    }

    [Fact]
    public void Sequence_can_throw()
    {
        var mock = new MockEmailService();
        mock.Send(Any, Any).Sequence()
            .Returns(true)
            .Throws<InvalidOperationException>();

        Assert.True(mock.Instance.Send("a", "b"));
        Assert.Throws<InvalidOperationException>(() => mock.Instance.Send("c", "d"));
    }

    [Fact]
    public void Void_method_callback_is_invoked()
    {
        var mock = new MockUserRepository();
        var invoked = false;

        mock.Delete(Any).Callback(() => invoked = true);

        mock.Instance.Delete(42);
        Assert.True(invoked);
    }

    [Fact]
    public void Void_method_throws_on_matched_call()
    {
        var mock = new MockUserRepository();
        mock.Delete(99).Throws(new InvalidOperationException("Cannot delete"));

        Assert.Throws<InvalidOperationException>(() => mock.Instance.Delete(99));
    }
}
