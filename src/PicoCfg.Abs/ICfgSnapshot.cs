namespace PicoCfg.Abs;

public interface ICfgSnapshot
{
    bool TryGetValue(string path, out string? value);
}
