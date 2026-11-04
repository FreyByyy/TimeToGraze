using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace TimeToGraze;

public class ITab_PenGrazing : ITab_PenBase
{
    private const float RowHeight = 26f;

    private Vector2 scroll;

    private float viewHeight = 1000f;

    public ITab_PenGrazing()
    {
        size = new Vector2(470f, 620f);
        labelKey = "TTG_TabGrazing";
    }

    protected override void FillTab()
    {
        CompGrazing comp = SelThing?.TryGetComp<CompGrazing>();
        if (comp == null)
        {
            return;
        }
        Rect outRect = new Rect(0f, 0f, size.x, size.y).ContractedBy(10f);
        outRect.yMin += 14f;
        Rect viewRect = new Rect(0f, 0f, outRect.width - 16f, viewHeight);
        scroll.y = Mathf.Min(scroll.y, Mathf.Max(0f, viewHeight - outRect.height));
        Widgets.BeginScrollView(outRect, ref scroll, viewRect);
        // One column: otherwise content taller than last frame's height wraps into an off-screen column
        // and the tab collapses to empty (it grows while a herd is out because of the status lines).
        Listing_Standard ls = new Listing_Standard { maxOneColumn = true };
        ls.Begin(viewRect);

        ls.CheckboxLabeled("TTG_Enable".Translate(), ref comp.enabled, "TTG_EnableDesc".Translate());
        DrawStatus(ls, comp);

        Section(ls, "TTG_Section_Pastures");
        DrawPastures(ls, comp);

        Section(ls, "TTG_Section_Start");
        ls.CheckboxLabeled("TTG_TriggerHunger".Translate(), ref comp.triggerHunger, "TTG_TriggerHungerDesc".Translate());
        if (comp.triggerHunger)
        {
            comp.hungerThreshold = PercentSlider(ls, "TTG_HungerThreshold", comp.hungerThreshold, 0.05f, 0.8f);
        }
        ls.CheckboxLabeled("TTG_TriggerPenFood".Translate(), ref comp.triggerPenFood, "TTG_TriggerPenFoodDesc".Translate());
        if (comp.triggerPenFood)
        {
            float days = comp.PenFoodDays();
            string current = float.IsPositiveInfinity(days) ? "∞" : days.ToString("0.#");
            ls.Label("TTG_PenFoodDays".Translate(comp.penFoodDays.ToString("0.#"), current));
            comp.penFoodDays = Mathf.Round(ls.Slider(comp.penFoodDays, 0.5f, 10f) * 2f) / 2f;
        }
        ls.CheckboxLabeled("TTG_UseSchedule".Translate(), ref comp.useSchedule, "TTG_UseScheduleDesc".Translate());
        if (comp.useSchedule)
        {
            ls.Label("TTG_Hours".Translate(comp.hours.min, comp.hours.max));
            Rect rangeRect = ls.GetRect(32f);
            Widgets.IntRange(rangeRect, comp.parent.thingIDNumber ^ 0x7769, ref comp.hours, 0, 24);
        }
        if (!comp.triggerHunger && !comp.triggerPenFood)
        {
            GUI.color = Color.gray;
            ls.Label("TTG_ScheduleOnlyNote".Translate());
            GUI.color = Color.white;
        }

        Section(ls, "TTG_Section_Return");
        ls.CheckboxLabeled("TTG_ReturnWhenFed".Translate(), ref comp.returnWhenFed);
        if (comp.returnWhenFed)
        {
            comp.fedThreshold = PercentSlider(ls, "TTG_FedThreshold", comp.fedThreshold, 0.5f, 1f);
        }
        if (comp.useSchedule)
        {
            GUI.color = Color.gray;
            ls.Label("TTG_ReturnAtWindowEnd".Translate());
            GUI.color = Color.white;
        }
        else
        {
            ls.CheckboxLabeled("TTG_ReturnAtNight".Translate(), ref comp.returnAtNight);
        }
        ls.CheckboxLabeled("TTG_ReturnOnDanger".Translate(), ref comp.returnOnDanger, "TTG_ReturnOnDangerDesc".Translate());

        Section(ls, "TTG_Section_Herd");
        ls.CheckboxLabeled("TTG_ExcludeSick".Translate(), ref comp.excludeSick);
        ls.CheckboxLabeled("TTG_ExcludePregnant".Translate(), ref comp.excludePregnant);
        ls.CheckboxLabeled("TTG_ExcludeYoung".Translate(), ref comp.excludeYoung);
        if (ls.ButtonText("TTG_ExcludedAnimals".Translate(comp.excludedAnimals.Count)))
        {
            OpenExclusionMenu(comp);
        }

        Section(ls, "TTG_Section_Guards");
        DrawGuards(ls, comp);

        viewHeight = ls.CurHeight + 12f;
        ls.End();
        Widgets.EndScrollView();
    }

