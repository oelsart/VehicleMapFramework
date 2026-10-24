using Verse;

namespace VehicleMapFramework.Test;

public readonly struct TimeSpeedScope : IDisposable
{
  private readonly TimeSpeed oldTimeSpeed;

  public TimeSpeedScope(TimeSpeed newTimeSpeed)
  {
    oldTimeSpeed = Find.TickManager.CurTimeSpeed;
    Find.TickManager.CurTimeSpeed = newTimeSpeed;
  }

  public void Dispose()
  {
    Find.TickManager.CurTimeSpeed = oldTimeSpeed;
  }
}