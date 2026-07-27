using System.Text.Json;

namespace Exp02_BindingAndActions.Protocol;

/// <summary>
/// The client → server envelope. The only message that travels *up* the stream,
/// and deliberately shaped like the inbound ones so the direction is the only
/// difference: <c>{"version": …, "action": {…}}</c>.
/// </summary>
internal sealed record A2uiActionMessage
{
    private static readonly JsonSerializerOptions Options = new()
    {
        // Property names only — dictionary keys are left alone, so the context
        // keys stay exactly as the producer declared them.
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public string Version { get; init; } = "v0.9";

    public required A2uiAction Action { get; init; }

    /// <summary>
    /// The wire form. There is no transport in this experiment, so this is what
    /// gets written to the log pane instead of to a socket — the message is real
    /// even though the wire is not.
    /// </summary>
    public string ToJson() => JsonSerializer.Serialize(this, Options);
}

/// <summary>A user interaction, with the declared context resolved to values.</summary>
internal sealed record A2uiAction
{
    public required string Name { get; init; }

    public required string SurfaceId { get; init; }

    public required string SourceComponentId { get; init; }

    /// <summary>
    /// ISO 8601 UTC. Kept as a string rather than a <see cref="DateTimeOffset"/>
    /// so the serialized form is exactly the <c>…Z</c> the spec shows, instead of
    /// the <c>+00:00</c> offset System.Text.Json would emit.
    /// </summary>
    public required string Timestamp { get; init; }

    /// <summary>
    /// The declared context with every <c>{path}</c> already resolved against the
    /// data model. Note what is *absent*: the full data model. The spec sends
    /// that only when <c>createSurface</c> sets <c>sendDataModel</c>, and this
    /// fixture does not — so the producer sees exactly what the component asked
    /// for, and nothing else.
    /// </summary>
    public required IReadOnlyDictionary<string, JsonElement> Context { get; init; }

    public static string Now() =>
        DateTimeOffset.UtcNow.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'");
}
