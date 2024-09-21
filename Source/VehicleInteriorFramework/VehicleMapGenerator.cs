using HarmonyLib;
using RimWorld.Planet;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using Verse;

namespace VehicleInteriors
{
    [HarmonyPatch]
    public static class VehicleMapGenerator
    {
        [HarmonyPatch(typeof(MapGenerator), nameof(MapGenerator.GenerateMap))]
        [HarmonyReversePatch]
        public static Map GenerateMap(IntVec3 mapSize, MapParent parent, MapGeneratorDef mapGenerator, IEnumerable<GenStepWithParams> extraGenStepDefs = null, Action<Map> extraInitBeforeContentGen = null, bool isPocketMap = false)
        {
            IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
            {
                var codes = instructions.ToList();
                var AddMap = AccessTools.Method(typeof(Game), nameof(Game.AddMap));
                var pos = codes.FindIndex(c => c.opcode == OpCodes.Callvirt && c.operand.Equals(AddMap));
                codes.RemoveRange(pos - 3, 4);
                return codes;
            }
            _ = Transpiler(null);

            throw new NotImplementedException();
        }
    }
}
