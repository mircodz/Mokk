using System.Threading.Tasks;
using Xunit;
using static Mokk.Wildcard;

namespace Mokk.Tests;

// Verify(Times), argument-scoped verification, VerifyInOrder, VerifyNoOtherCalls.
public class VerificationTests
{
    [Fact]
    public void Times_Once_passes_when_called_once()
    {
        var mock = new MockEmailService();
        mock.Send(Any, Any).Returns(true);
        mock.Instance.Send("a@b.com", "hi");

        mock.Send(Any, Any).Verify(Times.Once);
    }

    [Fact]
    public void Times_Once_fails_when_called_twice()
    {
        var mock = new MockEmailService();
        mock.Send(Any, Any).Returns(true);
        mock.Instance.Send("a@b.com", "hi");
        mock.Instance.Send("b@b.com", "hi");

        Assert.Throws<VerificationException>(() => mock.Send(Any, Any).Verify(Times.Once));
    }

    [Fact]
    public void Times_Never_passes_when_not_called()
    {
        var mock = new MockEmailService();
        mock.Send(Any, Any).Verify(Times.Never);
    }

    [Fact]
    public void Times_Never_fails_when_called()
    {
        var mock = new MockEmailService();
        mock.Send(Any, Any).Returns(true);
        mock.Instance.Send("a@b.com", "hi");

        Assert.Throws<VerificationException>(() => mock.Send(Any, Any).Verify(Times.Never));
    }

    [Fact]
    public void Times_AtLeastOnce_passes_when_called_multiple_times()
    {
        var mock = new MockEmailService();
        mock.Send(Any, Any).Returns(true);
        mock.Instance.Send("a@b.com", "hi");
        mock.Instance.Send("b@b.com", "hi");

        mock.Send(Any, Any).Verify(Times.AtLeastOnce);
    }

    [Fact]
    public void Times_AtLeastOnce_fails_when_never_called()
    {
        var mock = new MockEmailService();
        Assert.Throws<VerificationException>(() => mock.Send(Any, Any).Verify(Times.AtLeastOnce));
    }

    [Fact]
    public void Times_Exactly_passes()
    {
        var mock = new MockEmailService();
        mock.Send(Any, Any).Returns(true);
        mock.Instance.Send("a@b.com", "hi");
        mock.Instance.Send("b@b.com", "hi");
        mock.Instance.Send("c@b.com", "hi");

        mock.Send(Any, Any).Verify(Times.Exactly(3));
    }

    [Fact]
    public void Times_AtLeast_passes_when_count_is_sufficient()
    {
        var mock = new MockEmailService();
        mock.Send(Any, Any).Returns(true);
        mock.Instance.Send("a@b.com", "hi");
        mock.Instance.Send("b@b.com", "hi");

        mock.Send(Any, Any).Verify(Times.AtLeast(2));
    }

    [Fact]
    public void Times_AtMost_passes_when_under_limit()
    {
        var mock = new MockEmailService();
        mock.Send(Any, Any).Returns(true);
        mock.Instance.Send("a@b.com", "hi");

        mock.Send(Any, Any).Verify(Times.AtMost(3));
    }

    [Fact]
    public void Times_AtMost_fails_when_over_limit()
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
    public void Times_Between_passes_when_in_range()
    {
        var mock = new MockEmailService();
        mock.Send(Any, Any).Returns(true);
        mock.Instance.Send("a@b.com", "hi");
        mock.Instance.Send("b@b.com", "hi");
        mock.Instance.Send("c@b.com", "hi");

        mock.Send(Any, Any).Verify(Times.Between(2, 4));
    }

    [Fact]
    public void Times_Between_fails_when_outside_range()
    {
        var mock = new MockEmailService();
        mock.Send(Any, Any).Returns(true);
        mock.Instance.Send("a@b.com", "hi");

        Assert.Throws<VerificationException>(() => mock.Send(Any, Any).Verify(Times.Between(2, 4)));
    }

    [Fact]
    public void Verify_is_scoped_to_matching_arguments()
    {
        var mock = new MockEmailService();
        mock.Send(Any, Any).Returns(true);

        mock.Instance.Send("admin@site.com", "s1");
        mock.Instance.Send("admin@site.com", "s2");
        mock.Instance.Send("other@site.com", "s3");

        mock.Send("admin@site.com", Any).Verify(Times.Exactly(2));
    }

    [Fact]
    public void Verify_works_with_void_methods()
    {
        var repo = new MockUserRepository();
        repo.Instance.Delete(1);
        repo.Instance.Delete(2);

        repo.Delete(Any).Verify(Times.Exactly(2));
    }

    [Fact]
    public void InOrder_passes_when_calls_happen_in_order()
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
    public void InOrder_fails_when_calls_happen_in_wrong_order()
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
    public void InOrder_allows_interleaved_calls_between_steps()
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
    public void InOrder_respects_matchers_per_step()
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
    public void InOrder_failure_message_names_the_missing_step()
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
    public void InOrder_works_with_void_and_non_void_mixed()
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
    public void NoOtherCalls_passes_when_no_calls_made()
    {
        var mock = new MockEmailService();
        mock.VerifyNoOtherCalls();
    }

    [Fact]
    public void NoOtherCalls_passes_when_all_calls_verified()
    {
        var mock = new MockEmailService();
        mock.Send(Any, Any).Returns(true);
        mock.Instance.Send("a@b.com", "hi");

        mock.Send(Any, Any).Verify(Times.Once);
        mock.VerifyNoOtherCalls();
    }

    [Fact]
    public void NoOtherCalls_fails_when_a_call_was_not_verified()
    {
        var mock = new MockEmailService();
        mock.Send(Any, Any).Returns(true);
        mock.Instance.Send("a@b.com", "hi");

        Assert.Throws<VerificationException>(() => mock.VerifyNoOtherCalls());
    }

    [Fact]
    public void NoOtherCalls_fails_when_second_call_not_verified()
    {
        var mock = new MockEmailService();
        mock.Send(Any, Any).Returns(true);
        mock.Instance.Send("a@b.com", "hi");
        mock.Instance.Send("b@b.com", "hi");

        mock.Send("a@b.com", Any).Verify(Times.Once);

        Assert.Throws<VerificationException>(() => mock.VerifyNoOtherCalls());
    }

    [Fact]
    public void NoOtherCalls_multiple_verifies_cover_multiple_calls()
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
    public void NoOtherCalls_wildcard_verify_covers_all_matching_calls()
    {
        var mock = new MockEmailService();
        mock.Send(Any, Any).Returns(true);
        mock.Instance.Send("a@b.com", "hi");
        mock.Instance.Send("b@b.com", "hello");

        mock.Send(Any, Any).Verify(Times.Exactly(2));
        mock.VerifyNoOtherCalls();
    }

    [Fact]
    public void NoOtherCalls_different_methods_each_need_verification()
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
    public void Reset_clears_verified_state()
    {
        var mock = new MockEmailService();
        mock.Send(Any, Any).Returns(true);
        mock.Instance.Send("a@b.com", "hi");
        mock.Send(Any, Any).Verify(Times.Once);

        mock.Reset();

        mock.VerifyNoOtherCalls();
    }

    [Fact]
    public void Verify_failure_message_lists_recorded_calls_and_marks_matches()
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
    public void VerifyInOrder_failure_message_shows_step_breakdown_and_log()
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
