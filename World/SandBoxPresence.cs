using System;
using System.Collections.Generic;
using System.IO;
using Photon.Pun;
using Photon.Realtime;
using TMPro;
using UnityEngine;

namespace UnknownsSandbox
{
  // What other Unknowns players see of your SandBox: your tablet (three holo
  // panels in your accent colour, with your name), your pointer and your
  // placement preview. Sent unreliable about 12 times a second while any of them
  // is showing (SbNet Op.Presence); drawn here, smoothed between packets, with the
  // same pop in / fade out as your own.
  internal static class SbPresence
  {
    private const byte Version = 1;
    private const float Stale = 0.8f;       // no packet for this long = hidden
    private const float Forget = 8f;

    [Flags]
    internal enum F : byte { Tablet = 1, Pointer = 2, Ghost = 4, PtrUi = 8, PtrDelete = 16, GhostValid = 32, GhostCell = 64 }

    // ---------------------------------------------------------------- outgoing
    internal struct Out
    {
      public F flags;
      public Color accent, accent2;
      public Vector3 root; public float yaw, size, dist, height;
      public Vector3 from, to; public float press;
      public ushort ghostKey; public Vector3 ghostPos; public Quaternion ghostRot; public float ghostScale;
    }

    internal static byte[] Write(Out o)
    {
      using (var ms = new MemoryStream())
      using (var w = new BinaryWriter(ms))
      {
        w.Write(Version);
        w.Write((byte)o.flags);
        WC(w, o.accent); WC(w, o.accent2);
        if ((o.flags & F.Tablet) != 0) { WV(w, o.root); w.Write(o.yaw); w.Write(o.size); w.Write(o.dist); w.Write(o.height); }
        if ((o.flags & F.Pointer) != 0) { WV(w, o.from); WV(w, o.to); w.Write((byte)Mathf.RoundToInt(Mathf.Clamp01(o.press) * 255f)); }
        if ((o.flags & F.Ghost) != 0) { w.Write(o.ghostKey); WV(w, o.ghostPos); WQ(w, o.ghostRot); w.Write(o.ghostScale); }
        w.Flush();
        return ms.ToArray();
      }
    }

    private static void WC(BinaryWriter w, Color c)
    {
      w.Write((byte)Mathf.RoundToInt(Mathf.Clamp01(c.r) * 255f));
      w.Write((byte)Mathf.RoundToInt(Mathf.Clamp01(c.g) * 255f));
      w.Write((byte)Mathf.RoundToInt(Mathf.Clamp01(c.b) * 255f));
    }
    private static void WV(BinaryWriter w, Vector3 v) => SbNet.WV(w, v);
    private static void WQ(BinaryWriter w, Quaternion q) => SbNet.WQ(w, q);
    private static Color RC(BinaryReader r) { return new Color32(r.ReadByte(), r.ReadByte(), r.ReadByte(), 255); }
    private static Vector3 RV(BinaryReader r) => SbNet.RV(r);
    private static Quaternion RQ(BinaryReader r) => SbNet.RQ(r);
    private static bool Finite(Vector3 v) { return !float.IsNaN(v.x + v.y + v.z) && !float.IsInfinity(v.x + v.y + v.z) && v.sqrMagnitude < 1e8f; }

    // ---------------------------------------------------------------- incoming
    private sealed class Peer
    {
      public int actor;
      public float lastRecv = -10f;
      public F flags;
      public Color accent = Color.white, accent2 = Color.white;
      public Vector3 root, rootT; public float yaw, yawT, size = 0.85f, sizeT = 0.85f, dist = 0.85f, distT = 0.85f, height, heightT;
      public Vector3 from, fromT, to, toT; public float press, pressT;
      public SbItem ghostItem; public Vector3 gPos, gPosT; public Quaternion gRot = Quaternion.identity, gRotT = Quaternion.identity; public float gScale = 1f, gScaleT = 1f;
      public bool tabletSeen, ptrSeen, ghostSeen;
      public float tabletVis, ptrVis, ghostVis;
      public string name = string.Empty;
      public float nameAt;

      public GameObject root0;
      public Transform[] panels;
      public Material[] fillMats;
      public LineRenderer[] borders;
      public TextMeshPro title;
      public LineRenderer core, glow;
      public GameObject halo, gGo;
      public Material haloMat, gMat;
      public MeshFilter gMf;
      public MeshRenderer gMr;
    }

