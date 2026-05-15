# Mokk

C# mocking library powered by Roslyn source generators.

## Installation

```
dotnet add package Mokk
```

The package includes both the runtime library and the source generator.

```csharp
[assembly: GenerateMock(typeof(IEmailService))]

var mock = new MockEmailService();

mock.Send(Any, Any).Returns(true);
mock.GetTemplate("welcome", Any).Returns("Hi there!");

mock.Instance.Send("alice@example.com", "Welcome!");

mock.Send(Any, Any).Verify(Times.Once);
```

## Setup

```csharp
using Mokk;

[assembly: GenerateMock(typeof(IEmailService))]
[assembly: GenerateMock(typeof(IUserRepository))]
[assembly: GenerateMock(typeof(AbstractNotificationService))]
```

## Factory (C# 14, opt-in)

```csharp
var mock = new MockEmailService();   // always works
var mock = IEmailService.Mock();     // with <DefineConstants>MOKK_CSHARP14</DefineConstants> + LangVersion 14
```

`Mock(strict:, wrapping:, onUnusedSetup:)` forwards to the constructor.

## Matchers

```csharp
using static Mokk.Wildcard;

mock.Send(Any, Any).Returns(true);
mock.Send("alice@example.com", Any).Returns(true);
mock.Send(Arg.Is<string>(s => s.EndsWith(".com")), Any).Returns(true);
```

## Returns

```csharp
mock.GetTemplate(Any, Any).Returns("hello");
mock.GetTemplate(Any, Any).Returns(() => ComputeValue());
mock.GetTemplate(Any, Any).Returns((string name, int ver) => $"{name}-v{ver}");
```

## Callbacks and Throws

```csharp
mock.Send(Any, Any)
    .Callback((string to, string subject) => log.Add($"{to}: {subject}"))
    .Returns(true);

mock.Send("bad@actor.com", Any).Throws<UnauthorizedException>();
```

## Sequence setup

```csharp
mock.GetTemplate(Any, Any).Sequence()
    .Returns("first response")
    .Returns("second response")
    .Throws(new Exception("exhausted"));
```

## Async

```csharp
mock.GetUserAsync(Any).ReturnsAsync("Alice");
mock.GetUserAsync(Any).ReturnsAsync(() => Compute());
mock.GetUserAsync(Any).ThrowsAsync(new IOException()); // faulted task, not a sync throw

mock.GetUserAsync(Any).Sequence()
    .ReturnsAsync("first")
    .ThrowsAsync(new Exception("exhausted"));
```

## ref / out / in

```csharp
// out values are set from a callback; out params drop out of the setup signature
mock.TryParse("42").Callback(args => args[1] = 42).Returns(true);
mock.Instance.TryParse("42", out int value); // value == 42

mock.Increment(Any).Callback(args => args[0] = (int)args[0]! + 1);
```

## Generic methods

```csharp
mock.DoSomething<int>(Any).Returns(42);
mock.DoSomething<string>(Any).Returns("hello");
mock.DoSomething<AnyType>(Any).Callback(() => count++); // matches any T
```

## Generic types

```csharp
[assembly: GenerateMock(typeof(IMessage<,>))] // closed types collapse to this too

var mock = new MockMessage<string, int>();
mock.Get("answer").Returns(42);
```

## Properties

```csharp
// Auto-backed
mock.Instance.Name = "Alice";
Assert.Equal("Alice", mock.Instance.Name);

mock.Name.Getter().Returns("Alice");
mock.Name.Setter(Any).Verify(Times.Once);
```

## Events

```csharp
mock.Instance.UserChanged += (sender, e) => ...;

mock.UserChanged.Raise(mock.Instance, new UserChangedEventArgs(42));
mock.AuditLogged.Raise(7, "deleted"); // custom delegate: positional args
Assert.Equal(1, mock.UserChanged.SubscriberCount);

mock.UserChanged.Subscribed(handler, Times.Once);
mock.UserChanged.Unsubscribed(Times.Never);
mock.UserChanged.HandlerInvoked(handler, Times.Exactly(2));
```

