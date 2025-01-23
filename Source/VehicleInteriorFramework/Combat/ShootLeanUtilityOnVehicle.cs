using System.Collections.Generic;
using Verse;

namespace VehicleInteriors
{
    public static class ShootLeanUtilityOnVehicle
    {
        private static bool[] GetWorkingBlockedArray()
        {
            if (ShootLeanUtilityOnVehicle.blockedArrays.Count > 0)
            {
                return ShootLeanUtilityOnVehicle.blockedArrays.Dequeue();
            }
            return new bool[8];
        }

        private static void ReturnWorkingBlockedArray(bool[] ar)
        {
            ShootLeanUtilityOnVehicle.blockedArrays.Enqueue(ar);
            if (ShootLeanUtilityOnVehicle.blockedArrays.Count > 128)
            {
                Log.ErrorOnce("Too many blocked arrays to be feasible. >128", 388121);
            }
        }

        public static void CalcShootableCellsOf(List<IntVec3> outCells, Thing t, IntVec3 shooterPosOnBaseMap)
        {
            outCells.Clear();
            if (t is Pawn)
            {
                ShootLeanUtilityOnVehicle.LeanShootingSourcesFromTo(t.Position, shooterPosOnBaseMap, t.Map, outCells);
                return;
            }
            outCells.Add(t.Position);
            if (t.def.size.x != 1 || t.def.size.z != 1)
            {
                foreach (IntVec3 intVec in t.OccupiedRect())
                {
                    if (intVec != t.Position)
                    {
                        outCells.Add(intVec);
                    }
                }
            }
        }

        public static void LeanShootingSourcesFromTo(IntVec3 shooterLoc, IntVec3 targetPosBaseCol, Map map, List<IntVec3> listToFill)
        {
            var shooterLocBaseCol = shooterLoc;
            var baseMap = map.BaseMap();
            if (map.IsVehicleMapOf(out var vehicle))
            {
                shooterLocBaseCol = shooterLoc.ToBaseMapCoord(vehicle);
            }
            listToFill.Clear();
            float angleFlat = (targetPosBaseCol - shooterLocBaseCol).AngleFlat;
            bool flag = angleFlat > 270f || angleFlat < 90f;
            bool flag2 = angleFlat > 90f && angleFlat < 270f;
            bool flag3 = angleFlat > 180f;
            bool flag4 = angleFlat < 180f;
            bool[] workingBlockedArray = ShootLeanUtilityOnVehicle.GetWorkingBlockedArray();
            for (int i = 0; i < 8; i++)
            {
                workingBlockedArray[i] = !(shooterLocBaseCol + GenAdj.AdjacentCells[i]).CanBeSeenOverOnVehicle(baseMap);
            }
            if (!workingBlockedArray[1] && ((workingBlockedArray[0] && !workingBlockedArray[5] && flag) || (workingBlockedArray[2] && !workingBlockedArray[4] && flag2)))
            {
                listToFill.Add(shooterLoc + new IntVec3(1, 0, 0));
            }
            if (!workingBlockedArray[3] && ((workingBlockedArray[0] && !workingBlockedArray[6] && flag) || (workingBlockedArray[2] && !workingBlockedArray[7] && flag2)))
            {
                listToFill.Add(shooterLoc + new IntVec3(-1, 0, 0));
            }
            if (!workingBlockedArray[2] && ((workingBlockedArray[3] && !workingBlockedArray[7] && flag3) || (workingBlockedArray[1] && !workingBlockedArray[4] && flag4)))
            {
                listToFill.Add(shooterLoc + new IntVec3(0, 0, -1));
            }
            if (!workingBlockedArray[0] && ((workingBlockedArray[3] && !workingBlockedArray[6] && flag3) || (workingBlockedArray[1] && !workingBlockedArray[5] && flag4)))
            {
                listToFill.Add(shooterLoc + new IntVec3(0, 0, 1));
            }
            if (shooterLocBaseCol.CanBeSeenOverOnVehicle(baseMap))
            {
                listToFill.Add(shooterLoc);
            }
            for (int j = 0; j < 4; j++)
            {
                var adjacentCell = shooterLoc + GenAdj.AdjacentCells[j];
                if (!workingBlockedArray[j] && (j != 0 || flag) && (j != 1 || flag4) && (j != 2 || flag2) && (j != 3 || flag3) && adjacentCell.InBounds(map) && adjacentCell.GetCover(map) != null)
                {
                    listToFill.Add(shooterLoc + GenAdj.AdjacentCells[j]);
                }
            }
            ShootLeanUtilityOnVehicle.ReturnWorkingBlockedArray(workingBlockedArray);
        }

        private static Queue<bool[]> blockedArrays = new Queue<bool[]>();
    }
}
