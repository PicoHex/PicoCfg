namespace PicoCfg.Gen.Generator;

using System;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

public sealed partial class PicoCfgBindGenerator
{
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
}
