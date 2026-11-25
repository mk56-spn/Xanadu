using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Xanadu.Slang;

/// <summary>A number as a BUILD brick's sizes and schedule see it: whole (as Slang's uint and int) or not.</summary>
public readonly record struct BuildValue(double Number, bool Whole)
{
    public static BuildValue Of(long value) => new(value, true);

    public static BuildValue Of(bool value) => new(value ? 1 : 0, true);

    public long Long => (long)Number;

    public bool IsTrue => Number != 0;

    public override string ToString() => Whole ? Long.ToString(CultureInfo.InvariantCulture) : Number.ToString(CultureInfo.InvariantCulture);
}

/// <summary>
/// An expression the CPU works out from a BUILD brick: a list's size, a
/// <c>[Pass]</c>'s threads, a schedule's loop bounds, conditions and arguments. The C
/// operators over numbers and names (the brick's <c>static const</c>s, its uniforms,
/// the schedule's loop variables); whole numbers stay whole, so <c>count / 256</c> divides as Slang does.
/// </summary>
public abstract class BuildExpression
{
    /// <summary>The value, names looked up in <paramref name="lookup"/>; throws naming an unknown one.</summary>
    public abstract BuildValue Evaluate(Func<string, BuildValue?> lookup);

    /// <summary>Every name the expression reads.</summary>
    public abstract IEnumerable<string> Names { get; }

    /// <summary>Parses one whole expression from <paramref name="text"/>; throws on anything left over.</summary>
    public static BuildExpression Parse(string text)
    {
        var tokens = new BuildTokens(text, 0);
        BuildExpression parsed = tokens.Expression();
        if (!tokens.AtEnd)
            throw new FormatException($"unexpected '{tokens.Peek.Text}' in '{text.Trim()}'");
        return parsed;
    }

    internal sealed class Number(BuildValue value) : BuildExpression
    {
        public override BuildValue Evaluate(Func<string, BuildValue?> lookup) => value;

        public override IEnumerable<string> Names => [];
    }

    internal sealed class Name(string name) : BuildExpression
    {
        public override BuildValue Evaluate(Func<string, BuildValue?> lookup) =>
            lookup(name) ?? throw new FormatException($"'{name}' isn't a number the CPU knows here: a static const, a uniform or a loop's variable");

        public override IEnumerable<string> Names => [name];
    }

    internal sealed class Unary(string op, BuildExpression operand) : BuildExpression
    {
        public override BuildValue Evaluate(Func<string, BuildValue?> lookup)
        {
            BuildValue value = operand.Evaluate(lookup);
            return op switch
            {
                "-" => new BuildValue(-value.Number, value.Whole),
                "+" => value,
                "!" => BuildValue.Of(!value.IsTrue),
                "~" => BuildValue.Of((long)~(uint)value.Long),
                _ => throw new FormatException($"unknown operator '{op}'"),
            };
        }

        public override IEnumerable<string> Names => operand.Names;
    }