    private static void Section(Listing_Standard ls, string key)
    {
        ls.GapLine(8f);
        Text.Font = GameFont.Medium;
        ls.Label(key.Translate());
        Text.Font = GameFont.Small;
    }

    private static float PercentSlider(Listing_Standard ls, string key, float value, float min, float max)
    {
        ls.Label(key.Translate(value.ToStringPercent()));
        return Mathf.Round(ls.Slider(value, min, max) * 20f) / 20f;
    }

    private static void DrawStatus(Listing_Standard ls, CompGrazing comp)
    {
        ls.Gap(4f);
        ls.Label(comp.StatusText());
        if (comp.SessionActive)
        {
            ls.Label("TTG_Status_Guards".Translate(GuardLabel(comp.shepherd, comp.ShepherdPresent), GuardLabel(comp.dog, comp.DogPresent)));
            ls.Label("TTG_Status_Odds".Translate(comp.RoamMultiplier.ToString("0.00"), comp.PredatorMultiplier.ToString("0.00")));
        }
        Rect row = ls.GetRect(30f);
        Rect left = row.LeftHalf().ContractedBy(2f, 0f);
        Rect right = row.RightHalf().ContractedBy(2f, 0f);
        if (Button(left, "TTG_StartNow".Translate(), !comp.SessionActive && comp.pastures.Count > 0))
        {
            comp.ManualStart();
        }
        if (Button(right, "TTG_ReturnNow".Translate(), comp.SessionActive && !comp.Returning))
        {
            comp.ManualReturn();
        }
    }

    private static bool Button(Rect rect, string label, bool active)
    {
        if (!active)
        {
            GUI.color = Color.gray;
        }
        bool clicked = Widgets.ButtonText(rect, label, active: active);
        GUI.color = Color.white;
        return clicked && active;
    }

    private static string GuardLabel(Pawn guard, bool present)
    {
        if (guard == null)
        {
            return "TTG_None".Translate();
        }
        return guard.LabelShort + " (" + (present ? "TTG_OnDuty".Translate() : "TTG_OffDuty".Translate()) + ")";
    }

    private static void DrawPastures(Listing_Standard ls, CompGrazing comp)
    {
        Map map = comp.parent.Map;
        // Whole herd, including animals currently out at the pasture.
        IEnumerable<Pawn> herd = comp.AnimalsInPen().Concat(comp.Entries.Select(e => e.animal)).Distinct();
        float perDay = GrazingUtility.NutritionPerDay(herd);
        if (comp.pastures.Count == 0)
        {
            GUI.color = Color.gray;
            ls.Label("TTG_NoPastureLinkedHint".Translate());
            GUI.color = Color.white;
        }
        Zone_Pasture toRemove = null;
        foreach (Zone_Pasture pasture in comp.pastures)
        {
            Rect row = ls.GetRect(RowHeight);
            Widgets.DrawHighlightIfMouseover(row);
            float grass = pasture.GrassNutrition;
            string days = perDay > 0.001f ? (grass / perDay).ToString("0.#") : "∞";
            string label = pasture.label + "  —  " + "TTG_PastureRow".Translate(PenFoodCalculator.NutritionToString(grass, withUnits: false), days, pasture.CellCount);
            if (pasture == comp.ActivePasture)
            {
                label = "▶ " + label;
            }
            Rect labelRect = new Rect(row.x, row.y, row.width - 28f, row.height);
            Text.Anchor = TextAnchor.MiddleLeft;
            Widgets.Label(labelRect, label);
            Text.Anchor = TextAnchor.UpperLeft;
            if (Widgets.ButtonImage(new Rect(row.xMax - 24f, row.y + 1f, 24f, 24f), TexButton.Delete))
            {
                toRemove = pasture;
            }
            TooltipHandler.TipRegion(row, "TTG_PastureRowTip".Translate());
        }
        if (toRemove != null)
        {
            comp.pastures.Remove(toRemove);
        }
        if (ls.ButtonText("TTG_LinkPasture".Translate()))
        {
            List<FloatMenuOption> options = new List<FloatMenuOption>();
            foreach (Zone_Pasture pasture in map.zoneManager.AllZones.OfType<Zone_Pasture>())
            {
                if (comp.pastures.Contains(pasture))
                {
                    continue;
                }
                Zone_Pasture captured = pasture;
                options.Add(new FloatMenuOption(pasture.label, () => comp.pastures.Add(captured)));
            }
            if (options.Count == 0)
            {
                options.Add(new FloatMenuOption("TTG_NoPasturesOnMap".Translate(), null));
            }
            Find.WindowStack.Add(new FloatMenu(options));
        }
    }

