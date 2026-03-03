using FezEditor.Services;
using FezEditor.Structure;
using FezEditor.Tools;
using FEZRepacker.Core.Definitions.Game.Common;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace FezEditor.Actors;

public class TrixelsMesh : ActorComponent
{
    private static readonly Color SelectedColor = Color.Red with { A = 85 }; // 33%

    private static readonly Color HoveredColor = Color.White with { A = 85 }; // 53%

    public Texture2D? Texture { get; set; }

    public IReadOnlyList<TrixelFace> Faces => _faces;

    public bool Wireframe
    {
        get => _wireframe;
        set
        {
            if (_wireframe != value)
            {
                _wireframe = value;
                _rendering.MaterialSetFillMode(_material, _wireframe ? FillMode.WireFrame : FillMode.Solid);
                _rendering.MaterialSetCullMode(_material, _wireframe ? CullMode.None : CullMode.CullClockwiseFace);
            }
        }
    }

    private readonly RenderingService _rendering;

    private readonly Transform _transform;

    private readonly Rid _mesh;

    private readonly Rid _multiMesh;

    private readonly Rid _material;

    private bool _wireframe;

    private TrixelFace? _hoveredFace;

    private HashSet<TrixelFace> _selectedFaces = [];

    private Vector3 _objSize;

    private TrixelFace[] _faces = [];

    internal TrixelsMesh(Game game, Actor actor) : base(game, actor)
    {
        _rendering = game.GetService<RenderingService>();
        _transform = actor.GetComponent<Transform>();
        _mesh = _rendering.MeshCreate();
        _material = _rendering.MaterialCreate();
        _multiMesh = _rendering.MultiMeshCreate();
        _rendering.MultiMeshSetMesh(_multiMesh, _mesh);
        _rendering.InstanceSetMultiMesh(actor.InstanceRid, _multiMesh);
    }

    public override void LoadContent(IContentManager content)
    {
        var effect = content.Load<Effect>("Effects/TrixelsMesh");
        _rendering.MaterialAssignEffect(_material, effect);
        _rendering.MaterialSetFillMode(_material, FillMode.Solid);
        _rendering.MaterialSetCullMode(_material, CullMode.CullClockwiseFace);
        _rendering.MaterialShaderSetParam(_material, "Selected", SelectedColor);
        _rendering.MaterialShaderSetParam(_material, "Hovered", HoveredColor);

        var surface = MeshSurface.CreateQuad(Vector3.One);
        _rendering.MeshAddSurface(_mesh, PrimitiveType.TriangleList, surface, _material);
    }

    public void Visualize(TrixelObject obj)
    {
        _objSize = obj.Size;
        _faces = TrixelMaterializer.BuildVisibleFaces(obj).ToArray();
        _transform.Position = Vector3.Zero - (obj.Size / 2f);
        _rendering.MultiMeshAllocate(_multiMesh, _faces.Length, MultiMeshDataType.Matrix);
        _rendering.MaterialAssignBaseTexture(_material, Texture!);
        UploadInstances();
    }

    public void SetHoveredFace(TrixelFace? face)
    {
        _hoveredFace = face;
        if (_faces.Length > 0)
        {
            UploadInstances();
        }
    }

    public void SetSelectedFaces(HashSet<TrixelFace> faces)
    {
        _selectedFaces = faces;
        if (_faces.Length > 0)
        {
            UploadInstances();
        }
    }

    private void UploadInstances()
    {
        for (var i = 0; i < _faces.Length; i++)
        {
            var emplacement = _faces[i].Emplacement;
            var face = _faces[i].Face;

            var isHovered = _hoveredFace.HasValue && _faces[i] == _hoveredFace.Value;
            var isSelected = _selectedFaces.Contains(_faces[i]);

            var worldPos = (emplacement.ToVector3() + ((Vector3.One + face.AsVector()) * 0.5f)) * Mathz.TrixelSize;
            var quaternion = face.AsQuaternion();
            var (colStart, uAxis, vAxis, flipU, flipV) = face switch
            {
                FaceOrientation.Front => (0 / 6f, Vector3.UnitX, Vector3.UnitY, false, true),
                FaceOrientation.Right => (1 / 6f, Vector3.UnitZ, Vector3.UnitY, true, true),
                FaceOrientation.Back => (2 / 6f, Vector3.UnitX, Vector3.UnitY, true, true),
                FaceOrientation.Left => (3 / 6f, Vector3.UnitZ, Vector3.UnitY, false, true),
                FaceOrientation.Top => (4 / 6f, Vector3.UnitX, Vector3.UnitZ, false, false),
                FaceOrientation.Down => (5 / 6f, Vector3.UnitX, Vector3.UnitZ, false, true),
                _ => throw new InvalidOperationException()
            };

            var uSize = (int)((uAxis.X != 0 ? _objSize.X : uAxis.Z != 0 ? _objSize.Z : _objSize.Y) / Mathz.TrixelSize);
            var vSize = (int)((vAxis.Y != 0 ? _objSize.Y : vAxis.Z != 0 ? _objSize.Z : _objSize.X) / Mathz.TrixelSize);

            var uIndex = (emplacement.X * uAxis.X) + (emplacement.Y * uAxis.Y) + (emplacement.Z * uAxis.Z);
            var vIndex = (emplacement.X * vAxis.X) + (emplacement.Y * vAxis.Y) + (emplacement.Z * vAxis.Z);

            if (flipU)
            {
                uIndex = uSize - 1 - uIndex;
            }

            if (flipV)
            {
                vIndex = vSize - 1 - vIndex;
            }

            var uStep = 1f / (6f * uSize);
            var vStep = 1f / vSize;

            var u0 = colStart + (uIndex * uStep);
            var v0 = vIndex * vStep;

            var uv0 = new Vector2(u0, v0 + vStep);
            var uv1 = new Vector2(u0 + uStep, v0 + vStep);
            var uv2 = new Vector2(u0, v0);
            var uv3 = new Vector2(u0 + uStep, v0);

            var w = (int)face + (isHovered ? 10f : 0f) + (isSelected ? 20f : 0f);
            var data = new Matrix(
                worldPos.X, worldPos.Y, worldPos.Z, w,
                quaternion.X, quaternion.Y, quaternion.Z, quaternion.W,
                uv0.X, uv0.Y, uv1.X, uv1.Y,
                uv2.X, uv2.Y, uv3.X, uv3.Y
            );

            _rendering.MultiMeshSetInstanceMatrix(_multiMesh, i, data);
        }
    }

    public override void Dispose()
    {
        GC.SuppressFinalize(this);
        _rendering.FreeRid(_multiMesh);
        _rendering.FreeRid(_mesh);
        _rendering.FreeRid(_material);
        Texture?.Dispose();
    }
}