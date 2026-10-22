using System;
using System.Collections.Generic;
using GorillaLocomotion;
using Photon.Pun;
using UnityEngine;

namespace UnknownsSandbox
{
  // Flint and steel, fire, nether portals and the Nether.
  // A portal is lit inside an obsidian frame (inside at least 2 wide x 3 tall,
  // at most 21 x 21, corners not needed) like Minecraft. Standing in it for
  // 1.5 s takes you to the Nether ONLY in a modded lobby or when everyone in the
  // room runs the SandBox. The Nether is built here (same on every client),
  // far above the map, with its own return portal.
  internal sealed partial class SandBoxWorld
  {
    // ================================================================ flint and steel
    internal void UseFlint(Vector3 origin, Vector3 dir)
    {
      var ray = new Ray(origin, dir.normalized);
      RaycastHit wh; bool wHit = RaycastWorld(ray, 8f, out wh);
      float od; Vector3 on;
      SbObject o = RaycastObjects(ray, 8f, out od, out on, false, false);
      bool objHit = o != null && (!wHit || od <= wh.distance);
      if (!objHit && !wHit) return;
      Vector3 hp = objHit ? origin + ray.direction * od : wh.point;
      Vector3 hn = objHit ? on : wh.normal;
      SbFx.PlayMc("ignite", SbSound.Fuse, hp, 1f, 1f);
      SbFx.Burst(hp, 8, new Color(1f, 0.85f, 0.4f), Color.white, 0.5f, 2f, 0.01f, 0.025f, 0.15f, 0.35f, 1f, true, 0.01f, hn, 40f);

      if (objHit && o.Item.BlockFx == SbBlockFx.Tnt)
      {
        if (o.Mine) Prime(o, 4f, true);
        else if (o.Owner > 0) SbNet.SendHitObject(o.Owner, o.Id, Vector3.zero, hp, true);
        return;
      }
      if (objHit && o.Item.Kind == SbKind.Explosive)
      {
        if (o.Mine) ApplyHitToMine(o, Vector3.zero, hp, true);
        else if (o.Owner > 0) SbNet.SendHitObject(o.Owner, o.Id, Vector3.zero, hp, true);
        return;
      }
      if (objHit && o.IsCell && o.Item.Id == "mc_obsidian")
      {
        Vector3Int start = o.Cell + Vector3Int.RoundToInt(SnapAxis(on));
        if (TryLightPortal(o.SizeIdx, start)) return;
      }
      Vector3 at = hp + hn * 0.12f;
      // fire inside an obsidian frame lights the portal, wherever it lands
      if (TryLightPortalAround(at)) return;
      // an object catches fire (the fire sticks to it and burns for a while)
      if (objHit && !(o.IsCell && o.Item.Id == "mc_obsidian")) { IgniteObject(o, hp); return; }
      // fire on whatever was hit
      SbItem fire = SbCatalog.Get("mc_fire");
      if (fire == null || MineCount >= MaxMine) return;
      SbObject f = Create(0, nextId++, fire, at, Quaternion.identity, 1f, true, 0, Vector3Int.zero);
      if (f == null) return;
      f.FuseLeft = 8f;
      SbNet.QueueSpawn(f);
      // fire lights TNT next to it
      foreach (SbObject t in new List<SbObject>(List))
        if (t.Mine && !t.Primed && t.Item.BlockFx == SbBlockFx.Tnt && Vector3.Distance(t.Center, at) < t.BlockSize * 1.2f) Prime(t, 4f, true);
    }

    private void UpdateFires(float dt)
    {
      for (int i = List.Count - 1; i >= 0; i--)
      {
        if (i >= List.Count) continue;
        SbObject o = List[i];
        if (!o.Mine || o.Item.Id != "mc_fire") continue;
        o.FuseLeft -= dt;
        if (o.FuseLeft <= 0f) Remove(o, true, false);
      }
    }

    // ================================================================ portals
    private int portalCount;
    private static readonly Vector3Int[] PlaneX = { Vector3Int.right, Vector3Int.left, Vector3Int.up, Vector3Int.down };
    private static readonly Vector3Int[] PlaneZ = { new Vector3Int(0, 0, 1), new Vector3Int(0, 0, -1), Vector3Int.up, Vector3Int.down };

    private bool IsObsidian(int s, Vector3Int c)
    {
      SbObject b;
      return grid.TryGetValue(Pack(s, c.x, c.y, c.z), out b) && b != null && b.Item.Id == "mc_obsidian" && !b.Primed;
    }

