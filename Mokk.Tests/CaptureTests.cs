using Xunit;
using static Mokk.Capture;
using static Mokk.Wildcard;

namespace Mokk.Tests;

public class CaptureTests
{
    [Fact]
    public void Captures_Argument_On_Match()
    {
        var slot = Slot<string>();
        var mock = new MockEmailService();
        mock.Send(Into(slot), Any).Returns(true);

        mock.Instance.Send("hello@test.com", "subject");

        Assert.True(slot.HasValue);
        Assert.Equal("hello@test.com", slot.Value);
    }

    [Fact]
    public void Slot_Updated_On_Each_Call()
    {
        var slot = Slot<string>();
        var mock = new MockEmailService();
        mock.Send(Into(slot), Any).Returns(true);

        mock.Instance.Send("first@test.com", "subject");
        mock.Instance.Send("second@test.com", "subject");

        Assert.Equal("second@test.com", slot.Value);
    }

    [Fact]
    public void HasValue_False_When_Never_Called()
    {
        var slot = Slot<string>();
        var mock = new MockEmailService();
        mock.Send(Into(slot), Any).Returns(true);

        Assert.False(slot.HasValue);
        Assert.Null(slot.Value);
    }

    [Fact]
    public void Capture_Does_Not_Fire_For_Non_Matching_Setup()
    {
        var slot = Slot<string>();
        var mock = new MockEmailService();
        mock.Send(Into(slot), "specific").Returns(true);
        mock.Send(Any, Any).Returns(false); // this is the setup that wins

        mock.Instance.Send("hello@test.com", "other");

        Assert.False(slot.HasValue);
    }

    [Fact]
    public void Can_Capture_Value_Type_Argument()
    {
        var slot = Slot<int>();
        var mock = new MockEmailService();
        mock.GetTemplate(Any, Into(slot)).Returns("result");

        mock.Instance.GetTemplate("welcome", 42);

        Assert.Equal(42, slot.Value);
    }

    [Fact]
    public void Multiple_Slots_In_Same_Setup()
    {
        var toSlot = Slot<string>();
        var subjectSlot = Slot<string>();
        var mock = new MockEmailService();
        mock.Send(Into(toSlot), Into(subjectSlot)).Returns(true);

        mock.Instance.Send("a@test.com", "hello");

        Assert.Equal("a@test.com", toSlot.Value);
        Assert.Equal("hello", subjectSlot.Value);
    }
}
