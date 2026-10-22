using System;
using System.Collections.Generic;
using GorillaLocomotion;
using UnityEngine;

namespace UnknownsSandbox
{
  // The End, and the held tools (flint and steel, Eye of Ender).
  // An End portal is 12 End Portal Frames in a 5 x 5 ring without its corners,
  // all at the same height, each with an Eye of Ender in it: the 3 x 3 inside
  // becomes the portal. Jumping in takes you to the End: the main island with
  // its obsidian pillars and end crystals, and the exit portal in the middle
  // that brings you home. Like the Nether it is built the same on every client
  // at one fixed place, so everyone who goes through any portal ends up in the
  // same End, and it only works in a modded lobby or when everyone in the room
  // has the SandBox.
  internal sealed partial class SandBoxWorld
  {
    private bool EndEyeSwap;            // swapping a frame for its eye version: not a broken portal

    // ================================================================ tools
    private float toolTouchAt;

    // the tool picked on the tablet, pointed at something with the trigger
    internal void UseTool(SbItem item, Vector3 origin, Vector3 dir)
    {
      if (item == null) return;
      if (item.Id == "mc_endereye") UseEye(origin, dir);
      else if (item.Id == "ob_weld") UseWeld(origin, dir);
      else if (item.Pearl) ThrowPearlFrom(origin, dir);
      else UseFlint(origin, dir);
    }

    // the tool held in a hand: touching things with its tip, trigger = use it ahead
    private void UseHeldTool(SbObject o, bool right, bool triggerEdge)
    {
      Vector3 tip = o.T.TransformPoint(o.Item.Muzzle * 1f);
      Vector3 dir = tip - o.T.position;
      if (dir.sqrMagnitude < 1e-6f) dir = o.T.up;
      dir.Normalize();
      bool eye = o.Item.Id == "mc_endereye";
      if (triggerEdge)
      {
        if (eye) UseEye(tip - dir * 0.05f, dir);
        else UseFlint(tip - dir * 0.05f, dir);
        Haptic?.Invoke(right, 0.25f, 0.04f);
        toolTouchAt = Time.time + 0.5f;
        return;
      }
      if (Time.time < toolTouchAt) return;
      SbObject touched = TouchedObject(tip, 0.035f * Mathf.Max(0.5f, o.Scale), o);
      if (touched == null) return;
      toolTouchAt = Time.time + 0.5f;
      Haptic?.Invoke(right, 0.3f, 0.05f);
      if (eye)
      {
        if (touched.Item.Id == "mc_endframe") InsertEye(touched);
        return;
      }
      SbFx.Burst(tip, 8, new Color(1f, 0.85f, 0.4f), Color.white, 0.5f, 2f, 0.01f, 0.025f, 0.15f, 0.35f, 1f, true, 0.01f, dir, 40f);
      if (touched.IsCell && touched.Item.Id == "mc_obsidian")
      {
        // the inside face of a frame: light the portal
        Vector3 away = (tip - touched.Center);
        Vector3Int start = touched.Cell + Vector3Int.RoundToInt(SnapAxis(away));
        if (TryLightPortal(touched.SizeIdx, start)) return;
      }
      if (TryLightPortalAround(tip)) return;
      IgniteObject(touched, tip);
    }

    private static readonly Collider[] toolHits = new Collider[16];

    private SbObject TouchedObject(Vector3 p, float radius, SbObject except)
    {
      int n = Physics.OverlapSphereNonAlloc(p, radius, toolHits, ~0, QueryTriggerInteraction.Ignore);
      SbObject best = null;
      float bestD = float.MaxValue;
      for (int i = 0; i < n; i++)
      {
        SbRelay r = toolHits[i] != null ? toolHits[i].GetComponentInParent<SbRelay>() : null;
        if (r == null || r.Obj == null || r.Obj == except || r.Obj.T == null || r.Obj.Item.Id == "mc_fire") continue;
        float d = (r.Obj.Center - p).sqrMagnitude;
        if (d < bestD) { bestD = d; best = r.Obj; }
      }
      return best;
    }

    // ================================================================ eyes in frames
    private void UseEye(Vector3 origin, Vector3 dir)
    {
      var ray = new Ray(origin, dir.normalized);
      float od; Vector3 on;
      SbObject o = RaycastObjects(ray, 8f, out od, out on, false, false);
      if (o == null) return;
      RaycastHit wh;
      if (RaycastWorld(ray, od, out wh) && wh.distance < od - 0.01f) return;
      if (o.Item.Id == "mc_endframe") InsertEye(o);
    }

