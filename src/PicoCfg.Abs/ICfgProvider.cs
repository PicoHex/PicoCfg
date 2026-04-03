namespace PicoCfg.Abs;

public interface ICfgProvider : ICfgNode
{
    ValueTask LoadAsync(CancellationToken ct = default);
}
