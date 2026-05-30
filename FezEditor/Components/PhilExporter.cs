using FezEditor.Services;
using FezEditor.Tools;
using FEZRepacker.Core.Definitions.Game.ArtObject;
using FEZRepacker.Core.Definitions.Game.Common;
using FEZRepacker.Core.Definitions.Game.Graphics;
using FEZRepacker.Core.Definitions.Game.Level;
using FEZRepacker.Core.Definitions.Game.Sky;
using FEZRepacker.Core.Definitions.Game.TrileSet;
using ImGuiNET;
using Microsoft.Xna.Framework;
using Serilog;
using SharpGLTF.Geometry.VertexTypes;
using SharpGLTF.Materials;
using SharpGLTF.Schema2;
using SharpGLTF.Transforms;
using SharpGLTF.Validation;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using RgbaImage = SixLabors.ImageSharp.Image<SixLabors.ImageSharp.PixelFormats.Rgba32>;
using GltfAlphaMode = SharpGLTF.Materials.AlphaMode;

namespace FezEditor.Components;

public class PhilExporter : DrawableGameComponent
{
    private static readonly ILogger Logger = Logging.Create<PhilExporter>();

    private static readonly NVector3 TrileEmplacementOffset = new(0.5f);

    private const float SkyNoonDayFraction = 0.5f;

    private const int SkyFaceSize = 512;

    private const float CloudOpacity = 0.5f;

    private const int CloudDensity = 16;

    private readonly Level _level;

    private readonly string _outputPath;

    private readonly ResourceService _resources;

    private float _progress;

    private string _currentItem = "";

    private string _status = "";

    private State _state = State.Exporting;

    private State _previousState = State.Disposed;

    private string _popup = "";

    private CancellationTokenSource? _cts;

    private TimeSpan _disposeAfter = TimeSpan.Zero;

    public PhilExporter(Game game, Level level, string outputPath) : base(game)
    {
        _level = level;
        _outputPath = outputPath;
        _resources = game.GetService<ResourceService>();
        _ = ExportAsync();
    }

    public override void Update(GameTime gameTime)
    {
        if (_state == State.Disposed)
        {
            Game.RemoveComponent(this);
        }
    }

    public override void Draw(GameTime gameTime)
    {
        if (_state != _previousState)
        {
            _popup = _state switch
            {
                State.Exporting => "Diorama##Process",
                State.Complete => "Diorama##Complete",
                _ => _popup
            };
            ImGui.OpenPopup(_popup);
            _previousState = _state;
        }

        var isOpen = true;
        ImGuiX.SetNextWindowCentered(ImGuiCond.Always);
        if (ImGui.BeginPopupModal(_popup, ref isOpen, ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoCollapse))
        {
            switch (_state)
            {
                case State.Exporting:
                    {
                        ImGui.Text(_status);
                        ImGui.Text(_currentItem);
                        ImGuiX.ProgressBar(_progress, new Vector2(400, 0), $"{_progress * 100:F1}%");

                        if (ImGui.Button("Cancel"))
                        {
                            _cts?.Cancel();
                            ImGui.CloseCurrentPopup();
                        }

                        break;
                    }

                case State.Complete:
                    {
                        ImGuiX.TextColored(
                            _status.Contains("Error") ? Color.Red :
                            _status.Contains("complete") ? Color.Green :
                            Color.White,
                            _status);

                        _disposeAfter -= gameTime.ElapsedGameTime;
                        if (_disposeAfter < TimeSpan.Zero)
                        {
                            _state = State.Disposed;
                            ImGui.CloseCurrentPopup();
                        }

                        break;
                    }
            }

            ImGui.EndPopup();
        }

        if (!isOpen)
        {
            _state = State.Disposed;
        }
    }

    private async Task ExportAsync()
    {
        _cts = new CancellationTokenSource();
        _state = State.Exporting;
        _status = "Preparing export...";
        _progress = 0f;

        try
        {
            var ct = _cts.Token;
            await Task.Run(() => ExportInternal(ct), ct);
            _status = "Export complete!";
            _progress = 1.0f;
        }
        catch (OperationCanceledException)
        {
            _status = "Export cancelled";
        }
        catch (Exception ex)
        {
            _status = $"Error: {ex.Message}";
            Logger.Error(ex, "Diorama export failed");
        }
        finally
        {
            _state = State.Complete;
            _disposeAfter = TimeSpan.FromSeconds(3);
        }
    }

