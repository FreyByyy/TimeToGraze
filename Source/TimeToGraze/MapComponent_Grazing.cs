using System.Linq;
using Verse;

namespace TimeToGraze;

public class MapComponent_Grazing : MapComponent
{
    private const int TickInterval = 250;

    public MapComponent_Grazing(Map map) : base(map)
    {
    }

    public override void MapComponentTick()
    {
        if ((Find.TickManager.TicksGame + map.uniqueID) % TickInterval != 0)
        {
            return;
        }
        // Copy: a session ending can despawn nothing, but markers may be built/destroyed by other mods mid-loop.
        foreach (CompGrazing comp in GrazingUtility.CompsOn(map).ToList())
        {
            comp.GrazingTick();
        }
    }
}
