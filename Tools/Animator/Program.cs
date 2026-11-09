using Xanadu.Tools.Animator;

// dotnet run --project Tools/Animator                      -> the animator on contact.xaf
// dotnet run --project Tools/Animator -- --shot=<png>      -> saves frame 60 and quits (--shot-frames=<n> or --shot-seconds=<s>)
// dotnet run --project Tools/Animator -- --resolution=WxH  -> window size (default 1600x900)
// The other set-up flags (--flow, --clip, --paths, ...) are listed on AnimatorTool.ReadArguments.
new AnimatorTool(args).Run();
