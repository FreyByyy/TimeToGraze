using System.Collections.Generic;
using System.Linq;
using System.Text;
using RimWorld;
using UnityEngine;
using Verse;

namespace TimeToGraze;

public class Zone_Pasture : Zone
{
    private static readonly Color[] ZoneColors =
    {
        new Color(0.75f, 0.85f, 0.35f, 0.09f),
        new Color(0.55f, 0.80f, 0.45f, 0.09f),
        new Color(0.85f, 0.75f, 0.40f, 0.09f),
        new Color(0.60f, 0.70f, 0.30f, 0.09f)
    };

    private static int nextColorIndex;

    private Area_Pasture grazingArea;

    private float cachedNutrition;

    private int cachedNutritionTick = -99999;

    protected override Color NextZoneColor => ZoneColors[nextColorIndex++ % ZoneColors.Length];

    // Only the main thread builds the area; other readers (pathing) get whatever is already built.
    public Area GrazingAreaIfBuilt => grazingArea;

    public float GrassNutrition
    {
        get
        {
            int ticks = Find.TickManager.TicksGame;
            if (ticks - cachedNutritionTick > 250)
            {
                cachedNutrition = GrazingUtility.GrassNutritionIn(cells, Map);
                cachedNutritionTick = ticks;
            }
            return cachedNutrition;
        }
    }

    public Zone_Pasture()
    {
    }

    public Zone_Pasture(ZoneManager zoneManager) : base("TTG_PastureZone".Translate(), zoneManager)
    {
    }

    // The pathfinder only knows areas listed in the AreaManager, so the area is registered there while
    // a herd uses it (it is non-assignable and non-mutable, hence hidden from area UIs, and kept out of saves).
    public Area EnsureGrazingArea()
    {
        if (grazingArea == null || grazingArea.Map != Map)
        {
            ReleaseGrazingArea();
            grazingArea = new Area_Pasture(Map.areaManager, this);
            for (int i = 0; i < cells.Count; i++)
            {
                grazingArea.SetCell(cells[i], true);
            }
        }
        List<Area> areas = Map.areaManager.AllAreas;
        if (!areas.Contains(grazingArea))
        {
            areas.Add(grazingArea);
        }
        return grazingArea;
    }

    public void ReleaseGrazingArea()
    {
        if (grazingArea == null)
        {
            return;
        }
        grazingArea.areaManager.AllAreas.Remove(grazingArea);
        grazingArea = null;
    }

    public override void PostDeregister()
    {
        base.PostDeregister();
        ReleaseGrazingArea();
    }

    public override void AddCell(IntVec3 c)
    {
        base.AddCell(c);
        if (cells.Contains(c))
        {
            grazingArea?.SetCell(c, true);
        }
    }

    public override void RemoveCell(IntVec3 c)
    {
        base.RemoveCell(c);
        grazingArea?.SetCell(c, false);
    }

    public override string GetInspectString()
    {
        StringBuilder sb = new StringBuilder(base.GetInspectString());
        sb.AppendLine();
        sb.Append("TTG_PastureGrass".Translate(PenFoodCalculator.NutritionToString(GrassNutrition, withUnits: false)));
        List<string> pens = GrazingUtility.CompsOn(Map).Where(c => c.pastures.Contains(this)).Select(c => c.PenLabel).ToList();
        sb.AppendLine();
        sb.Append(pens.Any() ? "TTG_PastureLinkedPens".Translate(string.Join(", ", pens)) : "TTG_PastureNotLinked".Translate());
        return sb.ToString();
    }

    public override IEnumerable<Gizmo> GetGizmos()
    {
        yield return new Command_Hide_ZonePasture(this);
        foreach (Gizmo gizmo in base.GetGizmos())
        {
            yield return gizmo;
        }
    }

    public override IEnumerable<Gizmo> GetZoneAddGizmos()
    {
        yield return DesignatorUtility.FindAllowedDesignator<Designator_ZoneAdd_Pasture_Expand>();
    }
}

[StaticConstructorOnStartup]
public class Command_Hide_ZonePasture : Command_Hide
{
    private static Texture2D cachedIcon;

    public Command_Hide_ZonePasture(IHideable hideable) : base(hideable)
    {
    }

    protected override IEnumerable<FloatMenuOption> GetOptions()
    {
        return GetHideOptions();
    }

    public static IEnumerable<FloatMenuOption> GetHideOptions()
    {
        yield return new FloatMenuOption("ShowAllZones".Translate(), () => ToggleAll(hidden: false));
        yield return new FloatMenuOption("HideAllZones".Translate(), () => ToggleAll(hidden: true));
        cachedIcon ??= ContentFinder<Texture2D>.Get(Designator_ZoneAdd_Pasture.IconPath);
        foreach (FloatMenuOption option in ZoneTypeOptions<Zone_Pasture>("TTG_PastureGroup".Translate(), cachedIcon))
        {
            yield return option;
        }
    }
}

// Hidden allowed-area mirror of a pasture zone; never registered in the AreaManager, rebuilt on demand.
public class Area_Pasture : Area
{
    private readonly Zone_Pasture zone;

    public override string Label => zone.label;

    public override Color Color => zone.color;

    public override int ListPriority => 0;

    public Area_Pasture(AreaManager areaManager, Zone_Pasture zone) : base(areaManager)
    {
        this.zone = zone;
    }

    public void SetCell(IntVec3 c, bool value)
    {
        if (areaManager.AllAreas.Contains(this))
        {
            // Goes through Area.Set so the pathfinder and regions get the delta.
            this[c] = value;
        }
        else
        {
            InnerGrid[c] = value;
        }
    }

    public override string GetUniqueLoadID()
    {
        return "TTG_PastureArea_" + ID;
    }
}
