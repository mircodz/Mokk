using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Mokk.Generator;

[Generator]
public class MockGenerator : IIncrementalGenerator
{
    private static readonly SymbolDisplayFormat TypeFormat = SymbolDisplayFormat.FullyQualifiedFormat
        .WithGlobalNamespaceStyle(SymbolDisplayGlobalNamespaceStyle.Omitted);

    // Static abstract/virtual members exist only on the type, not an instance,
    // so an interceptor-backed mock can't implement them (and the mock type
    // can't satisfy the consumer's generic constraint either).
    private static readonly DiagnosticDescriptor StaticAbstractUnsupported = new(
        id: "MOKK001",
        title: "Static abstract members cannot be mocked",
        messageFormat: "'{0}' declares static abstract/virtual member(s)",
        category: "Mokk",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    // Ref structs (Span<T> etc.) and pointer/function-pointer types can't be a
    // Matcher<T>/MethodHandle<T> type argument or boxed into the args array, so
    // a member using one is unmockable.
    private static readonly DiagnosticDescriptor RefStructUnsupported = new(
        id: "MOKK002",
        title: "Ref-struct or pointer types cannot be mocked",
        messageFormat: "'{0}' has a member using a ref-struct or pointer type",
        category: "Mokk",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    private static bool HasStaticAbstractMember(INamedTypeSymbol iface)
        => iface.GetMembers()
            .Concat(iface.AllInterfaces.SelectMany(i => i.GetMembers()))
            .Any(m => m.IsStatic && (m.IsAbstract || m.IsVirtual)
                      && m is IMethodSymbol or IPropertySymbol or IEventSymbol);

    private static bool ReferencesUnmockableType(INamedTypeSymbol type)
    {
        static bool Bad(ITypeSymbol t)
            => t.IsRefLikeType || t.TypeKind is TypeKind.Pointer or TypeKind.FunctionPointer;

        var members = new List<ISymbol>();
        for (var t = type; t is not null && t.SpecialType != SpecialType.System_Object; t = t.BaseType)
            members.AddRange(t.GetMembers());
        members.AddRange(type.AllInterfaces.SelectMany(i => i.GetMembers()));

        foreach (var m in members)
        {
            switch (m)
            {
                case IMethodSymbol mm when Bad(mm.ReturnType) || mm.Parameters.Any(p => Bad(p.Type)):
                case IPropertySymbol pp when Bad(pp.Type) || pp.Parameters.Any(p => Bad(p.Type)):
                    return true;
            }
        }
        return false;
    }

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var allTargets = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                "Mokk.GenerateMockAttribute",
                predicate: static (node, _) => node is CompilationUnitSyntax,
                transform: static (ctx, _) => GetTargets(ctx))
            .SelectMany(static (x, _) => x);

        // The C#14 `extension` factory only compiles when the *consumer's*
        // language version is C# 14+. Detect it from parse options rather than
        // the target framework (a net10 project can still pin LangVersion 12).
        var supportsExtensions = context.ParseOptionsProvider.Select(static (po, _) =>
            po is CSharpParseOptions cs
            && (int)cs.LanguageVersion.MapSpecifiedToEffectiveVersion() >= 1400);

        context.RegisterSourceOutput(allTargets.Combine(supportsExtensions), static (spc, pair) =>
        {
            var (symbol, emitFactory) = pair;
            if (symbol.TypeKind == TypeKind.Interface)
                Execute(spc, symbol, emitFactory);
            else
                ExecuteAbstractClass(spc, symbol, emitFactory);
        });
    }

    private static ImmutableArray<INamedTypeSymbol> GetTargets(GeneratorAttributeSyntaxContext context)
    {
        var builder = ImmutableArray.CreateBuilder<INamedTypeSymbol>();
        foreach (var attr in context.Attributes)
        {
            if (attr.ConstructorArguments.Length == 1
                && attr.ConstructorArguments[0].Value is INamedTypeSymbol symbol
                && (symbol.TypeKind == TypeKind.Interface || (symbol.TypeKind == TypeKind.Class && symbol.IsAbstract)))
            {
                // Collapse unbound and closed generics to the open definition (IMessage<T, U>).
                builder.Add(symbol.OriginalDefinition);
            }
        }
        return builder.ToImmutable();
    }

    private static void Execute(SourceProductionContext context, INamedTypeSymbol interfaceSymbol, bool emitFactory)
    {
        if (HasStaticAbstractMember(interfaceSymbol))
        {
            context.ReportDiagnostic(Diagnostic.Create(
                StaticAbstractUnsupported, interfaceSymbol.Locations.FirstOrDefault(), interfaceSymbol.Name));
            return;
        }
        if (ReferencesUnmockableType(interfaceSymbol))
        {
            context.ReportDiagnostic(Diagnostic.Create(
                RefStructUnsupported, interfaceSymbol.Locations.FirstOrDefault(), interfaceSymbol.Name));
            return;
        }

        var interfaceName = interfaceSymbol.Name;
        var ns = interfaceSymbol.ContainingNamespace.IsGlobalNamespace
            ? null
            : interfaceSymbol.ContainingNamespace.ToDisplayString();

        var mockClassName = GetMockClassName(interfaceName);
        var generics = GetGenericInfo(interfaceSymbol);
        var qualifiedInterface = interfaceSymbol.ToDisplayString(TypeFormat);
        var members = CollectMembers(interfaceSymbol);

        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated />");
        sb.AppendLine("#nullable enable");
        sb.AppendLine();
        sb.AppendLine("using System;");
        sb.AppendLine("using System.Threading.Tasks;");
        sb.AppendLine("using Mokk;");
        sb.AppendLine();

        if (ns != null)
        {
            sb.AppendLine($"namespace {ns};");
            sb.AppendLine();
        }

        var vis = Visibility(interfaceSymbol);
        EmitMockClass(sb, mockClassName, qualifiedInterface, members, generics, vis);
        if (emitFactory)
            EmitFactoryExtension(sb, qualifiedInterface, mockClassName, generics, isInterface: true, vis);

        var fileName = ns != null ? $"{ns}.{mockClassName}" : mockClassName;
        if (generics.Arity > 0)
            fileName += $"_{generics.Arity}";
        context.AddSource($"{fileName}.g.cs", sb.ToString());
    }

