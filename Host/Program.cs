using Xanadu.Host;

// dotnet run --project Host                         -> window on the profiles screen; close it to quit
// dotnet run --project Host -- --demo               -> plays a level made in memory (notes on the beat)
// dotnet run --project Host -- --play[=<name>]      -> plays the first saved level (whose name or file contains <name>)
// dotnet run --project Host -- --shot=<path.png>    -> saves frame 60 and quits (--shot-frame=<n> to pick)
// dotnet run --project Host -- --resolution=1920x1080
// dotnet run --project Host -- --scene=<name>       -> starts on that screen (a SceneKind name)

// MoonWorks logs every tick that runs late; with one update per tick that is
// every slow frame, which buries everything else in the console.
System.Action<string> info = MoonWorks.Logger.LogInfo;
MoonWorks.Logger.LogInfo = message =>
{
    if (!message.StartsWith("Exceeded max update time", System.StringComparison.Ordinal))
        info(message);
};

new HostGame(HostOptions.Parse(args)).Run();
