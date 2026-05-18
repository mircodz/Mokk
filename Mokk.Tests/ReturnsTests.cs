using System;
using System.Threading.Tasks;
using Xunit;
using static Mokk.Wildcard;

namespace Mokk.Tests;

public class ReturnsTests
{
    [Fact]
    public void Returns_With_Typed_Args()
    {
        var mock = new MockEmailService();
        mock.GetTemplate(Any, Any).Returns((string name, int version) => $"{name}-v{version}");

        Assert.Equal("welcome-v2", mock.Instance.GetTemplate("welcome", 2));
        Assert.Equal("reset-v1", mock.Instance.GetTemplate("reset", 1));
    }

    [Fact]
    public void Returns_Factory_With_Single_Arg()
    {
        var mock = new MockUserRepository();
        mock.GetUserAsync(Any).Returns((int id) => Task.FromResult($"User#{id}"));

        Assert.Equal("User#42", mock.Instance.GetUserAsync(42).Result);
        Assert.Equal("User#99", mock.Instance.GetUserAsync(99).Result);
    }

    [Fact]
    public void Callback_Executes_On_Each_Matched_Call()
    {
        var mock = new MockEmailService();
        var count = 0;

        mock.Send(Any, Any).Callback(() => count++).Returns(true);

        mock.Instance.Send("a", "b");
        mock.Instance.Send("c", "d");
        Assert.Equal(2, count);
    }

    [Fact]
    public void Callback_Captures_Arguments()
    {
        var mock = new MockEmailService();
        var capturedTo = "";

        mock.Send(Any, Any).Callback(args => capturedTo = (string)args[0]!).Returns(true);

        mock.Instance.Send("target@test.com", "subject");
        Assert.Equal("target@test.com", capturedTo);
    }

    [Fact]
    public void Throws_A_Specific_Exception_Instance()
    {
        var mock = new MockEmailService();
        mock.Send("bad@evil.com", Any).Throws(new InvalidOperationException("Blocked!"));

        var ex = Assert.Throws<InvalidOperationException>(() =>
            mock.Instance.Send("bad@evil.com", "test"));
        Assert.Equal("Blocked!", ex.Message);
    }

    [Fact]
    public void Throws_A_Generic_Exception_Type()
    {
        var mock = new MockEmailService();
        mock.Send(Any, Any).Throws<ArgumentException>();

        Assert.Throws<ArgumentException>(() => mock.Instance.Send("a", "b"));
    }

    [Fact]
    public void Sequence_Returns_Values_In_Order()
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
    public void Sequence_Falls_Back_To_Default_After_Exhausted()
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
    public void Sequence_Can_Throw()
    {
        var mock = new MockEmailService();
        mock.Send(Any, Any).Sequence()
            .Returns(true)
            .Throws<InvalidOperationException>();

        Assert.True(mock.Instance.Send("a", "b"));
        Assert.Throws<InvalidOperationException>(() => mock.Instance.Send("c", "d"));
    }

    [Fact]
    public void Void_Method_Callback_Is_Invoked()
    {
        var mock = new MockUserRepository();
        var invoked = false;

        mock.Delete(Any).Callback(() => invoked = true);

        mock.Instance.Delete(42);
        Assert.True(invoked);
    }

    [Fact]
    public void Void_Method_Throws_On_Matched_Call()
    {
        var mock = new MockUserRepository();
        mock.Delete(99).Throws(new InvalidOperationException("Cannot delete"));

        Assert.Throws<InvalidOperationException>(() => mock.Instance.Delete(99));
    }
}