    private void ExportInternal(CancellationToken ct)
    {
        var triles = _level.Triles.Where(kv => kv.Value.TrileId != -1).ToDictionary();
        var artObjects = _level.ArtObjects.Where(kv => kv.Key != -1).ToDictionary();
        var bgPlanes = _level.BackgroundPlanes.Where(kv => kv.Key != -1).ToDictionary();
        var npcs = _level.NonPlayerCharacters.Where(kv => kv.Key != -1).ToDictionary();

        var sky = _resources.Load<Sky>($"Skies/{_level.SkyName}");
        var totalItems = triles.Count
                         + artObjects.Count
                         + bgPlanes.Count
                         + npcs.Count
                         + 1 // gomez
                         + 4; // sky faces
        var itemsDone = 0;

        void Advance(string label)
        {
            _currentItem = label;
            itemsDone++;
            _progress = (float)itemsDone / (totalItems + 1);
        }

        var levelModel = ModelRoot.CreateModel();
        var levelScene = levelModel.UseScene(_level.Name);

        var trilesNode = levelScene.CreateNode("Triles");
        var artObjectsNode = levelScene.CreateNode("ArtObjects");
        var bgPlanesNode = levelScene.CreateNode("BackgroundPlanes");
        var npcsNode = levelScene.CreateNode("NPCs");

        #region Triles

        {
            _status = "Exporting triles...";
            ct.ThrowIfCancellationRequested();

            var set = _resources.Load<TrileSet>($"Trile Sets/{_level.TrileSetName}");
            var meshes = new Dictionary<int, Mesh>();

            using (var atlasAlbedo = ExtractAlbedo(set.TextureAtlas))
            {
                var material = CreateMaterial(levelModel, atlasAlbedo, "TrileSet");

                foreach (var id in triles.Values.Select(ti => ti.TrileId).Distinct())
                {
                    ct.ThrowIfCancellationRequested();
                    if (!set.Triles.TryGetValue(id, out var trile) || trile.Geometry.Vertices.Length == 0)
                    {
                        continue;
                    }

                    var mesh = BuildMeshFromVertexInstances(levelModel, trile.Geometry, material,
                        reverseWinding: true);
                    if (mesh != null)
                    {
                        meshes[id] = mesh;
                    }
                }
            }

            foreach (var (emplacement, instance) in triles)
            {
                ct.ThrowIfCancellationRequested();
                if (!meshes.TryGetValue(instance.TrileId, out var mesh))
                {
                    continue;
                }

                var node = trilesNode.CreateNode($"Trile_{emplacement.X}_{emplacement.Y}_{emplacement.Z}");
                node.Mesh = mesh;

                var pos = instance.Position.ToNumerics() + TrileEmplacementOffset;
                var rot = NQuaternion.CreateFromAxisAngle(NVector3.UnitY, (instance.PhiLight - 2) * MathF.PI / 2f);
                node.LocalTransform = new AffineTransform(NVector3.One, rot, pos);

                Advance($"Trile {emplacement.X},{emplacement.Y},{emplacement.Z}");
            }
        }

        #endregion

        #region Art Objects

        {
            _status = "Exporting art objects...";

            var meshes = new Dictionary<string, Mesh>();
            foreach (var (id, instance) in artObjects)
            {
                ct.ThrowIfCancellationRequested();

                if (!meshes.TryGetValue(instance.Name, out var mesh))
                {
                    try
                    {
                        var ao = _resources.Load<ArtObject>($"Art Objects/{instance.Name}");
                        if (ao.Geometry.Vertices.Length > 0)
                        {
                            using var image = ExtractAlbedo(ao.Cubemap);
                            var material = CreateMaterial(levelModel, image, instance.Name);
                            mesh = BuildMeshFromVertexInstances(levelModel, ao.Geometry, material,
                                reverseWinding: true)!;
                            meshes[instance.Name] = mesh;
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.Warning(ex, "Skipping art object {0}", instance.Name);
                    }
                }

                if (mesh == null)
                {
                    continue;
                }

                var node = artObjectsNode.CreateNode($"{id}_{instance.Name}");
                node.Mesh = mesh;
                node.LocalTransform = new AffineTransform(
                    instance.Scale.ToNumerics(),
                    instance.Rotation.ToNumerics(),
                    instance.Position.ToNumerics());

                Advance($"AO {instance.Name}");
            }
        }

        #endregion

        #region Background Planes

        {
            _status = "Exporting background planes...";

            foreach (var (id, bgPlane) in bgPlanes)
            {
                ct.ThrowIfCancellationRequested();

                try
                {
                    var asset = _resources.Load<object>($"Background Planes/{bgPlane.TextureName}");
                    RgbaImage image;
                    NVector2 size;

                    if (asset is RAnimatedTexture anim)
                    {
                        image = CropFirstFrame(anim);
                        size = new NVector2(anim.FrameWidth, anim.FrameHeight) * Mathz.TrixelSize;
                    }
                    else if (asset is RTexture2D tex)
                    {
                        image = ImageFromTexture(tex);
                        size = new NVector2(tex.Width, tex.Height) * Mathz.TrixelSize;
                    }
                    else
                    {
                        Advance($"BG Plane {bgPlane.TextureName}");
                        continue;
                    }

                    using (image)
                    {
                        var material = CreateMaterial(levelModel, image, $"BGPlane_{id}", GltfAlphaMode.BLEND,
                            doubleSided: true);
                        var mesh = BuildQuadMesh(levelModel, material, size);

                        var node = bgPlanesNode.CreateNode($"{id}_{bgPlane.TextureName}");
                        node.Mesh = mesh;
                        var rotation = bgPlane.Rotation.ToNumerics();

                        // Push slightly along local +Z to avoid z-fighting with art objects
                        var normalOffset = NVector3.Transform(new NVector3(0f, 0f, 0.01f), rotation);
                        node.LocalTransform = new AffineTransform(
                            bgPlane.Scale.ToNumerics(),
                            rotation,
                            bgPlane.Position.ToNumerics() + normalOffset);
                    }
                }
                catch (Exception ex)
                {
                    Logger.Warning(ex, "Skipping background plane {0}: {1}", id, bgPlane.TextureName);
                }

                Advance($"BG Plane {bgPlane.TextureName}");
            }
        }

        #endregion

        #region NPCs + Gomez

        {
            _status = "Exporting NPCs...";

            foreach (var (id, instance) in npcs)
            {
                ct.ThrowIfCancellationRequested();
                ExportNpc(levelModel, npcsNode, instance.Name, instance.Position.X, instance.Position.Y,
                    instance.Position.Z, id.ToString());
                Advance($"NPC {instance.Name}");
            }

            ct.ThrowIfCancellationRequested();
            ExportNpc(levelModel, npcsNode, "Gomez",
                _level.StartingFace.Id.X,
                _level.StartingFace.Id.Y + 1f,
                _level.StartingFace.Id.Z,
                "Gomez", "IdleWink");
            Advance("Gomez");
        }

        #endregion

        #region Sky

        {
            _status = "Exporting sky...";
            ct.ThrowIfCancellationRequested();

            var levelSize = _level.Size.ToNumerics();
            var skyNode = levelScene.CreateNode("Sky");
            skyNode.LocalTransform = new AffineTransform(
                levelSize + new NVector3(1f),
                NQuaternion.Identity,
                levelSize / 2f);

            var sideFaces = Enum.GetValues<FaceOrientation>().Where(fo => fo.IsSide()).ToArray();
            for (var faceIndex = 0; faceIndex < sideFaces.Length; faceIndex++)
            {
                var face = sideFaces[faceIndex];
                ct.ThrowIfCancellationRequested();
                Advance($"Sky {face}");

                try
                {
                    using var faceImage = ComposeSkyFace(sky, faceIndex);
                    var material = CreateMaterial(levelModel, faceImage, $"Sky_{face}", doubleSided: false);
                    var mesh = BuildQuadMeshInward(levelModel, material, NVector2.One);

                    var faceRot = face.AsQuaternion().ToNumerics();
                    var faceNode = skyNode.CreateNode($"SkyFace_{face}");
                    faceNode.Mesh = mesh;
                    // Position face at +Z=0.5 in face-local space, then rotate into place
                    var facePosLocal = NVector3.Transform(new NVector3(0f, 0f, 0.5f), faceRot);
                    faceNode.LocalTransform = new AffineTransform(NVector3.One, faceRot, facePosLocal);
                }
                catch (Exception ex)
                {
                    Logger.Warning(ex, "Skipping sky face {0}", face);
                }
            }
        }

        #endregion

        #region Write GLB

        {
            _status = "Writing GLB...";
            ct.ThrowIfCancellationRequested();

            levelModel.Asset.Generator = "FEZEditor";
            levelModel.SaveGLB(_outputPath,
                new WriteSettings { Validation = ValidationMode.Skip, MergeBuffers = true });
            Logger.Information("GLB written to {0}", _outputPath);
        }

        #endregion
    }

    private void ExportNpc(ModelRoot model, Node parent, string name, float x, float y, float z,
        string nodeId, string? preferredAnim = null)
    {
        try
        {
            var animations = _resources.LoadAnimations($"Character Animations/{name}");
            if (animations.Count == 0)
            {
                return;
            }

            var animName = preferredAnim != null && animations.ContainsKey(preferredAnim) ? preferredAnim
                : animations.ContainsKey("Idle") ? "Idle"
                : animations.ContainsKey("Walk") ? "Walk"
                : animations.Keys.First();

            var anim = animations[animName];

            if (anim.Frames.Count == 0)
            {
                return;
            }

            using var image = CropFirstFrame(anim);
            var size = new NVector2(anim.FrameWidth, anim.FrameHeight) * Mathz.TrixelSize;
            var material = CreateMaterial(model, image, $"NPC_{nodeId}", GltfAlphaMode.BLEND, doubleSided: true);
            var mesh = BuildQuadMesh(model, material, size);

            var node = parent.CreateNode($"{nodeId}_{name}");
            node.Mesh = mesh;
            node.LocalTransform = new AffineTransform(NVector3.One, NQuaternion.Identity,
                new NVector3(x, y + size.Y / 2f, z));
        }
        catch (Exception ex)
        {
            Logger.Warning(ex, "Skipping NPC {0}", name);
        }
    }

    private RgbaImage ComposeSkyFace(Sky sky, int faceIndex)
    {
        var faceImage = new RgbaImage(SkyFaceSize, SkyFaceSize);
        if (!string.IsNullOrEmpty(sky.Background))
        {
            try
            {
                var bgTex = _resources.Load<RTexture2D>($"Skies/{sky.Name}/{sky.Background}");
                using var bgImg = ImageFromTexture(bgTex);

                var col = (int)(SkyNoonDayFraction * bgImg.Width) % bgImg.Width;

                faceImage.ProcessPixelRows(bgImg, (dstAcc, srcAcc) =>
                {
                    for (var y = 0; y < dstAcc.Height; y++)
                    {
                        var srcY = y * srcAcc.Height / dstAcc.Height;
                        var srcRow = srcAcc.GetRowSpan(srcY);
                        var dstRow = dstAcc.GetRowSpan(y);
                        var srcPx = srcRow[col];
                        for (var x = 0; x < dstRow.Length; x++)
                        {
                            dstRow[x] = srcPx;
                        }
                    }
                });
            }
            catch (Exception ex)
            {
                Logger.Warning(ex, "Failed to render sky background for face {0}", faceIndex);
            }
        }

        var fogColor = NVector3.Zero;
        var cloudTint = NVector3.One;

        if (!string.IsNullOrEmpty(sky.Background))
        {
            try
            {
                var bgTex = _resources.Load<RTexture2D>($"Skies/{sky.Name}/{sky.Background}");
                var col = (int)(SkyNoonDayFraction * bgTex.Width) % bgTex.Width;
                var midY = bgTex.Height / 2;
                var offset = (midY * bgTex.Width + col) * 4;
                fogColor = new NVector3(
                    bgTex.TextureData[offset] / 255f,
                    bgTex.TextureData[offset + 1] / 255f,
                    bgTex.TextureData[offset + 2] / 255f);
            }
            catch (Exception ex)
            {
                Logger.Warning(ex, "Failed to compute fog color for sky {0}", sky.Name);
            }
        }

        if (!string.IsNullOrEmpty(sky.CloudTint))
        {
            try
            {
                var tintTex = _resources.Load<RTexture2D>($"Skies/{sky.Name}/{sky.CloudTint}");
                var col = (int)(SkyNoonDayFraction * tintTex.Width) % tintTex.Width;
                var midY = tintTex.Height / 2;
                var offset = (midY * tintTex.Width + col) * 4;
                cloudTint = new NVector3(
                    tintTex.TextureData[offset] / 255f,
                    tintTex.TextureData[offset + 1] / 255f,
                    tintTex.TextureData[offset + 2] / 255f);
            }
            catch (Exception ex)
            {
                Logger.Warning(ex, "Failed to compute cloud tint for sky {0}", sky.Name);
            }
        }

        if (!string.IsNullOrEmpty(sky.Stars))
        {
            float starsOpacity;
            if (_level.Rainy || sky.Name is "PYRAMID_SKY" or "ABOVE")
            {
                starsOpacity = 1f;
            }
            else if (sky.Name == "OBS_SKY")
            {
                starsOpacity = 0.25f;
            }
            else
            {
                starsOpacity = 0f;
            }

            if (starsOpacity > 0f)
            {
                try
                {
                    var starsTex = _resources.Load<RTexture2D>($"Skies/{sky.Name}/{sky.Stars}");
                    using var starsImg = ImageFromTexture(starsTex);

                    var levelSize = _level.Size.ToNumerics();
                    var starsTexCoordsX = starsImg.Width * Mathz.TrixelSize;
                    var starsTexCoordsY = starsImg.Height * Mathz.TrixelSize;
                    var starsTilingX = (levelSize.X + 1f) / starsTexCoordsX;
                    var starsTilingY = (levelSize.Y + 1f) / starsTexCoordsY;
                    var starsOffset = faceIndex / 4f;

                    faceImage.ProcessPixelRows(starsImg, (dstAcc, srcAcc) =>
                    {
                        for (var dy = 0; dy < dstAcc.Height; dy++)
                        {
                            var dstRow = dstAcc.GetRowSpan(dy);
                            for (var dx = 0; dx < dstRow.Length; dx++)
                            {
                                var px = (float)dx / dstAcc.Width - 0.5f;
                                var py = (float)dy / dstAcc.Height - 0.5f;
                                var u = starsOffset + px * starsTilingX;
                                var v = starsOffset + py * starsTilingY;

                                var sx = ((int)(u * srcAcc.Width) % srcAcc.Width + srcAcc.Width) % srcAcc.Width;
                                var sy = ((int)(v * srcAcc.Height) % srcAcc.Height + srcAcc.Height) % srcAcc.Height;

                                var srcPx = srcAcc.GetRowSpan(sy)[sx];

                                // additive
                                ref var dstPx = ref dstRow[dx];
                                dstPx.R = (byte)Math.Min(255, dstPx.R + (int)(srcPx.R * starsOpacity));
                                dstPx.G = (byte)Math.Min(255, dstPx.G + (int)(srcPx.G * starsOpacity));
                                dstPx.B = (byte)Math.Min(255, dstPx.B + (int)(srcPx.B * starsOpacity));
                                dstPx.A = 255;
                            }
                        }
                    });
                }
                catch (Exception ex)
                {
                    Logger.Warning(ex, "Failed to render sky stars for face {0}", faceIndex);
                }
            }
        }

        if (sky.Clouds.Count > 0)
        {
            try
            {
                var cloudImages = new List<RgbaImage>();
                try
                {
                    foreach (var cloudName in sky.Clouds)
                    {
                        if (string.IsNullOrEmpty(cloudName))
                        {
                            continue;
                        }

                        try
                        {
                            var cloudTex = _resources.Load<RTexture2D>($"Skies/{sky.Name}/{cloudName}");
                            cloudImages.Add(ImageFromTexture(cloudTex));
                        }
                        catch (Exception ex)
                        {
                            Logger.Warning(ex, "Failed to load cloud texture {0} for sky {1}", cloudName, sky.Name);
                        }
                    }

                    if (cloudImages.Count > 0)
                    {
                        var rng = new Random(sky.Name.GetHashCode() ^ faceIndex);
                        var total = (int)(CloudDensity * sky.Density);

                        for (var i = 0; i < total; i++)
                        {
                            var cloudImg = cloudImages[rng.Next(cloudImages.Count)];
                            var cx = rng.Next(-cloudImg.Width / 2, SkyFaceSize + cloudImg.Width / 2);
                            var cy = rng.Next(-cloudImg.Height / 2, SkyFaceSize + cloudImg.Height / 2);

                            faceImage.ProcessPixelRows(cloudImg, (dstAcc, srcAcc) =>
                            {
                                for (var py = 0; py < srcAcc.Height; py++)
                                {
                                    var dy = cy + py;
                                    if (dy is < 0 or >= SkyFaceSize)
                                    {
                                        continue;
                                    }

                                    var srcRow = srcAcc.GetRowSpan(py);
                                    var dstRow = dstAcc.GetRowSpan(dy);
                                    for (var px = 0; px < srcAcc.Width; px++)
                                    {
                                        var dx = cx + px;
                                        if (dx is < 0 or >= SkyFaceSize)
                                        {
                                            continue;
                                        }

                                        var srcPx = srcRow[px];
                                        if (srcPx.A == 0)
                                        {
                                            continue;
                                        }

                                        // alpha-over with inverted src
                                        var alpha = srcPx.A / 255f * CloudOpacity;
                                        ref var dstPx = ref dstRow[dx];
                                        BlendOver(ref dstPx, 255 - srcPx.R, 255 - srcPx.G, 255 - srcPx.B, alpha);
                                    }
                                }
                            });
                        }
                    }
                }
                finally
                {
                    foreach (var img in cloudImages) img.Dispose();
                }
            }
            catch (Exception ex)
            {
                Logger.Warning(ex, "Failed to render clouds for sky face {0}", faceIndex);
            }
        }

        for (var layerIdx = 0; layerIdx < sky.Layers.Count; layerIdx++)
        {
            var layer = sky.Layers[layerIdx];
            if (string.IsNullOrEmpty(layer.Name))
            {
                continue;
            }

            try
            {
                var layerTex = _resources.Load<RTexture2D>($"Skies/{sky.Name}/{layer.Name}");
                using var layerImg = ImageFromTexture(layerTex);

                var diffuse = NVector3.Lerp(cloudTint, fogColor, layer.FogTint);
                var tintR = (byte)(diffuse.X * 255f);
                var tintG = (byte)(diffuse.Y * 255f);
                var tintB = (byte)(diffuse.Z * 255f);
                var opacity = layer.Opacity;

                var layerDepth = sky.Layers.Count > 1
                    ? (float)layerIdx / (sky.Layers.Count - 1)
                    : 0f;
                var uBase = (sky.NoPerFaceLayerXOffset ? 0f : faceIndex / 4f) + sky.LayerBaseXOffset;
                var layerDepthV = sky.VerticalTiling ? layerDepth : layerDepth - 0.5f;
                var vBase = sky.LayerBaseHeight + layerDepthV * sky.LayerBaseSpacing;

                var levelSize = _level.Size.ToNumerics();
                var texCoordsX = layerImg.Width * Mathz.TrixelSize;
                var texCoordsY = layerImg.Height * Mathz.TrixelSize;
                var tilingX = (levelSize.X + 1f) / texCoordsX;
                var tilingY = (levelSize.Y + 1f) / texCoordsY;

                faceImage.ProcessPixelRows(layerImg, (dstAcc, srcAcc) =>
                {
                    for (var dy = 0; dy < dstAcc.Height; dy++)
                    {
                        var dstRow = dstAcc.GetRowSpan(dy);
                        for (var dx = 0; dx < dstRow.Length; dx++)
                        {
                            var px = (float)dx / dstAcc.Width - 0.5f;
                            var py = (float)dy / dstAcc.Height - 0.5f;
                            var u = -uBase + px * tilingX;
                            var v = vBase + py * tilingY;

                            var sx = ((int)(u * srcAcc.Width) % srcAcc.Width + srcAcc.Width) % srcAcc.Width;
                            int sy;
                            if (sky.VerticalTiling)
                            {
                                sy = ((int)(v * srcAcc.Height) % srcAcc.Height + srcAcc.Height) % srcAcc.Height;
                            }
                            else
                            {
                                sy = Math.Clamp((int)(v * srcAcc.Height), 0, srcAcc.Height - 1);
                            }

                            var srcPx = srcAcc.GetRowSpan(sy)[sx];

                            // alpha-over with tinted src
                            var srcA = srcPx.A / 255f * opacity;
                            ref var dstPx = ref dstRow[dx];
                            BlendOver(ref dstPx,
                                r: srcPx.R * tintR / 255,
                                g: srcPx.G * tintG / 255,
                                b: srcPx.B * tintB / 255,
                                srcA);
                        }
                    }
                });
            }
            catch (Exception ex)
            {
                Logger.Warning(ex, "Failed to render sky layer {0} for face {1}", layer.Name, faceIndex);
            }
        }

        return faceImage;
    }

    private static void BlendOver(ref Rgba32 dst, int r, int g, int b, float alpha)
    {
        var inv = 1f - alpha;
        dst.R = (byte)(r * alpha + dst.R * inv);
        dst.G = (byte)(g * alpha + dst.G * inv);
        dst.B = (byte)(b * alpha + dst.B * inv);
        dst.A = 255;
    }

    private static RgbaImage CropFirstFrame(RAnimatedTexture anim)
    {
        var fullAtlas = SixLabors.ImageSharp.Image.LoadPixelData<Rgba32>(
            anim.TextureData, anim.AtlasWidth, anim.AtlasHeight);

        if (anim.Frames.Count == 0)
        {
            return fullAtlas;
        }

        var frame = anim.Frames[0].Rectangle;
        var cropped = fullAtlas.Clone(ctx => ctx.Crop(
            new SixLabors.ImageSharp.Rectangle(frame.X, frame.Y, frame.Width, frame.Height)));
        fullAtlas.Dispose();
        return cropped;
    }

    private static byte[] ImageToBytes(RgbaImage image)
    {
        using var ms = new MemoryStream();
        image.Save(ms, new PngEncoder());
        return ms.ToArray();
    }

    private static Material CreateMaterial(ModelRoot model, RgbaImage image, string name,
        GltfAlphaMode alphaMode = GltfAlphaMode.OPAQUE, float opacity = 1f, bool doubleSided = false)
    {
        var mb = new MaterialBuilder(name)
            .WithMetallicRoughnessShader()
            .WithAlpha(alphaMode)
            .WithDoubleSide(doubleSided);

        var channel = mb.UseChannel(KnownChannel.BaseColor);
        channel.Parameters[KnownProperty.RGBA] = new NVector4(1f, 1f, 1f, opacity);
        channel.UseTexture()
            .WithPrimaryImage(ImageToBytes(image))
            .WithSampler(TextureWrapMode.REPEAT, TextureWrapMode.REPEAT,
                TextureMipMapFilter.NEAREST, TextureInterpolationFilter.NEAREST);

        return model.CreateMaterial(mb);
    }

    private static Mesh BuildQuadMesh(ModelRoot model, Material material, NVector2 size)
    {
        var hw = size.X / 2f;
        var hh = size.Y / 2f;

        var vertices = new[]
        {
            (new VertexPositionNormal(new NVector3(-hw, -hh, 0f), new NVector3(0f, 0f, 1f)),
                new VertexTexture1(new NVector2(0f, 1f))),
            (new VertexPositionNormal(new NVector3(hw, -hh, 0f), new NVector3(0f, 0f, 1f)),
                new VertexTexture1(new NVector2(1f, 1f))),
            (new VertexPositionNormal(new NVector3(-hw, hh, 0f), new NVector3(0f, 0f, 1f)),
                new VertexTexture1(new NVector2(0f, 0f))),
            (new VertexPositionNormal(new NVector3(hw, hh, 0f), new NVector3(0f, 0f, 1f)),
                new VertexTexture1(new NVector2(1f, 0f)))
        };

        var indices = new[] { 0, 1, 2, 2, 1, 3 };

        var mesh = model.CreateMesh();
        mesh.CreatePrimitive()
            .WithMaterial(material)
            .WithVertexAccessors(vertices)
            .WithIndicesAccessor(PrimitiveType.TRIANGLES, indices);

        return mesh;
    }

    private static Mesh BuildQuadMeshInward(ModelRoot model, Material material, NVector2 size)
    {
        var hw = size.X / 2f;
        var hh = size.Y / 2f;

        // Normal points -Z (inward), winding is CCW from -Z so front face is visible from inside
        var vertices = new[]
        {
            (new VertexPositionNormal(new NVector3(-hw, -hh, 0f), new NVector3(0f, 0f, -1f)),
                new VertexTexture1(new NVector2(0f, 1f))),
            (new VertexPositionNormal(new NVector3(hw, -hh, 0f), new NVector3(0f, 0f, -1f)),
                new VertexTexture1(new NVector2(1f, 1f))),
            (new VertexPositionNormal(new NVector3(-hw, hh, 0f), new NVector3(0f, 0f, -1f)),
                new VertexTexture1(new NVector2(0f, 0f))),
            (new VertexPositionNormal(new NVector3(hw, hh, 0f), new NVector3(0f, 0f, -1f)),
                new VertexTexture1(new NVector2(1f, 0f)))
        };

        var indices = new[] { 0, 2, 1, 1, 2, 3 };

        var mesh = model.CreateMesh();
        mesh.CreatePrimitive()
            .WithMaterial(material)
            .WithVertexAccessors(vertices)
            .WithIndicesAccessor(PrimitiveType.TRIANGLES, indices);

        return mesh;
    }

    private static Mesh? BuildMeshFromVertexInstances<TInstance>(
        ModelRoot model,
        IndexedPrimitives<VertexInstance, TInstance> geom,
        Material material,
        bool reverseWinding = false)
    {
        if (geom.Vertices.Length == 0)
        {
            return null;
        }

        var vertices = new (VertexPositionNormal, VertexTexture1)[geom.Vertices.Length];
        for (var i = 0; i < geom.Vertices.Length; i++)
        {
            var v = geom.Vertices[i];
            vertices[i] = (
                new VertexPositionNormal(v.Position.ToNumerics(), v.Normal.ToNumerics()),
                new VertexTexture1(v.TextureCoordinate.ToNumerics())
            );
        }

        var rawIndices = geom.Indices;
        var indices = new int[rawIndices.Length];
        for (var i = 0; i < rawIndices.Length; i++)
        {
            var src = reverseWinding
                ? geom.PrimitiveType switch
                {
                    FEZRepacker.Core.Definitions.Game.XNA.PrimitiveType.TriangleList =>
                        i + (((i + 1) % 3) - 1),
                    FEZRepacker.Core.Definitions.Game.XNA.PrimitiveType.TriangleStrip =>
                        rawIndices.Length - (i + 1),
                    _ => i
                }
                : i;
            indices[i] = rawIndices[src];
        }

        var primitiveType = geom.PrimitiveType switch
        {
            FEZRepacker.Core.Definitions.Game.XNA.PrimitiveType.TriangleList => PrimitiveType.TRIANGLES,
            FEZRepacker.Core.Definitions.Game.XNA.PrimitiveType.TriangleStrip => PrimitiveType.TRIANGLE_STRIP,
            FEZRepacker.Core.Definitions.Game.XNA.PrimitiveType.LineList => PrimitiveType.LINES,
            FEZRepacker.Core.Definitions.Game.XNA.PrimitiveType.LineStrip => PrimitiveType.LINE_STRIP,
            _ => PrimitiveType.TRIANGLES
        };

        var mesh = model.CreateMesh();
        mesh.CreatePrimitive()
            .WithMaterial(material)
            .WithVertexAccessors(vertices)
            .WithIndicesAccessor(primitiveType, indices);

        return mesh;
    }

    #region Texture helpers (inlined from FEZRepacker internals)

    private static RgbaImage ImageFromTexture(RTexture2D tex)
    {
        return SixLabors.ImageSharp.Image.LoadPixelData<Rgba32>(tex.TextureData, tex.Width, tex.Height);
    }

    private static RgbaImage ExtractAlbedo(RTexture2D tex)
    {
        var img = ImageFromTexture(tex);
        img.ProcessPixelRows(acc =>
        {
            for (var y = 0; y < acc.Height; y++)
            {
                foreach (ref var px in acc.GetRowSpan(y))
                {
                    px.A = 255;
                }
            }
        });
        return img;
    }

    #endregion

    protected override void Dispose(bool disposing)
    {
        _cts?.Dispose();
        base.Dispose(disposing);
    }

    private enum State
    {
        Disposed,
        Exporting,
        Complete
    }
}