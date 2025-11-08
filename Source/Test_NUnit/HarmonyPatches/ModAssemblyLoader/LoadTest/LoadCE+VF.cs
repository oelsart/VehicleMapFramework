#if LOADTEST
namespace LoadTest;

public class LoadCE_VF : LoadMods
{
    protected override string[] LoadFolders =>
        [Configurations.Harmony, Configurations.VehicleFramework, Configurations.CombatExtended];

    protected override string[] AssemblyNames =>
        ["CombatExtended", "VehiclesCompat"];
}
#endif