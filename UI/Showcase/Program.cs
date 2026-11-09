using Xanadu.Ui.Showcase;

// dotnet run --project UI/Showcase                     -> the showcase
// dotnet run --project UI/Showcase -- --shot=<png>     -> saves frame 60 and quits (--shot-frame=<n> to pick)
new UiShowcase(args).Run();
