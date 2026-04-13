namespace PicoCfg.Extensions;

/// <summary>
/// Convenience methods for adding common configuration source types to a <see cref="CfgBuilder"/>.
/// </summary>
public static class CfgBuilderExtensions
{
    extension(CfgBuilder builder)
    {
        /// <summary>
        /// Adds a stream-based source.
        /// The stream content is parsed as line-based <c>key=value</c> text on each reload.
        /// </summary>
        public CfgBuilder Add(Func<Stream> streamFactory, Func<object?>? versionStampFactory = null) =>
            builder.AddSource(new StreamCfgSource(streamFactory, versionStampFactory));

        /// <summary>
        /// Adds inline text content as a stream-based source.
        /// The content is parsed as line-based <c>key=value</c> text.
        /// Each reload recreates a stream so inline text stays on the same parsing path as stream-based sources.
        /// </summary>
        public CfgBuilder Add(string configContent,
            Encoding? encoding = null,
            Func<object?>? versionStampFactory = null
        ) =>
            builder.AddSource(new StreamCfgSource(() =>
            {
                var stream = new MemoryStream();
                using var writer = new StreamWriter(stream, encoding ?? Encoding.UTF8, leaveOpen: true);
                writer.Write(configContent);
                writer.Flush();
                stream.Position = 0;
                return stream;
            }, versionStampFactory));

        /// <summary>
        /// Adds an in-memory dictionary source.
        /// Dictionary values are used as-is and are not reparsed as <c>key=value</c> text.
        /// </summary>
        public CfgBuilder Add(IDictionary<string, string> configData,
            Func<object?>? versionStampFactory = null
        ) => builder.AddSource(new DictionaryCfgSource(configData, versionStampFactory));

        public CfgBuilder Add(
            Func<IEnumerable<KeyValuePair<string, string>>> dataFactory,
            Func<object?>? versionStampFactory = null
        ) => builder.AddSource(new DictionaryCfgSource(dataFactory, versionStampFactory));
    }
}
