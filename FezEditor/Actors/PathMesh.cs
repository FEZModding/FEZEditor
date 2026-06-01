using FezEditor.Services;
using FezEditor.Structure;
using FezEditor.Tools;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace FezEditor.Actors;

public class PathMesh : ActorComponent, IPickable
{
    private static readonly Vector3 WaypointBoxSize = new(0.25f);

    public List<Vector3> Waypoints { get; set; } = new();

    public List<Color> WaypointColors { get; set; } = new();

    public bool Pickable { get; set; } = true;

    private readonly RenderingService _rendering;

    private readonly Rid _mesh;

    private Rid _material;

    internal PathMesh(Game game, Actor actor) : base(game, actor)
    {
        _rendering = game.GetService<RenderingService>();
        _mesh = _rendering.MeshCreate();
        _rendering.InstanceSetMesh(actor.InstanceRid, _mesh);
    }

    public override void LoadContent(IContentManager content)
    {
        _material = _rendering.MaterialCreate();
        _rendering.MaterialAssignEffect(_material, _rendering.BasicEffectVertexColor);
        _rendering.MaterialSetCullMode(_material, CullMode.None);
    }

    public override void Update(GameTime gameTime)
    {
        _rendering.MeshClear(_mesh);
        if (Waypoints.Count >= 2)
        {
            var surface = new MeshSurface
            {
                Vertices = new Vector3[Waypoints.Count],
                Colors = new Color[Waypoints.Count],
                Indices = new int[(Waypoints.Count - 1) * 2]
            };

            for (var i = 0; i < Waypoints.Count; i++)
            {
                surface.Vertices[i] = Waypoints[i];
                surface.Colors[i] = WaypointColors[i];
            }

            for (var i = 0; i < Waypoints.Count - 1; i++)
            {
                surface.Indices[i * 2] = i;
                surface.Indices[(i * 2) + 1] = i + 1;
            }

            _rendering.MeshAddSurface(_mesh, PrimitiveType.LineList, surface, _material);
        }

        for (var i = 0; i < Waypoints.Count; i++)
        {
            var box = MeshSurface.CreateWireframeBox(WaypointBoxSize, WaypointColors[i]);
            for (var v = 0; v < box.Vertices.Length; v++)
            {
                box.Vertices[v] += Waypoints[i];
            }

            _rendering.MeshAddSurface(_mesh, PrimitiveType.LineList, box, _material);
        }
    }

    public IEnumerable<BoundingBox> GetBounds()
    {
        var half = WaypointBoxSize / 2f;
        foreach (var waypoint in Waypoints)
        {
            yield return new BoundingBox(waypoint - half, waypoint + half);
        }
    }

    public PickHit? Pick(Ray ray)
    {
        float? nearest = null;
        var nearestIdx = 0;
        var idx = 0;

        foreach (var box in GetBounds())
        {
            var dist = ray.Intersects(box);
            if (dist.HasValue && (!nearest.HasValue || dist.Value < nearest.Value))
            {
                nearest = dist.Value;
                nearestIdx = idx;
            }

            idx++;
        }

        return nearest.HasValue ? new PickHit(nearest.Value, nearestIdx) : null;
    }

    public override void Dispose()
    {
        GC.SuppressFinalize(this);
        _rendering.FreeRid(_material);
        _rendering.FreeRid(_mesh);
    }
}