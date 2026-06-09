using BenchmarkDotNet.Attributes;
using HarmonyLib;

namespace VehicleMapFramework;

[WarmupCount(10)]
[IterationCount(100)]
public class GetMethodWarm
{
  [Benchmark]
  public void AccessToolsWarm()
  {
      _ = AccessTools.Method(typeof(GetMethodWarm), nameof(TargetMethodA));
  }
  
  [Benchmark]
  public void DelegateWarm()
  {
    _ = ((Delegate)TargetMethodB).Method;
  }
  
  public static void TargetMethodA(){}
  public static void TargetMethodB(){}
}