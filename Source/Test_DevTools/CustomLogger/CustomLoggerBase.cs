using DevTools.Testing;

namespace VehicleMapFramework.Test_Logics;

public abstract class CustomLoggerBase(Logger.Config config) : Logger(config)
{
    public bool initialized;
    
    protected const string Indent = "--\t";
    protected const string SpaceTwo = "  ";
    protected const string SpaceFour = "    ";
    protected const string PassedLabel = "[Passed]";
    protected const string FailedLabel = "[Failed]";
    protected const string SkippedLabel = "[Skipped]";

    public virtual void InitCustom(StreamWriter writer)
    {
    }
    
    public abstract void WriteCustom(StreamWriter writer, string message);

    public abstract void DisposeCustom(StreamWriter writer);
}