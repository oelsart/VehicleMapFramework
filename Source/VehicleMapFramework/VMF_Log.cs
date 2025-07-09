using Verse;

namespace VehicleMapFramework;

internal class VMF_Log
{
    public static void Error(string message)
    {
        Log.Error($"{LogLabel} {message}");
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

    internal const string LogLabel = "[VehicleMapFramework]";
}
