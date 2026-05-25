using System.Collections.Generic;
using System.Linq;
using System.Text;
using RimWorld;
using Verse;

namespace VehicleMapFramework;

public class Building_Hatch : Building_Bed, ISlotGroupParent, IStorageGroupMember, IHaulEnroute
{

  public static readonly BedInteractionCellSearchPattern customBedInteractionCellsOrder = new BedInteractionCellSearchPattern3xN();

  private static readonly StringBuilder sb = new();

  public readonly SlotGroup slotGroup;

  private List<IntVec3> cachedOccupiedCells;

  public string label;
  public StorageSettings settings;

  public StorageGroup storageGroup;

  public Building_Hatch()
  {
    slotGroup = new SlotGroup(this);
  }

  public int SpaceRemainingFor(ThingDef _)
  {
    return slotGroup.HeldThingsCount - def.building.maxItemsInCell * def.Size.Area;
  }

  public bool StorageTabVisible => true;

  public bool IgnoreStoredThingsBeauty => def.building.ignoreStoredThingsBeauty;

  public SlotGroup GetSlotGroup()
  {
    return slotGroup;
  }

  public virtual void Notify_ReceivedThing(Thing newItem)
  {
    if (Faction == Faction.OfPlayer && newItem.def.storedConceptLearnOpportunity != null)
    {
      LessonAutoActivator.TeachOpportunity(newItem.def.storedConceptLearnOpportunity, OpportunityType.GoodToKnow);
    }
  }

  public virtual void Notify_LostThing(Thing newItem) { }

  public virtual IEnumerable<IntVec3> AllSlotCells()
  {
    if (!Spawned)
      yield break;
    foreach (var intVec in GenAdj.CellsOccupiedBy(this))
    {
      yield return intVec;
    }
  }

  public List<IntVec3> AllSlotCellsList()
  {
    return cachedOccupiedCells ??= AllSlotCells().ToList();
  }

  public StorageSettings GetStoreSettings()
  {
    return storageGroup?.GetStoreSettings() ?? settings;
  }

  public StorageSettings GetParentStoreSettings()
  {
    return def.building.fixedStorageSettings ?? StorageSettings.EverStorableFixedSettings();
  }

  public void Notify_SettingsChanged()
  {
    if (Spawned && slotGroup != null)
    {
      base.Map.listerHaulables.Notify_SlotGroupChanged(slotGroup);
    }
  }

  public string SlotYielderLabel()
  {
    return LabelCap;
  }

  public string GroupingLabel => def.building.groupingLabel;

  public int GroupingOrder => def.building.groupingOrder;

  public bool HaulDestinationEnabled => true;

  public bool Accepts(Thing t)
  {
    return GetStoreSettings().AllowedToAccept(t);
  }

  StorageGroup IStorageGroupMember.Group
  {
    get => storageGroup;
    set => storageGroup = value;
  }

  bool IStorageGroupMember.DrawConnectionOverlay => Spawned;

  Map IStorageGroupMember.Map => MapHeld;

  string IStorageGroupMember.StorageGroupTag => def.building.storageGroupTag;

  StorageSettings IStorageGroupMember.StoreSettings => GetStoreSettings();

  StorageSettings IStorageGroupMember.ParentStoreSettings => GetParentStoreSettings();

  StorageSettings IStorageGroupMember.ThingStoreSettings => settings;

  bool IStorageGroupMember.DrawStorageTab => true;

  bool IStorageGroupMember.ShowRenameButton => Faction == Faction.OfPlayer;

  public override void PostMake()
  {
    base.PostMake();
    settings = new StorageSettings(this);
    if (def.building.defaultStorageSettings != null)
    {
      settings.CopyFrom(def.building.defaultStorageSettings);
    }
  }

  public override void SpawnSetup(Map map, bool respawningAfterLoad)
  {
    cachedOccupiedCells = null;
    base.SpawnSetup(map, respawningAfterLoad);
    if (storageGroup != null && map != storageGroup.Map)
    {
      var storeSettings = storageGroup.GetStoreSettings();
      storageGroup.RemoveMember(this);
      storageGroup = null;
      settings.CopyFrom(storeSettings);
    }
  }

  public override void DeSpawn(DestroyMode mode = DestroyMode.Vanish)
  {
    base.DeSpawn(mode);
    cachedOccupiedCells = null;
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
    Scribe_Deep.Look(ref settings, "settings", this);
    Scribe_References.Look(ref storageGroup, "storageGroup");
    Scribe_Values.Look(ref label, "label");
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
    if (Spawned)
    {
      if (storageGroup != null)
      {
        sb.AppendLineIfNotEmpty();
        sb.Append(
          $"{"StorageGroupLabel".Translate()}: {storageGroup.RenamableLabel.CapitalizeFirst()} ");
        sb.Append(storageGroup.MemberCount > 1
          ? $"({"NumBuildings".Translate(storageGroup.MemberCount)})"
          : $"({"OneBuilding".Translate()})");
      }
      if (slotGroup.HeldThings.Any())
      {
        sb.AppendLineIfNotEmpty();
        sb.Append("StoresThings".Translate());
        sb.Append(": ");
        sb.Append(slotGroup.HeldThings.Select(x => x.LabelShortCap).Distinct().ToCommaList());
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
    foreach (var gizmo2 in StorageSettingsClipboard.CopyPasteGizmosFor(GetStoreSettings()))
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
          yield return ContainingSelectionUtility.CreateSelectStorageGizmo("CommandSelectStoredThing".Translate(thing),
            ("CommandSelectStoredThingDesc".Translate() + "\n\n" + thing.LabelCap.Colorize(ColoredText.TipSectionTitleColor) + "\n\n" + thing.GetInspectString()).Resolve(),
            thing,
            thing,
            false);
        }
      }
    }
  }
}
