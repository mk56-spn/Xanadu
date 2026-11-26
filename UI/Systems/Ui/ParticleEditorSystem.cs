using System.Collections.Generic;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Xanadu.Core;
using Xanadu.Core.Items;
using Xanadu.Core.Particles;
using Xanadu.Ui.Ecs.Ui;

namespace Xanadu.Ui.Systems.Ui;

/// <summary>
/// Runs every particle editor panel: shows it while its host has a selection,
/// applies its buttons, fields and sliders to the host's own settings (through
/// the host, so its undo records them), and keeps its readouts in step with
/// them. The library is only presets: loaded into the settings, or stored from them.
/// </summary>
public sealed class ParticleEditorSystem : QuerySystem<ParticleEditor>
{
    private readonly ButtonPress _presses = new();

    public ParticleEditorSystem()
    {
        // The panel hides itself while the host has no selection, and still has to notice one.
        Filter.WithDisabled();
    }

    protected override void OnUpdate()
    {
        foreach (Entity panel in Query.ToEntityList())
            Update(panel);
    }

    private void Update(Entity panel)
    {
        ParticleEditorFrame frame = ParticleEditorFrame.Refresh(panel);
        ref ParticleEditor editor = ref panel.GetComponent<ParticleEditor>();
        if (frame.Particle is null)
        {
            if (editor.Sliding)
            {
                panel.EmitSignal(new EditGestureEnded());
                editor.Sliding = false;
            }
            UiLayout.SetHidden(panel, true);
            return;
        }

        if (UiLayout.Move(panel, editor.Home))
            panel.AddTag<Dirty>();
        UiLayout.SetHidden(panel, false);

        RunActions(panel, ref editor);
        XpfParticle? particle = ParticleEditorFrame.Refresh(panel).Particle;
        SyncFields(panel, ref editor, particle);
        if (particle is not null)
            SyncSliders(panel, ref editor, particle);
        SyncLabels(ref editor, particle);
    }

    // ------------------------------------------------------------- buttons

    private void RunActions(Entity panel, ref ParticleEditor editor)
    {
        var requests = new List<ParticleEditorActionKind>();
        foreach ((Entity button, ParticleEditorActionKind kind, _) in UiCommand.All<ParticleEditorActionKind>(Query.Store, panel))
        {
            if (_presses.Started(button))
                requests.Add(kind);
        }

        foreach (ParticleEditorActionKind kind in requests)
            Run(ref editor, kind);
    }