    private void InsertEye(SbObject frame)
    {
      if (frame == null || !frame.IsCell || frame.Item.Id != "mc_endframe") return;
      if (!frame.Mine && !frame.Generated)
      {
        Toast?.Invoke("End Portal", "That frame is another player's: put eyes in your own frames");
        return;
      }
      SbItem withEye = SbCatalog.Get("mc_endframe_eye");
      if (withEye == null) return;
      int s = frame.SizeIdx;
      Vector3Int cell = frame.Cell;
      Quaternion rot = frame.T != null ? frame.T.rotation : Quaternion.identity;
      Vector3 c = frame.Center;
      EndEyeSwap = true;
      try { Remove(frame, true, false); }
      finally { EndEyeSwap = false; }
      SbObject e = Create(0, nextId++, withEye, c, rot, 1f, true, s, cell);
      if (e == null) return;
      undo.Add(e.Id);
      SbNet.QueueSpawn(e);
      SbFx.PlayMc("eye_place", SbSound.Place, c, 1f, 1f);
      SbFx.Burst(c + Vector3.up * BlockSizes[s] * 0.5f, 14, new Color(0.35f, 1f, 0.6f), new Color(0.1f, 0.5f, 0.35f), 0.2f, 0.8f, 0.01f, 0.025f, 0.4f, 0.8f, -0.1f, true, BlockSizes[s] * 0.2f, Vector3.up, 50f);
      CheckEndPortal(s, cell);
    }

    private bool HasEye(int s, Vector3Int c)
    {
      SbObject b;
      return grid.TryGetValue(Pack(s, c.x, c.y, c.z), out b) && b != null && b.Item.Id == "mc_endframe_eye";
    }

    // the 12 frames around a 3 x 3 inside centred on p
    private static IEnumerable<Vector3Int> Ring(Vector3Int p)
    {
      for (int i = -1; i <= 1; i++)
      {
        yield return new Vector3Int(p.x + i, p.y, p.z + 2);
        yield return new Vector3Int(p.x + i, p.y, p.z - 2);
        yield return new Vector3Int(p.x + 2, p.y, p.z + i);
        yield return new Vector3Int(p.x - 2, p.y, p.z + i);
      }
    }

    private void CheckEndPortal(int s, Vector3Int changed)
    {
      SbItem portal = SbCatalog.Get("mc_endportal");
      if (portal == null) return;
      for (int dx = -2; dx <= 2; dx++)
        for (int dz = -2; dz <= 2; dz++)
        {
          var centre = new Vector3Int(changed.x + dx, changed.y, changed.z + dz);
          bool onRing = false, full = true;
          foreach (Vector3Int r in Ring(centre))
          {
            if (r == changed) onRing = true;
            if (!HasEye(s, r)) { full = false; break; }
          }
          if (!onRing || !full) continue;
          // the inside must be open
          bool open = true;
          for (int ix = -1; ix <= 1 && open; ix++)
            for (int iz = -1; iz <= 1 && open; iz++)
            {
              SbObject b;
              var ic = new Vector3Int(centre.x + ix, centre.y, centre.z + iz);
              if (grid.TryGetValue(Pack(s, ic.x, ic.y, ic.z), out b) && b != null) open = b.Item.BlockFx == SbBlockFx.EndPortal;
            }
          if (!open) continue;
          float size = BlockSizes[s];
          for (int ix = -1; ix <= 1; ix++)
            for (int iz = -1; iz <= 1; iz++)
            {
              var ic = new Vector3Int(centre.x + ix, centre.y, centre.z + iz);
              if (grid.ContainsKey(Pack(s, ic.x, ic.y, ic.z)) || MineCount >= MaxMine) continue;
              SbObject pb = Create(0, nextId++, portal, CellCenter(ic, size), Quaternion.identity, 1f, true, s, ic);
              if (pb != null) SbNet.QueueSpawn(pb);
            }
          Vector3 mid = CellCenter(centre, size);
          SbFx.PlayMc("end_portal_open", SbSound.Suck, mid, 1f, 1f);
          SbFx.Burst(mid, 60, new Color(0.3f, 1f, 0.75f), new Color(0.6f, 0.2f, 1f), 0.4f, 1.6f, 0.01f, 0.03f, 0.6f, 1.4f, -0.2f, true, size * 1.2f, Vector3.up, 70f, 1.5f);
          Toast?.Invoke("End Portal", "The portal is open: jump in to go to the End");
          return;
        }
    }

