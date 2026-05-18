using Xunit;
using static Mokk.Wildcard;

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
    public void Read_only_property_works_via_interceptor()
    {
        var mock = new MockUserRepository();
        mock.Age.Getter().Returns(30);

        Assert.Equal(30, mock.Instance.Age);
    }

    [Fact]
    public void Setter_is_intercepted_and_verifiable()
    {
        var mock = new MockUserRepository();
        mock.Instance.Name = "Bob";

        mock.Name.Setter(Any).Verify(Times.Once);
    }

    [Fact]
    public void Set_then_get_returns_set_value()
    {
        var mock = new MockUserRepository();
        mock.Instance.Name = "Alice";

        Assert.Equal("Alice", mock.Instance.Name);
    }

    [Fact]
    public void Set_value_takes_priority_over_getter_setup()
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

        Assert.Equal("", mock.Instance.Name);
    }

    [Fact]
    public void Init_only_property_getter_is_mockable()
    {
        var mock = new MockInitOnly();
        mock.Id.Getter().Returns(42);
        mock.Name.Getter().Returns("alice");

        Assert.Equal(42, mock.Instance.Id);
        Assert.Equal("alice", mock.Instance.Name);
    }

    [Fact]
    public void Indexer_getter_returns_value_for_matching_key()
    {
        var mock = new MockInventory();
        mock.Indexer("apple").Getter().Returns(5);
        mock.Indexer("pear").Getter().Returns(9);

        Assert.Equal(5, mock.Instance["apple"]);
        Assert.Equal(9, mock.Instance["pear"]);
    }

    [Fact]
    public void Indexer_getter_with_no_setup_returns_smart_default()
    {
        var mock = new MockInventory();

        Assert.Equal(0, mock.Instance["missing"]);
    }

    [Fact]
    public void Indexer_setter_is_intercepted_and_verifiable_by_key_and_value()
    {
        var mock = new MockInventory();

        mock.Instance["banana"] = 3;

        mock.Indexer("banana").Setter(3).Verify(Times.Once);
        mock.Indexer("banana").Setter(99).Verify(Times.Never);
    }

    [Fact]
    public void Overloaded_indexers_are_dispatched_by_argument_type()
    {
        var mock = new MockInventory();
        mock.Indexer("sku-1").Getter().Returns(7);
        mock.Indexer(0).Getter().Returns(42);

        Assert.Equal(7, mock.Instance["sku-1"]);
        Assert.Equal(42, mock.Instance[0]);
    }

    [Fact]
    public void Read_only_indexer_getter_callback_sees_index()
    {
        var mock = new MockInventory();
        mock.Indexer(Matcher<int>.Any).Getter().Returns<int>(i => i * 10);

        Assert.Equal(20, mock.Instance[2]);
        Assert.Equal(50, mock.Instance[5]);
    }

    [Fact]
    public void Multi_parameter_indexer_matches_on_all_args()
    {
        var mock = new MockGrid();
        mock.Indexer(1, 2).Getter().Returns("one-two");
        mock.Indexer(3, 4).Getter().Returns("three-four");

        Assert.Equal("one-two", mock.Instance[1, 2]);
        Assert.Equal("three-four", mock.Instance[3, 4]);
    }

    [Fact]
    public void Abstract_class_indexer_get_and_set()
    {
        var mock = new MockLookup();
        mock.Indexer(1).Getter().Returns("alice");

        Assert.Equal("alice", mock.Instance[1]);

        mock.Instance[2] = "bob";
        mock.Indexer(2).Setter("bob").Verify(Times.Once);
    }

    [Fact]
    public void Indexer_Bracket_Getter_On_Interface_Mock()
    {
        var mock = new MockInventory();
        mock[Arg<string>.Any()].Getter().Returns(7);

        Assert.Equal(7, mock.Instance["banana"]);
    }

    [Fact]
    public void Indexer_Bracket_Setter_Callback_Sees_Key_And_Value()
    {
        var mock = new MockInventory();
        string? gotKey = null;
        var gotVal = 0;
        mock[Arg<string>.Any()].Setter().Callback((key, value) => { gotKey = key; gotVal = value; });

        mock.Instance["apple"] = 50;

        Assert.Equal("apple", gotKey);
        Assert.Equal(50, gotVal);
        mock[Arg.Like("ap*")].Setter().Verify(Times.Once);
    }
}
