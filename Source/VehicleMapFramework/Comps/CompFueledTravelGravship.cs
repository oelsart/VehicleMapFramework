using System.Linq;
using RimWorld;
using UnityEngine;
using Vehicles;
using Verse;

namespace VehicleMapFramework
{
    public class CompFueledTravelGravship : CompFueledTravel
    {
        private Building_GravEngine Engine => field ??= GravshipUtility.GetPlayerGravEngine_NewTemp((Vehicle as VehiclePawnWithMap)?.VehicleMap);

        public override float FuelCapacity => Engine?.MaxFuel ?? 0f;

        public override bool TickByRequest => false;
        
        internal const float EfficiencyIdleMultiplier = 0.5f;

        private bool ShouldConsumeNow => !EmptyTank && Vehicle.Spawned && (ConsumeWhenDrafted || ConsumeWhenMoving || ConsumeAlways);

        private bool ConsumeAlways => FuelCondition.HasFlag(FuelConsumptionCondition.Always);

        private bool ConsumeWhenDrafted => Vehicle.Spawned && FuelCondition.HasFlag(FuelConsumptionCondition.Drafted) && Vehicle.Drafted;

        private bool ConsumeWhenMoving
        {
            get
            {
                if (!FuelCondition.HasFlag(FuelConsumptionCondition.Moving)) return false;
                if (Vehicle.Spawned && Vehicle.vehiclePather.Moving)
                {
                    return true;
                }
                var caravan = Vehicle.GetVehicleCaravan();
                return caravan != null && caravan.vehiclePather.MovingNow;
            }
        }

        //少ない燃料コンテナから優先的に分配
        public override void Refuel(float amount)
        {
            if (Engine is null) return;
            if (Engine.TotalFuel >= Engine.MaxFuel)
            {
                return;
            }

            var comps = Engine.GravshipComponents
                .Where(c => c.CanBeActive && c.Props.providesFuel)
                .Select(c => c.parent.GetComp<CompRefuelable>())
                .Where(c => c is not null)
                .OrderByDescending(c => c.Props.fuelCapacity - c.Fuel).ToList();

            var num = amount;
            for (var i = 0; i < comps.Count - 1; i++)
            {
                var diff = Mathf.Min(comps[i + 1].Fuel - comps[i].Fuel, num);
                if (diff < Mathf.Epsilon) continue;

                num -= diff;
                var div = diff / (i + 1);
                for (var j = 0; j <= i; j++)
                {
                    var refuelActual = Mathf.Min(div, comps[j].Props.fuelCapacity - comps[j].Fuel);
                    comps[j].Refuel(refuelActual);
                    num += div - refuelActual;
                }
                if (num < Mathf.Epsilon) break;
            }
            while (num > Mathf.Epsilon)
            {
                comps.RemoveAll(c => c.IsFull);
                if (comps.Empty()) break;

                var div = num / comps.Count;
                num = 0f;
                foreach (var comp in comps)
                {
                    var refuelActual = Mathf.Min(div, comp.Props.fuelCapacity - comp.Fuel);
                    comp.Refuel(refuelActual);
                    num += div - refuelActual;
                }
            }
            base.Refuel(amount);
        }

        //バニラと同じく各コンテナから割合で消費
        public override void ConsumeFuel(float amount)
        {
            var num = amount / Engine.TotalFuel;
            foreach (var comp in from compGravshipFacility in Engine.GravshipComponents
                     where compGravshipFacility.CanBeActive && compGravshipFacility.Props.providesFuel
                     select compGravshipFacility.parent.GetComp<CompRefuelable>())
            {
                comp?.ConsumeFuel(comp.Fuel * num);
            }
            base.ConsumeFuel(amount);
        }

        public override void ConsumeFuelWorld()
        {
            if (Fuel <= 0f)
                return;

            var fuelToConsume = ConsumptionRateWorldPerTick;
            var caravan = Vehicle.GetVehicleCaravan();
            if (!caravan.vehiclePather.Moving) fuelToConsume *= EfficiencyIdleMultiplier;

            ConsumeFuel(fuelToConsume);
        }

        public override void CompTick()
        {
            if (ShouldConsumeNow)
            {
                base.CompTick();
            }
            var diff = Engine?.TotalFuel - Fuel ?? 0f;
            if (diff < Mathf.Epsilon) return;

            if (diff > 0f)
            {
                base.Refuel(diff);
            }
            else
            {
                base.ConsumeFuel(diff);
            }
        }

        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            base.PostSpawnSetup(respawningAfterLoad);
            if (respawningAfterLoad) return;

            FrameDelay.DelayOne(static instance =>
            {
                var diff = (instance.Engine?.TotalFuel - instance.Fuel) ?? 0f;
                if (diff < Mathf.Epsilon) return;

                if (diff > 0f)
                {
                    instance.Refuel(diff);
                }
                else
                {
                    instance.ConsumeFuel(diff);
                }
            }, this);
        }
    }
}
