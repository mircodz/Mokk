using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Mokk;

public class SequenceSetupBuilder<TReturn>(SetupEntry entry)
{
    private readonly Queue<Func<object?>> _queue = entry.SequenceQueue = new();

    public SequenceSetupBuilder<TReturn> Returns(TReturn value)
    {
        _queue.Enqueue(() => value);
        return this;
    }

    public SequenceSetupBuilder<TReturn> Throws<TException>() where TException : Exception, new()
    {
        _queue.Enqueue(() => throw new TException());
        return this;
    }

    public SequenceSetupBuilder<TReturn> Throws(Exception ex)
    {
        _queue.Enqueue(() => throw ex);
        return this;
    }
}

public static class SequenceSetupBuilderExtensions
{
    public static SequenceSetupBuilder<Task<T>> ReturnsAsync<T>(this SequenceSetupBuilder<Task<T>> b, T value)
        => b.Returns(Task.FromResult(value));

    public static SequenceSetupBuilder<ValueTask<T>> ReturnsAsync<T>(this SequenceSetupBuilder<ValueTask<T>> b, T value)
        => b.Returns(new ValueTask<T>(value));

    public static SequenceSetupBuilder<Task> ReturnsAsync(this SequenceSetupBuilder<Task> b)
        => b.Returns(Task.CompletedTask);

    public static SequenceSetupBuilder<Task<T>> ThrowsAsync<T>(this SequenceSetupBuilder<Task<T>> b, Exception ex)
        => b.Returns(Task.FromException<T>(ex));

    public static SequenceSetupBuilder<ValueTask<T>> ThrowsAsync<T>(this SequenceSetupBuilder<ValueTask<T>> b, Exception ex)
        => b.Returns(new ValueTask<T>(Task.FromException<T>(ex)));

    public static SequenceSetupBuilder<Task> ThrowsAsync(this SequenceSetupBuilder<Task> b, Exception ex)
        => b.Returns(Task.FromException(ex));
}
