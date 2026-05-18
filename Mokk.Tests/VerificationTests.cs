using System.Threading.Tasks;
using Xunit;
using static Mokk.Wildcard;

namespace Mokk.Tests;

public class VerificationTests
{
    [Fact]
    public void Times_Once_Passes_When_Called_Once()
    {
        var mock = new MockEmailService();
        mock.Send(Any, Any).Returns(true);
        mock.Instance.Send("a@b.com", "hi");

        mock.Send(Any, Any).Verify(Times.Once);
    }

    [Fact]
    public void Times_Once_Fails_When_Called_Twice()
    {
        var mock = new MockEmailService();
        mock.Send(Any, Any).Returns(true);
        mock.Instance.Send("a@b.com", "hi");
        mock.Instance.Send("b@b.com", "hi");

        Assert.Throws<VerificationException>(() => mock.Send(Any, Any).Verify(Times.Once));
    }

    [Fact]
    public void Times_Never_Passes_When_Not_Called()
    {
        var mock = new MockEmailService();
        mock.Send(Any, Any).Verify(Times.Never);
    }

    [Fact]
    public void Times_Never_Fails_When_Called()
    {
        var mock = new MockEmailService();
        mock.Send(Any, Any).Returns(true);
        mock.Instance.Send("a@b.com", "hi");

        Assert.Throws<VerificationException>(() => mock.Send(Any, Any).Verify(Times.Never));
    }

    [Fact]
    public void Times_AtLeastOnce_Passes_When_Called_Multiple_Times()
    {
        var mock = new MockEmailService();
        mock.Send(Any, Any).Returns(true);
        mock.Instance.Send("a@b.com", "hi");
        mock.Instance.Send("b@b.com", "hi");

        mock.Send(Any, Any).Verify(Times.AtLeastOnce);
    }

    [Fact]
    public void Times_AtLeastOnce_Fails_When_Never_Called()
    {
        var mock = new MockEmailService();
        Assert.Throws<VerificationException>(() => mock.Send(Any, Any).Verify(Times.AtLeastOnce));
    }

    [Fact]
    public void Times_Exactly_Passes()
    {
        var mock = new MockEmailService();
        mock.Send(Any, Any).Returns(true);
        mock.Instance.Send("a@b.com", "hi");
        mock.Instance.Send("b@b.com", "hi");
        mock.Instance.Send("c@b.com", "hi");

        mock.Send(Any, Any).Verify(Times.Exactly(3));
    }

    [Fact]
    public void Times_AtLeast_Passes_When_Count_Is_Sufficient()
    {
        var mock = new MockEmailService();
        mock.Send(Any, Any).Returns(true);
        mock.Instance.Send("a@b.com", "hi");
        mock.Instance.Send("b@b.com", "hi");

        mock.Send(Any, Any).Verify(Times.AtLeast(2));
    }

    [Fact]
    public void Times_AtMost_Passes_When_Under_Limit()
    {
        var mock = new MockEmailService();
        mock.Send(Any, Any).Returns(true);
        mock.Instance.Send("a@b.com", "hi");

        mock.Send(Any, Any).Verify(Times.AtMost(3));
    }

    [Fact]
    public void Times_AtMost_Fails_When_Over_Limit()
    {
        var mock = new MockEmailService();
        mock.Send(Any, Any).Returns(true);
        mock.Instance.Send("a@b.com", "hi");
        mock.Instance.Send("b@b.com", "hi");
        mock.Instance.Send("c@b.com", "hi");
        mock.Instance.Send("d@b.com", "hi");

        Assert.Throws<VerificationException>(() => mock.Send(Any, Any).Verify(Times.AtMost(3)));
    }

    [Fact]
    public void Times_Between_Passes_When_In_Range()
    {
        var mock = new MockEmailService();
        mock.Send(Any, Any).Returns(true);
        mock.Instance.Send("a@b.com", "hi");
        mock.Instance.Send("b@b.com", "hi");
        mock.Instance.Send("c@b.com", "hi");

        mock.Send(Any, Any).Verify(Times.Between(2, 4));
    }

    [Fact]
    public void Times_Between_Fails_When_Outside_Range()
    {
        var mock = new MockEmailService();
        mock.Send(Any, Any).Returns(true);
        mock.Instance.Send("a@b.com", "hi");

        Assert.Throws<VerificationException>(() => mock.Send(Any, Any).Verify(Times.Between(2, 4)));
    }