    private static string GetMockClassName(string typeName)
    {
        const int abstractPrefixLength = 8; // "Abstract".Length
        string baseName;
        if (typeName.StartsWith("Abstract") && typeName.Length > abstractPrefixLength && char.IsUpper(typeName[abstractPrefixLength]))
            baseName = typeName.Substring(abstractPrefixLength);
        else if (typeName.Length > 1 && typeName[0] == 'I' && char.IsUpper(typeName[1]))
            baseName = typeName.Substring(1);
        else
            baseName = typeName;
        return $"Mock{baseName}";
    }

    private readonly struct GenericInfo(string typeParams, string constraints, int arity)
    {
        public string TypeParams { get; } = typeParams;   // "<T, U>" or ""
        public string Constraints { get; } = constraints;  // " where T : class ..." or ""
        public int Arity { get; } = arity;
    }

    private static GenericInfo GetGenericInfo(INamedTypeSymbol symbol)
    {
        var tps = symbol.TypeParameters;
        if (tps.Length == 0)
            return new GenericInfo("", "", 0);

        var typeParams = $"<{string.Join(", ", tps.Select(tp => tp.Name))}>";
        return new GenericInfo(typeParams, FormatConstraints(tps), tps.Length);
    }

    // Builds the " where T : ..." clauses for a set of type parameters. Shared
    // by the mocked type itself and by individual generic methods.
    private static string FormatConstraints(IEnumerable<ITypeParameterSymbol> tps)
    {
        var clauses = new StringBuilder();
        foreach (var tp in tps)
        {
            var parts = new List<string>();

            if (tp.HasReferenceTypeConstraint) parts.Add("class");
            else if (tp.HasUnmanagedTypeConstraint) parts.Add("unmanaged");
            else if (tp.HasValueTypeConstraint) parts.Add("struct");
            else if (tp.HasNotNullConstraint) parts.Add("notnull");

            foreach (var ct in tp.ConstraintTypes)
                parts.Add(ct.ToDisplayString(TypeFormat));

            if (tp.HasConstructorConstraint && !tp.HasValueTypeConstraint && !tp.HasUnmanagedTypeConstraint)
                parts.Add("new()");

            if (parts.Count > 0)
                clauses.Append($" where {tp.Name} : {string.Join(", ", parts)}");
        }

        return clauses.ToString();
    }

    private static MemberCollection CollectMembers(INamedTypeSymbol interfaceSymbol)
    {
        var methods = new List<MethodModel>();
        var properties = new List<PropertyModel>();
        var events = new List<EventModel>();
        var indexers = new List<IndexerModel>();
        var forwards = new List<ExplicitForwardModel>();
        var rawMethods = new List<IMethodSymbol>();
        var seen = new HashSet<string>();

        void Process(INamedTypeSymbol iface)
        {
            foreach (var member in iface.GetMembers())
            {
                switch (member)
                {
                    case IMethodSymbol { MethodKind: MethodKind.Ordinary } method:
                    {
                        rawMethods.Add(method);
                        break;
                    }
                    case IPropertySymbol { IsIndexer: true } indexer:
                    {
                        if (seen.Add(IndexerKey(indexer)))
                        {
                            indexers.Add(IndexerModel.From(indexer));
                        }

                        break;
                    }
                    case IPropertySymbol prop:
                    {
                        if (seen.Add($"p:{prop.Name}"))
                        {
                            properties.Add(PropertyModel.From(prop));
                        }

                        break;
                    }
                    case IEventSymbol ev:
                    {
                        if (seen.Add($"e:{ev.Name}"))
                        {
                            events.Add(EventModel.From(ev));
                        }

                        break;
                    }
                }
            }
        }

        Process(interfaceSymbol);
        foreach (var baseInterface in interfaceSymbol.AllInterfaces)
            Process(baseInterface);

        // Methods that share a name and parameters but differ in return type
        // (the IEnumerable<T> / IEnumerable.GetEnumerator pair) can't all be
        // implemented implicitly: C# binds one implicit method to one slot, so
        // the most-derived return becomes the real method and every other slot
        // gets an explicit interface implementation that just forwards to it.
        foreach (var group in rawMethods.GroupBy(MethodSignatureKey))
        {
            var siblings = group.ToList();
            var primary = siblings.FirstOrDefault(c =>
                siblings.All(o => ReturnAssignable(c.ReturnType, o.ReturnType))) ?? siblings[0];
            methods.Add(MethodModel.From(primary));

            foreach (var sibling in siblings)
                if (!SymbolEqualityComparer.Default.Equals(sibling.ReturnType, primary.ReturnType)
                    && ReturnAssignable(primary.ReturnType, sibling.ReturnType))
                    forwards.Add(ExplicitForwardModel.From(sibling));
        }

        return new MemberCollection(methods, properties, events, indexers, forwards);
    }

    // Ref-kind is part of the signature: M(int) and M(ref int) are distinct
    // interface slots that both need an implementation.
    private static string FormatParamType(IEnumerable<IParameterSymbol> ps)
        => string.Join(",", ps.Select(p =>
            (p.RefKind == RefKind.None ? "" : p.RefKind + " ") + p.Type.ToDisplayString(TypeFormat)));

    private static string MethodSignatureKey(IMethodSymbol m)
        => $"{m.Name}({FormatParamType(m.Parameters)})";

