using System;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace UnknownsSandbox
{
  // Tapping a SandBox Minecraft block (you or anyone else) plays the block's real
  // Minecraft step sound instead of Gorilla Tag's tap sound. VRRig.SetHandEffectData
  // fills in the tap's sound and position for every hand tap; afterwards, if that
  // position is on a Minecraft block, the game's tap is muted and ours plays.
  internal static class SbTapSounds
  {
    private static bool installed;
    private static FieldInfo posField, volField;
    private static readonly Collider[] hits = new Collider[12];

    internal static void Install(Harmony harmony)
    {
      if (installed || harmony == null) return;
      installed = true;
      try
      {
        MethodInfo m = AccessTools.Method(typeof(VRRig), "SetHandEffectData");
        Type ctx = AccessTools.TypeByName("HandEffectContext");
        if (m == null || ctx == null) { SbLog.Warn("hand tap hook not found"); return; }
        posField = AccessTools.Field(ctx, "position");
        volField = AccessTools.Field(ctx, "soundVolume");
        if (posField == null || volField == null) { SbLog.Warn("hand tap fields not found"); return; }
        harmony.Patch(m, postfix: new HarmonyMethod(typeof(SbTapSounds), nameof(Postfix)));
      }
      catch (Exception ex) { SbLog.Warn("hand tap hook: " + ex.Message); }
    }

    private static void Postfix(object effectContext, bool isDownTap)
    {
      try
      {
        if (effectContext == null || SandBoxWorld.Instance == null || SandBoxWorld.Instance.List.Count == 0) return;
        Vector3 pos = (Vector3)posField.GetValue(effectContext);
        string group = BlockSoundAt(pos);
        if (group == null) return;
        float vol = (float)volField.GetValue(effectContext);
        volField.SetValue(effectContext, 0f);
        if (!isDownTap) return;
        AudioClip clip = SbMinecraft.Sound("step_" + group) ?? SbMinecraft.Sound(group);
        if (clip != null) SbFx.PlayClip(clip, pos, Mathf.Clamp(vol, 0.05f, 0.6f) * 1.4f, 1f, 12f);
        else SbFx.Play(SbSound.Place, pos, Mathf.Clamp(vol, 0.05f, 0.5f), 1.4f, 12f);
      }
      catch { }
    }

    // the Minecraft sound group of a SandBox block right at this point, or null
    private static string BlockSoundAt(Vector3 p)
    {
      int n = Physics.OverlapSphereNonAlloc(p, 0.07f, hits, ~0, QueryTriggerInteraction.Ignore);
      for (int i = 0; i < n; i++)
      {
        Collider c = hits[i];
        if (c == null) continue;
        SbRelay r = c.GetComponentInParent<SbRelay>();
        if (r == null || r.Obj == null || r.Obj.Item == null) continue;
        SbItem it = r.Obj.Item;
        if (it.Tab == SbTab.Minecraft && it.Kind == SbKind.Block) return it.Sound;
      }
      return null;
    }
  }
}