    // an eye frame removed: the End portal next to it closes
    private void BreakEndPortalNear(int s, Vector3Int c)
    {
      var gone = new List<SbObject>();
      for (int dx = -3; dx <= 3; dx++)
        for (int dz = -3; dz <= 3; dz++)
        {
          SbObject b;
          if (grid.TryGetValue(Pack(s, c.x + dx, c.y, c.z + dz), out b) && b != null && b.Item.BlockFx == SbBlockFx.EndPortal && (b.Mine || b.Generated)) gone.Add(b);
        }
      foreach (SbObject b in gone) Remove(b, true, false);
    }

    // ================================================================ portal tracking
    private int endPortalCount;
    private readonly HashSet<int> endPortalSizes = new HashSet<int>();

    private void NoteEndPortal(SbObject o, bool added)
    {
      endPortalCount = Mathf.Max(0, endPortalCount + (added ? 1 : -1));
      if (added) endPortalSizes.Add(o.SizeIdx);
    }

    // the End portal block you are in (the surface is low in the frame: count the cell's lower half too)
    private SbObject EndPortalAt(Vector3 p)
    {
      if (endPortalCount <= 0) return null;
      foreach (int s in endPortalSizes)
      {
        float size = BlockSizes[s];
        for (int k = 0; k < 2; k++)
        {
          Vector3Int c = CellOf(p - Vector3.up * size * 0.5f * k, size);
          SbObject b;
          if (grid.TryGetValue(Pack(s, c.x, c.y, c.z), out b) && b != null && b.Item.BlockFx == SbBlockFx.EndPortal) return b;
        }
      }
      return null;
    }

    // ================================================================ travel
    internal bool InEnd { get; private set; }
    private bool endToastShown;
    private AudioSource endMusic;

    // true when it took care of this frame (the Nether checks then skip)
    private bool UpdateEnd(float dt, GTPlayer p, Vector3 body, Vector3 head)
    {
      if (InEnd)
      {
        if (!PortalAllowed) { TravelEnd(false, null); Toast?.Invoke("The End", "Sent back: the End only works in modded lobbies or when everyone has the SandBox"); return true; }
        // fell into the void
        if (body.y < EndOrigin.y - 25f) { p.TeleportTo(EndSpawn, Quaternion.LookRotation(Vector3.forward), false, true); return true; }
        if (portalCooldown <= 0f && (endExit.Contains(body) || endExit.Contains(head))) { TravelEnd(false, null); return true; }
        return true;
      }
      if (portalCooldown > 0f || endPortalCount <= 0) return false;
      SbObject at = EndPortalAt(body) ?? EndPortalAt(head);
      if (at == null) { endToastShown = false; return false; }
      if (!PortalAllowed || InNether)
      {
        if (!endToastShown)
        {
          endToastShown = true;
          Toast?.Invoke("End Portal", InNether ? "Go back home first: End portals don't work in the Nether"
            : "Only works in a modded lobby or when everyone in the room has the SandBox");
        }
        return false;
      }
      TravelEnd(true, at);
      return true;
    }

    private void TravelEnd(bool toEnd, SbObject portalBlock)
    {
      GTPlayer p = null;
      try { p = GTPlayer.Instance; } catch { }
      if (p == null) return;
      portalTime = 0f;
      portalCooldown = 3f;
      if (toEnd)
      {
        // home is next to the ring you jumped into, outside it
        Vector3 head = p.headCollider.transform.position;
        Vector3 mid = portalBlock != null ? EndPortalCentre(portalBlock) : head;
        float size = portalBlock != null ? portalBlock.BlockSize : 0.5f;
        Vector3 away = Vector3.ProjectOnPlane(head - mid, Vector3.up);
        if (away.sqrMagnitude < 1e-3f) away = Vector3.ProjectOnPlane(-p.headCollider.transform.forward, Vector3.up);
        if (away.sqrMagnitude < 1e-3f) away = Vector3.back;
        returnHead = mid + away.normalized * (size * 3f + 0.8f) + Vector3.up * (size + 1.2f);
        returnRot = p.transform.rotation;
        BuildEnd();
        p.TeleportTo(EndSpawn, Quaternion.LookRotation(Vector3.forward), false, true);
        InEnd = true;
        StartEndMusic(true);
      }
      else
      {
        p.TeleportTo(returnHead, returnRot, false, true);
        InEnd = false;
        StartEndMusic(false);
      }
      SbFx.PlayMc("portal_travel", SbSound.UiOpen, p.headCollider.transform.position, 0.5f);
    }