    // True when a value of type `from` can satisfy a `to` return slot, i.e.
    // `from` is `to`, derives from it, or implements it.
    private static bool ReturnAssignable(ITypeSymbol from, ITypeSymbol to)
    {
        if (SymbolEqualityComparer.Default.Equals(from, to))
            return true;
        if (from.AllInterfaces.Contains(to, SymbolEqualityComparer.Default))
            return true;
        for (var b = from.BaseType; b is not null; b = b.BaseType)
            if (SymbolEqualityComparer.Default.Equals(b, to))
                return true;
        return false;
    }

    // Indexers all share the name "this[]", so dedupe across the inheritance
    // chain by parameter types (overloaded indexers are kept separate).
    private static string IndexerKey(IPropertySymbol s)
        => $"i:({FormatParamType(s.Parameters)})";

    private static void EmitVerifyInOrder(StringBuilder sb, string interceptorRef, string indent)
    {
        var i = indent;
        sb.AppendLine($"{i}public void VerifyInOrder(params ICallSpec[] steps)");
        sb.AppendLine($"{i}{{");
        sb.AppendLine($"{i}    var parsed = new (string, System.Type[]?, IMatcher[])[steps.Length];");
        sb.AppendLine($"{i}    for (int si = 0; si < steps.Length; si++)");
        sb.AppendLine($"{i}        parsed[si] = (steps[si].Method, steps[si].TypeArgs, steps[si].Matchers);");
        sb.AppendLine($"{i}    {interceptorRef}.VerifyInOrder(parsed);");
        sb.AppendLine($"{i}}}");
    }

    // Setup-handle parameters: out params are produced by the mock, not matched,
    // so they're dropped from the handle signature and treated as wildcards.
    private static string MatcherParamList(IReadOnlyList<ParameterModel> ps)
        => string.Join(", ", ps.Where(p => !p.IsOut).Select(p => $"Matcher<{p.Type}> {p.Name}"));

    private static string MatcherArrayExpr(IReadOnlyList<ParameterModel> ps)
        => ps.Count == 0
            ? "Array.Empty<IMatcher>()"
            : $"new IMatcher[] {{ {string.Join(", ", ps.Select(p => p.IsOut ? $"Matcher<{p.Type}>.Any.Inner" : $"{p.Name}.Inner"))} }}";

    private static string HandleMatcherParams(MethodModel m) => MatcherParamList(m.Parameters);
    private static string HandleMatchersExpr(MethodModel m) => MatcherArrayExpr(m.Parameters);

    private static string TypeArgsExpr(MethodModel m)
        => m.TypeParameterNames.Count > 0
            ? $"new System.Type[] {{ {string.Join(", ", m.TypeParameterNames.Select(tp => $"typeof({tp})"))} }}"
            : "null";

    private static string TypeParamList(MethodModel m)
        => m.TypeParameterNames.Count > 0 ? $"<{string.Join(", ", m.TypeParameterNames)}>" : "";

    // The mock class already defines these; a member of the same name would
    // collide (CS0102/CS0111), so its setup handle is exposed as `{Name}Handle`.
    private static readonly HashSet<string> ReservedNames = new()
    {
        "Instance", "Reset", "CheckUnusedSetups", "VerifyNoOtherCalls", "VerifyInOrder", "Indexer",
    };

    private static string HandleName(string memberName)
        => ReservedNames.Contains(memberName) ? memberName + "Handle" : memberName;

    // Setup accessor: `Indexer(Matcher<int> i)` → IndexerHandle keyed on the
    // indexer's metadata name. Emitted on the mock for both interfaces and
    // abstract classes (a method, so it never clashes with an overridden
    // `this[...]` the way a property handle would clash with its property).
    private static void EmitIndexerSetupAccessor(StringBuilder sb, string indent, IndexerModel x)
    {
        sb.AppendLine($"{indent}public IndexerHandle<{x.ValueType}> Indexer({MatcherParamList(x.Parameters)})");
        sb.AppendLine($"{indent}    => new(_interceptor, \"{x.MetadataName}\", {MatcherArrayExpr(x.Parameters)});");
        sb.AppendLine();
    }

    // Sugar `mock[matcher]` for single-index *interface* mocks (the mock class
    // has no real indexer, so there's no `mock[5]` ambiguity). Abstract-class
    // mocks ARE the type and keep only the `Indexer(...)` method form.
    private static void EmitIndexerBracket(StringBuilder sb, string indent, IndexerModel x)
    {
        if (x.Parameters.Count != 1)
            return;
        var p = x.Parameters[0];
        sb.AppendLine($"{indent}public IndexerHandle<{p.Type}, {x.ValueType}> this[Matcher<{p.Type}> {p.Name}]");
        sb.AppendLine($"{indent}    => new(_interceptor, \"{x.MetadataName}\", {p.Name}.Inner);");
        sb.AppendLine();
    }

    // The real `this[...]` member that satisfies the interface/base type,
    // forwarding to the interceptor as get_/set_<MetadataName>.
    private static void EmitIndexerImpl(StringBuilder sb, string indent, string prefix, IndexerModel x)
    {
        var sig = string.Join(", ", x.Parameters.Select(p => $"{p.Type} {p.Name}"));
        var indexArgs = string.Join(", ", x.Parameters.Select(p => p.Name));

        // A by-ref indexer can't return a ref to interception storage (same as
        // ref-returning methods): emit a compiling stub that throws if used.
        if (x.RefReturnKind.Length > 0)
        {
            sb.AppendLine($"{indent}{prefix} {x.RefReturnKind}{x.ValueType} this[{sig}]");
            sb.AppendLine($"{indent}    => throw new System.NotSupportedException(\"Mokk cannot mock the ref-returning indexer.\");");
            sb.AppendLine();
            return;
        }

        sb.AppendLine($"{indent}{prefix} {x.ValueType} this[{sig}]");
        sb.AppendLine($"{indent}{{");
        if (x.HasGetter)
            sb.AppendLine($"{indent}    get => _interceptor.Intercept<{x.ValueType}>(\"get_{x.MetadataName}\", null, new object?[] {{ {indexArgs} }});");
        if (x.HasSetter)
            sb.AppendLine($"{indent}    set => _interceptor.InterceptVoid(\"set_{x.MetadataName}\", null, new object?[] {{ {indexArgs}, value }});");
        sb.AppendLine($"{indent}}}");
        sb.AppendLine();
    }

