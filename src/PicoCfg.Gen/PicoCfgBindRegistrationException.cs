namespace PicoCfg;

public sealed class PicoCfgBindRegistrationException : InvalidOperationException
{
    internal PicoCfgBindRegistrationException(string message)
        : base(message) { }

    public static PicoCfgBindRegistrationException CreateMissing(Type targetType, string operationName)
    {
        ArgumentNullException.ThrowIfNull(targetType);
        ArgumentException.ThrowIfNullOrEmpty(operationName);

        return new PicoCfgBindRegistrationException(
            $"No generated PicoCfg.Gen registration was found for '{targetType.FullName}' while calling PicoCfgBind.{operationName}<T>. "
            + "Ensure the consuming project references PicoCfg.Gen and uses a direct closed generic PicoCfgBind call so the source generator can register the binder."
        );
    }

    public static PicoCfgBindRegistrationException CreateIncompatible(
        Type targetType,
        string operationName,
        int expectedContractVersion,
        int actualContractVersion
    )
    {
        ArgumentNullException.ThrowIfNull(targetType);
        ArgumentException.ThrowIfNullOrEmpty(operationName);

        return new PicoCfgBindRegistrationException(
            $"Generated PicoCfg.Gen registration for '{targetType.FullName}' is incompatible while calling PicoCfgBind.{operationName}<T>. "
            + $"Expected contract version {expectedContractVersion}, but the generated registration reported version {actualContractVersion}."
        );
    }
}
