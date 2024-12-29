using LudeonTK;
using RimWorld;
using SmashTools;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace VehicleInteriors
{
    [StaticConstructorOnStartup]
    public static class VIF_Widgets
    {
        public static float HorizontalSlider(Rect rect, float value, float min, float max, bool middleAlignment = false, string label = null, string leftAlignedLabel = null, string rightAlignedLabel = null, float roundTo = -1f, Color colorFactor = default)
        {
            var color = GUI.color;
            float num = value;
            if (middleAlignment || !label.NullOrEmpty())
            {
                rect.y += Mathf.Round((rect.height - 10f) / 2f);
            }
            if (!label.NullOrEmpty())
            {
                rect.y += 5f;
            }
            int num2 = UI.GUIToScreenPoint(new Vector2(rect.x, rect.y)).GetHashCode();
            num2 = Gen.HashCombine<float>(num2, rect.width);
            num2 = Gen.HashCombine<float>(num2, rect.height);
            num2 = Gen.HashCombine<float>(num2, min);
            num2 = Gen.HashCombine<float>(num2, max);
            Rect rect2 = rect;
            rect2.xMin += 6f;
            rect2.xMax -= 6f;
            GUI.color = VIF_Widgets.RangeControlTextColor * colorFactor;
            Rect rect3 = new Rect(rect2.x, rect2.y + 2f, rect2.width, 8f);
            Widgets.DrawAtlas(rect3, VIF_Widgets.SliderRailAtlas);
            GUI.color = colorFactor;
            float x = Mathf.Clamp(rect2.x - 6f + rect2.width * Mathf.InverseLerp(min, max, num), rect2.xMin - 6f, rect2.xMax - 6f);
            GUI.DrawTexture(new Rect(x, rect3.center.y - 6f, 12f, 12f), VIF_Widgets.SliderHandle);
            if (Event.current.type == EventType.MouseDown && Mouse.IsOver(rect) && VIF_Widgets.sliderDraggingID != num2)
            {
                VIF_Widgets.sliderDraggingID = num2;
                SoundDefOf.DragSlider.PlayOneShotOnCamera(null);
                Event.current.Use();
            }
            if (VIF_Widgets.sliderDraggingID == num2 && UnityGUIBugsFixer.MouseDrag(0))
            {
                num = Mathf.Clamp((Event.current.mousePosition.x - rect2.x) / rect2.width * (max - min) + min, min, max);
                if (Event.current.type == EventType.MouseDrag)
                {
                    Event.current.Use();
                }
            }
            if (!label.NullOrEmpty() || !leftAlignedLabel.NullOrEmpty() || !rightAlignedLabel.NullOrEmpty())
            {
                TextAnchor anchor = Text.Anchor;
                GameFont font = Text.Font;
                Text.Font = GameFont.Small;
                float num3 = label.NullOrEmpty() ? 18f : Text.CalcSize(label).y;
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
                num = (float)Mathf.RoundToInt(num / roundTo) * roundTo;
            }
            if (value != num)
            {
                if (Time.realtimeSinceStartup > VIF_Widgets.lastDragSliderSoundTime + 0.075f)
                {
                    SoundDefOf.DragSlider.PlayOneShotOnCamera(null);
                    VIF_Widgets.lastDragSliderSoundTime = Time.realtimeSinceStartup;
                }
            }
            GUI.color = color;
            return num;
        }

        public static void DrawBoxRotated(Rect rect, int thickness = 1, Texture2D lineTexture = null, float rotation = 0f)
        {
            Vector2 RotatePoint(Vector2 point, Vector2 origin, float angle)
            {
                float x = Mathf.Cos(angle * angleToRad) * (point.x - origin.x) - Mathf.Sin(angle * angleToRad) * (point.y - origin.y) + origin.x;
                float y = Mathf.Sin(angle * angleToRad) * (point.x - origin.x) + Mathf.Cos(angle * angleToRad) * (point.y - origin.y) + origin.y;
                return new Vector2(x, y);
            }
            Vector2 vector = RotatePoint(new Vector2(rect.x, rect.y), rect.center, rotation);
            Vector2 vector2 = RotatePoint(new Vector2(rect.xMax, rect.yMax), rect.center, rotation);
            if (vector.x > vector2.x)
            {
                ref float ptr = ref vector.x;
                float num = vector2.x;
                float num2 = vector.x;
                ptr = num;
                vector2.x = num2;
            }
            if (vector.y > vector2.y)
            {
                ref float ptr = ref vector.y;
                float num2 = vector2.y;
                float num = vector.y;
                ptr = num2;
                vector2.y = num;
            }
            Vector3 vector3 = vector2 - vector;
			Matrix4x4 matrix = GUI.matrix;
            UI.RotateAroundPivot(-rotation, rect.center);
            GUI.DrawTexture(UIScaling.AdjustRectToUIScaling(new Rect(vector.x, vector.y, (float)thickness, vector3.y)), lineTexture ?? BaseContent.WhiteTex);
            GUI.DrawTexture(UIScaling.AdjustRectToUIScaling(new Rect(vector2.x - (float)thickness, vector.y, (float)thickness, vector3.y)), lineTexture ?? BaseContent.WhiteTex);
            GUI.DrawTexture(UIScaling.AdjustRectToUIScaling(new Rect(vector.x + (float)thickness, vector.y, vector3.x - (float)(thickness * 2), (float)thickness)), lineTexture ?? BaseContent.WhiteTex);
            GUI.DrawTexture(UIScaling.AdjustRectToUIScaling(new Rect(vector.x + (float)thickness, vector2.y - (float)thickness, vector3.x - (float)(thickness * 2), (float)thickness)), lineTexture ?? BaseContent.WhiteTex);

            GUI.matrix = matrix;
        }

        private static readonly Texture2D SliderRailAtlas = ContentFinder<Texture2D>.Get("UI/Buttons/SliderRail", true);

        private static readonly Texture2D SliderHandle = ContentFinder<Texture2D>.Get("UI/Buttons/SliderHandle", true);

        private static readonly Color RangeControlTextColor = new Color(0.6f, 0.6f, 0.6f);

        private const float angleToRad = 0.017453292519943f;

        private static float lastDragSliderSoundTime = -1f;

        private static int sliderDraggingID;
    }
}
