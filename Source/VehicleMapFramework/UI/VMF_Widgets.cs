using System.Collections.Generic;
using LudeonTK;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace VehicleMapFramework;

[StaticConstructorOnStartup]
public static class VMF_Widgets
{
  private static readonly Texture2D SliderRailAtlas = ContentFinder<Texture2D>.Get("UI/Buttons/SliderRail");

  private static readonly Texture2D SliderHandle = ContentFinder<Texture2D>.Get("UI/Buttons/SliderHandle");

  private static readonly Color RangeControlTextColor = new(0.6f, 0.6f, 0.6f);

  private static float lastDragSliderSoundTime = -1f;

  private static int sliderDraggingID;

  private struct ZoomPanState
  {
    public Vector2 scrollPosition;
    public float zoom;
  }

  private static readonly Stack<ZoomPanState> zoomPanStack = [];

  public static Vector2 CurrentScrollPosition =>
    zoomPanStack.Count > 0 ? zoomPanStack.Peek().scrollPosition : Vector2.zero;

  public static float CurrentZoom => zoomPanStack.Count > 0 ? zoomPanStack.Peek().zoom : 1f;

  public static Vector2 GUIToContent(Vector2 guiPos)
  {
    if (zoomPanStack.Count == 0) return guiPos;
    var state = zoomPanStack.Peek();
    return (guiPos / state.zoom) + state.scrollPosition;
  }

  public static Rect GUIToContent(Rect guiRect)
  {
    if (zoomPanStack.Count == 0) return guiRect;
    var state = zoomPanStack.Peek();
    return new Rect(
      (guiRect.x / state.zoom) + state.scrollPosition.x,
      (guiRect.y / state.zoom) + state.scrollPosition.y,
      guiRect.width / state.zoom,
      guiRect.height / state.zoom);
  }

  public static Vector2 ContentToGUI(Vector2 contentPos)
  {
    if (zoomPanStack.Count == 0) return contentPos;
    var state = zoomPanStack.Peek();
    return (contentPos - state.scrollPosition) * state.zoom;
  }

  public static Rect ContentToGUI(Rect contentRect)
  {
    if (zoomPanStack.Count == 0) return contentRect;
    var state = zoomPanStack.Peek();
    return new Rect(
      (contentRect.x - state.scrollPosition.x) * state.zoom,
      (contentRect.y - state.scrollPosition.y) * state.zoom,
      contentRect.width * state.zoom,
      contentRect.height * state.zoom);
  }

  public static Vector2 MousePositionContent => GUIToContent(Event.current.mousePosition);

  private static readonly Stack<Matrix4x4> matrixStack = [];

  private static int activePanControlID;

