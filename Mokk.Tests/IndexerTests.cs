using Xunit;

namespace Mokk.Tests;

public class IndexerTests
{
    [Fact]
    public void Getter_returns_value_for_matching_key()
    {
        var mock = new MockInventory();
        mock.Indexer("apple").Getter().Returns(5);
        mock.Indexer("pear").Getter().Returns(9);

        Assert.Equal(5, mock.Instance["apple"]);
        Assert.Equal(9, mock.Instance["pear"]);
    }

    [Fact]
    public void Getter_with_no_setup_returns_smart_default()
    {
        var mock = new MockInventory();

        Assert.Equal(0, mock.Instance["missing"]);
    }

    [Fact]
    public void Setter_is_intercepted_and_verifiable_by_key_and_value()
    {
        var mock = new MockInventory();

        mock.Instance["banana"] = 3;

        mock.Indexer("banana").Setter(3).Verify(Times.Once);
        mock.Indexer("banana").Setter(99).Verify(Times.Never);
    }

    [Fact]
    public void Overloaded_indexers_are_dispatched_by_argument_type()
    {
        // IInventory carries both its own this[string] and this[int] from IReadOnlyList<int>.
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

    // IInventory : IReadOnlyList<int> forces both IEnumerable<int>.GetEnumerator
    // and the non-generic IEnumerable.GetEnumerator. The latter is implemented
    // explicitly and must route through the same interception point.
    [Fact]
    public void Non_generic_IEnumerable_GetEnumerator_forwards_to_the_mock()
    {
        var mock = new MockInventory();
        mock.GetEnumerator().Returns(() =>
            ((System.Collections.Generic.IEnumerable<int>)new[] { 1, 2, 3 }).GetEnumerator());

        var seen = new System.Collections.Generic.List<object?>();
        foreach (var x in (System.Collections.IEnumerable)mock.Instance)
            seen.Add(x);

        Assert.Equal(new object?[] { 1, 2, 3 }, seen);
    }
}
