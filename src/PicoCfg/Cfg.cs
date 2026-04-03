namespace PicoCfg;

public static class Cfg
{
    public static ICfgBuilder CreateBuilder() => new CfgBuilder();
}
