using DevTools.Testing;

namespace VehicleMapFramework.Test_Logics;

public abstract class CustomLoggerBase(Logger.Config config) : Logger(config)
{
    public bool initialized;
    
    protected const string SettingUp = "Setting up";
    protected const string Executing = "Executing";
    protected const string TearingDown = "Tearing down";
    protected const string BeginGroup = "-- Begin Group";
    protected const string EndGroup = "-- End Group";
    protected const string PassedLabel = "[Passed]";
    protected const string FailedLabel = "[Failed]";
    protected const string SkippedLabel = "[Skipped]";
    
    protected State currentState = State.None;

    public virtual void InitCustom(StreamWriter writer)
    {
    }
    
    public abstract void WriteCustom(StreamWriter writer, string message);

    public abstract void DisposeCustom(StreamWriter writer);

    protected enum State
    {
        None,
        SettingUp,
        TearingDown,
    }
}