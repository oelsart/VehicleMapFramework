using RimWorld.Planet;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse.AI;
using Verse;
using Verse.AI.Group;
using static UnityEngine.Scripting.GarbageCollector;
using System.Security.Cryptography;
using static UnityEngine.GraphicsBuffer;
using Unity.Jobs;

namespace VehicleInteriors
{
    public static class ToilsAcrossMaps
    {
        public static Toil GotoVehicleEnterSpot(Thing enterSpot)
        {
            Toil toil = ToilMaker.MakeToil("GotoThingOnVehicle");
            IntVec3 dest = enterSpot.PositionOnBaseMap() - enterSpot.BaseFullRotationOfThing().FacingCell;
            toil.initAction = delegate ()
            {
                if (toil.actor.Position == dest)
                {
                    toil.actor.jobs.curDriver.ReadyForNextToil();
                    return;
                }
                toil.actor.pather.StartPath(dest, PathEndMode.OnCell);
            };
            toil.defaultCompleteMode = ToilCompleteMode.PatherArrival;
            toil.tickAction = () =>
            {
                var curDest = enterSpot.PositionOnBaseMap() - enterSpot.BaseFullRotationOfThing().FacingCell;
                if (dest != curDest)
                {
                    dest = curDest;
                    toil.actor.pather.StartPath(dest, PathEndMode.OnCell);
                }
            };
            toil.FailOn(() =>
            {
                return enterSpot == null || !enterSpot.Spawned || enterSpot.BaseMapOfThing() != toil.actor.BaseMapOfThing();
            });
            return toil;
        }
    }
}