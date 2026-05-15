using Xunit;
using static Mokk.Wildcard;

namespace Mokk.Tests;

// Mocking abstract classes and interface inheritance.
public class AbstractClassTests
{
    [Fact]
    public void Can_setup_abstract_method_and_call_via_instance()
    {
        var mock = new MockNotificationService();
        mock.Notify(Any, Any).Returns(true);

        Assert.True(mock.Instance.Notify("user@test.com", "Hello"));
    }

    [Fact]
    public void Exact_arg_match_on_abstract_method()
    {
        var mock = new MockNotificationService();
        mock.GetStatus(1).Returns("active");

        Assert.Equal("active", mock.Instance.GetStatus(1));
        Assert.Equal("", mock.Instance.GetStatus(2));
    }

    [Fact]
    public void Can_verify_abstract_method_call()
    {
        var mock = new MockNotificationService();
        mock.Notify(Any, Any).Returns(true);
        mock.Instance.Notify("a@b.com", "hi");

        mock.Notify(Any, Any).Verify(Times.Once);
    }

    [Fact]
    public void Instance_is_the_mock_itself()
    {
        var mock = new MockNotificationService();
        Assert.Same(mock, mock.Instance);
    }

    [Fact]
    public void Can_setup_virtual_property()
    {
        var mock = new MockNotificationService();
        mock.ServiceNameHandle.Getter().Returns("test-service");

        Assert.Equal("test-service", mock.Instance.ServiceName);
    }

    [Fact]
    public void Protected_abstract_method_is_accessible_via_shortcut()
    {
        var mock = new MockNotificationService();
        mock.Log(Any); // protected member exposed via the shortcut compiles & works
        mock.Instance.Notify("a@b.com", "hi");
    }

    [Fact]
    public void Reset_clears_call_history_on_abstract_mock()
    {
        var mock = new MockNotificationService();
        mock.Notify(Any, Any).Returns(true);
        mock.Instance.Notify("a@b.com", "hi");
        mock.Reset();

        mock.Notify(Any, Any).Verify(Times.Never);
    }

    [Fact]
    public void Mock_implements_base_and_derived_interface_members()
    {
        var mock = new MockExtendedService();
        mock.GetName().Returns("TestService");
        mock.GetCount().Returns(42);

        Assert.Equal("TestService", mock.Instance.GetName());
        Assert.Equal(42, mock.Instance.GetCount());
    }

    [Fact]
    public void Mock_is_usable_as_its_base_interface()
    {
        var mock = new MockExtendedService();
        mock.GetName().Returns("Base");

        IBaseService baseService = mock.Instance;
        Assert.Equal("Base", baseService.GetName());
    }

    [Fact]
    public void Interface_instance_implements_the_interface()
    {
        var mock = new MockEmailService();
        IEmailService service = mock.Instance;

        Assert.IsAssignableFrom<IEmailService>(service);
    }
}
