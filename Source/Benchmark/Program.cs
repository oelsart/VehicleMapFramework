using BenchmarkDotNet.Running;

namespace VehicleMapFramework;

internal class Program
{
  static void Main()
  {
    //BenchmarkRunner.Run<GetMethodFirstTime>();
    BenchmarkRunner.Run<GetStructMethodFirstTime>();
    //BenchmarkRunner.Run<GetMethodWarm>();
  }
}