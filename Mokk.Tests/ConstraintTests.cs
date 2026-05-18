using Xunit;

namespace Mokk.Tests;

public class ConstraintTests
{
    public class Boxed { public int Value; }

    [Fact]
    public void Constrained_generic_method_on_interface_is_setup_and_invoked()
    {
        var mock = new MockConstrained();
        var made = new Boxed { Value = 7 };
        mock.Create<Boxed>().Returns(made);

        Assert.Same(made, mock.Instance.Create<Boxed>());
    }

    [Fact]
    public void Constrained_generic_void_method_is_verifiable()
    {
        var mock = new MockConstrained();

        mock.Instance.Store<string, int>("k", 42);

        mock.Store<string, int>("k", 42).Verify(Times.Once);
        mock.Store<string, int>("k", 0).Verify(Times.Never);
    }

    [Fact]
    public void Constrained_generic_method_override_on_abstract_class()
    {
        var mock = new MockFactory();
        var made = new Boxed();
        mock.Make<Boxed>("widget").Returns(made);

        Assert.Same(made, mock.Instance.Make<Boxed>("widget"));
    }
}
