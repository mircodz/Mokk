using static Mokk.Wildcard;
using Xunit;

namespace Mokk.Tests;

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
    public void Verify_fails_when_not_called()
    {
        var mock = new MockNotificationService();

        Assert.Throws<VerificationException>(() =>
            mock.Notify(Any, Any).Verify(Times.Once));
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
    public void Can_setup_protected_abstract_method()
    {
        var mock = new MockNotificationService();
        // Log is protected - accessible via shortcut on the mock
        mock.Log(Any);

        mock.Instance.Notify("a@b.com", "hi"); // won't call Log directly, but setup works
        // Just verify no throw
    }

    [Fact]
    public void Reset_clears_call_history()
    {
        var mock = new MockNotificationService();
        mock.Notify(Any, Any).Returns(true);
        mock.Instance.Notify("a@b.com", "hi");
        mock.Reset();

        mock.Notify(Any, Any).Verify(Times.Never);
    }
}

public class InheritanceTests
{
    [Fact]
    public void Mock_implements_base_and_derived_methods()
    {
        var mock = new MockExtendedService();
        mock.GetName().Returns("TestService");
        mock.GetCount().Returns(42);

        Assert.Equal("TestService", mock.Instance.GetName());
        Assert.Equal(42, mock.Instance.GetCount());
    }

    [Fact]
    public void Mock_can_be_used_as_base_interface()
    {
        var mock = new MockExtendedService();
        mock.GetName().Returns("Base");

        IBaseService baseService = mock.Instance;
        Assert.Equal("Base", baseService.GetName());
    }

    [Fact]
    public void Instance_returns_interface_implementation()
    {
        var mock = new MockEmailService();
        IEmailService service = mock.Instance;

        Assert.NotNull(service);
        Assert.IsAssignableFrom<IEmailService>(service);
    }
}
