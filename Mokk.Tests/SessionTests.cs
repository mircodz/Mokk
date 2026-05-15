using Xunit;
using static Mokk.Wildcard;

namespace Mokk.Tests;

// Cross-mock ordered verification via a shared MockSession.
public class SessionTests
{
    [Fact]
    public void Verifies_call_order_across_mocks()
    {
        var email = new MockEmailService();
        var repo = new MockUserRepository();
        var session = new MockSession(email, repo);
        email.Send(Any, Any).Returns(true);

        email.Instance.Send("a@b.com", "hi");
        repo.Instance.Delete(1);
        email.Instance.GetTemplate("welcome", 1);

        session.VerifyInOrder(
            email.Send(Any, Any),
            repo.Delete(Any),
            email.GetTemplate(Any, Any)
        );
    }

    [Fact]
    public void Fails_when_cross_mock_order_is_wrong()
    {
        var email = new MockEmailService();
        var repo = new MockUserRepository();
        var session = new MockSession(email, repo);

        repo.Instance.Delete(1);
        email.Instance.Send("a@b.com", "hi");

        Assert.Throws<VerificationException>(() =>
            session.VerifyInOrder(
                email.Send(Any, Any),
                repo.Delete(Any)
            ));
    }

    [Fact]
    public void Allows_unrelated_interleaved_calls_between_steps()
    {
        var email = new MockEmailService();
        var repo = new MockUserRepository();
        var session = new MockSession(email, repo);

        email.Instance.Send("a@b.com", "hi");
        repo.Instance.Delete(99);          // unrelated, ignored
        email.Instance.GetTemplate("x", 1);
        repo.Instance.Delete(1);

        session.VerifyInOrder(
            email.Send(Any, Any),
            repo.Delete(1)
        );
    }

    [Fact]
    public void Matchers_are_respected_per_step()
    {
        var email = new MockEmailService();
        var repo = new MockUserRepository();
        var session = new MockSession(email, repo);

        email.Instance.Send("first@b.com", "hi");
        repo.Instance.Delete(7);
        email.Instance.Send("second@b.com", "hi");

        session.VerifyInOrder(
            email.Send("first@b.com", Any),
            repo.Delete(7),
            email.Send("second@b.com", Any)
        );

        Assert.Throws<VerificationException>(() =>
            session.VerifyInOrder(
                email.Send("second@b.com", Any),
                email.Send("first@b.com", Any)
            ));
    }

    [Fact]
    public void Untracked_mock_calls_are_not_in_the_timeline()
    {
        var email = new MockEmailService();
        var repo = new MockUserRepository();
        var session = new MockSession(email); // repo NOT tracked

        email.Instance.Send("a@b.com", "hi");
        repo.Instance.Delete(1);

        Assert.Throws<VerificationException>(() =>
            session.VerifyInOrder(
                email.Send(Any, Any),
                repo.Delete(Any)
            ));
    }

    [Fact]
    public void Track_returns_session_and_can_be_called_after_construction()
    {
        var email = new MockEmailService();
        var repo = new MockUserRepository();
        var session = new MockSession().Track(email, repo);

        email.Instance.Send("a@b.com", "hi");
        repo.Instance.Delete(1);

        session.VerifyInOrder(email.Send(Any, Any), repo.Delete(Any));
    }

    [Fact]
    public void Reset_clears_the_timeline()
    {
        var email = new MockEmailService();
        var repo = new MockUserRepository();
        var session = new MockSession(email, repo);

        email.Instance.Send("a@b.com", "hi");
        repo.Instance.Delete(1);
        session.Reset();

        Assert.Throws<VerificationException>(() =>
            session.VerifyInOrder(email.Send(Any, Any)));
    }

    [Fact]
    public void Failure_message_prefixes_steps_and_calls_with_mock_type()
    {
        var email = new MockEmailService();
        var repo = new MockUserRepository();
        var session = new MockSession(email, repo);

        email.Instance.Send("a@b.com", "hi");

        var ex = Assert.Throws<VerificationException>(() =>
            session.VerifyInOrder(email.Send(Any, Any), repo.Delete(Any)));

        Assert.Contains("Session.VerifyInOrder failed at step 2/2.", ex.Message);
        Assert.Contains("IEmailService.Send(_, _)", ex.Message);
        Assert.Contains("IUserRepository.Delete(_)", ex.Message);
        Assert.Contains("FAIL  no match after call 1", ex.Message);
        Assert.Contains("call 1  IEmailService  Send(\"a@b.com\", \"hi\")", ex.Message);
    }
}