    internal sealed class Binary(string op, BuildExpression left, BuildExpression right) : BuildExpression
    {
        public override BuildValue Evaluate(Func<string, BuildValue?> lookup)
        {
            BuildValue a = left.Evaluate(lookup);
            // Short-circuit, as Slang does: the side not taken may name what isn't there.
            if (op == "&&")
                return BuildValue.Of(a.IsTrue && right.Evaluate(lookup).IsTrue);
            if (op == "||")
                return BuildValue.Of(a.IsTrue || right.Evaluate(lookup).IsTrue);
            BuildValue b = right.Evaluate(lookup);
            bool whole = a.Whole && b.Whole;
            return op switch
            {
                "+" => new BuildValue(a.Number + b.Number, whole),
                "-" => new BuildValue(a.Number - b.Number, whole),
                "*" => new BuildValue(a.Number * b.Number, whole),
                "/" when whole => b.Long == 0 ? throw new FormatException("division by zero") : BuildValue.Of(a.Long / b.Long),
                "/" => new BuildValue(a.Number / b.Number, false),
                "%" => b.Long == 0 ? throw new FormatException("division by zero") : BuildValue.Of(a.Long % b.Long),
                "<<" => BuildValue.Of(a.Long << (int)b.Long),
                ">>" => BuildValue.Of(a.Long >> (int)b.Long),
                "&" => BuildValue.Of(a.Long & b.Long),
                "|" => BuildValue.Of(a.Long | b.Long),
                "^" => BuildValue.Of(a.Long ^ b.Long),
                "<" => BuildValue.Of(a.Number < b.Number),
                "<=" => BuildValue.Of(a.Number <= b.Number),
                ">" => BuildValue.Of(a.Number > b.Number),
                ">=" => BuildValue.Of(a.Number >= b.Number),
                "==" => BuildValue.Of(a.Number == b.Number),
                "!=" => BuildValue.Of(a.Number != b.Number),
                _ => throw new FormatException($"unknown operator '{op}'"),
            };
        }

        public override IEnumerable<string> Names => [.. left.Names, .. right.Names];
    }

    internal sealed class Choice(BuildExpression test, BuildExpression yes, BuildExpression no) : BuildExpression
    {
        public override BuildValue Evaluate(Func<string, BuildValue?> lookup) =>
            test.Evaluate(lookup).IsTrue ? yes.Evaluate(lookup) : no.Evaluate(lookup);

        public override IEnumerable<string> Names => [.. test.Names, .. yes.Names, .. no.Names];
    }

    /// <summary><c>min(a, b)</c> and <c>max(a, b)</c>: the two functions a size most often needs.</summary>
    internal sealed class Call(string function, BuildExpression[] arguments) : BuildExpression
    {
        public override BuildValue Evaluate(Func<string, BuildValue?> lookup)
        {
            if (arguments.Length != 2)
                throw new FormatException($"{function} takes two numbers");
            BuildValue a = arguments[0].Evaluate(lookup), b = arguments[1].Evaluate(lookup);
            bool first = function == "min" ? a.Number <= b.Number : a.Number >= b.Number;
            return first ? a : b;
        }

        public override IEnumerable<string> Names => [.. arguments.SelectMany(argument => argument.Names)];
    }
}

/// <summary>One token of a BUILD brick's expressions and schedule, with where it starts in the brick.</summary>
internal readonly record struct BuildToken(string Text, int Index, bool IsName, bool IsNumber);

/// <summary>Tokens over a piece of text, and the expression grammar over them (C's precedence).</summary>
internal sealed class BuildTokens
{
    private static readonly string[] Operators =
        ["<<=", ">>=", "<<", ">>", "<=", ">=", "==", "!=", "&&", "||", "++", "--", "+=", "-=", "*=", "/=", "|=", "&="];

    private readonly List<BuildToken> _tokens = [];
    private int _at;

    /// <param name="offset">Where <paramref name="text"/> starts in the brick, so each token knows its place there.</param>
    public BuildTokens(string text, int offset)
    {
        int i = 0;
        while (i < text.Length)
        {
            char c = text[i];
            if (char.IsWhiteSpace(c))
            {
                i++;
                continue;
            }

            int start = i;
            if (char.IsLetter(c) || c == '_')
            {
                while (i < text.Length && (char.IsLetterOrDigit(text[i]) || text[i] == '_'))
                    i++;
                _tokens.Add(new BuildToken(text[start..i], offset + start, true, false));
                continue;
            }

            if (char.IsDigit(c) || (c == '.' && i + 1 < text.Length && char.IsDigit(text[i + 1])))
            {
                while (i < text.Length && (char.IsLetterOrDigit(text[i]) || text[i] == '.'))
                    i++;
                _tokens.Add(new BuildToken(text[start..i], offset + start, false, true));
                continue;
            }

            string op = Operators.FirstOrDefault(o => string.CompareOrdinal(text, i, o, 0, o.Length) == 0) ?? c.ToString();
            i += op.Length;
            _tokens.Add(new BuildToken(op, offset + start, false, false));
        }
    }

