using Xanadu.Lab.MoonWorksParticles;

// dotnet run                      -> window, Esc quits
// dotnet run -- --screenshot x.png -> runs 150 frames, saves the frame, quits
string? shot = args.Length == 2 && args[0] == "--screenshot" ? System.IO.Path.GetFullPath(args[1]) : null;
new ParticleGame(shot).Run();
