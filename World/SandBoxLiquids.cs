using System;
using System.Collections.Generic;
using GorillaLocomotion;
using UnityEngine;

namespace UnknownsSandbox
{
  // Minecraft-style liquids on the block grid. Every liquid cell has an amount
  // (8 = source/falling, then one less per block for water, two less for lava),
  // falls straight down when it can, spreads sideways only when it is standing
  // on something, and dries up when its source goes. Water ticks every 0.25 s
  // (Minecraft: 5 game ticks), lava every 1.5 s (30 ticks). Every client runs the
  // same rules from the same sources, so only the sources are sent.
  internal sealed partial class SandBoxWorld
  {
    private readonly Dictionary<long, SbFluidCell> fluids = new Dictionary<long, SbFluidCell>();
    private readonly HashSet<long> pendWater = new HashSet<long>();
    private readonly HashSet<long> pendLava = new HashSet<long>();
    private readonly List<long> stepKeys = new List<long>();
    private readonly HashSet<int> fluidSizes = new HashSet<int>();
    private float waterTimer, lavaTimer;

    private sealed class FluidMesh
    {
      public GameObject go;
      public Mesh mesh;
      public bool dirty;
    }
    private readonly Dictionary<int, FluidMesh> fluidMeshes = new Dictionary<int, FluidMesh>();
    private Material waterMat, lavaMat;

    private static readonly Vector3Int[] Dirs6 =
    {
      new Vector3Int(1, 0, 0), new Vector3Int(-1, 0, 0), new Vector3Int(0, 0, 1), new Vector3Int(0, 0, -1),
      new Vector3Int(0, 1, 0), new Vector3Int(0, -1, 0)
    };

    private static long K(int s, Vector3Int c) { return Pack(s, c.x, c.y, c.z); }

    private SbFluidCell FluidAtCell(int s, Vector3Int c)
    {
      SbFluidCell f;
      return fluids.TryGetValue(K(s, c), out f) ? f : null;
    }

    private void MarkFluidDirty(int s, SbFluid t)
    {
      int key = s * 2 + (t == SbFluid.Lava ? 1 : 0);
      FluidMesh fm;
      if (!fluidMeshes.TryGetValue(key, out fm)) { fm = new FluidMesh(); fluidMeshes[key] = fm; }
      fm.dirty = true;
    }

    private void Schedule(int s, Vector3Int c, SbFluid t)
    {
      (t == SbFluid.Lava ? pendLava : pendWater).Add(K(s, c));
    }

    internal void ScheduleFluidAround(int s, Vector3Int c)
    {
      if (!fluidSizes.Contains(s)) return;
      Schedule(s, c, SbFluid.Water); Schedule(s, c, SbFluid.Lava);
      foreach (Vector3Int d in Dirs6) { Schedule(s, c + d, SbFluid.Water); Schedule(s, c + d, SbFluid.Lava); }
    }

    private void SetFluidSource(SbObject o, bool on)
    {
      int s = o.SizeIdx;
      long k = K(s, o.Cell);
      SbFluid t = o.Item.Fluid;
      if (on)
      {
        fluidSizes.Add(s);
        SbFluidCell cur;
        if (fluids.TryGetValue(k, out cur) && cur.Type != t)
        {
          // dropping one liquid straight onto the other
          React(s, o.Cell, cur.Type == SbFluid.Lava ? cur : null, true);
          if (grid.ContainsKey(k)) return; // the lava set into stone right there
        }
        fluids[k] = new SbFluidCell { Type = t, Amount = 8, Source = true, SourceObj = o };
        MarkFluidDirty(s, t);
        ScheduleFluidAround(s, o.Cell);
        CheckReactionsAround(s, o.Cell);
      }
      else
      {
        SbFluidCell cur;
        if (fluids.TryGetValue(k, out cur) && cur.SourceObj == o)
        {
          cur.Source = false;
          cur.SourceObj = null;
          ScheduleFluidAround(s, o.Cell);
          MarkFluidDirty(s, cur.Type);
        }
      }
    }

