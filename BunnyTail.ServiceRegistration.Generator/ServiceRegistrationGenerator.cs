namespace BunnyTail.ServiceRegistration.Generator;

using System;
using System.Collections.Immutable;
using System.Globalization;
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

        context.RegisterImplementationSourceOutput(
            compilationProvider.Combine(valueProvider).Combine(propertyProvider),
            static (context, provider) => Execute(context, provider.Left.Left, provider.Left.Right, provider.Right));
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
        var syntax = context.TargetNode;
        var symbol = (IMethodSymbol)context.TargetSymbol;

        // Validate method definition
        if (!symbol.IsStatic || !symbol.IsPartialDefinition || !symbol.IsExtensionMethod)
        {
            return Results.Error<MethodModel>(new DiagnosticInfo(Diagnostics.InvalidMethodDefinition, syntax.GetLocation(), symbol.Name));
        }

        // Validate parameter
        if ((symbol.Parameters.Length != 1) ||
            (symbol.Parameters[0].Type.ToDisplayString() != ServiceCollectionName))
        {
            return Results.Error<MethodModel>(new DiagnosticInfo(Diagnostics.InvalidMethodParameter, syntax.GetLocation(), symbol.Name));
        }

        // Validate return type
        if ((symbol.ReturnType is not INamedTypeSymbol returnTypeSymbol) ||
            (returnTypeSymbol.ToDisplayString() != ServiceCollectionName))
        {
            return Results.Error<MethodModel>(new DiagnosticInfo(Diagnostics.InvalidMethodReturnType, syntax.GetLocation(), symbol.Name));
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
            symbol.Parameters[0].Name,
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

            var lifetime = Convert.ToInt32(attributeData.ConstructorArguments[0].Value, CultureInfo.InvariantCulture);
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

            list.Add(new AttributeModel(lifetime, pattern, assembly, ns));
        }

        return list.ToArray();
    }

    // ------------------------------------------------------------
    // Generator
    // ------------------------------------------------------------

    private static void Execute(SourceProductionContext context, Compilation compilation, OptionModel option, ImmutableArray<Result<MethodModel>> methods)
    {
        foreach (var info in methods.SelectError())
        {
            context.ReportDiagnostic(info);
        }

        var ignoreInterfaces = option.IgnoreInterface
            .Split([','], StringSplitOptions.RemoveEmptyEntries)
            .Concat(IgnoreInterfaces)
            .ToArray();

        var builder = new SourceBuilder();
        foreach (var group in methods.SelectValue().GroupBy(static x => new { x.Namespace, x.ClassName }))
        {
            context.CancellationToken.ThrowIfCancellationRequested();

            builder.Clear();
            BuildSource(builder, compilation, ignoreInterfaces, group.ToList());

            var filename = MakeFilename(group.Key.Namespace, group.Key.ClassName);
            var source = builder.ToString();
            context.AddSource(filename, SourceText.From(source, Encoding.UTF8));
        }
    }

    private static void BuildSource(SourceBuilder builder, Compilation compilation, string[] ignoreInterfaces, List<MethodModel> methods)
    {
        var ns = methods[0].Namespace;
        var className = methods[0].ClassName;
        var isValueType = methods[0].IsValueType;

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
        foreach (var method in methods)
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

            foreach (var attribute in method.Attributes.ToArray())
            {
                var regex = new Regex(attribute.Pattern, RegexOptions.Compiled);

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

                    var interfaces = namedTypeSymbol.Interfaces
                        .Where(x => !ignoreInterfaces.Contains(x.ToDisplayString()))
                        .ToArray();
                    if (interfaces.Length == 0)
                    {
                        BuildRegistrationCall(builder, method.ParameterName, attribute.Lifetime, namedTypeSymbol);
                    }
                    else if (interfaces.Length == 1)
                    {
                        BuildRegistrationCall(builder, method.ParameterName, attribute.Lifetime, namedTypeSymbol, interfaces[0]);
                    }
                    else
                    {
                        BuildRegistrationCall(builder, method.ParameterName, attribute.Lifetime, namedTypeSymbol);
                        foreach (var serviceAs in interfaces)
                        {
                            BuildRegistrationCallAsInterface(builder, method.ParameterName, attribute.Lifetime, namedTypeSymbol, serviceAs);
                        }
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

    private static void BuildRegistrationCall(SourceBuilder builder, string parameter, int lifetime, INamedTypeSymbol service, INamedTypeSymbol? serviceAs = null)
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
                .Append(serviceAs.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)).Append(", ");
        }
        builder
            .Append(service.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat))
            .Append(">();")
            .NewLine();
    }

    private static void BuildRegistrationCallAsInterface(SourceBuilder builder, string parameter, int lifetime, INamedTypeSymbol service, INamedTypeSymbol serviceAs)
    {
        builder.
            Indent()
            .Append(parameter)
            .Append(".Add");
        AddScope(builder, lifetime);
        builder
            .Append('<')
            .Append(serviceAs.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat))
            .Append(">(static x => x.GetRequiredService<")
            .Append(service.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat))
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
                return assemblySymbol.GlobalNamespace.GetTypeMembersRecursive(ClassFilter);
            }
        }

        return [];

        static bool ClassFilter(INamedTypeSymbol symbol) => symbol.TypeKind == TypeKind.Class;
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
}
