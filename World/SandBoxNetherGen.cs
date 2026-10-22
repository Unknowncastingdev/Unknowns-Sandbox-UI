using System;
using System.Collections.Generic;
using GorillaLocomotion;
using Photon.Pun;
using UnityEngine;

namespace UnknownsSandbox
{
  // The Nether: endless like Minecraft's, generated in 16 m chunks around
  // you as you walk (and dropped again behind you). Each lobby gets its own random
  // Nether: the seed comes from the room name, so everyone in the same lobby walks
  // the same Nether. Soul sand valleys, netherrack wastes with magma by the lava
  // sea, pillars, glowstone in the ceiling, and the return portal (solid frame)
  // where you arrive. Natural piglins wander around you (SandBoxMobs.cs).
  internal sealed partial class SandBoxWorld
  {
    // inside Gorilla Tag's +-1024 m network range (rigs further out show up clamped)
    internal static readonly Vector3 NetherOrigin = new Vector3(0f, 600f, 0f);
    private const float NetherBlock = 0.5f;
    private const int NetherHeight = 72;       // taller (was 44): high ceilings, cliffs and ledges
    private const int NetherLava = 10;         // higher: big lava seas
    private const int NetherFloorAtSpawn = 16;
    private const int NetherChunk = 32;          // columns per chunk side (16 m)
    // render distance in chunks (16 m each), the SandBox settings slider sets it
    internal static int NetherViewChunks = 2;
    // no wall any more, the Nether goes on as far as you walk. (Past ~1 km from
    // the arrival portal Gorilla Tag can't send your position exactly, so other players
    // see you stuck at that distance; your own game is fine.)

    private GameObject nether;
    private Bounds netherPortal;
    private int netherSeed = -1;   // (the blocks: netherDim, SandBoxVoxels.cs)
    private Material netherRock;
    private Transform netherSky;
    private readonly List<Light> netherLights = new List<Light>();

    // the purple swirl while you stand in a portal (0..1), drawn by SbPortalOverlay and on the desktop
    internal static float PortalSwirl;
    private float swirlWant;

    private Vector3 NetherSpawn
    {
      get { return NetherOrigin + new Vector3(0.5f * NetherBlock, (NetherFloorAtSpawn + 3.4f) * NetherBlock, -3.5f * NetherBlock); }
    }

    // fixed noise (the End uses it too)
    private static float N2(float x, float z, int seed)
    {
      return Mathf.PerlinNoise(x + seed * 17.13f + 3000f, z + seed * 31.7f + 3000f);
    }

    // the lobby's Nether noise
    private float NN(int x, int z, float f, int k)
    {
      return N2(x * f, z * f, k + netherSeed * 7);
    }

    private static int RoomSeed()
    {
      string r = "offline";
      try { if (PhotonNetwork.InRoom && PhotonNetwork.CurrentRoom != null) r = PhotonNetwork.CurrentRoom.Name ?? r; } catch { }
      uint h = 2166136261u;
      foreach (char ch in r.ToUpperInvariant()) { h ^= ch; h *= 16777619u; }
      return (int)(h % 997u) + 1;
    }

    // (one column: SandBoxNetherBiomes.cs, )

    // ---------------------------------------------------------------- building
    // the Nether is real blocks (SandBoxVoxels.cs). The block data exists as
    // soon as anyone breaks something (so their edits are kept even before you go);
    // the meshes only near you, while you are there.
    private SbVoxelDim EnsureNetherDim()
    {
      int seed = RoomSeed();
      if (netherDim != null && seed == netherSeed) return netherDim;
      netherSeed = seed;
      if (netherDim != null) netherDim.DestroyAll();
      netherDim = new SbVoxelDim(1, NetherOrigin, NetherBlock, NetherHeight, FillNether, NetherLavaAt);
      return netherDim;
    }

