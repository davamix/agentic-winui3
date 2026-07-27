using System.Text.Json;
using System.Text.RegularExpressions;
using Exp02_BindingAndActions.Protocol;
using Exp02_BindingAndActions.Surfaces;

namespace Exp02_BindingAndActions.Actions;

/// <summary>
/// Stands in for the agent that experiment 04 will bring: maps an action name to
/// a canned A2UI stream and replays it.
///
/// The important structural point is that it replays through the *same*
/// <see cref="MessageDispatcher"/> the file stream uses. The response is not a
/// special case with a private path into the UI — it is ordinary A2UI arriving
/// after the surface was rendered. If that had needed its own route, the claim
/// "a live surface is just more of the same protocol" would be false.
/// </summary>
internal sealed partial class ScriptedResponder(
    MessageDispatcher dispatcher,
    IReadOnlyDictionary<string, string> scripts)
{
    /// <summary>Raised with a one-line summary for the log pane.</summary>
    public event Action<string>? Responded;

    public void OnAction(A2uiActionMessage message)
    {
        if (!scripts.TryGetValue(message.Action.Name, out var path))
        {
            Responded?.Invoke($"(no script for action '{message.Action.Name}')");
            return;
        }

        foreach (var line in File.ReadLines(path))
        {
            if (A2uiStreamReader.Parse(Substitute(line, message.Action.Context)) is not { } response)
            {
                continue;
            }

            Responded?.Invoke($"↩ replaying {Path.GetFileName(path)}");
            dispatcher.Dispatch(response);
        }
    }

    /// <summary>
    /// Replaces <c>${key}</c> with the action context's value for that key.
    ///
    /// This is <b>not part of A2UI</b>. A real producer composes its own strings;
    /// the placeholder exists only so the round trip is *evidential* — seeing your
    /// own typed name come back proves the context genuinely travelled, where a
    /// fixed "Submitted." would not.
    ///
    /// Substitution happens on the raw line, before parsing, so the replacement
    /// is JSON-escaped on the way in: a user who types a quote or a backslash must
    /// not be able to break the message they are about to receive. That is a
    /// miniature of the injection problem a real producer faces with any value
    /// that round-trips through a user.
    /// </summary>
    private static string Substitute(
        string line,
        IReadOnlyDictionary<string, JsonElement> context) =>
        PlaceholderPattern().Replace(line, match =>
            context.TryGetValue(match.Groups[1].Value, out var value)
                ? Escape(value)
                : match.Value);

    private static string Escape(JsonElement value)
    {
        var text = value.ValueKind == JsonValueKind.String
            ? value.GetString()!
            : value.GetRawText();

        // Serialize as a JSON string, then drop the surrounding quotes: the
        // placeholder already sits inside one.
        var quoted = JsonSerializer.Serialize(text);
        return quoted[1..^1];
    }

    [GeneratedRegex(@"\$\{(\w+)\}")]
    private static partial Regex PlaceholderPattern();
}