    // Emits a method that forwards to the interceptor. ref/out parameters require a
    // statement body so their values can be copied back from the (possibly mutated)
    // args array after the call.
    private static void EmitInterceptedMethod(StringBuilder sb, string indent, string prefix, MethodModel m)
    {
        var typeParams = TypeParamList(m);
        var typeArgs = TypeArgsExpr(m);
        // An override inherits its constraints (restating them is CS0460);
        // an implicit interface implementation must restate them (CS0425).
        var constraints = prefix.Contains("override") ? "" : m.Constraints;
        var sig = string.Join(", ", m.Parameters.Select(p => $"{p.Modifier}{p.Type} {p.Name}"));
        var argsLiteral = m.Parameters.Count > 0
            ? $"new object?[] {{ {string.Join(", ", m.Parameters.Select(p => p.IsOut ? $"default({p.Type})" : p.Name))} }}"
            : "Array.Empty<object?>()";
        var ret = m.IsVoid ? "void" : m.ReturnType;

        // A by-ref return must hand back a ref to real storage; interception
        // returns by value, so there's nothing to ref. Emit a compiling stub
        // (the rest of the mock stays usable) that throws if actually called.
        if (m.ReturnRefKind.Length > 0)
        {
            sb.AppendLine($"{indent}{prefix} {m.ReturnRefKind}{ret} {m.Name}{typeParams}({sig}){constraints}");
            sb.AppendLine($"{indent}    => throw new System.NotSupportedException(\"Mokk cannot mock ref-returning member '{m.Name}'.\");");
            sb.AppendLine();
            return;
        }

        if (!m.Parameters.Any(p => p.WritesBack))
        {
            sb.AppendLine($"{indent}{prefix} {ret} {m.Name}{typeParams}({sig}){constraints}");
            sb.AppendLine(m.IsVoid
                ? $"{indent}    => _interceptor.InterceptVoid(\"{m.Name}\", {typeArgs}, {argsLiteral});"
                : $"{indent}    => _interceptor.Intercept<{m.ReturnType}>(\"{m.Name}\", {typeArgs}, {argsLiteral});");
            sb.AppendLine();
            return;
        }

        sb.AppendLine($"{indent}{prefix} {ret} {m.Name}{typeParams}({sig}){constraints}");
        sb.AppendLine($"{indent}{{");
        sb.AppendLine($"{indent}    var __args = {argsLiteral};");
        sb.AppendLine(m.IsVoid
            ? $"{indent}    _interceptor.InterceptVoid(\"{m.Name}\", {typeArgs}, __args);"
            : $"{indent}    var __ret = _interceptor.Intercept<{m.ReturnType}>(\"{m.Name}\", {typeArgs}, __args);");
        for (int idx = 0; idx < m.Parameters.Count; idx++)
        {
            var p = m.Parameters[idx];
            if (p.WritesBack)
                sb.AppendLine($"{indent}    {p.Name} = ({p.Type})__args[{idx}]!;");
        }
        if (!m.IsVoid)
            sb.AppendLine($"{indent}    return __ret;");
        sb.AppendLine($"{indent}}}");
        sb.AppendLine();
    }

    private static string Visibility(INamedTypeSymbol s)
        => s.DeclaredAccessibility == Accessibility.Public ? "public" : "internal";

    private static void EmitFactoryExtension(
        StringBuilder sb, string qualifiedType, string mockClassName, GenericInfo generics, bool isInterface, string vis)
    {
        var mockType = $"{mockClassName}{generics.TypeParams}";
        var ctorParams = isInterface
            ? $"bool strict = false, {qualifiedType}? wrapping = null, System.Action<string>? onUnusedSetup = null"
            : "bool strict = false, System.Action<string>? onUnusedSetup = null";
        var ctorArgs = isInterface ? "strict, wrapping, onUnusedSetup" : "strict, onUnusedSetup";

        sb.AppendLine();
        sb.AppendLine("public static partial class MokkFactories");
        sb.AppendLine("{");
        sb.AppendLine($"    extension{generics.TypeParams}({qualifiedType}){generics.Constraints}");
        sb.AppendLine("    {");
        sb.AppendLine($"        {vis} static {mockType} Mock({ctorParams}) => new({ctorArgs});");
        sb.AppendLine("    }");
        sb.AppendLine("}");
    }

