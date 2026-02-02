using System.Numerics;
using ImGuiNET;

namespace FezEditor.Services;

public partial class ImGuiService
{
    /// <summary>
    /// Configures ImGui's visual style with custom colors, padding, rounding, and other appearance settings.
    /// </summary>
    private static void SetupStyle()
    {
        var style = ImGui.GetStyle();
        style.Alpha = 1.0f;
        style.DisabledAlpha = 0.6f;
        style.WindowPadding = new Vector2(8.0f, 8.0f);
        style.WindowRounding = 0.0f;
        style.WindowBorderSize = 1.0f;
        style.WindowMinSize = new Vector2(32.0f, 32.0f);
        style.WindowTitleAlign = new Vector2(0.0f, 0.5f);
        style.WindowMenuButtonPosition = ImGuiDir.Left;
        style.ChildRounding = 0.0f;
        style.ChildBorderSize = 1.0f;
        style.PopupRounding = 0.0f;
        style.PopupBorderSize = 1.0f;
        style.FramePadding = new Vector2(4.0f, 3.0f);
        style.FrameRounding = 0.0f;
        style.FrameBorderSize = 0.0f;
        style.ItemSpacing = new Vector2(8.0f, 4.0f);
        style.ItemInnerSpacing = new Vector2(4.0f, 4.0f);
        style.CellPadding = new Vector2(4.0f, 2.0f);
        style.IndentSpacing = 21.0f;
        style.ColumnsMinSpacing = 6.0f;
        style.ScrollbarSize = 14.0f;
        style.ScrollbarRounding = 9.0f;
        style.GrabMinSize = 10.0f;
        style.GrabRounding = 0.0f;
        style.TabRounding = 4.0f;
        style.TabBorderSize = 0.0f;
        style.TabMinWidthForCloseButton = 0.0f;
        style.ColorButtonPosition = ImGuiDir.Right;
        style.ButtonTextAlign = new Vector2(0.5f, 0.5f);
        style.SelectableTextAlign = new Vector2(0.0f, 0.0f);
        style.Colors[(int)ImGuiCol.Text] = new Vector4(1.0f, 1.0f, 1.0f, 1.0f);
        style.Colors[(int)ImGuiCol.TextDisabled] = new Vector4(0.49803922f, 0.49803922f, 0.49803922f, 1.0f);
        style.Colors[(int)ImGuiCol.WindowBg] = new Vector4(0.05882353f, 0.05882353f, 0.05882353f, 0.94f);
        style.Colors[(int)ImGuiCol.ChildBg] = new Vector4(1.0f, 1.0f, 1.0f, 0.0f);
        style.Colors[(int)ImGuiCol.PopupBg] = new Vector4(0.078431375f, 0.078431375f, 0.078431375f, 0.94f);
        style.Colors[(int)ImGuiCol.Border] = new Vector4(0.42745098f, 0.42745098f, 0.49803922f, 0.5f);
        style.Colors[(int)ImGuiCol.BorderShadow] = new Vector4(0.0f, 0.0f, 0.0f, 0.0f);
        style.Colors[(int)ImGuiCol.FrameBg] = new Vector4(0.2f, 0.20784314f, 0.21960784f, 0.54f);
        style.Colors[(int)ImGuiCol.FrameBgHovered] = new Vector4(0.4f, 0.4f, 0.4f, 0.4f);
        style.Colors[(int)ImGuiCol.FrameBgActive] = new Vector4(0.1764706f, 0.1764706f, 0.1764706f, 0.67f);
        style.Colors[(int)ImGuiCol.TitleBg] = new Vector4(0.039215688f, 0.039215688f, 0.039215688f, 1.0f);
        style.Colors[(int)ImGuiCol.TitleBgActive] = new Vector4(0.28627452f, 0.28627452f, 0.28627452f, 1.0f);
        style.Colors[(int)ImGuiCol.TitleBgCollapsed] = new Vector4(0.0f, 0.0f, 0.0f, 0.51f);
        style.Colors[(int)ImGuiCol.MenuBarBg] = new Vector4(0.13725491f, 0.13725491f, 0.13725491f, 1.0f);
        style.Colors[(int)ImGuiCol.ScrollbarBg] = new Vector4(0.019607844f, 0.019607844f, 0.019607844f, 0.53f);
        style.Colors[(int)ImGuiCol.ScrollbarGrab] = new Vector4(0.30980393f, 0.30980393f, 0.30980393f, 1.0f);
        style.Colors[(int)ImGuiCol.ScrollbarGrabHovered] = new Vector4(0.40784314f, 0.40784314f, 0.40784314f, 1.0f);
        style.Colors[(int)ImGuiCol.ScrollbarGrabActive] = new Vector4(0.50980395f, 0.50980395f, 0.50980395f, 1.0f);
        style.Colors[(int)ImGuiCol.CheckMark] = new Vector4(0.9372549f, 0.9372549f, 0.9372549f, 1.0f);
        style.Colors[(int)ImGuiCol.SliderGrab] = new Vector4(0.50980395f, 0.50980395f, 0.50980395f, 1.0f);
        style.Colors[(int)ImGuiCol.SliderGrabActive] = new Vector4(0.85882354f, 0.85882354f, 0.85882354f, 1.0f);
        style.Colors[(int)ImGuiCol.Button] = new Vector4(0.4392157f, 0.4392157f, 0.4392157f, 0.4f);
        style.Colors[(int)ImGuiCol.ButtonHovered] = new Vector4(0.45882353f, 0.46666667f, 0.47843137f, 1.0f);
        style.Colors[(int)ImGuiCol.ButtonActive] = new Vector4(0.41960785f, 0.41960785f, 0.41960785f, 1.0f);
        style.Colors[(int)ImGuiCol.Header] = new Vector4(0.69803923f, 0.69803923f, 0.69803923f, 0.31f);
        style.Colors[(int)ImGuiCol.HeaderHovered] = new Vector4(0.69803923f, 0.69803923f, 0.69803923f, 0.8f);
        style.Colors[(int)ImGuiCol.HeaderActive] = new Vector4(0.47843137f, 0.49803922f, 0.5176471f, 1.0f);
        style.Colors[(int)ImGuiCol.Separator] = new Vector4(0.42745098f, 0.42745098f, 0.49803922f, 0.5f);
        style.Colors[(int)ImGuiCol.SeparatorHovered] = new Vector4(0.7176471f, 0.7176471f, 0.7176471f, 0.78f);
        style.Colors[(int)ImGuiCol.SeparatorActive] = new Vector4(0.50980395f, 0.50980395f, 0.50980395f, 1.0f);
        style.Colors[(int)ImGuiCol.ResizeGrip] = new Vector4(0.9098039f, 0.9098039f, 0.9098039f, 0.25f);
        style.Colors[(int)ImGuiCol.ResizeGripHovered] = new Vector4(0.80784315f, 0.80784315f, 0.80784315f, 0.67f);
        style.Colors[(int)ImGuiCol.ResizeGripActive] = new Vector4(0.45882353f, 0.45882353f, 0.45882353f, 0.95f);
        style.Colors[(int)ImGuiCol.Tab] = new Vector4(0.1764706f, 0.34901962f, 0.5764706f, 0.862f);
        style.Colors[(int)ImGuiCol.TabHovered] = new Vector4(0.25882354f, 0.5882353f, 0.9764706f, 0.8f);
        style.Colors[(int)ImGuiCol.TabSelected] = new Vector4(0.19607843f, 0.40784314f, 0.6784314f, 1.0f);
        style.Colors[(int)ImGuiCol.TabDimmed] = new Vector4(0.06666667f, 0.101960786f, 0.14509805f, 0.9724f);
        style.Colors[(int)ImGuiCol.TabDimmedSelected] = new Vector4(0.13333334f, 0.25882354f, 0.42352942f, 1.0f);
        style.Colors[(int)ImGuiCol.PlotLines] = new Vector4(0.60784316f, 0.60784316f, 0.60784316f, 1.0f);
        style.Colors[(int)ImGuiCol.PlotLinesHovered] = new Vector4(1.0f, 0.42745098f, 0.34901962f, 1.0f);
        style.Colors[(int)ImGuiCol.PlotHistogram] = new Vector4(0.7294118f, 0.6f, 0.14901961f, 1.0f);
        style.Colors[(int)ImGuiCol.PlotHistogramHovered] = new Vector4(1.0f, 0.6f, 0.0f, 1.0f);
        style.Colors[(int)ImGuiCol.TableHeaderBg] = new Vector4(0.1882353f, 0.1882353f, 0.2f, 1.0f);
        style.Colors[(int)ImGuiCol.TableBorderStrong] = new Vector4(0.30980393f, 0.30980393f, 0.34901962f, 1.0f);
        style.Colors[(int)ImGuiCol.TableBorderLight] = new Vector4(0.22745098f, 0.22745098f, 0.24705882f, 1.0f);
        style.Colors[(int)ImGuiCol.TableRowBg] = new Vector4(0.0f, 0.0f, 0.0f, 0.0f);
        style.Colors[(int)ImGuiCol.TableRowBgAlt] = new Vector4(1.0f, 1.0f, 1.0f, 0.06f);
        style.Colors[(int)ImGuiCol.TextSelectedBg] = new Vector4(0.8666667f, 0.8666667f, 0.8666667f, 0.35f);
        style.Colors[(int)ImGuiCol.DragDropTarget] = new Vector4(1.0f, 1.0f, 0.0f, 0.9f);
        style.Colors[(int)ImGuiCol.NavHighlight] = new Vector4(0.6f, 0.6f, 0.6f, 1.0f);
        style.Colors[(int)ImGuiCol.NavWindowingHighlight] = new Vector4(1.0f, 1.0f, 1.0f, 0.7f);
        style.Colors[(int)ImGuiCol.NavWindowingDimBg] = new Vector4(0.8f, 0.8f, 0.8f, 0.2f);
        style.Colors[(int)ImGuiCol.ModalWindowDimBg] = new Vector4(0.8f, 0.8f, 0.8f, 0.35f);
    }
}