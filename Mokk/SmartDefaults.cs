using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;

namespace Mokk;

internal static class SmartDefaults
{
    private static readonly MethodInfo TaskFromResult = typeof(Task).GetMethod(nameof(Task.FromResult))!;

    public static TReturn For<TReturn>()
    {
        var type = typeof(TReturn);

        if (type == typeof(string))
        {
            return (TReturn)(object)"";
        }

        if (type == typeof(Task))
        {
            return (TReturn)(object)Task.CompletedTask;
        }

        if (type.IsArray)
        {
            var element = type.GetElementType()!;
            return (TReturn)(object)Array.CreateInstance(element, 0);
        }

        if (type.IsGenericType)
        {
            var def = type.GetGenericTypeDefinition();
            var args = type.GetGenericArguments();

            if (def == typeof(Task<>))
            {
                var inner = args[0];
                var defaultVal = inner.IsValueType ? Activator.CreateInstance(inner) : null;
                var result = TaskFromResult.MakeGenericMethod(inner).Invoke(null, [defaultVal]);
                return (TReturn)result!;
            }

            if (def == typeof(ValueTask<>))
            {
                var inner = args[0];
                var defaultVal = inner.IsValueType ? Activator.CreateInstance(inner) : null;
                var ctor = type.GetConstructor([inner])!;
                return (TReturn)ctor.Invoke([defaultVal])!;
            }

            if (args.Length == 1 && IsCollectionInterface(def))
            {
                return (TReturn)(object)Array.CreateInstance(args[0], 0);
            }

            if (def == typeof(List<>))
            {
                return (TReturn)Activator.CreateInstance(type)!;
            }
        }

        return default!;
    }

    private static bool IsCollectionInterface(Type def)
        => def == typeof(IEnumerable<>)
        || def == typeof(ICollection<>)
        || def == typeof(IList<>)
        || def == typeof(IReadOnlyList<>)
        || def == typeof(IReadOnlyCollection<>);
}
