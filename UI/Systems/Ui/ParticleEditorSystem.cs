using System;
using System.Collections.Generic;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Xanadu.Ecs;
using Xanadu.Items;
using Xanadu.Particles;
using Xanadu.Theme;
using Xanadu.Ui.Ecs.Ui;
using Position = Xanadu.Ecs.Position;

namespace Xanadu.Systems.Ui;

/// <summary>
/// Runs every particle editor panel: shows it while its host has a selection,
/// applies its buttons, fields and sliders to the shared library's effect, and
/// keeps its readouts in step with that effect. Choosing another effect goes
/// through the host, which records it however its tool does undo; changes to
/// the effect itself go to the library, which every tool and the game share.
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
        ref ParticleEditor editor = ref panel.GetComponent<ParticleEditor>();
        if (editor.Host.Particle is null)
        {
            UiLayout.SetHidden(panel, true);
            return;
        }

        if (UiLayout.Move(panel, editor.Home))
            panel.AddTag<Dirty>();
        UiLayout.SetHidden(panel, false);

        RunActions(panel, ref editor);
        XpfParticle? particle = ParticleLibrary.Find(editor.Host.Particle ?? "");
        SyncFields(panel, ref editor, particle);
        if (particle is not null)
            SyncSliders(panel, particle);
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
        string? name = editor.Host.Particle;
        XpfParticle? particle = name is null ? null : library.Find(name);
        switch (kind)
        {
            case ParticleEditorActionKind.Previous:
            case ParticleEditorActionKind.Next:
                if (XpfEditing.Step(library, name, kind == ParticleEditorActionKind.Next ? 1 : -1) is { } other && other != name)
                {
                    editor.Host.Use(other);
                    editor.Status = $"PLAYS {other}";
                }
                break;
            case ParticleEditorActionKind.New:
                string added = XpfEditing.Add(library);
                ParticleLibrary.MarkEdited();
                editor.Host.Use(added);
                editor.Status = $"ADDED {added}";
                break;
            case ParticleEditorActionKind.Copy:
                if (name is not null && XpfEditing.Copy(library, name) is { } copy)
                {
                    ParticleLibrary.MarkEdited();
                    editor.Host.Use(copy);
                    editor.Status = $"COPIED TO {copy}";
                }
                break;
            case ParticleEditorActionKind.Save:
                try
                {
                    ParticleLibrary.Save();
                    editor.Status = "LIBRARY SAVED";
                }
                catch (Exception exception) when (exception is System.IO.IOException or XpfFormatException
                                                      or UnauthorizedAccessException)
                {
                    editor.Status = "SAVE FAILED: " + exception.Message;
                }
                break;
            case ParticleEditorActionKind.Revert:
                ParticleLibrary.Reload();
                editor.Status = "LIBRARY RELOADED";
                break;
            case ParticleEditorActionKind.Texture when particle is not null:
                particle.Texture = XpfEditing.Next(particle.Texture);
                ParticleLibrary.MarkEdited();
                break;
            case ParticleEditorActionKind.Shape when particle is not null:
                particle.Shape = XpfEditing.Next(particle.Shape);
                ParticleLibrary.MarkEdited();
                break;
            case ParticleEditorActionKind.Upright when particle is not null:
                particle.Upright = !particle.Upright;
                ParticleLibrary.MarkEdited();
                break;
            case ParticleEditorActionKind.ScaleCurve when particle is not null:
                XpfEditing.SetScalePreset(particle, XpfEditing.Next(XpfEditing.ScalePreset(particle)));
                ParticleLibrary.MarkEdited();
                break;
            case ParticleEditorActionKind.Fade when particle is not null:
                XpfEditing.SetFadePreset(particle, XpfEditing.Next(XpfEditing.FadePreset(particle)));
                ParticleLibrary.MarkEdited();
                break;
        }
    }

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
                string old = particle.Name;
                if (typed.Trim() == old)
                    break;
                if (!XpfEditing.Rename(ParticleLibrary.Shared, old, typed))
                {
                    editor.Status = "NAME IS TAKEN OR BLANK";
                    break;
                }
                ParticleLibrary.MarkEdited();
                editor.Host.Use(particle.Name);
                editor.Status = $"RENAMED; OTHERS STILL PLAY {old}";
                break;
            case ParticleEditorFieldKind.Color:
                if (!XifEditing.TryParseFill(typed, out Color color))
                {
                    editor.Status = "COLOUR IS #RRGGBB OR #RRGGBBAA";
                    break;
                }
                if (color != particle.Color)
                {
                    particle.Color = color;
                    ParticleLibrary.MarkEdited();
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
    private void SyncSliders(Entity panel, XpfParticle particle)
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
                XpfEditing.Set(particle, binding.Field, slider.Value);
                edited = true;
            }
        }

        if (edited)
            ParticleLibrary.MarkEdited();

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
        string? name = editor.Host.Particle;
        UiText.Set(parts.Title, editor.Host.Title);
        UiText.Set(parts.Save, ParticleLibrary.Unsaved ? "SAVE *" : "SAVE");
        UiText.Set(parts.Status, particle is null ? $"NO EFFECT CALLED {name}" : editor.Status);
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
