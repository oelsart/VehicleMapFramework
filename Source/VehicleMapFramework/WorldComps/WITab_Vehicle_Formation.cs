using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using SmashTools;
using UnityEngine;
using VehicleMapFramework.VMF_HarmonyPatches;
using Vehicles;
using Vehicles.Rendering;
using Verse;

namespace VehicleMapFramework;

public class WITab_Vehicle_Formation : WITab
{
  private static readonly Vector2 WinSize = new(430f, 440f);
  private static readonly Vector2 ViewportSize = new(400f, 400f);
  private const float Padding = 10f;
  private const float SaveLoadButtonWidth = 180f;
  private const float SaveLoadButtonHeight = 22f;

  public Dictionary<VehiclePawn, VehiclePortrait> portraits;
  private Vector2 scrollPosition;
  private float zoom;

  private VehiclePawn lastDraggedVehicle;
  private Vector2 dragOffset;
  private VehiclePawn curDraggedVehicle;
  private CellRect curDraggedCellRect;
  private int dragAndDropGroup;
  private bool collision;

  public override bool IsVisible => VehicleMapUtility.VFUnstableRelease.Available &&
    SelObject.Vehicles.Any(v => v is VehiclePawnWithMap);

  protected VehicleFormationComp FormationComp => SelObject.GetComponent<VehicleFormationComp>();

  public WITab_Vehicle_Formation()
  {
    size = WinSize;
    labelKey = "VMF_VehicleFormation";
  }

  public override void OnOpen()
  {
    base.OnOpen();
    var drawPositions = FormationComp?.DrawPositions;
    if (drawPositions is null) return;

    var vehicleRect = CellRect.Empty;
    foreach (var value in drawPositions.Values)
    {
      vehicleRect = vehicleRect.Encapsulate(value.cellRect);
    }

    var winRect = new Rect(Vector2.zero, WinSize);
    var outRect = new Rect(Vector2.zero, new Vector2(ViewportSize.x, WinSize.y))
      .BottomPartPixels(ViewportSize.y)
      .CenteredOnXIn(winRect);
    outRect.y -= Padding;
    zoom = Mathf.Min(outRect.width, outRect.height) / Mathf.Max(vehicleRect.Width, vehicleRect.Height) * 0.8f;
    scrollPosition = vehicleRect.CenterVector3.MirrorVertical().ToVector2() +
                     Patch_Map_MapUpdate.MeshSize / 2f -
                     (ViewportSize / 2f - outRect.position / 2f) / zoom;

    portraits ??= [];
    foreach (var vehicle in drawPositions.Keys)
    {
      portraits[vehicle] = new VehiclePortrait();
    }
  }

