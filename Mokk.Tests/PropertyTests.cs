using static Mokk.Wildcard;
using Xunit;

namespace Mokk.Tests;

public class PropertyTests
{
    [Fact]
    public void Getter_returns_setup_value()
    {
        var mock = new MockUserRepository();
        mock.Name.Getter().Returns("Alice");
        mock.Age.Getter().Returns(30);

        Assert.Equal("Alice", mock.Instance.Name);
        Assert.Equal(30, mock.Instance.Age);
    }

    [Fact]
    public void Setter_is_intercepted_and_verifiable()
    {
        var mock = new MockUserRepository();
        mock.Instance.Name = "Bob";

        mock.Name.Setter(Any).Verify(Times.Once);
    }
}

public class AutoPropertyBackingStoreTests
{
    [Fact]
    public void Set_then_get_returns_set_value()
    {
        var mock = new MockUserRepository();
        mock.Instance.Name = "Alice";
        Assert.Equal("Alice", mock.Instance.Name);
    }

    [Fact]
    public void Get_without_set_falls_through_to_interceptor()
    {
        var mock = new MockUserRepository();
        mock.Name.Getter().Returns("FromSetup");
        Assert.Equal("FromSetup", mock.Instance.Name);
    }

    [Fact]
    public void Set_value_takes_priority_over_setup()
    {
        var mock = new MockUserRepository();
        mock.Name.Getter().Returns("FromSetup");
        mock.Instance.Name = "Direct";
        Assert.Equal("Direct", mock.Instance.Name);
    }

    [Fact]
    public void Reset_clears_backing_store()
    {
        var mock = new MockUserRepository();
        mock.Instance.Name = "Alice";
        mock.Reset();
        // After reset, no setup and no backing - falls through to smart default
        Assert.Equal("", mock.Instance.Name);
    }

    [Fact]
    public void Read_only_property_still_works_via_interceptor()
    {
        var mock = new MockUserRepository();
        mock.Age.Getter().Returns(30);
        Assert.Equal(30, mock.Instance.Age);
    }
}
