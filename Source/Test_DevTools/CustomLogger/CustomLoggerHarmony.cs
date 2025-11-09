using System.Globalization;
using System.Runtime.CompilerServices;
using DevTools;
using DevTools.Testing;
using HarmonyLib;
using Verse;

namespace VehicleMapFramework.Test_Logics;

[StaticConstructorOnStartup]
public static class CustomLoggerHarmony
{
    static CustomLoggerHarmony()
    {
        try
        {
            RuntimeHelpers.RunClassConstructor(typeof(Logger).TypeHandle);
            var harmony = new Harmony("OELS.VehicleMapFramework.Test_DevTools");
            harmony.Patch(
                AccessTools.Method(typeof(DevLog), nameof(DevLog.EnableLogger)),
                AccessTools.Method(typeof(CustomLoggerHarmony), nameof(EnableCustomLogger)));
            harmony.Patch(
                AccessTools.Method(typeof(Logger), nameof(Logger.Write)),
                AccessTools.Method(typeof(CustomLoggerHarmony), nameof(ReplaceWriteMethod)));
            harmony.Patch(
                AccessTools.Method(typeof(Logger), nameof(Logger.Dispose)),
                AccessTools.Method(typeof(CustomLoggerHarmony), nameof(SaveBeforeDispose)));
        }
        catch (Exception ex)
        {
            if (ex is HarmonyException)
                throw;
        }
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

    private static bool ReplaceWriteMethod(Logger __instance, Logger.Config ___config,
        FileInfo ___file, StreamWriter ___writer, Mutex ___writerMutex, string message)
    {
        if (__instance is not CustomLoggerBase customLogger)
            return true;
        if (__instance.Disposed)
            return false;
        if (!customLogger.initialized)
        {
            customLogger.InitCustom(___writer);
            customLogger.initialized = true;
        }

        ___file.Refresh();
        if (!___file.Exists || ___file.Length >= ___config.maxFileSize)
            return false;

        using MutexLock ml = new(___writerMutex);
        customLogger.WriteCustom(___writer, message);
        return false;
    }

    private static void SaveBeforeDispose(Logger __instance,  StreamWriter ___writer)
    {
        if (__instance is CustomLoggerBase customLogger)
            customLogger.DisposeCustom(___writer);
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