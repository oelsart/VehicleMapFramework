#if LOADTEST
namespace LoadTest;

public class LoadVehicleFramework : LoadMods
{
    protected override string[] LoadFolders => [Configurations.Harmony, Configurations.VehicleFramework];

    protected override string[] AssemblyNames => ["SmashTools", "UpdateLogTool", "Vehicles"];
}
#endif