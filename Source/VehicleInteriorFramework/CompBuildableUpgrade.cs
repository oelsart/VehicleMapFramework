using SmashTools;
using System.Collections.Generic;
using Verse;

namespace VehicleInteriors
{
    public class CompBuildableUpgrade : ThingComp
    {
        public CompProperties_BuildableUpgrade Props
        {
            get
            {
                return (CompProperties_BuildableUpgrade)this.props;
            }
        }
        
        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            if (this.parent.IsOnVehicleMapOf(out var vehicle))
            {
                foreach (var upgrade in this.Props.upgrades)
                {
                    if (upgrade is VehicleUpgradeBuildable buildable)
                    {
                        buildable.parent = this;
                        buildable.Unlock(vehicle, respawningAfterLoad);
                    }
                    else
                    {
                        upgrade.Unlock(vehicle, respawningAfterLoad);
                    }
                }
            }
        }

        public override void PostDeSpawn(Map map)
        {
            if (map.IsVehicleMapOf(out var vehicle))
            {
                foreach (var upgrade in this.Props.upgrades)
                {
                    if (upgrade is VehicleUpgradeBuildable buildable)
                    {
                        buildable.parent = this;
                        buildable.Refund(vehicle);
                    }
                    else
                    {
                        upgrade.Refund(vehicle);
                    }
                }
            }
        }

        public override void PostExposeData()
        {
            Scribe_Collections.Look(ref this.handlerUniqueIDs, "handlerUniqueIDs", LookMode.Deep);
        }

        public List<UpgradeID> handlerUniqueIDs = new List<UpgradeID>();
    }

    public class UpgradeID : IExposable
    {
        public string key;

        public string editKey;

        public int id;

        public UpgradeID() { }

        public UpgradeID(string key, string editKey, int id)
        {
            this.key = key;
            this.editKey = editKey;
            this.id = id;
        }

        public void ExposeData()
        {
            Scribe_Values.Look(ref this.key, "key");
            Scribe_Values.Look(ref this.editKey, "editKey");
            Scribe_Values.Look(ref this.id, "id");
        }
    }
}
