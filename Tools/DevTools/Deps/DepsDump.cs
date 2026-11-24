using System.Linq;

namespace Xanadu.Tools.DevTools.Deps;

/// <summary>DEPS as text, without a window: every built DLL by level, or one DLL's neighbours.</summary>
internal static class DepsDump
{
    public static void Print(string root, string? focus)
    {
        DepsGraph graph = DepsGraph.Read(root);
        if (graph.Cycle is { } cycle)
            Console.WriteLine($"CYCLE: {cycle}");

        if (focus is not null)
        {
            if (graph.Find(focus) is not { } one)
            {
                Console.WriteLine($"no built assembly named {focus}");
                return;
            }

            Console.WriteLine($"{one.Short} (level {one.Level}, built {DepsGraph.Age(one.Built)} ago)");
            Console.WriteLine($"  uses     {DepsGraph.List(one.Uses)}");
            Console.WriteLine($"  through  {DepsGraph.List(graph.Through(one).Select(a => a.Name))}");
            Console.WriteLine($"  used by  {DepsGraph.List(graph.UsedBy(one).Select(a => a.Name))}");
            return;
        }

        int width = graph.Assemblies.Max(a => a.Short.Length);
        foreach (DepsAssembly a in graph.Assemblies)
            Console.WriteLine($"{a.Level,2}  {a.Short.PadRight(width)}  {DepsGraph.Age(a.Built),4}  {DepsGraph.List(a.Uses)}");
    }
}
