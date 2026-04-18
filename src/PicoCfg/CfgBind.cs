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

    public static T Bind<T>(ICfgRoot root, string? section = null)
    {
        ArgumentNullException.ThrowIfNull(root);
        return PicoCfgBind.Bind<T>(root, section);
    }

    public static bool TryBind<T>(ICfgRoot root, [MaybeNullWhen(false)] out T value, string? section = null)
    {
        ArgumentNullException.ThrowIfNull(root);
        return PicoCfgBind.TryBind(root, out value, section);
    }

    public static void BindInto<T>(ICfgRoot root, T instance, string? section = null)
    {
        ArgumentNullException.ThrowIfNull(root);
        PicoCfgBind.BindInto(root, instance, section);
    }

    public static T Bind<T>(ICfg cfg, string? section = null)
    {
        ArgumentNullException.ThrowIfNull(cfg);

        return PicoCfgBind.Bind<T>(cfg, section);
    }

    public static bool TryBind<T>(ICfg cfg, [MaybeNullWhen(false)] out T value, string? section = null)
    {
        ArgumentNullException.ThrowIfNull(cfg);

        return PicoCfgBind.TryBind(cfg, out value, section);
    }

    public static void BindInto<T>(ICfg cfg, T instance, string? section = null)
    {
        ArgumentNullException.ThrowIfNull(cfg);

        PicoCfgBind.BindInto(cfg, instance, section);
    }
}
