using System.Threading.Tasks;
using Mokk;

[assembly: GenerateMock(typeof(Mokk.Tests.IEmailService))]
[assembly: GenerateMock(typeof(Mokk.Tests.IUserRepository))]
[assembly: GenerateMock(typeof(Mokk.Tests.IBaseService))]
[assembly: GenerateMock(typeof(Mokk.Tests.IExtendedService))]
[assembly: GenerateMock(typeof(Mokk.Tests.ITemplatedService))]
[assembly: GenerateMock(typeof(Mokk.Tests.AbstractNotificationService))]
[assembly: GenerateMock(typeof(Mokk.Tests.IMessage))]
[assembly: GenerateMock(typeof(Mokk.Tests.IMessage<>))]
[assembly: GenerateMock(typeof(Mokk.Tests.IMessage<,>))]
[assembly: GenerateMock(typeof(Mokk.Tests.IBox<>))]
[assembly: GenerateMock(typeof(Mokk.Tests.AbstractCache<,>))]

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

// Same base name, three different arities, all mocked in one assembly.
public interface IMessage
{
    string Describe();
}

public interface IMessage<T>
{
    T Echo(T value);
}

// Open generic interface: registered as typeof(IMessage<,>)
public interface IMessage<TKey, TValue>
{
    TValue Get(TKey key);
    void Put(TKey key, TValue value);
    TKey LastKey { get; set; }
    event System.Action<TKey, TValue> Updated;
}

// Generic interface with constraints, registered as typeof(IBox<>)
public interface IBox<T> where T : class, new()
{
    T Create();
    bool Contains(T item);
}

// Open generic abstract class: registered as typeof(AbstractCache<,>)
public abstract class AbstractCache<TKey, TValue> where TKey : notnull
{
    public abstract TValue Load(TKey key);
    public virtual bool Has(TKey key) => false;
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