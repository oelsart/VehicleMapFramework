using RimWorld;
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

        private static readonly Texture2D SliderRailAtlas = ContentFinder<Texture2D>.Get("UI/Buttons/SliderRail", true);

        private static readonly Texture2D SliderHandle = ContentFinder<Texture2D>.Get("UI/Buttons/SliderHandle", true);

        private static readonly Color RangeControlTextColor = new Color(0.6f, 0.6f, 0.6f);

        private static float lastDragSliderSoundTime = -1f;

        private static int sliderDraggingID;
    }
}
