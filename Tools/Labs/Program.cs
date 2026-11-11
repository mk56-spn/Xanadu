using Xanadu.Labs;

// dotnet run --project Tools/Labs                       -> the lab, on its first tab (the shader playground)
// dotnet run --project Tools/Labs -- --shader=<file>    -> the playground watches that file instead of its working copy
// dotnet run --project Tools/Labs -- --editor-file=<f> -> the shader editor opens and saves that file instead of its working copy
// dotnet run --project Tools/Labs -- --tab=<n>          -> opens tab n first
// dotnet run --project Tools/Labs -- --shot=<png>       -> saves frame 60 and quits (--shot-frame=<n> to pick)
new LabsApp(LabsOptions.Parse(args)).Run();
