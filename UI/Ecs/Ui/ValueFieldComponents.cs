using Friflo.Engine.ECS;
using Xanadu.Core.Rendering;
using Xanadu.Ui.Systems.Canvas;
using Matrix4x4 = System.Numerics.Matrix4x4;

namespace Xanadu.Ui.Ecs.Ui;

/// <summary>What each number of a value field is: a float, an integer, an integer never below zero, or an on/off switch.</summary>
public enum UiScalar
{
    Float,
    Int,
    UInt,
    Bool,
}

/// <summary>
/// The shape of a value a <see cref="UiValueField"/> edits: one scalar kind in
/// <see cref="Rows"/> by <see cref="Columns"/> numbers. A scalar is 1×1, a
/// vector 1×n, a matrix r×c (read row by row); a colour is a four-channel float
/// shown with a picker. It covers every value type a shader uniform can have
/// (<see cref="Parse"/> reads the type names), but knows nothing of shaders.
/// </summary>
public readonly record struct UiValueShape(UiScalar Scalar, int Rows = 1, int Columns = 1, bool Colour = false)
{
    public static readonly UiValueShape Float = new(UiScalar.Float);
    public static readonly UiValueShape Int = new(UiScalar.Int);
    public static readonly UiValueShape UInt = new(UiScalar.UInt);
    public static readonly UiValueShape Bool = new(UiScalar.Bool);
    public static readonly UiValueShape ColourValue = new(UiScalar.Float, 1, 4, Colour: true);

    /// <summary>A row of <paramref name="channels"/> numbers: <c>float2</c>, <c>int3</c>, <c>bool4</c>.</summary>
    public static UiValueShape Vector(int channels, UiScalar scalar = UiScalar.Float) =>
        new(scalar, 1, Math.Clamp(channels, 1, 4));

    /// <summary>A grid of <paramref name="rows"/> by <paramref name="columns"/>: <c>float3x3</c>.</summary>
    public static UiValueShape Matrix(int rows, int columns, UiScalar scalar = UiScalar.Float) =>
        new(scalar, Math.Clamp(rows, 1, 4), Math.Clamp(columns, 1, 4));

    /// <summary>How many numbers the value holds.</summary>
    public int Count => Rows * Columns;

    public bool IsMatrix => Rows > 1;

    /// <summary>Whether its numbers are whole (ints, uints and switches).</summary>
    public bool IsWhole => Scalar != UiScalar.Float;

    /// <summary>
    /// The shape a type name spells, as Slang and HLSL write them:
    /// <c>float</c>, <c>int3</c>, <c>uint2</c>, <c>bool4</c>, <c>float3x3</c>,
    /// <c>half2</c>, <c>colour</c>; null for anything else.
    /// </summary>
    public static UiValueShape? Parse(string type)
    {
        type = type.Trim();
        if (type is "colour" or "color")
            return ColourValue;

        (string Name, UiScalar Scalar)[] scalars =
        [
            ("float", UiScalar.Float), ("half", UiScalar.Float), ("double", UiScalar.Float),
            ("uint", UiScalar.UInt), ("int", UiScalar.Int), ("bool", UiScalar.Bool),
        ];
        foreach ((string name, UiScalar scalar) in scalars)
        {
            if (!type.StartsWith(name, StringComparison.Ordinal))
                continue;

            string rest = type[name.Length..];
            if (rest.Length == 0)
                return new UiValueShape(scalar);
            if (rest.Length == 1 && rest[0] is >= '1' and <= '4')
                return Vector(rest[0] - '0', scalar);
            if (rest.Length == 3 && rest[1] == 'x' && rest[0] is >= '1' and <= '4' && rest[2] is >= '1' and <= '4')
                return Matrix(rest[0] - '0', rest[2] - '0', scalar);
            return null;
        }

        return null;
    }

    /// <summary>
    /// The shape of a material's value as the host reports it; four channels
    /// named like a colour (<see cref="UniformRanges.IsColourName"/>) are a colour.
    /// </summary>
    public static UiValueShape Of(MaterialParam param)
    {
        UiScalar scalar = param.IsBool ? UiScalar.Bool
            : param.IsUnsigned ? UiScalar.UInt
            : param.IsInt ? UiScalar.Int
            : UiScalar.Float;
        if (param.IsMatrix)
            return Matrix(param.Rows, param.Channels, scalar);
        if (scalar == UiScalar.Float && param.Channels == 4 && UniformRanges.IsColourName(param.Name))
            return ColourValue;
        return Vector(param.Channels, scalar);
    }

    /// <summary>What number <paramref name="index"/> is called: <c>x</c>, <c>g</c>, <c>[1][2]</c>, or empty for a scalar.</summary>
    public string Element(int index)
    {
        if (IsMatrix)
            return $"[{index / Columns}][{index % Columns}]";
        if (Count == 1)
            return "";
        return (Colour ? "rgba" : "xyzw")[index].ToString();
    }

    /// <summary>Keeps <paramref name="value"/> to what the scalar can hold: whole, never below zero, 0 or 1.</summary>
    public float Clamp(float value) => Scalar switch
    {
        UiScalar.Int => MathF.Round(value),
        UiScalar.UInt => MathF.Max(0, MathF.Round(value)),
        UiScalar.Bool => value > 0.5f ? 1 : 0,
        _ => Colour ? Math.Clamp(value, 0, 1) : value,
    };
}