  public static float HorizontalSlider(Rect rect, float value, float min, float max, bool middleAlignment = false,
    string label = null, string leftAlignedLabel = null, string rightAlignedLabel = null, float roundTo = -1f,
    Color colorFactor = default)
  {
    var color = GUI.color;
    var num = value;
    if (middleAlignment || !label.NullOrEmpty())
    {
      rect.y += Mathf.Round((rect.height - 10f) / 2f);
    }

    if (!label.NullOrEmpty())
    {
      rect.y += 5f;
    }

    var num2 = UI.GUIToScreenPoint(new Vector2(rect.x, rect.y)).GetHashCode();
    num2 = Gen.HashCombine(num2, rect.width);
    num2 = Gen.HashCombine(num2, rect.height);
    num2 = Gen.HashCombine(num2, min);
    num2 = Gen.HashCombine(num2, max);
    var rect2 = rect;
    rect2.xMin += 6f;
    rect2.xMax -= 6f;
    GUI.color = RangeControlTextColor * colorFactor;
    Rect rect3 = new(rect2.x, rect2.y + 2f, rect2.width, 8f);
    Widgets.DrawAtlas(rect3, SliderRailAtlas);
    GUI.color = colorFactor;
    var x = Mathf.Clamp(rect2.x - 6f + (rect2.width * Mathf.InverseLerp(min, max, num)), rect2.xMin - 6f,
      rect2.xMax - 6f);
    GUI.DrawTexture(new Rect(x, rect3.center.y - 6f, 12f, 12f), SliderHandle);
    if (Event.current.type == EventType.MouseDown && Mouse.IsOver(rect) && sliderDraggingID != num2)
    {
      sliderDraggingID = num2;
      SoundDefOf.DragSlider.PlayOneShotOnCamera();
      Event.current.Use();
    }

    if (sliderDraggingID == num2 && UnityGUIBugsFixer.MouseDrag())
    {
      num = Mathf.Clamp(((Event.current.mousePosition.x - rect2.x) / rect2.width * (max - min)) + min, min, max);
      if (Event.current.type == EventType.MouseDrag)
      {
        Event.current.Use();
      }
    }

    if (!label.NullOrEmpty() || !leftAlignedLabel.NullOrEmpty() || !rightAlignedLabel.NullOrEmpty())
    {
      var anchor = Text.Anchor;
      var font = Text.Font;
      Text.Font = GameFont.Small;
      var num3 = label.NullOrEmpty() ? 18f : Text.CalcSize(label).y;
      rect.y = rect.y - num3 + 3f;
      if (!leftAlignedLabel.NullOrEmpty())
      {
        Text.Anchor = TextAnchor.UpperLeft;
        Widgets.Label(rect, leftAlignedLabel);
      }

      if (!rightAlignedLabel.NullOrEmpty())
      {
        Text.Anchor = TextAnchor.UpperRight;
        Widgets.Label(rect, rightAlignedLabel);
      }

      if (!label.NullOrEmpty())
      {
        Text.Anchor = TextAnchor.UpperCenter;
        Widgets.Label(rect, label);
      }

      Text.Anchor = anchor;
      Text.Font = font;
    }

    if (roundTo > 0f)
    {
      num = Mathf.RoundToInt(num / roundTo) * roundTo;
    }

    if (!Mathf.Approximately(value, num))
    {
      if (Time.realtimeSinceStartup > lastDragSliderSoundTime + 0.075f)
      {
        SoundDefOf.DragSlider.PlayOneShotOnCamera();
        lastDragSliderSoundTime = Time.realtimeSinceStartup;
      }
    }

    GUI.color = color;
    return num;
  }

  public static void DrawBoxRotated(Rect rect, int thickness = 1, Texture2D lineTexture = null, float rotation = 0f)
  {
    var center = rect.center;
    var vector = RotatePoint(new Vector2(rect.x, rect.y), center, -rotation);
    var vector2 = RotatePoint(new Vector2(rect.xMax, rect.yMax), center, -rotation);
    if (vector.x > vector2.x)
    {
      (vector.x, vector2.x) = (vector2.x, vector.x);
    }

    if (vector.y > vector2.y)
    {
      (vector.y, vector2.y) = (vector2.y, vector.y);
    }

    Vector3 vector3 = vector2 - vector;
    var matrix = GUI.matrix;
    UI.RotateAroundPivot(rotation, center);
    GUI.DrawTexture(UIScaling.AdjustRectToUIScaling(new Rect(vector.x, vector.y, thickness, vector3.y)),
      lineTexture ?? BaseContent.WhiteTex);
    GUI.DrawTexture(UIScaling.AdjustRectToUIScaling(new Rect(vector2.x - thickness, vector.y, thickness, vector3.y)),
      lineTexture ?? BaseContent.WhiteTex);
    GUI.DrawTexture(
      UIScaling.AdjustRectToUIScaling(new Rect(vector.x + thickness, vector.y, vector3.x - (thickness * 2), thickness)),
      lineTexture ?? BaseContent.WhiteTex);
    GUI.DrawTexture(
      UIScaling.AdjustRectToUIScaling(new Rect(vector.x + thickness, vector2.y - thickness, vector3.x - (thickness * 2),
        thickness)), lineTexture ?? BaseContent.WhiteTex);

    GUI.matrix = matrix;
  }