    private static void DrawGuards(Listing_Standard ls, CompGrazing comp)
    {
        Map map = comp.parent.Map;
        Rect shepherdRow = ls.GetRect(30f);
        Widgets.Label(shepherdRow.LeftPart(0.4f), "TTG_Shepherd".Translate());
        if (Widgets.ButtonText(shepherdRow.RightPart(0.6f), comp.shepherd?.LabelShort ?? "TTG_None".Translate()))
        {
            List<FloatMenuOption> options = new List<FloatMenuOption> { new FloatMenuOption("TTG_None".Translate(), () => comp.shepherd = null) };
            foreach (Pawn colonist in map.mapPawns.FreeColonistsSpawned.OrderByDescending(p => p.skills?.GetSkill(SkillDefOf.Animals).Level ?? 0))
            {
                Pawn captured = colonist;
                if (colonist.WorkTypeIsDisabled(WorkTypeDefOf.Handling))
                {
                    options.Add(new FloatMenuOption(colonist.LabelShort + " (" + "TTG_IncapableOfHandling".Translate() + ")", null));
                    continue;
                }
                int level = colonist.skills.GetSkill(SkillDefOf.Animals).Level;
                options.Add(new FloatMenuOption("TTG_ShepherdOption".Translate(colonist.LabelShort, level), () => comp.shepherd = captured));
            }
            Find.WindowStack.Add(new FloatMenu(options));
        }
        TooltipHandler.TipRegion(shepherdRow, "TTG_ShepherdDesc".Translate());

        Rect dogRow = ls.GetRect(30f);
        Widgets.Label(dogRow.LeftPart(0.4f), "TTG_Dog".Translate());
        if (Widgets.ButtonText(dogRow.RightPart(0.6f), comp.dog?.LabelShort ?? "TTG_None".Translate()))
        {
            List<FloatMenuOption> options = new List<FloatMenuOption> { new FloatMenuOption("TTG_None".Translate(), () => comp.dog = null) };
            foreach (Pawn animal in map.mapPawns.SpawnedColonyAnimals)
            {
                if (animal.training == null || !animal.training.HasLearned(TTG_DefOf.TTG_Herding))
                {
                    continue;
                }
                Pawn captured = animal;
                options.Add(new FloatMenuOption(animal.LabelShort + " (" + animal.KindLabel + ")", () => comp.dog = captured));
            }
            if (options.Count == 1)
            {
                options.Add(new FloatMenuOption("TTG_NoHerdingDogs".Translate(), null));
            }
            Find.WindowStack.Add(new FloatMenu(options));
        }
        TooltipHandler.TipRegion(dogRow, "TTG_DogDesc".Translate());

        GUI.color = Color.gray;
        ls.Label("TTG_GuardsHint".Translate());
        GUI.color = Color.white;
    }

    private static void OpenExclusionMenu(CompGrazing comp)
    {
        List<FloatMenuOption> options = new List<FloatMenuOption>();
        IEnumerable<Pawn> animals = comp.AnimalsInPen().Concat(comp.excludedAnimals).Distinct().OrderBy(p => p.LabelShort);
        foreach (Pawn animal in animals)
        {
            Pawn captured = animal;
            bool excluded = comp.excludedAnimals.Contains(animal);
            string label = (excluded ? "✗ " : "✓ ") + animal.LabelShort + " (" + animal.KindLabel + ")";
            options.Add(new FloatMenuOption(label, () =>
            {
                if (!comp.excludedAnimals.Remove(captured))
                {
                    comp.excludedAnimals.Add(captured);
                }
            }));
        }
        if (options.Count == 0)
        {
            options.Add(new FloatMenuOption("TTG_NoAnimalsInPen".Translate(), null));
        }
        Find.WindowStack.Add(new FloatMenu(options));
    }
}