    private static readonly Dictionary<int, Peer> peers = new Dictionary<int, Peer>();
    private static readonly List<int> scratch = new List<int>();
    private static readonly float[] PanelW = { 300f, 640f, 300f };
    private const float PanelH = 560f;

    internal static void Receive(int actor, BinaryReader r)
    {
      if (r.ReadByte() != Version) return;
      Peer p;
      if (!peers.TryGetValue(actor, out p)) { p = new Peer { actor = actor }; peers[actor] = p; }
      F f = (F)r.ReadByte();
      p.accent = RC(r); p.accent2 = RC(r);
      if ((f & F.Tablet) != 0)
      {
        Vector3 root = RV(r); float yaw = r.ReadSingle(), size = r.ReadSingle(), dist = r.ReadSingle(), height = r.ReadSingle();
        if (!Finite(root)) return;
        p.rootT = root; p.yawT = yaw; p.sizeT = Mathf.Clamp(size, 0.1f, 6f); p.distT = Mathf.Clamp(dist, 0.1f, 6f); p.heightT = Mathf.Clamp(height, -3f, 3f);
        if (!p.tabletSeen) { p.root = p.rootT; p.yaw = yaw; p.size = p.sizeT; p.dist = p.distT; p.height = p.heightT; }
      }
      if ((f & F.Pointer) != 0)
      {
        Vector3 a = RV(r), b = RV(r); float press = r.ReadByte() / 255f;
        if (!Finite(a) || !Finite(b)) return;
        p.fromT = a; p.toT = b; p.pressT = press;
        if (!p.ptrSeen) { p.from = a; p.to = b; }
      }
      if ((f & F.Ghost) != 0)
      {
        ushort key = r.ReadUInt16(); Vector3 pos = RV(r); Quaternion rot = RQ(r); float sc = r.ReadSingle();
        if (!Finite(pos)) return;
        SbItem item;
        if (!SbCatalog.ByKey.TryGetValue(key, out item)) f &= ~F.Ghost;
        else
        {
          if (item != p.ghostItem) { p.ghostItem = item; p.ghostVis = 0f; p.ghostSeen = false; }
          p.gPosT = pos; p.gRotT = rot; p.gScaleT = Mathf.Clamp(sc, 0.02f, 30f);
          if (!p.ghostSeen) { p.gPos = pos; p.gRot = rot; p.gScale = p.gScaleT; }
        }
      }
      p.flags = f;
      p.lastRecv = Time.unscaledTime;
    }

    internal static void Clear()
    {
      foreach (Peer p in peers.Values) Destroy(p);
      peers.Clear();
    }

    private static void Destroy(Peer p)
    {
      if (p.root0 != null) UnityEngine.Object.Destroy(p.root0);
      if (p.fillMats != null) foreach (Material m in p.fillMats) if (m != null) UnityEngine.Object.Destroy(m);
      if (p.haloMat != null) UnityEngine.Object.Destroy(p.haloMat);
      if (p.gMat != null) UnityEngine.Object.Destroy(p.gMat);
      p.root0 = null;
    }

    private static float Pop(float x)
    {
      x = Mathf.Clamp01(x);
      const float c1 = 1.4f, c3 = c1 + 1f;
      return 1f + c3 * Mathf.Pow(x - 1f, 3f) + c1 * Mathf.Pow(x - 1f, 2f);
    }