    private Vector3 EndPortalCentre(SbObject b)
    {
      Vector3 sum = Vector3.zero; int n = 0;
      int s = b.SizeIdx;
      for (int dx = -2; dx <= 2; dx++)
        for (int dz = -2; dz <= 2; dz++)
        {
          SbObject o;
          if (grid.TryGetValue(Pack(s, b.Cell.x + dx, b.Cell.y, b.Cell.z + dz), out o) && o != null && o.Item.BlockFx == SbBlockFx.EndPortal) { sum += o.Center; n++; }
        }
      return n > 0 ? sum / n : b.Center;
    }

    private void StartEndMusic(bool on)
    {
      AudioClip clip = SbMinecraft.Sound("end_amb");
      if (!on || clip == null)
      {
        if (endMusic != null) endMusic.Stop();
        return;
      }
      if (endMusic == null)
      {
        endMusic = new GameObject("SB End Music").AddComponent<AudioSource>();
        endMusic.transform.SetParent(transform, false);
        endMusic.spatialBlend = 0f;
        endMusic.loop = true;
      }
      endMusic.clip = clip;
      endMusic.volume = 0.25f * SbFx.Volume;
      endMusic.Play();
    }

    // ================================================================ the End
    // Gorilla Tag sends rig positions packed to +-1024 m per axis: anything
    // further out shows the player clamped far away, so the End sits at 880 m
    internal static readonly Vector3 EndOrigin = new Vector3(-25f, 880f, -25f);
    private const float EndBlock = 0.5f;
    private const int EndSize = 100;
    private const int EndTop = 24;           // the island's surface (blocks above the origin)
    private const int EndRadius = 36;
    private GameObject end;
    private Bounds endExit;

    private Vector3 EndSpawn
    {
      // inside the ring of pillars, looking at the exit portal in the middle
      get { int c = EndSize / 2; return EndOrigin + new Vector3((c + 0.5f) * EndBlock, (EndTop + 4f) * EndBlock, (c - 13.5f) * EndBlock); }
    }

    private struct EndPillar { public int x, z, r, h; }

    private static List<EndPillar> EndPillars()
    {
      // ten obsidian pillars in a ring, like the real End (sizes and heights fixed: same for everyone)
      var list = new List<EndPillar>();
      int c = EndSize / 2;
      for (int i = 0; i < 10; i++)
      {
        float a = i / 10f * Mathf.PI * 2f + 0.3f;
        int r = 2 + (i * 7 % 3);
        int h = 26 + (i * 13 % 7) * 5;
        list.Add(new EndPillar { x = c + Mathf.RoundToInt(Mathf.Cos(a) * 25f), z = c + Mathf.RoundToInt(Mathf.Sin(a) * 25f), r = r, h = h });
      }
      return list;
    }

    // the End is real blocks too (SandBoxVoxels.cs): the island's shape is
    // worked out once (the same for everyone), the blocks come from it
    private int[,] endBot, endTop, endObs;
    private int endPortalY;
    private const int EndHeight = 96;

    private SbVoxelDim EnsureEndDim()
    {
      if (endDim != null) return endDim;
      int n = EndSize, c = n / 2;
      int[,] bot = new int[n, n], top = new int[n, n], obsFrom = new int[n, n];
      for (int x = 0; x < n; x++)
        for (int z = 0; z < n; z++)
        {
          float dx = x - c + 0.5f, dz = z - c + 0.5f;
          // a slightly wobbly round island, deep and rounded underneath
          float wob = 1f + 0.12f * (N2(x * 0.08f, z * 0.08f, 11) - 0.5f);
          float d = Mathf.Sqrt(dx * dx + dz * dz) / (EndRadius * wob);
          obsFrom[x, z] = int.MaxValue;
          if (d >= 1f) { bot[x, z] = top[x, z] = 0; continue; }
          int t = EndTop + Mathf.RoundToInt(1.6f * (1f - d * d) + 1.2f * (N2(x * 0.15f, z * 0.15f, 12) - 0.5f));
          int depth = Mathf.RoundToInt(3f + 18f * Mathf.Sqrt(1f - d * d) * (0.8f + 0.4f * N2(x * 0.1f, z * 0.1f, 13)));
          top[x, z] = t;
          bot[x, z] = Mathf.Max(1, t - depth);
        }
      foreach (EndPillar pl in EndPillars())
        for (int x = pl.x - pl.r; x <= pl.x + pl.r; x++)
          for (int z = pl.z - pl.r; z <= pl.z + pl.r; z++)
          {
            if (x < 0 || z < 0 || x >= n || z >= n) continue;
            float ddx = x - pl.x, ddz = z - pl.z;
            if (ddx * ddx + ddz * ddz > (pl.r + 0.5f) * (pl.r + 0.5f) || top[x, z] <= 0) continue;
            obsFrom[x, z] = top[x, z];
            top[x, z] = Mathf.Min(EndHeight - 2, top[x, z] + pl.h);
          }
      endBot = bot; endTop = top; endObs = obsFrom;
      endPortalY = top[c, c];
      endDim = new SbVoxelDim(2, EndOrigin, EndBlock, EndHeight, FillEnd, null);
      return endDim;
    }

