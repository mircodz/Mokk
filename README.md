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

## Generic methods

```csharp
mock.DoSomething<int>(Any).Returns(42);
mock.DoSomething<string>(Any).Returns("hello");
mock.DoSomething<AnyType>(Any).Callback(() => count++); // matches any T
```

## Generic types

Register the open (unbound) generic and close it at the use site:

```csharp
public interface IMessage<TKey, TValue>
{
    TValue Get(TKey key);
    void Put(TKey key, TValue value);
}

[assembly: GenerateMock(typeof(IMessage<,>))]   // unbound
// typeof(IMessage<string,int>) also works — it's collapsed to the open form

var mock = new MockMessage<string, int>();
mock.Get("answer").Returns(42);
Assert.Equal(42, mock.Instance.Get("answer"));
```

Type-parameter constraints (`where T : class, new()`, `notnull`, etc.) are
carried onto the generated mock. The same base name at different arities can
all be registered in one assembly — `IMessage`, `IMessage<T>` and
`IMessage<TKey,TValue>` become `MockMessage`, `MockMessage<T>` and
`MockMessage<TKey,TValue>`, distinguished by generic arity like any C# type.

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
public interface INotifier
{
    event EventHandler<UserChangedEventArgs> UserChanged;
    event Action<int, string> AuditLogged;
}

var mock = new MockNotifier();

// Subscribe through the mocked instance as usual
mock.Instance.UserChanged += (sender, e) => Console.WriteLine(e.UserId);

// Raise the event from the test
mock.UserChanged.Raise(mock.Instance, new UserChangedEventArgs(42));

// Custom delegate types: positional args
mock.AuditLogged.Raise(7, "deleted");

// Inspect subscriptions
Assert.Equal(1, mock.UserChanged.SubscriberCount);
```

On abstract-class mocks the raiser is exposed as `{EventName}Handle` (the mock *is*
the class and can't reuse the event name), e.g. `mock.StatusChangedHandle.Raise(...)`.

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
