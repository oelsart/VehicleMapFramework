using JetBrains.Annotations;
using Vehicles;
using Verse;

namespace VehicleMapFramework
{
  [UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
  public class VehicleMapProps_Unique : VehicleMapProps
  {
    [Unsaved] public VehicleDef baseDef;
    public int placeholderCount = 32;
    private static bool hotReload;
    
    public override void ResolveReferences(Def parentDef)
    {
      base.ResolveReferences(parentDef);
      if (parentDef is not VehicleDef vehicleDef) return;
      
      LongEventHandler.ExecuteWhenFinished(() =>
      {
        if (hotReload && DefDatabase<VehicleDef>.GetNamedSilentFail($"{0.ToString()}_{parentDef.defName}") is not null)
          return;
        
        if (!UniqueVehicleManager.PlaceholderDefs.TryGetValue(vehicleDef, out var list))
          UniqueVehicleManager.PlaceholderDefs[vehicleDef] = list = [];
        list.Clear();
        for (var i = 0; i < placeholderCount; i++)
        {
          var def = UniqueVehicleUtility.GenerateUniqueVehicleDef(vehicleDef, i, hotReload);
          list.Add(def);
        }
        LongEventHandler.ExecuteWhenFinished(() =>
        {
          hotReload = true;
        });
      });
    }
  }
}