namespace PicoCfg.DI;

using PicoCfg.Abs;
using PicoDI.Abs;

public static class SvcContainerExtensions
{
    public static ISvcContainer RegisterPicoCfg(this ISvcContainer container, ICfgRuntime runtime)
    {
        ArgumentNullException.ThrowIfNull(container);
        ArgumentNullException.ThrowIfNull(runtime);

        return container
            .RegisterSingle<ICfgRuntime>(runtime)
            .RegisterTransient<ICfgSnapshot>(static scope => scope.GetService<ICfgRuntime>().Current)
            .RegisterTransient<ICfg>(static scope => scope.GetService<ICfgRuntime>().Current);
    }

    public static ISvcContainer RegisterPicoCfg(this ISvcContainer container, ICfgSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(container);
        ArgumentNullException.ThrowIfNull(snapshot);

        return container
            .RegisterSingle<ICfgSnapshot>(snapshot)
            .RegisterSingle<ICfg>(snapshot);
    }

    public static ISvcContainer RegisterCfgRoot(this ISvcContainer container, ICfgRoot root)
        => container
            .RegisterPicoCfg((ICfgRuntime)root)
            .RegisterSingle<ICfgRoot>(root);

    public static ISvcContainer RegisterCfgSnapshot(this ISvcContainer container, ICfgSnapshot snapshot)
        => container.RegisterPicoCfg(snapshot);

    public static ISvcContainer RegisterPicoCfgTransient<T>(this ISvcContainer container, string? section = null)
        where T : class
        => container.RegisterTransient<T>(scope => Bind<T>(scope, section));

    public static ISvcContainer RegisterPicoCfgScoped<T>(this ISvcContainer container, string? section = null)
        where T : class
        => container.RegisterScoped<T>(scope => Bind<T>(scope, section));

    public static ISvcContainer RegisterPicoCfgSingleton<T>(this ISvcContainer container, string? section = null)
        where T : class
        => container.RegisterSingleton<T>(scope => Bind<T>(scope, section));

    public static ISvcContainer RegisterCfgTransient<T>(this ISvcContainer container, string? section = null)
        where T : class
        => container.RegisterPicoCfgTransient<T>(section);

    public static ISvcContainer RegisterCfgScoped<T>(this ISvcContainer container, string? section = null)
        where T : class
        => container.RegisterPicoCfgScoped<T>(section);

    public static ISvcContainer RegisterCfgSingleton<T>(this ISvcContainer container, string? section = null)
        where T : class
        => container.RegisterPicoCfgSingleton<T>(section);

    private static T Bind<T>(ISvcScope scope, string? section)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(scope);

        var snapshot = TryGetSnapshot(scope);
        if (snapshot is not null)
            return CfgBind.Bind<T>(snapshot, section);

        throw new InvalidOperationException(
            "No PicoCfg configuration source is registered. Call RegisterPicoCfg(...), RegisterCfgRoot(...), or RegisterCfgSnapshot(...) before registering bound configuration services."
        );
    }

    private static ICfgSnapshot? TryGetSnapshot(ISvcScope scope)
    {
        ArgumentNullException.ThrowIfNull(scope);

        var snapshot = TryGetServices<ICfgSnapshot>(scope).LastOrDefault();
        if (snapshot is not null)
            return snapshot;

        var runtime = TryGetServices<ICfgRuntime>(scope).LastOrDefault();
        if (runtime is not null)
            return runtime.Current;

        var cfg = TryGetServices<ICfg>(scope).LastOrDefault();
        if (cfg is ICfgSnapshot cfgSnapshot)
            return cfgSnapshot;

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