    private SbFluidCell Desired(int s, Vector3Int c, SbFluid t, SbFluidCell cur)
    {
      if (cur != null && cur.Type != t) return cur;
      if (cur != null && cur.Source && cur.SourceObj != null) return cur;
      if (CellSolid(s, c)) return null;
      int drop = t == SbFluid.Lava ? 2 : 1;
      SbFluidCell above = FluidAtCell(s, c + Vector3Int.up);
      if (above != null && above.Type == t) return new SbFluidCell { Type = t, Amount = 8, Falling = true };
      int best = 0, sources = 0;
      for (int i = 0; i < 4; i++)
      {
        Vector3Int nc = c + Dirs6[i];
        SbFluidCell n = FluidAtCell(s, nc);
        if (n == null || n.Type != t) continue;
        if (n.Source) sources++;
        Vector3Int nb = nc + Vector3Int.down;
        SbFluidCell nbf = FluidAtCell(s, nb);
        bool supported = (nbf != null && nbf.Type == t) || CellSolid(s, nb);
        if (!supported) continue;
        int a = (n.Source || n.Falling) ? 8 : n.Amount;
        best = Mathf.Max(best, a - drop);
      }
      if (t == SbFluid.Water && sources >= 2)
      {
        SbFluidCell below = FluidAtCell(s, c + Vector3Int.down);
        if (CellSolid(s, c + Vector3Int.down) || (below != null && below.Type == SbFluid.Water && below.Source))
          return new SbFluidCell { Type = t, Amount = 8, Source = true };
      }
      if (cur != null && cur.Source && cur.SourceObj == null && t == SbFluid.Water && sources >= 1) return cur;
      if (best <= 0) return null;
      return new SbFluidCell { Type = t, Amount = best };
    }

    private static bool SameFluid(SbFluidCell a, SbFluidCell b)
    {
      if (a == null || b == null) return a == b;
      return a.Type == b.Type && a.Amount == b.Amount && a.Source == b.Source && a.Falling == b.Falling && a.SourceObj == b.SourceObj;
    }

    private void StepFluid(SbFluid t)
    {
      HashSet<long> pend = t == SbFluid.Lava ? pendLava : pendWater;
      if (pend.Count == 0) return;
      stepKeys.Clear();
      stepKeys.AddRange(pend);
      pend.Clear();
      var changes = new List<KeyValuePair<long, SbFluidCell>>();
      int budget = 1500;
      foreach (long k in stepKeys)
      {
        if (budget-- <= 0) { pend.Add(k); continue; }
        int s, x, y, z;
        Unpack(k, out s, out x, out y, out z);
        var c = new Vector3Int(x, y, z);
        SbFluidCell cur;
        fluids.TryGetValue(k, out cur);
        SbFluidCell want = Desired(s, c, t, cur);
        if (!SameFluid(cur, want)) changes.Add(new KeyValuePair<long, SbFluidCell>(k, want));
      }
      foreach (var ch in changes)
      {
        int s, x, y, z;
        Unpack(ch.Key, out s, out x, out y, out z);
        var c = new Vector3Int(x, y, z);
        SbFluidCell cur;
        bool had = fluids.TryGetValue(ch.Key, out cur);
        if (ch.Value == null)
        {
          if (had && cur.Type == t) fluids.Remove(ch.Key);
        }
        else
        {
          if (!had && fluids.Count >= MaxFluidCells) continue;
          fluids[ch.Key] = ch.Value;
        }
        MarkFluidDirty(s, t);
        Schedule(s, c, t);
        foreach (Vector3Int d in Dirs6) Schedule(s, c + d, t);
        if (ch.Value != null) CheckReactionsAround(s, c);
      }
    }

    // Water touching lava: a lava source turns to obsidian, flowing lava to cobblestone.
    private void CheckReactionsAround(int s, Vector3Int c)
    {
      SbFluidCell me = FluidAtCell(s, c);
      if (me == null) return;
      foreach (Vector3Int d in Dirs6)
      {
        SbFluidCell n = FluidAtCell(s, c + d);
        if (n == null || n.Type == me.Type) continue;
        if (me.Type == SbFluid.Lava) { React(s, c, me, false); return; }
        React(s, c + d, n, false);
      }
    }

