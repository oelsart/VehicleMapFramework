using System;
using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using static VehicleMapFramework.VehicleFormationManager;

namespace VehicleMapFramework;

public sealed class Dialog_FormationPresetList : Window
{
  private readonly List<FormationPreset> formationPresets;
  private readonly Action<int> interactAction;
  private readonly Action<string> newSaveAction;
  private readonly string interactButLabel;
  private readonly QuickSearchWidget search = new();
  private string typingName = "";

  private bool focusedSearch;
  private bool focusedNameArea;
  private Vector2 scrollPosition;

  private const float EntryHeight = 40f;
  private const float NameLeftMargin = 8f;
  private const float NameRightMargin = 4f;
  private const float InteractButWidth = 100f;
  private const float InteractButHeight = 36f;
  private const float ButtonHeight = 36f;
  private const float BottomAreaHeight = 55f;
  private const float NameTextFieldWidth = 400f;
  private const float NameTextFieldHeight = 35f;
  private const float NameTextFieldButtonSpace = 20f;
  
  public override Vector2 InitialSize => new(620f, 700f);

  private static bool FocusSearchField => false;

  public Dialog_FormationPresetList(
    List<FormationPreset> formationPresets, string interactButLabel,
    Action<int> interactAction, Action<string> newSaveAction = null)
  {
    doCloseButton = true;
    doCloseX = true;
    forcePause = true;
    absorbInputAroundWindow = true;
    closeOnAccept = false;
    this.formationPresets = formationPresets;
    this.interactAction = interactAction;
    this.interactButLabel = interactButLabel;
    this.newSaveAction = newSaveAction;
  }

  public override void DoWindowContents(Rect inRect)
  {
    var vector = new Vector2(inRect.width - 16f, EntryHeight);
    var y = vector.y;
    var totalHeight = FilesMatchingFilter() * y;
    var rect = new Rect(0f, 0f, inRect.width - 16f, totalHeight);
    var rect2 = inRect.LeftHalf();
    rect2.height = Text.LineHeight;
    search.OnGUI(rect2);
    if (!focusedSearch && FocusSearchField)
    {
      focusedSearch = true;
      search.Focus();
    }

    var rect3 = inRect;
    rect3.yMin = rect2.yMax + 10f;
    rect3.yMax -= CloseButSize.y + BottomAreaHeight + 10f;
    var shouldDoTypeInField = newSaveAction is not null;
    if (shouldDoTypeInField)
    {
      rect3.yMax -= 53f;
    }

    Widgets.BeginScrollView(rect3, ref this.scrollPosition, rect);
    var curY = 0f;
    for (var i = 0; i < formationPresets.Count; i++)
    {
      var preset = formationPresets[i];
      if (search.filter.Matches(preset.InspectLabel))
      {
        if (curY + vector.y >= scrollPosition.y && curY <= scrollPosition.y + rect3.height)
        {
          var rect4 = new Rect(0f, curY, vector.x, vector.y);
          if (i % 2 == 1)
          {
            Widgets.DrawAltRect(rect4);
          }

          Widgets.BeginGroup(rect4);
          var rect5 = new Rect(
            rect4.width - InteractButHeight, (rect4.height - InteractButHeight) / 2f,
            InteractButHeight, InteractButHeight);
          if (Widgets.ButtonImage(rect5, TexButton.Delete, Color.white, GenUI.SubtleMouseoverColor))
          {
            var num = i;
            Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation("ConfirmDelete".Translate(preset.RenamableLabel),
              () =>
              {
                formationPresets.RemoveAt(num);
              }, true));
          }
          var rect6 = new Rect(
            rect5.x - InteractButHeight, (rect4.height - InteractButHeight) / 2f,
            InteractButHeight, InteractButHeight);
          if (Widgets.ButtonImage(rect6, TexButton.Rename, Color.white, GenUI.SubtleMouseoverColor))
          {
            Find.WindowStack.Add(new FormationPreset.Dialog_RenameFormationPreset(formationPresets[i]));
          }

          Text.Font = GameFont.Small;
          var rect7 = new Rect(rect6.x - InteractButWidth, (rect4.height - ButtonHeight) / 2f, InteractButWidth, ButtonHeight);
          if (Widgets.ButtonText(rect7, interactButLabel))
          {
            interactAction(i);
          }
          
          GUI.color = Color.white;
          Text.Anchor = TextAnchor.UpperLeft;
          GUI.color = new Color(1f, 1f, 0.6f);
          var rect8 = new Rect(NameLeftMargin, 0f, rect7.x - NameLeftMargin - NameRightMargin, rect4.height);
          Text.Anchor = TextAnchor.MiddleLeft;
          Text.Font = GameFont.Small;
          Widgets.Label(rect8, preset.InspectLabel.Truncate(rect8.width * 1.8f));
          GUI.color = Color.white;
          Text.Anchor = TextAnchor.UpperLeft;
          Widgets.EndGroup();
        }

        curY += vector.y;
      }
    }

    Widgets.EndScrollView();
    if (shouldDoTypeInField)
    {
      DoTypeInField(inRect.TopPartPixels(inRect.height - CloseButSize.y - 18f));
    }
  }
  
  private void DoTypeInField(Rect rect)
  {
    Widgets.BeginGroup(rect);
    var flag = Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Return;
    var num = rect.height - 35f;
    Text.Font = GameFont.Small;
    Text.Anchor = TextAnchor.MiddleLeft;
    GUI.SetNextControlName("FormationNameField");
    var text = Widgets.TextField(new Rect(5f, num, NameTextFieldWidth, NameTextFieldHeight), this.typingName);
    if (GenText.IsValidFilename(text))
    {
      typingName = text;
    }
    if (!focusedNameArea)
    {
      UI.FocusControl("FormationNameField", this);
      focusedNameArea = true;
    }
    if (Widgets.ButtonText(new Rect(420f, num, rect.width - 400f - 20f, 35f), "Save".Translate()) || flag)
    {
      if (typingName.NullOrEmpty())
      {
        Messages.Message("NeedAName".Translate(), MessageTypeDefOf.RejectInput, false);
      }
      else
      {
        var text2 = typingName;
        newSaveAction(text2?.Trim());
      }
    }
    Text.Anchor = TextAnchor.UpperLeft;
    Widgets.EndGroup();
  }

  private int FilesMatchingFilter()
  {
    return formationPresets.Count(p => search.filter.Matches(p.InspectLabel));
  }
}