using System.Threading.Tasks;
using Mokk;

[assembly: GenerateMock(typeof(Mokk.Tests.IEmailService))]
[assembly: GenerateMock(typeof(Mokk.Tests.IUserRepository))]
[assembly: GenerateMock(typeof(Mokk.Tests.IBaseService))]
[assembly: GenerateMock(typeof(Mokk.Tests.IExtendedService))]
[assembly: GenerateMock(typeof(Mokk.Tests.ITemplatedService))]
[assembly: GenerateMock(typeof(Mokk.Tests.IConstrained))]
[assembly: GenerateMock(typeof(Mokk.Tests.AbstractFactory))]
[assembly: GenerateMock(typeof(Mokk.Tests.AbstractNotificationService))]
[assembly: GenerateMock(typeof(Mokk.Tests.IMessage))]
[assembly: GenerateMock(typeof(Mokk.Tests.IMessage<>))]
[assembly: GenerateMock(typeof(Mokk.Tests.IMessage<,>))]
[assembly: GenerateMock(typeof(Mokk.Tests.IBox<>))]
[assembly: GenerateMock(typeof(Mokk.Tests.AbstractCache<,>))]
[assembly: GenerateMock(typeof(Mokk.Tests.IParser))]
[assembly: GenerateMock(typeof(Mokk.Tests.AbstractRefSink))]
[assembly: GenerateMock(typeof(Mokk.Tests.IInventory))]
[assembly: GenerateMock(typeof(Mokk.Tests.IGrid))]
[assembly: GenerateMock(typeof(Mokk.Tests.AbstractLookup))]
[assembly: GenerateMock(typeof(Mokk.Tests.IInitOnly))]
[assembly: GenerateMock(typeof(Mokk.Tests.IRefReturn))]
[assembly: GenerateMock(typeof(Mokk.Tests.AbstractSeeded))]

namespace Mokk.Tests;

public interface IEmailService
{
    bool Send(string to, string subject);
    string GetTemplate(string name, int version);
}

public class UserChangedEventArgs(int userId) : System.EventArgs
{
    public int UserId { get; } = userId;
}

public interface IUserRepository
{
    string Name { get; set; }
    int Age { get; }
    Task<string> GetUserAsync(int id);
    ValueTask<int> CountAsync();
    Task SaveAsync();
    ValueTask FlushAsync();
    void Delete(int id);

    event System.EventHandler<UserChangedEventArgs> UserChanged;
    event System.Action<int, string> AuditLogged;
}

public interface IBaseService
{
    string GetName();
}

public interface IExtendedService : IBaseService
{
    int GetCount();
}

public interface ITemplatedService
{
    T DoSomething<T>(T value);
}

public interface IConstrained
{
    T Create<T>() where T : class, new();
    void Store<TKey, TValue>(TKey key, TValue value) where TKey : notnull where TValue : struct;
}

// Abstract class with a constrained generic method: the override must NOT
// restate the constraints (CS0460), unlike the interface implicit impl.
public abstract class AbstractFactory
{
    public abstract T Make<T>(string tag) where T : class, new();
}

// Same base name, three different arities, all mocked in one assembly.
public interface IMessage
{
    string Describe();
}

public interface IMessage<T>
{
    T Echo(T value);
}

public interface IMessage<TKey, TValue>
{
    TValue Get(TKey key);
    void Put(TKey key, TValue value);
    TKey LastKey { get; set; }
    event System.Action<TKey, TValue> Updated;
}

public class Widget
{
    public int Id { get; set; }
}

public interface IBox<T> where T : class, new()
{
    T Create();
    bool Contains(T item);
}

public abstract class AbstractCache<TKey, TValue> where TKey : notnull
{
    public abstract TValue Load(TKey key);
    public virtual bool Has(TKey key) => false;
}

public interface IParser
{
    bool TryParse(string text, out int value);
    void Increment(ref int counter);
    int Sum(in int a, in int b);
}

public abstract class AbstractRefSink
{
    public abstract bool TryTake(out int value);
}

// Settable single-arg indexer alongside a normal property, and derives the
// whole IReadOnlyList<T> chain (IReadOnlyCollection<T>, IEnumerable<T>,
// IEnumerable) the way real collection interfaces do.
public interface IInventory : System.Collections.Generic.IReadOnlyList<int>
{
    int this[string sku] { get; set; }
}

// Read-only multi-parameter indexer.
public interface IGrid
{
    string this[int row, int col] { get; }
}

// Abstract class with a virtual indexer to exercise the override path.
public abstract class AbstractLookup
{
    public abstract string this[int id] { get; set; }
    public virtual int Capacity => 0;
}

public interface IInitOnly
{
    int Id { get; init; }
    string Name { get; init; }
}

// Mix of ref / ref readonly returns and a normal method: the ref members get
// throwing stubs, the normal one stays fully mockable.
public interface IRefReturn
{
    ref int Slot();
    ref readonly int Peek();
    int Normal(int x);
}

// No accessible parameterless constructor: the mock must chain to base(int).
public abstract class AbstractSeeded
{
    protected AbstractSeeded(int seed) => Seed = seed;
    public int Seed { get; }
    public abstract int Next(int step);
}

// Real implementation used by wrapping tests
public class RealEmailService : IEmailService
{
    public bool Send(string to, string subject) => true;
    public string GetTemplate(string name, int version) => $"real:{name}-v{version}";
}

// Abstract class used to test abstract class mock generation
public abstract class AbstractNotificationService
{
    public abstract bool Notify(string recipient, string message);
    public abstract string GetStatus(int id);
    public virtual string ServiceName => "base";
    protected abstract void Log(string entry);
    public abstract event System.EventHandler<UserChangedEventArgs> StatusChanged;
}