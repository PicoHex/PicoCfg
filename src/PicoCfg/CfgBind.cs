namespace PicoCfg;

using System.Diagnostics.CodeAnalysis;

public static class CfgBind
{
    public static T Bind<T>(ICfgSnapshot snapshot, string? section = null)
        => PicoCfgBind.Bind<T>(snapshot, section);

    public static bool TryBind<T>(ICfgSnapshot snapshot, [MaybeNullWhen(false)] out T value, string? section = null)
        => PicoCfgBind.TryBind(snapshot, out value, section);

    public static void BindInto<T>(ICfgSnapshot snapshot, T instance, string? section = null)
        => PicoCfgBind.BindInto(snapshot, instance, section);

    public static T Bind<T>(ICfgRuntime runtime, string? section = null)
    {
        ArgumentNullException.ThrowIfNull(runtime);
        return Bind<T>(runtime.Current, section);
    }

    public static bool TryBind<T>(ICfgRuntime runtime, [MaybeNullWhen(false)] out T value, string? section = null)
    {
        ArgumentNullException.ThrowIfNull(runtime);
        return TryBind(runtime.Current, out value, section);
    }

    public static void BindInto<T>(ICfgRuntime runtime, T instance, string? section = null)
    {
        ArgumentNullException.ThrowIfNull(runtime);
        BindInto(runtime.Current, instance, section);
    }

    public static T Bind<T>(ICfg cfg, string? section = null)
    {
        ArgumentNullException.ThrowIfNull(cfg);

        if (cfg is ICfgSnapshot snapshot)
            return Bind<T>(snapshot, section);

        throw new InvalidOperationException(
            $"{nameof(CfgBind)} currently requires an {nameof(ICfgSnapshot)} instance. Received '{cfg.GetType().FullName}'."
        );
    }

    public static bool TryBind<T>(ICfg cfg, [MaybeNullWhen(false)] out T value, string? section = null)
    {
        ArgumentNullException.ThrowIfNull(cfg);

        if (cfg is ICfgSnapshot snapshot)
            return TryBind(snapshot, out value, section);

        throw new InvalidOperationException(
            $"{nameof(CfgBind)} currently requires an {nameof(ICfgSnapshot)} instance. Received '{cfg.GetType().FullName}'."
        );
    }

    public static void BindInto<T>(ICfg cfg, T instance, string? section = null)
    {
        ArgumentNullException.ThrowIfNull(cfg);

        if (cfg is ICfgSnapshot snapshot)
        {
            BindInto(snapshot, instance, section);
            return;
        }

        throw new InvalidOperationException(
            $"{nameof(CfgBind)} currently requires an {nameof(ICfgSnapshot)} instance. Received '{cfg.GetType().FullName}'."
        );
    }
}
