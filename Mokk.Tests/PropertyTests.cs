using Xunit;
using static Mokk.Wildcard;

namespace Mokk.Tests;

public class PropertyTests
{
    [Fact]
    public void Getter_Returns_Setup_Value()
    {
        var mock = new MockUserRepository();
        mock.Name.Getter().Returns("Alice");
        mock.Age.Getter().Returns(30);

        Assert.Equal("Alice", mock.Instance.Name);
        Assert.Equal(30, mock.Instance.Age);
    }

    [Fact]
    public void Read_Only_Property_Works_Via_Interceptor()
    {
        var mock = new MockUserRepository();
        mock.Age.Getter().Returns(30);

        Assert.Equal(30, mock.Instance.Age);
    }

    [Fact]
    public void Setter_Is_Intercepted_And_Verifiable()
    {
        var mock = new MockUserRepository();
        mock.Instance.Name = "Bob";

        mock.Name.Setter(Any).Verify(Times.Once);
    }

    [Fact]
    public void Set_Then_Get_Returns_Set_Value()
    {
        var mock = new MockUserRepository();
        mock.Instance.Name = "Alice";

        Assert.Equal("Alice", mock.Instance.Name);
    }

    [Fact]
    public void Set_Value_Takes_Priority_Over_Getter_Setup()
    {
        var mock = new MockUserRepository();
        mock.Name.Getter().Returns("FromSetup");
        mock.Instance.Name = "Direct";

        Assert.Equal("Direct", mock.Instance.Name);
    }

    [Fact]
    public void Reset_Clears_Backing_Store()
    {
        var mock = new MockUserRepository();
        mock.Instance.Name = "Alice";
        mock.Reset();

        Assert.Equal("", mock.Instance.Name);
    }

    [Fact]
    public void Init_Only_Property_Getter_Is_Mockable()
    {
        var mock = new MockInitOnly();
        mock.Id.Getter().Returns(42);
        mock.Name.Getter().Returns("alice");

        Assert.Equal(42, mock.Instance.Id);
        Assert.Equal("alice", mock.Instance.Name);
    }

    [Fact]
    public void Indexer_Getter_Returns_Value_For_Matching_Key()
    {
        var mock = new MockInventory();
        mock.Indexer("apple").Getter().Returns(5);
        mock.Indexer("pear").Getter().Returns(9);

        Assert.Equal(5, mock.Instance["apple"]);
        Assert.Equal(9, mock.Instance["pear"]);
    }

    [Fact]
    public void Indexer_Getter_With_No_Setup_Returns_Smart_Default()
    {
        var mock = new MockInventory();

        Assert.Equal(0, mock.Instance["missing"]);
    }

    [Fact]
    public void Indexer_Setter_Is_Intercepted_And_Verifiable_By_Key_And_Value()
    {
        var mock = new MockInventory();

        mock.Instance["banana"] = 3;

        mock.Indexer("banana").Setter(3).Verify(Times.Once);
        mock.Indexer("banana").Setter(99).Verify(Times.Never);
    }

    [Fact]
    public void Overloaded_Indexers_Are_Dispatched_By_Argument_Type()
    {
        var mock = new MockInventory();
        mock.Indexer("sku-1").Getter().Returns(7);
        mock.Indexer(0).Getter().Returns(42);

        Assert.Equal(7, mock.Instance["sku-1"]);
        Assert.Equal(42, mock.Instance[0]);
    }

    [Fact]
    public void Read_Only_Indexer_Getter_Callback_Sees_Index()
    {
        var mock = new MockInventory();
        mock.Indexer(Matcher<int>.Any).Getter().Returns<int>(i => i * 10);

        Assert.Equal(20, mock.Instance[2]);
        Assert.Equal(50, mock.Instance[5]);
    }

    [Fact]
    public void Multi_Parameter_Indexer_Matches_On_All_Args()
    {
        var mock = new MockGrid();
        mock.Indexer(1, 2).Getter().Returns("one-two");
        mock.Indexer(3, 4).Getter().Returns("three-four");

        Assert.Equal("one-two", mock.Instance[1, 2]);
        Assert.Equal("three-four", mock.Instance[3, 4]);
    }

    [Fact]
    public void Abstract_Class_Indexer_Get_And_Set()
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
