using System.Text.Json;
using System.Text.Json.Nodes;

namespace Exp02_BindingAndActions.Surfaces;

/// <summary>
/// A surface's data model: a mutable JSON object addressed by JSON Pointer
/// (RFC 6901), plus a change notification so bound controls can react.
///
/// This is the piece that makes a surface *live* rather than static. Both
/// directions go through it — the stream writes with <see cref="Write"/>, the UI
/// writes with <see cref="WriteString"/> — which is what keeps the model the
/// single source of truth rather than the controls.
/// </summary>
internal sealed class DataModel
{
    private static readonly JsonElement JsonNull = JsonSerializer.Deserialize<JsonElement>("null");

    private JsonObject _root = [];

    /// <summary>
    /// Raised with the pointer that was written. The root is reported as the
    /// empty string, which <see cref="Affects"/> treats as "everything".
    /// </summary>
    public event Action<string>? Changed;

    /// <summary>
    /// Applies an <c>updateDataModel</c>. A null value removes the key, per the
    /// spec. Missing intermediate objects are created, so a producer can write a
    /// deep path without seeding its parents first.
    /// </summary>
    public void Write(string pointer, JsonElement? value)
    {
        var tokens = Parse(pointer);

        if (tokens.Length == 0)
        {
            if (value is not { ValueKind: JsonValueKind.Object } root)
            {
                throw new InvalidOperationException(
                    "A write to the data model root must supply a JSON object.");
            }

            _root = (JsonObject)JsonNode.Parse(root.GetRawText())!;
            Changed?.Invoke("");
            return;
        }

        var parent = _root;
        for (var i = 0; i < tokens.Length - 1; i++)
        {
            if (parent[tokens[i]] is JsonObject existing)
            {
                parent = existing;
            }
            else
            {
                var created = new JsonObject();
                parent[tokens[i]] = created;
                parent = created;
            }
        }

        var leaf = tokens[^1];
        if (value is { } present)
        {
            parent[leaf] = JsonNode.Parse(present.GetRawText());
        }
        else
        {
            parent.Remove(leaf);
        }

        Changed?.Invoke(Normalize(tokens));
    }

    /// <summary>
    /// Writes a string from the UI side — the write half of two-way binding.
    /// No-ops when the value is unchanged, which stops a control from being
    /// re-assigned the value it just produced (see <c>BindingResolver</c>).
    /// </summary>
    public void WriteString(string pointer, string value)
    {
        if (ReadString(pointer) == value)
        {
            return;
        }

        Write(pointer, JsonSerializer.SerializeToElement(value));
    }

    /// <summary>
    /// Reads a pointer as display text: JSON strings come back as themselves,
    /// other values as their JSON form, and a missing path as empty. Returning
    /// empty rather than throwing is the "degrade gracefully" rule — a binding to
    /// a path the producer never seeded should leave a blank control, not kill
    /// the surface.
    /// </summary>
    public string ReadString(string pointer) => Read(pointer) switch
    {
        null => string.Empty,
        JsonValue value when value.TryGetValue<string>(out var text) => text,
        var node => node.ToJsonString(),
    };

    /// <summary>Reads a pointer as a JSON value, for an action's context.</summary>
    public JsonElement ReadElement(string pointer) =>
        Read(pointer) is { } node ? node.Deserialize<JsonElement>() : JsonNull;

    /// <summary>
    /// Whether a write to <paramref name="changedPointer"/> affects something
    /// bound to <paramref name="boundPointer"/> — true for the path itself and
    /// for any ancestor of it, since seeding <c>/form</c> changes
    /// <c>/form/status</c> just as much as writing to it directly does.
    /// </summary>
    public static bool Affects(string changedPointer, string boundPointer) =>
        changedPointer.Length == 0
        || boundPointer == changedPointer
        || boundPointer.StartsWith(changedPointer + "/", StringComparison.Ordinal);

    private JsonNode? Read(string pointer)
    {
        JsonNode? node = _root;

        foreach (var token in Parse(pointer))
        {
            if (node is not JsonObject obj || !obj.TryGetPropertyValue(token, out node))
            {
                return null;
            }
        }

        return node;
    }

    /// <summary>
    /// Splits a JSON Pointer into its unescaped tokens.
    ///
    /// One deliberate divergence from RFC 6901: the pointer <c>"/"</c> is treated
    /// as the document root. Strictly it means "the member whose key is the empty
    /// string", but A2UI documents <c>path</c> as defaulting to <c>"/"</c> meaning
    /// the root, and the fixtures use it that way. Following the protocol here
    /// rather than the RFC.
    ///
    /// Array indices are not handled — nothing in this experiment iterates a
    /// collection, and relative pointers are out of scope with them.
    /// </summary>
    private static string[] Parse(string pointer)
    {
        if (pointer.Length == 0 || pointer == "/")
        {
            return [];
        }

        if (pointer[0] != '/')
        {
            throw new FormatException(
                $"'{pointer}' is not an absolute JSON Pointer — it must start with '/'.");
        }

        return [.. pointer[1..].Split('/').Select(Unescape)];
    }

    // Order matters: ~1 before ~0, or an escaped tilde would corrupt a slash.
    private static string Unescape(string token) =>
        token.Replace("~1", "/", StringComparison.Ordinal)
             .Replace("~0", "~", StringComparison.Ordinal);

    private static string Normalize(string[] tokens) =>
        "/" + string.Join('/', tokens.Select(Escape));

    private static string Escape(string token) =>
        token.Replace("~", "~0", StringComparison.Ordinal)
             .Replace("/", "~1", StringComparison.Ordinal);
}