    // one End column: end stone, obsidian pillars, and the bedrock exit portal in the middle
    private void FillEnd(int x, int z, byte[] col)
    {
      Array.Clear(col, 0, col.Length);
      int n = EndSize, c = n / 2;
      if (x < 0 || z < 0 || x >= n || z >= n) return;
      int b = endBot[x, z], t = endTop[x, z], o = endObs[x, z];
      for (int y = b; y < t && y < col.Length; y++) col[y] = y >= o ? (byte)SbVox.Obsidian : (byte)SbVox.EndStone;
      int ax = Mathf.Abs(x - c), az = Mathf.Abs(z - c), ty = endPortalY;
      if (ax > 3 || az > 3 || (ax == 3 && az == 3)) return;
      bool rim = ax == 3 || az == 3 || (ax == 2 && az == 2);
      if (rim) { col[ty] = (byte)SbVox.Bedrock; return; }
      if (ax == 0 && az == 0) { for (int y = ty - 1; y < ty + 4; y++) col[y] = (byte)SbVox.Bedrock; return; }
      // the basin: a bedrock floor, open above (the portal surface is drawn there)
      col[ty - 1] = (byte)SbVox.Bedrock;
      for (int y = ty; y < ty + 4; y++) col[y] = 0;
    }

    private void BuildEnd()
    {
      if (end != null) { end.SetActive(true); return; }
      EnsureEndDim();
      int n = EndSize, c = n / 2;
      int[,] top = endTop;
      float s = EndBlock;
      end = new GameObject("Unknowns_SandBoxEnd");
      end.transform.position = EndOrigin;
      var pale = new Material(SbMats.BlockLit) { hideFlags = HideFlags.DontSave };
      if (pale.HasProperty("_BaseColor")) pale.SetColor("_BaseColor", new Color(0.78f, 0.76f, 0.86f));
      EnsureLayers();
      endDim.Root = end.transform;
      endDim.Rock = pale;
      endDim.Layer = solidLayer;
      endDim.BuildRange(0, 0, (n - 1) / SbVoxelDim.N, (n - 1) / SbVoxelDim.N);

      // the exit portal's surface in the basin (the bedrock is part of the blocks),
      // drawn like Minecraft's portal
      int ty = endPortalY;
      var exitQuads = new List<Vector3[]>();
      for (int x = -2; x <= 2; x++)
        for (int z = -2; z <= 2; z++)
        {
          int ax = Mathf.Abs(x), az = Mathf.Abs(z);
          if ((ax == 2 && az == 2) || (x == 0 && z == 0) || ax > 2 || az > 2) continue;
          Vector3 centre = EndOrigin + new Vector3((c + x + 0.5f) * s, (ty + 0.3f) * s, (c + z + 0.5f) * s);
          exitQuads.Add(SbEndPortalArt.Rect(centre, s, s));
        }
      var exit = new GameObject("exit portal");
      exit.transform.SetParent(end.transform, false);
      SbEndPortalArt.Build(exit.transform, exitQuads);
      Vector3 exitCentre = EndOrigin + new Vector3((c + 0.5f) * s, ty * s + 0.6f, (c + 0.5f) * s);
      // the 3 x 3 basin, from just below the portal surface to above your head
      endExit = new Bounds(exitCentre, new Vector3(3f * s, 1.6f, 3f * s));
      var el = new GameObject("exit light").AddComponent<Light>();
      el.transform.SetParent(end.transform, false);
      el.transform.position = exitCentre + Vector3.up * s;
      el.type = LightType.Point; el.color = new Color(0.4f, 1f, 0.8f); el.range = 6f; el.intensity = 1.2f; el.shadows = LightShadows.None;

      // end crystals on the pillars: , breakable ones for the dragon fight (SandBoxBosses.cs)
      endCrystalSpots.Clear();
      foreach (EndPillar pl in EndPillars())
      {
        Vector3 at = EndOrigin + new Vector3((pl.x + 0.5f) * s, (top[Mathf.Clamp(pl.x, 0, n - 1), Mathf.Clamp(pl.z, 0, n - 1)] + 1.6f) * s, (pl.z + 0.5f) * s);
        endCrystalSpots.Add(at);
      }

      // the sky: almost black with a purple tint, and stars
      var sky = GameObject.CreatePrimitive(PrimitiveType.Sphere);
      sky.name = "end sky";
      var sc = sky.GetComponent<Collider>(); if (sc != null) Destroy(sc);
      sky.transform.SetParent(end.transform, false);
      sky.transform.localPosition = new Vector3(n * s * 0.5f, EndTop * s, n * s * 0.5f);
      sky.transform.localScale = Vector3.one * n * s * 2.2f;
      var skyMat = new Material(SbMats.Line) { hideFlags = HideFlags.DontSave, color = new Color(0.06f, 0.035f, 0.08f, 1f), renderQueue = 2900 };
      var skr = sky.GetComponent<Renderer>();
      skr.sharedMaterial = skyMat;
      skr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
      ParticleSystem stars = SbFx.Burst(sky.transform.position, 1, Color.white, Color.white, 0f, 0f, 0f, 0f, 1f, 1f, 0f, true);
      if (stars != null)
      {
        stars.transform.SetParent(end.transform, true);
        var main = stars.main;
        main.loop = false;
        main.stopAction = ParticleSystemStopAction.None;
        main.startLifetime = 1e6f;
        main.startSpeed = 0f;
        main.startSize = new ParticleSystem.MinMaxCurve(0.15f, 0.4f);
        main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.9f, 0.85f, 1f, 0.8f), new Color(0.6f, 0.5f, 0.9f, 0.5f));
        main.maxParticles = 500;
        main.gravityModifier = 0f;
        var sh = stars.shape; sh.shapeType = ParticleSystemShapeType.Sphere; sh.radius = n * s * 1.0f; sh.radiusThickness = 0f;
        var em = stars.emission; em.rateOverTime = 0f;
        stars.Clear();
        stars.Emit(450);
      }