    public bool AtEnd => _at >= _tokens.Count;

    public BuildToken Peek => AtEnd ? new BuildToken("end", _tokens.Count > 0 ? _tokens[^1].Index : 0, false, false) : _tokens[_at];

    public BuildToken Next() => AtEnd ? throw new FormatException("the code ends too soon") : _tokens[_at++];

    public bool Is(string text) => !AtEnd && _tokens[_at].Text == text;

    public bool Take(string text)
    {
        if (!Is(text))
            return false;
        _at++;
        return true;
    }

    public BuildToken Expect(string text) =>
        Is(text) ? Next() : throw new FormatException($"expected '{text}', found '{Peek.Text}'");

    public string ExpectName() =>
        Peek.IsName ? Next().Text : throw new FormatException($"expected a name, found '{Peek.Text}'");

    private static readonly string[][] Levels =
    [
        ["||"], ["&&"], ["|"], ["^"], ["&"], ["==", "!="], ["<", "<=", ">", ">="], ["<<", ">>"], ["+", "-"], ["*", "/", "%"],
    ];

    public BuildExpression Expression()
    {
        BuildExpression test = Level(0);
        if (!Take("?"))
            return test;
        BuildExpression yes = Expression();
        Expect(":");
        return new BuildExpression.Choice(test, yes, Expression());
    }

    private BuildExpression Level(int level)
    {
        if (level == Levels.Length)
            return Unary();
        BuildExpression left = Level(level + 1);
        while (!AtEnd && Levels[level].Contains(Peek.Text))
        {
            string op = Next().Text;
            left = new BuildExpression.Binary(op, left, Level(level + 1));
        }

        return left;
    }

    private BuildExpression Unary()
    {
        if (Is("-") || Is("+") || Is("!") || Is("~"))
            return new BuildExpression.Unary(Next().Text, Unary());
        if (Take("("))
        {
            // A cast: (uint)x, (int)(…), (float)y.
            if (Peek.Text is "uint" or "int" or "float" && _at + 1 < _tokens.Count && _tokens[_at + 1].Text == ")")
            {
                string type = Next().Text;
                Expect(")");
                BuildExpression cast = Unary();
                return type == "float" ? cast : new BuildExpression.Binary("|", cast, new BuildExpression.Number(BuildValue.Of(0)));
            }

            BuildExpression inner = Expression();
            Expect(")");
            return inner;
        }

        BuildToken token = Next();
        if (token.IsNumber)
            return new BuildExpression.Number(ParseNumber(token.Text));
        if (!token.IsName)
            throw new FormatException($"unexpected '{token.Text}'");
        if (token.Text is "true" or "false")
            return new BuildExpression.Number(BuildValue.Of(token.Text == "true"));
        if (token.Text is "min" or "max" && Take("("))
        {
            List<BuildExpression> arguments = [Expression()];
            while (Take(","))
                arguments.Add(Expression());
            Expect(")");
            return new BuildExpression.Call(token.Text, [.. arguments]);
        }

        return new BuildExpression.Name(token.Text);
    }

    private static BuildValue ParseNumber(string text)
    {
        string bare = text.TrimEnd('u', 'U', 'l', 'L');
        if (bare.StartsWith("0x", StringComparison.OrdinalIgnoreCase) &&
            long.TryParse(bare[2..], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out long hex))
            return BuildValue.Of(hex);
        if (long.TryParse(bare, NumberStyles.None, CultureInfo.InvariantCulture, out long whole))
            return BuildValue.Of(whole);
        if (double.TryParse(text.TrimEnd('f', 'F', 'h', 'H'), NumberStyles.Float, CultureInfo.InvariantCulture, out double real))
            return new BuildValue(real, false);
        throw new FormatException($"'{text}' isn't a number");
    }
}
