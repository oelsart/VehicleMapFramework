using DevTools.Testing;
using HarmonyLib;
using Verse;

namespace VehicleMapFramework.Test_Logics;

public static class SaveTester
{
  private const string SaveFileName = "_TEST_SAVE";
  
  public static void Clear()
  {
    File.Delete(GenFilePaths.FilePathForSavedGame(SaveFileName));
  }

  public static void Save(IExposable exposable)
  {
    try
    {
      Scribe.saver.InitSaving(GenFilePaths.FilePathForSavedGame(SaveFileName), "savegame");
      Scribe.mode = LoadSaveMode.Saving;
      exposable.ExposeData();
      Scribe.saver.FinalizeSaving();
    }
    catch(Exception ex)
    {
      Scribe.ForceStop();
      Test.Fail(ex);
    }
  }

  public static void Load(IExposable exposable)
  {
    try
    {
      Scribe.loader.InitLoading(GenFilePaths.FilePathForSavedGame(SaveFileName));
      Scribe.mode = LoadSaveMode.LoadingVars;
      exposable.ExposeData();
      Scribe.mode = LoadSaveMode.ResolvingCrossRefs;
      exposable.ExposeData();
      Scribe.mode = LoadSaveMode.PostLoadInit;
      exposable.ExposeData();
      Scribe.mode = LoadSaveMode.Inactive;
    }
    catch(Exception ex)
    {
      Scribe.ForceStop();
      Test.Fail(ex);
    }
  }

  public class Container(IExposable mainExposable, params IExposable[] exposables) : IExposable
  {
    public void ExposeData()
    {
      for (var i = 0; i < exposables.Length; i++)
      {
        var exposable = exposables[i];
        Scribe_Deep.Look(ref exposable, exposable.ToString());
        exposables[i] = exposable;
      }
      
      mainExposable.ExposeData();
    }
  }

  public struct MockLoaded : IDisposable
  {
    private static readonly AccessTools.FieldRef<CrossRefHandler, LoadedObjectDirectory> loadedObjectDirectory =
      AccessTools.FieldRefAccess<CrossRefHandler, LoadedObjectDirectory>("loadedObjectDirectory");

    public MockLoaded(params ILoadReferenceable[] referenceables)
    {
      Scribe.loader.crossRefs.Clear(false);
      Scribe.mode = LoadSaveMode.LoadingVars;
      var directory = loadedObjectDirectory(Scribe.loader.crossRefs);
      foreach (var referenceable in referenceables)
      {
        directory.RegisterLoaded(referenceable);
      }
      Scribe.mode = LoadSaveMode.Inactive;
    }

    public void Dispose()
    {
      loadedObjectDirectory(Scribe.loader.crossRefs).Clear();
    }
  }
}