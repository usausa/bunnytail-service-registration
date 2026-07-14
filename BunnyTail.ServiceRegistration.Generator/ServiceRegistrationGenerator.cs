namespace BunnyTail.ServiceRegistration.Generator;

using System;
using System.Collections.Immutable;
using System.Text;
using System.Text.RegularExpressions;

using BunnyTail.ServiceRegistration.Generator.Models;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;

using SourceGenerateHelper;

[Generator]
public sealed class ServiceRegistrationGenerator : IIncrementalGenerator
{
    private const string AttributeName = "BunnyTail.ServiceRegistration.ServiceRegistrationAttribute";

    private const string ServiceCollectionName = "Microsoft.Extensions.DependencyInjection.IServiceCollection";

    private static readonly string[] IgnoreInterfaces =
    [
        "System.IDisposable",
        "System.IAsyncDisposable"
    ];

    // ------------------------------------------------------------
    // Initialize
    // ------------------------------------------------------------

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var compilationProvider = context.CompilationProvider;

        var valueProvider = context.AnalyzerConfigOptionsProvider
            .Select(static (provider, _) => SelectOption(provider));

        var propertyProvider = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                AttributeName,
                static (syntax, _) => IsTargetSyntax(syntax),
                static (context, _) => GetMethodModel(context))
            .Collect();

        // Method definition diagnostics depend only on the parsed methods, so report them separately.
        context.RegisterSourceOutput(
            propertyProvider,
            static (context, methods) => ReportMethodDiagnostics(context, methods));

        // Symbol resolution stays driven by the CompilationProvider (required for correctness: the
        // generated registrations depend on the whole compilation, and ForAttributeWithMetadataName
        // transforms are not re-run when other files change). The resolution is collapsed into an
        // equatable per-class model, so the actual source emission is skipped while the resolved
        // registrations are unchanged, instead of regenerating every file on any compilation change.
        var resolvedProvider = compilationProvider
            .Combine(valueProvider)
            .Combine(propertyProvider)
            .Select(static (provider, token) => Resolve(provider.Left.Left, provider.Left.Right, provider.Right, token));

        context.RegisterSourceOutput(
            resolvedProvider,
            static (context, resolved) => ReportResolveDiagnostics(context, resolved));

        var groups = resolvedProvider.SelectMany(static (resolved, _) => resolved.Groups.ToImmutableArray());
        context.RegisterImplementationSourceOutput(
            groups,
            static (context, group) => Execute(context, group));
    }

    // ------------------------------------------------------------
    // Parser
    // ------------------------------------------------------------

    private static OptionModel SelectOption(AnalyzerConfigOptionsProvider provider)
    {
        var value = provider.GlobalOptions.TryGetValue("build_property.ServiceRegistrationIgnoreInterface", out var result) ? result : string.Empty;
        return new OptionModel(value);
    }

    private static bool IsTargetSyntax(SyntaxNode syntax) =>
        syntax is MethodDeclarationSyntax;

    private static Result<MethodModel> GetMethodModel(GeneratorAttributeSyntaxContext context)
    {
        var syntax = (MethodDeclarationSyntax)context.TargetNode;
        var symbol = (IMethodSymbol)context.TargetSymbol;

        // Validate method definition
        if (!symbol.IsStatic || !symbol.IsPartialDefinition || !symbol.IsExtensionMethod)
        {
            return Results.Error<MethodModel>(new DiagnosticInfo(Diagnostics.InvalidMethodDefinition, syntax.Identifier.GetLocation(), symbol.Name));
        }

        // Validate parameter
        var firstParam = symbol.Parameters.Length == 1 ? symbol.Parameters[0] : default;
        if ((firstParam is null) || (firstParam.Type.ToDisplayString() != ServiceCollectionName))
        {
            return Results.Error<MethodModel>(new DiagnosticInfo(Diagnostics.InvalidMethodParameter, syntax.Identifier.GetLocation(), symbol.Name));
        }

        // Validate return type
        if ((symbol.ReturnType is not INamedTypeSymbol returnTypeSymbol) ||
            (returnTypeSymbol.ToDisplayString() != ServiceCollectionName))
        {
            return Results.Error<MethodModel>(new DiagnosticInfo(Diagnostics.InvalidMethodReturnType, syntax.Identifier.GetLocation(), symbol.Name));
        }

        var containingType = symbol.ContainingType;
        var ns = String.IsNullOrEmpty(containingType.ContainingNamespace.Name)
            ? string.Empty
            : containingType.ContainingNamespace.ToDisplayString();

        return Results.Success(new MethodModel(
            ns,
            containingType.GetClassName(),
            containingType.IsValueType,
            symbol.DeclaredAccessibility,
            symbol.Name,
            firstParam.Name,
            new EquatableArray<AttributeModel>(GetAttributeModel(symbol))));
    }

    private static AttributeModel[] GetAttributeModel(IMethodSymbol symbol)
    {
        var list = new List<AttributeModel>();

        foreach (var attributeData in symbol.GetAttributes())
        {
            if (attributeData.AttributeClass?.ToDisplayString() != AttributeName)
            {
                continue;
            }

            var lifetime = (int)attributeData.ConstructorArguments[0].Value!;
            var pattern = attributeData.ConstructorArguments[1].Value?.ToString() ?? string.Empty;
            var assembly = string.Empty;
            var ns = string.Empty;

            foreach (var parameter in attributeData.NamedArguments)
            {
                var name = parameter.Key;
                var value = parameter.Value.Value;

                if (String.IsNullOrEmpty(name) || (value is null))
                {
                    continue;
                }

                switch (name)
                {
                    case "Assembly":
                        assembly = value.ToString();
                        break;
                    case "Namespace":
                        ns = value.ToString();
                        break;
                }
            }

            var locationInfo = attributeData.ApplicationSyntaxReference is { } syntaxRef
                ? LocationInfo.CreateFrom(syntaxRef.GetSyntax())
                : null;
            list.Add(new AttributeModel(lifetime, pattern, assembly, ns, locationInfo));
        }

        return list.ToArray();
    }

    // ------------------------------------------------------------
    // Resolver
    // ------------------------------------------------------------

    // Resolves the compilation-dependent registrations into an equatable model. This runs on every
    // compilation change (required for correctness), but its equatable output lets the downstream
    // source emission cache per file.
    private static ResolvedRegistrations Resolve(
        Compilation compilation,
        OptionModel option,
        ImmutableArray<Result<MethodModel>> methods,
        CancellationToken token)
    {
        var parts = option.IgnoreInterface.Split([','], StringSplitOptions.RemoveEmptyEntries);
        var ignoreInterfaces = new string[parts.Length + IgnoreInterfaces.Length];
        parts.CopyTo(ignoreInterfaces, 0);
        IgnoreInterfaces.CopyTo(ignoreInterfaces, parts.Length);

        var groups = ImmutableArray.CreateBuilder<ClassRegistrationModel>();
        var diagnostics = ImmutableArray.CreateBuilder<DiagnosticInfo>();

        foreach (var group in methods.SelectValue().GroupBy(static x => new { x.Namespace, x.ClassName }))
        {
            token.ThrowIfCancellationRequested();

            var groupMethods = group.ToList();
            var methodModels = ImmutableArray.CreateBuilder<MethodRegistrationModel>();

            foreach (var method in groupMethods)
            {
                var registrations = ImmutableArray.CreateBuilder<RegistrationModel>();

                foreach (var attribute in method.Attributes)
                {
                    Regex regex;
                    try
                    {
                        regex = new Regex(attribute.Pattern);
                    }
                    catch (ArgumentException)
                    {
                        diagnostics.Add(new DiagnosticInfo(Diagnostics.InvalidPattern, attribute.Location, attribute.Pattern));
                        continue;
                    }

                    foreach (var namedTypeSymbol in ResolveClasses(compilation, attribute.Assembly))
                    {
                        if (!String.IsNullOrEmpty(attribute.Namespace))
                        {
                            var symbolNamespace = namedTypeSymbol.ContainingNamespace.ToDisplayString();
                            if ((symbolNamespace != attribute.Namespace) && !symbolNamespace.StartsWith(attribute.Namespace + ".", StringComparison.Ordinal))
                            {
                                continue;
                            }
                        }

                        if (!regex.IsMatch(namedTypeSymbol.Name))
                        {
                            continue;
                        }

                        var interfaceNames = namedTypeSymbol.Interfaces
                            .Where(x => !ignoreInterfaces.Contains(x.ToDisplayString()))
                            .Select(static x => x.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat))
                            .ToArray();

                        registrations.Add(new RegistrationModel(
                            namedTypeSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                            new EquatableArray<string>(interfaceNames),
                            attribute.Lifetime));
                    }
                }

                methodModels.Add(new MethodRegistrationModel(
                    method.MethodAccessibility,
                    method.MethodName,
                    method.ParameterName,
                    new EquatableArray<RegistrationModel>(registrations.ToArray())));
            }

            groups.Add(new ClassRegistrationModel(
                group.Key.Namespace,
                group.Key.ClassName,
                groupMethods[0].IsValueType,
                new EquatableArray<MethodRegistrationModel>(methodModels.ToArray())));
        }

        return new ResolvedRegistrations(
            new EquatableArray<ClassRegistrationModel>(groups.ToArray()),
            new EquatableArray<DiagnosticInfo>(diagnostics.ToArray()));
    }

    private static IEnumerable<INamedTypeSymbol> ResolveClasses(Compilation compilation, string assembly)
    {
        if (String.IsNullOrEmpty(assembly))
        {
            return compilation.Assembly.GlobalNamespace.GetTypeMembersRecursive(ClassFilter);
        }

        foreach (var reference in compilation.References)
        {
            if ((compilation.GetAssemblyOrModuleSymbol(reference) is IAssemblySymbol assemblySymbol) &&
                String.Equals(assemblySymbol.Identity.Name, assembly, StringComparison.Ordinal))
            {
                return assemblySymbol.GlobalNamespace.GetTypeMembersRecursive(ClassFilter)
                    .Where(x => compilation.IsSymbolAccessibleWithin(x, compilation.Assembly));
            }
        }

        return [];

        static bool ClassFilter(INamedTypeSymbol symbol) =>
            (symbol.TypeKind == TypeKind.Class) &&
            !symbol.IsStatic &&
            !symbol.IsAbstract &&
            !symbol.IsGenericType &&
            !symbol.IsFileLocal;
    }

    // ------------------------------------------------------------
    // Diagnostics
    // ------------------------------------------------------------

    private static void ReportMethodDiagnostics(SourceProductionContext context, ImmutableArray<Result<MethodModel>> methods)
    {
        foreach (var info in methods.SelectError())
        {
            context.ReportDiagnostic(info);
        }
    }

    private static void ReportResolveDiagnostics(SourceProductionContext context, ResolvedRegistrations resolved)
    {
        foreach (var info in resolved.Diagnostics)
        {
            context.ReportDiagnostic(info);
        }
    }

    // ------------------------------------------------------------
    // Generator
    // ------------------------------------------------------------

    private static void Execute(SourceProductionContext context, ClassRegistrationModel group)
    {
        context.CancellationToken.ThrowIfCancellationRequested();

        var builder = new SourceBuilder();
        BuildSource(builder, group);

        var filename = MakeFilename(group.Namespace, group.ClassName);
        context.AddSource(filename, SourceText.From(builder.ToString(), Encoding.UTF8));
    }

    private static void BuildSource(SourceBuilder builder, ClassRegistrationModel group)
    {
        var ns = group.Namespace;
        var className = group.ClassName;
        var isValueType = group.IsValueType;

        builder.AutoGenerated();
        builder.EnableNullable();
        builder.NewLine();

        // namespace
        if (!String.IsNullOrEmpty(ns))
        {
            builder.Namespace(ns);
            builder.NewLine();
        }

        // using
        builder.Using("Microsoft.Extensions.DependencyInjection");
        builder.NewLine();

        // class
        builder
            .Indent()
            .Append("partial ")
            .Append(isValueType ? "struct " : "class ")
            .Append(className)
            .NewLine();
        builder.BeginScope();

        var first = true;
        foreach (var method in group.Methods)
        {
            if (first)
            {
                first = false;
            }
            else
            {
                builder.NewLine();
            }

            // method
            builder
                .Indent()
                .Append(method.MethodAccessibility.ToText())
                .Append(" static partial global::")
                .Append(ServiceCollectionName)
                .Append(' ')
                .Append(method.MethodName)
                .Append("(this global::")
                .Append(ServiceCollectionName)
                .Append(' ')
                .Append(method.ParameterName)
                .Append(')')
                .NewLine();
            builder.BeginScope();

            foreach (var registration in method.Registrations)
            {
                var interfaces = registration.InterfaceTypeNames.ToArray();
                if (interfaces.Length == 0)
                {
                    BuildRegistrationCall(builder, method.ParameterName, registration.Lifetime, registration.ServiceTypeName);
                }
                else if (interfaces.Length == 1)
                {
                    BuildRegistrationCall(builder, method.ParameterName, registration.Lifetime, registration.ServiceTypeName, interfaces[0]);
                }
                else
                {
                    BuildRegistrationCall(builder, method.ParameterName, registration.Lifetime, registration.ServiceTypeName);
                    foreach (var serviceAs in interfaces)
                    {
                        BuildRegistrationCallAsInterface(builder, method.ParameterName, registration.Lifetime, registration.ServiceTypeName, serviceAs);
                    }
                }
            }

            builder
                .Indent()
                .Append("return ")
                .Append(method.ParameterName)
                .Append(';')
                .NewLine();
            builder.EndScope();
        }

        builder.EndScope();
    }

    private static void BuildRegistrationCall(SourceBuilder builder, string parameter, int lifetime, string service, string? serviceAs = null)
    {
        builder
            .Indent()
            .Append(parameter)
            .Append(".Add");
        AddScope(builder, lifetime);
        builder.Append('<');
        if (serviceAs is not null)
        {
            builder
                .Append(serviceAs).Append(", ");
        }
        builder
            .Append(service)
            .Append(">();")
            .NewLine();
    }

    private static void BuildRegistrationCallAsInterface(SourceBuilder builder, string parameter, int lifetime, string service, string serviceAs)
    {
        builder.
            Indent()
            .Append(parameter)
            .Append(".Add");
        AddScope(builder, lifetime);
        builder
            .Append('<')
            .Append(serviceAs)
            .Append(">(static x => x.GetRequiredService<")
            .Append(service)
            .Append(">());")
            .NewLine();
    }

    private static void AddScope(SourceBuilder builder, int lifetime)
    {
        builder.Append(lifetime switch
        {
            1 => "Singleton",
            2 => "Scoped",
            _ => "Transient"
        });
    }

    // ------------------------------------------------------------
    // Helper
    // ------------------------------------------------------------

    private static string MakeFilename(string ns, string className)
    {
        var buffer = new StringBuilder();

        if (!String.IsNullOrEmpty(ns))
        {
            buffer.Append(ns.Replace('.', '_'));
            buffer.Append('_');
        }

        buffer.Append(className.Replace('<', '[').Replace('>', ']'));
        buffer.Append(".g.cs");

        return buffer.ToString();
    }

    // ------------------------------------------------------------
    // Model
    // ------------------------------------------------------------

    private sealed record ResolvedRegistrations(
        EquatableArray<ClassRegistrationModel> Groups,
        EquatableArray<DiagnosticInfo> Diagnostics);

    private sealed record ClassRegistrationModel(
        string Namespace,
        string ClassName,
        bool IsValueType,
        EquatableArray<MethodRegistrationModel> Methods);

    private sealed record MethodRegistrationModel(
        Accessibility MethodAccessibility,
        string MethodName,
        string ParameterName,
        EquatableArray<RegistrationModel> Registrations);

    private sealed record RegistrationModel(
        string ServiceTypeName,
        EquatableArray<string> InterfaceTypeNames,
        int Lifetime);
}
