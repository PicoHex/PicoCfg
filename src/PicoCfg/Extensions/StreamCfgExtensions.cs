namespace PicoCfg.Extensions;

public static class StreamCfgExtensions
{
    extension(CfgBuilder builder)
    {
        public CfgBuilder Add(Func<Stream> streamFactory) =>
            builder.AddSource(new StreamCfgSource(streamFactory));

        public CfgBuilder Add(string configContent,
            Encoding? encoding = null
        ) =>
            builder.Add(() =>
            {
                var stream = new MemoryStream();
                using var writer = new StreamWriter(stream, encoding ?? Encoding.UTF8, leaveOpen: true);
                writer.Write(configContent);
                writer.Flush();
                stream.Position = 0;
                return stream;
            });

        public CfgBuilder Add(IDictionary<string, string> configData
        ) => builder.AddSource(new DictionaryCfgSource(configData));
    }
}