  private static Vector2 RotatePoint(Vector2 point, Vector2 origin, float angle)
  {
    var x = (Mathf.Cos(angle * Mathf.Deg2Rad) * (point.x - origin.x)) -
      (Mathf.Sin(angle * Mathf.Deg2Rad) * (point.y - origin.y)) + origin.x;
    var y = (Mathf.Sin(angle * Mathf.Deg2Rad) * (point.x - origin.x)) +
            (Mathf.Cos(angle * Mathf.Deg2Rad) * (point.y - origin.y)) + origin.y;
    return new Vector2(x, y);
  }

  public static void BeginZoomPanArea(Rect outRect, ref Vector2 scrollPosition, ref float zoom, Rect viewRect,
    float minZoom = 0.25f, float maxZoom = 1f, int panMouseButton = 0, int ignoreDragGroup = -1)
  {
    var contracted = outRect.ContractedBy(5f);
    var controlID = GUIUtility.GetControlID(FocusType.Passive);
    var currentEvent = Event.current;
    var mousePos = currentEvent.mousePosition;
    var isMouseOver = outRect.Contains(mousePos);
    var isOtherDragging = DragAndDropWidget.Dragging ||
                          ignoreDragGroup != -1 &&
                          DragAndDropWidget.DraggableAt(ignoreDragGroup, GUIToContent(mousePos)) is not null ||
                          ReorderableWidget.Dragging ||
                          Widgets.Painting ||
                          sliderDraggingID != 0;

    // マウスホイールによるズーム
    if (isMouseOver && currentEvent.type == EventType.ScrollWheel)
    {
      var zoomDelta = -currentEvent.delta.y * 0.5f;
      var newZoom = Mathf.Clamp(zoom + zoomDelta, minZoom, maxZoom);

      // マウスカーソル直下のコンテンツ座標を維持するようにスクロール位置を補正
      var mouseLocalPos = mousePos - outRect.position;
      var contentPos = (mouseLocalPos / zoom) + scrollPosition;
      scrollPosition = contentPos - (mouseLocalPos / newZoom);
      zoom = newZoom;
      currentEvent.Use();
    }

    // マウスドラッグによるパンニング
    if (!isOtherDragging)
    {
      switch (currentEvent.type)
      {
        case EventType.MouseDown when isMouseOver && currentEvent.button == panMouseButton:
          activePanControlID = controlID;
          GUIUtility.hotControl = controlID;
          break;

        case EventType.MouseDrag when activePanControlID == controlID:
          scrollPosition -= currentEvent.delta / zoom;
          currentEvent.Use();
          break;

        case EventType.MouseUp when activePanControlID == controlID && currentEvent.button == panMouseButton:
          activePanControlID = 0;
          if (GUIUtility.hotControl == controlID)
          {
            GUIUtility.hotControl = 0;
          }

          currentEvent.Use();
          break;
      }
    }
    else if (activePanControlID == controlID)
    {
      activePanControlID = 0;
      if (GUIUtility.hotControl == controlID)
      {
        GUIUtility.hotControl = 0;
      }
    }

    // スクロール位置のクランプ
    if (viewRect is { width: > 0f, height: > 0f })
    {
      scrollPosition.x = Mathf.Clamp(scrollPosition.x, viewRect.xMin - contracted.width, viewRect.xMax);
      scrollPosition.y = Mathf.Clamp(scrollPosition.y, viewRect.yMin - contracted.height, viewRect.yMax);
    }

    zoomPanStack.Push(new ZoomPanState
    {
      scrollPosition = scrollPosition,
      zoom = zoom
    });

    Widgets.BeginGroup(contracted);
    UnityGUIBugsFixer.Notify_BeginGroup();
  }

  public static void EndZoomPanArea()
  {
    if (zoomPanStack.Count > 0)
    {
      zoomPanStack.Pop();
    }

    Widgets.EndGroup();
    UnityGUIBugsFixer.Notify_EndGroup();
  }
}