namespace Pico.CFG.Abs;

public interface ICfgSource
{
    ValueTask<ICfgProvider> BuildProviderAsync(CancellationToken ct = default);
}
