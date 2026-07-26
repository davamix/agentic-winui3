namespace Exp01_StaticRender.Surfaces;

/// <summary>
/// Owns the surfaces the stream has opened. Experiment 01 only ever has one, but
/// the protocol keys every message by surface id, so the lookup is modelled from
/// the start.
/// </summary>
internal sealed class SurfaceManager
{
    private readonly Dictionary<string, Surface> _surfaces = new(StringComparer.Ordinal);

    public Surface Create(string surfaceId, string? catalogId)
    {
        var surface = new Surface(surfaceId, catalogId);
        _surfaces[surfaceId] = surface;
        return surface;
    }

    public Surface Get(string surfaceId) =>
        _surfaces.TryGetValue(surfaceId, out var surface)
            ? surface
            : throw new InvalidOperationException(
                $"Surface '{surfaceId}' was referenced before createSurface opened it.");
}
