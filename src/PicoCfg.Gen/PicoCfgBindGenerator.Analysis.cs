namespace PicoCfg.Gen.Generator;

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;

// Validates target shapes and translates symbols into generation models.
public sealed partial class PicoCfgBindGenerator
{
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
}