      // soft pale light from above
      for (int i = 0; i < 4; i++)
      {
        var l = new GameObject("end light").AddComponent<Light>();
        l.transform.SetParent(end.transform, false);
        float a = i / 4f * Mathf.PI * 2f;
        l.transform.localPosition = new Vector3((c + Mathf.Cos(a) * 16f) * s, (EndTop + 18) * s, (c + Mathf.Sin(a) * 16f) * s);
        l.type = LightType.Point; l.color = new Color(0.85f, 0.8f, 1f); l.range = 26f; l.intensity = 1.1f; l.shadows = LightShadows.None;
      }
    }

  }

  // An end crystal: a glowing pink core in two spinning glass cages, bobbing, with a light.
  internal sealed class SbEndCrystal : MonoBehaviour
  {
    private Transform core, cageA, cageB;
    private float t;

    private void Start()
    {
      Material coreMat = new Material(SbMats.Line) { hideFlags = HideFlags.DontSave, color = new Color(1f, 0.45f, 0.85f, 1f) };
      Material cageMat = new Material(SbMats.Line) { hideFlags = HideFlags.DontSave, color = new Color(0.85f, 0.75f, 1f, 0.35f), renderQueue = 3010 };
      core = Part("core", coreMat, 0.32f);
      cageA = Part("cage a", cageMat, 0.6f);
      cageB = Part("cage b", cageMat, 0.78f);
      var l = gameObject.AddComponent<Light>();
      l.type = LightType.Point; l.color = new Color(1f, 0.5f, 0.9f); l.range = 5f; l.intensity = 1.3f; l.shadows = LightShadows.None;
      t = transform.position.x * 1.7f;
    }

    private Transform Part(string name, Material m, float size)
    {
      GameObject g = GameObject.CreatePrimitive(PrimitiveType.Cube);
      g.name = name;
      Collider c = g.GetComponent<Collider>(); if (c != null) Destroy(c);
      g.transform.SetParent(transform, false);
      g.transform.localScale = Vector3.one * size;
      Renderer r = g.GetComponent<Renderer>();
      r.sharedMaterial = m;
      r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
      return g.transform;
    }

    private void Update()
    {
      t += Time.deltaTime;
      float bob = Mathf.Sin(t * 1.6f) * 0.12f;
      if (core != null) { core.localPosition = Vector3.up * bob; core.localRotation = Quaternion.Euler(t * 70f, t * 90f, 0f); }
      if (cageA != null) { cageA.localPosition = Vector3.up * bob; cageA.localRotation = Quaternion.Euler(45f, t * -60f, 35f); }
      if (cageB != null) { cageB.localPosition = Vector3.up * bob; cageB.localRotation = Quaternion.Euler(t * 40f, 30f, t * 50f); }
    }
  }
}

