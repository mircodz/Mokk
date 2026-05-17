using System;
using System.Threading.Tasks;

namespace Mokk;

public static class MethodHandleExtensions
{
    public static MethodHandle<Task<T>> ReturnsAsync<T>(this MethodHandle<Task<T>> handle, T value)
        => handle.Returns(Task.FromResult(value));

    public static MethodHandle<ValueTask<T>> ReturnsAsync<T>(this MethodHandle<ValueTask<T>> handle, T value)
        => handle.Returns(new ValueTask<T>(value));

    public static MethodHandle<Task<T>> ReturnsAsync<T>(this MethodHandle<Task<T>> handle, Func<T> factory)
        => handle.Returns(() => Task.FromResult(factory()));

    public static MethodHandle<ValueTask<T>> ReturnsAsync<T>(this MethodHandle<ValueTask<T>> handle, Func<T> factory)
        => handle.Returns(() => new ValueTask<T>(factory()));

    // Faulted-task semantics: the method returns a failed Task rather than
    // throwing synchronously, so `await` (not the call) observes the exception.
    public static void ThrowsAsync(this MethodHandle<Task> handle, Exception ex)
        => handle.Returns(Task.FromException(ex));

    public static void ThrowsAsync<T>(this MethodHandle<Task<T>> handle, Exception ex)
        => handle.Returns(Task.FromException<T>(ex));

    public static void ThrowsAsync(this MethodHandle<ValueTask> handle, Exception ex)
        => handle.Returns(new ValueTask(Task.FromException(ex)));

    public static void ThrowsAsync<T>(this MethodHandle<ValueTask<T>> handle, Exception ex)
        => handle.Returns(new ValueTask<T>(Task.FromException<T>(ex)));

    public static void ThrowsAsync<TException>(this MethodHandle<Task> handle)
        where TException : Exception, new()
        => handle.Returns(Task.FromException(new TException()));

    public static void ThrowsAsync<TException>(this MethodHandle<ValueTask> handle)
        where TException : Exception, new()
        => handle.Returns(new ValueTask(Task.FromException(new TException())));
}