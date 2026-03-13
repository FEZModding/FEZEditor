using FezEditor.Structure;
using FezEditor.Tools;
using FEZRepacker.Core.Definitions.Game.Common;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace FezEditor.Actors;

public class SkyStarsMesh : SkyBaseMesh
{
    internal Texture2D? Texture { get; private set; }

    private readonly Dictionary<FaceOrientation, SideInstance> _sides = new();

    private Func<float>? _computeOpacity;

    internal SkyStarsMesh(Game game, Actor actor) : base(game, actor)
    {
        var i = 0;
        foreach (var face in Enum.GetValues<FaceOrientation>().Where(fo => fo.IsSide()))
        {
            var instance = _rendering.InstanceCreate(Actor.InstanceRid);
            var mesh = _rendering.MeshCreate();
            var material = _rendering.MaterialCreate();
            _sides[face] = new SideInstance(instance, mesh, material, i++ / 4f);
        }
    }

    public override void Dispose()
    {
        GC.SuppressFinalize(this);
        foreach (var side in _sides.Values)
        {
            _rendering.FreeRid(side.Material);
            _rendering.FreeRid(side.Mesh);
            _rendering.FreeRid(side.Instance);
        }

        _sides.Clear();
        Texture?.Dispose();
    }

    public override void LoadContent(IContentManager content)
    {
        var effect = content.Load<Effect>("Effects/SkyStarsMesh");
        foreach (var instance in _sides.Values)
        {
            _rendering.MaterialAssignEffect(instance.Material, effect);
        }
    }

    public void Visualize(string sky, string stars, bool rainy)
    {
        Texture?.Dispose();
        if (string.IsNullOrEmpty(stars))
        {
            return;
        }

        var texture = (RTexture2D)_resources.Load($"Skies/{sky}/{stars}");
        Texture = RepackerExtensions.ConvertToTexture2D(texture);

        foreach (var (face, side) in _sides)
        {
            _rendering.MaterialAssignBaseTexture(side.Material, Texture);
            _rendering.MaterialSetBlendMode(side.Material, BlendMode.StarsOverClouds);
            _rendering.MaterialSetDepthWrite(side.Material, false);
            _rendering.MaterialSetDepthTest(side.Material, CompareFunction.LessEqual);
            _rendering.MaterialSetCullMode(side.Material, CullMode.None);
            _rendering.MaterialSetSamplerState(side.Material, SamplerState.PointWrap);
            _rendering.MaterialSetStencilWrite(side.Material, false);

            var surface = MeshSurface.CreateFaceQuad(Vector3.One, face);
            _rendering.MeshClear(side.Mesh);
            _rendering.MeshAddSurface(side.Mesh, PrimitiveType.TriangleList, surface, side.Material);
            _rendering.InstanceSetMesh(side.Instance, side.Mesh);
        }

        if (rainy || sky == "PYRAMID_SKY" || sky == "ABOVE")
        {
            _computeOpacity = () => 1f;
        }
        else if (sky == "OBS_SKY")
        {
            _computeOpacity = () => MathHelper.Lerp(Sky!.Clock.NightContribution, 1f, 0.25f);
        }
        else
        {
            _computeOpacity = () => Sky!.Clock.NightContribution;
        }
    }

    public override void Update(GameTime gameTime)
    {
        var opacity = _computeOpacity?.Invoke() ?? 0f;
        if (Texture == null || opacity == 0f)
        {
            Actor.Visible = false;
            return;
        }

        var orthogonal = Sky.Camera.Projection == Camera.ProjectionType.Orthographic;

        Vector3 scale;
        if (orthogonal)
        {
            scale = new Vector3(Sky.Camera.Size * 2f + ExtraScale) * BaseScale;
            Actor.Transform.Position = Sky.Camera.Position;
        }
        else
        {
            scale = Sky.LevelSize + new Vector3(ExtraScale);
            Actor.Transform.Position = Sky.LevelSize / 2f;
        }

        Actor.Visible = true;
        Actor.Transform.Scale = scale;

        var albedo = new Color(1f, 1f, 1f, opacity);
        var scaleU = scale.X / (Texture.Width * Mathz.TrixelSize);
        var scaleV = scale.Y / (Texture.Height * Mathz.TrixelSize);

        foreach (var side in _sides.Values)
        {
            var textureMatrix = new Matrix(
                scaleU, 0, 0, 0,
                0, scaleV, 0, 0,
                side.Offset - scaleU / 2f, side.Offset - scaleV / 2, 1, 0,
                0, 0, 0, 1
            );

            _rendering.MaterialSetAlbedo(side.Material, albedo);
            _rendering.MaterialSetTextureTransform(side.Material, textureMatrix);
        }
    }

    private readonly record struct SideInstance(Rid Instance, Rid Mesh, Rid Material, float Offset);
}