    [Fact]
    public void Verify_Is_Scoped_To_Matching_Arguments()
    {
        var mock = new MockEmailService();
        mock.Send(Any, Any).Returns(true);

        mock.Instance.Send("admin@site.com", "s1");
        mock.Instance.Send("admin@site.com", "s2");
        mock.Instance.Send("other@site.com", "s3");

        mock.Send("admin@site.com", Any).Verify(Times.Exactly(2));
    }

    [Fact]
    public void Verify_Works_With_Void_Methods()
    {
        var repo = new MockUserRepository();
        repo.Instance.Delete(1);
        repo.Instance.Delete(2);

        repo.Delete(Any).Verify(Times.Exactly(2));
    }

    [Fact]
    public void InOrder_Passes_When_Calls_Happen_In_Order()
    {
        var mock = new MockEmailService();
        mock.Send(Any, Any).Returns(true);

        mock.Instance.GetTemplate("welcome", 1);
        mock.Instance.Send("a@b.com", "hi");

        mock.VerifyInOrder(
            mock.GetTemplate(Any, Any),
            mock.Send(Any, Any)
        );
    }

    [Fact]
    public void InOrder_Fails_When_Calls_Happen_In_Wrong_Order()
    {
        var mock = new MockEmailService();
        mock.Send(Any, Any).Returns(true);

        mock.Instance.Send("a@b.com", "hi");
        mock.Instance.GetTemplate("welcome", 1);

        Assert.Throws<VerificationException>(() =>
            mock.VerifyInOrder(
                mock.GetTemplate(Any, Any),
                mock.Send(Any, Any)
            ));
    }

    [Fact]
    public void InOrder_Allows_Interleaved_Calls_Between_Steps()
    {
        var mock = new MockEmailService();
        mock.Send(Any, Any).Returns(true);

        mock.Instance.GetTemplate("welcome", 1);
        mock.Instance.GetTemplate("footer", 2);  // interleaved - should be ignored
        mock.Instance.Send("a@b.com", "hi");

        mock.VerifyInOrder(
            mock.GetTemplate(Any, Any),
            mock.Send(Any, Any)
        );
    }

    [Fact]
    public void InOrder_Respects_Matchers_Per_Step()
    {
        var mock = new MockEmailService();
        mock.Send(Any, Any).Returns(true);

        mock.Instance.Send("a@b.com", "hi");
        mock.Instance.Send("b@b.com", "hi");

        mock.VerifyInOrder(
            mock.Send("a@b.com", Any),
            mock.Send("b@b.com", Any)
        );
    }

    [Fact]
    public void InOrder_Failure_Message_Names_The_Missing_Step()
    {
        var mock = new MockEmailService();

        mock.Instance.GetTemplate("welcome", 1);

        var ex = Assert.Throws<VerificationException>(() =>
            mock.VerifyInOrder(
                mock.GetTemplate(Any, Any),
                mock.Send(Any, Any)
            ));

        Assert.Contains("Send", ex.Message);
        Assert.Contains("GetTemplate", ex.Message);
    }

    [Fact]
    public void InOrder_Works_With_Void_And_Non_Void_Mixed()
    {
        var mock = new MockUserRepository();
        mock.GetUserAsync(Any).Returns((int id) => Task.FromResult($"User#{id}"));

        mock.Instance.GetUserAsync(1);
        mock.Instance.Delete(1);

        mock.VerifyInOrder(
            mock.GetUserAsync(Any),
            mock.Delete(Any)
        );
    }

    [Fact]
    public void NoOtherCalls_Passes_When_No_Calls_Made()
    {
        var mock = new MockEmailService();
        mock.VerifyNoOtherCalls();
    }

    [Fact]
    public void NoOtherCalls_Passes_When_All_Calls_Verified()
    {
        var mock = new MockEmailService();
        mock.Send(Any, Any).Returns(true);
        mock.Instance.Send("a@b.com", "hi");

        mock.Send(Any, Any).Verify(Times.Once);
        mock.VerifyNoOtherCalls();
    }

