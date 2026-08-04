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

    public override void ResolveReferences(Def parentDef)
    {
      base.ResolveReferences(parentDef);
      if (parentDef is not VehicleDef vehicleDef) return;

      LongEventHandler.ExecuteWhenFinished(() =>
      {
        if (!UniqueVehicleManager.PlaceholderDefs.TryGetValue(vehicleDef, out var list))
          UniqueVehicleManager.PlaceholderDefs[vehicleDef] = list = [];
        list.Clear();
        for (var i = 0; i < placeholderCount; i++)
        {
          var def = UniqueVehicleUtility.GenerateUniqueVehicleDef(vehicleDef, i);
          list.Add(def);
        }
      });
    }
  }
}