    private static void EmitMockClass(
        StringBuilder sb, string className, string qualifiedInterface, MemberCollection members,
        GenericInfo generics, string vis)
    {
        sb.AppendLine($"{vis} sealed class {className}{generics.TypeParams} : global::Mokk.IMockObject{generics.Constraints}");
        sb.AppendLine("{");
        sb.AppendLine($"    private readonly MockInterceptor _interceptor;");
        sb.AppendLine($"    private readonly __Instance _inner;");
        sb.AppendLine($"    public {qualifiedInterface} Instance => _inner;");
        sb.AppendLine($"    global::Mokk.MockInterceptor global::Mokk.IMockObject.Interceptor => _interceptor;");
        sb.AppendLine();
        sb.AppendLine($"    public {className}(bool strict = false, {qualifiedInterface}? wrapping = null, System.Action<string>? onUnusedSetup = null)");
        sb.AppendLine($"    {{");
        sb.AppendLine($"        _interceptor = new(strict, wrapping, typeof({qualifiedInterface}), onUnusedSetup);");
        sb.AppendLine($"        _inner = new(_interceptor);");
        sb.AppendLine($"    }}");
        sb.AppendLine();
        sb.AppendLine($"    public void Reset() {{ _interceptor.Reset(); _inner.ResetState(); }}");
        sb.AppendLine($"    public void CheckUnusedSetups() => _interceptor.CheckUnusedSetups();");
        sb.AppendLine($"    public void VerifyNoOtherCalls() => _interceptor.VerifyNoOtherCalls();");
        sb.AppendLine();

        // Matchers drop ref/out, so ref-differing overloads collapse to one
        // handle signature. The runtime keys by method name anyway, so a single
        // handle drives all of them; emit it once to avoid CS0111.
        var seenHandles = new HashSet<string>();
        foreach (var m in members.Methods)
        {
            var typeParams = TypeParamList(m);
            var matcherParms = HandleMatcherParams(m);
            if (!seenHandles.Add($"{m.Name}{typeParams}({matcherParms})"))
                continue;
            var typeArgsExpr = TypeArgsExpr(m);
            var matchersExpr = HandleMatchersExpr(m);

            if (m.IsVoid)
            {
                sb.AppendLine($"    public VoidMethodHandle {HandleName(m.Name)}{typeParams}({matcherParms}){m.Constraints}");
                sb.AppendLine($"        => new(_interceptor, \"{m.Name}\", {typeArgsExpr}, {matchersExpr});");
            }
            else
            {
                sb.AppendLine($"    public MethodHandle<{m.ReturnType}> {HandleName(m.Name)}{typeParams}({matcherParms}){m.Constraints}");
                sb.AppendLine($"        => new(_interceptor, \"{m.Name}\", {typeArgsExpr}, {matchersExpr});");
            }
            sb.AppendLine();
        }

        foreach (var p in members.Properties)
        {
            sb.AppendLine($"    public PropertyHandle<{p.Type}> {HandleName(p.Name)} => new(_interceptor, \"{p.Name}\");");
            sb.AppendLine();
        }

        foreach (var e in members.Events)
        {
            sb.AppendLine($"    public EventHandle {HandleName(e.Name)} => new(_interceptor, \"{e.Name}\");");
            sb.AppendLine();
        }

        foreach (var x in members.Indexers)
        {
            EmitIndexerSetupAccessor(sb, "    ", x);
            EmitIndexerBracket(sb, "    ", x);
        }

        EmitVerifyInOrder(sb, "_interceptor", indent: "    ");
        sb.AppendLine();
        EmitInstanceClass(sb, qualifiedInterface, members, indent: "    ");
        sb.AppendLine("}");
    }

    private static void EmitInstanceClass(
        StringBuilder sb, string qualifiedInterface, MemberCollection members, string indent)
    {
        var i = indent;
        var ii = indent + "    ";

        sb.AppendLine($"{i}private sealed class __Instance : {qualifiedInterface}");
        sb.AppendLine($"{i}{{");
        sb.AppendLine($"{ii}private readonly MockInterceptor _interceptor;");

        var settableProps = members.Properties.Where(p => p.HasSetter).ToList();
        foreach (var p in settableProps)
        {
            sb.AppendLine($"{ii}private {p.Type}? _backing_{p.Name};");
            sb.AppendLine($"{ii}private bool _backing_{p.Name}_set;");
        }
        sb.AppendLine();
        sb.AppendLine($"{ii}internal __Instance(MockInterceptor interceptor) => _interceptor = interceptor;");
        sb.AppendLine();

        if (settableProps.Count > 0)
        {
            sb.AppendLine($"{ii}internal void ResetState()");
            sb.AppendLine($"{ii}{{");
            foreach (var p in settableProps)
            {
                sb.AppendLine($"{ii}    _backing_{p.Name} = default;");
                sb.AppendLine($"{ii}    _backing_{p.Name}_set = false;");
            }
            sb.AppendLine($"{ii}}}");
        }
        else
        {
            sb.AppendLine($"{ii}internal void ResetState() {{ }}");
        }
        sb.AppendLine();

        foreach (var m in members.Methods)
            EmitInterceptedMethod(sb, ii, "public", m);

        foreach (var p in members.Properties)
        {
            if (p.RefReturnKind.Length > 0)
            {
                sb.AppendLine($"{ii}public {p.RefReturnKind}{p.Type} {p.Name} => throw new System.NotSupportedException(\"Mokk cannot mock ref-returning property '{p.Name}'.\");");
                sb.AppendLine();
                continue;
            }
            sb.AppendLine($"{ii}public {p.Type} {p.Name}");
            sb.AppendLine($"{ii}{{");
            if (p.HasGetter)
            {
                if (p.HasSetter)
                    sb.AppendLine($"{ii}    get => _backing_{p.Name}_set ? ({p.Type})_backing_{p.Name}! : _interceptor.Intercept<{p.Type}>(\"get_{p.Name}\", null, Array.Empty<object?>());");
                else
                    sb.AppendLine($"{ii}    get => _interceptor.Intercept<{p.Type}>(\"get_{p.Name}\", null, Array.Empty<object?>());");
            }
            if (p.HasSetter)
                sb.AppendLine($"{ii}    {p.SetterKeyword} {{ _backing_{p.Name}_set = true; _backing_{p.Name} = value; _interceptor.InterceptVoid(\"set_{p.Name}\", null, new object?[] {{ value }}); }}");
            sb.AppendLine($"{ii}}}");
            sb.AppendLine();
        }

        foreach (var e in members.Events)
        {
            sb.AppendLine($"{ii}public event {e.HandlerType} {e.Name}");
            sb.AppendLine($"{ii}{{");
            sb.AppendLine($"{ii}    add => _interceptor.AddEventHandler(\"{e.Name}\", value);");
            sb.AppendLine($"{ii}    remove => _interceptor.RemoveEventHandler(\"{e.Name}\", value);");
            sb.AppendLine($"{ii}}}");
            sb.AppendLine();
        }

        foreach (var x in members.Indexers)
            EmitIndexerImpl(sb, ii, "public", x);

        // Explicitly implement covariant-return siblings (e.g. the non-generic
        // IEnumerable.GetEnumerator) by forwarding to the implicit method.
        foreach (var f in members.ExplicitForwards)
        {
            var sig = string.Join(", ", f.Parameters.Select(p => $"{p.Modifier}{p.Type} {p.Name}"));
            var args = string.Join(", ", f.Parameters.Select(p => $"{p.Modifier}{p.Name}"));
            sb.AppendLine($"{ii}{f.ReturnType} {f.InterfaceType}.{f.Name}({sig}) => this.{f.Name}({args});");
            sb.AppendLine();
        }

        sb.AppendLine($"{i}}}");
    }

