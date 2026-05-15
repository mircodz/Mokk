using System;
using Xunit;

namespace Mokk.Tests;

public class EventVerificationTests
{
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
