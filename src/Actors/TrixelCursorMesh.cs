using FezEditor.Services;
using FezEditor.Structure;
using FezEditor.Tools;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace FezEditor.Actors;

public sealed class TrixelCursorMesh : ActorComponent
{
    private const float CursorDepthBias = -0.001f;

    private readonly RenderingService _rendering;

    private readonly Rid _mesh;

    private readonly Rid _material;

    private readonly Rid _multiMesh;

    private readonly Rid _instance;

    private readonly List<TrixelFace> _faces = new();

    private Vector3 _offset;

    private bool _dirty;

    internal TrixelCursorMesh(Game game, Actor actor) : base(game, actor)
    {
        _rendering = game.GetService<RenderingService>();

        _mesh = _rendering.MeshCreate();
        _material = _rendering.MaterialCreate();
        _multiMesh = _rendering.MultiMeshCreate();
        _rendering.MultiMeshSetMesh(_multiMesh, _mesh);
        _instance = _rendering.InstanceCreate(actor.InstanceRid);
        _rendering.InstanceSetMultiMesh(_instance, _multiMesh);
        _rendering.InstanceSetVisibility(_instance, false);
    }

    public override void LoadContent(IContentManager content)
    {
        var effect = content.Load<Effect>("Effects/TrixelCursorFaceMesh");
        _rendering.MaterialAssignEffect(_material, effect);
        _rendering.MaterialSetCullMode(_material, CullMode.None);
        _rendering.MaterialSetBlendMode(_material, BlendMode.AlphaBlend);
        _rendering.MaterialSetDepthBias(_material, CursorDepthBias, 0.0f);

        var quad = MeshSurface.CreateQuad(Vector3.One);
        _rendering.MeshAddSurface(_mesh, PrimitiveType.TriangleList, quad, _material);
    }

    public void SetFaces(IEnumerable<TrixelFace> faces, Vector3 offset, Color color)
    {
        _faces.Clear();
        _faces.AddRange(faces);
        _offset = offset;
        _rendering.MaterialSetAlbedo(_material, color);
        _dirty = true;
    }

    public void Clear()
    {
        if (_faces.Count == 0)
        {
            return;
        }

        _faces.Clear();
        _dirty = true;
    }

    public override void Update(GameTime gameTime)
    {
        if (!_dirty)
        {
            return;
        }

        _dirty = false;
        _rendering.MultiMeshAllocate(_multiMesh, _faces.Count, MultiMeshDataType.Matrix);

        for (var i = 0; i < _faces.Count; i++)
        {
            var face = _faces[i];
            var trixelCenter = face.Emplacement.ToVector3() + (Vector3.One + face.Face.AsVector()) * 0.5f;
            var faceCenter = trixelCenter * Mathz.TrixelSize - _offset;
            var rotation = face.Face.AsQuaternion();

            var matrix = new Matrix(
                faceCenter.X, faceCenter.Y, faceCenter.Z, 0f,
                rotation.X, rotation.Y, rotation.Z, rotation.W,
                Mathz.TrixelSize, Mathz.TrixelSize, Mathz.TrixelSize, 0f,
                0f, 0f, 0f, 0f
            );
            _rendering.MultiMeshSetInstanceMatrix(_multiMesh, i, matrix);
        }

        _rendering.InstanceSetVisibility(_instance, _faces.Count > 0);
    }

    public override void Dispose()
    {
        _rendering.FreeRid(_instance);
        _rendering.FreeRid(_multiMesh);
        _rendering.FreeRid(_mesh);
        _rendering.FreeRid(_material);
    }
}