    // ---------------------------------------------------------------- draw
    internal static void Tick(bool showTablet, bool showPointer, bool showGhost, Transform head)
    {
      float now = Time.unscaledTime;
      float dt = Mathf.Min(Time.unscaledDeltaTime, 0.1f);
      if (!PhotonNetwork.InRoom) { if (peers.Count > 0) Clear(); return; }
      scratch.Clear();
      foreach (KeyValuePair<int, Peer> kv in peers)
      {
        Peer p = kv.Value;
        bool fresh = now - p.lastRecv < Stale;
        bool inRoom = false;
        try { inRoom = PhotonNetwork.CurrentRoom != null && PhotonNetwork.CurrentRoom.GetPlayer(p.actor) != null; } catch { }
        if (!inRoom || now - p.lastRecv > Forget) { scratch.Add(kv.Key); continue; }
        bool wantT = fresh && showTablet && (p.flags & F.Tablet) != 0;
        bool wantP = fresh && showPointer && (p.flags & F.Pointer) != 0;
        bool wantG = fresh && showGhost && (p.flags & F.Ghost) != 0 && p.ghostItem != null;
        p.tabletVis = Mathf.MoveTowards(p.tabletVis, wantT ? 1f : 0f, dt / (wantT ? 0.45f : 0.3f));
        p.ptrVis = Mathf.MoveTowards(p.ptrVis, wantP ? 1f : 0f, dt / (wantP ? 0.16f : 0.14f));
        p.ghostVis = Mathf.MoveTowards(p.ghostVis, wantG ? 1f : 0f, dt / (wantG ? 0.22f : 0.14f));
        if (!wantT && p.tabletVis <= 0f) p.tabletSeen = false;
        if (!wantP && p.ptrVis <= 0f) p.ptrSeen = false;
        if (!wantG && p.ghostVis <= 0f) p.ghostSeen = false;
        if (wantT) p.tabletSeen = true;
        if (wantP) p.ptrSeen = true;
        if (wantG) p.ghostSeen = true;

        float k = 1f - Mathf.Exp(-14f * dt);
        p.root = Vector3.Lerp(p.root, p.rootT, k);
        p.yaw = Mathf.LerpAngle(p.yaw, p.yawT, k);
        p.size = Mathf.Lerp(p.size, p.sizeT, k);
        p.dist = Mathf.Lerp(p.dist, p.distT, k);
        p.height = Mathf.Lerp(p.height, p.heightT, k);
        float kp = 1f - Mathf.Exp(-20f * dt);
        p.from = Vector3.Lerp(p.from, p.fromT, kp);
        p.to = Vector3.Lerp(p.to, p.toT, kp);
        p.press = Mathf.MoveTowards(p.press, p.pressT, dt * 8f);
        p.gPos = Vector3.Lerp(p.gPos, p.gPosT, kp);
        p.gRot = Quaternion.Slerp(p.gRot, p.gRotT, kp);
        p.gScale = Mathf.Lerp(p.gScale, p.gScaleT, kp);

        if (p.tabletVis <= 0f && p.ptrVis <= 0f && p.ghostVis <= 0f)
        {
          if (p.root0 != null && p.root0.activeSelf) p.root0.SetActive(false);
          continue;
        }
        try { Draw(p, head, now); }
        catch (Exception ex) { SbLog.Warn("presence draw: " + ex.Message); scratch.Add(kv.Key); }
      }
      foreach (int a in scratch) { Peer p; if (peers.TryGetValue(a, out p)) Destroy(p); peers.Remove(a); }
    }

    private static LineRenderer Line(Transform parent, string name, int queue, bool world, bool loop)
    {
      var go = new GameObject(name);
      go.transform.SetParent(parent, false);
      var lr = go.AddComponent<LineRenderer>();
      lr.useWorldSpace = world;
      lr.loop = loop;
      lr.positionCount = loop ? 4 : 2;
      lr.sharedMaterial = SbMats.Line;
      lr.numCapVertices = loop ? 0 : 4;
      lr.numCornerVertices = loop ? 3 : 0;
      lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
      return lr;
    }

    private static GameObject Quad(Transform parent, string name, Material mat)
    {
      GameObject q = GameObject.CreatePrimitive(PrimitiveType.Quad);
      q.name = name;
      Collider c = q.GetComponent<Collider>(); if (c != null) UnityEngine.Object.Destroy(c);
      q.transform.SetParent(parent, false);
      Renderer r = q.GetComponent<Renderer>();
      r.sharedMaterial = mat;
      r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
      return q;
    }