    private static void ExecuteAbstractClass(SourceProductionContext context, INamedTypeSymbol classSymbol, bool emitFactory)
    {
        if (ReferencesUnmockableType(classSymbol))
        {
            context.ReportDiagnostic(Diagnostic.Create(
                RefStructUnsupported, classSymbol.Locations.FirstOrDefault(), classSymbol.Name));
            return;
        }

        var className = classSymbol.Name;
        var ns = classSymbol.ContainingNamespace.IsGlobalNamespace
            ? null
            : classSymbol.ContainingNamespace.ToDisplayString();

        var mockClassName = GetMockClassName(className);
        var generics = GetGenericInfo(classSymbol);
        var qualifiedClass = classSymbol.ToDisplayString(TypeFormat);
        var members = CollectAbstractClassMembers(classSymbol);

        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated />");
        sb.AppendLine("#nullable enable");
        sb.AppendLine();
        sb.AppendLine("using System;");
        sb.AppendLine("using System.Threading.Tasks;");
        sb.AppendLine("using Mokk;");
        sb.AppendLine();

        if (ns != null)
        {
            sb.AppendLine($"namespace {ns};");
            sb.AppendLine();
        }

        var vis = Visibility(classSymbol);
        EmitAbstractClassMock(sb, mockClassName, classSymbol, qualifiedClass, members, generics, vis);
        if (emitFactory)
            EmitFactoryExtension(sb, qualifiedClass, mockClassName, generics, isInterface: false, vis);

        var fileName = ns != null ? $"{ns}.{mockClassName}" : mockClassName;
        if (generics.Arity > 0)
            fileName += $"_{generics.Arity}";
        context.AddSource($"{fileName}.g.cs", sb.ToString());
    }

    private static MemberCollection CollectAbstractClassMembers(INamedTypeSymbol classSymbol)
    {
        var methods = new List<MethodModel>();
        var properties = new List<PropertyModel>();
        var events = new List<EventModel>();
        var indexers = new List<IndexerModel>();
        var seen = new HashSet<string>();

        INamedTypeSymbol? current = classSymbol;
        while (current != null && current.SpecialType != SpecialType.System_Object)
        {
            foreach (var member in current.GetMembers())
            {
                switch (member)
                {
                    case IMethodSymbol { MethodKind: MethodKind.Ordinary } method
                        when (method.IsAbstract || method.IsVirtual)
                          && method.DeclaredAccessibility != Accessibility.Private:
                    {
                        if (seen.Add("m:" + MethodSignatureKey(method)))
                            methods.Add(MethodModel.From(method));
                        break;
                    }
                    case IPropertySymbol { IsIndexer: true } indexer
                        when (indexer.IsAbstract || indexer.IsVirtual)
                          && indexer.DeclaredAccessibility != Accessibility.Private:
                    {
                        if (seen.Add(IndexerKey(indexer)))
                            indexers.Add(IndexerModel.From(indexer));
                        break;
                    }
                    case IPropertySymbol prop
                        when (prop.IsAbstract || prop.IsVirtual)
                          && prop.DeclaredAccessibility != Accessibility.Private:
                    {
                        if (seen.Add($"p:{prop.Name}"))
                            properties.Add(PropertyModel.From(prop));
                        break;
                    }
                    case IEventSymbol ev
                        when (ev.IsAbstract || ev.IsVirtual)
                          && ev.DeclaredAccessibility != Accessibility.Private:
                    {
                        if (seen.Add($"e:{ev.Name}"))
                            events.Add(EventModel.From(ev));
                        break;
                    }
                }
            }
            current = current.BaseType;
        }

        return new MemberCollection(methods, properties, events, indexers);
    }

