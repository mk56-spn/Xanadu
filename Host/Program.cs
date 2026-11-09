using Xanadu.Host;

// dotnet run --project Host                         -> window, Esc quits
// dotnet run --project Host -- --shot=<path.png>    -> saves frame 60 and quits (--shot-frame=<n> to pick)
// dotnet run --project Host -- --resolution=1920x1080
new HostGame(HostOptions.Parse(args)).Run();
