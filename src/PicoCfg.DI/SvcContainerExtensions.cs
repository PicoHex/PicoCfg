namespace PicoCfg.DI;

using PicoCfg.Abs;
using PicoDI.Abs;

public static class SvcContainerExtensions
{
    public static ISvcContainer RegisterCfgRoot(this ISvcContainer container, ICfgRoot root)
        => container
            .RegisterSingle<ICfgRoot>(root)
            .RegisterTransient<ICfgSnapshot>(static scope => scope.GetService<ICfgRoot>().Snapshot);

    public static ISvcContainer RegisterCfgSnapshot(this ISvcContainer container, ICfgSnapshot snapshot)
        => container.RegisterSingle<ICfgSnapshot>(snapshot);

    public static ISvcContainer RegisterCfgTransient<T>(this ISvcContainer container, string? section = null)
        where T : class
        => container.RegisterTransient<T>(scope => Bind<T>(scope, section));

    public static ISvcContainer RegisterCfgScoped<T>(this ISvcContainer container, string? section = null)
        where T : class
        => container.RegisterScoped<T>(scope => Bind<T>(scope, section));

    public static ISvcContainer RegisterCfgSingleton<T>(this ISvcContainer container, string? section = null)
        where T : class
        => container.RegisterSingleton<T>(scope => Bind<T>(scope, section));

    private static T Bind<T>(ISvcScope scope, string? section)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(scope);

        var snapshot = TryGetSnapshot(scope);
        if (snapshot is not null)
            return PicoCfgBind.Bind<T>(snapshot, section);

        throw new InvalidOperationException(
            "No PicoCfg configuration source is registered. Call RegisterCfgRoot(...) or RegisterCfgSnapshot(...) before registering bound configuration services."
        );
    }

    private static ICfgSnapshot? TryGetSnapshot(ISvcScope scope)
    {
        ArgumentNullException.ThrowIfNull(scope);

        var snapshot = TryGetServices<ICfgSnapshot>(scope).LastOrDefault();
        if (snapshot is not null)
            return snapshot;

        var root = TryGetServices<ICfgRoot>(scope).LastOrDefault();
        return root?.Snapshot;
    }

    private static IEnumerable<T> TryGetServices<T>(ISvcScope scope)
    {
        ArgumentNullException.ThrowIfNull(scope);

        try
        {
            return scope.GetServices<T>();
        }
        catch (PicoDiException)
        {
            return [];
        }
    }
}
