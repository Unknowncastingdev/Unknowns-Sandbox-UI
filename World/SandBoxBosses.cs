using System;
using System.Collections.Generic;
using GorillaLocomotion;
using TMPro;
using UnityEngine;

namespace UnknownsSandbox
{
  // the world's side of the newer mobs.
  //  - Mob shots: arrows and fireballs that can hit you (knocked back, like the rest of
  //    the SandBox: there is no health).
  //  - Fire spreads over wood, leaves, wool and the like, and burns them away.
  //  - The Ender Dragon fight: in the End, End crystals sit on the pillars (hit or shoot
  //    them: they blow up), and the dragon circles the island, strafes you with fireballs,
  //    charges at you and is healed by the crystals still standing. A boss bar floats at
  //    the top of your view (in the headset and on the desktop).
  //  - The Wither: a T of 4 soul sand with 3 wither skeleton skulls on top comes to life.
  internal sealed partial class SandBoxWorld
  {
    private static bool mobShooting;

    // the hidden items mob shots are sent as (so everyone sees them)
    private static string ProjItemId(SbAmmo a)
    {
      switch (a)
      {
        case SbAmmo.Arrow: return "proj_arrow";
        case SbAmmo.Fireball: return "proj_fireball";
        case SbAmmo.GhastBall: return "proj_ghastball";
        case SbAmmo.WitherSkull: return "proj_witherskull";
        case SbAmmo.DragonBall: return "proj_dragonball";
      }
      return null;
    }

    internal void MobShoot(SbAmmo a, Vector3 from, Vector3 dir)
    {
      SbItem it = SbCatalog.Get(ProjItemId(a));
      if (it == null) return;
      int seed = UnityEngine.Random.Range(1, int.MaxValue);
      mobShooting = true;
      try { Shoot(it, from, dir, seed, true, 1f); }
      finally { mobShooting = false; }
      SbNet.SendShot(it.Key, from, dir, seed, 1f);
      if (a == SbAmmo.Arrow) SbFx.PlayMc("bow_shoot", SbSound.Swing, from, 0.8f, 1.1f, 20f);
      else if (a == SbAmmo.GhastBall) SbFx.PlayMc("ghast_shoot", SbSound.Suck, from, 1f, 1f, 50f);
      else SbFx.PlayMc("blaze_shoot", SbSound.Flame, from, 0.8f, 1f, 30f);
    }

    // a mob shot passing through you
    private bool SegmentHitsMe(Vector3 a, Vector3 b, out Vector3 at)
    {
      at = b;
      try
      {
        GTPlayer p = GTPlayer.Instance;
        if (p == null || p.bodyCollider == null) return false;
        Vector3 head = p.headCollider.transform.position, body = p.bodyCollider.transform.position;
        foreach (Vector3 c in new[] { head, body, (head + body) * 0.5f })
        {
          Vector3 ab = b - a;
          float t = Mathf.Clamp01(Vector3.Dot(c - a, ab) / Mathf.Max(1e-6f, ab.sqrMagnitude));
          Vector3 q = a + ab * t;
          if ((q - c).sqrMagnitude < 0.3f * 0.3f * p.scale * p.scale) { at = q; return true; }
        }
      }
      catch { }
      return false;
    }

    private void MobShotHitsMe(SbProj p, Vector3 at)
    {
      Vector3 dir = p.Vel.sqrMagnitude > 1e-4f ? p.Vel.normalized : Vector3.up;
      if (ShieldBlocks(dir, at)) return;   // a shield held up stops it
      switch (p.Ammo)
      {
        case SbAmmo.GhastBall:
        case SbAmmo.WitherSkull:
          Explode(at, p.Ammo == SbAmmo.GhastBall ? 2.4f : 1.8f, SbBlast.Mc, true);
          return;
        case SbAmmo.DragonBall:
          SbFx.Burst(at, 60, new Color(0.8f, 0.3f, 1f), new Color(0.4f, 0.1f, 0.6f), 0.5f, 2.5f, 0.1f, 0.3f, 1.5f, 3f, -0.05f, false, 1.2f, Vector3.up, 80f, 0.5f, 1.5f, true);
          KnockLocalPlayer(at - dir, 3f, 1.4f);
          return;
        default:
          SbFx.Play(SbSound.Hit, at, 0.9f, 1f);
          SbFx.Burst(at, 10, new Color(1f, 0.3f, 0.3f), new Color(0.6f, 0.1f, 0.1f), 0.5f, 1.5f, 0.02f, 0.05f, 0.2f, 0.4f, 0.5f, false, 0.05f);
          KnockLocalPlayer(at - dir * 0.6f, 1.6f, p.Ammo == SbAmmo.Arrow ? 0.6f : 0.8f);
          return;
      }
    }

