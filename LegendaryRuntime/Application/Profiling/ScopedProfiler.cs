using System.Diagnostics;
using OpenTK.Graphics.OpenGL4;

namespace LegendaryRenderer.LegendaryRuntime.Application.Profiling;
public class ScopedProfiler : IDisposable
{
    private readonly string _profilerName;
    private Stopwatch _cpuStopwatch;
    private readonly int _gpuQueryStart;
    private readonly int _gpuQueryEnd;
    public static Dictionary<string, float> Statistics = new Dictionary<string, float>();

    public ScopedProfiler(string profilerName)
    {
        _profilerName = profilerName;

        // Initialize CPU Stopwatch
        _cpuStopwatch = Stopwatch.StartNew();

        // Initialize GPU Queries
        _gpuQueryStart = GL.GenQuery();
        _gpuQueryEnd = GL.GenQuery();

        // Start GPU Timing Query
        GL.QueryCounter(_gpuQueryStart, QueryCounterTarget.Timestamp);
    }

    public void StartTimingCPU()
    {
        _cpuStopwatch = Stopwatch.StartNew();
    }

    public float StopTimingCPU()
    {
        _cpuStopwatch.Stop();
        double cpuElapsedTime = _cpuStopwatch.Elapsed.TotalMilliseconds;
        
        return (float)cpuElapsedTime;
    }

    public void Dispose()
    {
        // Stop CPU Stopwatch
        _cpuStopwatch.Stop();
        double cpuElapsedTime = _cpuStopwatch.Elapsed.TotalMilliseconds;

        // End GPU Timing Query
        GL.QueryCounter(_gpuQueryEnd, QueryCounterTarget.Timestamp);

        // Wait for GPU results
        int available = 0;
        while (available == 0)
        {
            GL.GetQueryObject(_gpuQueryEnd, GetQueryObjectParam.QueryResultAvailable, out available);
        }

        // Retrieve GPU timing results
        long gpuStartTime, gpuEndTime;
        GL.GetQueryObject(_gpuQueryStart, GetQueryObjectParam.QueryResult, out gpuStartTime);
        GL.GetQueryObject(_gpuQueryEnd, GetQueryObjectParam.QueryResult, out gpuEndTime);

        double gpuElapsedTime = (gpuEndTime - gpuStartTime) / 1_000_000.0; // Convert nanoseconds to milliseconds
        
        Statistics.Add($"{_profilerName}(CPU)", (float)cpuElapsedTime);
        Statistics.Add($"{_profilerName}(GPU)", (float)gpuElapsedTime);
        // Cleanup
        GL.DeleteQuery(_gpuQueryStart);
        GL.DeleteQuery(_gpuQueryEnd);
    }

    public static void ResetStats()
    {
        Statistics.Clear();
    }

    public static void PrintStatistics()
    {
        Console.Write("[ScopedProfiler]\n");
        int x = 0;
        foreach (var kvp in Statistics)
        {
            Console.Write($"{kvp.Key}: {kvp.Value:F3}ms ");
            x++;

            if (x > 1)
            {
                x = 0;
                Console.Write("\n");
            }
        
        }
        Console.Write("[EndProfiler]");
    }
}