namespace UnknownsSandbox
{
  // the End portal like Minecraft's: the dark End sky with layers of drifting,
  // tinted stars over it (Minecraft's rendertype_end_portal shader: 15 layers of
  // entity/end_portal.png over environment/end_sky.png, each with its own colour, size
  // and drift). Here each layer is a flat sheet with its own material; the texture
  // coordinates come from the world position, so a whole portal reads as one surface.
  internal static class SbEndPortalArt
  {
    private static readonly Color[] Colors =
    {
      new Color(0.022087f, 0.098399f, 0.110818f), new Color(0.011892f, 0.095924f, 0.089485f), new Color(0.027636f, 0.101689f, 0.100326f),
      new Color(0.046564f, 0.109883f, 0.114838f), new Color(0.064901f, 0.117696f, 0.097189f), new Color(0.063761f, 0.086895f, 0.123646f),
      new Color(0.084817f, 0.111994f, 0.166380f), new Color(0.097489f, 0.154120f, 0.091064f), new Color(0.106152f, 0.131144f, 0.195191f),
      new Color(0.097721f, 0.110188f, 0.187229f), new Color(0.133516f, 0.138278f, 0.148582f), new Color(0.070006f, 0.243332f, 0.235792f),
      new Color(0.196766f, 0.142899f, 0.214696f), new Color(0.047281f, 0.315338f, 0.321970f), new Color(0.204675f, 0.390010f, 0.302066f),
      new Color(0.080955f, 0.314821f, 0.661491f)
    };
    private const int Layers = 8;
    private static Material baseMat;
    private static Material[] layerMats;
    private static Texture2D stars, sky;
    private static int tickFrame = -1;

    private static Texture2D Stars()
    {
      if (stars != null) return stars;
      // the stars with their brightness as alpha (so stacking sheets adds light, like the shader)
      Texture2D src = SbMinecraft.Image("textures/entity/end_portal.png");
      int w = src != null ? src.width : 128, h = src != null ? src.height : 128;
      Color32[] px = src != null ? src.GetPixels32() : null;
      var o = new Color32[w * h];
      var rng = new System.Random(77);
      for (int i = 0; i < o.Length; i++)
      {
        Color c;
        if (px != null) c = px[i];
        else { double r = rng.NextDouble(); c = r > 0.985 ? new Color(0.9f, 1f, 0.95f) : r > 0.97 ? new Color(0.4f, 0.7f, 0.8f) : Color.black; }
        float b = Mathf.Max(c.r, Mathf.Max(c.g, c.b));
        o[i] = new Color(b > 0.01f ? c.r / b : 0f, b > 0.01f ? c.g / b : 0f, b > 0.01f ? c.b / b : 0f, b);
      }
      stars = new Texture2D(w, h, TextureFormat.RGBA32, false) { name = "Unknowns End stars", filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Repeat, hideFlags = HideFlags.DontSave };
      stars.SetPixels32(o);
      stars.Apply(false, false);
      return stars;
    }

    private static Texture2D Sky()
    {
      if (sky != null) return sky;
      sky = SbMinecraft.Image("textures/environment/end_sky.png");
      if (sky == null)
      {
        sky = new Texture2D(2, 2) { hideFlags = HideFlags.DontSave };
        sky.SetPixels(new[] { Color.gray, Color.gray, Color.gray, Color.gray });
        sky.Apply();
      }
      sky.wrapMode = TextureWrapMode.Repeat;
      return sky;
    }

    internal static void Reset()
    {
      stars = null; sky = null;
      if (baseMat != null) baseMat.mainTexture = Sky();
      if (layerMats != null) foreach (Material m in layerMats) if (m != null) m.mainTexture = Stars();
    }