    // a fired arrow stays where it hit for a while
    private void StuckArrow(Vector3 point, Vector3 dir, SbAmmo ammo = SbAmmo.Arrow)
    {
      Mesh m;
      if (!ammoMeshes.TryGetValue(ammo, out m) || m == null) return;
      var go = new GameObject("SB stuck arrow");
      go.transform.SetParent(SbFx.Root, false);
      go.transform.SetPositionAndRotation(point - dir * 0.12f, Quaternion.LookRotation(dir));
      go.AddComponent<MeshFilter>().sharedMesh = m;
      go.AddComponent<MeshRenderer>().sharedMaterials = new[] { SbMats.Lit, SbMats.Glow };
      Destroy(go, 12f);
    }

    // ================================================================ fire
    private readonly Dictionary<SbObject, SbObject> fireOnBlock = new Dictionary<SbObject, SbObject>();
    private float fireSpreadAt;

    private static bool Flammable(SbItem it)
    {
      if (it == null || it.Kind != SbKind.Block) return false;
      string id = it.Id;
      return it.Sound == "wood" || it.Sound == "wool" || id.Contains("planks") || id.Contains("_log") || id.Contains("leaves") || id.Contains("wool") ||
             id.Contains("bookshelf") || id == "mc_hay" || id.StartsWith("mc_bed_", StringComparison.Ordinal);
    }

    internal void StartFireAt(Vector3 at, SbObject onBlock = null)
    {
      SbItem fire = SbCatalog.Get("mc_fire");
      if (fire == null || MineCount >= MaxMine) return;
      int fires = 0;
      foreach (SbObject o in List) if (o.Mine && o.Item.Id == "mc_fire") fires++;
      if (fires >= 40) return;
      float size = onBlock != null ? onBlock.BlockSize : 1f;
      SbObject f = Create(0, nextId++, fire, at, Quaternion.identity, size, true, 0, Vector3Int.zero);
      if (f == null) return;
      f.FuseLeft = 6f + UnityEngine.Random.value * 4f;
      SbNet.QueueSpawn(f);
      if (onBlock != null) fireOnBlock[f] = onBlock;
    }

    // every frame: fires burn the flammable blocks under them and jump to the ones next to them
    private void UpdateFireSpread()
    {
      // burnt out: the block it sat on is gone
      foreach (KeyValuePair<SbObject, SbObject> kv in new List<KeyValuePair<SbObject, SbObject>>(fireOnBlock))
      {
        SbObject f = kv.Key, b = kv.Value;
        bool fireGone = f == null || f.T == null || !objects.ContainsKey(f.Key);
        if (!fireGone && f.FuseLeft > 0.15f) continue;
        fireOnBlock.Remove(f);
        if (b != null && b.T != null && objects.ContainsKey(b.Key) && (b.Mine || b.Generated)) Remove(b, true, true);
      }
      if (Time.time < fireSpreadAt) return;
      fireSpreadAt = Time.time + 1.2f;
      foreach (SbObject f in new List<SbObject>(List))
      {
        if (!f.Mine || f.Item.Id != "mc_fire" || f.T == null || UnityEngine.Random.value > 0.35f) continue;
        foreach (SbObject b in List)
        {
          if (!b.IsCell || !(b.Mine || b.Generated) || !Flammable(b.Item) || b.T == null) continue;
          float s = b.BlockSize;
          if ((b.Center - f.T.position).sqrMagnitude > (s * 1.6f) * (s * 1.6f)) continue;
          if (fireOnBlock.ContainsValue(b)) continue;
          // on top of it, unless something is there
          Vector3Int up = b.Cell + Vector3Int.up;
          SbObject above;
          if (grid.TryGetValue(Pack(b.SizeIdx, up.x, up.y, up.z), out above) && above != null) continue;
          StartFireAt(b.Center + Vector3.up * s * 0.55f, b);
          break;
        }
      }
    }

    // ================================================================ water (for swimming mobs)
    internal bool WaterAt(Vector3 p)
    {
      SbFluid t;
      return FluidAt(p, out t) && t == SbFluid.Water;
    }

