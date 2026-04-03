namespace PicoCfg.Abs;

public interface ICfgSource
{
    ValueTask<ICfgProvider> OpenAsync(CancellationToken ct = default);
}
