namespace PicoCfg.DI;

public static class SvcContainerExtensions
{
    extension(ISvcContainer container)
    {
        public ISvcContainer RegisterPicoCfg(ICfgRoot root)
        {
            ArgumentNullException.ThrowIfNull(container);
            ArgumentNullException.ThrowIfNull(root);

            return container
                .RegisterSingle<ICfgRoot>(root)
                .RegisterTransient<ICfg>(static scope => scope.GetService<ICfgRoot>());
        }

        public ISvcContainer RegisterCfgRoot(ICfgRoot root)
            => container.RegisterPicoCfg(root);

        public ISvcContainer RegisterPicoCfgTransient<T>(string? section = null)
            where T : class
            => container.RegisterTransient<T>(scope => Bind<T>(scope, section));

        public ISvcContainer RegisterPicoCfgScoped<T>(string? section = null)
            where T : class
            => container.RegisterScoped<T>(scope => Bind<T>(scope, section));

        public ISvcContainer RegisterPicoCfgSingleton<T>(string? section = null)
            where T : class
            => container.RegisterSingleton<T>(scope => Bind<T>(scope, section));

        public ISvcContainer RegisterCfgTransient<T>(string? section = null)
            where T : class
            => container.RegisterPicoCfgTransient<T>(section);

        public ISvcContainer RegisterCfgScoped<T>(string? section = null)
            where T : class
            => container.RegisterPicoCfgScoped<T>(section);

        public ISvcContainer RegisterCfgSingleton<T>(string? section = null)
            where T : class
            => container.RegisterPicoCfgSingleton<T>(section);
    }

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

        throw new InvalidOperationException(
            "No PicoCfg configuration source is registered. Call RegisterPicoCfg(...) or RegisterCfgRoot(...) before registering bound configuration services."
        );
    }

    private static IEnumerable<T> TryGetServices<T>(ISvcScope scope) where T : notnull
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
