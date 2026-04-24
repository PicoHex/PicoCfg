namespace PicoCfg;

[EditorBrowsable(EditorBrowsableState.Never)]
public static class CfgBindRuntime
{
    public const int ContractVersion = 1;

    public static void Register<T>(
        int contractVersion,
        Func<ICfg, string?, T>? bind,
        PicoCfgGeneratedTryBindDelegate<T>? tryBind,
        PicoCfgGeneratedBindIntoDelegate<T> bindInto
    )
    {
        ArgumentNullException.ThrowIfNull(bindInto);
        PicoCfgBindRegistrationStore<T>.Registration = new PicoCfgBindRegistration<T>(
            contractVersion,
            bind,
            tryBind,
            bindInto
        );
    }

    public static string CombinePath(string? section, string propertyName)
    {
        ArgumentException.ThrowIfNullOrEmpty(propertyName);
        return string.IsNullOrEmpty(section)
            ? propertyName
            : string.Concat(section, ":", propertyName);
    }

    public static FormatException CreateConversionException(
        string path,
        string targetTypeDisplayName,
        string memberDisplayName
    )
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        ArgumentException.ThrowIfNullOrEmpty(targetTypeDisplayName);
        ArgumentException.ThrowIfNullOrEmpty(memberDisplayName);

        return new FormatException(
            $"Configuration value at '{path}' could not be converted to '{targetTypeDisplayName}' for '{memberDisplayName}'."
        );
    }

    public static bool TryParseBoolean(string? raw, out bool value) =>
        bool.TryParse(raw, out value);

    public static bool TryParseByte(string? raw, out byte value) =>
        byte.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);

    public static bool TryParseSByte(string? raw, out sbyte value) =>
        sbyte.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);

    public static bool TryParseInt16(string? raw, out short value) =>
        short.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);

    public static bool TryParseUInt16(string? raw, out ushort value) =>
        ushort.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);

    public static bool TryParseInt32(string? raw, out int value) =>
        int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);

    public static bool TryParseUInt32(string? raw, out uint value) =>
        uint.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);

    public static bool TryParseInt64(string? raw, out long value) =>
        long.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);

    public static bool TryParseUInt64(string? raw, out ulong value) =>
        ulong.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);

    public static bool TryParseSingle(string? raw, out float value) =>
        float.TryParse(
            raw,
            NumberStyles.Float | NumberStyles.AllowThousands,
            CultureInfo.InvariantCulture,
            out value
        );

    public static bool TryParseDouble(string? raw, out double value) =>
        double.TryParse(
            raw,
            NumberStyles.Float | NumberStyles.AllowThousands,
            CultureInfo.InvariantCulture,
            out value
        );

    public static bool TryParseDecimal(string? raw, out decimal value) =>
        decimal.TryParse(raw, NumberStyles.Number, CultureInfo.InvariantCulture, out value);

    public static bool TryParseGuid(string? raw, out Guid value) => Guid.TryParse(raw, out value);

    public static bool TryParseEnum<TEnum>(string? raw, out TEnum value)
        where TEnum : struct, Enum => Enum.TryParse(raw, ignoreCase: false, out value);

    internal static PicoCfgBindRegistration<T> GetRequiredRegistration<T>(string operationName)
    {
        var registration = PicoCfgBindRegistrationStore<T>.Registration;
        if (registration is null)
            throw PicoCfgBindRegistrationException.CreateMissing(typeof(T), operationName);

        if (registration.ContractVersion != ContractVersion)
        {
            throw PicoCfgBindRegistrationException.CreateIncompatible(
                typeof(T),
                operationName,
                ContractVersion,
                registration.ContractVersion
            );
        }

        return registration;
    }

    private static class PicoCfgBindRegistrationStore<T>
    {
        public static PicoCfgBindRegistration<T>? Registration;
    }
}



[EditorBrowsable(EditorBrowsableState.Never)]
public delegate bool PicoCfgGeneratedTryBindDelegate<T>(
    ICfg cfg,
    string? section,
    [MaybeNullWhen(false)] out T value
);

[EditorBrowsable(EditorBrowsableState.Never)]
public delegate void PicoCfgGeneratedBindIntoDelegate<in T>(ICfg cfg, string? section, T instance);

internal sealed class PicoCfgBindRegistration<T>(
    int contractVersion,
    Func<ICfg, string?, T>? bind,
    PicoCfgGeneratedTryBindDelegate<T>? tryBind,
    PicoCfgGeneratedBindIntoDelegate<T> bindInto
)
{
    public int ContractVersion { get; } = contractVersion;
    public Func<ICfg, string?, T>? Bind { get; } = bind;
    public PicoCfgGeneratedTryBindDelegate<T>? TryBind { get; } = tryBind;
    public PicoCfgGeneratedBindIntoDelegate<T> BindInto { get; } = bindInto;
}
