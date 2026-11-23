using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Xanadu.Ui.Terminal;

/// <summary>What an argument may be; the line is checked against it before a command runs.</summary>
public enum TerminalArgKind
{
    /// <summary>on or off (also true/false, yes/no, 1/0).</summary>
    Switch,

    /// <summary>A whole number.</summary>
    Whole,

    /// <summary>Any number.</summary>
    Number,

    /// <summary>One of a fixed list of words; the command gets its index.</summary>
    Choice,

    /// <summary>Any one word.</summary>
    Word,
}

/// <summary>One argument a command takes: its name for the help, what it may be, and whether it can be left out.</summary>
public sealed record TerminalArg(string Name, TerminalArgKind Kind, string[]? Choices = null, bool Optional = false)
{
    public static TerminalArg Switch(string name, bool optional = false) => new(name, TerminalArgKind.Switch, null, optional);

    public static TerminalArg Whole(string name, bool optional = false) => new(name, TerminalArgKind.Whole, null, optional);

    public static TerminalArg Number(string name, bool optional = false) => new(name, TerminalArgKind.Number, null, optional);

    public static TerminalArg Word(string name, bool optional = false) => new(name, TerminalArgKind.Word, null, optional);

    public static TerminalArg Choice(string name, string[] choices, bool optional = false) =>
        new(name, TerminalArgKind.Choice, choices, optional);

    /// <summary>How the help shows it: &lt;name&gt;, [name] when optional, a choice's words joined by |.</summary>
    public string Usage => Shown(full: true);

    /// <summary>The same, but a long list of choices is just its name, for the help's one-line list.</summary>
    public string ShortUsage => Shown(full: false);

    private string Shown(bool full)
    {
        string choices = string.Join("|", Choices ?? []);
        string inner = Kind switch
        {
            TerminalArgKind.Switch => "on|off",
            TerminalArgKind.Choice when full || choices.Length <= 32 => choices,
            _ => Name,
        };
        return Optional ? "[" + inner + "]" : "<" + inner + ">";
    }
}

/// <summary>One argument as the line gave it, already parsed to its kind.</summary>
public readonly record struct TerminalValue(TerminalArgKind Kind, bool Switch, long Whole, double Number, int Choice, string Word);

/// <summary>A command's arguments, parsed: read each by its position as the kind it was declared.</summary>
public readonly struct TerminalValues(TerminalValue[] values)
{
    private readonly TerminalValue[] _values = values;

    public int Count => _values.Length;

    /// <summary>Whether optional argument <paramref name="i"/> was given.</summary>
    public bool Has(int i) => i < _values.Length;

    public bool Switch(int i) => _values[i].Switch;

    public long Whole(int i) => _values[i].Whole;

    public double Number(int i) => _values[i].Number;

    /// <summary>The index of the word chosen, in the argument's choices.</summary>
    public int Choice(int i) => _values[i].Choice;

    public string Word(int i) => _values[i].Word;
}

/// <summary>
/// A command: its name, a line of help, the arguments it takes, and what it
/// does with them once they parse. It answers with lines for the log (empty
/// for nothing to say).
/// </summary>
public sealed record TerminalCommand(string Name, string Help, TerminalArg[] Args, Func<TerminalValues, string> Run)
{
    public string Usage => Args.Length == 0 ? Name : Name + " " + string.Join(" ", Args.Select(a => a.Usage));

    /// <summary><see cref="Usage"/> with long choice lists shortened to their names.</summary>
    public string ShortUsage => Args.Length == 0 ? Name : Name + " " + string.Join(" ", Args.Select(a => a.ShortUsage));
}

/// <summary>What running a line gave: the lines to log, and whether it went wrong.</summary>
public readonly record struct TerminalReply(string Text, bool Failed);

/// <summary>
/// The commands a terminal knows. Whoever owns the terminal (the host) adds
/// its own; anything else it lends the registry to adds more. A line is split
/// and parsed here, once: commands only ever see typed values.
/// </summary>
public sealed class TerminalCommands
{
    private readonly SortedDictionary<string, TerminalCommand> _commands = new(StringComparer.OrdinalIgnoreCase);

    public TerminalCommands()
    {
        Add(new TerminalCommand("help", "Lists the commands, or shows how to use one",
            [TerminalArg.Word("command", optional: true)], Help));
    }

    public IEnumerable<TerminalCommand> All => _commands.Values;

    /// <summary>Adds a command, replacing one of the same name.</summary>
    public void Add(TerminalCommand command) => _commands[command.Name] = command;

    public void Remove(string name) => _commands.Remove(name);

