using FezEditor.Services;
using FezEditor.Structure;
using FezEditor.Tools;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace FezEditor.Actors;

public class Grid : ActorComponent
{
    public GridPlane Plane { get; set; } = GridPlane.Z;

    public Color PrimaryColor { get; set; } = new(0.5f, 0.5f, 0.5f);

    public Color SecondaryColor { get; set; } = new(0.3f, 0.3f, 0.3f, 0.8f);

    public float CellSize { get; set; } = 1f;

    public int NumberOfCells { get; set; } = 1000;

    public int PrimarySteps { get; set; } = 10;

    public int SecondaryStep { get; set; } = 1;

    private readonly RenderingService _rendering;

    private readonly Rid _primaryMaterial;

    private readonly Rid _secondaryMaterial;

    private readonly GridPlaneData[] _planes = new GridPlaneData[3];

    internal Grid(Game game, Actor actor) : base(game, actor)
    {
        _rendering = game.GetService<RenderingService>();
        _primaryMaterial = _rendering.MaterialCreate();
        _secondaryMaterial = _rendering.MaterialCreate();

        for (var i = 0; i < 3; i++)
        {
            var mesh = _rendering.MeshCreate();
            var instance = _rendering.InstanceCreate(actor.InstanceRid);
            _rendering.InstanceSetMesh(instance, mesh);
            _planes[i] = new GridPlaneData(instance, mesh);
        }

        _rendering.InstanceSetRotation(_planes[1].Instance,
            Quaternion.CreateFromAxisAngle(Vector3.UnitX, MathHelper.PiOver2));
        _rendering.InstanceSetRotation(_planes[2].Instance,
            Quaternion.CreateFromAxisAngle(Vector3.UnitZ, MathHelper.PiOver2));
    }

    public override void LoadContent(IContentManager content)
    {
        var effect = content.Load<Effect>("Effects/Grid");
        _rendering.MaterialAssignEffect(_primaryMaterial, effect);
        _rendering.MaterialAssignEffect(_secondaryMaterial, effect);

        GenerateGridMesh(_planes[0].Mesh, GridPlane.X);
        GenerateGridMesh(_planes[1].Mesh, GridPlane.Y);
        GenerateGridMesh(_planes[2].Mesh, GridPlane.Z);
    }

    public override void Dispose()
    {
        GC.SuppressFinalize(this);
        foreach (var plane in _planes)
        {
            _rendering.FreeRid(plane.Instance);
            _rendering.FreeRid(plane.Mesh);
        }

        _rendering.FreeRid(_primaryMaterial);
        _rendering.FreeRid(_secondaryMaterial);
    }

    public override void Update(GameTime gameTime)
    {
        _rendering.MaterialSetAlbedo(_primaryMaterial, PrimaryColor);
        _rendering.MaterialSetAlbedo(_secondaryMaterial, SecondaryColor);

        for (var i = 0; i < 3; i++)
        {
            _rendering.InstanceSetVisibility(_planes[i].Instance, Enabled && i == (int)Plane);
        }
    }

    private void GenerateGridMesh(Rid meshRid, GridPlane plane)
    {
        _rendering.MeshClear(meshRid);

        // Generate two surfaces: primary (bold) and secondary (faint) grid lines
        var primarySurface = CreateGridSurface(plane, PrimarySteps);
        _rendering.MeshAddSurface(meshRid, PrimitiveType.LineList, primarySurface, _primaryMaterial);

        var secondarySurface = CreateGridSurface(plane, SecondaryStep);
        _rendering.MeshAddSurface(meshRid, PrimitiveType.LineList, secondarySurface, _secondaryMaterial);
    }

    private MeshSurface CreateGridSurface(GridPlane plane, int stepInCells)
    {
        var vertices = new List<Vector3>();
        var indices = new List<int>();
        var colors = new List<Color>();

        var worldExtent = NumberOfCells * CellSize;
        var worldStep = stepInCells * CellSize;
        var vertexIndex = 0;

        switch (plane)
        {
            case GridPlane.X:
                for (var z = -worldExtent; z <= worldExtent; z += worldStep)
                {
                    vertices.Add(new Vector3(-worldExtent, 0, z));
                    vertices.Add(new Vector3(worldExtent, 0, z));
                    colors.Add(Color.White);
                    colors.Add(Color.White);
                    indices.Add(vertexIndex++);
                    indices.Add(vertexIndex++);
                }

                for (var x = -worldExtent; x <= worldExtent; x += worldStep)
                {
                    vertices.Add(new Vector3(x, 0, -worldExtent));
                    vertices.Add(new Vector3(x, 0, worldExtent));
                    colors.Add(Color.White);
                    colors.Add(Color.White);
                    indices.Add(vertexIndex++);
                    indices.Add(vertexIndex++);
                }

                break;

            case GridPlane.Y:
                for (var y = -worldExtent; y <= worldExtent; y += worldStep)
                {
                    vertices.Add(new Vector3(-worldExtent, y, 0));
                    vertices.Add(new Vector3(worldExtent, y, 0));
                    colors.Add(Color.White);
                    colors.Add(Color.White);
                    indices.Add(vertexIndex++);
                    indices.Add(vertexIndex++);
                }

                for (var x = -worldExtent; x <= worldExtent; x += worldStep)
                {
                    vertices.Add(new Vector3(x, -worldExtent, 0));
                    vertices.Add(new Vector3(x, worldExtent, 0));
                    colors.Add(Color.White);
                    colors.Add(Color.White);
                    indices.Add(vertexIndex++);
                    indices.Add(vertexIndex++);
                }

                break;

            case GridPlane.Z:
                for (var z = -worldExtent; z <= worldExtent; z += worldStep)
                {
                    vertices.Add(new Vector3(0, -worldExtent, z));
                    vertices.Add(new Vector3(0, worldExtent, z));
                    colors.Add(Color.White);
                    colors.Add(Color.White);
                    indices.Add(vertexIndex++);
                    indices.Add(vertexIndex++);
                }

                for (var y = -worldExtent; y <= worldExtent; y += worldStep)
                {
                    vertices.Add(new Vector3(0, y, -worldExtent));
                    vertices.Add(new Vector3(0, y, worldExtent));
                    colors.Add(Color.White);
                    colors.Add(Color.White);
                    indices.Add(vertexIndex++);
                    indices.Add(vertexIndex++);
                }

                break;

            default:
                throw new ArgumentOutOfRangeException(nameof(plane));
        }

        return new MeshSurface
        {
            Vertices = vertices.ToArray(),
            Indices = indices.ToArray(),
            Colors = colors.ToArray()
        };
    }

    private readonly record struct GridPlaneData(Rid Instance, Rid Mesh);
}