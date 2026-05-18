using System;

namespace Mokk;

public sealed class EventHandle(MockInterceptor interceptor, string eventName)
{
    public void Raise(params object?[] args) => interceptor.RaiseEvent(eventName, args);

    public void Raise(object? sender, EventArgs e) => interceptor.RaiseEvent(eventName, [sender, e]);

    public int SubscriberCount => interceptor.EventSubscriberCount(eventName);

    public void Subscribed(Times times) => interceptor.VerifyEventSubscribed(eventName, null, times);
    public void Subscribed(Delegate handler, Times times) => interceptor.VerifyEventSubscribed(eventName, handler, times);

    public void Unsubscribed(Times times) => interceptor.VerifyEventUnsubscribed(eventName, null, times);
    public void Unsubscribed(Delegate handler, Times times) => interceptor.VerifyEventUnsubscribed(eventName, handler, times);

    public void HandlerInvoked(Times times) => interceptor.VerifyEventHandlerInvoked(eventName, null, times);
    public void HandlerInvoked(Delegate handler, Times times) => interceptor.VerifyEventHandlerInvoked(eventName, handler, times);
}