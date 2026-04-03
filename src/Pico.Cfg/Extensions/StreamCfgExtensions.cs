namespace Pico.CFG.Extensions;

public static class StreamCfgExtensions
{
    extension(ICfgBuilder builder)
    {
        public ICfgBuilder Add(Func<Stream> streamFactory) =>
            builder.AddSource(new StreamCfgSource(streamFactory));

        public ICfgBuilder Add(string configContent,
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

        public ICfgBuilder Add(IDictionary<string, string> configData
        ) => builder.Add(string.Join("\n", configData.Select(kv => $"{kv.Key}={kv.Value}")));
    }
}
