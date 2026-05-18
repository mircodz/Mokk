using Xunit;
using static Mokk.Wildcard;

namespace Mokk.Tests;

public class AbstractClassTests
{
    [Fact]
    public void Can_Setup_Abstract_Method_And_Call_Via_Instance()
    {
        var mock = new MockNotificationService();
        mock.Notify(Any, Any).Returns(true);

        Assert.True(mock.Instance.Notify("user@test.com", "Hello"));
    }

    [Fact]
    public void Exact_Arg_Match_On_Abstract_Method()
    {
        var mock = new MockNotificationService();
        mock.GetStatus(1).Returns("active");

        Assert.Equal("active", mock.Instance.GetStatus(1));
        Assert.Equal("", mock.Instance.GetStatus(2));
    }

    [Fact]
    public void Can_Verify_Abstract_Method_Call()
    {
        var mock = new MockNotificationService();
        mock.Notify(Any, Any).Returns(true);
        mock.Instance.Notify("a@b.com", "hi");

        mock.Notify(Any, Any).Verify(Times.Once);
    }

    [Fact]
    public void Instance_Is_The_Mock_Itself()
    {
        var mock = new MockNotificationService();
        Assert.Same(mock, mock.Instance);
    }

    [Fact]
    public void Can_Setup_Virtual_Property()
    {
        var mock = new MockNotificationService();
        mock.ServiceNameHandle.Getter().Returns("test-service");

        Assert.Equal("test-service", mock.Instance.ServiceName);
    }

    [Fact]
    public void Protected_Abstract_Method_Is_Accessible_Via_Shortcut()
    {
        var mock = new MockNotificationService();
        mock.Log(Any); // protected member exposed via the shortcut compiles & works
        mock.Instance.Notify("a@b.com", "hi");
    }

    [Fact]
    public void Reset_Clears_Call_History_On_Abstract_Mock()
    {
        var mock = new MockNotificationService();
        mock.Notify(Any, Any).Returns(true);
        mock.Instance.Notify("a@b.com", "hi");
        mock.Reset();

        mock.Notify(Any, Any).Verify(Times.Never);
    }

    [Fact]
    public void Mock_Implements_Base_And_Derived_Interface_Members()
    {
        var mock = new MockExtendedService();
        mock.GetName().Returns("TestService");
        mock.GetCount().Returns(42);

        Assert.Equal("TestService", mock.Instance.GetName());
        Assert.Equal(42, mock.Instance.GetCount());
    }

    [Fact]
    public void Mock_Is_Usable_As_Its_Base_Interface()
    {
        var mock = new MockExtendedService();
        mock.GetName().Returns("Base");

        IBaseService baseService = mock.Instance;
        Assert.Equal("Base", baseService.GetName());
    }

    [Fact]
    public void Interface_Instance_Implements_The_Interface()
    {
        var mock = new MockEmailService();
        IEmailService service = mock.Instance;

        Assert.IsAssignableFrom<IEmailService>(service);
    }

    [Fact]
    public void Abstract_Class_Without_Parameterless_Ctor_Is_Mockable()
    {
        var mock = new MockSeeded();
        mock.Next(1).Returns(5);

        Assert.Equal(5, mock.Instance.Next(1));
        Assert.Equal(0, mock.Instance.Seed); // base(int) chained with default
    }
}