    // a hit at one player: you are knocked back, anyone else is sent a hit
    internal void KnockLocalPlayerAt(Vector3 player, Vector3 from, float strength, float up)
    {
      try
      {
        GTPlayer me = GTPlayer.Instance;
        if (me != null && me.headCollider != null && (me.headCollider.transform.position - player).sqrMagnitude < 0.6f)
        {
          KnockLocalPlayer(from, 6f, strength * 0.4f);
          return;
        }
      }
      catch { }
      foreach (VRRig r in Rigs())
      {
        if (r == null || r.isOfflineVRRig) continue;
        Vector3 h = r.headMesh != null ? r.headMesh.transform.position : r.transform.position;
        if ((h - player).sqrMagnitude > 0.6f) continue;
        int actor = ActorOf(r);
        if (actor > 0) SbNet.SendHitPlayer(actor, ((player - from).normalized + Vector3.up * up).normalized * strength * 3f);
        return;
      }
    }

    // ================================================================ deaths
    internal void OnMobDeath(SbObject o, SbMobDefs.Def d)
    {
      if (o == null || d == null) return;
      Vector3 c = o.Center;
      if (d.BlastOnDeath)
      {
        Explode(c, 3f, SbBlast.Normal, true);
        crystals.Remove(o);
      }
      if (d.Type == SbMobType.EnderDragon)
      {
        dragonDefeated = true;
        dragon = null;
        for (int i = 0; i < 6; i++)
          SbFx.Burst(c + UnityEngine.Random.insideUnitSphere * 4f, 80, new Color(1f, 0.9f, 1f), new Color(0.8f, 0.4f, 1f), 2f, 9f, 0.1f, 0.35f, 1.5f, 3f, -0.2f, true, 1.5f);
        SbFx.Flash(c, new Color(1f, 0.8f, 1f), 6f, 60f, 2f);
        SbFx.PlayMc("enderdragon_death", SbSound.Suck, c, 1f, 1f, 200f);
        Toast?.Invoke("The End", "The Ender Dragon is defeated!");
      }
      else if (d.Type == SbMobType.Wither)
      {
        SbFx.Flash(c, new Color(0.8f, 0.85f, 1f), 4f, 30f, 1.2f);
        SbFx.PlayMc("wither_death", SbSound.Suck, c, 1f, 1f, 150f);
        Toast?.Invoke("Wither", "The Wither is defeated!");
      }
    }

    // ================================================================ the dragon fight
    private readonly List<Vector3> endCrystalSpots = new List<Vector3>();
    private readonly List<SbObject> crystals = new List<SbObject>();
    private SbObject dragon;
    private bool dragonDefeated, crystalsPlaced;
    private float dragonSpawnAt = -1f;

    internal Vector3 DragonCentre
    {
      get { int c = EndSize / 2; return EndOrigin + new Vector3((c + 0.5f) * EndBlock, (EndTop + 2f) * EndBlock, (c + 0.5f) * EndBlock); }
    }

    internal bool NearestCrystal(Vector3 p, float max, out Vector3 at)
    {
      at = Vector3.zero;
      float best = max * max;
      bool found = false;
      foreach (SbObject c in crystals)
      {
        if (c == null || c.T == null || !objects.ContainsKey(c.Key)) continue;
        float d = (c.Center - p).sqrMagnitude;
        if (d < best) { best = d; at = c.Center + Vector3.up * 0.3f; found = true; }
      }
      return found;
    }

    private void UpdateDragonFight()
    {
      crystals.RemoveAll(c => c == null || c.T == null || !objects.ContainsKey(c.Key));
      if (!InEnd)
      {
        // gone when you leave (it comes back when you return, unless you beat it)
        foreach (SbObject c in crystals) Remove(c, false, false);
        crystals.Clear();
        crystalsPlaced = false;
        if (dragon != null && objects.ContainsKey(dragon.Key)) Remove(dragon, false, false);
        dragon = null;
        dragonSpawnAt = -1f;
        return;
      }
      if (dragonDefeated) return;
      if (!crystalsPlaced && endCrystalSpots.Count > 0)
      {
        crystalsPlaced = true;
        SbItem ci = SbCatalog.Get("mob_end_crystal");
        if (ci != null)
          foreach (Vector3 at in endCrystalSpots)
          {
            SbObject c = Create(-1, nextId++, ci, at + Vector3.down * 0.7f, Quaternion.identity, 1.4f, false, 0, Vector3Int.zero);
            if (c != null) crystals.Add(c);
          }
      }
      if (dragon == null || dragon.T == null || !objects.ContainsKey(dragon.Key))
      {
        if (dragonSpawnAt < 0f) dragonSpawnAt = Time.time + 4f;
        if (Time.time < dragonSpawnAt) return;
        dragonSpawnAt = -1f;
        SbItem di = SbCatalog.Get("mob_ender_dragon");
        if (di == null) return;
        dragon = Create(-1, nextId++, di, DragonCentre + new Vector3(26f, 18f, 0f), Quaternion.identity, 1f, false, 0, Vector3Int.zero);
        if (dragon == null) return;
        // it flies through the pillars and the island (its colliders only catch hits)
        foreach (Collider col in dragon.Cols) if (col != null) col.isTrigger = true;
        SbFx.PlayMc("enderdragon_growl", SbSound.Suck, dragon.T.position, 1f, 0.9f, 200f);
        Toast?.Invoke("The End", "The Ender Dragon appears! Break the End crystals first: they heal it.");
      }
    }