    private void React(int s, Vector3Int lavaCell, SbFluidCell lava, bool _)
    {
      if (lava == null || lava.Type != SbFluid.Lava) return;
      long k = K(s, lavaCell);
      bool source = lava.Source;
      SbObject src = lava.SourceObj;
      fluids.Remove(k);
      MarkFluidDirty(s, SbFluid.Lava);
      if (src != null) Remove(src, false, false);
      SbItem item = SbCatalog.Get(source ? "mc_obsidian" : "mc_cobble");
      if (item != null && !grid.ContainsKey(k))
        Create(-1, nextGenId++, item, CellCenter(lavaCell, BlockSizes[s]), Quaternion.identity, 1f, true, s, lavaCell);
      SbFx.PlayMc("fizz", SbSound.Sizzle, CellCenter(lavaCell, BlockSizes[s]), 0.8f);
      SbFx.Burst(CellCenter(lavaCell, BlockSizes[s]), 10, new Color(0.8f, 0.8f, 0.8f, 0.7f), new Color(0.5f, 0.5f, 0.5f, 0.6f), 0.3f, 0.8f, 0.1f, 0.25f, 0.8f, 1.5f, -0.2f, false, 0.1f, null, 0f, 0f, 1.6f, true);
      ScheduleFluidAround(s, lavaCell);
    }

    private void Soak(SbObject sponge)
    {
      Vector3 c = sponge.Center;
      float r = 3.5f * sponge.BlockSize;
      var remove = new List<long>();
      foreach (var kv in fluids)
      {
        if (kv.Value.Type != SbFluid.Water) continue;
        int s, x, y, z;
        Unpack(kv.Key, out s, out x, out y, out z);
        if ((CellCenter(new Vector3Int(x, y, z), BlockSizes[s]) - c).sqrMagnitude <= r * r) remove.Add(kv.Key);
      }
      foreach (long k in remove)
      {
        SbFluidCell f = fluids[k];
        int s, x, y, z;
        Unpack(k, out s, out x, out y, out z);
        fluids.Remove(k);
        if (f.SourceObj != null) Remove(f.SourceObj, false, false);
        MarkFluidDirty(s, SbFluid.Water);
        ScheduleFluidAround(s, new Vector3Int(x, y, z));
      }
      if (remove.Count > 0) SbFx.Play(SbSound.Splash, c, 0.6f, 1.3f);
    }

    private void UpdateFluids(float dt)
    {
      waterTimer += dt;
      lavaTimer += dt;
      if (waterTimer >= 0.25f) { waterTimer = 0f; StepFluid(SbFluid.Water); }
      if (lavaTimer >= 1.5f) { lavaTimer = 0f; StepFluid(SbFluid.Lava); }
      foreach (var kv in fluidMeshes)
        if (kv.Value.dirty) { kv.Value.dirty = false; RebuildFluidMesh(kv.Key / 2, kv.Key % 2 == 1 ? SbFluid.Lava : SbFluid.Water, kv.Value); }
      float t = Time.time;
      if (waterMat != null) SetOffset(waterMat, new Vector2(t * 0.05f, -t * 0.35f));
      if (lavaMat != null) SetOffset(lavaMat, new Vector2(t * 0.02f, -t * 0.08f));
    }

    private static void SetOffset(Material m, Vector2 o)
    {
      if (m.HasProperty("_BaseMap")) m.SetTextureOffset("_BaseMap", o);
      if (m.HasProperty("_MainTex")) m.SetTextureOffset("_MainTex", o);
    }

    private Material FluidMaterial(SbFluid t)
    {
      if (t == SbFluid.Water && waterMat != null) return waterMat;
      if (t == SbFluid.Lava && lavaMat != null) return lavaMat;
      int tile = t == SbFluid.Water ? SbBlockArt.Water : SbBlockArt.Lava;
      Rect r = SbBlockArt.TileRect(tile);
      var tex = new Texture2D(16, 16, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Point, hideFlags = HideFlags.DontSave, name = "Unknowns SandBox " + t };
      var px = new Color32[256];
      for (int y = 0; y < 16; y++)
        for (int x = 0; x < 16; x++)
          px[y * 16 + x] = SbBlockArt.Sample(new Vector2(r.x + (x + 0.5f) / 16f * r.width, r.y + (y + 0.5f) / 16f * r.height));
      tex.SetPixels32(px);
      tex.Apply(false, false);
      Material m = new Material(t == SbFluid.Water ? SbMats.Water : SbMats.Lava) { hideFlags = HideFlags.DontSave };
      if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", tex);
      if (m.HasProperty("_MainTex")) m.SetTexture("_MainTex", tex);
      if (t == SbFluid.Water) waterMat = m; else lavaMat = m;
      return m;
    }

    private float FluidHeight(SbFluidCell f, int s, Vector3Int c)
    {
      SbFluidCell above = FluidAtCell(s, c + Vector3Int.up);
      if (above != null && above.Type == f.Type) return 1f;
      if (f.Falling) return 0.9f;
      if (f.Source) return 0.88f;
      return Mathf.Clamp(f.Amount / 9f, 0.12f, 0.86f);
    }

