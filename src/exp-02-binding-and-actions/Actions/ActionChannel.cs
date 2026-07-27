using Exp02_BindingAndActions.Protocol;
using Exp02_BindingAndActions.Rendering;
using Exp02_BindingAndActions.Surfaces;

namespace Exp02_BindingAndActions.Actions;

/// <summary>
/// The return channel: turns a click on a component into the client → server
/// <c>action</c> message, and hands it to whoever is listening.
///
/// There is no transport here on purpose. The channel's job is to *build a
/// correct message*; whether that message then goes to a socket, an agent, or —
/// as in this experiment — a log pane and a canned responder, is the host's
/// choice. Splitting it that way is what lets experiment 04 replace the producer
/// without touching a line of this.
/// </summary>
internal sealed class ActionChannel(Surface surface, BindingResolver bindings)
{
    /// <summary>Raised with the fully-formed message, ready to serialize.</summary>
    public event Action<A2uiActionMessage>? ActionSent;

    public void Send(ActionDeclaration declaration, string sourceComponentId)
    {
        var message = new A2uiActionMessage
        {
            Action = new A2uiAction
            {
                Name = declaration.Name,
                SurfaceId = surface.SurfaceId,
                SourceComponentId = sourceComponentId,
                Timestamp = A2uiAction.Now(),

                // Resolved here, at click time — not when the tree was built.
                // That is the whole reason the message carries what the user
                // actually typed rather than the values the surface opened with.
                Context = bindings.ResolveContext(declaration.Context),
            },
        };

        ActionSent?.Invoke(message);
    }
}
