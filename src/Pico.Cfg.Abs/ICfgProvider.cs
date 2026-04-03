namespace Pico.CFG.Abs;

public interface ICfgProvider : ICfgNode
{
    ValueTask LoadAsync(CancellationToken ct = default);
}
