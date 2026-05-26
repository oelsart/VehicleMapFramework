using DevTools.Testing;

namespace VehicleMapFramework.Test_Logics;

public abstract class CustomLoggerBase(Logger.Config config) : Logger(config)
{

  protected const string Tab = "--\t";
  protected const string Space = "  ";
  protected const string PassedLabel = "[Passed]";
  protected const string FailedLabel = "[Failed]";
  protected const string SkippedLabel = "[Skipped]";
  public bool initialized;

  public abstract void WriteCustom(string message);

  public abstract void DisposeCustom(StreamWriter writer);
}