    private void BuildNether()
    {
      SbVoxelDim before = netherDim;
      EnsureNetherDim();
      if (nether != null && before != netherDim)
      {
        // another lobby, another Nether
        Destroy(nether);
        nether = null;
        netherLights.Clear();
      }
      if (nether != null) { nether.SetActive(true); netherDim.BuildNear(NetherSpawn, 1, int.MaxValue); return; }

      nether = new GameObject("Unknowns_SandBoxNether");
      nether.transform.position = NetherOrigin;
      netherRock = new Material(SbMats.BlockLit) { hideFlags = HideFlags.DontSave };
      if (netherRock.HasProperty("_BaseColor")) netherRock.SetColor("_BaseColor", new Color(0.82f, 0.7f, 0.66f));
      EnsureLayers();
      netherDim.Root = nether.transform;
      netherDim.Rock = netherRock;
      netherDim.Lava = FluidMaterial(SbFluid.Lava);
      netherDim.Layer = solidLayer;
      BuildNetherPortal();

      // the sky: a dark red shell that moves with you (hides the map below)
      var sky = GameObject.CreatePrimitive(PrimitiveType.Sphere);
      sky.name = "nether sky";
      var sc = sky.GetComponent<Collider>(); if (sc != null) Destroy(sc);
      sky.transform.SetParent(nether.transform, false);
      sky.transform.localScale = Vector3.one * 140f;
      var skyMat = new Material(SbMats.Line) { hideFlags = HideFlags.DontSave, color = new Color(0.22f, 0.04f, 0.03f, 1f), renderQueue = 2900 };
      var skr = sky.GetComponent<Renderer>();
      skr.sharedMaterial = skyMat;
      skr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
      netherSky = sky.transform;

      // warm lava light and glowstone light around you
      for (int i = 0; i < 6; i++)
      {
        var l = new GameObject("nether light").AddComponent<Light>();
        l.transform.SetParent(nether.transform, false);
        l.type = LightType.Point;
        l.color = i % 2 == 0 ? new Color(1f, 0.4f, 0.1f) : new Color(1f, 0.85f, 0.5f);
        l.range = 18f; l.intensity = 1.4f; l.shadows = LightShadows.None;
        netherLights.Add(l);
      }
      // the ground you arrive on is there straight away
      netherDim.BuildNear(NetherSpawn, 1, int.MaxValue);
    }

    // the return portal: obsidian frame 4 x 5 on the spawn ground, solid like real obsidian
    private void BuildNetherPortal()
    {
      float s = NetherBlock;
      var pv = new List<Vector3>(); var pn = new List<Vector3>(); var puv = new List<Vector2>(); var pt = new List<int>(); var portalTris = new List<int>();
      int px0 = -1, py0 = NetherFloorAtSpawn, pz = 0;
      Action<int, int, int, bool> cube = (bx, by, bz, isPortal) =>
      {
        int tile = isPortal ? SbBlockArt.NetherPortal : SbBlockArt.Obsidian;
        Mesh unit = SbBlockMesh.Get(tile, tile, tile, tile);
        Vector3[] uvVerts = unit.vertices; Vector3[] uvN = unit.normals; Vector2[] uvs = unit.uv; int[] tri = unit.triangles;
        int baseI = pv.Count;
        Vector3 center = new Vector3((bx + 0.5f) * s, (by + 0.5f) * s, (bz + 0.5f) * s);
        Vector3 scale = isPortal ? new Vector3(s, s, s * 0.25f) : Vector3.one * s;
        for (int i = 0; i < uvVerts.Length; i++) { pv.Add(center + Vector3.Scale(uvVerts[i], scale)); pn.Add(uvN[i]); puv.Add(uvs[i]); }
        foreach (int i in tri) (isPortal ? portalTris : pt).Add(baseI + i);
      };
      for (int i = 0; i < 4; i++) { cube(px0 - 1 + i, py0, pz, false); cube(px0 - 1 + i, py0 + 4, pz, false); }
      for (int y = 1; y < 4; y++) { cube(px0 - 1, py0 + y, pz, false); cube(px0 + 2, py0 + y, pz, false); }
      for (int y = 1; y < 4; y++) for (int i = 0; i < 2; i++) cube(px0 + i, py0 + y, pz, true);
      var fm = new Mesh { name = "SB Nether Portal", hideFlags = HideFlags.DontSave };
      fm.SetVertices(pv); fm.SetNormals(pn); fm.SetUVs(0, puv);
      fm.subMeshCount = 2; fm.SetTriangles(pt, 0); fm.SetTriangles(portalTris, 1);
      fm.RecalculateBounds();
      var frame = new GameObject("return portal");
      frame.transform.SetParent(nether.transform, false);
      frame.AddComponent<MeshFilter>().sharedMesh = fm;
      frame.AddComponent<MeshRenderer>().sharedMaterials = new[] { SbMats.BlockLit, SbMats.BlockClear };
      // the obsidian is solid (the portal inside still lets you walk in)
      var colMesh = new Mesh { name = "SB Nether Portal frame", hideFlags = HideFlags.DontSave };
      colMesh.SetVertices(pv); colMesh.SetTriangles(pt, 0); colMesh.RecalculateBounds();
      var colGo = new GameObject("frame collider");
      colGo.transform.SetParent(frame.transform, false);
      colGo.layer = solidLayer;
      colGo.AddComponent<MeshCollider>().sharedMesh = colMesh;
      Vector3 pMin = NetherOrigin + new Vector3(px0 * s, (py0 + 1) * s, pz * s);
      netherPortal = new Bounds(pMin + new Vector3(s, 1.5f * s, 0.5f * s), new Vector3(2f * s, 3f * s, s * 0.9f));
      var pl = new GameObject("portal light").AddComponent<Light>();
      pl.transform.SetParent(nether.transform, false);
      pl.transform.position = netherPortal.center;
      pl.type = LightType.Point; pl.color = new Color(0.6f, 0.2f, 1f); pl.range = 5f; pl.intensity = 1.6f; pl.shadows = LightShadows.None;
    }

