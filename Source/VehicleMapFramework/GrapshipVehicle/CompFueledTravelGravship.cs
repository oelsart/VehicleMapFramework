using RimWorld;
using System.Linq;
using UnityEngine;
using Vehicles;
using Vehicles.World;
using Verse;

namespace VehicleMapFramework
{
    public class CompFueledTravelGravship : CompFueledTravel
    {
        private Building_GravEngine cachedGravEngine;

        public Building_GravEngine Engine => cachedGravEngine ??= GravshipUtility.GetPlayerGravEngine((Vehicle as VehiclePawnWithMap)?.VehicleMap) as Building_GravEngine;

        public override float FuelCapacity => Engine.MaxFuel;

        public override bool TickByRequest
        {
            get
            {
                return false;
            }
        }

        private bool ShouldConsumeNow
        {
            get
            {
                return !EmptyTank && Vehicle.Spawned && (ConsumeWhenDrafted || ConsumeWhenMoving || ConsumeAlways);
            }
        }

        private bool ConsumeAlways
        {
            get
            {
                return FuelCondition.HasFlag(FuelConsumptionCondition.Always);
            }
        }

        private bool ConsumeWhenDrafted
        {
            get
            {
                return Vehicle.Spawned && FuelCondition.HasFlag(FuelConsumptionCondition.Drafted) && Vehicle.Drafted;
            }
        }

        private bool ConsumeWhenMoving
        {
            get
            {
                if (FuelCondition.HasFlag(FuelConsumptionCondition.Moving))
                {
                    if (Vehicle.Spawned && Vehicle.vehiclePather.Moving)
                    {
                        return true;
                    }
                    VehicleCaravan caravan = Vehicle.GetVehicleCaravan();
                    if (caravan != null && caravan.vehiclePather.MovingNow)
                    {
                        return true;
                    }
                }
                return false;
            }
        }

        //少ない燃料コンテナから優先的に分配
        public override void Refuel(float amount)
        {
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
            foreach (var compGravshipFacility in Engine.GravshipComponents)
            {
                if (compGravshipFacility.CanBeActive && compGravshipFacility.Props.providesFuel)
                {
                    CompRefuelable comp = compGravshipFacility.parent.GetComp<CompRefuelable>();
                    comp?.ConsumeFuel(comp.Fuel * num);
                }
            }
            base.ConsumeFuel(amount);
        }

        public override void StartTicking()
        {
            base.StartTicking();
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

            Delay.AfterNSeconds(0, () =>
            {
                var diff = (Engine?.TotalFuel - Fuel) ?? 0f;
                if (diff < Mathf.Epsilon) return;

                if (diff > 0f)
                {
                    base.Refuel(diff);
                }
                else
                {
                    base.ConsumeFuel(diff);
                }
            });
        }
    }
}
