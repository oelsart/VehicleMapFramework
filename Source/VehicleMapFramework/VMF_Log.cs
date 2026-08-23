using System;
using System.Diagnostics;
using Verse;

namespace VehicleMapFramework;

internal static class VMF_Log
{
  private const string LogLabel = "[VehicleMapFramework]";

  public static void Error(string message)
  {
    if (UnitTestDetector.IsTestingContext)
    {
      Console.WriteLine(message);
    }
    Log.Error($"{LogLabel} {message}\n{new StackTrace(2, true)}");
  }

  public static void Warning(string message)
  {
    Log.Warning($"{LogLabel} {message}");
  }

  public static void Message(string message)
  {
    Log.Message($"{LogLabel} {message}");
  }

  public static void Message(object obj)
  {
    Log.Message($"{LogLabel} {obj}");
  }

  [Conditional("DEBUG")]
  public static void DebugMessage(string message)
  {
    Log.Message(message);
  }

  [Conditional("DEBUG")]
  [Conditional("DEV")]
  public static void DebugWarning(string message)
  {
    Log.Warning(message);
  }

  [Conditional("DEBUG")]
  [Conditional("DEV")]
  public static void DebugError(string message)
  {
    Log.Error(message);
  }
}
