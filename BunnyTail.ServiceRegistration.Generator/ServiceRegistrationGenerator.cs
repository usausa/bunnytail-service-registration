namespace BunnyTail.ServiceRegistration.Generator;

using System;
using System.Collections.Immutable;
using System.Text.RegularExpressions;

using BunnyTail.ServiceRegistration.Generator.Models;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

using SourceGenerateHelper;

[Generator]
public sealed class ServiceRegistrationGenerator : IIncrementalGenerator
{
    private const string AttributeName = "BunnyTail.ServiceRegistration.ServiceRegistrationAttribute";

    private const string ServiceCollectionName = "Microsoft.Extensions.DependencyInjection.IServiceCollection";

    private const string ResolveReferencedAssemblyProperty = "build_property.ServiceRegistrationResolveReferencedAssembly";
    private const string IgnoreInterfaceProperty = "build_property.ServiceRegistrationIgnoreInterface";

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
        var optionProvider = context.AnalyzerConfigOptionsProvider
            .Select(static (provider, _) => SelectOption(provider));

        var propertyProvider = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                AttributeName,
                static (syntax, _) => IsTargetSyntax(syntax),
                static (context, _) => GetMethodModel(context))
            .Collect();

        context.RegisterSourceOutput(
            propertyProvider,
            static (context, methods) => ReportMethodDiagnostics(context, methods));

        var candidateProvider = context.SyntaxProvider
            .CreateSyntaxProvider(
                static (syntax, _) => IsCandidateSyntax(syntax),
                static (context, token) => GetCandidateModel(context, token))
            .Where(static x => x is not null)
            .Select(static (x, _) => x!)
            .Collect()
            .WithTrackingName("Candidates");

        var referenceProvider = context.CompilationProvider
            .Combine(optionProvider)
            .Combine(propertyProvider)
            .Select(static (provider, token) => SelectReferenceCandidates(provider.Left.Left, provider.Left.Right, provider.Right, token))
            .WithTrackingName("References");

        var resolvedProvider = propertyProvider
            .Combine(optionProvider)
            .Combine(candidateProvider)
            .Combine(referenceProvider)
            .Select(static (provider, token) => Resolve(provider.Left.Left.Left, provider.Left.Left.Right, provider.Left.Right, provider.Right, token));

        var resolveDiagnosticProvider = resolvedProvider
            .Select(static (resolved, _) => resolved.Diagnostics)
            .WithTrackingName("Diagnostics");
        context.RegisterSourceOutput(
            resolveDiagnosticProvider,
            static (context, diagnostics) => ReportResolveDiagnostics(context, diagnostics));

        var classProvider = resolvedProvider
            .SelectMany(static (resolved, _) => resolved.Classes.ToImmutableArray())
            .WithTrackingName("Classes");
        context.RegisterImplementationSourceOutput(
            classProvider,
            static (context, classModel) => Execute(context, classModel));
    }

    // ------------------------------------------------------------
    // Parser
    // ------------------------------------------------------------

    private static OptionModel SelectOption(AnalyzerConfigOptionsProvider provider)
    {
        var resolveReferencedAssembly = provider.GlobalOptions.TryGetValue(ResolveReferencedAssemblyProperty, out var value) &&
            Boolean.TryParse(value, out var result) &&
            result;
        var ignoreInterface = provider.GlobalOptions.TryGetValue(IgnoreInterfaceProperty, out value) ? value : string.Empty;
        return new OptionModel(resolveReferencedAssembly, ignoreInterface);
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
            var asType = default(string?);
            var withInterfaces = false;

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
                    case "As":
                        asType = (value as ITypeSymbol)?.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                        break;
                    case "WithInterfaces":
                        withInterfaces = value is true;
                        break;
                }
            }

            var locationInfo = attributeData.ApplicationSyntaxReference is { } syntaxRef
                ? LocationInfo.CreateFrom(syntaxRef.GetSyntax())
                : null;
            list.Add(new AttributeModel(lifetime, pattern, assembly, ns, asType, withInterfaces, locationInfo));
        }

#pragma warning disable IDE0028
        return list.ToArray();
