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

    [Fact]
    public void Subscribed_and_Unsubscribed_count_any_handler()
    {
        var mock = new MockUserRepository();
        EventHandler<UserChangedEventArgs> a = (_, _) => { };
        EventHandler<UserChangedEventArgs> b = (_, _) => { };

        mock.Instance.UserChanged += a;
        mock.Instance.UserChanged += b;
        mock.Instance.UserChanged -= a;

        mock.UserChanged.Subscribed(Times.Exactly(2));
        mock.UserChanged.Unsubscribed(Times.Once);
    }

    [Fact]
    public void Subscribed_and_Unsubscribed_count_a_specific_handler()
    {
        var mock = new MockUserRepository();
        EventHandler<UserChangedEventArgs> h = (_, _) => { };

        mock.Instance.UserChanged += h;
        mock.Instance.UserChanged += (_, _) => { };
        mock.Instance.UserChanged -= h;

        mock.UserChanged.Subscribed(h, Times.Once);
        mock.UserChanged.Unsubscribed(h, Times.Once);
        mock.UserChanged.Subscribed(Times.Exactly(2));
    }

    [Fact]
    public void HandlerInvoked_counts_invocations_per_handler()
    {
        var mock = new MockUserRepository();
        EventHandler<UserChangedEventArgs> a = (_, _) => { };
        EventHandler<UserChangedEventArgs> b = (_, _) => { };

        mock.Instance.UserChanged += a;
        mock.Instance.UserChanged += b;

        mock.UserChanged.Raise(null, new UserChangedEventArgs(1));
        mock.UserChanged.Raise(null, new UserChangedEventArgs(2));

        mock.UserChanged.HandlerInvoked(a, Times.Exactly(2));
        mock.UserChanged.HandlerInvoked(Times.Exactly(4)); // 2 handlers x 2 raises
    }

    [Fact]
    public void HandlerInvoked_ignores_unsubscribed_handler()
    {
        var mock = new MockUserRepository();
        EventHandler<UserChangedEventArgs> h = (_, _) => { };

        mock.Instance.UserChanged += h;
        mock.UserChanged.Raise(null, new UserChangedEventArgs(1));
        mock.Instance.UserChanged -= h;
        mock.UserChanged.Raise(null, new UserChangedEventArgs(2));

        mock.UserChanged.HandlerInvoked(h, Times.Once);
    }

    [Fact]
    public void Verify_failure_throws_VerificationException()
    {
        var mock = new MockUserRepository();
        mock.Instance.UserChanged += (_, _) => { };

        Assert.Throws<VerificationException>(() => mock.UserChanged.Subscribed(Times.Never));
        Assert.Throws<VerificationException>(() => mock.UserChanged.Unsubscribed(Times.Once));
    }

    [Fact]
    public void Reset_clears_subscription_and_invocation_history()
    {
        var mock = new MockUserRepository();
        EventHandler<UserChangedEventArgs> h = (_, _) => { };
        mock.Instance.UserChanged += h;
        mock.UserChanged.Raise(null, new UserChangedEventArgs(1));

        mock.Reset();

        mock.UserChanged.Subscribed(Times.Never);
        mock.UserChanged.HandlerInvoked(Times.Never);
    }
}