    // ---------------------------------------------------------------- every frame in the Nether
    private void UpdateNetherWorld(Vector3 head)
    {
      if (nether == null || netherDim == null) return;
      // one new chunk a frame, nearest first; far ones go
      netherDim.BuildNear(head, NetherViewChunks, 1);
      netherDim.DropFar(head, NetherViewChunks + 1);
      float s = NetherBlock;
      if (netherSky != null)
      {
        netherSky.position = head;
        // the sky shell sits just past the last chunk, hiding the edge
        netherSky.localScale = Vector3.one * Mathf.Max(70f, (NetherViewChunks + 1) * NetherChunk * NetherBlock * 2.2f);
      }
      for (int i = 0; i < netherLights.Count; i++)
      {
        Light l = netherLights[i];
        if (l == null) continue;
        float a = i / (float)netherLights.Count * Mathf.PI * 2f;
        l.transform.position = new Vector3(head.x + Mathf.Cos(a) * 11f, NetherOrigin.y + (i % 2 == 0 ? NetherLava + 2 : NetherHeight - 10) * s, head.z + Mathf.Sin(a) * 11f);
      }
    }

    // the ground height (world y) of the Nether at a world point, or false over lava / in a wall
    internal bool NetherGround(Vector3 p, out float y)
    {
      float s = NetherBlock;
      int x = Mathf.FloorToInt((p.x - NetherOrigin.x) / s), z = Mathf.FloorToInt((p.z - NetherOrigin.z) / s);
      int f, c, t; bool g;
      NetherColumn(x, z, out f, out c, out t, out g);
      y = NetherOrigin.y + f * s;
      return f >= NetherLava && c - f >= 6;
    }
  }

  // The purple swirl in front of your eyes while you stand in a Nether portal
  // (and as you arrive), like Minecraft's portal animation.
  internal static class SbPortalOverlay
  {
    private static GameObject quad;
    private static Material mat;
    private static Texture2D tex;

    // a new texture pack: take the new portal tile
    internal static void Reset()
    {
      tex = null;
      if (mat != null && mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", Texture);
    }

    internal static Texture2D Texture
    {
      get
      {
        if (tex == null) tex = SbBlockArt.TileTexture(SbBlockArt.NetherPortal);
        return tex;
      }
    }

    internal static void Tick(float amount)
    {
      Camera cam = Camera.main;
      if (amount <= 0.001f || cam == null)
      {
        if (quad != null && quad.activeSelf) quad.SetActive(false);
        return;
      }
      if (quad == null)
      {
        quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
        quad.name = "Unknowns Portal Swirl";
        var c = quad.GetComponent<Collider>(); if (c != null) UnityEngine.Object.Destroy(c);
        mat = new Material(SbMats.Line) { hideFlags = HideFlags.DontSave, renderQueue = 4000 };
        if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", Texture);
        var r = quad.GetComponent<Renderer>();
        r.sharedMaterial = mat;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
      }
      if (quad.transform.parent != cam.transform) quad.transform.SetParent(cam.transform, false);
      if (!quad.activeSelf) quad.SetActive(true);
      float t = Time.unscaledTime;
      quad.transform.localPosition = new Vector3(0f, 0f, Mathf.Max(0.06f, cam.nearClipPlane * 1.5f));
      quad.transform.localRotation = Quaternion.Euler(0f, 0f, t * 12f);
      quad.transform.localScale = Vector3.one * 0.6f * (1f + 0.05f * Mathf.Sin(t * 3f));
      mat.mainTextureScale = new Vector2(3f, 3f);
      mat.mainTextureOffset = new Vector2(Mathf.Sin(t * 0.7f) * 0.3f, t * 0.35f);
      mat.color = new Color(0.75f, 0.35f, 1f, Mathf.Clamp01(amount) * 0.85f);
    }
  }
}
