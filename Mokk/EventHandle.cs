using System;

namespace Mokk;

public sealed class EventHandle
{
    private readonly MockInterceptor _interceptor;
    private readonly string _eventName;

    public EventHandle(MockInterceptor interceptor, string eventName)
    {
        _interceptor = interceptor;
        _eventName = eventName;
    }

    public void Raise(params object?[] args) => _interceptor.RaiseEvent(_eventName, args);

    public void Raise(object? sender, EventArgs e) => _interceptor.RaiseEvent(_eventName, new[] { sender, e });

    public int SubscriberCount => _interceptor.EventSubscriberCount(_eventName);

    public void Subscribed(Times times) => _interceptor.VerifyEventSubscribed(_eventName, null, times);
    public void Subscribed(Delegate handler, Times times) => _interceptor.VerifyEventSubscribed(_eventName, handler, times);

    public void Unsubscribed(Times times) => _interceptor.VerifyEventUnsubscribed(_eventName, null, times);
    public void Unsubscribed(Delegate handler, Times times) => _interceptor.VerifyEventUnsubscribed(_eventName, handler, times);

    public void HandlerInvoked(Times times) => _interceptor.VerifyEventHandlerInvoked(_eventName, null, times);
    public void HandlerInvoked(Delegate handler, Times times) => _interceptor.VerifyEventHandlerInvoked(_eventName, handler, times);
}