    /// <summary>Parses and runs one line.</summary>
    public TerminalReply Run(string line)
    {
        string[] words = Split(line);
        if (words.Length == 0)
            return new TerminalReply("", false);

        if (!_commands.TryGetValue(words[0], out TerminalCommand? command))
            return new TerminalReply($"No command '{words[0]}'. Type help for the list.", true);

        if (Parse(command, words.AsSpan(1), out TerminalValue[] values) is { } problem)
            return new TerminalReply(problem + "\nusage: " + command.Usage, true);

        try
        {
            return new TerminalReply(command.Run(new TerminalValues(values)), false);
        }
        catch (Exception exception)
        {
            return new TerminalReply($"{command.Name} failed: {exception.Message}", true);
        }
    }

    /// <summary>
    /// What a line could go on to, for Tab: command names while the first word
    /// is typed, then the current argument's choices (switches offer on and off).
    /// </summary>
    public IReadOnlyList<string> Complete(string line)
    {
        string[] words = Split(line);
        bool fresh = line.Length == 0 || char.IsWhiteSpace(line[^1]);
        if (words.Length == 0 || (words.Length == 1 && !fresh))
        {
            string start = words.Length == 0 ? "" : words[0];
            return _commands.Keys.Where(k => k.StartsWith(start, StringComparison.OrdinalIgnoreCase)).ToList();
        }

        if (!_commands.TryGetValue(words[0], out TerminalCommand? command))
            return [];

        int index = fresh ? words.Length - 1 : words.Length - 2;
        if (index >= command.Args.Length)
            return [];

        string typed = fresh ? "" : words[^1];
        TerminalArg arg = command.Args[index];
        string[] options = arg.Kind switch
        {
            TerminalArgKind.Switch => ["on", "off"],
            TerminalArgKind.Choice => arg.Choices ?? [],
            TerminalArgKind.Word when command.Name == "help" => [.. _commands.Keys],
            _ => [],
        };
        return options.Where(o => o.StartsWith(typed, StringComparison.OrdinalIgnoreCase)).ToList();
    }

    private static string[] Split(string line) =>
        line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    /// <summary>Each word as its argument's kind; the problem, or null when all of them fit.</summary>
    private static string? Parse(TerminalCommand command, ReadOnlySpan<string> words, out TerminalValue[] values)
    {
        values = [];
        TerminalArg[] args = command.Args;
        if (words.Length > args.Length)
            return $"{command.Name} takes at most {args.Length} argument{(args.Length == 1 ? "" : "s")}.";

        int needed = args.Count(a => !a.Optional);
        if (words.Length < needed)
            return $"{command.Name} needs {args[words.Length].Usage}.";

        var parsed = new TerminalValue[words.Length];
        for (int i = 0; i < words.Length; i++)
        {
            TerminalArg arg = args[i];
            string word = words[i];
            switch (arg.Kind)
            {
                case TerminalArgKind.Switch:
                    bool? on = word.ToLowerInvariant() switch
                    {
                        "on" or "true" or "yes" or "1" => true,
                        "off" or "false" or "no" or "0" => false,
                        _ => null,
                    };
                    if (on is null)
                        return $"'{word}' isn't on or off.";
                    parsed[i] = new TerminalValue(arg.Kind, on.Value, 0, 0, 0, word);
                    break;
                case TerminalArgKind.Whole:
                    if (!long.TryParse(word, NumberStyles.Integer, CultureInfo.InvariantCulture, out long whole))
                        return $"'{word}' isn't a whole number.";
                    parsed[i] = new TerminalValue(arg.Kind, false, whole, whole, 0, word);
                    break;
                case TerminalArgKind.Number:
                    if (!double.TryParse(word, NumberStyles.Float, CultureInfo.InvariantCulture, out double number))
                        return $"'{word}' isn't a number.";
                    parsed[i] = new TerminalValue(arg.Kind, false, (long)number, number, 0, word);
                    break;
                case TerminalArgKind.Choice:
                    int choice = Array.FindIndex(arg.Choices ?? [], c => string.Equals(c, word, StringComparison.OrdinalIgnoreCase));
                    if (choice < 0)
                        return $"'{word}' isn't one of {string.Join(", ", arg.Choices ?? [])}.";
                    parsed[i] = new TerminalValue(arg.Kind, false, 0, 0, choice, word);
                    break;
                default:
                    parsed[i] = new TerminalValue(arg.Kind, false, 0, 0, 0, word);
                    break;
            }
        }

        values = parsed;
        return null;
    }

    private string Help(TerminalValues args)
    {
        if (args.Has(0))
        {
            return _commands.TryGetValue(args.Word(0), out TerminalCommand? command)
                ? command.Usage + "\n  " + command.Help
                : $"No command '{args.Word(0)}'.";
        }

        int width = _commands.Values.Max(c => c.ShortUsage.Length);
        return string.Join("\n", _commands.Values.Select(c => c.ShortUsage.PadRight(width + 3) + c.Help));
    }
}
