using System;
using System.Diagnostics.Tracing;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Diagnostics.NETCore.Client;

namespace Xanadu.Host.Autopsy;

/// <summary>
/// The .NET runtime's own sampling profiler, pointed at this process: about
/// once a millisecond it records every managed thread's call stack into an
/// EventPipe trace, which goes to the launch's capture (<see cref="AutopsyStore"/>)
/// for the Autopsy tool's METHODS view. On with <c>XANADU_PROFILE=1</c> only:
/// each sample briefly pauses the managed threads, so it costs a little frame time.
/// </summary>
internal sealed class CpuSampler
{
    private const string SampleProvider = "Microsoft-DotNETCore-SampleProfiler";
    private const string RuntimeProvider = "Microsoft-Windows-DotNETRuntime";

    /// <summary>The runtime keywords dotnet-trace's cpu-sampling profile asks for: loader, JIT, GC and the rest of its default set.</summary>
    private const long RuntimeKeywords = 0x14C14FCCBD;

    private readonly EventPipeSession _session;
    private readonly Stream _file;
    private readonly Task _copy;
    private readonly string _app;

    private CpuSampler(string app, EventPipeSession session, Stream file)
    {
        _app = app;
        _session = session;
        _file = file;
        _copy = Task.Run(() => session.EventStream.CopyTo(file));
    }

    public static bool Wanted => Environment.GetEnvironmentVariable("XANADU_PROFILE") == "1";

    public static CpuSampler? Start(string app)
    {
        if (!Wanted)
            return null;
        try
        {
            var client = new DiagnosticsClient(Environment.ProcessId);
            EventPipeSession session = client.StartEventPipeSession(
                [
                    new EventPipeProvider(SampleProvider, EventLevel.Informational),
                    new EventPipeProvider(RuntimeProvider, EventLevel.Informational, RuntimeKeywords),
                ],
                requestRundown: true,
                circularBufferMB: 256);
            Console.WriteLine("CPU sampler on: method times go to the Autopsy tool when this closes");
            return new CpuSampler(app, session, AutopsyStore.CreateTrace(app));
        }
        catch (Exception exception) when (exception is ServerNotAvailableException or DiagnosticsClientException or IOException
                                              or UnauthorizedAccessException or TimeoutException)
        {
            Console.WriteLine($"CPU sampler not started: {exception.Message}");
            return null;
        }
    }

    /// <summary>Ends the session (the runtime then writes the method names) and closes the trace.</summary>
    public void Stop()
    {
        try
        {
            _session.Stop();
            if (!_copy.Wait(TimeSpan.FromSeconds(20)))
                Console.WriteLine("CPU sampler: the trace didn't finish writing in time");
        }
        catch (Exception exception) when (exception is ServerNotAvailableException or IOException or AggregateException
                                              or TimeoutException)
        {
            Console.WriteLine($"CPU sampler: {exception.Message}");
        }
        finally
        {
            _file.Dispose();
            _session.Dispose();
        }
        AutopsyStore.FinishTrace(_app);
    }
}