    private static void Build(Peer p)
    {
      p.root0 = new GameObject("Unknowns SandBox Presence " + p.actor);
      UnityEngine.Object.DontDestroyOnLoad(p.root0);
      Transform t = p.root0.transform;
      p.panels = new Transform[3];
      p.fillMats = new Material[3];
      p.borders = new LineRenderer[3];
      for (int i = 0; i < 3; i++)
      {
        var pg = new GameObject("panel" + i);
        pg.transform.SetParent(t, false);
        p.panels[i] = pg.transform;
        p.fillMats[i] = new Material(SbMats.Line) { hideFlags = HideFlags.DontSave, renderQueue = 3040 };
        Quad(pg.transform, "fill", p.fillMats[i]);
        LineRenderer b = Line(pg.transform, "border", 3041, false, true);
        b.SetPositions(new[] { new Vector3(-0.5f, -0.5f, 0f), new Vector3(-0.5f, 0.5f, 0f), new Vector3(0.5f, 0.5f, 0f), new Vector3(0.5f, -0.5f, 0f) });
        p.borders[i] = b;
      }
      var tg = new GameObject("title");
      tg.transform.SetParent(t, false);
      p.title = tg.AddComponent<TextMeshPro>();
      p.title.alignment = TextAlignmentOptions.Center;
      p.title.fontStyle = FontStyles.Bold;
      p.title.fontSize = 2.2f;
      p.title.textWrappingMode = TextWrappingModes.NoWrap;
      p.title.rectTransform.sizeDelta = new Vector2(6f, 1f);

      p.glow = Line(t, "glow", 3099, true, false);
      p.core = Line(t, "core", 3101, true, false);
      p.haloMat = new Material(SbMats.Line) { hideFlags = HideFlags.DontSave, renderQueue = 3100 };
      if (p.haloMat.HasProperty("_MainTex")) p.haloMat.SetTexture("_MainTex", SbMats.SoftDot);
      p.halo = Quad(t, "halo", p.haloMat);

      p.gGo = new GameObject("preview");
      p.gGo.transform.SetParent(t, false);
      p.gMf = p.gGo.AddComponent<MeshFilter>();
      p.gMr = p.gGo.AddComponent<MeshRenderer>();
      p.gMr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
      p.gMat = new Material(SbMats.Ghost) { hideFlags = HideFlags.DontSave };
    }

