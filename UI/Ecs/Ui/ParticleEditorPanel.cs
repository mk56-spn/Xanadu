using System.Collections.Generic;
using Friflo.Engine.ECS;
using Xanadu.Particles;
using Xanadu.Theme;
using Xanadu.Ui.Ecs.Ui;

namespace Xanadu.Ecs;

/// <summary>
/// Builds a particle editor panel: which library effect the selected thing
/// plays, its name, its look and a slider for each of its numbers. Any tool
/// can host one, the way it hosts the XIF editor;
/// <see cref="Systems.Ui.ParticleEditorSystem"/> runs it and the host says
/// what is selected through <see cref="IParticleEditorHost"/>.
/// </summary>
public static class ParticleEditorPanel
{
    public const float Width = 260f;
    private const float Row = 22f;
    private const float Gap = 3f;
    private const float LabelWidth = 64f;
    private const float StepWidth = 28f;
    private const float ReadoutWidth = 44f;
    private const float TrackHeight = 8f;

    /// <summary>Rows above the sliders: title, effect, name, file, look twice, colour; and the status under them.</summary>
    private const int FixedRows = 7;

    /// <summary>Total height, so a host can place the panel before it is laid out.</summary>
    public static float Height
    {
        get
        {
            int rows = FixedRows + XpfEditing.Fields.Length + 1;
            return rows * Row + (rows - 1) * Gap + UiTheme.ContainerPadding * 2;
        }
    }

    private static float Inner => Width - UiTheme.ContainerPadding * 2;

    /// <summary>
    /// Builds the panel hidden on <paramref name="layer"/>; it shows at
    /// <paramref name="home"/> (its centre) whenever the host has a selection.
    /// </summary>
    public static Entity Build(UiNode layer, Vector2 home, IParticleEditorHost host)
    {
        UiNode panel = layer.Panel().Size(Width, Height).Gap(Gap).Hidden()
            .Main(StackMainAlignment.Start);
        Entity editor = panel;
        var parts = new ParticleEditorParts { Sliders = new List<Entity>() };

        parts.Title = panel.Text("", UiType.Label, UiTheme.Accent).Size(Inner, Row);

        UiNode effect = panel.Row().Size(Inner, Row).Gap(4);
        Button(effect, "<", ParticleEditorActionKind.Previous, editor, StepWidth, "Play the previous effect in the library");
        Button(effect, "+ NEW", ParticleEditorActionKind.New, editor, (Inner - StepWidth * 2 - 12) / 2,
            "Add a plain effect and play it here");
        Button(effect, "COPY", ParticleEditorActionKind.Copy, editor, (Inner - StepWidth * 2 - 12) / 2,
            "Play a copy of this effect, to change it without changing everything else that plays it");
        Button(effect, ">", ParticleEditorActionKind.Next, editor, StepWidth, "Play the next effect in the library");

        UiNode name = panel.Row().Size(Inner, Row).Gap(6);
        name.Text("EFFECT", UiType.Caption, UiTheme.TextDim).Size(LabelWidth, Row);
        parts.Name = Field(name, ParticleEditorFieldKind.Name, editor, Inner - LabelWidth - 6,
            "The effect's name in the library; renaming it here leaves other users on the old name");

        UiNode file = panel.Row().Size(Inner, Row).Gap(4);
        parts.Save = Button(file, "SAVE", ParticleEditorActionKind.Save, editor, (Inner - 4) / 2,
            "Write the particle library, which every editor and the game read");
        Button(file, "REVERT", ParticleEditorActionKind.Revert, editor, (Inner - 4) / 2,
            "Drop unsaved particle edits and read the library back");

        float third = (Inner - 8) / 3;
        UiNode look = panel.Row().Size(Inner, Row).Gap(4);
        parts.Texture = Button(look, "", ParticleEditorActionKind.Texture, editor, third, "Draw soft dots or note glyphs");
        parts.Shape = Button(look, "", ParticleEditorActionKind.Shape, editor, third,
            "Start every particle at the spot, or anywhere in a box");
        parts.Upright = Button(look, "UPRIGHT", ParticleEditorActionKind.Upright, editor, third,
            "Keep particles' turn relative to upright, however the emitter is turned");

        UiNode life = panel.Row().Size(Inner, Row).Gap(4);
        parts.ScaleCurve = Button(life, "", ParticleEditorActionKind.ScaleCurve, editor, (Inner - 4) / 2,
            "How a particle's size changes over its life");
        parts.Fade = Button(life, "", ParticleEditorActionKind.Fade, editor, (Inner - 4) / 2,
            "How a particle fades over its life");

        UiNode color = panel.Row().Size(Inner, Row).Gap(6);
        color.Text("COLOUR", UiType.Caption, UiTheme.TextDim).Size(LabelWidth, Row);
        parts.Color = Field(color, ParticleEditorFieldKind.Color, editor, Inner - LabelWidth - 6,
            "The particles' colour as #rrggbb or #rrggbbaa; whatever plays it may tint it further");

        foreach (XpfField field in XpfEditing.Fields)
            parts.Sliders.Add(Slider(panel, field, editor));

        parts.Status = panel.Text("", UiType.Caption, UiTheme.TextDim).Size(Inner, Row);

        panel.With(new ParticleEditor
        {
            Host = host,
            Home = home,
            Parts = parts,
            Status = "",
        });
        return editor;
    }

    /// <summary>A number set by dragging: its name, the track, then the value; a right-click puts back what it was built with.</summary>
    private static Entity Slider(UiNode panel, XpfField field, Entity editor)
    {
        XpfFieldRange range = XpfEditing.Range(field);
        UiNode row = panel.Row().Size(Inner, Row).Gap(6).Cross(StackCrossAlignment.Center);
        row.Text(range.Label, UiType.Caption, UiTheme.TextDim).Size(LabelWidth, Row);

        var size = new Vector2(Inner - LabelWidth - ReadoutWidth - 12, TrackHeight);
        UiNode track = row.Rect(Palette.Muted, size: size).Input();
        UiNode fill = row.Rect(Palette.PrimaryMuted, "", size).IgnoreLayout();
        UiNode readout = row.Text("", UiType.Caption, UiTheme.Text, TextAlign.Right).Size(ReadoutWidth, Row);

        float rest = XpfEditing.Get(new XpfParticle(), field);
        return track
            .With(new UiSlider(fill, readout, range.Min, range.Max, rest, format: range.Format, defaultValue: rest))
            .With(new ParticleEditorSlider(field, editor))
            .Tooltip(range.Tooltip + " · right-click resets");
    }

    private static Entity Button(
        UiNode row,
        string label,
        ParticleEditorActionKind kind,
        Entity editor,
        float width,
        string tooltip) =>
        row.Button(label, UiType.Caption).Size(width, Row)
            .With(UiCommand.Of(kind, owner: editor))
            .Tooltip(tooltip);

    private static Entity Field(UiNode row, ParticleEditorFieldKind kind, Entity editor, float width, string tooltip) =>
        row.Rect(Palette.SurfaceInset, "", new Vector2(width, Row))
            .Input()
            .With(new TextStyle(UiType.Caption, UiTheme.Text))
            .Align(TextAlign.Left)
            .With(new TextField(""))
            .With(new ParticleEditorField(kind, editor))
            .Tooltip(tooltip);
}