  protected override void FillTab()
  {
    var drawPositions = FormationComp?.DrawPositions;
    if (drawPositions is null) return;
    
    var component = Find.World.GetComponent<VehicleFormationManager>();
    if (component?.FormationPresets is { } formationPresets)
    {
      var saveButRect = new Rect(
        WinSize.x / 2f - 2.5f - SaveLoadButtonWidth, 5f,
        SaveLoadButtonWidth, SaveLoadButtonHeight);
      if (Widgets.ButtonText(saveButRect, "Save".Translate()))
      {
        Find.WindowStack.Add(new Dialog_FormationPresetList(formationPresets, "Save".Translate(),
          i =>
          {
            formationPresets[i].drawPositions = drawPositions.ToDictionary(p => p.Key, p => p.Value);
            Messages.Message("SavedAs".Translate(formationPresets[i].RenamableLabel), MessageTypeDefOf.NeutralEvent, false);
          },
          name =>
          {
            formationPresets.Add(new VehicleFormationManager.FormationPreset
            {
              drawPositions = drawPositions.ToDictionary(p => p.Key, p => p.Value),
              RenamableLabel = name
            });
            Messages.Message("SavedAs".Translate(name), MessageTypeDefOf.NeutralEvent, false);
          }));
      }
      var loadButRect = new Rect(
        WinSize.x / 2f + 2.5f, 5f,
        SaveLoadButtonWidth, SaveLoadButtonHeight);
      if (Widgets.ButtonText(loadButRect, "Load".Translate()))
      {
        Find.WindowStack.Add(new Dialog_FormationPresetList(formationPresets, "Load".Translate(),
          i =>
          {
            drawPositions.Clear();
            var vehicles = SelObject.Vehicles.ToList();
            foreach (var (vehicle, data) in formationPresets[i].drawPositions)
            {
              if (!vehicles.Contains(vehicle)) continue;
              
              drawPositions[vehicle] = data;
            }
            FormationComp.CenteredDrawPositions();
          }));
      }
    }

    Action overlayAction = null;
    var outRect = new Rect(Vector2.zero, new Vector2(ViewportSize.x, WinSize.y))
      .BottomPartPixels(ViewportSize.y)
      .CenteredOnXIn(new Rect(Vector2.zero, WinSize));
    outRect.y -= Padding;
    Widgets.DrawWindowBackground(outRect, new Color(0.4f, 0.8f, 0.4f));
    var viewRect = new Rect(Vector2.zero, Patch_Map_MapUpdate.MeshSize);

    var groupID = DragAndDropWidget.NewGroup();
    dragAndDropGroup = groupID == -1 ? dragAndDropGroup : groupID;
    VMF_Widgets.BeginZoomPanArea(outRect, ref scrollPosition, ref zoom, viewRect, outRect.width / viewRect.width, 50f,
      ignoreDragGroup: dragAndDropGroup);
    DragAndDropWidget.DropArea(dragAndDropGroup, outRect.ContractedBy(5f).AtZero(), OnDrop, null);

    var center = Patch_Map_MapUpdate.MeshSize / 2f;

    foreach (var (vehicle, value) in drawPositions)
    {
      if (!portraits.TryGetValue(vehicle, out var portrait))
      {
        portrait = portraits[vehicle] = new VehiclePortrait();
      }

      var cellRect = value.cellRect;
      var contentRect = new Rect(center + new Vector2(cellRect.minX, -cellRect.maxZ), cellRect.Size.ToVector2());
      var guiRect = VMF_Widgets.ContentToGUI(contentRect);

      if (DragAndDropWidget.Draggable(dragAndDropGroup, guiRect, vehicle))
      {
        var mousePosGUI = Event.current.mousePosition;
        if (lastDraggedVehicle != vehicle)
        {
          lastDraggedVehicle = vehicle;
          dragOffset = mousePosGUI - guiRect.position;
        }

        var currentGUIPos = mousePosGUI - dragOffset;
        var currentContentPos = VMF_Widgets.GUIToContent(currentGUIPos);

        currentContentPos.x = Mathf.Round(currentContentPos.x);
        currentContentPos.y = Mathf.Round(currentContentPos.y);

        guiRect = VMF_Widgets.ContentToGUI(new Rect(currentContentPos, contentRect.size));

        curDraggedVehicle = vehicle;
        var newMinX = Mathf.RoundToInt(currentContentPos.x - center.x);
        var newMaxZ = Mathf.RoundToInt(center.y - currentContentPos.y);
        var newMinZ = newMaxZ - cellRect.Height + 1;
        curDraggedCellRect = [with(newMinX, newMinZ, cellRect.Width, cellRect.Height)];
        
        var activeContentRect = new Rect(currentContentPos, contentRect.size);
        overlayAction = () =>
        {
          DrawFadingGrid(activeContentRect);
          collision = false;
          foreach (var pair in drawPositions)
          {
            if (pair.Key == curDraggedVehicle) continue;
            var cellRect2 = pair.Value.cellRect;
            if (!cellRect2.Overlaps(curDraggedCellRect)) continue;
            foreach (var cell in cellRect2.ClipInsideRect(curDraggedCellRect))
            {
              collision = true;
              var rect = VMF_Widgets.ContentToGUI(new Rect(center + new Vector2(cell.x, -cell.z + 1), Vector2.one));
              Widgets.DrawBoxSolidWithOutline(rect, Color.red.WithAlpha(0.2f), Color.red.WithAlpha(0.4f));
            }
          }
        };
      }
      else if (lastDraggedVehicle == vehicle && !DragAndDropWidget.Dragging)
      {
        lastDraggedVehicle = null;
      }

      var request = BlitRequest.For(vehicle);
      request.rot = Rot8.North;
      var guiRect2 = guiRect with
      {
        size = (vehicle.VehicleDef.graphicData.drawSize / vehicle.VehicleDef.uiIconScale) * zoom,
        center = guiRect.center +
                 (vehicle is VehiclePawnWithMap { CompVehicleDrawOffset: { } offsetComp }
                   ? (offsetComp.DrawOffsetFull(Rot8.North).MirrorVertical().ToVector2()) * zoom
                   : Vector2.zero)
      };
      portrait.Draw(guiRect2, request);
    }
    overlayAction?.Invoke();

    VMF_Widgets.EndZoomPanArea();
  }

  private void OnDrop(object obj)
  {
    if (obj is VehiclePawn droppedVehicle && curDraggedVehicle == droppedVehicle && !collision)
    {
      FormationComp.DrawPositions[droppedVehicle] = new VehicleFormationComp.DrawData(
        curDraggedCellRect,
        curDraggedCellRect.CenterVector3.SetToAltitude(AltitudeLayer.LayingPawn));
      FormationComp.CenteredDrawPositions();
    }
    curDraggedVehicle = null;
  }

  protected override void CloseTab()
  {
    base.CloseTab();
    FormationComp?.CenteredDrawPositions();
    foreach (var portrait in portraits.Values)
    {
      portrait.Dispose();
    }

    portraits.Clear();
  }
  
  public static void DrawFadingGrid(Rect contentRect, int radius = 4, float maxAlpha = 0.2f)
  {
    if (Event.current.type != EventType.Repaint) return;

    var minX = Mathf.FloorToInt(contentRect.xMin) - radius;
    var maxX = Mathf.CeilToInt(contentRect.xMax) + radius;
    var minY = Mathf.FloorToInt(contentRect.yMin) - radius;
    var maxY = Mathf.CeilToInt(contentRect.yMax) + radius;

    for (var x = minX; x < maxX; x++)
    {
      for (var y = minY; y < maxY; y++)
      {
        var cellContentRect = new Rect(x, y, 1f, 1f);
        if (contentRect.Contains(cellContentRect.position))
          continue;
        
        var cellGUIRect = VMF_Widgets.ContentToGUI(cellContentRect);
        
        var dx = 0f;
        if (x + 1 < contentRect.xMin) dx = contentRect.xMin - (x + 1);
        else if (x > contentRect.xMax) dx = x - contentRect.xMax;

        var dy = 0f;
        if (y + 1 < contentRect.yMin) dy = contentRect.yMin - (y + 1);
        else if (y > contentRect.yMax) dy = y - contentRect.yMax;

        var dist = new Vector2(dx, dy).magnitude;
        if (dist > radius) continue;

        var alpha = Mathf.Lerp(maxAlpha, 0f, dist / radius);
        if (alpha <= 0.001f) continue;

        var color = GUI.color;
        GUI.color = new Color(1f, 1f, 1f, alpha);
        Widgets.DrawBox(cellGUIRect);
        GUI.color = color;
      }
    }
  }
}