using System;
using System.Collections.Generic;
using UnityEngine;

namespace UnknownsSandbox
{
  // Players joining or leaving the lobby you are in (any lobby).
  // Join: the player is hidden (and any camera copy of them, which copies
  // what the real renderers show) while a wireframe of their body draws in from
  // the feet up behind a glowing scan ring with an accent glow over it; at the
  // top the player appears and the wire fades. The body is baked every frame, so
  // the wire follows their animation. Nothing shows through walls, and no sound.
  // Leave: their last pose is frozen, flashes, and the wire dissolves from the
  // head down behind the ring, as if they were beamed out.
  internal sealed class SbSpawnFx : MonoBehaviour
  {
    private const float JoinReveal = 1.15f;   // the scan from feet to head
    private const float JoinLife = 2.3f;
    private const float LeaveFlash = 0.22f;
    private const float LeaveLife = 1.7f;
    private const int MaxEdges = 9000;

    private static Material wireMat, chamsMat;
    private static readonly Dictionary<VRRig, SbSpawnFx> active = new Dictionary<VRRig, SbSpawnFx>();

    private VRRig rig;
    private SkinnedMeshRenderer skin;
    private bool leaving;
    private Mesh baked, wire, chams;
    private int[] edges;
    private readonly List<Vector3> verts = new List<Vector3>();
    private MaterialPropertyBlock wireBlock, chamsBlock;
    private Renderer wireR, chamsR;
    private readonly List<Renderer> hiddenRenderers = new List<Renderer>();
    private bool rigShown = true;
    private LineRenderer ring;
    private float minH, maxH, t, rigScale = 1f;
    private Color accent, accent2;
    private bool burstDone;

    internal static void Play(VRRig rig, Color accent, Color accent2) { Begin(rig, accent, accent2, false); }

    // call before the game takes the rig away (NetworkSystem.PlayerLeft prefix)
    internal static void PlayLeave(VRRig rig, Color accent, Color accent2) { Begin(rig, accent, accent2, true); }

    private static void Begin(VRRig rig, Color accent, Color accent2, bool leave)
    {
      if (rig == null) return;
      SbSpawnFx running;
      if (active.TryGetValue(rig, out running))
      {
        if (!leave) return;
        if (running != null) Destroy(running.gameObject);   // left mid-join: the leave takes over
        active.Remove(rig);
      }
      try
      {
        // a leaving rig may already be switched off: its last pose is still there
        if (!leave && !rig.gameObject.activeInHierarchy) return;
        SkinnedMeshRenderer smr = VisibleBody(rig);
        if (smr == null && leave && rig.mainSkin != null && rig.mainSkin.sharedMesh != null) smr = rig.mainSkin;
        if (smr == null) return;
        var go = new GameObject(leave ? "Unknowns Player Leave" : "Unknowns Player Join");
        go.transform.SetParent(SbFx.Root, false);
        var fx = go.AddComponent<SbSpawnFx>();
        if (!fx.Build(rig, smr, accent, accent2, leave)) { Destroy(go); return; }
        if (!leave) active[rig] = fx;
      }
      catch (Exception ex) { SbLog.Warn("join/leave effect: " + ex.Message); }
    }

    // the body that is actually drawn (the game can swap mainSkin for another body type)
    private static SkinnedMeshRenderer VisibleBody(VRRig rig)
    {
      SkinnedMeshRenderer s = null;
      try { if (rig.bodyRenderer != null) s = rig.bodyRenderer.GetBody(GorillaBodyType.Default); } catch { }
      if (Usable(s)) return s;
      if (Usable(rig.mainSkin)) return rig.mainSkin;
      SkinnedMeshRenderer best = null;
      foreach (SkinnedMeshRenderer r in rig.GetComponentsInChildren<SkinnedMeshRenderer>())
        if (Usable(r) && (best == null || r.sharedMesh.vertexCount > best.sharedMesh.vertexCount)) best = r;
      return best ?? (rig.mainSkin != null && rig.mainSkin.sharedMesh != null ? rig.mainSkin : null);
    }

