namespace Pico.CFG.Abs;

public interface ICfgBuilder
{
    ICfgBuilder AddSource(ICfgSource source);
    ValueTask<ICfgRoot> BuildAsync(CancellationToken ct = default);
}
