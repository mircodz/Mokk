using System.Threading.Tasks;
using Xunit;
using static Mokk.Wildcard;

namespace Mokk.Tests;

public class VerificationTests
{
    [Fact]
    public void Exactly_N_calls()
    {
        var mock = new MockEmailService();
        mock.Send(Any, Any).Returns(true);

        mock.Instance.Send("a@test.com", "s1");
        mock.Instance.Send("b@test.com", "s2");

        mock.Send(Any, Any).Verify(Times.Exactly(2));
    }

    [Fact]
    public void AtLeast_N_calls()
    {
        var mock = new MockEmailService();
        mock.Send(Any, Any).Returns(true);

        mock.Instance.Send("a@test.com", "s1");
        mock.Instance.Send("b@test.com", "s2");
        mock.Instance.Send("c@test.com", "s3");

        mock.Send(Any, Any).Verify(Times.AtLeast(2));
    }

    [Fact]
    public void Fails_when_count_doesnt_match()
    {
        var mock = new MockEmailService();
        mock.Send(Any, Any).Returns(true);
        mock.Instance.Send("a@test.com", "s1");

        Assert.Throws<VerificationException>(() =>
            mock.Send(Any, Any).Verify(Times.Exactly(2)));
    }

    [Fact]
    public void Scoped_to_specific_argument()
    {
        var mock = new MockEmailService();
        mock.Send(Any, Any).Returns(true);

        mock.Instance.Send("admin@site.com", "s1");
        mock.Instance.Send("admin@site.com", "s2");
        mock.Instance.Send("other@site.com", "s3");

        mock.Send("admin@site.com", Any).Verify(Times.Exactly(2));
    }

    [Fact]
    public void Void_method_calls()
    {
        var mock = new MockUserRepository();
        mock.Instance.Delete(1);
        mock.Instance.Delete(2);

        mock.Delete(Any).Verify(Times.Exactly(2));
    }
}

public class VerifyInOrderTests
{
    [Fact]
    public void Passes_when_calls_happen_in_order()
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
    public void Fails_when_calls_happen_in_wrong_order()
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
    public void Allows_interleaved_calls_between_steps()
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
    public void Matchers_are_respected_per_step()
    {
        var mock = new MockEmailService();
        mock.Send(Any, Any).Returns(true);

        mock.Instance.Send("a@b.com", "hi");
        mock.Instance.Send("b@b.com", "hi");

        // First Send to a@b.com, then any Send - order + matchers both apply
        mock.VerifyInOrder(
            mock.Send("a@b.com", Any),
            mock.Send("b@b.com", Any)
        );
    }

    [Fact]
    public void Fails_with_message_naming_the_missing_step()
    {
        var mock = new MockEmailService();

        mock.Instance.GetTemplate("welcome", 1);
        // Send never called

        var ex = Assert.Throws<VerificationException>(() =>
            mock.VerifyInOrder(
                mock.GetTemplate(Any, Any),
                mock.Send(Any, Any)
            ));

        Assert.Contains("Send", ex.Message);
        Assert.Contains("GetTemplate", ex.Message);
    }

    [Fact]
    public void Works_with_void_and_non_void_methods_mixed()
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
}

public class VerifyNoOtherCallsTests
{
    [Fact]
    public void Passes_when_no_calls_made()
    {
        var mock = new MockEmailService();
        mock.VerifyNoOtherCalls();
    }

    [Fact]
    public void Passes_when_all_calls_verified()
    {
        var mock = new MockEmailService();
        mock.Send(Any, Any).Returns(true);
        mock.Instance.Send("a@b.com", "hi");

        mock.Send(Any, Any).Verify(Times.Once);
        mock.VerifyNoOtherCalls();
    }

    [Fact]
    public void Fails_when_call_was_not_verified()
    {
        var mock = new MockEmailService();
        mock.Send(Any, Any).Returns(true);
        mock.Instance.Send("a@b.com", "hi");

        Assert.Throws<VerificationException>(() => mock.VerifyNoOtherCalls());
    }

    [Fact]
    public void Fails_when_second_call_not_verified()
    {
        var mock = new MockEmailService();
        mock.Send(Any, Any).Returns(true);
        mock.Instance.Send("a@b.com", "hi");
        mock.Instance.Send("b@b.com", "hi");

        mock.Send("a@b.com", Any).Verify(Times.Once);

        // second Send was not covered by that Verify
        Assert.Throws<VerificationException>(() => mock.VerifyNoOtherCalls());
    }

    [Fact]
    public void Multiple_verifies_cover_multiple_calls()
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
    public void Wildcard_verify_covers_all_matching_calls()
    {
        var mock = new MockEmailService();
        mock.Send(Any, Any).Returns(true);
        mock.Instance.Send("a@b.com", "hi");
        mock.Instance.Send("b@b.com", "hello");

        mock.Send(Any, Any).Verify(Times.Exactly(2));
        mock.VerifyNoOtherCalls();
    }

    [Fact]
    public void Different_methods_each_need_verification()
    {
        var mock = new MockEmailService();
        mock.Send(Any, Any).Returns(true);
        mock.GetTemplate(Any, Any).Returns("t");
        mock.Instance.Send("a@b.com", "hi");
        mock.Instance.GetTemplate("welcome", 1);

        mock.Send(Any, Any).Verify(Times.Once);
        // GetTemplate not verified
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

        // After reset, no calls - should pass
        mock.VerifyNoOtherCalls();
    }
}

public class TimesTests
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
    public void Times_works_with_void_methods()
    {
        var repo = new MockUserRepository();
        repo.Instance.Delete(1);
        repo.Instance.Delete(2);

        repo.Delete(Any).Verify(Times.Exactly(2));
    }
}
