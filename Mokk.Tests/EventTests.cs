using System;
using Xunit;

namespace Mokk.Tests;

public class EventTests
{
    [Fact]
    public void Raise_Invokes_Subscribed_Eventhandler()
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
    public void Raise_Invokes_All_Subscribers()
    {
        var mock = new MockUserRepository();
        int count = 0;

        mock.Instance.UserChanged += (_, _) => count++;
        mock.Instance.UserChanged += (_, _) => count++;

        mock.UserChanged.Raise(null, new UserChangedEventArgs(1));

        Assert.Equal(2, count);
    }

    [Fact]
    public void Unsubscribed_Handler_Is_Not_Invoked()
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
    public void Raise_With_No_Subscribers_Is_A_Noop()
    {
        var mock = new MockUserRepository();
        mock.UserChanged.Raise(null, new UserChangedEventArgs(1)); // must not throw
    }

    [Fact]
    public void SubscriberCount_Tracks_Add_And_Remove()
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
    public void Raise_Works_With_Custom_Delegate_Type()
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
    public void Abstract_Class_Event_Can_Be_Raised()
    {
        var mock = new MockNotificationService();
        UserChangedEventArgs? received = null;

        mock.Instance.StatusChanged += (_, e) => received = e;
        mock.StatusChangedHandle.Raise(mock.Instance, new UserChangedEventArgs(99));

        Assert.NotNull(received);
        Assert.Equal(99, received!.UserId);
    }

    [Fact]
    public void Subscribed_And_Unsubscribed_Count_Any_Handler()
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
    public void Subscribed_And_Unsubscribed_Count_A_Specific_Handler()
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
    public void HandlerInvoked_Counts_Invocations_Per_Handler()
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
    public void HandlerInvoked_Ignores_Unsubscribed_Handler()
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
    public void Verify_Failure_Throws_VerificationException()
    {
        var mock = new MockUserRepository();
        mock.Instance.UserChanged += (_, _) => { };

        Assert.Throws<VerificationException>(() => mock.UserChanged.Subscribed(Times.Never));
        Assert.Throws<VerificationException>(() => mock.UserChanged.Unsubscribed(Times.Once));
    }

    [Fact]
    public void Reset_Clears_Subscription_And_Invocation_History()
    {
        var mock = new MockUserRepository();
        EventHandler<UserChangedEventArgs> h = (_, _) => { };
        mock.Instance.UserChanged += h;
        mock.UserChanged.Raise(null, new UserChangedEventArgs(1));

        mock.Reset();

        Assert.Equal(0, mock.UserChanged.SubscriberCount);
        mock.UserChanged.Subscribed(Times.Never);
        mock.UserChanged.HandlerInvoked(Times.Never);
    }
}
