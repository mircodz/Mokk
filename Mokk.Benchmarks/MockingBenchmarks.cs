using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Order;
using FakeItEasy;
using NSubstitute;
using MoqMock = Moq.Mock<Mokk.Benchmarks.IBenchmarkService>;
using MoqIt = Moq.It;
using NSArg = NSubstitute.Arg;
using Imposter.Abstractions;

namespace Mokk.Benchmarks;

/// <summary>
/// Measures the cost of a single method call on a pre-configured mock.
/// Setup is done once in GlobalSetup - only the call itself is timed.
/// The Mokk call log is reset after each iteration to prevent unbounded growth.
/// </summary>
[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.Net80, warmupCount: 3, iterationCount: 10)]
[Orderer(SummaryOrderPolicy.FastestToSlowest)]
[RankColumn]
public class MethodCallBenchmarks
{
    private MockBenchmarkService _spectermock = null!;
    private IBenchmarkService _specter = null!;
    private IBenchmarkService _moq = null!;
    private IBenchmarkService _nsubstitute = null!;
    private IBenchmarkService _fakeItEasy = null!;
    private IBenchmarkService _imposter = null!;

    [GlobalSetup]
    public void Setup()
    {
        // Mokk
        _spectermock = new MockBenchmarkService();
        _spectermock.Process(Wildcard.Any).Returns(true);
        _specter = _spectermock.Instance;

        // Moq
        var m = new MoqMock();
        m.Setup(x => x.Process(MoqIt.IsAny<string>())).Returns(true);
        _moq = m.Object;

        // NSubstitute
        _nsubstitute = Substitute.For<IBenchmarkService>();
        _nsubstitute.Process(NSArg.Any<string>()).Returns(true);

        // FakeItEasy
        _fakeItEasy = A.Fake<IBenchmarkService>();
        A.CallTo(() => _fakeItEasy.Process(A<string>._)).Returns(true);

        // Imposter (fully qualified: Mokk.Arg<T> would otherwise win name lookup
        // from the enclosing Mokk namespace).
        var imp = new IBenchmarkServiceImposter();
        imp.Process(global::Imposter.Abstractions.Arg<string>.Any()).Returns(true);
        _imposter = imp.Instance();
    }

    [IterationSetup]
    public void IterationSetup() => _spectermock.Reset();

    [Benchmark(Description = "Mokk")]
    public bool Mokk() => _specter.Process("hello");

    [Benchmark(Description = "Moq")]
    public bool Moq() => _moq.Process("hello");

    [Benchmark(Description = "NSubstitute")]
    public bool NSubstitute() => _nsubstitute.Process("hello");

    [Benchmark(Description = "FakeItEasy")]
    public bool FakeItEasy() => _fakeItEasy.Process("hello");

    [Benchmark(Description = "Imposter")]
    public bool Imposter() => _imposter.Process("hello");
}