    // ================================================================ the Wither
    // a skull was placed: is it the last piece of a Wither (3 skulls on a T of soul sand)?
    internal void CheckWitherBuild(SbObject skull)
    {
      if (skull == null || !skull.IsCell) return;
      int s = skull.SizeIdx;
      Vector3Int k = skull.Cell;
      foreach (Vector3Int axis in new[] { Vector3Int.right, new Vector3Int(0, 0, 1) })
        for (int o = -1; o <= 1; o++)
        {
          Vector3Int mid = k - axis * o;
          var parts = new List<SbObject>();
          bool ok = true;
          for (int i = -1; i <= 1 && ok; i++)
          {
            SbObject sk = CellAt(s, mid + axis * i), sand = CellAt(s, mid + axis * i + Vector3Int.down);
            ok = sk != null && sk.Item.Id == "mc_wither_skull" && sand != null && sand.Item.Id == "mc_soulsand" && sk.Mine && sand.Mine;
            if (ok) { parts.Add(sk); parts.Add(sand); }
          }
          SbObject bottom = CellAt(s, mid + Vector3Int.down * 2);
          if (!ok || bottom == null || bottom.Item.Id != "mc_soulsand" || !bottom.Mine) continue;
          parts.Add(bottom);
          Vector3 at = CellCenter(mid + Vector3Int.down, BlockSizes[s]);
          foreach (SbObject p in parts) Remove(p, true, false);
          SbItem wi = SbCatalog.Get("mob_wither");
          if (wi == null) return;
          SbObject w = Create(0, nextId++, wi, at, Quaternion.identity, 1f, false, 0, Vector3Int.zero);
          if (w == null) return;
          undo.Add(w.Id);
          SbNet.QueueSpawn(w);
          SbFx.Flash(at, new Color(0.7f, 0.75f, 1f), 5f, 25f, 1f);
          SbFx.Burst(at, 80, new Color(0.25f, 0.25f, 0.3f), new Color(0.05f, 0.05f, 0.08f), 2f, 6f, 0.15f, 0.4f, 1f, 2f, -0.1f, false, 1f, null, 0f, 1f, 1.5f, true);
          SbFx.PlayMc("wither_spawn", SbSound.Suck, at, 1f, 0.8f, 200f);
          Toast?.Invoke("Wither", "You have summoned the Wither!");
          return;
        }
    }

    private SbObject CellAt(int s, Vector3Int c)
    {
      SbObject o;
      return grid.TryGetValue(Pack(s, c.x, c.y, c.z), out o) ? o : null;
    }

    // ================================================================ the boss bar
    private string bossName;
    private float bossFrac, bossAt = -10f;
    private Vector3 bossPos;
    private GameObject bossBar;
    private Transform bossFill;
    private TextMeshPro bossText;

    internal void ReportBoss(string name, float frac, Vector3 pos)
    {
      Transform head = HeadProvider != null ? HeadProvider() : null;
      // the nearest boss wins
      if (head != null && Time.time - bossAt < 0.2f && bossName != name && (pos - head.position).sqrMagnitude > (bossPos - head.position).sqrMagnitude) return;
      bossName = name; bossFrac = frac; bossPos = pos; bossAt = Time.time;
    }

