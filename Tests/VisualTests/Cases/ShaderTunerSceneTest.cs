using System.Collections.Generic;
using System.Linq;
using Friflo.Engine.ECS;
using Godot;
using Xanadu.Ecs;
using Xanadu.Systems.Input;
using Xanadu.Systems.Shaders;

namespace Xanadu.Tests.VisualTests;

/// <summary>
/// Three materials sharing one shader, as song select's note art does. Tuning
/// one in the F2 panel must leave the others alone, and opening the panel must
/// change nothing. Needs real materials, which a headless NUnit run does not
/// have; the shader is built here because this project does not ship the game's.
/// </summary>
public sealed class ShaderTunerSceneTest : SceneTest
{
    private const float Nudge = 0.37f;

    private readonly List<Entity> _owners = new();
    private Dictionary<ShaderMaterial, (float Speed, Color Notes)> _before = new();

    public override string Name => "Shader tuner tunes one material";
    public override string Slug => "shader-tuner";

    protected override void SetUpSteps()
    {
        AddStep("make three materials of one shader", MakeMaterials);
        AddStep("note every material's values", Remember);

        Toggle("open the tuner");
        AddUntilStep("wait for the panel", () => Buttons(ShaderTunerAction.Reset).Any());
        AddAssert("opening the panel changes no material", () => Changed().Count == 0);
        AddAssert("the panel has a way to the other materials", () => Buttons(ShaderTunerAction.Next).Any());

        AddStep("move the speed slider", () => MoveSpeed(Nudge));
        AddUntilStep("wait for one material to change", () => Changed().Count > 0);
        AddAssert("only that one material changed", () => Changed().Count == 1);

        AddStep("press NEXT", () => Buttons(ShaderTunerAction.Next).First().AddTag<Pressed>());
        AddStep("move the next material's speed slider", () => MoveSpeed(Nudge));
        AddUntilStep("wait for the second material to change", () => Changed().Count == 2);
        AddAssert("the two edits stayed apart", () => Changed().Count == 2);
        AddAssert("no colour was touched", NoColourChanged);

        AddStep("reset the second material", () => Buttons(ShaderTunerAction.Reset).First().AddTag<Pressed>());
        AddStep("press PREV", () => Buttons(ShaderTunerAction.Previous).First().AddTag<Pressed>());
        AddStep("reset the first material", () => Buttons(ShaderTunerAction.Reset).First().AddTag<Pressed>());
        Toggle("close the tuner");
        AddUntilStep("wait for the panel to close", () => !Buttons(ShaderTunerAction.Reset).Any());
        AddStep("remove the materials", () =>
        {
            foreach (Entity owner in _owners)
                owner.DeleteEntity();

            _owners.Clear();
        });
    }

    private void MakeMaterials()
    {
        var shader = new Shader
        {
            Code = """
                shader_type canvas_item;
                uniform float speed = 1.0;
                uniform vec4 note_color : source_color = vec4(1.0);
                """,
            ResourcePath = "res://visual-test/shader_tuner.gdshader",
        };

        string[] names = ["background", "level art", "player art"];
        for (int i = 0; i < names.Length; i++)
        {
            var material = new ShaderMaterial { Shader = shader };
            material.SetMeta(ShaderMaterials.InstanceMeta, names[i]);
            material.SetShaderParameter("speed", 1f + i);
            material.SetShaderParameter("note_color", new Color(i / 2f, 1f, 0f));

            Entity owner = Context.Entities.CreateEntity();
            owner.AddComponent(new UiMaterial(material));
            _owners.Add(owner);
        }
    }

    private void Toggle(string name)
    {
        AddStep(name + ": press F2", () => Context.Keyboard.Press(DebugKeys.ShaderTuner));
        AddStep(name + ": release F2", () => Context.Keyboard.Release(DebugKeys.ShaderTuner));
    }

    private void Remember() => _before = _owners
        .Select(owner => (ShaderMaterial)owner.GetComponent<UiMaterial>().Material)
        .ToDictionary(
            material => material,
            material => (Speed(material), Notes(material)));

    private List<ShaderMaterial> Changed() => _before
        .Where(entry => !Mathf.IsEqualApprox(Speed(entry.Key), entry.Value.Speed))
        .Select(entry => entry.Key)
        .ToList();

    private bool NoColourChanged() =>
        _before.All(entry => Notes(entry.Key).IsEqualApprox(entry.Value.Notes));

    private static float Speed(ShaderMaterial material) =>
        material.GetShaderParameter("speed").AsSingle();

    private static Color Notes(ShaderMaterial material) =>
        material.GetShaderParameter("note_color").AsColor();

    private void MoveSpeed(float by)
    {
        foreach (Entity slider in Context.Entities.Query<UiSlider, ShaderParam>().ToEntityList())
        {
            if (slider.GetComponent<ShaderParam>().Uniform == "speed")
                slider.GetComponent<UiSlider>().Value += by;
        }
    }

    private IEnumerable<Entity> Buttons(ShaderTunerAction action) => Context.Entities
        .Query<ShaderTunerButton>()
        .WithDisabled()
        .ToEntityList()
        .Where(button => button.GetComponent<ShaderTunerButton>().Action == action);
}
