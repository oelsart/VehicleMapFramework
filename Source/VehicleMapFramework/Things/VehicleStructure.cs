using JetBrains.Annotations;
using RimWorld;
using SmashTools;
using Verse;

namespace VehicleMapFramework;

[UsedImplicitly]
public class VehicleStructure : Building
{
    public override void PreApplyDamage(ref DamageInfo dinfo, out bool absorbed)
    {
        if (this.IsOnVehicleMapOf(out var vehicle) && dinfo.Def != DamageDefOf.Bomb)
        {
            vehicle.TakeDamage(dinfo, (Position.ToVector3() - VehicleMapUtility.OffsetFor(vehicle, Rot8.North)).ToIntVec3().ToIntVec2);
        }
        base.PreApplyDamage(ref dinfo, out absorbed);
    }

    public override void SpawnSetup(Map map, bool respawningAfterLoad)
    {
        base.SpawnSetup(map, respawningAfterLoad);
        if (!this.IsOnVehicleMapOf(out var vehicle)) return;
        
        vehicle.mapEdgeCellsDirty = true;
        vehicle.structureCellsDirty = true;
        BackwardCompatibility();
    }

    public override void DeSpawn(DestroyMode mode = DestroyMode.Vanish)
    {
        if (this.IsOnVehicleMapOf(out var vehicle))
        {
            vehicle.mapEdgeCellsDirty = true;
            vehicle.structureCellsDirty = true;
        }
        base.DeSpawn(mode);
    }
    
    private void BackwardCompatibility()
    {
        if (def != VMF_DefOf.VMF_VehicleStructureEmpty) return;

        Map.terrainGrid.SetTerrain(Position, VMF_DefOf.VMF_ImpassableFloor);
        allowDestroyNonDestroyable = true;
        Destroy();
        allowDestroyNonDestroyable = false;
    }
}