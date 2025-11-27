using System.Collections.Generic;
using JetBrains.Annotations;
using RimWorld;
using SmashTools;
using Vehicles;
using Verse;

namespace VehicleMapFramework;

[UsedImplicitly(ImplicitUseTargetFlags.Members)]
public class CompProperties_NpcVehicleMap : VehicleCompProperties
{
    public CompProperties_NpcVehicleMap()
    {
        compClass = typeof(CompNpcVehicleMap);
    }
    
    public float pawnCountWeight = 1f;
    
    public List<VehicleMapParams> mapParamsList;

    public class VehicleMapParams
    {
        public IntRange pawnCountRange;

        public PrefabDef prefabDef;
        
        public Rot8 preferredDir;
    }
}