namespace PicoCfg.DI;

using PicoCfg.Abs;
using PicoDI.Abs;

public static class SvcContainerExtensions
{
    public static ISvcContainer RegisterPicoCfg(this ISvcContainer container, ICfgRoot root)
    {
        ArgumentNullException.ThrowIfNull(container);
        ArgumentNullException.ThrowIfNull(root);

        return container
            .RegisterSingle<ICfgRoot>(root)
            .RegisterTransient<ICfg>(static scope => scope.GetService<ICfgRoot>());
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
        => container.RegisterPicoCfg(root);

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

        var root = TryGetServices<ICfgRoot>(scope).LastOrDefault();
        if (root is not null)
            return CfgBind.Bind<T>(root, section);

        var cfg = TryGetServices<ICfg>(scope).LastOrDefault();
        if (cfg is not null)
            return CfgBind.Bind<T>(cfg, section);

        var snapshot = TryGetSnapshot(scope);
        if (snapshot is not null)
            return CfgBind.Bind<T>(snapshot, section);

        throw new InvalidOperationException(
            "No PicoCfg configuration source is registered. Call RegisterPicoCfg(...) or RegisterCfgRoot(...) before registering bound configuration services. RegisterCfgSnapshot(...) remains available for advanced fixed-snapshot scenarios."
        );
    }

    private static ICfgSnapshot? TryGetSnapshot(ISvcScope scope)
    {
        ArgumentNullException.ThrowIfNull(scope);

        var snapshot = TryGetServices<ICfgSnapshot>(scope).LastOrDefault();
        if (snapshot is not null)
            return snapshot;

        return null;
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