    private bool TryLightPortal(int s, Vector3Int start)
    {
      SbItem portal = SbCatalog.Get("mc_portal");
      if (portal == null) return false;
      for (int orient = 0; orient < 2; orient++)
      {
        Vector3Int[] dirs = orient == 0 ? PlaneX : PlaneZ;
        var inside = new HashSet<Vector3Int>();
        var queue = new Queue<Vector3Int>();
        bool ok = !grid.ContainsKey(Pack(s, start.x, start.y, start.z)) && !WorldSolid(s, start);
        if (ok) { inside.Add(start); queue.Enqueue(start); }
        while (ok && queue.Count > 0)
        {
          Vector3Int c = queue.Dequeue();
          foreach (Vector3Int d in dirs)
          {
            Vector3Int n = c + d;
            if (inside.Contains(n) || IsObsidian(s, n)) continue;
            if (grid.ContainsKey(Pack(s, n.x, n.y, n.z)) || WorldSolid(s, n)) { ok = false; break; }
            inside.Add(n);
            queue.Enqueue(n);
            if (inside.Count > 441) { ok = false; break; }
          }
        }
        if (!ok || inside.Count == 0) continue;
        int minU = int.MaxValue, maxU = int.MinValue, minY = int.MaxValue, maxY = int.MinValue;
        foreach (Vector3Int c in inside)
        {
          int u = orient == 0 ? c.x : c.z;
          minU = Mathf.Min(minU, u); maxU = Mathf.Max(maxU, u); minY = Mathf.Min(minY, c.y); maxY = Mathf.Max(maxY, c.y);
        }
        if (maxU - minU + 1 < 2 || maxY - minY + 1 < 3 || maxU - minU + 1 > 21 || maxY - minY + 1 > 21) continue;
        Quaternion rot = orient == 0 ? Quaternion.identity : Quaternion.Euler(0f, 90f, 0f);
        foreach (Vector3Int c in inside)
        {
          if (MineCount >= MaxMine) break;
          SbObject p = Create(0, nextId++, portal, CellCenter(c, BlockSizes[s]), rot, 1f, true, s, c);
          if (p != null) SbNet.QueueSpawn(p);
        }
        SbFx.PlayMc("portal_trigger", SbSound.Suck, CellCenter(start, BlockSizes[s]), 0.8f);
        return true;
      }
      return false;
    }

    // fire at this point: is it inside an obsidian frame (of any block size)? Light it.
    internal bool TryLightPortalAround(Vector3 p)
    {
      for (int s = 0; s < BlockSizes.Length; s++)
      {
        Vector3Int c = CellOf(p, BlockSizes[s]);
        if (grid.ContainsKey(Pack(s, c.x, c.y, c.z))) continue;
        // only worth the flood fill with obsidian right next to it
        bool near = false;
        foreach (Vector3Int d in Dirs6) if (IsObsidian(s, c + d)) { near = true; break; }
        if (!near) continue;
        if (TryLightPortal(s, c)) return true;
      }
      return false;
    }

    // breaking the frame (or a portal block) breaks the whole portal
    private void BreakPortalsNear(int s, Vector3Int c)
    {
      var stack = new Stack<Vector3Int>();
      foreach (Vector3Int d in Dirs6) stack.Push(c + d);
      stack.Push(c);
      var seen = new HashSet<Vector3Int>();
      while (stack.Count > 0)
      {
        Vector3Int n = stack.Pop();
        if (!seen.Add(n) || seen.Count > 600) continue;
        SbObject b;
        if (!grid.TryGetValue(Pack(s, n.x, n.y, n.z), out b) || b == null || b.Item.BlockFx != SbBlockFx.Portal) continue;
        if (b.Mine || b.Generated) Remove(b, true, false);
        foreach (Vector3Int d in Dirs6) stack.Push(n + d);
      }
    }

    // ================================================================ travel
    internal bool InNether { get; private set; }
    private float portalTime, portalCooldown;
    private bool portalToastShown;
    private Vector3 returnHead;
    private Quaternion returnRot = Quaternion.identity;
    private AudioSource netherAmbience;
    private float nextNetherMood;

    internal bool PortalAllowed
    {
      get { return PhotonNetwork.InRoom && SolidAllowed && (GateReason == "MODDED LOBBY" || GateReason == "ALL UNKNOWNS"); }
    }

    private bool InsidePortal(Vector3 p)
    {
      if (InNether && netherPortal.size.sqrMagnitude > 0f && netherPortal.Contains(p)) return true;
      if (portalCount <= 0) return false;
      foreach (int s in portalSizes)
      {
        SbObject b;
        Vector3Int c = CellOf(p, BlockSizes[s]);
        if (grid.TryGetValue(Pack(s, c.x, c.y, c.z), out b) && b != null && b.Item.BlockFx == SbBlockFx.Portal) return true;
      }
      return false;
    }

