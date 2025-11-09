#if LOADTEST
using ModAssemblyLoader;

namespace LoadTest;

public abstract class LoadMods
{
    protected AssemblyLoader loader;

    protected abstract string[] LoadFolders { get; }
    
    protected abstract string[] AssemblyNames { get; }

    [SetUp]
    public void SetUp()
    {
        loader = new AssemblyLoader(Configurations.Version, Configurations.RimWorld);
    }
    
    [Test]
    public virtual void LoadAssemblies()
    {
        foreach (var folder in LoadFolders)
        {
            loader.LoadModFolder(folder);
        }
        var assemblies = AppDomain.CurrentDomain.GetAssemblies();
        foreach (var assemblyName in AssemblyNames)
        {
            Assert.That(assemblies.Any(assembly => assembly.FullName.Contains(assemblyName)));
        }
    }
}
#endif