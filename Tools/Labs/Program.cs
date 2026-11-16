using Xanadu.Labs;

// dotnet run --project Tools/Labs                       -> the lab, on its first tab
// dotnet run --project Tools/Labs -- --tab=<n>          -> opens tab n first
// dotnet run --project Tools/Labs -- --shot=<png>       -> saves frame 60 and quits (--shot-frame=<n> to pick)
new LabsApp(LabsOptions.Parse(args)).Run();
