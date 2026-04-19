namespace PicoCfg.Gen;

// Holds the internal models shared across discovery, analysis, and rendering.
public sealed partial class PicoCfgBindGenerator
{
    private sealed class TargetRegistration(ITypeSymbol targetType)
    {
        public ITypeSymbol TargetType { get; } = targetType;
        public BindOperation Operations { get; set; }
        public ImmutableArray<Location>.Builder Locations { get; } =
            ImmutableArray.CreateBuilder<Location>();
    }

    private sealed class BindCall(
        ITypeSymbol targetType,
        BindOperation operation,
        Location location
    )
    {
        public ITypeSymbol TargetType { get; } = targetType;
        public BindOperation Operation { get; } = operation;
        public Location Location { get; } = location;
    }

    private sealed class TargetModel(
        INamedTypeSymbol targetType,
        BindOperation operations,
        ImmutableArray<PropertyModel> properties,
        bool hasPublicParameterlessConstructor
    )
    {
        public INamedTypeSymbol TargetType { get; } = targetType;
        public BindOperation Operations { get; } = operations;
        public ImmutableArray<PropertyModel> Properties { get; } = properties;
        public bool HasPublicParameterlessConstructor { get; } = hasPublicParameterlessConstructor;
    }

    private sealed class PropertyModel(
        string name,
        ITypeSymbol type,
        ScalarKind scalarKind,
        ITypeSymbol underlyingType,
        bool isNullable
    )
    {
        public string Name { get; } = name;
        public ITypeSymbol Type { get; } = type;
        public ScalarKind ScalarKind { get; } = scalarKind;
        public ITypeSymbol UnderlyingType { get; } = underlyingType;
        public bool IsNullable { get; } = isNullable;
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