/// <summary>
/// A value of any <see cref="UiValueShape"/>, edited in slots: numbers are
/// typed into a slot or dragged sideways on it, switches are toggled, a colour
/// also has a picker, and a whole number alone has − and + steppers. Carried
/// by the field's stack. Each edit is announced with the entity's native
/// <c>OnComponentChanged</c> (an update of this component); see
/// <see cref="UiValueFields.OnValueChanged"/>.
/// </summary>
/// <summary>
/// How a field's value stands against keys, as editors that key values show
/// it: keyed right here, animated (between keys), or changed and not keyed.
/// </summary>
public enum UiValueMark
{
    None,

    /// <summary>A key sits at the current time: yellow.</summary>
    Keyed,

    /// <summary>Keys drive it, but none sits at the current time: green.</summary>
    Animated,

    /// <summary>Edited away from what the keys say and not keyed yet: orange.</summary>
    Changed,
}

public struct UiValueField : IComponent
{
    public UiValueShape Shape;

    /// <summary>The numbers, row by row; <see cref="UiValueShape.Count"/> long.</summary>
    public float[] Values;

    /// <summary>What a right-click on a slot puts back.</summary>
    public float[] Defaults;

    /// <summary>One slot per number: a numeric text field, or a toggle for a switch.</summary>
    public Entity[] Slots;

    /// <summary>The span a slot's bar shows and a drag across 200 px covers.</summary>
    public float Min;
    public float Max;

    /// <summary>The colour picker of a colour, else null.</summary>
    public Entity Picker;

    /// <summary>The − and + steppers of a lone whole number, else null.</summary>
    public Entity Less;
    public Entity More;

    /// <summary>A drag (on a slot or the picker) is still going, so the edit isn't final yet.</summary>
    public bool Live;

    /// <summary>
    /// How the value stands against the keys of whatever owns it, shown as a
    /// tint on its number slots; the owner sets it, the field never does.
    /// </summary>
    public UiValueMark Mark;

    internal bool LessHeld;
    internal bool MoreHeld;
}

/// <summary>One number of a <see cref="UiValueField"/>: which field, which number, and its bar.</summary>
public struct UiValueSlot(Entity field, int index) : IComponent
{
    public Entity Field = field;
    public int Index = index;

    /// <summary>The thin bar along the slot's foot showing where the value sits in the field's range; null for whole numbers.</summary>
    public Entity Bar;

    internal bool WasFocused;

    /// <summary>A switch's toggle as last seen, so a press that flipped it tells apart from a value set in code.</summary>
    internal bool ShownOn;
}

/// <summary>Writes a <see cref="UiValueField"/> into a material's value of that name on every change, as the shader reads it.</summary>
public struct UiMaterialBinding(CanvasMaterial material, string name) : IComponent
{
    public CanvasMaterial Material = material;
    public string Name = name;
}

