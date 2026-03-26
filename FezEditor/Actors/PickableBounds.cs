using FezEditor.Services;
using FezEditor.Structure;
using FezEditor.Tools;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace FezEditor.Actors;

public class PickableBounds : ActorComponent
{
    public Color WireColor { get; set; } = Color.Green;

    private readonly RenderingService _rendering;

    private readonly Rid _mesh;

    private readonly Rid _material;

    public PickableBounds(Game game, Actor actor) : base(game, actor)
    {
        _rendering = game.GetService<RenderingService>();
        _mesh = _rendering.MeshCreate();
        _material = _rendering.MaterialCreate();
        _rendering.InstanceSetMesh(actor.InstanceRid, _mesh);
    }

    public override void Dispose()
    {
        GC.SuppressFinalize(this);
        _rendering.FreeRid(_material);
        _rendering.FreeRid(_mesh);
    }

    public override void LoadContent(IContentManager content)
    {
        _rendering.MaterialAssignEffect(_material, _rendering.BasicEffectVertexColor);
        _rendering.MaterialSetCullMode(_material, CullMode.None);
    }

    public void Visualize(IEnumerable<Actor> actors)
    {
        var boxes = new List<BoundingBox>();
        foreach (var actor in actors)
        {
            var pickable = actor.FindComponent<IPickable>();
            if (pickable != null)
            {
                boxes.AddRange(pickable.GetBounds());
            }
        }

        _rendering.MeshClear(_mesh);
        if (boxes.Count == 0)
        {
            return;
        }

        var surface = new MeshSurface
        {
            Vertices = new Vector3[boxes.Count * 8],
            Colors = new Color[boxes.Count * 8],
            Indices = new int[boxes.Count * 24]
        };

        for (var b = 0; b < boxes.Count; b++)
        {
            var box = boxes[b];
            var min = box.Min;
            var max = box.Max;
            var v = b * 8;

            surface.Vertices[v + 0] = new Vector3(min.X, min.Y, min.Z);
            surface.Vertices[v + 1] = new Vector3(max.X, min.Y, min.Z);
            surface.Vertices[v + 2] = new Vector3(max.X, max.Y, min.Z);
            surface.Vertices[v + 3] = new Vector3(min.X, max.Y, min.Z);
            surface.Vertices[v + 4] = new Vector3(min.X, min.Y, max.Z);
            surface.Vertices[v + 5] = new Vector3(max.X, min.Y, max.Z);
            surface.Vertices[v + 6] = new Vector3(max.X, max.Y, max.Z);
            surface.Vertices[v + 7] = new Vector3(min.X, max.Y, max.Z);

            for (var i = 0; i < 8; i++)
            {
                surface.Colors[v + i] = WireColor;
            }

            var idx = b * 24;
            surface.Indices[idx + 0] = v + 0;
            surface.Indices[idx + 1] = v + 1;
            surface.Indices[idx + 2] = v + 1;
            surface.Indices[idx + 3] = v + 2;
            surface.Indices[idx + 4] = v + 2;
            surface.Indices[idx + 5] = v + 3;
            surface.Indices[idx + 6] = v + 3;
            surface.Indices[idx + 7] = v + 0;
            surface.Indices[idx + 8] = v + 4;
            surface.Indices[idx + 9] = v + 5;
            surface.Indices[idx + 10] = v + 5;
            surface.Indices[idx + 11] = v + 6;
            surface.Indices[idx + 12] = v + 6;
            surface.Indices[idx + 13] = v + 7;
            surface.Indices[idx + 14] = v + 7;
            surface.Indices[idx + 15] = v + 4;
            surface.Indices[idx + 16] = v + 0;
            surface.Indices[idx + 17] = v + 4;
            surface.Indices[idx + 18] = v + 1;
            surface.Indices[idx + 19] = v + 5;
            surface.Indices[idx + 20] = v + 2;
            surface.Indices[idx + 21] = v + 6;
            surface.Indices[idx + 22] = v + 3;
            surface.Indices[idx + 23] = v + 7;
        }

        _rendering.MeshAddSurface(_mesh, PrimitiveType.LineList, surface, _material);
    }
}