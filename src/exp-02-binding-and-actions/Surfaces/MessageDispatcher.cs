using Exp02_BindingAndActions.Protocol;

namespace Exp02_BindingAndActions.Surfaces;

/// <summary>
/// Routes each incoming message to the <see cref="SurfaceManager"/> and reports
/// what it did. Rendering is not performed here — the dispatcher only signals
/// that a surface is ready, so the host decides when and on which thread to build
/// controls.
/// </summary>
internal sealed class MessageDispatcher(SurfaceManager surfaces)
{
    /// <summary>Raised once per routed message, with a one-line summary for the log pane.</summary>
    public event Action<string>? MessageRouted;

    /// <summary>Raised when <c>beginRendering</c> declares a surface complete.</summary>
    public event Action<Surface>? RenderRequested;

    public void Dispatch(A2uiMessage message)
    {
        if (message.CreateSurface is { } create)
        {
            surfaces.Create(create.SurfaceId, create.CatalogId);
            MessageRouted?.Invoke(
                $"createSurface · {create.SurfaceId} · catalog {create.CatalogId ?? "(none)"}");
        }

        if (message.UpdateDataModel is { } model)
        {
            surfaces.Get(model.SurfaceId).Data.Write(model.Path, model.Value);
            MessageRouted?.Invoke($"updateDataModel · {model.SurfaceId} · {model.Path}");
        }

        if (message.UpdateComponents is { } update)
        {
            surfaces.Get(update.SurfaceId).Apply(update.Components);
            MessageRouted?.Invoke(
                $"updateComponents · {update.SurfaceId} · {update.Components.Count} components");
        }

        if (message.BeginRendering is { } begin)
        {
            var surface = surfaces.Get(begin.SurfaceId);
            MessageRouted?.Invoke($"beginRendering · {begin.SurfaceId}");
            RenderRequested?.Invoke(surface);
        }
    }
}
