using System.Globalization;
using DevTools;
using DevTools.Testing;
using HarmonyLib;
using Verse;

namespace VehicleMapFramework.Test_Logics;

public class Test_Logics : Mod
{
    public Test_Logics(ModContentPack content) : base(content)
    {
        var harmony = new Harmony("OELS.VehicleMapFramework.Test_Logics");
        harmony.Patch(
            AccessTools.Method(typeof(DevLog), nameof(DevLog.EnableLogger)),
            AccessTools.Method(typeof(Test_Logics), nameof(EnableCustomLogger)));
        harmony.Patch(
            AccessTools.Method(typeof(Logger), nameof(Logger.Write)),
            AccessTools.Method(typeof(Test_Logics), nameof(ReplaceWriteMethod)));
        harmony.Patch(
            AccessTools.Method(typeof(Logger), nameof(Logger.Dispose)),
            AccessTools.Method(typeof(Test_Logics), nameof(SaveBeforeDispose)));
    }

    private static bool EnableCustomLogger(Logger.Config config, ref Logger ___logger)
    {
        var logger = ParseLoggerArgs();
        if (ParseLoggerArgs() == typeof(Logger))
            return true;
        ___logger = (Logger)Activator.CreateInstance(logger, config);
        DevLog.Write(DateTime.Now.ToString("g", DateTimeFormatInfo.CurrentInfo) + Environment.NewLine + Environment.NewLine);
        DevLog.WriteLine();
        return false;
        
        static Type ParseLoggerArgs()
        {
            var loggerType = LoggerType.Default;
            var args = Environment.GetCommandLineArgs();
            if (args.Length == 0)
                return typeof(Logger);
            for (var i = 0; i < args.Length; i++)
            {
                if (args[i] == "--logger")
                {
                    if (i + 1 < args.Length)
                    {
                        loggerType = (LoggerType)Enum.Parse(typeof(LoggerType), args[++i], ignoreCase: true);
                    }
                    break;
                }
            }

            return loggerType switch
            {
                LoggerType.Default => typeof(Logger),
                LoggerType.JUnit => typeof(Logger_JUnit),
                _ => throw new ArgumentOutOfRangeException()
            };
        }
    }

    private static bool ReplaceWriteMethod(Logger __instance, Logger.Config ___config, FileInfo ___file,
        StreamWriter ___writer, Mutex ___writerMutex, string message)
    {
        if (__instance is not Logger_JUnit loggerJUnit)
            return true;
        if (__instance.Disposed)
            return false;

        ___file.Refresh();
        if (!___file.Exists || ___file.Length >= ___config.maxFileSize)
            return false;

        using MutexLock ml = new(___writerMutex);
        loggerJUnit.ParseAndAdd(message);
        return false;
    }

    private static bool SaveBeforeDispose(Logger __instance,  FileStream ___fileStream)
    {
        if (__instance is Logger_JUnit loggerJUnit)
        {
            loggerJUnit.Save(___fileStream);
            return false;
        }

        return true;
    }
    
    private readonly struct MutexLock(Mutex mutex) : IDisposable
    {
        private readonly bool taken = mutex.WaitOne();

        public void Dispose()
        {
            if (taken)
                mutex.ReleaseMutex();
        }
    }
}