    private static void Draw(Peer p, Transform head, float now)
    {
      if (p.root0 == null) Build(p);
      if (!p.root0.activeSelf) p.root0.SetActive(true);
      if (now >= p.nameAt)
      {
        p.nameAt = now + 3f;
        try
        {
          Player pl = PhotonNetwork.CurrentRoom.GetPlayer(p.actor);
          string n = pl != null ? pl.NickName : null;
          p.name = string.IsNullOrEmpty(n) ? "SANDBOX" : n.ToUpperInvariant() + "'S SANDBOX";
        }
        catch { p.name = "SANDBOX"; }
      }

      // tablet: three panels laid out like the owner's (SbAnimate), popping open
      bool tabletOn = p.tabletVis > 0f;
      for (int i = 0; i < 3; i++) if (p.panels[i].gameObject.activeSelf != tabletOn) p.panels[i].gameObject.SetActive(tabletOn);
      if (p.title.gameObject.activeSelf != tabletOn) p.title.gameObject.SetActive(tabletOn);
      if (tabletOn)
      {
        float v = p.tabletVis;
        float sy = Mathf.Lerp(0.03f, 1f, Pop(v / 0.65f));
        float sx = Mathf.Lerp(0.02f, 1f, 1f - Mathf.Pow(1f - Mathf.Clamp01((v - 0.25f) / 0.75f), 3f));
        float fade = Mathf.Clamp01(v * 2f);
        float size = p.size, d = p.dist;
        Quaternion rootRot = Quaternion.Euler(0f, p.yaw, 0f);
        float wc = PanelW[1] * 0.001f * size, ws = PanelW[0] * 0.001f * size;
        float theta = (wc * 0.5f + ws * 0.5f + 0.03f * size) / Mathf.Max(0.2f, d) * Mathf.Rad2Deg;
        Color fill = new Color(p.accent.r * 0.12f + 0.02f, p.accent.g * 0.12f + 0.02f, p.accent.b * 0.12f + 0.03f, 0.55f * fade);
        Color edge = p.accent; edge.a = 0.9f * fade;
        for (int i = 0; i < 3; i++)
        {
          float ang = i == 0 ? -theta : i == 2 ? theta : 0f;
          Quaternion pr = rootRot * Quaternion.Euler(0f, ang, 0f);
          Transform pt = p.panels[i];
          pt.position = p.root + Vector3.up * p.height + pr * Vector3.forward * d;
          pt.rotation = pr;
          pt.localScale = new Vector3(PanelW[i] * 0.001f * size * sx, PanelH * 0.001f * size * sy, 1f);
          p.fillMats[i].color = fill;
          LineRenderer b = p.borders[i];
          b.widthMultiplier = 0.004f * size;
          b.startColor = b.endColor = edge;
        }
        Transform c = p.panels[1];
        p.title.transform.position = c.position + c.up * (PanelH * 0.001f * size * sy * 0.5f + 0.035f * size);
        p.title.transform.rotation = c.rotation;
        p.title.transform.localScale = Vector3.one * size * 0.06f * sx;
        p.title.text = p.name;
        Color tc = Color.Lerp(p.accent, Color.white, 0.55f); tc.a = fade;
        p.title.color = tc;
      }

      // pointer
      bool ptrOn = p.ptrVis > 0f;
      p.core.enabled = p.glow.enabled = ptrOn;
      if (p.halo.activeSelf != ptrOn) p.halo.SetActive(ptrOn);
      if (ptrOn)
      {
        float v = p.ptrVis, pop = Pop(v), press = p.press;
        bool ui = (p.flags & F.PtrUi) != 0, del = (p.flags & F.PtrDelete) != 0;
        Color a = Color.Lerp(p.accent, Color.white, 0.3f * press);
        Color b = del ? new Color(1f, 0.28f, 0.22f) : Color.Lerp(p.accent2, Color.white, 0.25f * press);
        Vector3 to = Vector3.Lerp(p.from, p.to, Mathf.Clamp01(v * 1.3f));
        p.core.SetPosition(0, p.from); p.core.SetPosition(1, to);
        p.glow.SetPosition(0, p.from); p.glow.SetPosition(1, to);
        float wk = pop * (1f + 0.45f * press);
        p.core.startWidth = 0.0032f * wk;
        p.core.endWidth = (ui ? 0.0018f : 0.0026f) * wk;
        p.core.startColor = new Color(a.r, a.g, a.b, 0.9f * v);
        p.core.endColor = new Color(b.r, b.g, b.b, (ui ? 0.85f : 0.5f) * v);
        float gw = pop * (1f + 0.9f * press);
        p.glow.startWidth = 0.012f * gw;
        p.glow.endWidth = 0.008f * gw;
        p.glow.startColor = new Color(a.r, a.g, a.b, (0.08f + 0.22f * press) * v);
        p.glow.endColor = new Color(b.r, b.g, b.b, (0.03f + 0.14f * press) * v);
        Vector3 look = head != null ? to - head.position : p.to - p.from;
        if (look.sqrMagnitude > 1e-6f) p.halo.transform.rotation = Quaternion.LookRotation(look);
        p.halo.transform.position = to;
        p.halo.transform.localScale = Vector3.one * (ui ? 0.04f : 0.07f) * pop * (1f + 0.7f * press);
        p.haloMat.color = new Color(b.r, b.g, b.b, (0.3f + 0.4f * press) * v);
      }

      // placement preview
      bool gOn = p.ghostVis > 0f && p.ghostItem != null;
      if (p.gGo.activeSelf != gOn) p.gGo.SetActive(gOn);
      if (gOn)
      {
        SbItem item = p.ghostItem;
        Mesh mesh = item.Kind == SbKind.Fluid ? SbBlockMesh.Get(SbBlockArt.Glass, SbBlockArt.Glass, SbBlockArt.Glass, SbBlockArt.Glass) : item.Mesh;
        if (p.gMf.sharedMesh != mesh)
        {
          p.gMf.sharedMesh = mesh;
          int subs = mesh != null ? mesh.subMeshCount : 1;
          var mats = new Material[subs];
          for (int i = 0; i < subs; i++) mats[i] = p.gMat;
          p.gMr.sharedMaterials = mats;
        }
        bool valid = (p.flags & F.GhostValid) != 0;
        Color gc = valid ? p.accent : new Color(1f, 0.28f, 0.22f);
        gc.a = (valid ? 0.26f + 0.04f * Mathf.Sin(now * 3f) : 0.28f) * Mathf.Clamp01(p.ghostVis * 1.6f);
        p.gMat.color = gc;
        p.gGo.transform.SetPositionAndRotation(p.gPos, p.gRot);
        p.gGo.transform.localScale = Vector3.one * Mathf.Max(0.0001f, p.gScale * Pop(p.ghostVis));
      }
    }
  }
}
