using FezEditor.Components.Zu;
using FezEditor.Tools;
using ImGuiNET;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace FezEditor.Components;

public class ZuEditor : EditorComponent
{
    public override object Asset => Font;

    public FezFont Font { get; }

    public Texture2D FontTexture { get; private set; }

    public IntPtr FontTexturePtr { get; private set; }

    public ImFontPtr CharactersFont { get; private set; }

    public int SelectedIndex { get; set; } = -1;

    private FontProperties _properties = null!;

    private FontAtlas _atlas = null!;

    private FontPreview _preview = null!;

    private TempTextureTracker? _atlasTracker;

    public ZuEditor(Game game, string title, FezFont font) : base(game, title)
    {
        Font = font;
        FontTexture = null!;
        History.Track(font);
    }

    public override void LoadContent()
    {
        FontTexture = RepackerExtensions.ConvertToTexture2D(Font.Texture);
        FontTexturePtr = ImGuiX.Bind(FontTexture);
        CharactersFont = SelectCharactersFont();

        _properties = new FontProperties(this);
        _preview = new FontPreview(Title, this);
        _atlas = new FontAtlas(_preview, this);

        if (!ResourceService.IsReadonly)
        {
            var path = Path.ChangeExtension(ResourceService.GetFullPath(Title), ".png");
            if (File.Exists(path))
            {
                _atlasTracker = new TempTextureTracker(Game, path);
                _atlasTracker.Changed += OnAtlasChanged;
            }
        }
    }

    public override void Draw()
    {
        ImGuiX.PushStyleVar(ImGuiStyleVar.ItemSpacing, new Vector2(8, 8));

        if (ImGuiX.BeginChild("##Left", new Vector2(340f, 0), ImGuiChildFlags.Border))
        {
            _properties.Draw();
            ImGui.EndChild();
        }

        ImGui.SameLine();

        if (ImGuiX.BeginChild("##Right", Vector2.Zero, ImGuiChildFlags.Border))
        {
            _atlas.Draw();
            ImGui.EndChild();
        }

        ImGui.PopStyleVar();

        _preview.DrawWindow();
    }

    public void AddCharacter(char character)
    {
        using (History.BeginScope("Add New Character"))
        {
            Font.Characters.Add(character);
            Font.GlyphBounds.Add(new RRectangle(0, 0, 8, 8));
            Font.Cropping.Add(new RRectangle(0, 0, 8, 12));
            Font.KerningData.Add(new RVector3(0, 8, 0));
            SelectedIndex = Font.Characters.Count - 1;
        }
    }

    public void SetCharacter(int selectedIndex, char character)
    {
        using (History.BeginScope("Set a Character"))
        {
            Font.Characters[selectedIndex] = character;
        }
    }

    public void DuplicateSelected()
    {
        if (SelectedIndex < 0)
        {
            return;
        }

        using (History.BeginScope("Duplicate Selected Characters"))
        {
            Font.Characters.Add(Font.Characters[SelectedIndex]);

            Font.GlyphBounds.Add(SelectedIndex < Font.GlyphBounds.Count
                ? Font.GlyphBounds[SelectedIndex]
                : new RRectangle(0, 0, 0, 0)
            );

            Font.Cropping.Add(SelectedIndex < Font.Cropping.Count
                ? Font.Cropping[SelectedIndex]
                : new RRectangle(0, 0, 0, 0)
            );

            Font.KerningData.Add(SelectedIndex < Font.KerningData.Count
                ? Font.KerningData[SelectedIndex]
                : RVector3.Zero
            );

            SelectedIndex = Font.Characters.Count - 1;
        }
    }

    public void DeleteSelected()
    {
        if (SelectedIndex < 0)
        {
            return;
        }

        using (History.BeginScope("Delete Selected Characters"))
        {
            RemoveAt(Font.Characters, SelectedIndex);
            RemoveAt(Font.GlyphBounds, SelectedIndex);
            RemoveAt(Font.Cropping, SelectedIndex);
            RemoveAt(Font.KerningData, SelectedIndex);
            SelectedIndex = Math.Min(SelectedIndex, Font.Characters.Count - 1);
        }
    }

    public int HitTest(NVector2 atlasPixel)
    {
        for (var i = Font.GlyphBounds.Count - 1; i >= 0; i--)
        {
            var r = Font.GlyphBounds[i];
            if (atlasPixel.X >= r.X && atlasPixel.X <= r.X + r.Width &&
                atlasPixel.Y >= r.Y && atlasPixel.Y <= r.Y + r.Height)
            {
                return i;
            }
        }

        return -1;
    }

    private static void RemoveAt<T>(List<T> list, int i)
    {
        if (i < list.Count)
        {
            list.RemoveAt(i);
        }
    }

    private void OnAtlasChanged(Texture2D texture)
    {
        ImGuiX.Unbind(FontTexture);
        FontTexture.Dispose();
        FontTexture = texture;
        FontTexturePtr = ImGuiX.Bind(FontTexture);
    }

    private ImFontPtr SelectCharactersFont()
    {
        if (Title.Contains("japanese", StringComparison.OrdinalIgnoreCase))
        {
            return ImGuiX.Fonts.NotoSansJp;
        }

        if (Title.Contains("korean", StringComparison.OrdinalIgnoreCase))
        {
            return ImGuiX.Fonts.NotoSansKr;
        }

        if (Title.Contains("chinese", StringComparison.OrdinalIgnoreCase))
        {
            return ImGuiX.Fonts.NotoSansTc;
        }

        return ImGuiX.Fonts.NotoSans;
    }

    public override void Dispose()
    {
        if (_atlasTracker != null)
        {
            _atlasTracker.Changed -= OnAtlasChanged;
            _atlasTracker.Dispose();
        }

        ImGuiX.Unbind(FontTexture);
        FontTexture.Dispose();
        FontTexturePtr = IntPtr.Zero;
        base.Dispose();
    }

    public static object Create()
    {
        return new FezFont
        {
            Texture = new RTexture2D
            {
                Width = 64,
                Height = 64,
                TextureData = new byte[64 * 64 * 4]
            }
        };
    }
}