    private void UpdateBossBar()
    {
      Transform head = HeadProvider != null ? HeadProvider() : null;
      bool show = head != null && Time.time - bossAt < 0.5f && (bossPos - head.position).sqrMagnitude < 150f * 150f;
      if (!show) { if (bossBar != null && bossBar.activeSelf) bossBar.SetActive(false); return; }
      if (bossBar == null)
      {
        bossBar = new GameObject("SB boss bar");
        bossBar.transform.SetParent(SbFx.Root, false);
        Quad(bossBar.transform, "back", new Color(0.1f, 0.05f, 0.12f, 0.85f), new Vector3(0.82f, 0.04f, 1f), Vector3.zero, 3100);
        bossFill = Quad(bossBar.transform, "fill", new Color(0.85f, 0.2f, 0.85f, 1f), new Vector3(0.8f, 0.026f, 1f), new Vector3(0f, 0f, -0.001f), 3101);
        var tg = new GameObject("name");
        tg.transform.SetParent(bossBar.transform, false);
        tg.transform.localPosition = new Vector3(0f, 0.045f, 0f);
        bossText = tg.AddComponent<TextMeshPro>();
        bossText.alignment = TextAlignmentOptions.Center;
        bossText.fontSize = 0.45f;
        bossText.textWrappingMode = TextWrappingModes.NoWrap;
        bossText.rectTransform.sizeDelta = new Vector2(1.2f, 0.1f);
        bossText.color = Color.white;
      }
      if (!bossBar.activeSelf) bossBar.SetActive(true);
      // at the top of your view, facing you
      bossBar.transform.SetPositionAndRotation(head.position + head.forward * 1.3f + head.up * 0.42f, Quaternion.LookRotation(head.forward, head.up));
      float f = Mathf.Clamp01(bossFrac);
      bossFill.localScale = new Vector3(0.8f * f, 0.026f, 1f);
      bossFill.localPosition = new Vector3(-0.4f * (1f - f), 0f, -0.001f);
      if (bossText != null && bossText.text != bossName) bossText.text = bossName;
    }

    private static Transform Quad(Transform parent, string name, Color c, Vector3 scale, Vector3 at, int queue)
    {
      GameObject g = GameObject.CreatePrimitive(PrimitiveType.Quad);
      g.name = name;
      Collider col = g.GetComponent<Collider>(); if (col != null) Destroy(col);
      g.transform.SetParent(parent, false);
      g.transform.localPosition = at;
      g.transform.localScale = scale;
      Renderer r = g.GetComponent<Renderer>();
      r.sharedMaterial = new Material(SbMats.Line) { hideFlags = HideFlags.DontSave, color = c, renderQueue = queue };
      r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
      return g.transform;
    }

    // ================================================================ every frame
    private void UpdateBossWorld()
    {
      try { UpdateDragonFight(); } catch (Exception ex) { SbLog.Throttled("SandBox dragon", ex); }
      try { UpdateFireSpread(); } catch (Exception ex) { SbLog.Throttled("SandBox fire", ex); }
      try { UpdateBossBar(); } catch (Exception ex) { SbLog.Throttled("SandBox boss bar", ex); }
    }
  }

  // the hidden items mob shots are sent as, and the wither skeleton skull
  internal static class SbBossItems
  {
    internal static void AddToCatalog(Action<SbItem> add)
    {
      foreach (var p in new[] { ("proj_arrow", SbAmmo.Arrow, 26f), ("proj_fireball", SbAmmo.Fireball, 14f), ("proj_ghastball", SbAmmo.GhastBall, 12f),
                                ("proj_witherskull", SbAmmo.WitherSkull, 16f), ("proj_dragonball", SbAmmo.DragonBall, 14f) })
        add(new SbItem { Id = p.Item1, Name = p.Item1, Desc = "", Tab = SbTab.Weapons, Section = "GUNS", Kind = SbKind.Gun, Hidden = true, Ammo = p.Item2, Speed = p.Item3, Spread = 0f, Pellets = 1,
                         Build = k => k.Box(Vector3.zero, Vector3.one * 0.05f, Color.white) });
      add(new SbItem { Id = "mc_wither_skull", Name = "Wither Skeleton Skull", Desc = "Three on top of a T of 4 soul sand bring the Wither to life.", Tab = SbTab.Minecraft, Section = "BOSSES",
                       Kind = SbKind.Block, Shape = SbShape.Skull, Top = SbBlockArt.T("blackstone"), Side = SbBlockArt.T("blackstone"), Bottom = SbBlockArt.T("blackstone"), Front = SbBlockArt.T("blackstone"), Sound = "stone" });
    }
  }
}
