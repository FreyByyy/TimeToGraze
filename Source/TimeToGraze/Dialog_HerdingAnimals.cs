using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace TimeToGraze;

// Mod settings: pick which animals may learn Herding.
public class Dialog_HerdingAnimals : Window
{
    private const float RowHeight = 32f;

    private string search = "";

    private Vector2 scroll;

    public override Vector2 InitialSize => new Vector2(560f, 680f);

    public Dialog_HerdingAnimals()
    {
        doCloseX = true;
        doCloseButton = true;
        closeOnClickedOutside = false;
        absorbInputAroundWindow = true;
        forcePause = true;
    }

    public override void DoWindowContents(Rect inRect)
    {
        float y = inRect.y;
        Text.Font = GameFont.Medium;
        Widgets.Label(new Rect(inRect.x, y, inRect.width, 36f), "TTG_HerdingDialog_Title".Translate());
        y += 38f;
        Text.Font = GameFont.Small;
        string desc = "TTG_HerdingDialog_Desc".Translate();
        float descHeight = Text.CalcHeight(desc, inRect.width);
        GUI.color = Color.gray;
        Widgets.Label(new Rect(inRect.x, y, inRect.width, descHeight), desc);
        GUI.color = Color.white;
        y += descHeight + 8f;

        Rect searchRect = new Rect(inRect.x, y, inRect.width - 170f, 30f);
        search = Widgets.TextField(searchRect, search);
        if (search.NullOrEmpty())
        {
            GUI.color = new Color(1f, 1f, 1f, 0.4f);
            Widgets.Label(new Rect(searchRect.x + 6f, searchRect.y + 5f, searchRect.width, 24f), "TTG_HerdingDialog_Search".Translate());
            GUI.color = Color.white;
        }
        if (Widgets.ButtonText(new Rect(inRect.xMax - 160f, y, 160f, 30f), "TTG_HerdingDialog_Reset".Translate()))
        {
            HerdingRaces.ResetToDefaults();
        }
        y += 38f;

        string filter = search.Trim().ToLowerInvariant();
        List<ThingDef> list = HerdingRaces.Candidates
            .Where(d => filter.Length == 0 || d.label.ToLowerInvariant().Contains(filter) || d.defName.ToLowerInvariant().Contains(filter) || (d.modContentPack?.Name ?? "").ToLowerInvariant().Contains(filter))
            .ToList();

        Rect outRect = new Rect(inRect.x, y, inRect.width, inRect.yMax - y - 50f);
        Rect viewRect = new Rect(0f, 0f, outRect.width - 16f, Mathf.Max(list.Count * RowHeight, outRect.height));
        Widgets.BeginScrollView(outRect, ref scroll, viewRect);
        for (int i = 0; i < list.Count; i++)
        {
            DrawRow(new Rect(0f, i * RowHeight, viewRect.width, RowHeight), list[i], i);
        }
        if (list.Count == 0)
        {
            Widgets.Label(new Rect(0f, 0f, viewRect.width, 30f), "TTG_HerdingDialog_None".Translate());
        }
        Widgets.EndScrollView();
    }

    private static void DrawRow(Rect row, ThingDef def, int index)
    {
        if (index % 2 == 1)
        {
            Widgets.DrawLightHighlight(row);
        }
        Widgets.DrawHighlightIfMouseover(row);
        Widgets.ThingIcon(new Rect(row.x + 4f, row.y + 2f, 28f, 28f), def);
        string label = def.LabelCap;
        if (HerdingRaces.IsDefault(def))
        {
            label += " (" + "TTG_HerdingDialog_Default".Translate() + ")";
        }
        Text.Anchor = TextAnchor.MiddleLeft;
        Widgets.Label(new Rect(row.x + 40f, row.y, row.width * 0.55f, row.height), label);
        GUI.color = Color.gray;
        Text.Font = GameFont.Tiny;
        Widgets.Label(new Rect(row.x + 40f + row.width * 0.55f, row.y, row.width * 0.45f - 76f, row.height), def.modContentPack?.Name ?? "");
        Text.Font = GameFont.Small;
        GUI.color = Color.white;
        Text.Anchor = TextAnchor.UpperLeft;
        bool allowed = HerdingRaces.Allowed(def);
        bool before = allowed;
        Widgets.Checkbox(new Vector2(row.xMax - 30f, row.y + 4f), ref allowed);
        if (allowed != before)
        {
            HerdingRaces.SetAllowed(def, allowed);
        }
        TooltipHandler.TipRegion(row, def.defName);
    }

    public override void PostClose()
    {
        base.PostClose();
        LoadedModManager.GetMod<TimeToGrazeMod>().WriteSettings();
    }
}
