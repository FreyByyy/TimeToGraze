using System.Reflection;
using HarmonyLib;
using UnityEngine;
using Verse;

namespace TimeToGraze;

public class TimeToGrazeMod : Mod
{
    public static TTG_Settings Settings;

    public TimeToGrazeMod(ModContentPack content) : base(content)
    {
        Settings = GetSettings<TTG_Settings>();
        new Harmony("svyat.TimeToGraze").PatchAll(Assembly.GetExecutingAssembly());
    }

    public override string SettingsCategory()
    {
        return "TTG_ModName".Translate();
    }

    public override void DoSettingsWindowContents(Rect inRect)
    {
        Settings.DoWindowContents(inRect);
    }
}