#pragma warning restore IDE0028
    }

    private static bool IsCandidateSyntax(SyntaxNode syntax) =>
        (syntax is ClassDeclarationSyntax classSyntax) &&
        (classSyntax.TypeParameterList is null) &&
        !classSyntax.Modifiers.Any(SyntaxKind.StaticKeyword) &&
        !classSyntax.Modifiers.Any(SyntaxKind.AbstractKeyword);

    private static CandidateClassModel? GetCandidateModel(GeneratorSyntaxContext context, CancellationToken token)
    {
        var syntax = (ClassDeclarationSyntax)context.Node;
        if ((context.SemanticModel.GetDeclaredSymbol(syntax, token) is not { } symbol) || !ClassFilter(symbol))
        {
            return null;
        }

        var references = symbol.DeclaringSyntaxReferences;
        if ((references.Length > 1) &&
            ((references[0].SyntaxTree != syntax.SyntaxTree) || (references[0].Span != syntax.Span)))
        {
            return null;
        }

        return CreateCandidateModel(symbol);
    }

    private static CandidateClassModel CreateCandidateModel(INamedTypeSymbol symbol)
    {
        var interfaces = symbol.Interfaces
            .Select(static x => new InterfaceModel(x.ToDisplayString(), x.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)))
            .ToArray();
        return new CandidateClassModel(
            symbol.ContainingNamespace.ToDisplayString(),
            symbol.Name,
            symbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
            new EquatableArray<InterfaceModel>(interfaces));
    }

    private static bool ClassFilter(INamedTypeSymbol symbol) =>
        (symbol.TypeKind == TypeKind.Class) &&
        !symbol.IsStatic &&
        !symbol.IsAbstract &&
        !symbol.IsGenericType &&
        !symbol.IsFileLocal;

    // ------------------------------------------------------------
    // Resolver
    // ------------------------------------------------------------

    private static EquatableArray<ReferenceAssemblyModel> SelectReferenceCandidates(
        Compilation compilation,
        OptionModel option,
        ImmutableArray<Result<MethodModel>> methods,
        CancellationToken token)
    {
        if (!option.ResolveReferencedAssembly)
        {
            return [with([])];
        }

        // Collect assembly names specified by attributes
        var assemblyNames = new List<string>();
        foreach (var method in methods.SelectValue())
        {
            foreach (var attribute in method.Attributes)
            {
                if (!String.IsNullOrEmpty(attribute.Assembly) && !assemblyNames.Contains(attribute.Assembly))
                {
                    assemblyNames.Add(attribute.Assembly);
                }
            }
        }

        if (assemblyNames.Count == 0)
        {
            return [with([])];
        }

        var list = new List<ReferenceAssemblyModel>();
        foreach (var reference in compilation.References)
        {
            token.ThrowIfCancellationRequested();

            if ((compilation.GetAssemblyOrModuleSymbol(reference) is IAssemblySymbol assemblySymbol) &&
                assemblyNames.Contains(assemblySymbol.Identity.Name))
            {
                var candidates = assemblySymbol.GlobalNamespace
                    .GetTypeMembersRecursive(ClassFilter)
                    .Where(x => compilation.IsSymbolAccessibleWithin(x, compilation.Assembly))
                    .Select(CreateCandidateModel)
                    .ToArray();
                list.Add(new ReferenceAssemblyModel(assemblySymbol.Identity.Name, new EquatableArray<CandidateClassModel>(candidates)));
            }
        }

        return new(list);
    }

    private static ResolvedRegistrationModel Resolve(
        ImmutableArray<Result<MethodModel>> methods,
        OptionModel option,
        ImmutableArray<CandidateClassModel> candidates,
        EquatableArray<ReferenceAssemblyModel> references,
        CancellationToken token)
    {
        // Combine ignore interfaces
        var parts = option.IgnoreInterface.Split([','], StringSplitOptions.RemoveEmptyEntries);
        var ignoreInterfaces = new string[parts.Length + IgnoreInterfaces.Length];
        parts.CopyTo(ignoreInterfaces, 0);
        IgnoreInterfaces.CopyTo(ignoreInterfaces, parts.Length);

        var classes = ImmutableArray.CreateBuilder<ClassModel>();
        var diagnostics = ImmutableArray.CreateBuilder<DiagnosticInfo>();

        // Group by class
        foreach (var group in methods.SelectValue().GroupBy(static x => new { x.Namespace, x.ClassName }))
        {
            token.ThrowIfCancellationRequested();

            var groupMethods = group.ToList();
            var methodRegistrations = ImmutableArray.CreateBuilder<MethodRegistrationModel>();

            foreach (var method in groupMethods)
            {
                var registrations = ImmutableArray.CreateBuilder<RegistrationModel>();
                foreach (var attribute in method.Attributes)
                {
                    // Compile class name pattern
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

                    if ((attribute.AsType is not null) && attribute.WithInterfaces)
                    {
                        diagnostics.Add(new DiagnosticInfo(Diagnostics.ConflictingInterfaceRegistration, attribute.Location, attribute.Pattern));
                    }

                    // Select candidate source
                    IEnumerable<CandidateClassModel> targets;
                    if (String.IsNullOrEmpty(attribute.Assembly))
                    {
                        targets = candidates;
                    }
                    else if (!option.ResolveReferencedAssembly)
                    {
                        diagnostics.Add(new DiagnosticInfo(Diagnostics.ReferencedAssemblyDisabled, attribute.Location, attribute.Assembly));
                        continue;
                    }
                    else
                    {
                        targets = FindReferenceCandidates(references, attribute.Assembly);
                    }

                    var patternMatched = false;
                    foreach (var candidate in targets)
                    {
                        // Filter by namespace
                        if (!String.IsNullOrEmpty(attribute.Namespace))
                        {
                            var candidateNamespace = candidate.Namespace;
                            if ((candidateNamespace != attribute.Namespace) && !candidateNamespace.StartsWith(attribute.Namespace + ".", StringComparison.Ordinal))
                            {
                                continue;
                            }
                        }

                        // Filter by class name
                        if (!regex.IsMatch(candidate.Name))
                        {
                            continue;
                        }

                        patternMatched = true;

                        // Select interfaces
                        var interfaceNames = candidate.Interfaces
                            .Where(x => !ignoreInterfaces.Contains(x.DisplayName))
                            .Select(static x => x.FullyQualifiedName)
                            .ToArray();
                        registrations.Add(new RegistrationModel(
                            candidate.FullyQualifiedName,
                            new EquatableArray<string>(interfaceNames),
                            attribute.AsType,
                            attribute.WithInterfaces,
                            attribute.Lifetime));
                    }

                    if (!patternMatched)
                    {
                        diagnostics.Add(new DiagnosticInfo(Diagnostics.PatternNoMatch, attribute.Location, attribute.Pattern));
                    }
                }

                // Build method registration model
                methodRegistrations.Add(new MethodRegistrationModel(
                    method.MethodAccessibility,
                    method.MethodName,
                    method.ParameterName,
                    new EquatableArray<RegistrationModel>(registrations)));
            }

            // Build class registration model
            classes.Add(new ClassModel(
                group.Key.Namespace,
                group.Key.ClassName,
                groupMethods[0].IsValueType,
                new EquatableArray<MethodRegistrationModel>(methodRegistrations)));
        }

        return new ResolvedRegistrationModel(
            new EquatableArray<ClassModel>(classes),
            new EquatableArray<DiagnosticInfo>(diagnostics));
    }

    private static EquatableArray<CandidateClassModel> FindReferenceCandidates(EquatableArray<ReferenceAssemblyModel> references, string assembly)
    {
        foreach (var reference in references)
        {
            if (String.Equals(reference.AssemblyName, assembly, StringComparison.Ordinal))
            {
                return reference.Classes;
            }
        }

        return [with([])];
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

    private static void ReportResolveDiagnostics(SourceProductionContext context, EquatableArray<DiagnosticInfo> diagnostics)
    {
        foreach (var info in diagnostics)
        {
            context.ReportDiagnostic(info);
        }
    }

    // ------------------------------------------------------------
    // Generator
    // ------------------------------------------------------------

    private static void Execute(SourceProductionContext context, ClassModel classModel)
    {
        context.CancellationToken.ThrowIfCancellationRequested();

        var builder = new SourceBuilder();
        BuildSource(builder, classModel);

        context.AddSource(HintNameBuilder.Build(classModel.Namespace, classModel.ClassName), builder);
    }

    private static void BuildSource(SourceBuilder builder, ClassModel classModel)
    {
        var ns = classModel.Namespace;
        var className = classModel.ClassName;
        var isValueType = classModel.IsValueType;

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
        foreach (var method in classModel.Methods)
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
                if (registration.AsType is not null)
                {
                    BuildRegistrationCall(builder, method.ParameterName, registration.Lifetime, registration.ServiceTypeName, registration.AsType);
                    continue;
                }

                BuildRegistrationCall(builder, method.ParameterName, registration.Lifetime, registration.ServiceTypeName);
                if (!registration.WithInterfaces)
                {
                    continue;
                }

                foreach (var serviceAs in registration.InterfaceTypeNames)
                {
                    BuildRegistrationCallAsInterface(builder, method.ParameterName, registration.Lifetime, registration.ServiceTypeName, serviceAs);
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
}
