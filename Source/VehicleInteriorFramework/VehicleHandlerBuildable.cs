using HarmonyLib;
using RimWorld.Planet;
using System.Linq;
using Vehicles;
using Verse;

namespace VehicleInteriors
{
    public class VehicleHandlerBuildable : VehicleHandler, IExposable
    {
        public VehicleHandlerBuildable()
        {
            if (this.handlers == null)
            {
                this.handlers = new ThingOwner<Pawn>(this, false, LookMode.Deep);
            }
        }

        public VehicleHandlerBuildable(VehiclePawn vehicle) : this()
        {
            this.uniqueID = VehicleIdManager.Instance.GetNextHandlerId();
            this.vehicle = vehicle;
        }

        public VehicleHandlerBuildable(VehiclePawn vehicle, VehicleRoleBuildable role) : this(vehicle)
        {
            this.role = role;
            roleKey(this) = role.key;
        }

        new public void ExposeData()
        {
            Scribe_Values.Look<int>(ref this.uniqueID, "uniqueID", -1, false);
            Scribe_References.Look<VehiclePawn>(ref this.vehicle, "vehicle", true);
            Scribe_Values.Look<string>(ref roleKey(this), "role", null, true);
            if (Scribe.mode == LoadSaveMode.Saving)
            {
                ThingOwner thingOwner = this.handlers;
                Pawn pawn = this.handlers.InnerListForReading.FirstOrDefault<Pawn>();
                thingOwner.contentsLookMode = ((pawn != null && pawn.IsWorldPawn()) ? LookMode.Reference : LookMode.Deep);
            }
            Scribe_Deep.Look<ThingOwner<Pawn>>(ref this.handlers, "handlers", new object[]
            {
                this
            });
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                this.role = new VehicleRole();
                if (this.role == null)
                {
                    Log.Error("Unable to load role=" + roleKey(this) + ". Creating empty role to avoid game-breaking issues.");
                    if (this.role == null)
                    {
                        this.role = new VehicleRole
                        {
                            key = roleKey(this) + "_INVALID",
                            label = roleKey(this) + " (INVALID)"
                        };
                    }
                }
            }
        }

        private static readonly AccessTools.FieldRef<VehicleHandler, string> roleKey = AccessTools.FieldRefAccess<VehicleHandler, string>("roleKey");
    }
}
