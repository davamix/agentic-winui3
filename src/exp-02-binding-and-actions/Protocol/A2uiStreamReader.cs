using System.Text.Json;

namespace Exp02_BindingAndActions.Protocol;

/// <summary>
/// Reads an A2UI stream in JSON Lines form — one message per line. In this
/// experiment the "producer" is a file on disk; experiment 03 replaces it with a
/// live transport without the messages themselves changing.
/// </summary>
internal static class A2uiStreamReader
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    /// <summary>
    /// Yields one message per non-empty line. Lazy, so the same method works
    /// unchanged against an incremental source later.
    /// </summary>
    public static IEnumerable<A2uiMessage> Read(string path)
    {
        foreach (var line in File.ReadLines(path))
        {
            if (Parse(line) is { } message)
            {
                yield return message;
            }
        }
    }

    /// <summary>
    /// Parses a single line, or returns null for a blank one. Exposed separately
    /// because the <c>ScriptedResponder</c> rewrites a line's text before parsing
    /// it — the only caller that needs the two steps apart.
    /// </summary>
    public static A2uiMessage? Parse(string line) =>
        string.IsNullOrWhiteSpace(line)
            ? null
            : JsonSerializer.Deserialize<A2uiMessage>(line, Options);
}
