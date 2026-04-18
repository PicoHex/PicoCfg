namespace PicoCfg.Gen.Generator;

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

[Generator(LanguageNames.CSharp)]
public sealed partial class PicoCfgBindGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var calls = context.SyntaxProvider
            .CreateSyntaxProvider(static (node, _) => IsCandidateInvocation(node), static (ctx, _) => TransformInvocation(ctx))
            .Where(static call => call is not null)
            .Select(static (call, _) => call!);

        context.RegisterSourceOutput(calls.Collect(), static (spc, collectedCalls) => Execute(spc, collectedCalls));
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
            messageFormat: "CfgBind.Bind<T> and CfgBind.TryBind<T> require '{0}' to declare a public parameterless constructor",
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
