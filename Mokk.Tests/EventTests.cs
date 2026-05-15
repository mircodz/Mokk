using System;
using Xunit;

namespace Mokk.Tests;

public class EventTests
{
    [Fact]
    public void Raise_invokes_subscribed_eventhandler()
    {
        var mock = new MockUserRepository();
        UserChangedEventArgs? received = null;
        object? receivedSender = null;

        mock.Instance.UserChanged += (s, e) => { receivedSender = s; received = e; };

        mock.UserChanged.Raise(mock.Instance, new UserChangedEventArgs(42));

        Assert.Same(mock.Instance, receivedSender);
        Assert.NotNull(received);
        Assert.Equal(42, received!.UserId);
    }

    [Fact]
    public void Raise_invokes_all_subscribers()
    {
        var mock = new MockUserRepository();
        int count = 0;

        mock.Instance.UserChanged += (_, _) => count++;
        mock.Instance.UserChanged += (_, _) => count++;

        mock.UserChanged.Raise(null, new UserChangedEventArgs(1));

        Assert.Equal(2, count);
    }

    [Fact]
    public void Unsubscribed_handler_is_not_invoked()
    {
        var mock = new MockUserRepository();
        int count = 0;
        EventHandler<UserChangedEventArgs> handler = (_, _) => count++;

        mock.Instance.UserChanged += handler;
        mock.Instance.UserChanged -= handler;
        mock.UserChanged.Raise(null, new UserChangedEventArgs(1));

        Assert.Equal(0, count);
    }

    [Fact]
    public void Raise_with_no_subscribers_is_a_noop()
    {
        var mock = new MockUserRepository();
        mock.UserChanged.Raise(null, new UserChangedEventArgs(1)); // must not throw
    }

    [Fact]
    public void SubscriberCount_tracks_add_and_remove()
    {
        var mock = new MockUserRepository();
        EventHandler<UserChangedEventArgs> handler = (_, _) => { };

        Assert.Equal(0, mock.UserChanged.SubscriberCount);

        mock.Instance.UserChanged += handler;
        Assert.Equal(1, mock.UserChanged.SubscriberCount);

        mock.Instance.UserChanged -= handler;
        Assert.Equal(0, mock.UserChanged.SubscriberCount);
    }

    [Fact]
    public void Raise_works_with_custom_delegate_type()
    {
        var mock = new MockUserRepository();
        int capturedId = 0;
        string capturedMsg = "";

        mock.Instance.AuditLogged += (id, msg) => { capturedId = id; capturedMsg = msg; };

        mock.AuditLogged.Raise(7, "deleted");

        Assert.Equal(7, capturedId);
        Assert.Equal("deleted", capturedMsg);
    }

    [Fact]
    public void Reset_clears_event_subscriptions()
    {
        var mock = new MockUserRepository();
        int count = 0;
        mock.Instance.UserChanged += (_, _) => count++;

        mock.Reset();
        mock.UserChanged.Raise(null, new UserChangedEventArgs(1));

        Assert.Equal(0, count);
        Assert.Equal(0, mock.UserChanged.SubscriberCount);
    }

    [Fact]
    public void Abstract_class_event_can_be_raised()
    {
        var mock = new MockNotificationService();
        UserChangedEventArgs? received = null;

        mock.Instance.StatusChanged += (_, e) => received = e;
        mock.StatusChangedHandle.Raise(mock.Instance, new UserChangedEventArgs(99));

        Assert.NotNull(received);
        Assert.Equal(99, received!.UserId);
    }
}