Abstract-class mocks expose the raiser as `{EventName}Handle`.

## Verify

```csharp
mock.Send(Any, Any).Verify(Times.Once);
mock.Send(Any, Any).Verify(Times.Never);
mock.Send(Any, Any).Verify(Times.Exactly(3));
mock.Send(Any, Any).Verify(Times.AtLeast(2));
mock.Send(Any, Any).Verify(Times.AtMost(5));
mock.Send(Any, Any).Verify(Times.Between(2, 5));
```

`Verify` throws `VerificationException` on failure.

## VerifyInOrder

Assert relative call order on a single mock:

```csharp
mock.VerifyInOrder(
    mock.Login(Any),
    mock.GetUser(Any),
    mock.Logout()
);
```

## VerifyNoOtherCalls

```csharp
mock.Send(Any, Any).Returns(true);
mock.Instance.Send("a@b.com", "hi");

mock.Send(Any, Any).Verify(Times.Once);
mock.VerifyNoOtherCalls(); // passes — all calls were covered
```

## ReceivedCalls

Programmatic access to the call log:

```csharp
var calls = mock.Send(Any, Any).ReceivedCalls();
Assert.Equal(2, calls.Count);
Assert.Equal("alice@example.com", (string)calls[0].Args[0]);
```

## Argument capture

```csharp
using static Mokk.Capture;

var slot = Slot<string>();
mock.Send(Into(slot), Any).Returns(true);

mock.Instance.Send("hello@test.com", "subject");

Assert.Equal("hello@test.com", slot.Value);
```

## Wrapping

```csharp
var mock = new MockEmailService(wrapping: new RealEmailService(smtpClient));

// Overrides one method; all others delegate to the real implementation
mock.GetTemplate("welcome", 1).Returns("cached");

mock.Instance.Send("alice@example.com", "hi"); // calls real Send
mock.Send(Any, Any).Verify(Times.Once);
```

## Abstract class mocks

```csharp
[assembly: GenerateMock(typeof(AbstractNotificationService))]

var mock = new MockNotificationService();
mock.Notify(Any, Any).Returns(true);

mock.ServiceNameHandle.Getter().Returns("test-service");

Assert.True(mock.Instance.Notify("user@test.com", "Hello"));
mock.Notify(Any, Any).Verify(Times.Once);
```

## Strict mode

```csharp
var mock = new MockEmailService(strict: true);
mock.Instance.Send("a@b.com", "hi"); // throws MissingSetupException — no setup matched
```

## Unused setup warnings

```csharp
var warnings = new List<string>();
var mock = new MockEmailService(onUnusedSetup: warnings.Add);

mock.GetTemplate(Any, Any).Returns("Hi!");
mock.Instance.Send("a@b.com", "hello");

mock.CheckUnusedSetups();
```

## Reset

```csharp
mock.Reset();
```

## Benchmarks

```
BenchmarkDotNet v0.15.8, .NET 8.0, Linux, 12th Gen Intel Core i7-12700KF

| Method      | Mean       | Error      | StdDev     | Rank | Gen0   | Gen1   | Allocated |
|------------ |-----------:|-----------:|-----------:|-----:|-------:|-------:|----------:|
| Mokk        | 164.042 ns |  8.3301 ns |  4.3568 ns |    1 | 0.0219 | 0.0055 |    1056 B |
| Imposter    | 202.634 ns | 11.0567 ns |  7.3133 ns |    2 | 0.0124 | 0.0119 |     168 B |
| Moq         | 290.735 ns | 14.6240 ns |  9.6729 ns |    3 | 0.0205 | 0.0100 |     528 B |
| NSubstitute | 357.505 ns | 11.7103 ns |  7.7456 ns |    4 | 0.0219 | 0.0110 |     304 B |
| FakeItEasy  | 953.988 ns | 30.6226 ns | 20.2550 ns |    5 | 0.0591 | 0.0572 |     808 B |
```
