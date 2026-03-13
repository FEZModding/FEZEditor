using FezEditor.Structure;
using FezEditor.Tools;
using FEZRepacker.Core.Definitions.Game.Common;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PrimitiveType = Microsoft.Xna.Framework.Graphics.PrimitiveType;

namespace FezEditor.Actors;

public class SkyBackgroundMesh : SkyBaseMesh
{
    private static readonly SamplerState LinearUWrapVClamp = new()
    {
        Filter = TextureFilter.Linear,
        AddressU = TextureAddressMode.Wrap,
        AddressV = TextureAddressMode.Clamp
    };

    internal Texture2D? Texture { get; private set; }

    private readonly Rid _mesh;

    private readonly Rid _material;

    private Matrix _textureMatrix = Matrix.Identity;

    internal SkyBackgroundMesh(Game game, Actor actor) : base(game, actor)
    {
        _mesh = _rendering.MeshCreate();
        _material = _rendering.MaterialCreate();
        _rendering.InstanceSetMesh(actor.InstanceRid, _mesh);
    }

    public override void Dispose()
    {
        GC.SuppressFinalize(this);
        Texture?.Dispose();
        _rendering.FreeRid(_mesh);
        _rendering.FreeRid(_material);
    }

    public override void LoadContent(IContentManager content)
    {
        var effect = content.Load<Effect>("Effects/SkyBackgroundMesh");
        _rendering.MaterialAssignEffect(_material, effect);
    }

    public void Visualize(string name, string background)
    {
        var texture = (RTexture2D)_resources.Load($"Skies/{name}/{background}");
        Texture?.Dispose();
        Texture = RepackerExtensions.ConvertToTexture2D(texture);

        _rendering.MaterialAssignBaseTexture(_material, Texture);
        _rendering.MaterialSetBlendMode(_material, BlendMode.Screen);
        _rendering.MaterialSetDepthWrite(_material, false);
        _rendering.MaterialSetDepthTest(_material, CompareFunction.Always);
        _rendering.MaterialSetSamplerState(_material, LinearUWrapVClamp);
        _rendering.MaterialSetCullMode(_material, CullMode.None);
        _rendering.MaterialSetAlbedo(_material, Color.White);
        _rendering.MaterialSetStencilWrite(_material, false);

        var surface = MeshSurface.CreateFaceQuad(Vector3.One * 2f, FaceOrientation.Front);
        _rendering.MeshClear(_mesh);
        _rendering.MeshAddSurface(_mesh, PrimitiveType.TriangleList, surface, _material);
    }

    public override void Update(GameTime gameTime)
    {
        _textureMatrix.M11 = 0.0001f;
        _textureMatrix.M31 = Sky.Clock.DayFraction;
        _rendering.MaterialSetTextureTransform(_material, _textureMatrix);
    }
}