    private readonly HashSet<int> portalSizes = new HashSet<int>();
    internal void NotePortal(SbObject o, bool added)
    {
      if (o.Item.BlockFx == SbBlockFx.EndPortal) { NoteEndPortal(o, added); return; }
      if (o.Item.BlockFx != SbBlockFx.Portal) return;
      portalCount = Mathf.Max(0, portalCount + (added ? 1 : -1));
      if (added) portalSizes.Add(o.SizeIdx);
    }

    private void UpdatePortals(float dt)
    {
      UpdateFires(dt);
      swirlWant = 0f;
      if (portalCooldown > 0f) portalCooldown -= dt;
      GTPlayer p = null;
      try { p = GTPlayer.Instance; } catch { }
      if (p == null || p.bodyCollider == null) return;
      Vector3 body = p.bodyCollider.transform.position;
      Vector3 head = p.headCollider.transform.position;
      // the End (SandBoxEnd.cs): its portals, the void, the way home
      if (UpdateEnd(dt, p, body, head)) return;

      if (InNether)
      {
        UpdateNetherWorld(head);
      }
      if (InNether)
      {
        // lost the right to be here (a non-Unknowns player joined, left the room...) -> home
        if (!PortalAllowed) { Travel(false); Toast?.Invoke("Nether", "Sent back: the Nether only works in modded lobbies or when everyone has the SandBox"); return; }
        // fell in the lava sea / out of the world
        if (body.y < NetherOrigin.y + NetherLava * NetherBlock + 0.15f || body.y < NetherOrigin.y - 5f)
        {
          SbFx.PlayMc("lava", SbSound.Sizzle, body, 1f);
          p.TeleportTo(NetherSpawn, p.transform.rotation, false, true);
        }
        if (Time.time >= nextNetherMood)
        {
          nextNetherMood = Time.time + UnityEngine.Random.Range(12f, 30f);
          AudioClip mood = SbMinecraft.Sound("nether_mood");
          if (mood != null) SbFx.PlayClip(mood, head + UnityEngine.Random.onUnitSphere * 4f, 0.7f, 1f, 30f);
        }
        if (UnityEngine.Random.value < dt * 6f)
          SbFx.Burst(head + UnityEngine.Random.insideUnitSphere * 4f, 3, new Color(0.6f, 0.55f, 0.5f, 0.8f), new Color(0.9f, 0.4f, 0.2f, 0.8f), 0.05f, 0.2f, 0.015f, 0.03f, 4f, 6f, 0.01f, false, 0.5f);
      }

      bool inside = portalCooldown <= 0f && (InsidePortal(body) || InsidePortal(head));
      if (!inside) { portalTime = 0f; portalToastShown = false; return; }
      if (!PortalAllowed)
      {
        if (!portalToastShown)
        {
          portalToastShown = true;
          Toast?.Invoke("Nether Portal", "Only works in a modded lobby or when everyone in the room has the SandBox");
        }
        return;
      }
      if (portalTime == 0f) SbFx.PlayMc("portal_trigger", SbSound.Suck, head, 0.7f);
      portalTime += dt;
      swirlWant = Mathf.Clamp01(portalTime / 1.5f);
      if (portalTime >= 1.5f) Travel(!InNether);
    }

    private void Travel(bool toNether)
    {
      GTPlayer p = null;
      try { p = GTPlayer.Instance; } catch { }
      if (p == null) return;
      portalTime = 0f;
      portalCooldown = 3f;
      if (toNether)
      {
        returnHead = p.headCollider.transform.position;
        Vector3 back = Vector3.ProjectOnPlane(p.headCollider.transform.forward, Vector3.up);
        if (back.sqrMagnitude < 1e-4f) back = Vector3.forward;
        returnHead -= back.normalized * 1.2f;   // step out of the portal you came from
        returnRot = p.transform.rotation;
        BuildNether();
        p.TeleportTo(NetherSpawn, p.transform.rotation, false, true);
        InNether = true;
        StartNetherAmbience(true);
      }
      else
      {
        p.TeleportTo(returnHead, returnRot, false, true);
        InNether = false;
        StartNetherAmbience(false);
      }
      SbFx.PlayMc("portal_travel", SbSound.UiOpen, p.headCollider.transform.position, 0.5f);
    }

    private void StartNetherAmbience(bool on)
    {
      AudioClip amb = SbMinecraft.Sound("nether_amb");
      if (!on || amb == null)
      {
        if (netherAmbience != null) netherAmbience.Stop();
        return;
      }
      if (netherAmbience == null)
      {
        netherAmbience = new GameObject("SB Nether Ambience").AddComponent<AudioSource>();
        netherAmbience.transform.SetParent(transform, false);
        netherAmbience.spatialBlend = 0f;
        netherAmbience.loop = true;
      }
      netherAmbience.clip = amb;
      netherAmbience.volume = 0.6f * SbFx.Volume;
      netherAmbience.Play();
    }
  }
}
