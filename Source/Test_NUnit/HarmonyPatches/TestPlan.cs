namespace VehicleMapFramework.Test_HarmonyPatches;

[Serializable]
public class TestPlan
{
  public string Name { get; set; }

  public List<string> Mods { get; set; }

  public List<string> Categories { get; set; }

  public bool LoadOnly { get; set; }
}