    /// <summary>Applies one button; also how tests and hosts drive the editor without a pointer.</summary>
    public static void Run(ref ParticleEditor editor, ParticleEditorActionKind kind)
    {
        XpfLibrary library = ParticleLibrary.Shared;
        Entity panel = editor.Parts.Panel;
        XpfParticle? particle = ParticleEditorFrame.Refresh(panel).Particle;
        switch (kind)
        {
            case ParticleEditorActionKind.Previous:
            case ParticleEditorActionKind.Next:
                if (XpfEditing.Step(library, particle?.Name, kind == ParticleEditorActionKind.Next ? 1 : -1) is { } other &&
                    library.Find(other) is { } preset)
                {
                    panel.EmitSignal(new ParticleUseAsked(preset.Clone()));
                    editor.Status = $"LOADED PRESET {other}";
                }
                else
                {
                    editor.Status = "THE LIBRARY HAS NO PRESETS";
                }
                break;
            case ParticleEditorActionKind.New:
                var plain = new XpfParticle();
                XpfEditing.SetFadePreset(plain, XpfFadePreset.Out);
                panel.EmitSignal(new ParticleUseAsked(plain));
                editor.Status = "STARTED OVER";
                break;
            case ParticleEditorActionKind.Store when particle is not null:
                XpfParticle stored = particle.Clone();
                int at = library.Particles.FindIndex(preset => preset.Name == stored.Name);
                if (at >= 0)
                    library.Particles[at] = stored;
                else
                    library.Particles.Add(stored);
                ParticleLibrary.MarkEdited();
                editor.Status = $"STORED AS PRESET {stored.Name} · SAVE TO KEEP IT";
                break;
            case ParticleEditorActionKind.Save:
                try
                {
                    ParticleLibrary.Save();
                    editor.Status = "LIBRARY SAVED";
                }
                catch (Exception exception) when (exception is System.IO.IOException or UnauthorizedAccessException)
                {
                    editor.Status = "SAVE FAILED: " + exception.Message;
                }
                break;
            case ParticleEditorActionKind.Revert:
                ParticleLibrary.Reload();
                editor.Status = "LIBRARY RELOADED";
                break;
            case ParticleEditorActionKind.Texture when particle is not null:
                Edit(ref editor, particle, () => particle.Texture = XpfEditing.Next(particle.Texture));
                break;
            case ParticleEditorActionKind.Shape when particle is not null:
                Edit(ref editor, particle, () => particle.Shape = XpfEditing.Next(particle.Shape));
                break;
            case ParticleEditorActionKind.Upright when particle is not null:
                Edit(ref editor, particle, () => particle.Upright = !particle.Upright);
                break;
            case ParticleEditorActionKind.ScaleCurve when particle is not null:
                Edit(ref editor, particle, () => XpfEditing.SetScalePreset(particle, XpfEditing.Next(XpfEditing.ScalePreset(particle))));
                break;
            case ParticleEditorActionKind.Fade when particle is not null:
                Edit(ref editor, particle, () => XpfEditing.SetFadePreset(particle, XpfEditing.Next(XpfEditing.FadePreset(particle))));
                break;
        }
    }

    /// <summary>One edit of the settings in place, as one undo step of the owner's.</summary>
    private static void Edit(ref ParticleEditor editor, XpfParticle particle, Action change) =>
        editor.Parts.Panel.EmitSignal(new EditAsked(() =>
        {
            change();
            particle.Touch();
        }));

    // -------------------------------------------------------------- fields

    private void SyncFields(Entity panel, ref ParticleEditor editor, XpfParticle? particle)
    {
        foreach (Entity field in Query.Store.Query<ParticleEditorField>().ToEntityList())
        {
            ref ParticleEditorField binding = ref field.GetComponent<ParticleEditorField>();
            if (binding.Editor.Id != panel.Id || !field.HasComponent<TextField>())
                continue;

            bool focused = field.Tags.Has<TextFieldFocused>();
            if (!focused && binding.WasFocused && particle is not null)
                Commit(ref editor, particle, binding.Kind, field.GetComponent<TextField>().Value);
            binding.WasFocused = focused;

            if (!focused)
                SetField(field, particle is null ? "" : Format(particle, binding.Kind));
        }
    }

    /// <summary>Applies a typed value; one that cannot be read is refused and the field shows the effect again.</summary>
    public static void Commit(ref ParticleEditor editor, XpfParticle particle, ParticleEditorFieldKind kind, string typed)
    {
        switch (kind)
        {
            case ParticleEditorFieldKind.Name:
                string name = typed.Trim();
                if (name == particle.Name)
                    break;
                if (name.Length == 0 || name.Length > XpfParticle.MaxNameLength)
                {
                    editor.Status = "NAME IS BLANK OR TOO LONG";
                    break;
                }
                Edit(ref editor, particle, () => particle.Name = name);
                editor.Status = "RENAMED";
                break;
            case ParticleEditorFieldKind.Color:
                if (!XifEditing.TryParseFill(typed, out Color color))
                {
                    editor.Status = "COLOUR IS #RRGGBB OR #RRGGBBAA";
                    break;
                }
                if (color != particle.Color)
                {
                    Edit(ref editor, particle, () => particle.Color = color);
                    editor.Status = "COLOUR SET";
                }
                break;
        }
    }

    private static string Format(XpfParticle particle, ParticleEditorFieldKind kind) => kind switch
    {
        ParticleEditorFieldKind.Name => particle.Name,
        ParticleEditorFieldKind.Color => XifEditing.FormatFill(particle.Color),
        _ => "",
    };

