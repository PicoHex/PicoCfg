namespace PicoCfg;

using System.Diagnostics.CodeAnalysis;

public static class CfgBind
{
    public static T Bind<T>(ICfgRoot root, string? section = null)
    {
        ArgumentNullException.ThrowIfNull(root);
        return Bind<T>((ICfg)root, section);
    }

    public static bool TryBind<T>(ICfgRoot root, [MaybeNullWhen(false)] out T value, string? section = null)
    {
        ArgumentNullException.ThrowIfNull(root);
        return TryBind((ICfg)root, out value, section);
    }

    public static void BindInto<T>(ICfgRoot root, T instance, string? section = null)
    {
        ArgumentNullException.ThrowIfNull(root);
        BindInto((ICfg)root, instance, section);
    }

    public static T Bind<T>(ICfg cfg, string? section = null)
    {
        ArgumentNullException.ThrowIfNull(cfg);

        var registration = CfgBindRuntime.GetRequiredRegistration<T>(nameof(Bind));
        if (registration.Bind is null)
            throw PicoCfgBindRegistrationException.CreateMissing(typeof(T), nameof(Bind));

        return registration.Bind(cfg, section);
    }

    public static bool TryBind<T>(ICfg cfg, [MaybeNullWhen(false)] out T value, string? section = null)
    {
        ArgumentNullException.ThrowIfNull(cfg);

        var registration = CfgBindRuntime.GetRequiredRegistration<T>(nameof(TryBind));
        if (registration.TryBind is null)
            throw PicoCfgBindRegistrationException.CreateMissing(typeof(T), nameof(TryBind));

        return registration.TryBind(cfg, section, out value);
    }

    public static void BindInto<T>(ICfg cfg, T instance, string? section = null)
    {
        ArgumentNullException.ThrowIfNull(cfg);
        ArgumentNullException.ThrowIfNull(instance);

        var registration = CfgBindRuntime.GetRequiredRegistration<T>(nameof(BindInto));
        registration.BindInto(cfg, section, instance);
    }
}
