namespace PicoCfg;

using System.Diagnostics.CodeAnalysis;

public static class PicoCfgBind
{
    public static T Bind<T>(ICfgSnapshot snapshot, string? section = null)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        var registration = PicoCfgBindRuntime.GetRequiredRegistration<T>(nameof(Bind));
        if (registration.Bind is null)
            throw PicoCfgBindRegistrationException.CreateMissing(typeof(T), nameof(Bind));

        return registration.Bind(snapshot, section);
    }

    public static bool TryBind<T>(ICfgSnapshot snapshot, [MaybeNullWhen(false)] out T value, string? section = null)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        var registration = PicoCfgBindRuntime.GetRequiredRegistration<T>(nameof(TryBind));
        if (registration.TryBind is null)
            throw PicoCfgBindRegistrationException.CreateMissing(typeof(T), nameof(TryBind));

        return registration.TryBind(snapshot, section, out value);
    }

    public static void BindInto<T>(ICfgSnapshot snapshot, T instance, string? section = null)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(instance);

        var registration = PicoCfgBindRuntime.GetRequiredRegistration<T>(nameof(BindInto));
        registration.BindInto(snapshot, section, instance);
    }

    public static T Bind<T>(ICfgRoot root, string? section = null)
    {
        ArgumentNullException.ThrowIfNull(root);
        var snapshot = root.Snapshot;
        return Bind<T>(snapshot, section);
    }

    public static bool TryBind<T>(ICfgRoot root, [MaybeNullWhen(false)] out T value, string? section = null)
    {
        ArgumentNullException.ThrowIfNull(root);
        var snapshot = root.Snapshot;
        return TryBind(snapshot, out value, section);
    }

    public static void BindInto<T>(ICfgRoot root, T instance, string? section = null)
    {
        ArgumentNullException.ThrowIfNull(root);
        var snapshot = root.Snapshot;
        BindInto(snapshot, instance, section);
    }
}