    private static bool Usable(SkinnedMeshRenderer r)
    {
      return r != null && r.sharedMesh != null && r.enabled && r.gameObject.activeInHierarchy;
    }

    // ---------------------------------------------------------------- materials
    // Hidden/Internal-Colored takes blend/cull/ztest as properties (it is what GL
    // lines use, so it is always in the build); Sprites/Default is the fallback.
    private static Material MakeMat(string name, bool additive, int zTest, int queue)
    {
      Shader s = Shader.Find("Hidden/Internal-Colored");
      Material m;
      if (s != null && s.isSupported)
      {
        m = new Material(s) { name = name, hideFlags = HideFlags.DontSave };
        m.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        m.SetInt("_DstBlend", additive ? (int)UnityEngine.Rendering.BlendMode.One : (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        m.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off);
        m.SetInt("_ZWrite", 0);
        m.SetInt("_ZTest", zTest);
      }
      else m = new Material(SbMats.Line) { name = name, hideFlags = HideFlags.DontSave };
      m.renderQueue = queue;
      return m;
    }

    private static void EnsureMats()
    {
      if (wireMat == null) wireMat = MakeMat("Unknowns Join Wire", true, (int)UnityEngine.Rendering.CompareFunction.LessEqual, 3120);
      if (chamsMat == null) chamsMat = MakeMat("Unknowns Join Chams", true, (int)UnityEngine.Rendering.CompareFunction.LessEqual, 3110);
    }

    // the real player (every renderer on the rig and their cosmetics) while they spawn in
    private void HideRig()
    {
      var list = new List<Renderer>();
      try
      {
        rig.GetComponentsInChildren(true, list);
        if (rig.cosmetics != null)
          foreach (GameObject go in rig.cosmetics) if (go != null && !go.transform.IsChildOf(rig.transform)) list.AddRange(go.GetComponentsInChildren<Renderer>(true));
        if (rig.overrideCosmetics != null)
          foreach (GameObject go in rig.overrideCosmetics) if (go != null && !go.transform.IsChildOf(rig.transform)) list.AddRange(go.GetComponentsInChildren<Renderer>(true));
      }
      catch { }
      foreach (Renderer r in list)
      {
        if (r == null || r.forceRenderingOff || r.transform.IsChildOf(transform)) continue;
        r.forceRenderingOff = true;
        hiddenRenderers.Add(r);
      }
      rigShown = false;
    }

    private void ShowRig()
    {
      if (rigShown) return;
      rigShown = true;
      foreach (Renderer r in hiddenRenderers) if (r != null) r.forceRenderingOff = false;
      hiddenRenderers.Clear();
    }

    // ---------------------------------------------------------------- build
    private bool Build(VRRig r, SkinnedMeshRenderer smr, Color a, Color a2, bool leave)
    {
      rig = r;
      skin = smr;
      leaving = leave;
      accent = a; accent.a = 1f;
      accent2 = a2; accent2.a = 1f;
      try { rigScale = r.scaleFactor > 0.01f ? r.scaleFactor : 1f; } catch { }
      EnsureMats();

      baked = new Mesh { name = "Unknowns join bake", hideFlags = HideFlags.DontSave };
      baked.MarkDynamic();
      smr.BakeMesh(baked, true);
      int vc = baked.vertexCount;
      if (vc < 3) return false;
      baked.GetVertices(verts);

      // unique triangle edges, sorted bottom to top
      var tris = baked.triangles;
      var seen = new HashSet<long>();
      var list = new List<int>(tris.Length);
      for (int i = 0; i + 2 < tris.Length && list.Count < MaxEdges * 2; i += 3)
      {
        AddEdge(seen, list, tris[i], tris[i + 1]);
        AddEdge(seen, list, tris[i + 1], tris[i + 2]);
        AddEdge(seen, list, tris[i + 2], tris[i]);
      }
      if (list.Count < 2) return false;

      Quaternion rot = smr.transform.rotation;
      var h = new float[vc];
      minH = float.MaxValue; maxH = float.MinValue;
      for (int i = 0; i < vc; i++)
      {
        h[i] = (rot * verts[i]).y;
        if (h[i] < minH) minH = h[i];
        if (h[i] > maxH) maxH = h[i];
      }
      int edgeCount = list.Count / 2;
      var order = new int[edgeCount];
      var key = new float[edgeCount];
      for (int e = 0; e < edgeCount; e++) { order[e] = e; key[e] = Mathf.Min(h[list[e * 2]], h[list[e * 2 + 1]]); }
      Array.Sort(key, order);
      edges = new int[edgeCount * 2];
      for (int e = 0; e < edgeCount; e++) { edges[e * 2] = list[order[e] * 2]; edges[e * 2 + 1] = list[order[e] * 2 + 1]; }

      var white = new Color[vc];
      for (int i = 0; i < vc; i++) white[i] = Color.white;

      wire = new Mesh { name = "Unknowns join wire", hideFlags = HideFlags.DontSave, indexFormat = baked.indexFormat };
      wire.MarkDynamic();
      wire.SetVertices(verts);
      wire.colors = white;
      wire.SetIndices(edges, 0, leaving ? edges.Length : 0, MeshTopology.Lines, 0, false);

      chams = new Mesh { name = "Unknowns join chams", hideFlags = HideFlags.DontSave, indexFormat = baked.indexFormat };
      chams.MarkDynamic();
      chams.SetVertices(verts);
      chams.colors = white;
      chams.triangles = tris;
      chams.RecalculateBounds();
      wire.bounds = chams.bounds;

      wireBlock = new MaterialPropertyBlock();
      chamsBlock = new MaterialPropertyBlock();
      Part("wire", wire, wireMat, out wireR);
      Part("chams", chams, chamsMat, out chamsR);

      var rg = new GameObject("ring");
      rg.transform.SetParent(transform, false);
      ring = rg.AddComponent<LineRenderer>();
      ring.useWorldSpace = true;
      ring.loop = true;
      ring.positionCount = 48;
      ring.sharedMaterial = SbMats.Line;
      ring.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
      ring.numCapVertices = 2;
      ring.enabled = false;

      transform.SetPositionAndRotation(smr.transform.position, smr.transform.rotation);
      if (leaving)
      {
        // a frozen copy: the rig is about to be taken away
        Destroy(baked); baked = null;
      }
      else HideRig();
      Apply(0f);
      return true;
    }

    private static void AddEdge(HashSet<long> seen, List<int> list, int a, int b)
    {
      long k = a < b ? ((long)a << 32) | (uint)b : ((long)b << 32) | (uint)a;
      if (!seen.Add(k)) return;
      list.Add(a); list.Add(b);
    }

    private void Part(string name, Mesh mesh, Material mat, out Renderer r)
    {
      var go = new GameObject(name);
      go.transform.SetParent(transform, false);
      go.AddComponent<MeshFilter>().sharedMesh = mesh;
      var mr = go.AddComponent<MeshRenderer>();
      mr.sharedMaterial = mat;
      mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
      mr.receiveShadows = false;
      r = mr;
    }

    // ---------------------------------------------------------------- animate
    private void Follow()
    {
      Transform st = skin.transform;
      transform.SetPositionAndRotation(st.position, st.rotation);
      skin.BakeMesh(baked, true);
      if (baked.vertexCount != wire.vertexCount) return;
      baked.GetVertices(verts);
      wire.SetVertices(verts);
      chams.SetVertices(verts);
      chams.RecalculateBounds();
      wire.bounds = chams.bounds;
    }

    private static float EaseOut(float x) { x = Mathf.Clamp01(x); return 1f - (1f - x) * (1f - x) * (1f - x); }
    private static float EaseInOut(float x) { x = Mathf.Clamp01(x); return x * x * (3f - 2f * x); }

    private void LateUpdate()
    {
      t += Time.deltaTime;
      if (t >= (leaving ? LeaveLife : JoinLife)) { Destroy(gameObject); return; }
      if (!leaving)
      {
        if (rig == null || skin == null || !rig.gameObject.activeInHierarchy) { Destroy(gameObject); return; }
        try { Follow(); }
        catch { Destroy(gameObject); return; }
      }
      Apply(t);
    }

    private void Apply(float time)
    {
      int edgeCount = edges.Length / 2;
      float wireA, chamsA, ringA, scan;   // scan: 0 = feet, 1 = head
      int shown;
      if (!leaving)
      {
        float reveal = EaseOut(time / JoinReveal);
        scan = reveal;
        shown = Mathf.CeilToInt(edgeCount * reveal);
        wireA = time < JoinReveal ? 1f : 1f - EaseInOut((time - JoinReveal) / (JoinLife - JoinReveal - 0.1f));
        chamsA = time < JoinReveal ? Mathf.Lerp(0.05f, 0.42f, reveal) : 0.42f * (1f - EaseInOut((time - JoinReveal) / 0.8f));
        ringA = time < JoinReveal ? Mathf.Clamp01(time / 0.15f) : 1f - Mathf.Clamp01((time - JoinReveal) / 0.25f);
      }
      else
      {
        // flash, then dissolve from the head down
        float flash = Mathf.Clamp01(time / LeaveFlash);
        float dis = EaseInOut((time - LeaveFlash) / (LeaveLife - LeaveFlash - 0.25f));
        scan = 1f - dis;
        shown = Mathf.CeilToInt(edgeCount * (1f - dis));
        wireA = time < LeaveFlash ? flash : 1f - Mathf.Clamp01((time - (LeaveLife - 0.35f)) / 0.35f);
        chamsA = time < LeaveFlash ? 0.6f * flash : 0.6f * (1f - EaseInOut((time - LeaveFlash) / 0.6f));
        ringA = time < LeaveFlash ? flash : 1f - Mathf.Clamp01((time - (LeaveLife - 0.4f)) / 0.3f);
      }
      chamsA *= 0.88f + 0.12f * Mathf.Sin(time * 20f);
      wire.SetIndices(edges, 0, Mathf.Clamp(shown, 0, edgeCount) * 2, MeshTopology.Lines, 0, false);

      Color wc = Color.Lerp(accent, Color.white, 0.25f); wc.a = Mathf.Clamp01(wireA);
      Color cc = accent; cc.a = Mathf.Clamp01(chamsA);
      wireBlock.SetColor("_Color", wc);
      wireR.SetPropertyBlock(wireBlock);
      chamsBlock.SetColor("_Color", cc);
      chamsR.SetPropertyBlock(chamsBlock);

      ring.enabled = ringA > 0.01f;
      if (ring.enabled)
      {
        Vector3 c = transform.position;
        c.y += Mathf.Lerp(minH, maxH, scan);
        float rad = 0.36f * rigScale * (1f + 0.12f * Mathf.Sin(time * 9f));
        for (int i = 0; i < ring.positionCount; i++)
        {
          float a = i / (float)ring.positionCount * Mathf.PI * 2f;
          ring.SetPosition(i, c + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * rad);
        }
        ring.widthMultiplier = 0.012f * rigScale;
        Color r0 = accent; r0.a = Mathf.Clamp01(ringA);
        Color r1 = accent2; r1.a = Mathf.Clamp01(ringA) * 0.8f;
        ring.startColor = r0;
        ring.endColor = r1;
      }

      float burstAt = leaving ? 0.05f : JoinReveal;
      if (!burstDone && time >= burstAt)
      {
        burstDone = true;
        Vector3 top = transform.position + Vector3.up * maxH;
        Vector3 mid = transform.position + Vector3.up * Mathf.Lerp(minH, maxH, 0.5f);
        SbFx.Burst(leaving ? mid : top, leaving ? 34 : 26, accent, accent2, 0.3f, 1.1f, 0.008f, 0.02f, 0.35f, 0.75f, -0.2f, true, leaving ? 0.25f : 0.12f, Vector3.up, 60f, 2f);
        // the scan reached the head: the player is there
        if (!leaving) ShowRig();
      }
    }

    private void OnDestroy()
    {
      ShowRig();
      SbSpawnFx me;
      if (!leaving && rig != null && active.TryGetValue(rig, out me) && me == this) active.Remove(rig);
      if (baked != null) Destroy(baked);
      if (wire != null) Destroy(wire);
      if (chams != null) Destroy(chams);
    }
  }
}
