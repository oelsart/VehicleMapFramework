using System.Reflection;
using System.Xml.Linq;

namespace ModAssemblyLoader;

public class AssemblyLoader(string version, string workshopPath, string localModsPath = null)
{
    private const string LoadFoldersFileName = "LoadFolders.xml";
    
    private const string AssembliesFolderName = "Assemblies";
    
    private const string defaultVersionName = "default";
    
    private const string CommonFolderName = "Common";
    
    public List<Assembly> LoadModFolder(string folderName)
    {
        ArgumentNullException.ThrowIfNull(folderName);
        var rootFolder = TryLoadFolder(Path.Combine(workshopPath, folderName));
        if (rootFolder is null && localModsPath != null)
        {
            rootFolder = TryLoadFolder(Path.Combine(localModsPath, folderName));
            if (rootFolder is null)
                throw new FileNotFoundException($"Folder {folderName} not found");
        }
        var loadFolders = TryLoadXDoc(Path.Combine(rootFolder!.FullName, LoadFoldersFileName));
        var folders = 
            (loadFolders?.Root?.Element($"v{version}") ?? loadFolders?.Root?.Element("default"))?
            .Elements("li").Select(l => l.Value).ToList();
        folders ??= [];
        if (folders.Count == 0)
        {
            if (Directory.Exists(Path.Combine(rootFolder.FullName, version)))
                folders.Add(version);
            else
            {
                var versionLatest = rootFolder.GetDirectories()
                    .Select(folder => Version.TryParse(folder.Name, out var v) ? v : null)
                    .Where(v => v != null)
                    .Max();
                if (versionLatest is null)
                {
                    folders.Add("/");
                }
                else if (versionLatest.Major > 0)
                {
                    folders.Add(versionLatest.ToString());
                }
            }
            if (Directory.Exists(Path.Combine(rootFolder.FullName, CommonFolderName)))
                folders.Add(CommonFolderName);
        }
        
        var loadedAssemblies = new List<Assembly>();
        foreach (var name in folders.OrderBy(name => name is "/" or "\\"))
        {
            var fullPath = rootFolder.FullName;
            if (name != "/" && name != "\\")
            {
                fullPath = Path.Combine(fullPath, name);
            }
            fullPath = Path.Combine(fullPath, AssembliesFolderName);
            var folder = TryLoadFolder(fullPath);
            if (folder is null)
                continue;
            foreach (var fileInfo in folder.GetFiles("*.dll", SearchOption.AllDirectories))
            {
                try
                {
                    loadedAssemblies.Add(Assembly.LoadFrom(fileInfo.FullName));
                }
                catch
                {
                    // ignored
                }
            }
        }
        return loadedAssemblies;

        static DirectoryInfo TryLoadFolder(string path) => Directory.Exists(path) ? new DirectoryInfo(path) : null;
        
        static XDocument TryLoadXDoc(string path)
        {
            try
            {
                return XDocument.Load(path);
            }
            catch
            {
                return null; 
            }
        }
    }
}