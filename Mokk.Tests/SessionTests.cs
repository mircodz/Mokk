using Xunit;
using static Mokk.Wildcard;

namespace Mokk.Tests;

public class SessionTests
{
    [Fact]
    public void Verifies_Call_Order_Across_Mocks()
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
    public void Fails_When_Cross_Mock_Order_Is_Wrong()
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
    public void Allows_Unrelated_Interleaved_Calls_Between_Steps()
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
    public void Matchers_Are_Respected_Per_Step()
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
    public void Untracked_Mock_Calls_Are_Not_In_The_Timeline()
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
    public void Track_Returns_Session_And_Can_Be_Called_After_Construction()
    {
        var email = new MockEmailService();
        var repo = new MockUserRepository();
        var session = new MockSession().Track(email, repo);

        email.Instance.Send("a@b.com", "hi");
        repo.Instance.Delete(1);

        session.VerifyInOrder(email.Send(Any, Any), repo.Delete(Any));
    }

    [Fact]
    public void Reset_Clears_The_Timeline()
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
    public void Failure_Message_Prefixes_Steps_And_Calls_With_Mock_Type()
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
