using System;
using System.IO;
using System.Threading.Tasks;
using OELSMods;
using UnityEditor;
using UnityEngine;

namespace OELSMods
{
  public static class ModIds
  {
    public const string VehicleMapFramework = "OELS.VehicleMapFramework";
    public const string VehicleMapFrameworkDev = "OELS.VehicleMapFramework.dev";
  }
}

namespace SmashTools
{
    public class AssetBundleBuilder : MonoBehaviour
    {
        private const string TextureFolderName = "Textures";
        private const string SoundFolderName = "Sounds";

        private const string ShaderFileName = "Shaders";

        // RimWorld stores Shaders in Materials/ so asset bundle paths have to match it for their
        // loader to be able to find the content.
        private const string ShaderFolderName = "Materials";

        private const string OutputPath = "../../1.6/AssetBundles";
        private const string OutputPathDev = "../../../VehicleMapFrameworkDev/1.6/AssetBundles";

        private const string DefaultBundleName = "AssetBundles";

        private static readonly BuildTarget[] BuildTargets =
        {
            BuildTarget.StandaloneWindows64, BuildTarget.StandaloneOSX, BuildTarget.StandaloneLinux64
        };

        private static string PlatformSuffix(BuildTarget buildTarget)
        {
            return buildTarget switch
            {
                BuildTarget.StandaloneWindows64 => "_win",
                BuildTarget.StandaloneOSX => "_mac",
                BuildTarget.StandaloneLinux64 => "_linux",
                _ => throw new NotSupportedException(buildTarget.ToString())
            };
        }

        private static string[] GetAssetPaths<T>(string packageId)
        {
            string folderName = FolderName();

            string[] guids =
                AssetDatabase.FindAssets($"t:{typeof(T).Name}",
                    new[]
                    {
                        $"Assets/Data/{packageId}/{folderName}"
                    });

            string[] paths = new string[guids.Length];
            for (int i = 0; i < guids.Length; i++)
            {
                string guid = guids[i];
                string path = AssetDatabase.GUIDToAssetPath(guid);
                paths[i] = path;
            }
            return paths;

            string FolderName()
            {
                if (typeof(T) == typeof(Texture2D))
                    return TextureFolderName;
                if (typeof(T) == typeof(AudioClip))
                    return SoundFolderName;
                if (typeof(T) == typeof(Shader))
                    return ShaderFolderName;

                throw new NotImplementedException();
            }
        }
        
        [MenuItem("Assets/Build AssetBundles/Vehicle Map Framework + Dev")]
        private static void BuildAssetBundlesBoth()
        {
            BuildAssetBundles();
            BuildAssetBundlesDev();
        }

        [MenuItem("Assets/Build AssetBundles/Vehicle Map Framework")]
        private static void BuildAssetBundles()
        {
            BuildForMod(ModIds.VehicleMapFramework, OutputPath);
        }

        [MenuItem("Assets/Build AssetBundles/Vehicle Map Framework Dev")]
        private static void BuildAssetBundlesDev()
        {
            var dir = $"Assets/Data/{ModIds.VehicleMapFramework}";
            var dirDev = $"Assets/Data/{ModIds.VehicleMapFrameworkDev}";
            try
            {
                AssetDatabase.MoveAsset(dir, dirDev);
                BuildForMod(ModIds.VehicleMapFrameworkDev, OutputPathDev);
            }
            finally
            {
                AssetDatabase.MoveAsset(dirDev, dir);
            }
        }

        public static void BuildForMod(string packageId, string outputPath)
        {
            const string TextureBundleName = "oels_vehiclemapframework_textures";
            const string ShaderBundleName = "oels_vehiclemapframework_shaders";

            // Start fresh for build folder
            if (!Directory.Exists(outputPath))
                throw new DirectoryNotFoundException(outputPath);

            Directory.Delete(outputPath, true);
            Directory.CreateDirectory(outputPath);

            // Platform independent
            AssetBundleBuild[] bundles = new AssetBundleBuild[1];
            bundles[0].assetBundleName = TextureBundleName;
            bundles[0].assetNames = GetAssetPaths<Texture2D>(packageId);

            BuildPipeline.BuildAssetBundles(outputPath,
                bundles,
                BuildAssetBundleOptions.ChunkBasedCompression,
                BuildTarget.StandaloneWindows64);


            // Platform dependent
            AssetBundleBuild[] platformBundles = new AssetBundleBuild[1];
            platformBundles[0].assetBundleName = ShaderBundleName;
            platformBundles[0].assetNames = GetAssetPaths<Shader>(packageId);

            BuildForPlatform(outputPath,
                platformBundles,
                BuildAssetBundleOptions.ChunkBasedCompression);
            
            if (File.Exists($"{outputPath}/{DefaultBundleName}"))
                File.Delete($"{outputPath}/{DefaultBundleName}");
            if (File.Exists($"{outputPath}/{DefaultBundleName}.manifest"))
                File.Delete($"{outputPath}/{DefaultBundleName}.manifest");
        }

        private static void BuildForPlatform(string directoryPath, AssetBundleBuild[] bundles,
            BuildAssetBundleOptions bundleOptions)
        {
            foreach (BuildTarget buildTarget in BuildTargets)
            {
                AssetBundleBuild[] platformBundles =
                    new AssetBundleBuild[bundles.Length];
                for (int i = 0; i < bundles.Length; i++)
                {
                    AssetBundleBuild bundle = bundles[i];
                    AssetBundleBuild platformBundle = bundle;
                    platformBundle.assetBundleName = bundle.assetBundleName + PlatformSuffix(buildTarget);
                    platformBundles[i] = platformBundle;
                }
                BuildPipeline.BuildAssetBundles(directoryPath, platformBundles, bundleOptions, buildTarget);
            }
        }
    }
}