    private static void EmitAbstractClassMock(
        StringBuilder sb, string className, INamedTypeSymbol classSymbol, string qualifiedClass,
        MemberCollection members, GenericInfo generics, string vis)
    {
        // The mock derives from the abstract class, so its ctor must chain to an
        // accessible base ctor. With no parameterless one, pick the smallest
        // accessible ctor and pass defaults (a mock never uses base state).
        var baseCtor = classSymbol.InstanceConstructors
            .Where(c => c.DeclaredAccessibility is Accessibility.Public
                                                or Accessibility.Protected
                                                or Accessibility.ProtectedOrInternal
                        && !c.Parameters.Any(p => p.RefKind is RefKind.Out or RefKind.Ref))
            .OrderBy(c => c.Parameters.Length)
            .FirstOrDefault();
        var baseArgs = baseCtor is null || baseCtor.Parameters.Length == 0
            ? ""
            : string.Join(", ", baseCtor.Parameters.Select(p => $"default({p.Type.ToDisplayString(TypeFormat)})!"));

        sb.AppendLine($"{vis} sealed class {className}{generics.TypeParams} : {qualifiedClass}, global::Mokk.IMockObject{generics.Constraints}");
        sb.AppendLine("{");
        sb.AppendLine("    private readonly MockInterceptor _interceptor;");
        sb.AppendLine($"    public {qualifiedClass} Instance => this;");
        sb.AppendLine($"    global::Mokk.MockInterceptor global::Mokk.IMockObject.Interceptor => _interceptor;");
        sb.AppendLine();
        sb.AppendLine($"    public {className}(bool strict = false, System.Action<string>? onUnusedSetup = null) : base({baseArgs})");
        sb.AppendLine($"        => _interceptor = new(strict, null, typeof({qualifiedClass}), onUnusedSetup);");
        sb.AppendLine();
        sb.AppendLine("    public void Reset() => _interceptor.Reset();");
        sb.AppendLine("    public void CheckUnusedSetups() => _interceptor.CheckUnusedSetups();");
        sb.AppendLine("    public void VerifyNoOtherCalls() => _interceptor.VerifyNoOtherCalls();");
        sb.AppendLine();

        foreach (var m in members.Methods)
            EmitInterceptedMethod(sb, "    ", $"{(m.IsProtected ? "protected" : "public")} override", m);

        var settableProps = members.Properties.Where(p => p.HasSetter).ToList();
        foreach (var p in settableProps)
        {
            sb.AppendLine($"    private {p.Type}? _backing_{p.Name};");
            sb.AppendLine($"    private bool _backing_{p.Name}_set;");
        }
        if (settableProps.Count > 0)
            sb.AppendLine();

        foreach (var p in members.Properties)
        {
            var access = p.IsProtected ? "protected" : "public";
            if (p.RefReturnKind.Length > 0)
            {
                sb.AppendLine($"    {access} override {p.RefReturnKind}{p.Type} {p.Name} => throw new System.NotSupportedException(\"Mokk cannot mock ref-returning property '{p.Name}'.\");");
                sb.AppendLine();
                continue;
            }
            sb.AppendLine($"    {access} override {p.Type} {p.Name}");
            sb.AppendLine("    {");
            if (p.HasGetter)
            {
                if (p.HasSetter)
                    sb.AppendLine($"        get => _backing_{p.Name}_set ? ({p.Type})_backing_{p.Name}! : _interceptor.Intercept<{p.Type}>(\"get_{p.Name}\", null, Array.Empty<object?>());");
                else
                    sb.AppendLine($"        get => _interceptor.Intercept<{p.Type}>(\"get_{p.Name}\", null, Array.Empty<object?>());");
            }
            if (p.HasSetter)
                sb.AppendLine($"        set {{ _backing_{p.Name}_set = true; _backing_{p.Name} = value; _interceptor.InterceptVoid(\"set_{p.Name}\", null, new object?[] {{ value }}); }}");
            sb.AppendLine("    }");
            sb.AppendLine();
        }

        foreach (var e in members.Events)
        {
            var access = e.IsProtected ? "protected" : "public";
            sb.AppendLine($"    {access} override event {e.HandlerType} {e.Name}");
            sb.AppendLine("    {");
            sb.AppendLine($"        add => _interceptor.AddEventHandler(\"{e.Name}\", value);");
            sb.AppendLine($"        remove => _interceptor.RemoveEventHandler(\"{e.Name}\", value);");
            sb.AppendLine("    }");
            sb.AppendLine();
        }

        foreach (var x in members.Indexers)
            EmitIndexerImpl(sb, "    ", $"{(x.IsProtected ? "protected" : "public")} override", x);

        EmitAbstractClassShortcuts(sb, "_interceptor", members, indent: "    ");
        EmitVerifyInOrder(sb, "_interceptor", indent: "    ");
        sb.AppendLine("}");
    }

    private static void EmitAbstractClassShortcuts(
        StringBuilder sb, string interceptorRef, MemberCollection members, string indent)
    {
        var i = indent;

        var seenHandles = new HashSet<string>();
        foreach (var m in members.Methods)
        {
            var typeParams = TypeParamList(m);
            var matcherParms = HandleMatcherParams(m);
            // A parameterless method's name is already taken by the override, so
            // expose its handle as `{Name}Handle` (same trick as properties).
            var handleName = m.Parameters.Count == 0 ? m.Name + "Handle" : m.Name;
            if (!seenHandles.Add($"{handleName}{typeParams}({matcherParms})"))
                continue;
            var typeArgsExpr = TypeArgsExpr(m);
            var matchersExpr = HandleMatchersExpr(m);

            if (m.IsVoid)
            {
                sb.AppendLine($"{i}public VoidMethodHandle {handleName}{typeParams}({matcherParms}){m.Constraints}");
                sb.AppendLine($"{i}    => new({interceptorRef}, \"{m.Name}\", {typeArgsExpr}, {matchersExpr});");
            }
            else
            {
                sb.AppendLine($"{i}public MethodHandle<{m.ReturnType}> {handleName}{typeParams}({matcherParms}){m.Constraints}");
                sb.AppendLine($"{i}    => new({interceptorRef}, \"{m.Name}\", {typeArgsExpr}, {matchersExpr});");
            }
            sb.AppendLine();
        }

        // Property handles use "{Name}Handle" since the mock IS the class and can't reuse the property name
        foreach (var p in members.Properties)
        {
            sb.AppendLine($"{i}public PropertyHandle<{p.Type}> {p.Name}Handle => new({interceptorRef}, \"{p.Name}\");");
            sb.AppendLine();
        }

        // Event handles use "{Name}Handle" for the same reason: the mock IS the class
        foreach (var e in members.Events)
        {
            sb.AppendLine($"{i}public EventHandle {e.Name}Handle => new({interceptorRef}, \"{e.Name}\");");
            sb.AppendLine();
        }

        // The indexer setup accessor is a method named `Indexer`, so unlike
        // property handles it doesn't collide with the overridden `this[...]`.
        foreach (var x in members.Indexers)
            EmitIndexerSetupAccessor(sb, i, x);
    }

