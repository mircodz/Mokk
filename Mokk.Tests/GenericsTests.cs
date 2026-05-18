using Xunit;
using static Mokk.Wildcard;

namespace Mokk.Tests;

public class GenericsTests
{
    [Fact]
    public void Exact_type_setup_returns_value()
    {
        var mock = new MockTemplatedService();
        mock.DoSomething<int>(Any).Returns(1);

        Assert.Equal(1, mock.Instance.DoSomething(123));
    }

    [Fact]
    public void Different_type_args_are_independent()
    {
        var mock = new MockTemplatedService();
        mock.DoSomething<int>(Any).Returns(1);
        mock.DoSomething<string>(Any).Returns("hello");

        Assert.Equal(1, mock.Instance.DoSomething<int>(0));
        Assert.Equal("hello", mock.Instance.DoSomething<string>(""));
    }

    [Fact]
    public void AnyType_wildcard_matches_all_type_args_in_verify()
    {
        var mock = new MockTemplatedService();

        mock.Instance.DoSomething<int>(1);
        mock.Instance.DoSomething<string>("x");

        mock.DoSomething<AnyType>(Any).Verify(Times.Exactly(2));
    }

    [Fact]
    public void AnyType_wildcard_matches_for_callback()
    {
        var mock = new MockTemplatedService();
        var count = 0;
        mock.DoSomething<AnyType>(Any).Callback(() => count++);

        mock.Instance.DoSomething<int>(1);
        mock.Instance.DoSomething<string>("x");

        Assert.Equal(2, count);
    }

    [Fact]
    public void Exact_type_wins_over_AnyType_wildcard()
    {
        var mock = new MockTemplatedService();
        mock.DoSomething<AnyType>(Any).Callback(() => { });
        mock.DoSomething<int>(Any).Returns(99);

        Assert.Equal(99, mock.Instance.DoSomething<int>(0));
    }

    [Fact]
    public void Open_generic_interface_can_be_mocked_and_closed_at_use_site()
    {
        var mock = new MockMessage<string, int>();

        mock.Get(Any).Returns(0);
        mock.Get("answer").Returns(42); // more specific setup registered last wins

        Assert.Equal(42, mock.Instance.Get("answer"));
        Assert.Equal(0, mock.Instance.Get("other"));
        mock.Get("answer").Verify(Times.Once);
    }

    [Fact]
    public void Generic_interface_property_and_void_method_work()
    {
        var mock = new MockMessage<string, int>();

        mock.Instance.LastKey = "k";
        Assert.Equal("k", mock.Instance.LastKey);

        mock.Instance.Put("k", 9);
        mock.Put("k", Any).Verify(Times.Once);
    }

    [Fact]
    public void Generic_interface_event_can_be_raised_with_type_parameters()
    {
        var mock = new MockMessage<string, int>();
        string? key = null;
        int value = 0;

        mock.Instance.Updated += (k, v) => { key = k; value = v; };
        mock.Updated.Raise("name", 5);

        Assert.Equal("name", key);
        Assert.Equal(5, value);
    }

    [Fact]
    public void Different_closings_are_independent_instances()
    {
        var ints = new MockMessage<string, int>();
        var strs = new MockMessage<int, string>();

        ints.Get("x").Returns(1);
        strs.Get(7).Returns("seven");

        Assert.Equal(1, ints.Instance.Get("x"));
        Assert.Equal("seven", strs.Instance.Get(7));
    }

    [Fact]
    public void Constrained_generic_interface_is_supported()
    {
        var mock = new MockBox<Widget>();
        var w = new Widget { Id = 3 };

        mock.Create().Returns(w);
        mock.Contains(Any).Returns(true);

        Assert.Same(w, mock.Instance.Create());
        Assert.True(mock.Instance.Contains(new Widget()));
    }

    [Fact]
    public void Same_name_different_arities_coexist_in_one_assembly()
    {
        var plain = new MockMessage();
        var one = new MockMessage<int>();
        var two = new MockMessage<string, int>();

        plain.Describe().Returns("plain");
        one.Echo(7).Returns(7);
        two.Get("k").Returns(99);

        Assert.Equal("plain", plain.Instance.Describe());
        Assert.Equal(7, one.Instance.Echo(7));
        Assert.Equal(99, two.Instance.Get("k"));
    }

    [Fact]
    public void Open_generic_abstract_class_is_supported()
    {
        var mock = new MockCache<string, int>();

        mock.Load("k").Returns(11);

        Assert.Equal(11, mock.Instance.Load("k"));
        mock.Load("k").Verify(Times.Once);
    }

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
