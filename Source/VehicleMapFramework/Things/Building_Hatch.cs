using System.Collections.Generic;
using System.Linq;
using System.Text;
using RimWorld;
using Verse;

namespace VehicleMapFramework;

public class Building_Hatch : Building_Bed, ISlotGroupParent, IStorageGroupMember, IHaulEnroute
{
    public StorageSettings settings;

    public StorageGroup storageGroup;

    public string label;

    public readonly SlotGroup slotGroup;

    private List<IntVec3> cachedOccupiedCells;
    
    public static readonly BedInteractionCellSearchPattern customBedInteractionCellsOrder = new BedInteractionCellSearchPattern3xN();

    private static readonly StringBuilder sb = new ();
    
    public Building_Hatch()
	{
		this.slotGroup = new SlotGroup(this);
	}

	StorageGroup IStorageGroupMember.Group
	{
		get => this.storageGroup;
        set => this.storageGroup = value;
    }

	bool IStorageGroupMember.DrawConnectionOverlay => Spawned;

    Map IStorageGroupMember.Map => MapHeld;

    string IStorageGroupMember.StorageGroupTag => def.building.storageGroupTag;

    StorageSettings IStorageGroupMember.StoreSettings => GetStoreSettings();

    StorageSettings IStorageGroupMember.ParentStoreSettings => GetParentStoreSettings();

    StorageSettings IStorageGroupMember.ThingStoreSettings => settings;

    bool IStorageGroupMember.DrawStorageTab => true;

    bool IStorageGroupMember.ShowRenameButton => Faction == Faction.OfPlayer;

    public bool StorageTabVisible => true;

    public bool IgnoreStoredThingsBeauty => def.building.ignoreStoredThingsBeauty;

    public SlotGroup GetSlotGroup()
	{
		return this.slotGroup;
	}

	public virtual void Notify_ReceivedThing(Thing newItem)
	{
		if (Faction == Faction.OfPlayer && newItem.def.storedConceptLearnOpportunity != null)
		{
			LessonAutoActivator.TeachOpportunity(newItem.def.storedConceptLearnOpportunity, OpportunityType.GoodToKnow);
		}
	}

	public virtual void Notify_LostThing(Thing newItem)
	{
	}

	public virtual IEnumerable<IntVec3> AllSlotCells()
	{
		if (!base.Spawned)
			yield break;
		foreach (var intVec in GenAdj.CellsOccupiedBy(this))
			yield return intVec;
	}

    public List<IntVec3> AllSlotCellsList() => cachedOccupiedCells ??= AllSlotCells().ToList();

	public StorageSettings GetStoreSettings() => storageGroup?.GetStoreSettings() ?? this.settings;

    public StorageSettings GetParentStoreSettings() =>
        def.building.fixedStorageSettings ?? StorageSettings.EverStorableFixedSettings();

	public void Notify_SettingsChanged()
	{
		if (base.Spawned && this.slotGroup != null)
		{
			base.Map.listerHaulables.Notify_SlotGroupChanged(this.slotGroup);
		}
	}

    public string SlotYielderLabel() => LabelCap;

	public string GroupingLabel => this.def.building.groupingLabel;

    public int GroupingOrder => this.def.building.groupingOrder;

    public bool HaulDestinationEnabled => true;

    public bool Accepts(Thing t)
	{
		return this.GetStoreSettings().AllowedToAccept(t);
	}

	public int SpaceRemainingFor(ThingDef _) =>
        slotGroup.HeldThingsCount - def.building.maxItemsInCell * def.Size.Area;

	public override void PostMake()
	{
		base.PostMake();
		this.settings = new StorageSettings(this);
		if (this.def.building.defaultStorageSettings != null)
		{
			this.settings.CopyFrom(this.def.building.defaultStorageSettings);
		}
	}

	public override void SpawnSetup(Map map, bool respawningAfterLoad)
	{
		this.cachedOccupiedCells = null;
		base.SpawnSetup(map, respawningAfterLoad);
		if (this.storageGroup != null && map != storageGroup.Map)
		{
			var storeSettings = storageGroup.GetStoreSettings();
			this.storageGroup.RemoveMember(this);
			this.storageGroup = null;
			this.settings.CopyFrom(storeSettings);
		}
	}

	public override void DeSpawn(DestroyMode mode = DestroyMode.Vanish)
	{
		base.DeSpawn(mode);
		this.cachedOccupiedCells = null;
	}

	public override void Destroy(DestroyMode mode = DestroyMode.Vanish)
	{
		base.Destroy(mode);
		if (storageGroup != null)
		{
            storageGroup?.RemoveMember(this);
            storageGroup = null;
		}
		BillUtility.Notify_ISlotGroupRemoved(slotGroup);
	}

	public override void ExposeData()
	{
		base.ExposeData();
		Scribe_Deep.Look(ref this.settings, "settings", this);
		Scribe_References.Look(ref this.storageGroup, "storageGroup");
		Scribe_Values.Look(ref this.label, "label");
	}

	public override void DrawExtraSelectionOverlays()
	{
		base.DrawExtraSelectionOverlays();
		StorageGroupUtility.DrawSelectionOverlaysFor(this);
	}

	public override string GetInspectString()
	{
		sb.Clear();
		sb.Append(base.GetInspectString());
		if (base.Spawned)
		{
			if (this.storageGroup != null)
			{
				sb.AppendLineIfNotEmpty();
				sb.Append(
                    $"{"StorageGroupLabel".Translate()}: {this.storageGroup.RenamableLabel.CapitalizeFirst()} ");
                sb.Append(this.storageGroup.MemberCount > 1
                    ? $"({"NumBuildings".Translate(this.storageGroup.MemberCount)})"
                    : $"({"OneBuilding".Translate()})");
            }
			if (slotGroup.HeldThings.Any())
			{
				sb.AppendLineIfNotEmpty();
				sb.Append("StoresThings".Translate());
				sb.Append(": ");
				sb.Append(this.slotGroup.HeldThings.Select(x => x.LabelShortCap).Distinct().ToCommaList());
				sb.Append(".");
			}
		}
		return sb.ToString();
	}

	public override IEnumerable<Gizmo> GetGizmos()
	{
		foreach (var gizmo in base.GetGizmos())
		{
			yield return gizmo;
		}
		foreach (var gizmo2 in StorageSettingsClipboard.CopyPasteGizmosFor(this.GetStoreSettings()))
		{
			yield return gizmo2;
		}
		if (StorageTabVisible && MapHeld != null)
		{
			foreach (var gizmo3 in StorageGroupUtility.StorageGroupMemberGizmos(this))
			{
				yield return gizmo3;
			}
			if (Find.Selector.NumSelected == 1)
			{
				foreach (var thing in slotGroup.HeldThings)
				{
					yield return ContainingSelectionUtility.CreateSelectStorageGizmo("CommandSelectStoredThing".Translate(thing), ("CommandSelectStoredThingDesc".Translate() + "\n\n" + thing.LabelCap.Colorize(ColoredText.TipSectionTitleColor) + "\n\n" + thing.GetInspectString()).Resolve(), thing, thing, false);
				}
			}
		}
	}
}