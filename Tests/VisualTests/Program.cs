using Xanadu.Tests.VisualTests;

// dotnet run --project Tests/VisualTests                                         -> the browser
// dotnet run --project Tests/VisualTests -- --visual-test=<slug> --visual-test-exit -> one test, exit 0/1
// dotnet run --project Tests/VisualTests -- --visual-test=all --visual-test-exit    -> every test
// --visual-test-shot=<png> saves the frame once it settles; --visual-test-full drops the sidebar.
Xanadu.Host.Diagnostics.GodotEngineTrap.Install();
var harness = new VisualTestMain(args);
harness.Run();
return harness.ExitCode;
