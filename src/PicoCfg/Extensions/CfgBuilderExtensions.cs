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
        /// When <paramref name="versionStampFactory"/> is provided, equal consecutive stamps are treated
        /// as authoritative unchanged signals for future reloads after the first completed materialization.
        /// Once a stamp has been accepted, repeated equal values, including <see langword="null"/>, skip
        /// reopening the stream. A changed stamp triggers a reread, but the current snapshot may still be
        /// retained when the reparsed content is unchanged.
        /// </summary>
        public CfgBuilder Add(Func<Stream> streamFactory, Func<object?>? versionStampFactory = null) =>
            builder.AddSource(new StreamCfgSource(streamFactory, versionStampFactory));

        /// <summary>
        /// Adds inline text content as a stream-based source.
        /// The content is parsed as line-based <c>key=value</c> text.
        /// Each reload recreates a stream so inline text stays on the same parsing path as stream-based sources.
        /// When <paramref name="versionStampFactory"/> is provided, equal consecutive stamps are treated
        /// as authoritative unchanged signals for future reloads after the first completed materialization.
        /// Once a stamp has been accepted, repeated equal values, including <see langword="null"/>, skip
        /// reparsing the inline content. A changed stamp triggers reparsing, but the current snapshot may still
        /// be retained when the reparsed content is unchanged.
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
        /// When <paramref name="versionStampFactory"/> is provided, equal consecutive stamps are treated
        /// as authoritative unchanged signals for future reloads after the first completed materialization.
        /// Once a stamp has been accepted, repeated equal values, including <see langword="null"/>, skip
        /// rereading the dictionary. A changed stamp triggers reread, but the current snapshot may still be
        /// retained when the visible dictionary content is unchanged.
        /// </summary>
        public CfgBuilder Add(IDictionary<string, string> configData,
            Func<object?>? versionStampFactory = null
        ) => builder.AddSource(new DictionaryCfgSource(configData, versionStampFactory));

        /// <summary>
        /// Adds a dictionary-backed factory source.
        /// Each reload enumerates the current key/value pairs unless <paramref name="versionStampFactory"/>
        /// returns the same value as the previously accepted authoritative stamp.
        /// The first completed materialization establishes that baseline; later equal values, including
        /// repeated <see langword="null"/>, skip source work, while changed stamps force re-enumeration even
        /// if the current snapshot instance is ultimately retained.
        /// </summary>
        public CfgBuilder Add(
            Func<IEnumerable<KeyValuePair<string, string>>> dataFactory,
            Func<object?>? versionStampFactory = null
        ) => builder.AddSource(new DictionaryCfgSource(dataFactory, versionStampFactory));
    }
}
