using System;
using Xanadu.Ui.Systems.Canvas;

namespace Xanadu.Ui.Ecs.Ui.Tree;

/// <summary>
/// A value of any shape (<see cref="UiNode.ValueField"/>): a number, a vector as
/// a row of slots, a matrix as a grid, switches as toggles, a colour with its
/// picker. Its node carries <see cref="UiValueField"/>; listen with
/// <see cref="UiValueFields.OnValueChanged"/>.
/// </summary>
public sealed class ValueField(
    string label,
    UiValueShape shape,
    float[] initial,
    float width,
    (float Min, float Max)? range = null,
    string? tip = null)
    : UiSpec(tip: tip)
{
    protected override UiNode Create(UiNode parent) => parent.ValueField(label, shape, initial, width, range);
}

/// <summary>A material's value as a <see cref="ValueField"/> shaped as the host reports it and bound to it (<see cref="UiNode.UniformField"/>).</summary>
public sealed class UniformField(CanvasMaterial material, MaterialParam param, float width, (float Min, float Max)? range = null)
    : UiSpec
{
    protected override UiNode Create(UiNode parent) => parent.UniformField(material, param, width, range);
}
