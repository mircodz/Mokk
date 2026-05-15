using System;
using System.Threading.Tasks;
using Xunit;
using static Mokk.Wildcard;

namespace Mokk.Tests;

public class AsyncTests
{
    [Fact]
    public async Task ReturnsAsync_for_Task()
    {
        var mock = new MockUserRepository();
        mock.GetUserAsync(42).ReturnsAsync("Alice");

        Assert.Equal("Alice", await mock.Instance.GetUserAsync(42));
    }

    [Fact]
    public async Task ReturnsAsync_for_ValueTask()
    {
        var mock = new MockUserRepository();
        mock.CountAsync().ReturnsAsync(5);

        Assert.Equal(5, await mock.Instance.CountAsync());
    }

    [Fact]
    public async Task ReturnsAsync_factory_is_invoked_per_call()
    {
        var mock = new MockUserRepository();
        int n = 0;
        mock.GetUserAsync(Any).ReturnsAsync(() => $"user{++n}");

        Assert.Equal("user1", await mock.Instance.GetUserAsync(1));
        Assert.Equal("user2", await mock.Instance.GetUserAsync(1));
    }

    [Fact]
    public async Task ThrowsAsync_faults_Task_of_T()
    {
        var mock = new MockUserRepository();
        mock.GetUserAsync(Any).ThrowsAsync(new InvalidOperationException("boom"));

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => mock.Instance.GetUserAsync(1));
        Assert.Equal("boom", ex.Message);
    }

    [Fact]
    public async Task ThrowsAsync_faults_ValueTask_of_T()
    {
        var mock = new MockUserRepository();
        mock.CountAsync().ThrowsAsync(new InvalidOperationException());

        await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await mock.Instance.CountAsync());
    }

    [Fact]
    public async Task ThrowsAsync_faults_plain_Task()
    {
        var mock = new MockUserRepository();
        mock.SaveAsync().ThrowsAsync(new InvalidOperationException());

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => mock.Instance.SaveAsync());
    }

    [Fact]
    public async Task ThrowsAsync_generic_for_plain_ValueTask()
    {
        var mock = new MockUserRepository();
        mock.FlushAsync().ThrowsAsync<InvalidOperationException>();

        await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await mock.Instance.FlushAsync());
    }

    [Fact]
    public async Task ThrowsAsync_returns_a_faulted_task_not_a_sync_throw()
    {
        var mock = new MockUserRepository();
        mock.GetUserAsync(Any).ThrowsAsync(new InvalidOperationException());

        var task = mock.Instance.GetUserAsync(1); // must not throw here
        Assert.True(task.IsFaulted);
        await Assert.ThrowsAsync<InvalidOperationException>(() => task);
    }

    [Fact]
    public async Task Async_sequence_returns_then_faults()
    {
        var mock = new MockUserRepository();
        mock.GetUserAsync(Any).Sequence()
            .ReturnsAsync("first")
            .ThrowsAsync(new InvalidOperationException("exhausted"));

        Assert.Equal("first", await mock.Instance.GetUserAsync(1));
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => mock.Instance.GetUserAsync(1));
    }
}
