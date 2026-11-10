using Xanadu.Host;
using Xanadu.Tools.Autopsy;

// dotnet run --project Tools/Autopsy                    -> the last launch of each exe, most recent first
// dotnet run --project Tools/Autopsy -- --shot=<png>    -> saves frame 60 and quits (--shot-frame=<n> to pick)
// dotnet run --project Tools/Autopsy -- --methods        -> opens on the METHODS view
// dotnet run --project Tools/Autopsy -- --dump[=app]     -> prints the latest (or that app's) capture as text, no window
//                                         --under=<name>  -> with --dump: the methods run beneath methods named like that
string? shot = null;
int frame = 60;
bool methods = false;
foreach (string arg in args)
{
    if (arg == "--dump" || arg.StartsWith("--dump=", System.StringComparison.Ordinal))
    {
        HostApp.UseGameRoots();
        string? under = System.Array.Find(args, a => a.StartsWith("--under=", System.StringComparison.Ordinal))?["--under=".Length..];
        AutopsyDump.Print(arg.Length > "--dump=".Length ? arg["--dump=".Length..] : null, under);
        return;
    }
    if (arg.StartsWith("--under=", System.StringComparison.Ordinal))
        continue;
    if (arg == "--methods")
        methods = true;
    else if (arg.StartsWith("--shot=", System.StringComparison.Ordinal))
        shot = System.IO.Path.GetFullPath(arg["--shot=".Length..]);
    else if (arg.StartsWith("--shot-frame=", System.StringComparison.Ordinal))
        frame = int.Parse(arg["--shot-frame=".Length..]);
    else
        throw new System.ArgumentException($"Unknown argument: {arg}");
}
new AutopsyApp(new HostWindowOptions(1600, 900, shot, frame), methods).Run();
