namespace PicoCfg;

using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;

[EditorBrowsable(EditorBrowsableState.Never)]
[Obsolete("Use CfgBind instead.")]
public static class PicoCfgBind
{
    public static T Bind<T>(ICfgSnapshot snapshot, string? section = null)
        => CfgBind.Bind<T>(snapshot, section);

    public static T Bind<T>(ICfg cfg, string? section = null)
        => CfgBind.Bind<T>(cfg, section);

    public static bool TryBind<T>(ICfgSnapshot snapshot, [MaybeNullWhen(false)] out T value, string? section = null)
        => CfgBind.TryBind(snapshot, out value, section);

    public static bool TryBind<T>(ICfg cfg, [MaybeNullWhen(false)] out T value, string? section = null)
        => CfgBind.TryBind(cfg, out value, section);

    public static void BindInto<T>(ICfgSnapshot snapshot, T instance, string? section = null)
        => CfgBind.BindInto(snapshot, instance, section);

    public static void BindInto<T>(ICfg cfg, T instance, string? section = null)
        => CfgBind.BindInto(cfg, instance, section);

    public static T Bind<T>(ICfgRoot root, string? section = null)
        => CfgBind.Bind<T>(root, section);

    public static bool TryBind<T>(ICfgRoot root, [MaybeNullWhen(false)] out T value, string? section = null)
        => CfgBind.TryBind(root, out value, section);

    public static void BindInto<T>(ICfgRoot root, T instance, string? section = null)
        => CfgBind.BindInto(root, instance, section);
}
