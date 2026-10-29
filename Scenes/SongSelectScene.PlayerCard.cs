using Friflo.Engine.ECS;
using Godot;
using Xanadu.Ecs;
using Xanadu.Profiles;
using Xanadu.Strings;
using Xanadu.Systems.Shaders;

namespace Xanadu.Scenes;

/// <summary>The player banner and the level readout beneath it.</summary>
public static partial class SongSelectScene
{
    private const string BackgroundShader = "res://Assets/Shaders/note_background.gdshader";


    /// <summary>Builds the banner and the level readout under it, returning the readout's label and panel.</summary>
    private static (Entity Label, Entity Panel) BuildPlayerCard(
        UiBuilder ui,
        ProfileStore profiles,
        bool loadAssets)
    {
        UiNode root = UiNode.Wrap(ui.Current, ui.Visuals);

        // The banner is a skewed container whose right side runs off screen.
        const float bannerLeft = 1511.73f;
        const float bannerWidth = 1941.542f - bannerLeft;
        UiNode banner = root.Row()
            .Size(bannerWidth, 98.396f)
            .At(new Vector2(bannerLeft + bannerWidth / 2, 115.94f))
            .Fill(PanelBlack)
            .Pad(1568.914f - bannerLeft - TextInset)
            .Main(StackMainAlignment.Start)
            .Skew(PreviewTilt);
        Frost(banner.Entity, loadAssets);

        string name = profiles.ActiveProfile?.Name ??
            LocalizedText.Get(SongSelectStrings.NoProfile);
        name = Fit(name.ToUpperInvariant(), 20);

        // The banner fits eight characters at full size; longer names shrink to stay inside it.
        int size = Mathf.Clamp(Mathf.FloorToInt(620f / name.Length), 28, 70);
        UiNode bannerText = banner.Text(name, size, Colors.White, TextAlign.Left)
            .Size(350, 84)
            .Bold();

        if (loadAssets)
        {
            // Gold to cream across 285 pixels from where the text starts.
            bannerText.With(new UiMaterial(UiTextGradient.Create(
                Gold,
                GoldLight,
                new Vector2(-171, 0),
                new Vector2(114.4f, 0))));
        }

        // The readout's panel sizes itself to its label and keeps its left edge put.
        UiNode panel = root.Row()
            .Size(200, LevelPanelHeight)
            .At(new Vector2(
                LevelPanelEdge + 100,
                LevelPanelTop + LevelPanelHeight / 2))
            .Fill(Dim)
            .Pad(LevelTextLeft - TextInset - LevelPanelEdge)
            .AutoWidth()
            .Main(StackMainAlignment.Start)
            .Skew(PreviewTilt)
            .AnchorLeft();

        UiNode level = panel.Text("", 50, LevelFill, TextAlign.Left)
            .Size(LevelLabelWidth(0), 70)
            .Bold()
            .Outline(LevelEdge(LevelColor(1)), LevelOutline)
            .Glow(LevelGlow(LevelColor(1)), radius: 12, intensity: LevelGlowIntensity);

        return (level.Entity, panel.Entity);
    }

    private const float LevelTextLeft = 1525.582f;

    /// <summary>The panel's left edge at mid height; its top corner leans further right.</summary>
    private const float LevelPanelEdge = 1501.51f;
    private const float LevelPanelTop = 170.052f;
    private const float LevelPanelHeight = 84;
    private const int LevelOutline = 5;

    // Kept well below full brightness: the readout should not outshine the song's own title.
    private static readonly Color LevelFill = new(0.82f, 0.85f, 0.88f);
    private const float LevelGlowIntensity = 1.1f;

    private static Color LevelEdge(Color level) => new(level.Lerp(Colors.Black, 0.3f), 0.8f);

    private static Color LevelGlow(Color level) => new(level, 0.4f);

    /// <summary>A little over the advance of one bold 50px capital or digit: text wider than its box is clipped.</summary>
    private const float LevelGlyphWidth = 27;

    /// <summary>The label's width for <paramref name="characters"/>, with the canvas's text padding either side.</summary>
    private static float LevelLabelWidth(int characters) =>
        characters * LevelGlyphWidth + TextInset * 2;

    /// <summary>Green for the easiest levels, through yellow to red by level 50.</summary>
    private static Color LevelColor(int level)
    {
        float t = Mathf.Clamp((level - 1) / 49f, 0, 1);
        return Color.FromHsv(Mathf.Lerp(0.38f, 0f, t), 0.85f, 1f);
    }

    /// <summary>Shows the selected level, fits its panel to the text and tints the outline and glow by level.</summary>
    private static void RefreshLevel(ref SongSelectState state)
    {
        bool empty = state.Count == 0;
        string text = empty ? "" : $"LEVEL {state.Selected + 1}";
        SceneUi.SetText(state.LevelLabel, text);

        // The panel measures its label, so resizing the label is all it takes.
        ref Rectangle label = ref state.LevelLabel.GetComponent<Rectangle>();
        Vector2 size = new(LevelLabelWidth(text.Length), label.Bounds.Size.Y);
        if (label.Bounds.Size != size)
        {
            label.Bounds = new Rect2(-size / 2, size);
            state.LevelPanel.AddTag<LayoutDirty>();
        }

        ref FillColor fill = ref state.LevelPanel.GetComponent<FillColor>();
        Color panelFill = empty ? Transparent : Dim;
        if (fill.Value != panelFill)
        {
            fill.Value = panelFill;
            state.LevelPanel.AddTag<Dirty>();
        }

        Color color = LevelColor(state.Selected + 1);
        state.LevelLabel.GetComponent<TextOutline>().Color = LevelEdge(color);
        state.LevelLabel.GetComponent<TextGlow>().Color = LevelGlow(color);
        state.LevelLabel.AddTag<Dirty>();
    }

    /// <summary>Gives the full-screen background the gold notes shader, when assets are available.</summary>
    private static void ApplyBackgroundShader(Entity background, bool loadAssets)
    {
        if (NoteMaterial(loadAssets, aspect: 1920f / 1080f, zoom: 1f) is { } material)
        {
            background.AddComponent(new UiMaterial(material));
            background.AddComponent(new SpectrumEnergy());
        }
    }

    /// <summary>
    /// The notes shader as a material: a colour for the notes and the two ends
    /// of the gradient behind them, with its cells kept square for a surface of
    /// the given aspect. Null when assets are not available.
    /// </summary>
    private static ShaderMaterial? NoteMaterial(
        bool loadAssets,
        float aspect,
        float zoom,
        Color? notes = null,
        Color? top = null,
        Color? bottom = null)
    {
        if (!loadAssets || ShaderMaterials.Load(BackgroundShader) is not { } material)
            return null;

        material.SetShaderParameter("notes", NoteAtlas.Get());
        material.SetShaderParameter("glyph_count", NoteAtlas.Glyphs.Length);
        material.SetShaderParameter("aspect", aspect);
        material.SetShaderParameter("zoom", zoom);
        if (notes is { } noteColor)
            material.SetShaderParameter("note_color", noteColor);

        if (top is { } topColor)
            material.SetShaderParameter("top_color", topColor);

        if (bottom is { } bottomColor)
            material.SetShaderParameter("bottom_color", bottomColor);

        return material;
    }
}
