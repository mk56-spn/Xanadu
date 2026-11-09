using Xanadu.Host;

// dotnet run --project Host                         -> window on the profiles screen; close it to quit
// dotnet run --project Host -- --demo               -> plays a level made in memory (notes on the beat)
// dotnet run --project Host -- --play[=<name>]      -> plays the first saved level (whose name or file contains <name>)
// dotnet run --project Host -- --shot=<path.png>    -> saves frame 60 and quits (--shot-frame=<n> to pick)
// dotnet run --project Host -- --resolution=1920x1080
Xanadu.Host.Diagnostics.GodotEngineTrap.Install();
new HostGame(HostOptions.Parse(args)).Run();
