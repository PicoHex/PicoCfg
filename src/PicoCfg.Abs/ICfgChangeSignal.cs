namespace PicoCfg.Abs;

public interface ICfgChangeSignal
{
    bool HasChanged { get; }
    ValueTask WaitForChangeAsync(CancellationToken ct = default);
}
