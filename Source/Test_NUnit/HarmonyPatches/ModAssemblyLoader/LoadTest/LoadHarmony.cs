#if LOADTEST
namespace LoadTest;

public class LoadHarmony : LoadMods
{
    protected override string[] LoadFolders => [Configurations.Harmony];

    protected override string[] AssemblyNames => ["0Harmony", "HarmonyMod"];
}
#endif