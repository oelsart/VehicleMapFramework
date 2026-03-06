using System;
using System.Runtime.CompilerServices;
using Verse;

namespace VehicleMapFramework;

public readonly struct DeepProfilerScope: IDisposable
{
    private readonly bool force;
    private readonly bool enabled;
    private readonly bool logVerbose;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public DeepProfilerScope(string label, bool force = false)
    {
        if (force)
        {
            this.force = true;
            this.enabled = DeepProfiler.enabled;
            this.logVerbose = Prefs.LogVerbose;
            DeepProfiler.enabled = true;
            Prefs.LogVerbose = true;
        }
        DeepProfiler.Start(label);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    void IDisposable.Dispose()
    {
        DeepProfiler.End();
        if (force)
        {
            DeepProfiler.enabled = enabled;
            Prefs.LogVerbose = logVerbose;
        }
    }
}