/// <summary>Reading, setting and listening to value fields.</summary>
public static class UiValueFields
{
    /// <summary>
    /// Calls <paramref name="changed"/> with the values (row by row) each time
    /// they are edited; <c>live</c> is true while a drag is still going, so a
    /// tool can make a whole drag one edit by acting when it is false. It is the
    /// entity's native <c>OnComponentChanged</c>, narrowed to updates of
    /// <see cref="UiValueField"/>; the handler is dropped with the entity.
    /// </summary>
    public static Entity OnValueChanged(this Entity field, Action<float[], bool> changed)
    {
        field.OnComponentChanged += change =>
        {
            if (change.Action == ComponentChangedAction.Update && change.Type == typeof(UiValueField))
            {
                UiValueField value = change.Component<UiValueField>();
                changed(value.Values, value.Live);
            }
        };
        return field;
    }

    /// <summary>Sets the values from code (no change is announced); missing numbers keep theirs.</summary>
    public static void Set(Entity field, ReadOnlySpan<float> values)
    {
        ref UiValueField state = ref field.GetComponent<UiValueField>();
        for (int i = 0; i < Math.Min(values.Length, state.Values.Length); i++)
            state.Values[i] = state.Shape.Clamp(values[i]);
    }

    public static void Set(Entity field, Vector4 value) => Set(field, [value.X, value.Y, value.Z, value.W]);

    public static void Set(Entity field, Matrix4x4 value) => Set(field, FromMatrix(value, field.GetComponent<UiValueField>().Shape));

    /// <summary>A copy of the values, row by row.</summary>
    /// <summary>A setting's name as its field shows it: <c>flare_box</c> reads FLARE BOX.</summary>
    public static string SettingLabel(string name) => name.Replace('_', ' ').ToUpperInvariant();

    /// <summary>A material value's declared default, one number per slot of <paramref name="shape"/> (a matrix starts as identity).</summary>
    public static float[] Defaults(MaterialParam param, UiValueShape shape) =>
        shape.IsMatrix
            ? FromMatrix(Matrix4x4.Identity, shape)
            : new[] { param.Default.X, param.Default.Y, param.Default.Z, param.Default.W }[..shape.Count];

    public static float[] Get(Entity field) => (float[])field.GetComponent<UiValueField>().Values.Clone();

    /// <summary>Up to the first four values, missing ones zero.</summary>
    public static Vector4 AsVector(float[] values) => new(At(values, 0), At(values, 1), At(values, 2), At(values, 3));

    /// <summary>A matrix's values in a <see cref="Matrix4x4"/> read <c>m[row, column]</c>, the rest the identity's.</summary>
    public static Matrix4x4 AsMatrix(float[] values, UiValueShape shape)
    {
        Matrix4x4 matrix = Matrix4x4.Identity;
        for (int i = 0; i < Math.Min(values.Length, shape.Count); i++)
            matrix[i / shape.Columns, i % shape.Columns] = values[i];
        return matrix;
    }

    /// <summary>The numbers of <paramref name="matrix"/> the shape holds, row by row.</summary>
    public static float[] FromMatrix(Matrix4x4 matrix, UiValueShape shape)
    {
        var values = new float[shape.Count];
        for (int i = 0; i < values.Length; i++)
            values[i] = matrix[i / shape.Columns, i % shape.Columns];
        return values;
    }

    /// <summary>A slot's span when none is given: 0..1 for colours and switches, else a few times the biggest value, both ways if any is negative or it is a matrix.</summary>
    public static (float Min, float Max) GuessRange(UiValueShape shape, ReadOnlySpan<float> values)
    {
        if (shape.Colour || shape.Scalar == UiScalar.Bool)
            return (0, 1);

        float biggest = 0;
        bool negative = false;
        foreach (float value in values)
        {
            biggest = MathF.Max(biggest, MathF.Abs(value));
            negative |= value < 0;
        }

        (float min, float max) = UniformRanges.Guess(false, biggest);
        if (shape.Scalar == UiScalar.UInt)
            return (0, MathF.Max(max, 8));
        if (shape.Scalar == UiScalar.Int)
            max = MathF.Max(max, 8);
        return negative || shape.IsMatrix ? (-max, max) : (min, max);
    }

    private static float At(float[] values, int index) => index < values.Length ? values[index] : 0;
}
