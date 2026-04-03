namespace Pico.CFG;

public static class Cfg
{
    public static ICfgBuilder CreateBuilder() => new CfgBuilder();
}