    private static void EnsureMats()
    {
      if (baseMat != null) return;
      baseMat = new Material(SbMats.Line) { name = "Unknowns End portal sky", hideFlags = HideFlags.DontSave, renderQueue = 3000 };
      baseMat.mainTexture = Sky();
      // the sky alone is far too bright: Minecraft scales it by its first colour
      Color c0 = Colors[0] * 2.2f; c0.a = 1f;
      baseMat.color = c0;
      layerMats = new Material[Layers];
      for (int i = 0; i < Layers; i++)
      {
        var m = new Material(SbMats.Line) { name = "Unknowns End portal stars " + i, hideFlags = HideFlags.DontSave, renderQueue = 3001 + i };
        m.mainTexture = Stars();
        // later layers are brighter and bigger (the shader's colour table, scaled up for blending)
        Color c = Colors[Mathf.Min(Colors.Length - 1, i * 2)];
        float peak = Mathf.Max(c.r, Mathf.Max(c.g, c.b));
        m.color = new Color(c.r / peak, c.g / peak, c.b / peak, Mathf.Clamp01(0.35f + peak * 1.4f));
        float scale = (4.5f - (i + 1) / 4f) * 0.35f;
        m.mainTextureScale = new Vector2(1f / scale, 1f / scale);
        layerMats[i] = m;
      }
    }

    // the sheets drift: Minecraft's translate (17 / layer, (2 + layer / 1.5) * time) slowed down
    internal static void Tick()
    {
      if (layerMats == null || tickFrame == Time.frameCount) return;
      tickFrame = Time.frameCount;
      float t = Time.time * 0.012f;
      for (int i = 0; i < layerMats.Length; i++)
      {
        float layer = i + 1;
        float a = (layer * layer * 4321f + layer * 9f) * 2f * Mathf.Deg2Rad;
        Vector2 drift = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * (2f + layer / 1.5f) * t;
        layerMats[i].mainTextureOffset = new Vector2(17f / layer, 0f) + drift;
      }
    }

    // flat sheets over these world rectangles (corners a, b, c, d), as children of parent
    internal static GameObject Build(Transform parent, List<Vector3[]> quads)
    {
      EnsureMats();
      var root = new GameObject("end portal surface");
      root.transform.SetParent(parent, false);
      root.AddComponent<SbEndPortalTicker>();
      for (int layer = -1; layer < Layers; layer++)
      {
        var v = new List<Vector3>(); var uv = new List<Vector2>(); var n = new List<Vector3>(); var t = new List<int>();
        float lift = 0.0015f * (layer + 2);
        foreach (Vector3[] q in quads)
        {
          int i = v.Count;
          for (int k = 0; k < 4; k++)
          {
            Vector3 w = q[k] + Vector3.up * lift;
            v.Add(root.transform.InverseTransformPoint(w));
            // world-anchored, each layer turned a different way (the shader's rotation)
            float ang = layer < 0 ? 0f : (layer + 1) * 37f * Mathf.Deg2Rad;
            float ca = Mathf.Cos(ang), sa = Mathf.Sin(ang);
            uv.Add(new Vector2(w.x * ca - w.z * sa, w.x * sa + w.z * ca) * (layer < 0 ? 0.5f : 1f));
            n.Add(Vector3.up);
          }
          t.Add(i); t.Add(i + 2); t.Add(i + 1); t.Add(i); t.Add(i + 3); t.Add(i + 2);
        }
        var mesh = new Mesh { name = "Unknowns End portal layer", hideFlags = HideFlags.DontSave };
        mesh.SetVertices(v); mesh.SetUVs(0, uv); mesh.SetNormals(n); mesh.SetTriangles(t, 0);
        mesh.RecalculateBounds();
        var g = new GameObject(layer < 0 ? "sky" : "stars " + layer);
        g.transform.SetParent(root.transform, false);
        g.AddComponent<MeshFilter>().sharedMesh = mesh;
        var mr = g.AddComponent<MeshRenderer>();
        mr.sharedMaterial = layer < 0 ? baseMat : layerMats[layer];
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = false;
      }
      return root;
    }

    internal static Vector3[] Rect(Vector3 centre, float sx, float sz)
    {
      float hx = sx * 0.5f, hz = sz * 0.5f;
      return new[] { centre + new Vector3(-hx, 0f, -hz), centre + new Vector3(hx, 0f, -hz), centre + new Vector3(hx, 0f, hz), centre + new Vector3(-hx, 0f, hz) };
    }
  }

  internal sealed class SbEndPortalTicker : MonoBehaviour
  {
    private void Update() { SbEndPortalArt.Tick(); }
    private void OnDestroy()
    {
      foreach (MeshFilter mf in GetComponentsInChildren<MeshFilter>(true)) if (mf.sharedMesh != null) Destroy(mf.sharedMesh);
    }
  }
}
