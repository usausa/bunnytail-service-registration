namespace BunnyTail.ServiceRegistration.Generator;

using System;
using System.Collections.Immutable;
using System.Globalization;
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

    private const string ResolveReferencedAssemblyProperty = "ServiceRegistrationResolveReferencedAssembly";
    private const string IgnoreInterfaceProperty = "build_property.ServiceRegistrationIgnoreInterface";

    private const string ServiceCollectionExtensionsName = "global::Microsoft.Extensions.DependencyInjection.ServiceCollectionServiceExtensions";
    private const string ServiceProviderExtensionsName = "global::Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions";

    private static readonly string[] IgnoreInterfaces =
    [
        "System.IDisposable",
        "System.IAsyncDisposable"
    ];

    private static readonly SymbolDisplayFormat ExpandedTupleFormat =
        SymbolDisplayFormat.FullyQualifiedFormat.AddMiscellaneousOptions(SymbolDisplayMiscellaneousOptions.ExpandValueTuple);

    // ------------------------------------------------------------
    // Initialize
    // ------------------------------------------------------------

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var optionProvider = context.AnalyzerConfigOptionsProvider
            .Select(static (provider, _) => SelectOption(provider));

        context.RegisterSourceOutput(
            optionProvider,
            static (context, option) => ReportOptionDiagnostics(context, option));

        var propertyProvider = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                AttributeName,
                static (syntax, _) => IsTargetSyntax(syntax),
                static (context, _) => GetMethodModel(context))
            .Collect();

        var treeProvider = context.ForAttributeWithMetadataNameSyntaxTrees(
            AttributeName,
            static (syntax, _) => IsTargetSyntax(syntax));

        context.RegisterSourceOutput(
            propertyProvider.Combine(treeProvider),
            static (context, provider) => ReportMethodDiagnostics(context, provider.Left, provider.Right));

        var candidateProvider = propertyProvider
            .Combine(context.CompilationProvider)
            .Select(static (provider, token) => SelectCandidates(provider.Right, provider.Left, token))
            .WithTrackingName("Candidates");

        var requestProvider = propertyProvider
            .Select(static (methods, _) => SelectRequestedAssemblies(methods));
        var compilationKeyProvider = context.CompilationProvider
            .Select(static (compilation, _) => new CompilationKeyModel(compilation.AssemblyName, compilation.Options));
        var referenceProvider = context.MetadataReferencesProvider
            .Collect()
            .Combine(compilationKeyProvider)
            .Combine(optionProvider)
            .Combine(requestProvider)
            .Select(static (provider, token) => SelectReferenceCandidates(provider.Left.Left.Left, provider.Left.Left.Right, provider.Left.Right, provider.Right, token))
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
            resolveDiagnosticProvider.Combine(treeProvider),
            static (context, provider) => ReportResolveDiagnostics(context, provider.Left, provider.Right));

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
        provider.GlobalOptions.TryGetValue<bool>(ResolveReferencedAssemblyProperty, out var resolveReferencedAssembly, out var invalidValue);
        var ignoreInterface = provider.GlobalOptions.TryGetValue(IgnoreInterfaceProperty, out var value) ? value : string.Empty;
        return new OptionModel(resolveReferencedAssembly, invalidValue, ignoreInterface);
    }

    private static bool IsTargetSyntax(SyntaxNode syntax) =>
        syntax is MethodDeclarationSyntax;

    private static Result<MethodModel> GetMethodModel(GeneratorAttributeSyntaxContext context)
    {
        var syntax = (MethodDeclarationSyntax)context.TargetNode;
        var symbol = (IMethodSymbol)context.TargetSymbol;

        // Validate method definition
        if (!symbol.IsStatic || !symbol.IsPartialDefinition || (symbol.PartialImplementationPart is not null) || !symbol.IsExtensionMethod)
        {
            return Results.Error<MethodModel>(new DiagnosticInfo(Diagnostics.InvalidMethodDefinition, syntax.Identifier.GetLocation(), symbol.Name));
        }

        // Validate parameter
        var firstParam = symbol.Parameters.Length == 1 ? symbol.Parameters[0] : default;
        if ((firstParam is null) ||
            !firstParam.Type.HasFullyQualifiedMetadataName(ServiceCollectionName) ||
            (firstParam.Type.NullableAnnotation == NullableAnnotation.Annotated))
        {
            return Results.Error<MethodModel>(new DiagnosticInfo(Diagnostics.InvalidMethodParameter, syntax.Identifier.GetLocation(), symbol.Name));
        }

        // Validate return type
        if (!symbol.ReturnType.HasFullyQualifiedMetadataName(ServiceCollectionName))
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
            symbol.GetImplementationSignature(syntax),
            CSharpIdentifier.Escape(firstParam.Name),
            new EquatableArray<AttributeModel>(GetAttributeModel(context.Attributes))));
    }

    private static AttributeModel[] GetAttributeModel(ImmutableArray<AttributeData> attributes)
    {
        var list = new List<AttributeModel>();

        foreach (var attributeData in attributes)
        {
            if (!attributeData.TryGetConstructorArgument<int>(0, out var lifetime) ||
                !attributeData.TryGetConstructorArgument(1, out var patternArgument))
            {
                continue;
            }

            var pattern = patternArgument.Value as string ?? string.Empty;
            var assembly = attributeData.TryGetNamedArgument<string>("Assembly", out var assemblyName) ? assemblyName : string.Empty;
            var ns = attributeData.TryGetNamedArgument<string>("Namespace", out var namespaceName) ? namespaceName : string.Empty;
            attributeData.TryGetNamedArgument<ITypeSymbol>("As", out var asSymbol);
            var withInterfaces = attributeData.TryGetNamedArgument<bool>("WithInterfaces", out var withInterfacesValue) && withInterfacesValue;

            var locationInfo = attributeData.ApplicationSyntaxReference is { } syntaxRef
                ? LocationInfo.CreateFrom(syntaxRef.GetSyntax())
                : null;
            list.Add(new AttributeModel(
                lifetime,
                IsDefinedEnumValue(attributeData.ConstructorArguments[0]),
                pattern,
                assembly,
                ns,
                asSymbol?.ToDisplayString(ExpandedTupleFormat),
                asSymbol?.ToDisplayString(SymbolDisplayFormats.FullyQualifiedNullable),
                withInterfaces,
                locationInfo));
        }

#pragma warning disable IDE0028
        return list.ToArray();
#pragma warning restore IDE0028
    }

    private static bool IsDefinedEnumValue(TypedConstant constant)
    {
        if ((constant.Type is not INamedTypeSymbol { TypeKind: TypeKind.Enum } type) || (constant.Value is null))
        {
            return true;
        }

        var value = ToBits(constant.Value);
        var flags = type.HasAttribute("System.FlagsAttribute");
        var all = 0UL;
        foreach (var member in type.GetMembers())
        {
            if ((member is IFieldSymbol { HasConstantValue: true } field) && (field.ConstantValue is not null))
            {
                var bits = ToBits(field.ConstantValue);
                if (bits == value)
                {
                    return true;
                }

                all |= bits;
            }
        }

        return flags && ((value & ~all) == 0);

        static ulong ToBits(object value) => value switch
        {
            sbyte x => unchecked((ulong)x),
            byte x => x,
            short x => unchecked((ulong)x),
            ushort x => x,
            int x => unchecked((ulong)x),
            uint x => x,
            long x => unchecked((ulong)x),
            ulong x => x,
            _ => 0
        };
    }

    private static CandidateClassModel CreateCandidateModel(INamedTypeSymbol symbol, bool isAccessible)
    {
        if (!isAccessible)
        {
            return new CandidateClassModel(
                symbol.ContainingNamespace.ToDisplayString(),
                symbol.Name,
                symbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                false,
                new EquatableArray<InterfaceModel>([]),
                new EquatableArray<string>([]));
        }

        var interfaces = symbol.Interfaces
            .Select(static x => new InterfaceModel(x.ToDisplayString(), x.ToDisplayString(SymbolDisplayFormats.FullyQualifiedNullable)))
            .ToArray();

        var serviceTypes = new List<string>();
        for (var type = symbol.BaseType; type is not null; type = type.BaseType)
        {
            serviceTypes.Add(type.ToDisplayString(ExpandedTupleFormat));
        }
        serviceTypes.AddRange(symbol.AllInterfaces.Select(static x => x.ToDisplayString(ExpandedTupleFormat)));

        return new CandidateClassModel(
            symbol.ContainingNamespace.ToDisplayString(),
            symbol.Name,
            symbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
            true,
            new EquatableArray<InterfaceModel>(interfaces),
            new EquatableArray<string>(serviceTypes.ToArray()));
    }

    private static bool ClassFilter(INamedTypeSymbol symbol) =>
        (symbol.TypeKind == TypeKind.Class) &&
        !symbol.IsStatic &&
        !symbol.IsAbstract &&
        !symbol.IsGenericType &&
        !(symbol.IsObsolete(out var isError) && isError);

    private static bool IsAccessible(INamedTypeSymbol symbol, Compilation compilation)
    {
        for (var type = symbol; type is not null; type = type.ContainingType)
        {
            if (type.IsFileLocal)
            {
                return false;
            }
        }

        return compilation.IsSymbolAccessibleWithin(symbol, compilation.Assembly);
    }

    // ------------------------------------------------------------
    // Resolver
    // ------------------------------------------------------------

    private static EquatableArray<CandidateClassModel> SelectCandidates(Compilation compilation, ImmutableArray<Result<MethodModel>> methods, CancellationToken token)
    {
        var regexes = new List<Regex>();
        foreach (var method in methods.SelectValue())
        {
            foreach (var attribute in method.Attributes)
            {
                if (String.IsNullOrEmpty(attribute.Assembly) && (CreateRegex(attribute.Pattern) is { } regex))
                {
                    regexes.Add(regex);
                }
            }
        }

        if (regexes.Count == 0)
        {
            return [with([])];
        }

        var candidates = new List<(INamedTypeSymbol Symbol, bool IsAccessible)>();
        foreach (var symbol in compilation.GetSymbolsWithName(name => regexes.Exists(x => x.IsMatch(name)), SymbolFilter.Type, token))
        {
            if ((symbol is INamedTypeSymbol type) && ClassFilter(type))
            {
                candidates.Add((type, IsAccessible(type, compilation)));
            }
        }

        var treeOrder = new Dictionary<SyntaxTree, int>();
        foreach (var tree in compilation.SyntaxTrees)
        {
            treeOrder[tree] = treeOrder.Count;
        }

        return new(candidates
            .OrderBy(x => x.Symbol.Locations[0].SourceTree is { } tree && treeOrder.TryGetValue(tree, out var order) ? order : Int32.MaxValue)
            .ThenBy(static x => x.Symbol.Locations[0].SourceSpan.Start)
            .Select(static x => CreateCandidateModel(x.Symbol, x.IsAccessible))
            .ToArray());
    }

    private static Regex? CreateRegex(string pattern)
    {
        if (String.IsNullOrEmpty(pattern))
        {
            return null;
        }

        try
        {
            return new Regex(pattern);
        }
        catch (ArgumentException)
        {
            return null;
        }
    }

    private static EquatableArray<string> SelectRequestedAssemblies(ImmutableArray<Result<MethodModel>> methods)
    {
        var names = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var method in methods.SelectValue())
        {
            foreach (var attribute in method.Attributes)
            {
                if (!String.IsNullOrEmpty(attribute.Assembly))
                {
                    names.Add(attribute.Assembly);
                }
            }
        }

        return new(names.ToArray());
    }

    private static EquatableArray<ReferenceAssemblyModel> SelectReferenceCandidates(
        ImmutableArray<MetadataReference> references,
        CompilationKeyModel key,
        OptionModel option,
        EquatableArray<string> assemblyNames,
        CancellationToken token)
    {
        if (!option.ResolveReferencedAssembly || (assemblyNames.Count == 0) || (key.Options is not CSharpCompilationOptions options))
        {
            return [with([])];
        }

        var compilation = CSharpCompilation.Create(key.AssemblyName, references: references, options: options);
        var list = new List<ReferenceAssemblyModel>();
        foreach (var reference in references)
        {
            token.ThrowIfCancellationRequested();

            if ((compilation.GetAssemblyOrModuleSymbol(reference) is IAssemblySymbol assemblySymbol) &&
                assemblyNames.Contains(assemblySymbol.Identity.Name))
            {
                var candidates = assemblySymbol.GlobalNamespace
                    .GetTypeMembersRecursive(ClassFilter)
                    .Where(x => !x.IsFileLocal && compilation.IsSymbolAccessibleWithin(x, compilation.Assembly))
                    .Select(static x => CreateCandidateModel(x, true))
                    .ToArray();
                list.Add(new ReferenceAssemblyModel(assemblySymbol.Identity.Name, new EquatableArray<CandidateClassModel>(candidates)));
            }
        }

        return new(list);
    }

    private static ResolvedRegistrationModel Resolve(
        ImmutableArray<Result<MethodModel>> methods,
        OptionModel option,
        EquatableArray<CandidateClassModel> candidates,
        EquatableArray<ReferenceAssemblyModel> references,
        CancellationToken token)
    {
        // Combine ignore interfaces
        var parts = option.IgnoreInterface.Split([','], StringSplitOptions.RemoveEmptyEntries).Select(static x => x.Trim()).ToArray();
        var ignoreInterfaces = new string[parts.Length + IgnoreInterfaces.Length];
        parts.CopyTo(ignoreInterfaces, 0);
        IgnoreInterfaces.CopyTo(ignoreInterfaces, parts.Length);

        var classes = new List<ClassModel>();
        var locations = new List<LocationInfo?>();
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
                    if (!attribute.IsLifetimeDefined)
                    {
                        diagnostics.Add(new DiagnosticInfo(Diagnostics.UndefinedLifetime, attribute.Location, attribute.Lifetime.ToString(CultureInfo.InvariantCulture)));
                        continue;
                    }

                    // Compile class name pattern
                    if (String.IsNullOrEmpty(attribute.Pattern))
                    {
                        diagnostics.Add(new DiagnosticInfo(Diagnostics.EmptyPattern, attribute.Location));
                        continue;
                    }

                    var regex = CreateRegex(attribute.Pattern);
                    if (regex is null)
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
                    else if (FindReferenceCandidates(references, attribute.Assembly) is { } referenceCandidates)
                    {
                        targets = referenceCandidates;
                    }
                    else
                    {
                        diagnostics.Add(new DiagnosticInfo(Diagnostics.AssemblyNotReferenced, attribute.Location, attribute.Assembly));
                        continue;
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

                        if (!candidate.IsAccessible)
                        {
                            diagnostics.Add(new DiagnosticInfo(Diagnostics.ClassNotAccessible, attribute.Location, candidate.Name));
                            continue;
                        }

                        if ((attribute.AsType is not null) &&
                            (attribute.AsType != candidate.FullyQualifiedName) &&
                            !candidate.ServiceTypes.Contains(attribute.AsType))
                        {
                            diagnostics.Add(new DiagnosticInfo(Diagnostics.AsTypeNotImplemented, attribute.Location, candidate.Name));
                            continue;
                        }

                        // Select interfaces
                        var interfaceNames = candidate.Interfaces
                            .Where(x => !ignoreInterfaces.Contains(x.DisplayName))
                            .Select(static x => x.FullyQualifiedName)
                            .ToArray();
                        registrations.Add(new RegistrationModel(
                            candidate.FullyQualifiedName,
                            new EquatableArray<string>(interfaceNames),
                            attribute.AsTypeName,
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
                    method.Signature,
                    method.ParameterName,
                    new EquatableArray<RegistrationModel>(registrations)));
            }

            // Build class registration model
            classes.Add(new ClassModel(
                group.Key.Namespace,
                group.Key.ClassName,
                groupMethods[0].IsValueType,
                new EquatableArray<MethodRegistrationModel>(methodRegistrations)));
            locations.Add(groupMethods.SelectMany(static x => x.Attributes).Select(static x => x.Location).FirstOrDefault());
        }

        var generated = new List<ClassModel>();
        var firsts = new Dictionary<string, ClassModel>(StringComparer.OrdinalIgnoreCase);
        foreach (var index in Enumerable.Range(0, classes.Count).OrderBy(x => MakeHintName(classes[x]), StringComparer.Ordinal))
        {
            var classModel = classes[index];
            var hintName = MakeHintName(classModel);
            if (firsts.TryGetValue(hintName, out var first))
            {
                if (MakeHintName(first) != hintName)
                {
                    diagnostics.Add(new DiagnosticInfo(Diagnostics.HintNameCollision, locations[index], MakeDisplayName(classModel), MakeDisplayName(first)));
                }

                continue;
            }

            firsts.Add(hintName, classModel);
        }

        foreach (var classModel in classes)
        {
            if (firsts.TryGetValue(MakeHintName(classModel), out var first) && ReferenceEquals(first, classModel))
            {
                generated.Add(classModel);
            }
        }

        return new ResolvedRegistrationModel(
            new EquatableArray<ClassModel>(generated),
            new EquatableArray<DiagnosticInfo>(diagnostics));
    }

    private static string MakeHintName(ClassModel classModel) =>
        HintNameBuilder.Build(classModel.Namespace, classModel.ClassName);

    private static string MakeDisplayName(ClassModel classModel) =>
        String.IsNullOrEmpty(classModel.Namespace) ? classModel.ClassName : $"{classModel.Namespace}.{classModel.ClassName}";

    private static EquatableArray<CandidateClassModel>? FindReferenceCandidates(EquatableArray<ReferenceAssemblyModel> references, string assembly)
    {
        foreach (var reference in references)
        {
            if (String.Equals(reference.AssemblyName, assembly, StringComparison.Ordinal))
            {
                return reference.Classes;
            }
        }

        return null;
    }

    // ------------------------------------------------------------
    // Diagnostics
    // ------------------------------------------------------------

    private static void ReportOptionDiagnostics(SourceProductionContext context, OptionModel option)
    {
        if (option.InvalidResolveReferencedAssembly is not null)
        {
            context.ReportDiagnostic(new DiagnosticInfo(Diagnostics.InvalidPropertyValue, (Location?)null, ResolveReferencedAssemblyProperty, option.InvalidResolveReferencedAssembly));
        }
    }

    private static void ReportMethodDiagnostics(SourceProductionContext context, ImmutableArray<Result<MethodModel>> methods, ImmutableArray<SyntaxTree> trees) =>
        context.ReportDiagnostics(methods.SelectError().Distinct(), trees);

    private static void ReportResolveDiagnostics(SourceProductionContext context, EquatableArray<DiagnosticInfo> diagnostics, ImmutableArray<SyntaxTree> trees) =>
        context.ReportDiagnostics(diagnostics.Distinct(), trees);

    // ------------------------------------------------------------
    // Generator
    // ------------------------------------------------------------

    private static void Execute(SourceProductionContext context, ClassModel classModel)
    {
        context.CancellationToken.ThrowIfCancellationRequested();

        var builder = new SourceBuilder();
        BuildSource(builder, classModel);

        context.AddSource(MakeHintName(classModel), builder);
    }

    private static void BuildSource(SourceBuilder builder, ClassModel classModel)
    {
        var ns = classModel.Namespace;
        var className = classModel.ClassName;
        var isValueType = classModel.IsValueType;

        builder.AutoGenerated();
        builder.EnableNullable();
        builder.Disable("CS0612, CS0618");
        builder.NewLine();

        // namespace
        if (!String.IsNullOrEmpty(ns))
        {
            builder.Namespace(ns);
            builder.NewLine();
        }

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
                .Append(method.Signature)
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
            .Append(ServiceCollectionExtensionsName)
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
            .Append(">(")
            .Append(parameter)
            .Append(");")
            .NewLine();
    }

    private static void BuildRegistrationCallAsInterface(SourceBuilder builder, string parameter, int lifetime, string service, string serviceAs)
    {
        builder.
            Indent()
            .Append(ServiceCollectionExtensionsName)
            .Append(".Add");
        AddScope(builder, lifetime);
        builder
            .Append('<')
            .Append(serviceAs)
            .Append(">(")
            .Append(parameter)
            .Append(", static x => ")
            .Append(ServiceProviderExtensionsName)
            .Append(".GetRequiredService<")
            .Append(service)
            .Append(">(x));")
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
