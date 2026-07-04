namespace VehicleMapFramework.Test_CompatPatches;

public class Configurations
{

  public const string Version = "1.6";

  public const string SteamWorkshopRoot = BuildConstants.SteamWorkshopRoot;

  public const string TestProjectRoot = "../../..";

  public const string LocalModsRoot = TestProjectRoot + "/../../..";

  public const string WorkshopIdsFileName = "WorkshopIds.yml";

  public const string TestPlansFileName = "TestPlans.yml";
  
  public static readonly bool IsRemote = Environment.GetEnvironmentVariable("GITHUB_ACTIONS") == "true";

  public static readonly bool UseWorkshopVersion = Environment.GetEnvironmentVariable("UseWorkshopVersion") == "true";

  public static readonly string RimWorldAssemblyFolder = IsRemote ? TestProjectRoot + "/Stubs" : LocalModsRoot + "/../RimWorldWin64_Data/Managed";
}
