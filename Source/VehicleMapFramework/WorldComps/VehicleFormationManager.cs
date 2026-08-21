using System.Collections.Generic;
using RimWorld.Planet;
using Vehicles;
using Verse;

namespace VehicleMapFramework;

public class VehicleFormationManager(World world) : WorldComponent(world)
{
  public List<FormationPreset> FormationPresets => formationPresets;
  
  private List<FormationPreset> formationPresets = [];
  
  public override void ExposeData()
  {
    Scribe_Collections.Look(ref formationPresets, nameof(formationPresets), LookMode.Deep);
  }

  public class FormationPreset : IExposable, IRenameable
  {
    public Dictionary<VehiclePawn, VehicleFormationComp.DrawData> drawPositions;
    private string labelInt;
    private List<VehiclePawn> keysWorkingList;
    private List<VehicleFormationComp.DrawData> valuesWorkingList;

    public string BaseLabel => "Formation";
    public string RenamableLabel
    {
      get => labelInt ?? BaseLabel;
      set => labelInt = value;
    }
    public string InspectLabel => RenamableLabel;

    void IExposable.ExposeData()
    {
      Scribe_Values.Look(ref labelInt, nameof(labelInt));
      Scribe_Collections.Look(ref drawPositions, nameof(drawPositions),
        LookMode.Reference, LookMode.Deep, ref keysWorkingList, ref valuesWorkingList);
    }

    public class Dialog_RenameFormationPreset(FormationPreset preset) : Dialog_Rename<FormationPreset>(preset);
  }
}