    private static void SetField(Entity entity, string value)
    {
        ref TextField field = ref entity.GetComponent<TextField>();
        if (field.Value != value)
            field.Value = value;
        UiText.Set(entity, value);
    }

    // ------------------------------------------------------------- sliders

    /// <summary>
    /// A slider that moved off what was last shown was dragged: its value goes
    /// to the effect. Then every slider shows the effect, which may differ
    /// after a drag (a low end pushing its high end) or another selection.
    /// </summary>
    private void SyncSliders(Entity panel, ref ParticleEditor editor, XpfParticle particle)
    {
        bool edited = false;
        foreach (Entity track in Query.Store.Query<ParticleEditorSlider, UiSlider>().ToEntityList())
        {
            ref ParticleEditorSlider binding = ref track.GetComponent<ParticleEditorSlider>();
            if (binding.Editor.Id != panel.Id)
                continue;

            ref UiSlider slider = ref track.GetComponent<UiSlider>();
            if (!float.IsNaN(binding.Shown) && slider.Value != binding.Shown)
            {
                // A drag is one undo step: opened on the first change, closed on the first frame without one.
                if (!editor.Sliding)
                {
                    editor.Parts.Panel.EmitSignal(new EditGestureStarted());
                    editor.Sliding = true;
                }
                XpfEditing.Set(particle, binding.Field, slider.Value);
                edited = true;
            }
        }

        if (edited)
        {
            particle.Touch();
            editor.Parts.Panel.EmitSignal(new EditChanged());
        }
        else if (editor.Sliding)
        {
            editor.Parts.Panel.EmitSignal(new EditGestureEnded());
            editor.Sliding = false;
        }

        foreach (Entity track in Query.Store.Query<ParticleEditorSlider, UiSlider>().ToEntityList())
        {
            ref ParticleEditorSlider binding = ref track.GetComponent<ParticleEditorSlider>();
            if (binding.Editor.Id != panel.Id)
                continue;

            float value = XpfEditing.Get(particle, binding.Field);
            track.GetComponent<UiSlider>().Value = value;
            binding.Shown = value;
        }
    }

    // -------------------------------------------------------------- labels

    private static void SyncLabels(ref ParticleEditor editor, XpfParticle? particle)
    {
        ParticleEditorParts parts = editor.Parts;
        UiText.Set(parts.Title, parts.Panel.GetComponent<ParticleEditorFrame>().Title);
        UiText.Set(parts.Save, ParticleLibrary.Unsaved ? "SAVE *" : "SAVE");
        UiText.Set(parts.Status, editor.Status);
        if (particle is null)
            return;

        UiText.Set(parts.Texture, particle.Texture == XpfTexture.NoteGlyphs ? "NOTES" : "DOTS");
        UiText.Set(parts.Shape, particle.Shape == XpfShape.Box ? "BOX" : "POINT");
        UiText.Set(parts.ScaleCurve, "SIZE " + XpfEditing.ScalePreset(particle).ToString().ToUpperInvariant());
        UiText.Set(parts.Fade, XpfEditing.FadePreset(particle) switch
        {
            XpfFadePreset.Out => "FADE OUT",
            XpfFadePreset.InOut => "FADE IN-OUT",
            _ => "NO FADE",
        });
        Paint(parts.Upright, particle.Upright);
    }

    /// <summary>Bright blue only while a toggle is on; muted at rest, like every editor button.</summary>
    private static void Paint(Entity button, bool on)
    {
        if (button.IsNull || !button.HasComponent<ButtonVisual>())
            return;

        Color color = on ? UiTheme.ButtonActive : UiTheme.Button;
        ref ButtonVisual visual = ref button.GetComponent<ButtonVisual>();
        if (visual.Normal == color)
            return;

        visual.Normal = color;
        visual.Hover = color.Lerp(Colors.White, 0.16f);
    }
}