    private void RebuildFluidMesh(int s, SbFluid t, FluidMesh fm)
    {
      if (fm.go == null)
      {
        fm.go = new GameObject("SB " + t + " " + s);
        fm.go.transform.SetParent(objRoot, false);
        fm.go.AddComponent<MeshFilter>();
        var mr = fm.go.AddComponent<MeshRenderer>();
        mr.sharedMaterial = FluidMaterial(t);
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        fm.mesh = new Mesh { name = "SB fluid", hideFlags = HideFlags.DontSave, indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
        fm.go.GetComponent<MeshFilter>().sharedMesh = fm.mesh;
      }
      var v = new List<Vector3>(); var n = new List<Vector3>(); var uv = new List<Vector2>(); var tris = new List<int>();
      float size = BlockSizes[s];
      foreach (var kv in fluids)
      {
        SbFluidCell f = kv.Value;
        if (f.Type != t) continue;
        int ss, x, y, z;
        Unpack(kv.Key, out ss, out x, out y, out z);
        if (ss != s) continue;
        if (!ShowOthers && f.SourceObj != null && !f.SourceObj.Mine && !f.SourceObj.Generated) { }
        var c = new Vector3Int(x, y, z);
        float h = FluidHeight(f, s, c);
        Vector3 b = new Vector3(x * size, y * size, z * size);
        for (int d = 0; d < 6; d++)
        {
          Vector3Int dir = Dirs6[d];
          SbFluidCell nb = FluidAtCell(s, c + dir);
          if (nb != null && nb.Type == t)
          {
            if (d == 4 || d == 5) continue;
            if (FluidHeight(nb, s, c + dir) >= h - 0.001f) continue;
          }
          if (d == 5 && CellSolid(s, c + dir)) continue;
          AddFluidFace(v, n, uv, tris, b, size, h, d);
        }
      }
      fm.mesh.Clear();
      fm.mesh.SetVertices(v);
      fm.mesh.SetNormals(n);
      fm.mesh.SetUVs(0, uv);
      fm.mesh.SetTriangles(tris, 0);
      fm.mesh.RecalculateBounds();
      fm.go.SetActive(v.Count > 0);
    }

    private static void AddFluidFace(List<Vector3> v, List<Vector3> n, List<Vector2> uv, List<int> t, Vector3 b, float s, float h, int d)
    {
      Vector3 p0, p1, p2, p3, nn;
      float top = h * s;
      switch (d)
      {
        case 0: p0 = b + new Vector3(s, 0, 0); p1 = b + new Vector3(s, 0, s); p2 = b + new Vector3(s, top, s); p3 = b + new Vector3(s, top, 0); nn = Vector3.right; break;
        case 1: p0 = b + new Vector3(0, 0, s); p1 = b + new Vector3(0, 0, 0); p2 = b + new Vector3(0, top, 0); p3 = b + new Vector3(0, top, s); nn = Vector3.left; break;
        case 2: p0 = b + new Vector3(s, 0, s); p1 = b + new Vector3(0, 0, s); p2 = b + new Vector3(0, top, s); p3 = b + new Vector3(s, top, s); nn = Vector3.forward; break;
        case 3: p0 = b + new Vector3(0, 0, 0); p1 = b + new Vector3(s, 0, 0); p2 = b + new Vector3(s, top, 0); p3 = b + new Vector3(0, top, 0); nn = Vector3.back; break;
        case 4: p0 = b + new Vector3(0, top, 0); p1 = b + new Vector3(s, top, 0); p2 = b + new Vector3(s, top, s); p3 = b + new Vector3(0, top, s); nn = Vector3.up; break;
        default: p0 = b + new Vector3(0, 0, s); p1 = b + new Vector3(s, 0, s); p2 = b + new Vector3(s, 0, 0); p3 = b + new Vector3(0, 0, 0); nn = Vector3.down; break;
      }
      int i = v.Count;
      v.Add(p0); v.Add(p1); v.Add(p2); v.Add(p3);
      for (int k = 0; k < 4; k++) n.Add(nn);
      // world-aligned UVs: one texture repeat per block
      foreach (Vector3 p in new[] { p0, p1, p2, p3 })
      {
        if (d >= 4) uv.Add(new Vector2(p.x / s, p.z / s));
        else if (d <= 1) uv.Add(new Vector2(p.z / s, p.y / s));
        else uv.Add(new Vector2(p.x / s, p.y / s));
      }
      Vector3 face = Vector3.Cross(p1 - p0, p2 - p0);
      if (Vector3.Dot(face, nn) >= 0f) { t.Add(i); t.Add(i + 1); t.Add(i + 2); t.Add(i); t.Add(i + 2); t.Add(i + 3); }
      else { t.Add(i); t.Add(i + 2); t.Add(i + 1); t.Add(i); t.Add(i + 3); t.Add(i + 2); }
    }

    // ================================================================ falling sand / gravel
    private readonly List<SbObject> fallers = new List<SbObject>();

    private void CheckFalling(SbObject o)
    {
      if (o == null || o.Item.BlockFx != SbBlockFx.Falls || o.Falling || o.Primed || o.T == null) return;
      if (CellSolid(o.SizeIdx, o.Cell + Vector3Int.down)) return;
      RemoveFromGrid(o);
      o.Falling = true;
      o.FallVel = 0f;
      Vector3Int target = o.Cell;
      for (int i = 0; i < 256; i++)
      {
        Vector3Int below = target + Vector3Int.down;
        if (CellSolid(o.SizeIdx, below)) break;
        target = below;
      }
      o.FallTarget = target;
      fallers.Add(o);
      ScheduleFluidAround(o.SizeIdx, o.Cell);
      CheckFallingAbove(o.SizeIdx, o.Cell);
    }

    internal void CheckFallingAbove(int s, Vector3Int c)
    {
      SbObject above;
      if (grid.TryGetValue(K(s, c + Vector3Int.up), out above)) CheckFalling(above);
    }

    private void UpdateFalling(float dt)
    {
      for (int i = fallers.Count - 1; i >= 0; i--)
      {
        SbObject o = fallers[i];
        if (o == null || o.T == null || !objects.ContainsKey(o.Key)) { fallers.RemoveAt(i); continue; }
        float s = o.BlockSize;
        o.FallVel += 9.8f * dt;
        Vector3 p = o.T.position;
        float targetY = CellCenter(o.FallTarget, s).y;
        p.y -= o.FallVel * dt;
        if (p.y <= targetY)
        {
          Vector3Int cell = o.FallTarget;
          while (grid.ContainsKey(K(o.SizeIdx, cell))) cell += Vector3Int.up;
          o.Cell = cell;
          o.Falling = false;
          o.T.position = CellCenter(cell, s);
          AddToGrid(o);
          ScheduleFluidAround(o.SizeIdx, cell);
          SbFx.Play(SbSound.Place, o.T.position, 0.5f, 0.8f);
          fallers.RemoveAt(i);
          if (!CellSolid(o.SizeIdx, cell + Vector3Int.down)) CheckFalling(o);
          continue;
        }
        o.T.position = p;
      }
    }

    // ================================================================ swimming
    internal bool InWater { get; private set; }
    internal bool InLava { get; private set; }
    private bool headInFluid;
    private SbFluid headFluid;
    private Vector3 lastRelL, lastRelR;
    private bool haveRel;
    private GameObject tintQuad;
    private Material tintMat;
    private float nextSizzle;
    private bool wasInWater;

    private bool FluidAt(Vector3 p, out SbFluid type)
    {
      type = SbFluid.None;
      if (fluids.Count == 0) return false;
      foreach (int s in fluidSizes)
      {
        float size = BlockSizes[s];
        Vector3Int c = CellOf(p, size);
        SbFluidCell f = FluidAtCell(s, c);
        if (f == null) continue;
        float top = (c.y + FluidHeight(f, s, c)) * size;
        if (p.y <= top) { type = f.Type; return true; }
      }
      return false;
    }

    private void FixedUpdate()
    {
      try { ApplyAttachForces(); } catch (Exception ex) { SbLog.Warn("attachments: " + ex.Message); }
      try { ApplyDriving(); } catch (Exception ex) { SbLog.Warn("driving: " + ex.Message); }
      try { ApplyMcMovement(); } catch (Exception ex) { SbLog.Warn("slow fall / elytra: " + ex.Message); }
      InWater = InLava = false;
      headInFluid = false;
      if (fluids.Count == 0) { haveRel = false; return; }
      GTPlayer p;
      try { p = GTPlayer.Instance; } catch { return; }
      if (p == null || p.bodyCollider == null || p.headCollider == null) return;
      Vector3 body = p.bodyCollider.transform.position;
      Vector3 head = p.headCollider.transform.position;
      SbFluid bt, ht;
      bool bodyIn = FluidAt(body, out bt);
      bool headIn = FluidAt(head, out ht);
      headInFluid = headIn; headFluid = ht;
      SbFluid t = bodyIn ? bt : ht;
      InWater = (bodyIn || headIn) && t == SbFluid.Water;
      InLava = (bodyIn || headIn) && t == SbFluid.Lava;
      if (!(bodyIn || headIn) || !SolidAllowed) { haveRel = false; return; }

      Rigidbody rb = p.playerRigidBody;
      if (rb == null) return;
      float dt = Time.fixedDeltaTime;
      float g = Physics.gravity.magnitude * p.scale;
      float sub = headIn ? 1f : 0.7f;
      bool water = t == SbFluid.Water;
      rb.AddForce(Vector3.up * g * (water ? 1.06f : 0.98f) * sub, ForceMode.Acceleration);
      float drag = water ? 2.0f : 5.5f;
      rb.linearVelocity *= Mathf.Exp(-drag * dt);

      // arm strokes push you through the liquid
      Transform lh = HandProvider != null ? HandProvider(false) : null;
      Transform rh = HandProvider != null ? HandProvider(true) : null;
      if (lh != null && rh != null)
      {
        Vector3 relL = lh.position - head, relR = rh.position - head;
        if (haveRel)
        {
          SbFluid ft;
          float k = water ? 9f : 3f;
          if (FluidAt(lh.position, out ft)) rb.AddForce(Vector3.ClampMagnitude(-(relL - lastRelL) / dt, 6f) * k, ForceMode.Acceleration);
          if (FluidAt(rh.position, out ft)) rb.AddForce(Vector3.ClampMagnitude(-(relR - lastRelR) / dt, 6f) * k, ForceMode.Acceleration);
        }
        lastRelL = relL; lastRelR = relR; haveRel = true;
      }
      if (rb.linearVelocity.magnitude > 7f) rb.linearVelocity = rb.linearVelocity.normalized * 7f;
    }

    private void UpdateSwimVisuals()
    {
      Transform head = HeadProvider != null ? HeadProvider() : null;
      if (InWater && !wasInWater && head != null) SbFx.PlayMc("splash", SbSound.Splash, head.position, 0.8f);
      wasInWater = InWater;
      if (InLava && Time.time >= nextSizzle && head != null)
      {
        nextSizzle = Time.time + 0.7f;
        SbFx.PlayMc("lava", SbSound.Sizzle, head.position, 0.6f);
        SbFx.Burst(head.position + Vector3.down * 0.4f, 8, new Color(1f, 0.6f, 0.2f), new Color(1f, 0.2f, 0.05f), 0.4f, 1.2f, 0.06f, 0.14f, 0.3f, 0.7f, -0.3f, true, 0.25f);
      }
      bool show = headInFluid && head != null;
      if (!show) { if (tintQuad != null && tintQuad.activeSelf) tintQuad.SetActive(false); return; }
      Camera cam = Camera.main;
      if (cam == null) return;
      if (tintQuad == null)
      {
        tintQuad = GameObject.CreatePrimitive(PrimitiveType.Quad);
        tintQuad.name = "SB Underwater";
        var col = tintQuad.GetComponent<Collider>(); if (col != null) Destroy(col);
        tintMat = new Material(SbMats.Line) { hideFlags = HideFlags.DontSave };
        tintMat.renderQueue = 4000;
        var r = tintQuad.GetComponent<Renderer>();
        r.sharedMaterial = tintMat;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
      }
      if (tintQuad.transform.parent != cam.transform)
      {
        tintQuad.transform.SetParent(cam.transform, false);
        tintQuad.transform.localPosition = new Vector3(0f, 0f, Mathf.Max(cam.nearClipPlane * 1.5f, 0.06f));
        tintQuad.transform.localRotation = Quaternion.identity;
        tintQuad.transform.localScale = Vector3.one * 0.6f;
      }
      tintMat.color = headFluid == SbFluid.Lava ? new Color(1f, 0.35f, 0.05f, 0.72f) : new Color(0.12f, 0.3f, 0.8f, 0.38f);
      if (!tintQuad.activeSelf) tintQuad.SetActive(true);
    }
  }
}