    private class MemberCollection(
        List<MethodModel> methods, List<PropertyModel> properties,
        List<EventModel> events, List<IndexerModel> indexers,
        List<ExplicitForwardModel>? forwards = null)
    {
        public List<MethodModel> Methods { get; } = methods;
        public List<PropertyModel> Properties { get; } = properties;
        public List<EventModel> Events { get; } = events;
        public List<IndexerModel> Indexers { get; } = indexers;
        public List<ExplicitForwardModel> ExplicitForwards { get; } = forwards ?? new();
    }

    private class MethodModel
    {
        public string Name { get; private set; } = "";
        public string ReturnType { get; private set; } = "";
        public bool IsVoid { get; private set; }
        public bool IsProtected { get; private set; }
        public List<ParameterModel> Parameters { get; private set; } = new();
        public List<string> TypeParameterNames { get; private set; } = new();
        public string Constraints { get; private set; } = "";  // " where T : ..." or ""
        public string ReturnRefKind { get; private set; } = "";  // "", "ref ", "ref readonly "

        public static MethodModel From(IMethodSymbol s) => new()
        {
            Name = s.Name,
            IsVoid = s.ReturnsVoid,
            ReturnType = s.ReturnType.ToDisplayString(TypeFormat),
            Parameters = s.Parameters.Select(ParameterModel.From).ToList(),
            TypeParameterNames = s.TypeParameters.Select(tp => tp.Name).ToList(),
            Constraints = FormatConstraints(s.TypeParameters),
            ReturnRefKind = s.ReturnsByRefReadonly ? "ref readonly " : s.ReturnsByRef ? "ref " : "",
            IsProtected = IsProtectedAccess(s),
        };
    }

    private static bool IsProtectedAccess(ISymbol s)
        => s.DeclaredAccessibility is Accessibility.Protected or Accessibility.ProtectedAndInternal;

    private class ParameterModel
    {
        public string Name { get; private set; } = "";
        public string Type { get; private set; } = "";
        public string Modifier { get; private set; } = "";  // "", "ref ", "out ", "in ", "ref readonly "
        public bool IsOut { get; private set; }
        public bool WritesBack { get; private set; }         // out or ref: value must be copied back

        public static ParameterModel From(IParameterSymbol s)
        {
            var (modifier, isOut, writesBack) = s.RefKind switch
            {
                RefKind.Out => ("out ", true, true),
                RefKind.Ref => ("ref ", false, true),
                RefKind.In => ("in ", false, false),
                RefKind.RefReadOnlyParameter => ("ref readonly ", false, false),
                _ => ("", false, false),
            };
            return new()
            {
                Name = s.Name,
                Type = s.Type.ToDisplayString(TypeFormat),
                Modifier = modifier,
                IsOut = isOut,
                WritesBack = writesBack,
            };
        }
    }

    private class PropertyModel
    {
        public string Name { get; private set; } = "";
        public string Type { get; private set; } = "";
        public bool HasGetter { get; private set; }
        public bool HasSetter { get; private set; }
        public bool IsInitOnly { get; private set; }
        public bool IsProtected { get; private set; }
        public string RefReturnKind { get; private set; } = "";  // "", "ref ", "ref readonly "

        public string SetterKeyword => IsInitOnly ? "init" : "set";

        public static PropertyModel From(IPropertySymbol s) => new()
        {
            Name = s.Name,
            Type = s.Type.ToDisplayString(TypeFormat),
            HasGetter = !s.IsWriteOnly,
            HasSetter = !s.IsReadOnly,
            IsInitOnly = s.SetMethod?.IsInitOnly == true,
            RefReturnKind = s.ReturnsByRefReadonly ? "ref readonly " : s.ReturnsByRef ? "ref " : "",
            IsProtected = IsProtectedAccess(s),
        };
    }

    private class EventModel
    {
        public string Name { get; private set; } = "";
        public string HandlerType { get; private set; } = "";
        public bool IsProtected { get; private set; }

        public static EventModel From(IEventSymbol s) => new()
        {
            Name = s.Name,
            HandlerType = s.Type.ToDisplayString(TypeFormat),
            IsProtected = IsProtectedAccess(s),
        };
    }

    // An indexer is an IPropertySymbol with IsIndexer == true. Its accessors are
    // emitted as get_/set_<MetadataName> ("Item" by default), so it behaves like
    // a method pair whose arguments are the index parameters.
    private class IndexerModel
    {
        public string MetadataName { get; private set; } = "Item";
        public string ValueType { get; private set; } = "";
        public bool HasGetter { get; private set; }
        public bool HasSetter { get; private set; }
        public bool IsProtected { get; private set; }
        public string RefReturnKind { get; private set; } = "";  // "", "ref ", "ref readonly "
        public List<ParameterModel> Parameters { get; private set; } = new();

        public static IndexerModel From(IPropertySymbol s) => new()
        {
            MetadataName = s.MetadataName,
            ValueType = s.Type.ToDisplayString(TypeFormat),
            HasGetter = !s.IsWriteOnly,
            HasSetter = !s.IsReadOnly,
            RefReturnKind = s.ReturnsByRefReadonly ? "ref readonly " : s.ReturnsByRef ? "ref " : "",
            IsProtected = IsProtectedAccess(s),
            Parameters = s.Parameters.Select(ParameterModel.From).ToList(),
        };
    }

    // A method shadowed by a covariant-return collision, implemented explicitly
    // and forwarded to the implicit (most-derived) sibling so both interface
    // slots are satisfied with one interception point.
    private class ExplicitForwardModel
    {
        public string InterfaceType { get; private set; } = "";
        public string ReturnType { get; private set; } = "";
        public string Name { get; private set; } = "";
        public List<ParameterModel> Parameters { get; private set; } = new();

        public static ExplicitForwardModel From(IMethodSymbol s) => new()
        {
            InterfaceType = s.ContainingType.ToDisplayString(TypeFormat),
            ReturnType = s.ReturnType.ToDisplayString(TypeFormat),
            Name = s.Name,
            Parameters = s.Parameters.Select(ParameterModel.From).ToList(),
        };
    }
}