    [Fact]
    public void NoOtherCalls_Fails_When_A_Call_Was_Not_Verified()
    {
        var mock = new MockEmailService();
        mock.Send(Any, Any).Returns(true);
        mock.Instance.Send("a@b.com", "hi");

        Assert.Throws<VerificationException>(() => mock.VerifyNoOtherCalls());
    }

    [Fact]
    public void NoOtherCalls_Fails_When_Second_Call_Not_Verified()
    {
        var mock = new MockEmailService();
        mock.Send(Any, Any).Returns(true);
        mock.Instance.Send("a@b.com", "hi");
        mock.Instance.Send("b@b.com", "hi");

        mock.Send("a@b.com", Any).Verify(Times.Once);

        Assert.Throws<VerificationException>(() => mock.VerifyNoOtherCalls());
    }

    [Fact]
    public void NoOtherCalls_Multiple_Verifies_Cover_Multiple_Calls()
    {
        var mock = new MockEmailService();
        mock.Send(Any, Any).Returns(true);
        mock.Instance.Send("a@b.com", "hi");
        mock.Instance.Send("b@b.com", "hi");

        mock.Send("a@b.com", Any).Verify(Times.Once);
        mock.Send("b@b.com", Any).Verify(Times.Once);
        mock.VerifyNoOtherCalls();
    }

    [Fact]
    public void NoOtherCalls_Wildcard_Verify_Covers_All_Matching_Calls()
    {
        var mock = new MockEmailService();
        mock.Send(Any, Any).Returns(true);
        mock.Instance.Send("a@b.com", "hi");
        mock.Instance.Send("b@b.com", "hello");

        mock.Send(Any, Any).Verify(Times.Exactly(2));
        mock.VerifyNoOtherCalls();
    }

    [Fact]
    public void NoOtherCalls_Different_Methods_Each_Need_Verification()
    {
        var mock = new MockEmailService();
        mock.Send(Any, Any).Returns(true);
        mock.GetTemplate(Any, Any).Returns("t");
        mock.Instance.Send("a@b.com", "hi");
        mock.Instance.GetTemplate("welcome", 1);

        mock.Send(Any, Any).Verify(Times.Once);
        Assert.Throws<VerificationException>(() => mock.VerifyNoOtherCalls());
    }

    [Fact]
    public void Reset_Clears_Verified_State()
    {
        var mock = new MockEmailService();
        mock.Send(Any, Any).Returns(true);
        mock.Instance.Send("a@b.com", "hi");
        mock.Send(Any, Any).Verify(Times.Once);

        mock.Reset();

        mock.VerifyNoOtherCalls();
    }

    [Fact]
    public void Verify_Failure_Message_Lists_Recorded_Calls_And_Marks_Matches()
    {
        var mock = new MockEmailService();
        mock.Send(Any, Any).Returns(true);
        mock.Instance.Send("admin@site.com", "hello");
        mock.Instance.Send("other@site.com", "x");

        var ex = Assert.Throws<VerificationException>(() =>
            mock.Send("admin@site.com", Any).Verify(Times.Exactly(2)));

        Assert.Contains("expected exactly 2 calls, got 1", ex.Message);
        Assert.Contains("Recorded on IEmailService:", ex.Message);
        Assert.Contains("Send(\"admin@site.com\", \"hello\")   [matched]", ex.Message);
        Assert.Contains("Send(\"other@site.com\", \"x\")", ex.Message);
    }

    [Fact]
    public void VerifyInOrder_Failure_Message_Shows_Step_Breakdown_And_Log()
    {
        var mock = new MockEmailService();
        mock.Send(Any, Any).Returns(true);

        mock.Instance.Send("a@b.com", "hi");
        mock.Instance.GetTemplate("welcome", 1);

        var ex = Assert.Throws<VerificationException>(() =>
            mock.VerifyInOrder(mock.GetTemplate(Any, Any), mock.Send(Any, Any)));

        Assert.Contains("VerifyInOrder on IEmailService failed at step 2/2.", ex.Message);
        Assert.Contains("step 1  GetTemplate(_, _)", ex.Message);
        Assert.Contains("OK    @ call 2", ex.Message);
        Assert.Contains("FAIL  no match after call 2", ex.Message);
        Assert.Contains("call 1  Send(\"a@b.com\", \"hi\")", ex.Message);
    }
}
