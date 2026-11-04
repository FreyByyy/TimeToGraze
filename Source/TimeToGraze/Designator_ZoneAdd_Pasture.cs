using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace TimeToGraze;

public class Designator_ZoneAdd_Pasture : Designator_ZoneAdd
{
    public const string IconPath = "UI/Designators/TTG_ZoneCreate_Pasture";

    protected override string NewZoneLabel => "TTG_PastureZone".Translate();

    protected virtual bool ShowRightClickHideOptions => true;

    public override IEnumerable<FloatMenuOption> RightClickFloatMenuOptions
    {
        get
        {
            if (!ShowRightClickHideOptions)
            {
                yield break;
            }
            foreach (FloatMenuOption option in Command_Hide_ZonePasture.GetHideOptions())
            {
                yield return option;
            }
        }
    }

    public Designator_ZoneAdd_Pasture()
    {
        zoneTypeToPlace = typeof(Zone_Pasture);
        defaultLabel = "TTG_PastureZone".Translate();
        defaultDesc = "TTG_PastureZoneDesc".Translate();
        icon = ContentFinder<Texture2D>.Get(IconPath);
        soundSucceeded = SoundDefOf.Designate_ZoneAdd_Growing;
    }

    protected override Zone MakeNewZone()
    {
        return new Zone_Pasture(Find.CurrentMap.zoneManager);
    }
}

public class Designator_ZoneAdd_Pasture_Expand : Designator_ZoneAdd_Pasture
{
    protected override bool ShowRightClickHideOptions => false;

    public Designator_ZoneAdd_Pasture_Expand()
    {
        defaultLabel = "DesignatorZoneExpand".Translate();
        hotKey = KeyBindingDefOf.Misc6;
    }
}
