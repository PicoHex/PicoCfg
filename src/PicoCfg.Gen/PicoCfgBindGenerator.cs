namespace PicoCfg.Gen.Generator;

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

[Generator(LanguageNames.CSharp)]
public sealed class PicoCfgBindGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var calls = context.SyntaxProvider
            .CreateSyntaxProvider(static (node, _) => IsCandidateInvocation(node), static (ctx, _) => TransformInvocation(ctx))
            .Where(static call => call is not null)
            .Select(static (call, _) => call!);

        context.RegisterSourceOutput(calls.Collect(), static (spc, collectedCalls) => Execute(spc, collectedCalls));
    }

    private static bool IsCandidateInvocation(SyntaxNode node)
    {
        if (node is not InvocationExpressionSyntax invocation)
            return false;

        return invocation.Expression switch
        {
            MemberAccessExpressionSyntax { Name: SimpleNameSyntax simpleName } => IsTargetMethodName(simpleName.Identifier.ValueText),
            SimpleNameSyntax simpleName => IsTargetMethodName(simpleName.Identifier.ValueText),
            _ => false,
        };
    }

    private static BindCall? TransformInvocation(GeneratorSyntaxContext context)
    {
        if (context.Node is not InvocationExpressionSyntax invocation)
            return null;

        if (context.SemanticModel.Compilation.AssemblyName == "PicoCfg.Gen")
            return null;

        if (context.SemanticModel.GetSymbolInfo(invocation).Symbol is not IMethodSymbol method)
            return null;

        if (!TryGetOperation(method, out var operation))
            return null;

        if (method.TypeArguments.Length != 1)
            return null;

        return new BindCall(method.TypeArguments[0], operation, invocation.GetLocation());
    }

    private static void Execute(SourceProductionContext context, ImmutableArray<BindCall> calls)
    {
        if (calls.IsDefaultOrEmpty)
            return;

        var targets = new Dictionary<ITypeSymbol, TargetRegistration>(SymbolEqualityComparer.Default);

        foreach (var call in calls)
        {
            if (!targets.TryGetValue(call.TargetType, out var registration))
            {
                registration = new TargetRegistration(call.TargetType);
                targets.Add(call.TargetType, registration);
            }

            registration.Operations |= call.Operation;
            registration.Locations.Add(call.Location);
        }

        var validTargets = new List<TargetModel>(targets.Count);
        foreach (var registration in targets.Values)
        {
            if (!TryAnalyzeTarget(context, registration, out var model))
                continue;

            validTargets.Add(model);
        }

        if (validTargets.Count == 0)
            return;

        context.AddSource("PicoCfgBindRegistrations.g.cs", SourceText.From(Render(validTargets), Encoding.UTF8));
    }

    private static bool TryAnalyzeTarget(SourceProductionContext context, TargetRegistration registration, out TargetModel model)
    {
        model = null!;

        if (registration.TargetType is not INamedTypeSymbol namedType)
        {
            ReportAll(context, registration.Locations, Diagnostics.TargetMustBeClosedNamedType, registration.TargetType.ToDisplayString());
            return false;
        }

        if (ContainsTypeParameter(namedType))
        {
            ReportAll(context, registration.Locations, Diagnostics.TargetMustBeClosedNamedType, namedType.ToDisplayString());
            return false;
        }

        if (namedType.TypeKind != TypeKind.Class || namedType.IsAbstract)
        {
            ReportAll(context, registration.Locations, Diagnostics.TargetMustBeReferenceType, namedType.ToDisplayString());
            return false;
        }

        var properties = new List<PropertyModel>();
        foreach (var member in namedType.GetMembers())
        {
            if (member is not IPropertySymbol property)
                continue;

            if (property.IsStatic || property.DeclaredAccessibility != Accessibility.Public)
                continue;

            if (property.IsIndexer || property.SetMethod is null || property.SetMethod.DeclaredAccessibility != Accessibility.Public)
            {
                ReportAll(
                    context,
                    registration.Locations,
                    Diagnostics.UnsupportedProperty,
                    namedType.ToDisplayString(),
                    property.Name,
                    "public writable scalar properties"
                );
                return false;
            }

            if (!TryCreatePropertyModel(context, registration, namedType, property, out var propertyModel))
                return false;

            properties.Add(propertyModel);
        }

        var requiresCtor = (registration.Operations & (BindOperation.Bind | BindOperation.TryBind)) != 0;
        var hasPublicParameterlessCtor = HasPublicParameterlessConstructor(namedType);
        if (requiresCtor && !hasPublicParameterlessCtor)
        {
            ReportAll(context, registration.Locations, Diagnostics.MissingPublicParameterlessConstructor, namedType.ToDisplayString());
            return false;
        }

        model = new TargetModel(namedType, registration.Operations, properties.ToImmutableArray(), hasPublicParameterlessCtor);
        return true;
    }

    private static bool TryCreatePropertyModel(
        SourceProductionContext context,
        TargetRegistration registration,
        INamedTypeSymbol namedType,
        IPropertySymbol property,
        out PropertyModel propertyModel
    )
    {
        propertyModel = null!;
        if (TryGetScalarKind(property.Type, out var scalarKind, out var underlyingType))
        {
            propertyModel = new PropertyModel(property.Name, property.Type, scalarKind, underlyingType, IsNullable(property.Type));
            return true;
        }

        if (IsCollectionType(property.Type))
        {
            ReportAll(context, registration.Locations, Diagnostics.UnsupportedCollectionProperty, namedType.ToDisplayString(), property.Name, property.Type.ToDisplayString());
            return false;
        }

        if (property.Type is INamedTypeSymbol { TypeKind: TypeKind.Class or TypeKind.Struct or TypeKind.Interface } complexType)
        {
            ReportAll(context, registration.Locations, Diagnostics.UnsupportedComplexProperty, namedType.ToDisplayString(), property.Name, complexType.ToDisplayString());
            return false;
        }

        ReportAll(context, registration.Locations, Diagnostics.UnsupportedPropertyType, namedType.ToDisplayString(), property.Name, property.Type.ToDisplayString());
        return false;
    }

    private static string Render(IReadOnlyList<TargetModel> targets)
    {
        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated />");
        sb.AppendLine("#nullable enable");
        sb.AppendLine("namespace PicoCfg;");
        sb.AppendLine();
        sb.AppendLine("[global::System.Runtime.CompilerServices.CompilerGeneratedAttribute]");
        sb.AppendLine("internal static class PicoCfgBindGeneratedRegistrations");
        sb.AppendLine("{");
        sb.AppendLine("    [global::System.Runtime.CompilerServices.ModuleInitializerAttribute]");
        sb.AppendLine("    internal static void Initialize()");
        sb.AppendLine("    {");

        for (var i = 0; i < targets.Count; i++)
        {
            var target = targets[i];
            var typeName = target.TargetType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            var bindDelegateType = "global::System.Func<global::PicoCfg.Abs.ICfgSnapshot, string?, " + typeName + ">";
            var tryBindDelegateType = "global::PicoCfg.PicoCfgGeneratedTryBindDelegate<" + typeName + ">";
            var bindIntoDelegateType = "global::PicoCfg.PicoCfgGeneratedBindIntoDelegate<" + typeName + ">";
            var hasCtor = target.HasPublicParameterlessConstructor ? "(" + bindDelegateType + ")Bind_" + i : "null";
            var hasTryBind = target.HasPublicParameterlessConstructor ? "(" + tryBindDelegateType + ")TryBind_" + i : "null";

            sb.Append("        global::PicoCfg.PicoCfgBindRuntime.Register<").Append(typeName).AppendLine(">(");
            sb.AppendLine("            contractVersion: global::PicoCfg.PicoCfgBindRuntime.ContractVersion,");
            sb.Append("            bind: ").Append(hasCtor).AppendLine(",");
            sb.Append("            tryBind: ").Append(hasTryBind).AppendLine(",");
            sb.Append("            bindInto: (").Append(bindIntoDelegateType).Append(")BindInto_").Append(i).AppendLine(");");
            sb.AppendLine();
        }

        sb.AppendLine("    }");
        sb.AppendLine();

        for (var i = 0; i < targets.Count; i++)
            AppendTargetMethods(sb, targets[i], i);

        sb.AppendLine("}");
        return sb.ToString();
    }

    private static void AppendTargetMethods(StringBuilder sb, TargetModel target, int index)
    {
        var typeName = target.TargetType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

        if (target.HasPublicParameterlessConstructor)
        {
            sb.Append("    private static ").Append(typeName).Append(" Bind_").Append(index).AppendLine("(global::PicoCfg.Abs.ICfgSnapshot snapshot, string? section)");
            sb.AppendLine("    {");
            sb.Append("        var instance = new ").Append(typeName).AppendLine("();");
            sb.Append("        BindInto_").Append(index).AppendLine("(snapshot, section, instance);");
            sb.AppendLine("        return instance;");
            sb.AppendLine("    }");
            sb.AppendLine();

            sb.Append("    private static bool TryBind_").Append(index).Append("(global::PicoCfg.Abs.ICfgSnapshot snapshot, string? section, out ").Append(typeName).AppendLine(" value)");
            sb.AppendLine("    {");
            sb.Append("        var instance = new ").Append(typeName).AppendLine("();");
            sb.Append("        if (!TryBindInto_").Append(index).AppendLine("(snapshot, section, instance))");
            sb.AppendLine("        {");
            sb.AppendLine("            value = default!;");
            sb.AppendLine("            return false;");
            sb.AppendLine("        }");
            sb.AppendLine();
            sb.AppendLine("        value = instance;");
            sb.AppendLine("        return true;");
            sb.AppendLine("    }");
            sb.AppendLine();
        }

        sb.Append("    private static void BindInto_").Append(index).Append("(global::PicoCfg.Abs.ICfgSnapshot snapshot, string? section, ").Append(typeName).AppendLine(" instance)");
        sb.AppendLine("    {");
        foreach (var property in target.Properties)
            AppendBindProperty(sb, target, property, throwOnFailure: true);
        sb.AppendLine("    }");
        sb.AppendLine();

        sb.Append("    private static bool TryBindInto_").Append(index).Append("(global::PicoCfg.Abs.ICfgSnapshot snapshot, string? section, ").Append(typeName).AppendLine(" instance)");
        sb.AppendLine("    {");
        sb.AppendLine("        var any = false;");
        foreach (var property in target.Properties)
            AppendBindProperty(sb, target, property, throwOnFailure: false);
        sb.AppendLine("        return any;");
        sb.AppendLine("    }");
        sb.AppendLine();
    }

    private static void AppendBindProperty(StringBuilder sb, TargetModel target, PropertyModel property, bool throwOnFailure)
    {
        var propertyPathLiteral = SymbolDisplay.FormatLiteral(property.Name, true);
        var targetDisplay = SymbolDisplay.FormatLiteral(property.Type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat), true);
        var memberDisplay = SymbolDisplay.FormatLiteral(target.TargetType.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat) + "." + property.Name, true);
        var rawName = "__raw_" + property.Name;
        var pathName = "__path_" + property.Name;
        var valueName = "__value_" + property.Name;

        sb.Append("        var ").Append(pathName).Append(" = global::PicoCfg.PicoCfgBindRuntime.CombinePath(section, ").Append(propertyPathLiteral).AppendLine(");");
        sb.Append("        if (snapshot.TryGetValue(").Append(pathName).Append(", out var ").Append(rawName).AppendLine("))");
        sb.AppendLine("        {");
        if (!throwOnFailure)
            sb.AppendLine("            any = true;");

        if (property.ScalarKind == ScalarKind.String)
        {
            sb.Append("            instance.").Append(property.Name).Append(" = ").Append(rawName).AppendLine(";");
        }
        else if (property.IsNullable)
        {
            sb.Append("            if (string.IsNullOrEmpty(").Append(rawName).AppendLine("))");
            sb.AppendLine("            {");
            sb.Append("                instance.").Append(property.Name).AppendLine(" = null;");
            sb.AppendLine("            }");
            sb.AppendLine("            else");
            sb.AppendLine("            {");
            AppendParseBlock(sb, property, rawName, valueName, pathName, targetDisplay, memberDisplay, throwOnFailure, indent: "                ");
            sb.Append("                instance.").Append(property.Name).Append(" = ").Append(valueName).AppendLine(";");
            sb.AppendLine("            }");
        }
        else
        {
            AppendParseBlock(sb, property, rawName, valueName, pathName, targetDisplay, memberDisplay, throwOnFailure, indent: "            ");
            sb.Append("            instance.").Append(property.Name).Append(" = ").Append(valueName).AppendLine(";");
        }

        sb.AppendLine("        }");
    }

    private static void AppendParseBlock(
        StringBuilder sb,
        PropertyModel property,
        string rawName,
        string valueName,
        string pathName,
        string targetDisplay,
        string memberDisplay,
        bool throwOnFailure,
        string indent
    )
    {
        var targetTypeName = property.UnderlyingType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        var parseCall = GetParseCall(property.ScalarKind, rawName, valueName, targetTypeName);

        sb.Append(indent).Append("if (!").Append(parseCall).AppendLine(")");
        sb.Append(indent).AppendLine("{");
        if (throwOnFailure)
        {
            sb.Append(indent)
                .Append("    throw global::PicoCfg.PicoCfgBindRuntime.CreateConversionException(")
                .Append(pathName)
                .Append(", ")
                .Append(targetDisplay)
                .Append(", ")
                .Append(memberDisplay)
                .AppendLine(");");
        }
        else
        {
            sb.Append(indent).AppendLine("    return false;");
        }

        sb.Append(indent).AppendLine("}");
    }

    private static string GetParseCall(ScalarKind scalarKind, string rawName, string valueName, string targetTypeName)
        => scalarKind switch
        {
            ScalarKind.Boolean => $"global::PicoCfg.PicoCfgBindRuntime.TryParseBoolean({rawName}, out var {valueName})",
            ScalarKind.Byte => $"global::PicoCfg.PicoCfgBindRuntime.TryParseByte({rawName}, out var {valueName})",
            ScalarKind.SByte => $"global::PicoCfg.PicoCfgBindRuntime.TryParseSByte({rawName}, out var {valueName})",
            ScalarKind.Int16 => $"global::PicoCfg.PicoCfgBindRuntime.TryParseInt16({rawName}, out var {valueName})",
            ScalarKind.UInt16 => $"global::PicoCfg.PicoCfgBindRuntime.TryParseUInt16({rawName}, out var {valueName})",
            ScalarKind.Int32 => $"global::PicoCfg.PicoCfgBindRuntime.TryParseInt32({rawName}, out var {valueName})",
            ScalarKind.UInt32 => $"global::PicoCfg.PicoCfgBindRuntime.TryParseUInt32({rawName}, out var {valueName})",
            ScalarKind.Int64 => $"global::PicoCfg.PicoCfgBindRuntime.TryParseInt64({rawName}, out var {valueName})",
            ScalarKind.UInt64 => $"global::PicoCfg.PicoCfgBindRuntime.TryParseUInt64({rawName}, out var {valueName})",
            ScalarKind.Single => $"global::PicoCfg.PicoCfgBindRuntime.TryParseSingle({rawName}, out var {valueName})",
            ScalarKind.Double => $"global::PicoCfg.PicoCfgBindRuntime.TryParseDouble({rawName}, out var {valueName})",
            ScalarKind.Decimal => $"global::PicoCfg.PicoCfgBindRuntime.TryParseDecimal({rawName}, out var {valueName})",
            ScalarKind.Guid => $"global::PicoCfg.PicoCfgBindRuntime.TryParseGuid({rawName}, out var {valueName})",
            ScalarKind.Enum => $"global::PicoCfg.PicoCfgBindRuntime.TryParseEnum<{targetTypeName}>({rawName}, out var {valueName})",
            _ => throw new InvalidOperationException($"Unexpected scalar kind '{scalarKind}'."),
        };

    private static bool TryGetScalarKind(ITypeSymbol type, out ScalarKind scalarKind, out ITypeSymbol underlyingType)
    {
        underlyingType = type;
        if (type is INamedTypeSymbol namedType && namedType.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T)
            underlyingType = namedType.TypeArguments[0];

        if (underlyingType.SpecialType == SpecialType.System_String)
        {
            scalarKind = ScalarKind.String;
            return true;
        }

        if (underlyingType.TypeKind == TypeKind.Enum)
        {
            scalarKind = ScalarKind.Enum;
            return true;
        }

        switch (underlyingType.SpecialType)
        {
            case SpecialType.System_Boolean: scalarKind = ScalarKind.Boolean; return true;
            case SpecialType.System_Byte: scalarKind = ScalarKind.Byte; return true;
            case SpecialType.System_SByte: scalarKind = ScalarKind.SByte; return true;
            case SpecialType.System_Int16: scalarKind = ScalarKind.Int16; return true;
            case SpecialType.System_UInt16: scalarKind = ScalarKind.UInt16; return true;
            case SpecialType.System_Int32: scalarKind = ScalarKind.Int32; return true;
            case SpecialType.System_UInt32: scalarKind = ScalarKind.UInt32; return true;
            case SpecialType.System_Int64: scalarKind = ScalarKind.Int64; return true;
            case SpecialType.System_UInt64: scalarKind = ScalarKind.UInt64; return true;
            case SpecialType.System_Single: scalarKind = ScalarKind.Single; return true;
            case SpecialType.System_Double: scalarKind = ScalarKind.Double; return true;
            case SpecialType.System_Decimal: scalarKind = ScalarKind.Decimal; return true;
        }

        if (underlyingType is INamedTypeSymbol namedGuidType
            && namedGuidType.Name == nameof(Guid)
            && namedGuidType.ContainingNamespace.ToDisplayString() == "System")
        {
            scalarKind = ScalarKind.Guid;
            return true;
        }

        scalarKind = default;
        return false;
    }

    private static bool IsNullable(ITypeSymbol type)
        => type is INamedTypeSymbol namedType && namedType.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T;

    private static bool ContainsTypeParameter(ITypeSymbol type)
    {
        if (type.TypeKind == TypeKind.TypeParameter)
            return true;
        if (type is IArrayTypeSymbol arrayType)
            return ContainsTypeParameter(arrayType.ElementType);
        if (type is INamedTypeSymbol namedType)
        {
            if (namedType.IsUnboundGenericType)
                return true;
            foreach (var typeArgument in namedType.TypeArguments)
            {
                if (ContainsTypeParameter(typeArgument))
                    return true;
            }
        }
        return false;
    }

    private static bool IsCollectionType(ITypeSymbol type)
    {
        if (type.SpecialType == SpecialType.System_String)
            return false;
        if (type is IArrayTypeSymbol)
            return true;
        foreach (var iface in type.AllInterfaces)
        {
            if (iface.SpecialType == SpecialType.System_Collections_IEnumerable)
                return true;
        }
        return false;
    }

    private static bool HasPublicParameterlessConstructor(INamedTypeSymbol type)
        => type.InstanceConstructors.Any(ctor => ctor.DeclaredAccessibility == Accessibility.Public && ctor.Parameters.Length == 0);

    private static void ReportAll(SourceProductionContext context, IEnumerable<Location> locations, DiagnosticDescriptor descriptor, params object[] args)
    {
        foreach (var location in locations)
            context.ReportDiagnostic(Diagnostic.Create(descriptor, location, args));
    }

    private static bool TryGetOperation(IMethodSymbol method, out BindOperation operation)
    {
        operation = default;
        if (!method.IsGenericMethod || method.TypeArguments.Length != 1)
            return false;

        if (method.ContainingType.Name == "PicoCfgBind" && method.ContainingType.ContainingNamespace.ToDisplayString() == "PicoCfg")
        {
            switch (method.Name)
            {
                case "Bind": operation = BindOperation.Bind; return true;
                case "TryBind": operation = BindOperation.TryBind; return true;
                case "BindInto": operation = BindOperation.BindInto; return true;
                default: return false;
            }
        }

        if (method.ContainingType.Name == "SvcContainerExtensions" && method.ContainingType.ContainingNamespace.ToDisplayString() == "PicoCfg.DI")
        {
            switch (method.Name)
            {
                case "RegisterCfgTransient": operation = BindOperation.Bind; return true;
                case "RegisterCfgScoped": operation = BindOperation.Bind; return true;
                case "RegisterCfgSingleton": operation = BindOperation.Bind; return true;
                case "RegisterPicoCfgTransient": operation = BindOperation.Bind; return true;
                case "RegisterPicoCfgScoped": operation = BindOperation.Bind; return true;
                case "RegisterPicoCfgSingleton": operation = BindOperation.Bind; return true;
                default: return false;
            }
        }

        return false;
    }

    private static bool IsTargetMethodName(string methodName)
        => methodName is "Bind" or "TryBind" or "BindInto"
            or "RegisterCfgTransient" or "RegisterCfgScoped" or "RegisterCfgSingleton"
            or "RegisterPicoCfgTransient" or "RegisterPicoCfgScoped" or "RegisterPicoCfgSingleton";

    private sealed class TargetRegistration(ITypeSymbol targetType)
    {
        public ITypeSymbol TargetType { get; } = targetType;
        public BindOperation Operations { get; set; }
        public ImmutableArray<Location>.Builder Locations { get; } = ImmutableArray.CreateBuilder<Location>();
    }

    private sealed class BindCall
    {
        public BindCall(ITypeSymbol targetType, BindOperation operation, Location location)
        {
            TargetType = targetType;
            Operation = operation;
            Location = location;
        }

        public ITypeSymbol TargetType { get; }
        public BindOperation Operation { get; }
        public Location Location { get; }
    }

    private sealed class TargetModel
    {
        public TargetModel(INamedTypeSymbol targetType, BindOperation operations, ImmutableArray<PropertyModel> properties, bool hasPublicParameterlessConstructor)
        {
            TargetType = targetType;
            Operations = operations;
            Properties = properties;
            HasPublicParameterlessConstructor = hasPublicParameterlessConstructor;
        }

        public INamedTypeSymbol TargetType { get; }
        public BindOperation Operations { get; }
        public ImmutableArray<PropertyModel> Properties { get; }
        public bool HasPublicParameterlessConstructor { get; }
    }

    private sealed class PropertyModel
    {
        public PropertyModel(string name, ITypeSymbol type, ScalarKind scalarKind, ITypeSymbol underlyingType, bool isNullable)
        {
            Name = name;
            Type = type;
            ScalarKind = scalarKind;
            UnderlyingType = underlyingType;
            IsNullable = isNullable;
        }

        public string Name { get; }
        public ITypeSymbol Type { get; }
        public ScalarKind ScalarKind { get; }
        public ITypeSymbol UnderlyingType { get; }
        public bool IsNullable { get; }
    }

    [Flags]
    private enum BindOperation
    {
        None = 0,
        Bind = 1,
        TryBind = 2,
        BindInto = 4,
    }

    private enum ScalarKind
    {
        String,
        Boolean,
        Byte,
        SByte,
        Int16,
        UInt16,
        Int32,
        UInt32,
        Int64,
        UInt64,
        Single,
        Double,
        Decimal,
        Guid,
        Enum,
    }

    private static class Diagnostics
    {
        public static readonly DiagnosticDescriptor TargetMustBeClosedNamedType = new(
            id: "PCFGGEN001",
            title: "Binding target must be a closed named type",
            messageFormat: "Direct closed named target type required; '{0}' is not supported",
            category: "PicoCfg.Gen",
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true
        );

        public static readonly DiagnosticDescriptor MissingPublicParameterlessConstructor = new(
            id: "PCFGGEN002",
            title: "Binding target must have a public parameterless constructor",
            messageFormat: "PicoCfgBind.Bind<T> and PicoCfgBind.TryBind<T> require '{0}' to declare a public parameterless constructor",
            category: "PicoCfg.Gen",
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true
        );

        public static readonly DiagnosticDescriptor UnsupportedComplexProperty = new(
            id: "PCFGGEN003",
            title: "Complex properties are not supported",
            messageFormat: "PicoCfg.Gen v1 only supports flat scalar properties; '{0}.{1}' has unsupported complex type '{2}'",
            category: "PicoCfg.Gen",
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true
        );

        public static readonly DiagnosticDescriptor UnsupportedCollectionProperty = new(
            id: "PCFGGEN004",
            title: "Collection properties are not supported",
            messageFormat: "PicoCfg.Gen v1 does not support collection property '{0}.{1}' of type '{2}'",
            category: "PicoCfg.Gen",
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true
        );

        public static readonly DiagnosticDescriptor UnsupportedPropertyType = new(
            id: "PCFGGEN005",
            title: "Property type is not supported",
            messageFormat: "PicoCfg.Gen v1 does not support property '{0}.{1}' of type '{2}'",
            category: "PicoCfg.Gen",
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true
        );

        public static readonly DiagnosticDescriptor UnsupportedProperty = new(
            id: "PCFGGEN006",
            title: "Property shape is not supported",
            messageFormat: "PicoCfg.Gen v1 only supports {2}; '{0}.{1}' is not supported",
            category: "PicoCfg.Gen",
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true
        );

        public static readonly DiagnosticDescriptor TargetMustBeReferenceType = new(
            id: "PCFGGEN007",
            title: "Binding target must be a concrete class",
            messageFormat: "PicoCfg.Gen v1 only supports concrete class targets; '{0}' is not supported",
            category: "PicoCfg.Gen",
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true
        );
    }
}
