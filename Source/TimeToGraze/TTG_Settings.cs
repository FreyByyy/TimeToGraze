using UnityEngine;
using Verse;

namespace TimeToGraze;

public class TTG_Settings : ModSettings
{
    // Multipliers on the vanilla wander-off chance while out at the pasture.
    public float roamNone = 0.5f;

    public float roamDog = 0.25f;

    public float roamShepherd = 0.15f;

    public float roamBoth = 0.05f;

    // Multipliers on the chance that a predator picks a grazing animal as prey.
    public float predatorNone = 1f;

    public float predatorDog = 0.6f;

    public float predatorShepherd = 0.5f;

    public float predatorBoth = 0.3f;

    public bool shepherdSkillScaling = true;

    public bool guardsRepelPredators = true;

    public bool shepherdThought = true;

    public bool routineMessages = true;

    public float minGrazingHours = 2f;

    public float maxGrazingHours = 16f;

    private Vector2 scroll;

    private float viewHeight = 900f;

    public override void ExposeData()
    {
        base.ExposeData();
        Scribe_Values.Look(ref roamNone, "roamNone", 0.5f);
        Scribe_Values.Look(ref roamDog, "roamDog", 0.25f);
        Scribe_Values.Look(ref roamShepherd, "roamShepherd", 0.15f);
        Scribe_Values.Look(ref roamBoth, "roamBoth", 0.05f);
        Scribe_Values.Look(ref predatorNone, "predatorNone", 1f);
        Scribe_Values.Look(ref predatorDog, "predatorDog", 0.6f);
        Scribe_Values.Look(ref predatorShepherd, "predatorShepherd", 0.5f);
        Scribe_Values.Look(ref predatorBoth, "predatorBoth", 0.3f);
        Scribe_Values.Look(ref shepherdSkillScaling, "shepherdSkillScaling", true);
        Scribe_Values.Look(ref guardsRepelPredators, "guardsRepelPredators", true);
        Scribe_Values.Look(ref shepherdThought, "shepherdThought", true);
        Scribe_Values.Look(ref routineMessages, "routineMessages", true);
        Scribe_Values.Look(ref minGrazingHours, "minGrazingHours", 2f);
        Scribe_Values.Look(ref maxGrazingHours, "maxGrazingHours", 16f);
    }

    public void Reset()
    {
        roamNone = 0.5f;
        roamDog = 0.25f;
        roamShepherd = 0.15f;
        roamBoth = 0.05f;
        predatorNone = 1f;
        predatorDog = 0.6f;
        predatorShepherd = 0.5f;
        predatorBoth = 0.3f;
        shepherdSkillScaling = true;
        guardsRepelPredators = true;
        shepherdThought = true;
        routineMessages = true;
        minGrazingHours = 2f;
        maxGrazingHours = 16f;
    }

    public void DoWindowContents(Rect inRect)
    {
        Rect viewRect = new Rect(0f, 0f, inRect.width - 16f, viewHeight);
        Widgets.BeginScrollView(inRect, ref scroll, viewRect);
        Listing_Standard listing = new Listing_Standard { maxOneColumn = true };
        listing.Begin(viewRect);

        Header(listing, "TTG_Settings_Roam");
        listing.Label("TTG_Settings_RoamDesc".Translate());
        PercentSlider(listing, "TTG_Settings_Unguarded", ref roamNone);
        PercentSlider(listing, "TTG_Settings_Dog", ref roamDog);
        PercentSlider(listing, "TTG_Settings_Shepherd", ref roamShepherd);
        PercentSlider(listing, "TTG_Settings_Both", ref roamBoth);

        Header(listing, "TTG_Settings_Predators");
        listing.Label("TTG_Settings_PredatorsDesc".Translate());
        PercentSlider(listing, "TTG_Settings_Unguarded", ref predatorNone);
        PercentSlider(listing, "TTG_Settings_Dog", ref predatorDog);
        PercentSlider(listing, "TTG_Settings_Shepherd", ref predatorShepherd);
        PercentSlider(listing, "TTG_Settings_Both", ref predatorBoth);

        Header(listing, "TTG_Settings_General");
        listing.CheckboxLabeled("TTG_Settings_SkillScaling".Translate(), ref shepherdSkillScaling, "TTG_Settings_SkillScalingDesc".Translate());
        listing.CheckboxLabeled("TTG_Settings_Repel".Translate(), ref guardsRepelPredators, "TTG_Settings_RepelDesc".Translate());
        listing.CheckboxLabeled("TTG_Settings_Thought".Translate(), ref shepherdThought);
        listing.CheckboxLabeled("TTG_Settings_Messages".Translate(), ref routineMessages);
        listing.Label("TTG_Settings_MinHours".Translate(minGrazingHours.ToString("0.#")));
        minGrazingHours = Mathf.Round(listing.Slider(minGrazingHours, 0f, 8f) * 2f) / 2f;
        listing.Label("TTG_Settings_MaxHours".Translate(maxGrazingHours.ToString("0")));
        maxGrazingHours = Mathf.Round(listing.Slider(maxGrazingHours, 4f, 48f));

        listing.Gap();
        if (listing.ButtonText("TTG_Settings_Reset".Translate()))
        {
            Reset();
        }

        viewHeight = listing.CurHeight + 12f;
        listing.End();
        Widgets.EndScrollView();
    }

    private static void Header(Listing_Standard listing, string key)
    {
        listing.GapLine();
        Text.Font = GameFont.Medium;
        listing.Label(key.Translate());
        Text.Font = GameFont.Small;
    }

    private static void PercentSlider(Listing_Standard listing, string key, ref float value)
    {
        listing.Label(key.Translate() + ": ×" + value.ToString("0.00"));
        value = Mathf.Round(listing.Slider(value, 0f, 1f) * 100f) / 100f;
    }
}
