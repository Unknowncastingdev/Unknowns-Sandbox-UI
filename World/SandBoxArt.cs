using System;
using System.Collections.Generic;
using UnityEngine;

namespace UnknownsSandbox
{
  // SandBox Tablet art. Everything is made in code: no asset bundle, no game
  // assets. Models are built from boxes/cylinders/spheres coloured through a
  // small palette texture (each part's UVs sit in the middle of one colour cell),
  // so every model shares ONE lit material and ONE glow material. Minecraft-style
  // blocks use a 16 px pixel-art atlas painted here.

  // ---------------------------------------------------------------- palette
  internal static class SbPalette
  {
    private const int Cells = 64;          // 64 x 64 colours (was 32 x 32; it filled up, and item pixels got the nearest colour instead)
    private const int CellPx = 4;
    private static readonly Dictionary<int, int> index = new Dictionary<int, int>();
    private static readonly List<Color32> colors = new List<Color32>();
    private static Texture2D texture;
    private static Color32[] pixels;
    internal static bool Dirty = true;

    private static int Key(Color32 c) { return (c.r << 24) | (c.g << 16) | (c.b << 8) | c.a; }

    internal static Vector2 UV(Color c)
    {
      Color32 c32 = c;
      int k = Key(c32);
      int i;
      if (!index.TryGetValue(k, out i))
      {
        if (colors.Count >= Cells * Cells)
        {
          // Full: use the nearest colour already in the palette.
          int best = 0; int bestD = int.MaxValue;
          for (int j = 0; j < colors.Count; j++)
          {
            Color32 o = colors[j];
            int d = (o.r - c32.r) * (o.r - c32.r) + (o.g - c32.g) * (o.g - c32.g) + (o.b - c32.b) * (o.b - c32.b);
            if (d < bestD) { bestD = d; best = j; }
          }
          i = best;
        }
        else
        {
          i = colors.Count;
          colors.Add(c32);
          Dirty = true;
        }
        index[k] = i;
      }
      int cx = i % Cells, cy = i / Cells;
      return new Vector2((cx + 0.5f) / Cells, (cy + 0.5f) / Cells);
    }

    internal static Texture2D Texture
    {
      get
      {
        if (texture == null)
        {
          texture = new Texture2D(Cells * CellPx, Cells * CellPx, TextureFormat.RGBA32, false, false);
          texture.name = "Unknowns SandBox Palette";
          texture.filterMode = FilterMode.Point;
          texture.wrapMode = TextureWrapMode.Clamp;
          texture.hideFlags = HideFlags.DontSave;
          Dirty = true;
        }
        if (Dirty)
        {
          int size = Cells * CellPx;
          if (pixels == null) pixels = new Color32[size * size];
          for (int i = 0; i < colors.Count; i++)
          {
            int cx = (i % Cells) * CellPx, cy = (i / Cells) * CellPx;
            for (int y = 0; y < CellPx; y++)
              for (int x = 0; x < CellPx; x++)
                pixels[(cy + y) * size + cx + x] = colors[i];
          }
          texture.SetPixels32(pixels);
          texture.Apply(false, false);
          Dirty = false;
        }
        return texture;
      }
    }

    internal static Color32 Sample(Vector2 uv)
    {
      int i = Mathf.Clamp((int)(uv.y * Cells), 0, Cells - 1) * Cells + Mathf.Clamp((int)(uv.x * Cells), 0, Cells - 1);
      return i < colors.Count ? colors[i] : new Color32(255, 0, 255, 255);
    }
  }

  // ---------------------------------------------------------------- mesh kit
  internal sealed class SbMeshKit
  {
    private readonly List<Vector3> verts = new List<Vector3>();
    private readonly List<Vector3> norms = new List<Vector3>();
    private readonly List<Vector2> uvs = new List<Vector2>();
    private readonly List<int> lit = new List<int>();
    private readonly List<int> glow = new List<int>();
    private Matrix4x4 m = Matrix4x4.identity;
    private Matrix4x4 mn = Matrix4x4.identity;
    private Vector2 uv;
    private bool g;

    // Round parts get Detail x their segments (smoother, more realistic models);
    // imported (Kenney) triangles are smooth shaded with a crease angle in ToMesh.
    internal static int Detail = 2;
    private static int D(int seg) { return Mathf.Clamp(seg * Detail, 3, 64); }
    private const float CreaseDeg = 40f;
    private readonly List<int> rawVerts = new List<int>();
    private readonly List<Vector3> rawFaceN = new List<Vector3>();

    // The grip point (where a hand holds it) and the muzzle/tip, in model space.
    internal Vector3 Grip;
    internal Vector3 Muzzle = new Vector3(0f, 0f, 0.3f);
    internal bool HasGrip;

    private void Part(Vector3 c, Quaternion r, Vector3 s, Color col, bool glowing)
    {
      m = Matrix4x4.TRS(c, r, s);
      mn = m.inverse.transpose;
      uv = SbPalette.UV(col);
      g = glowing;
    }

    private int V(Vector3 p, Vector3 n)
    {
      verts.Add(m.MultiplyPoint3x4(p));
      Vector3 wn = mn.MultiplyVector(n);
      norms.Add(wn.sqrMagnitude > 1e-10f ? wn.normalized : Vector3.up);
      uvs.Add(uv);
      return verts.Count - 1;
    }

    private void T(int a, int b, int c)
    {
      // Unity: front face when cross(b-a, c-a) points at the viewer. Fix the
      // order from the vertex normals so every primitive below can be written
      // without worrying about winding.
      Vector3 pa = verts[a], pb = verts[b], pc = verts[c];
      Vector3 face = Vector3.Cross(pb - pa, pc - pa);
      Vector3 n = norms[a] + norms[b] + norms[c];
      List<int> list = g ? glow : lit;
      if (Vector3.Dot(face, n) < 0f) { list.Add(a); list.Add(c); list.Add(b); }
      else { list.Add(a); list.Add(b); list.Add(c); }
    }

    private void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 n)
    {
      int ia = V(a, n), ib = V(b, n), ic = V(c, n), id = V(d, n);
      T(ia, ib, ic); T(ia, ic, id);
    }

    internal SbMeshKit Box(Vector3 c, Vector3 size, Color col, bool glowing = false) { return Box(c, size, Quaternion.identity, col, glowing); }

    internal SbMeshKit Box(Vector3 c, Vector3 size, Quaternion r, Color col, bool glowing = false)
    {
      Part(c, r, size, col, glowing);
      const float h = 0.5f;
      Quad(new Vector3(-h, -h, h), new Vector3(h, -h, h), new Vector3(h, h, h), new Vector3(-h, h, h), Vector3.forward);
      Quad(new Vector3(h, -h, -h), new Vector3(-h, -h, -h), new Vector3(-h, h, -h), new Vector3(h, h, -h), Vector3.back);
      Quad(new Vector3(h, -h, h), new Vector3(h, -h, -h), new Vector3(h, h, -h), new Vector3(h, h, h), Vector3.right);
      Quad(new Vector3(-h, -h, -h), new Vector3(-h, -h, h), new Vector3(-h, h, h), new Vector3(-h, h, -h), Vector3.left);
      Quad(new Vector3(-h, h, h), new Vector3(h, h, h), new Vector3(h, h, -h), new Vector3(-h, h, -h), Vector3.up);
      Quad(new Vector3(-h, -h, -h), new Vector3(h, -h, -h), new Vector3(h, -h, h), new Vector3(-h, -h, h), Vector3.down);
      return this;
    }

    // Frustum along local +Y from y=-0.5 to y=0.5 (scaled by h); r0 bottom, r1 top.
    internal SbMeshKit Frustum(Vector3 c, float r0, float r1, float h, Quaternion r, Color col, int seg = 12, bool glowing = false, bool caps = true)
    {
      seg = D(seg);
      Part(c, r, Vector3.one, col, glowing);
      float y0 = -h * 0.5f, y1 = h * 0.5f;
      float slope = (r0 - r1) / Mathf.Max(0.0001f, h);
      for (int i = 0; i < seg; i++)
      {
        float a0 = i * Mathf.PI * 2f / seg, a1 = (i + 1) * Mathf.PI * 2f / seg;
        Vector3 d0 = new Vector3(Mathf.Cos(a0), 0f, Mathf.Sin(a0));
        Vector3 d1 = new Vector3(Mathf.Cos(a1), 0f, Mathf.Sin(a1));
        Vector3 n0 = (d0 + Vector3.up * slope).normalized, n1 = (d1 + Vector3.up * slope).normalized;
        int ia = V(d0 * r0 + Vector3.up * y0, n0), ib = V(d1 * r0 + Vector3.up * y0, n1);
        int ic = V(d1 * r1 + Vector3.up * y1, n1), id = V(d0 * r1 + Vector3.up * y1, n0);
        if (r0 > 0.00001f) T(ia, ib, ic);
        if (r1 > 0.00001f) T(ia, ic, id);
        if (caps)
        {
          if (r1 > 0.00001f)
          {
            int t0 = V(Vector3.up * y1, Vector3.up), t1 = V(d0 * r1 + Vector3.up * y1, Vector3.up), t2 = V(d1 * r1 + Vector3.up * y1, Vector3.up);
            T(t0, t1, t2);
          }
          if (r0 > 0.00001f)
          {
            int b0 = V(Vector3.up * y0, Vector3.down), b1 = V(d0 * r0 + Vector3.up * y0, Vector3.down), b2 = V(d1 * r0 + Vector3.up * y0, Vector3.down);
            T(b0, b1, b2);
          }
        }
      }
      return this;
    }

    internal SbMeshKit Cyl(Vector3 c, float radius, float h, Quaternion r, Color col, int seg = 12, bool glowing = false)
    {
      return Frustum(c, radius, radius, h, r, col, seg, glowing);
    }

    // Cylinder along local Z (handy for barrels).
    internal SbMeshKit Tube(Vector3 c, float radius, float length, Color col, int seg = 10, bool glowing = false)
    {
      return Frustum(c, radius, radius, length, Quaternion.Euler(90f, 0f, 0f), col, seg, glowing);
    }

    internal SbMeshKit Cone(Vector3 c, float radius, float h, Quaternion r, Color col, int seg = 12, bool glowing = false)
    {
      return Frustum(c, radius, 0f, h, r, col, seg, glowing);
    }

    internal SbMeshKit Sphere(Vector3 c, Vector3 radii, Color col, int seg = 12, bool glowing = false) { return Sphere(c, radii, Quaternion.identity, col, seg, glowing); }

    internal SbMeshKit Sphere(Vector3 c, Vector3 radii, Quaternion r, Color col, int seg = 12, bool glowing = false)
    {
      seg = D(seg);
      Part(c, r, radii, col, glowing);
      int rings = Mathf.Max(4, seg / 2);
      int start = verts.Count;
      for (int y = 0; y <= rings; y++)
      {
        float v = (float)y / rings;
        float lat = Mathf.PI * (v - 0.5f);
        for (int x = 0; x <= seg; x++)
        {
          float lon = x * Mathf.PI * 2f / seg;
          Vector3 p = new Vector3(Mathf.Cos(lat) * Mathf.Cos(lon), Mathf.Sin(lat), Mathf.Cos(lat) * Mathf.Sin(lon));
          V(p, p);
        }
      }
      for (int y = 0; y < rings; y++)
        for (int x = 0; x < seg; x++)
        {
          int a = start + y * (seg + 1) + x, b = a + 1, c2 = a + seg + 1, d = c2 + 1;
          if (y > 0) T(a, b, d);
          if (y < rings - 1) T(a, d, c2);
        }
      return this;
    }

    // A slice of a sphere between two longitudes (beach ball panels).
    internal SbMeshKit Lune(Vector3 c, float radius, float lon0Deg, float lon1Deg, Color col, int seg = 4, int rings = 10)
    {
      seg = D(seg); rings = D(rings);
      Part(c, Quaternion.identity, Vector3.one * radius, col, false);
      int start = verts.Count;
      for (int y = 0; y <= rings; y++)
      {
        float lat = Mathf.PI * ((float)y / rings - 0.5f);
        for (int x = 0; x <= seg; x++)
        {
          float lon = Mathf.Lerp(lon0Deg, lon1Deg, (float)x / seg) * Mathf.Deg2Rad;
          Vector3 p = new Vector3(Mathf.Cos(lat) * Mathf.Cos(lon), Mathf.Sin(lat), Mathf.Cos(lat) * Mathf.Sin(lon));
          V(p, p);
        }
      }
      for (int y = 0; y < rings; y++)
        for (int x = 0; x < seg; x++)
        {
          int a = start + y * (seg + 1) + x, b = a + 1, c2 = a + seg + 1, d = c2 + 1;
          if (y > 0) T(a, b, d);
          if (y < rings - 1) T(a, d, c2);
        }
      return this;
    }

    internal SbMeshKit Ball(Vector3 c, float radius, Color col, int seg = 12, bool glowing = false)
    {
      return Sphere(c, Vector3.one * radius, col, seg, glowing);
    }

    // Ring around local Y.
    internal SbMeshKit Torus(Vector3 c, float radius, float thick, Quaternion r, Color col, int seg = 16, int side = 8, bool glowing = false)
    {
      seg = D(seg); side = Mathf.Clamp(side * Detail, 3, 32);
      Part(c, r, Vector3.one, col, glowing);
      int start = verts.Count;
      for (int i = 0; i <= seg; i++)
      {
        float a = i * Mathf.PI * 2f / seg;
        Vector3 dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
        for (int j = 0; j <= side; j++)
        {
          float b = j * Mathf.PI * 2f / side;
          Vector3 n = dir * Mathf.Cos(b) + Vector3.up * Mathf.Sin(b);
          V(dir * radius + n * thick, n);
        }
      }
      for (int i = 0; i < seg; i++)
        for (int j = 0; j < side; j++)
        {
          int a = start + i * (side + 1) + j, b = a + 1, c2 = a + side + 1, d = c2 + 1;
          T(a, b, d); T(a, d, c2);
        }
      return this;
    }

    // Ramp: full height at -Z, zero at +Z, size w x h x d.
    internal SbMeshKit Wedge(Vector3 c, Vector3 size, Quaternion r, Color col, bool glowing = false)
    {
      Part(c, r, size, col, glowing);
      const float h = 0.5f;
      Vector3 a = new Vector3(-h, -h, -h), b = new Vector3(h, -h, -h), cc = new Vector3(h, -h, h), d = new Vector3(-h, -h, h);
      Vector3 e = new Vector3(-h, h, -h), f = new Vector3(h, h, -h);
      Quad(a, b, cc, d, Vector3.down);
      Quad(b, a, e, f, Vector3.back);
      Vector3 slope = new Vector3(0f, 1f, 1f).normalized;
      Quad(e, f, cc, d, slope);
      int i0 = V(a, Vector3.left), i1 = V(d, Vector3.left), i2 = V(e, Vector3.left); T(i0, i1, i2);
      int j0 = V(b, Vector3.right), j1 = V(f, Vector3.right), j2 = V(cc, Vector3.right); T(j0, j1, j2);
      return this;
    }

    // one flat-shaded triangle in model space (imported models)
    internal SbMeshKit RawTri(Vector3 a, Vector3 b, Vector3 c, Color col, bool glowing = false)
    {
      m = Matrix4x4.identity; mn = Matrix4x4.identity;
      uv = SbPalette.UV(col); g = glowing;
      Vector3 n = Vector3.Cross(b - a, c - a);
      if (n.sqrMagnitude < 1e-14f) return this;
      Vector3 area = n;   // length = 2 x area: bigger faces weigh more in the smoothing
      n.Normalize();
      int ia = V(a, n), ib = V(b, n), ic = V(c, n);
      T(ia, ib, ic);
      rawVerts.Add(ia); rawVerts.Add(ib); rawVerts.Add(ic);
      rawFaceN.Add(area); rawFaceN.Add(area); rawFaceN.Add(area);
      return this;
    }

    // Smooth normals for the imported triangles: corners at the same spot and colour
    // share a normal, except across edges sharper than CreaseDeg (those stay crisp).
    private void SmoothRaw()
    {
      if (rawVerts.Count == 0) return;
      var groups = new Dictionary<long, List<int>>();
      for (int i = 0; i < rawVerts.Count; i++)
      {
        Vector3 p = verts[rawVerts[i]];
        Vector2 u = uvs[rawVerts[i]];
        long k = ((long)Mathf.RoundToInt(p.x * 4000f) * 73856093L) ^ ((long)Mathf.RoundToInt(p.y * 4000f) * 19349663L) ^
                 ((long)Mathf.RoundToInt(p.z * 4000f) * 83492791L) ^ ((long)Mathf.RoundToInt(u.x * 8192f + u.y * 8192f * 8192f) * 2654435761L);
        List<int> l;
        if (!groups.TryGetValue(k, out l)) { l = new List<int>(4); groups[k] = l; }
        l.Add(i);
      }
      float cos = Mathf.Cos(CreaseDeg * Mathf.Deg2Rad);
      foreach (List<int> l in groups.Values)
      {
        if (l.Count < 2) continue;
        foreach (int i in l)
        {
          Vector3 mine = rawFaceN[i].normalized, sum = Vector3.zero;
          foreach (int j in l)
          {
            Vector3 other = rawFaceN[j];
            if (Vector3.Dot(mine, other.normalized) >= cos) sum += other;
          }
          if (sum.sqrMagnitude > 1e-14f) norms[rawVerts[i]] = sum.normalized;
        }
      }
    }

    // A 16 x 16 picture (rows from the top) extruded into boxes, one per run of
    // same-coloured pixels. Used for Minecraft item sprites.
    internal SbMeshKit Pixels(Color32[] px, float size, float depth, Quaternion rot, Vector3 origin)
    {
      for (int r = 0; r < 16; r++)
      {
        int c = 0;
        while (c < 16)
        {
          Color32 p = px[r * 16 + c];
          if (p.a < 128) { c++; continue; }
          int start = c;
          while (c < 16 && px[r * 16 + c].a >= 128 && px[r * 16 + c].r == p.r && px[r * 16 + c].g == p.g && px[r * 16 + c].b == p.b) c++;
          int len = c - start;
          Vector3 local = new Vector3((start + len * 0.5f) * size, (16 - r - 0.5f) * size, 0f);
          Box(origin + rot * local, new Vector3(len * size, size, depth), rot, new Color32(p.r, p.g, p.b, 255));
        }
      }
      return this;
    }

    internal SbMeshKit GripAt(Vector3 p) { Grip = p; HasGrip = true; return this; }
    internal SbMeshKit MuzzleAt(Vector3 p) { Muzzle = p; return this; }

    internal Mesh ToMesh(string name)
    {
      Mesh mesh = new Mesh();
      mesh.name = name;
      mesh.hideFlags = HideFlags.DontSave;
      SmoothRaw();
      if (verts.Count > 65000) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
      mesh.SetVertices(verts);
      mesh.SetNormals(norms);
      mesh.SetUVs(0, uvs);
      mesh.subMeshCount = 2;
      mesh.SetTriangles(lit, 0, false);
      mesh.SetTriangles(glow, 1, false);
      mesh.RecalculateBounds();
      return mesh;
    }

    internal bool HasGlow { get { return glow.Count > 0; } }
  }

  // ---------------------------------------------------------------- block atlas
  internal static class SbBlockArt
  {
    internal const int Tile = 16;
    internal const int PerRow = 32;          // 32 x 32 tiles (was 16 x 16), room for the extra blocks
    internal const int Size = Tile * PerRow;  // 512 x 512
    private static Texture2D atlas;
    private static Color32[] px;

    // Tile ids
    internal const int GrassTop = 0, GrassSide = 1, Dirt = 2, Stone = 3, Cobble = 4, Planks = 5, LogSide = 6, LogTop = 7,
      Leaves = 8, Sand = 9, Gravel = 10, Glass = 11, Bricks = 12, StoneBricks = 13, Bookshelf = 14, CraftTop = 15,
      CraftSide = 16, CraftFront = 17, TntSide = 18, TntTop = 19, TntBottom = 20, Obsidian = 21, Bedrock = 22, Diamond = 23,
      Gold = 24, Iron = 25, Emerald = 26, Redstone = 27, Lapis = 28, CoalOre = 29, IronOre = 30, DiamondOre = 31,
      GoldOre = 32, Glowstone = 33, Netherrack = 34, SoulSand = 35, Ice = 36, Snow = 37, PumpkinSide = 38, PumpkinTop = 39,
      PumpkinFace = 40, JackFace = 41, MelonSide = 42, MelonTop = 43, WoolWhite = 44, WoolRed = 45, WoolBlue = 46, WoolGreen = 47,
      WoolYellow = 48, WoolBlack = 49, HaySide = 50, HayTop = 51, Slime = 52, SeaLantern = 53, Sponge = 54, Magma = 55,
      FurnaceFront = 56, FurnaceSide = 57, FurnaceTop = 58, Water = 59, Lava = 60, Mossy = 61, Quartz = 62, WoolPurple = 63,
      WoolOrange = 64, WoolPink = 65, WoolCyan = 66, EmeraldOre = 67, Crate = 68, Dummy = 69, NetherPortal = 70, NetherBricks = 71,
      EndStone = 72, EndFrameTop = 73, EndFrameSide = 74, EndFrameEye = 75, EndPortal = 76, Fire = 77;

    internal static Texture2D Atlas
    {
      get
      {
        if (atlas != null) return atlas;
        px = new Color32[Size * Size];
        for (int i = 0; i < px.Length; i++) px[i] = new Color32(255, 0, 255, 255);
        PaintAll();
        try { ApplyMinecraft(); } catch (Exception ex) { SbLog.Warn("Minecraft tiles: " + ex.Message); }
        atlas = new Texture2D(Size, Size, TextureFormat.RGBA32, false, false);
        atlas.name = "Unknowns SandBox Blocks";
        atlas.filterMode = FilterMode.Point;
        atlas.wrapMode = TextureWrapMode.Clamp;
        atlas.hideFlags = HideFlags.DontSave;
        atlas.SetPixels32(px);
        atlas.Apply(false, false);
        return atlas;
      }
    }

    internal static Color32 Sample(Vector2 uv)
    {
      if (px == null) { var unused = Atlas; }
      int x = Mathf.Clamp((int)(uv.x * Size), 0, Size - 1);
      int y = Mathf.Clamp((int)(uv.y * Size), 0, Size - 1);
      return px[y * Size + x];
    }

    // another texture pack: repaint the atlas in place (every block material uses it)
    internal static void Reload()
    {
      if (atlas == null) return;
      for (int i = 0; i < px.Length; i++) px[i] = new Color32(255, 0, 255, 255);
      PaintAll();
      try { ApplyMinecraft(); } catch (Exception ex) { SbLog.Warn("Minecraft tiles: " + ex.Message); }
      atlas.SetPixels32(px);
      atlas.Apply(false, false);
      foreach (Texture2D t in tileTextures.Values) if (t != null) UnityEngine.Object.Destroy(t);
      tileTextures.Clear();
    }

    // one tile as its own repeating texture (the portal swirl overlay)
    private static readonly Dictionary<int, Texture2D> tileTextures = new Dictionary<int, Texture2D>();
    internal static Texture2D TileTexture(int tile)
    {
      Texture2D t;
      if (tileTextures.TryGetValue(tile, out t) && t != null) return t;
      if (px == null) { var unused = Atlas; }
      int tx = (tile % PerRow) * Tile, ty = (tile / PerRow) * Tile;
      var o = new Color32[Tile * Tile];
      for (int y = 0; y < Tile; y++)
        for (int x = 0; x < Tile; x++)
        {
          Color32 c = px[(ty + y) * Size + tx + x];
          c.a = 255;
          o[y * Tile + x] = c;
        }
      t = new Texture2D(Tile, Tile, TextureFormat.RGBA32, false) { name = "Unknowns Tile " + tile, filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Repeat, hideFlags = HideFlags.DontSave };
      t.SetPixels32(o);
      t.Apply(false, false);
      tileTextures[tile] = t;
      return t;
    }

    // UV rect of a tile (inset half a texel so point sampling never bleeds).
    internal static Rect TileRect(int tile)
    {
      int tx = tile % PerRow, ty = tile / PerRow;
      float inset = 0.02f / Size;
      return new Rect((tx * Tile) / (float)Size + inset, (ty * Tile) / (float)Size + inset, Tile / (float)Size - inset * 2f, Tile / (float)Size - inset * 2f);
    }

    // ---- real Minecraft textures (SbMinecraft reads them from the player's own install)
    private static readonly string[,] McTiles =
    {
      { "GrassTop", "block/grass_block_top", "grass" }, { "GrassSide", "block/grass_block_side", "side" }, { "Dirt", "block/dirt", "" },
      { "Stone", "block/stone", "" }, { "Cobble", "block/cobblestone", "" }, { "Planks", "block/oak_planks", "" },
      { "LogSide", "block/oak_log", "" }, { "LogTop", "block/oak_log_top", "" }, { "Leaves", "block/oak_leaves", "leaves" },
      { "Sand", "block/sand", "" }, { "Gravel", "block/gravel", "" }, { "Glass", "block/glass", "clear" },
      { "Bricks", "block/bricks", "" }, { "StoneBricks", "block/stone_bricks", "" }, { "Bookshelf", "block/bookshelf", "" },
      { "CraftTop", "block/crafting_table_top", "" }, { "CraftSide", "block/crafting_table_side", "" }, { "CraftFront", "block/crafting_table_front", "" },
      { "TntSide", "block/tnt_side", "" }, { "TntTop", "block/tnt_top", "" }, { "TntBottom", "block/tnt_bottom", "" },
      { "Obsidian", "block/obsidian", "" }, { "Bedrock", "block/bedrock", "" }, { "Diamond", "block/diamond_block", "" },
      { "Gold", "block/gold_block", "" }, { "Iron", "block/iron_block", "" }, { "Emerald", "block/emerald_block", "" },
      { "Redstone", "block/redstone_block", "" }, { "Lapis", "block/lapis_block", "" }, { "CoalOre", "block/coal_ore", "" },
      { "IronOre", "block/iron_ore", "" }, { "DiamondOre", "block/diamond_ore", "" }, { "GoldOre", "block/gold_ore", "" },
      { "Glowstone", "block/glowstone", "" }, { "Netherrack", "block/netherrack", "" }, { "SoulSand", "block/soul_sand", "" },
      { "Ice", "block/ice", "clear" }, { "Snow", "block/snow", "" }, { "PumpkinSide", "block/pumpkin_side", "" },
      { "PumpkinTop", "block/pumpkin_top", "" }, { "PumpkinFace", "block/carved_pumpkin", "" }, { "JackFace", "block/jack_o_lantern", "" },
      { "MelonSide", "block/melon_side", "" }, { "MelonTop", "block/melon_top", "" }, { "WoolWhite", "block/white_wool", "" },
      { "WoolRed", "block/red_wool", "" }, { "WoolBlue", "block/blue_wool", "" }, { "WoolGreen", "block/green_wool", "" },
      { "WoolYellow", "block/yellow_wool", "" }, { "WoolBlack", "block/black_wool", "" }, { "HaySide", "block/hay_block_side", "" },
      { "HayTop", "block/hay_block_top", "" }, { "Slime", "block/slime_block", "clear" }, { "SeaLantern", "block/sea_lantern", "" },
      { "Sponge", "block/sponge", "" }, { "Magma", "block/magma", "" }, { "FurnaceFront", "block/furnace_front", "" },
      { "FurnaceSide", "block/furnace_side", "" }, { "FurnaceTop", "block/furnace_top", "" }, { "Water", "block/water_still", "water" },
      { "Lava", "block/lava_still", "" }, { "Mossy", "block/mossy_cobblestone", "" }, { "Quartz", "block/quartz_block_side", "" },
      { "WoolPurple", "block/purple_wool", "" }, { "WoolOrange", "block/orange_wool", "" }, { "WoolPink", "block/pink_wool", "" },
      { "WoolCyan", "block/cyan_wool", "" }, { "EmeraldOre", "block/emerald_ore", "" },
      { "NetherPortal", "block/nether_portal", "clear" }, { "NetherBricks", "block/nether_bricks", "" },
      { "EndStone", "block/end_stone", "" }, { "EndFrameTop", "block/end_portal_frame_top", "" }, { "EndFrameSide", "block/end_portal_frame_side", "clear" },
      { "EndFrameEye", "block/end_portal_frame_eye", "clear" }, { "EndPortal", "entity/end_portal", "" }, { "Fire", "block/fire_0", "clear" },
    };

    internal static readonly string[] McItems = { "item/wooden_sword", "item/stone_sword", "item/iron_sword", "item/golden_sword", "item/diamond_sword", "item/netherite_sword", "item/water_bucket", "item/lava_bucket", "item/flint_and_steel", "item/ender_eye", "item/ender_pearl" };

    // more Minecraft blocks. Texture (under textures/), built-in colour, mode
    // ("" solid, "clear" keeps see-through pixels, "leaf:#rrggbb" tinted leaves with the
    // holes filled, "tint:#rrggbb" tinted). Tile ids follow on from ExtraBase.
    internal const int ExtraBase = 96;
    internal static readonly string[,] ExtraTiles =
    {
      // wood
      { "block/spruce_planks", "#735532", "" }, { "block/spruce_log", "#3b2712", "" }, { "block/spruce_log_top", "#6b4f2c", "" },
      { "block/birch_planks", "#c5b37b", "" }, { "block/birch_log", "#d8d7d2", "" }, { "block/birch_log_top", "#b9a56c", "" },
      { "block/jungle_planks", "#a0734d", "" }, { "block/jungle_log", "#56451b", "" }, { "block/jungle_log_top", "#9f7a4a", "" },
      { "block/acacia_planks", "#a85a32", "" }, { "block/acacia_log", "#676157", "" }, { "block/acacia_log_top", "#a5562f", "" },
      { "block/dark_oak_planks", "#432b14", "" }, { "block/dark_oak_log", "#3c2e1a", "" }, { "block/dark_oak_log_top", "#4a3520", "" },
      { "block/mangrove_planks", "#763631", "" }, { "block/mangrove_log", "#543d29", "" }, { "block/mangrove_log_top", "#6d3a2d", "" },
      { "block/cherry_planks", "#e2b4ad", "" }, { "block/cherry_log", "#3a1f27", "" }, { "block/cherry_log_top", "#b88583", "" },
      { "block/crimson_planks", "#6a344b", "" }, { "block/crimson_stem", "#5d1a1e", "" }, { "block/crimson_stem_top", "#702a3d", "" },
      { "block/warped_planks", "#2b6963", "" }, { "block/warped_stem", "#3a3a4c", "" }, { "block/warped_stem_top", "#356c66", "" },
      { "block/cherry_leaves", "#e5a2c2", "leaf:#ffffff" }, { "block/azalea_leaves", "#5a7a2a", "leaf:#ffffff" },
      { "block/spruce_leaves", "#3d5e3d", "leaf:#619961" }, { "block/birch_leaves", "#5e7f3b", "leaf:#80a755" },
      // stone
      { "block/granite", "#956754", "" }, { "block/polished_granite", "#9a6a55", "" }, { "block/diorite", "#bdbcbd", "" },
      { "block/polished_diorite", "#c1c1c3", "" }, { "block/andesite", "#888889", "" }, { "block/polished_andesite", "#848687", "" },
      { "block/deepslate", "#505053", "" }, { "block/deepslate_top", "#575759", "" }, { "block/cobbled_deepslate", "#4d4d51", "" },
      { "block/polished_deepslate", "#48484a", "" }, { "block/deepslate_bricks", "#464647", "" }, { "block/deepslate_tiles", "#373737", "" },
      { "block/tuff", "#6c6d67", "" }, { "block/calcite", "#dfe0dc", "" }, { "block/smooth_stone", "#9e9e9e", "" },
      { "block/sandstone", "#d8cb9b", "" }, { "block/sandstone_top", "#dfd5aa", "" }, { "block/sandstone_bottom", "#d8cb9b", "" },
      { "block/red_sand", "#bf6721", "" }, { "block/red_sandstone", "#b5621f", "" }, { "block/red_sandstone_top", "#b5621f", "" },
      { "block/red_sandstone_bottom", "#b5621f", "" }, { "block/mossy_stone_bricks", "#737969", "" }, { "block/cracked_stone_bricks", "#767676", "" },
      { "block/chiseled_stone_bricks", "#777777", "" }, { "block/prismarine", "#63a99a", "" }, { "block/prismarine_bricks", "#63ab9e", "" },
      { "block/dark_prismarine", "#335b4b", "" }, { "block/mud_bricks", "#89684f", "" }, { "block/packed_mud", "#8e6b50", "" },
      { "block/end_stone_bricks", "#dadfa3", "" }, { "block/purpur_block", "#a97da9", "" }, { "block/purpur_pillar", "#ab81ab", "" },
      { "block/purpur_pillar_top", "#ac82ac", "" }, { "block/blackstone", "#2a2328", "" }, { "block/blackstone_top", "#2a2328", "" },
      { "block/polished_blackstone_bricks", "#302a30", "" }, { "block/basalt_side", "#49484d", "" }, { "block/basalt_top", "#515156", "" },
      { "block/quartz_bricks", "#eae5dd", "" }, { "block/chiseled_quartz_block", "#e7e2da", "" }, { "block/chiseled_quartz_block_top", "#e7e3db", "" },
      { "block/smooth_basalt", "#48484e", "" }, { "block/dripstone_block", "#866b5c", "" }, { "block/terracotta", "#985e43", "" },
      // ores and minerals
      { "block/deepslate_coal_ore", "#4a4a4c", "" }, { "block/deepslate_iron_ore", "#6a645f", "" }, { "block/deepslate_gold_ore", "#73674a", "" },
      { "block/deepslate_diamond_ore", "#536a6c", "" }, { "block/deepslate_emerald_ore", "#4f6b58", "" }, { "block/redstone_ore", "#8c6d6d", "" },
      { "block/lapis_ore", "#6b7591", "" }, { "block/copper_ore", "#7c7d75", "" }, { "block/copper_block", "#c06c50", "" },
      { "block/exposed_copper", "#a1806a", "" }, { "block/weathered_copper", "#6c9e6d", "" }, { "block/oxidized_copper", "#52a385", "" },
      { "block/raw_iron_block", "#a6876b", "" }, { "block/raw_gold_block", "#dda92e", "" }, { "block/raw_copper_block", "#9a6a4f", "" },
      { "block/coal_block", "#101010", "" }, { "block/netherite_block", "#423d3f", "" }, { "block/amethyst_block", "#8662bf", "" },
      { "block/nether_gold_ore", "#73372a", "" }, { "block/nether_quartz_ore", "#75413e", "" }, { "block/ancient_debris_side", "#5f4239", "" },
      { "block/ancient_debris_top", "#5e4b45", "" },
      // natural
      { "block/coarse_dirt", "#77553b", "" }, { "block/podzol_side", "#7a5a2c", "" }, { "block/podzol_top", "#5c3f19", "" },
      { "block/mycelium_side", "#6f6265", "" }, { "block/mycelium_top", "#6f6265", "" }, { "block/rooted_dirt", "#90674c", "" },
      { "block/moss_block", "#596e2d", "" }, { "block/clay", "#a0a6b3", "" }, { "block/mud", "#3c393d", "" },
      { "block/packed_ice", "#8db4fa", "" }, { "block/blue_ice", "#74a8fd", "" }, { "block/dirt_path_side", "#8f6b44", "" },
      { "block/dirt_path_top", "#94793f", "" },
      // concrete, terracotta, the rest of the wool, stained glass (16 colours)
      { "block/white_concrete", "#cfd5d6", "" }, { "block/orange_concrete", "#e06101", "" }, { "block/magenta_concrete", "#a9309f", "" },
      { "block/light_blue_concrete", "#2489c7", "" }, { "block/yellow_concrete", "#f1af15", "" }, { "block/lime_concrete", "#5ea918", "" },
      { "block/pink_concrete", "#d6658f", "" }, { "block/gray_concrete", "#373a3e", "" }, { "block/light_gray_concrete", "#7d7d73", "" },
      { "block/cyan_concrete", "#157788", "" }, { "block/purple_concrete", "#64209c", "" }, { "block/blue_concrete", "#2d2f8f", "" },
      { "block/brown_concrete", "#603c20", "" }, { "block/green_concrete", "#495b24", "" }, { "block/red_concrete", "#8e2121", "" },
      { "block/black_concrete", "#080a0f", "" },
      { "block/white_terracotta", "#d1b2a1", "" }, { "block/orange_terracotta", "#a15325", "" }, { "block/magenta_terracotta", "#95586c", "" },
      { "block/light_blue_terracotta", "#716d8a", "" }, { "block/yellow_terracotta", "#ba8523", "" }, { "block/lime_terracotta", "#677535", "" },
      { "block/pink_terracotta", "#a14e4e", "" }, { "block/gray_terracotta", "#392a23", "" }, { "block/light_gray_terracotta", "#876a61", "" },
      { "block/cyan_terracotta", "#575b5b", "" }, { "block/purple_terracotta", "#764656", "" }, { "block/blue_terracotta", "#4a3b5b", "" },
      { "block/brown_terracotta", "#4d3323", "" }, { "block/green_terracotta", "#4c532a", "" }, { "block/red_terracotta", "#8f3d2e", "" },
      { "block/black_terracotta", "#251610", "" },
      { "block/light_gray_wool", "#8e8e86", "" }, { "block/gray_wool", "#3e4447", "" }, { "block/brown_wool", "#724728", "" },
      { "block/lime_wool", "#70b919", "" }, { "block/light_blue_wool", "#3aafd9", "" }, { "block/magenta_wool", "#bd44b3", "" },
      { "block/white_stained_glass", "#ffffff", "clear" }, { "block/orange_stained_glass", "#d87f33", "clear" }, { "block/magenta_stained_glass", "#b24cd8", "clear" },
      { "block/light_blue_stained_glass", "#6699d8", "clear" }, { "block/yellow_stained_glass", "#e5e533", "clear" }, { "block/lime_stained_glass", "#7fcc19", "clear" },
      { "block/pink_stained_glass", "#f27fa5", "clear" }, { "block/gray_stained_glass", "#4c4c4c", "clear" }, { "block/light_gray_stained_glass", "#999999", "clear" },
      { "block/cyan_stained_glass", "#4c7f99", "clear" }, { "block/purple_stained_glass", "#7f3fb2", "clear" }, { "block/blue_stained_glass", "#334cb2", "clear" },
      { "block/brown_stained_glass", "#664c33", "clear" }, { "block/green_stained_glass", "#667f33", "clear" }, { "block/red_stained_glass", "#993333", "clear" },
      { "block/black_stained_glass", "#191919", "clear" },
      // nether
      { "block/crimson_nylium", "#831f1f", "" }, { "block/crimson_nylium_side", "#6b2a2a", "" }, { "block/warped_nylium", "#2b7265", "" },
      { "block/warped_nylium_side", "#41403f", "" }, { "block/nether_wart_block", "#730303", "" }, { "block/warped_wart_block", "#167979", "" },
      { "block/shroomlight", "#f19347", "" }, { "block/crying_obsidian", "#200a3c", "" }, { "block/soul_soil", "#4c3a2e", "" },
      { "block/red_nether_bricks", "#450709", "" }, { "block/gilded_blackstone", "#382b26", "" },
      // light
      { "block/ochre_froglight_side", "#f5e9a6", "" }, { "block/ochre_froglight_top", "#fbf5cf", "" }, { "block/verdant_froglight_side", "#d4f2c2", "" },
      { "block/verdant_froglight_top", "#e5f4e4", "" }, { "block/pearlescent_froglight_side", "#f0dce6", "" }, { "block/pearlescent_froglight_top", "#f5f0ef", "" },
      { "block/redstone_lamp_on", "#c67c3e", "" },
      // functional
      { "block/jukebox_side", "#58392a", "" }, { "block/jukebox_top", "#5b3c2c", "" }, { "block/note_block", "#58392a", "" },
      { "block/target_side", "#e5b0a8", "" }, { "block/target_top", "#e3aba0", "" }, { "block/barrel_side", "#6c5131", "" },
      { "block/barrel_top", "#86643b", "" }, { "block/dispenser_front", "#7a7a7a", "" }, { "block/honey_block_side", "#fbb931", "clear" },
      { "block/honey_block_top", "#fbbf35", "clear" },
      // more blocks
      // wood
      { "block/pale_oak_planks", "#e4dad8", "" }, { "block/pale_oak_log", "#57504d", "" }, { "block/pale_oak_log_top", "#c9bcb8", "" },
      { "block/bamboo_planks", "#c4ad4d", "" }, { "block/bamboo_mosaic", "#bea84b", "" }, { "block/bamboo_block", "#7f8f2f", "" },
      { "block/bamboo_block_top", "#8f9a3a", "" }, { "block/stripped_oak_log", "#b29157", "" }, { "block/stripped_oak_log_top", "#a8874f", "" },
      { "block/stripped_spruce_log", "#73593a", "" }, { "block/stripped_spruce_log_top", "#6b5334", "" }, { "block/stripped_birch_log", "#c4b077", "" },
      { "block/stripped_birch_log_top", "#b9a56d", "" }, { "block/stripped_jungle_log", "#ab8455", "" }, { "block/stripped_jungle_log_top", "#a17c4e", "" },
      { "block/stripped_acacia_log", "#ae5d3b", "" }, { "block/stripped_acacia_log_top", "#a55837", "" }, { "block/stripped_dark_oak_log", "#604c32", "" },
      { "block/stripped_dark_oak_log_top", "#59462e", "" }, { "block/stripped_mangrove_log", "#7a3a33", "" }, { "block/stripped_mangrove_log_top", "#73372f", "" },
      { "block/stripped_cherry_log", "#d6919b", "" }, { "block/stripped_cherry_log_top", "#cc8a92", "" }, { "block/stripped_pale_oak_log", "#f1eaea", "" },
      { "block/stripped_pale_oak_log_top", "#e8dfde", "" }, { "block/stripped_crimson_stem", "#8a3a5a", "" }, { "block/stripped_crimson_stem_top", "#7f3553", "" },
      { "block/stripped_warped_stem", "#3a9690", "" }, { "block/stripped_warped_stem_top", "#368a84", "" }, { "block/stripped_bamboo_block", "#c2ae4e", "" },
      { "block/stripped_bamboo_block_top", "#b8a44a", "" },
      { "block/acacia_leaves", "#5e8a24", "leaf:#77ab2f" }, { "block/dark_oak_leaves", "#3e7a20", "leaf:#77ab2f" }, { "block/jungle_leaves", "#3c8a1c", "leaf:#77ab2f" },
      { "block/mangrove_leaves", "#5a8a24", "leaf:#92c648" }, { "block/pale_oak_leaves", "#a0a69c", "leaf:#ffffff" },
      // stone
      { "block/polished_blackstone", "#36303a", "" }, { "block/chiseled_polished_blackstone", "#35303a", "" }, { "block/cracked_polished_blackstone_bricks", "#2c2629", "" },
      { "block/chiseled_deepslate", "#36363a", "" }, { "block/cracked_deepslate_bricks", "#404042", "" }, { "block/cracked_deepslate_tiles", "#343434", "" },
      { "block/tuff_bricks", "#626860", "" }, { "block/polished_tuff", "#616863", "" }, { "block/chiseled_tuff", "#5e6460", "" },
      { "block/chiseled_tuff_top", "#5e6460", "" }, { "block/chiseled_tuff_bricks", "#626860", "" }, { "block/chiseled_tuff_bricks_top", "#626860", "" },
      { "block/cut_sandstone", "#d9cf9e", "" }, { "block/chiseled_sandstone", "#d8ca9b", "" }, { "block/cut_red_sandstone", "#bd6522", "" },
      { "block/chiseled_red_sandstone", "#b7601f", "" }, { "block/quartz_pillar", "#ebe5de", "" }, { "block/quartz_pillar_top", "#e9e3db", "" },
      { "block/polished_basalt_side", "#59595e", "" }, { "block/polished_basalt_top", "#636367", "" }, { "block/cracked_nether_bricks", "#2c1519", "" },
      { "block/chiseled_nether_bricks", "#2f171b", "" }, { "block/resin_block", "#d9631c", "" }, { "block/resin_bricks", "#ce5a1c", "" },
      { "block/chiseled_resin_bricks", "#c8561a", "" }, { "block/reinforced_deepslate_side", "#5a5e58", "" }, { "block/reinforced_deepslate_top", "#505350", "" },
      { "block/reinforced_deepslate_bottom", "#505350", "" },
      // ores and minerals
      { "block/deepslate_copper_ore", "#5c5d58", "" }, { "block/deepslate_lapis_ore", "#4f5a78", "" }, { "block/deepslate_redstone_ore", "#684648", "" },
      { "block/cut_copper", "#bf6b51", "" }, { "block/exposed_cut_copper", "#9a7965", "" }, { "block/weathered_cut_copper", "#6d9166", "" },
      { "block/oxidized_cut_copper", "#4f9a7e", "" }, { "block/chiseled_copper", "#b8654c", "" }, { "block/exposed_chiseled_copper", "#9a7965", "" },
      { "block/weathered_chiseled_copper", "#6d9166", "" }, { "block/oxidized_chiseled_copper", "#4f9a7e", "" }, { "block/copper_grate", "#c06c50", "clear" },
      { "block/exposed_copper_grate", "#a1806a", "clear" }, { "block/weathered_copper_grate", "#6c9e6d", "clear" }, { "block/oxidized_copper_grate", "#52a385", "clear" },
      { "block/copper_bulb", "#c47a50", "" }, { "block/exposed_copper_bulb", "#a1806a", "" }, { "block/weathered_copper_bulb", "#6c9e6d", "" },
      { "block/oxidized_copper_bulb", "#52a385", "" }, { "block/budding_amethyst", "#8a5fc0", "" },
      // natural
      { "block/sculk", "#0d1e24", "" }, { "block/powder_snow", "#f8fdfd", "" }, { "block/farmland_moist", "#52341c", "" },
      { "block/grass_block_snow", "#d8e4e4", "" }, { "block/suspicious_sand_0", "#d8ce9a", "" }, { "block/suspicious_gravel_0", "#857c7a", "" },
      { "block/muddy_mangrove_roots_side", "#463a2c", "" }, { "block/muddy_mangrove_roots_top", "#463a2c", "" }, { "block/pale_moss_block", "#6c7068", "" },
      { "block/dried_kelp_side", "#323a26", "" }, { "block/dried_kelp_top", "#333a26", "" }, { "block/dried_kelp_bottom", "#333a26", "" },
      { "block/bone_block_side", "#e5e1cf", "" }, { "block/bone_block_top", "#d2cdb6", "" }, { "block/red_mushroom_block", "#c82f2d", "" },
      { "block/brown_mushroom_block", "#956f51", "" }, { "block/mushroom_stem", "#cbc4b9", "" }, { "block/mushroom_block_inside", "#c9aa78", "" },
      { "block/honeycomb_block", "#e5941e", "" }, { "block/wet_sponge", "#aab446", "" }, { "block/tinted_glass", "#2c2730", "clear" },
      { "block/tube_coral_block", "#3157cf", "" }, { "block/brain_coral_block", "#cf5b9f", "" }, { "block/bubble_coral_block", "#a51ba2", "" },
      { "block/fire_coral_block", "#a4232f", "" }, { "block/horn_coral_block", "#d8c843", "" }, { "block/dead_tube_coral_block", "#827b77", "" },
      { "block/dead_brain_coral_block", "#7c7570", "" }, { "block/dead_bubble_coral_block", "#847d78", "" }, { "block/dead_fire_coral_block", "#867e79", "" },
      { "block/dead_horn_coral_block", "#857e78", "" },
      // concrete powder and glazed terracotta (16 colours)
      { "block/white_concrete_powder", "#e1e3e3", "" }, { "block/orange_concrete_powder", "#e3831f", "" }, { "block/magenta_concrete_powder", "#c053b8", "" },
      { "block/light_blue_concrete_powder", "#4ab4d5", "" }, { "block/yellow_concrete_powder", "#e8c736", "" }, { "block/lime_concrete_powder", "#7dbd29", "" },
      { "block/pink_concrete_powder", "#e499b5", "" }, { "block/gray_concrete_powder", "#4c5154", "" }, { "block/light_gray_concrete_powder", "#9a9a94", "" },
      { "block/cyan_concrete_powder", "#24939d", "" }, { "block/purple_concrete_powder", "#8337b1", "" }, { "block/blue_concrete_powder", "#4649a6", "" },
      { "block/brown_concrete_powder", "#7d5435", "" }, { "block/green_concrete_powder", "#61772c", "" }, { "block/red_concrete_powder", "#a83632", "" },
      { "block/black_concrete_powder", "#191a1f", "" },
      { "block/white_glazed_terracotta", "#bcd4ca", "" }, { "block/orange_glazed_terracotta", "#9a933a", "" }, { "block/magenta_glazed_terracotta", "#d064bf", "" },
      { "block/light_blue_glazed_terracotta", "#5ea4d0", "" }, { "block/yellow_glazed_terracotta", "#eac058", "" }, { "block/lime_glazed_terracotta", "#a2c537", "" },
      { "block/pink_glazed_terracotta", "#eb9ab5", "" }, { "block/gray_glazed_terracotta", "#535a5d", "" }, { "block/light_gray_glazed_terracotta", "#90a6a7", "" },
      { "block/cyan_glazed_terracotta", "#34767d", "" }, { "block/purple_glazed_terracotta", "#6d309c", "" }, { "block/blue_glazed_terracotta", "#2f418b", "" },
      { "block/brown_glazed_terracotta", "#77634f", "" }, { "block/green_glazed_terracotta", "#758e43", "" }, { "block/red_glazed_terracotta", "#b53b35", "" },
      { "block/black_glazed_terracotta", "#431e20", "" },
      // functional
      { "block/smithing_table_top", "#39283a", "" }, { "block/smithing_table_side", "#3a2a26", "" }, { "block/smithing_table_front", "#3a2a26", "" },
      { "block/smithing_table_bottom", "#4a3424", "" }, { "block/fletching_table_top", "#c5b583", "" }, { "block/fletching_table_side", "#a68b5c", "" },
      { "block/fletching_table_front", "#a68b5c", "" }, { "block/cartography_table_top", "#5c4a36", "" }, { "block/cartography_table_side3", "#5c4a36", "" },
      { "block/cartography_table_side1", "#5c4a36", "" }, { "block/loom_top", "#8e7a5a", "" }, { "block/loom_side", "#8e6a46", "" },
      { "block/loom_front", "#8e6a46", "" }, { "block/loom_bottom", "#6a5034", "" }, { "block/smoker_top", "#555252", "" },
      { "block/smoker_side", "#6a5a48", "" }, { "block/smoker_front", "#5a4c3c", "" }, { "block/smoker_bottom", "#5a5a5a", "" },
      { "block/blast_furnace_top", "#525252", "" }, { "block/blast_furnace_side", "#6c6c6c", "" }, { "block/blast_furnace_front", "#5a5a5a", "" },
      { "block/observer_top", "#5c5c5c", "" }, { "block/observer_side", "#686868", "" }, { "block/observer_front", "#4a4a4a", "" },
      { "block/observer_back", "#5a5a5a", "" }, { "block/piston_top", "#9e8a62", "" }, { "block/piston_top_sticky", "#7aa060", "" },
      { "block/piston_side", "#6e6a62", "" }, { "block/piston_bottom", "#666666", "" }, { "block/lodestone_top", "#8e9094", "" },
      { "block/lodestone_side", "#77797e", "" }, { "block/bee_nest_top", "#c8a24a", "" }, { "block/bee_nest_side", "#c6a046", "" },
      { "block/bee_nest_front", "#c09840", "" }, { "block/bee_nest_bottom", "#a07a3a", "" }, { "block/beehive_end", "#b4925a", "" },
      { "block/beehive_side", "#9e7a4a", "" }, { "block/beehive_front", "#9e7a4a", "" }, { "block/chiseled_bookshelf_top", "#b0905a", "" },
      { "block/chiseled_bookshelf_side", "#a8844e", "" }, { "block/chiseled_bookshelf_occupied", "#8a5a3a", "" }, { "block/dropper_front", "#6a6a6a", "" },
      { "block/crafter_top", "#6a6a6a", "" }, { "block/crafter_north", "#6a6a6a", "" }, { "block/crafter_east", "#6a6a6a", "" },
      { "block/crafter_bottom", "#5a5a5a", "" }, { "block/respawn_anchor_top_off", "#2a1838", "" }, { "block/respawn_anchor_side0", "#2a1838", "" },
      { "block/respawn_anchor_bottom", "#2a1838", "" }, { "block/sculk_catalyst_top", "#1a2a30", "" }, { "block/sculk_catalyst_side", "#3a4040", "" },
      { "block/sculk_catalyst_bottom", "#4a4a4a", "" }, { "block/command_block_front", "#b88a5e", "" }, { "block/command_block_side", "#b88a5e", "" },
      { "block/command_block_back", "#b88a5e", "" }, { "block/repeating_command_block_front", "#6a4ab8", "" }, { "block/repeating_command_block_side", "#6a4ab8", "" },
      { "block/repeating_command_block_back", "#6a4ab8", "" }, { "block/structure_block", "#5a4a5a", "" }, { "block/jigsaw_top", "#4a3a4a", "" },
      { "block/jigsaw_side", "#4a3a4a", "" }, { "block/jigsaw_bottom", "#4a3a4a", "" }, { "block/spawner", "#1a2a36", "clear" },
      { "block/trial_spawner_top_inactive", "#3a4040", "clear" }, { "block/trial_spawner_side_inactive", "#3a4040", "clear" }, { "block/trial_spawner_bottom", "#3a4040", "" },
      { "block/vault_top", "#3a4040", "" }, { "block/vault_side_off", "#3a4040", "" }, { "block/vault_front_off", "#3a4040", "" },
      { "block/vault_bottom", "#3a4040", "" }, { "block/creaking_heart", "#4a3a30", "" }, { "block/creaking_heart_top", "#5a4a3a", "" },
      { "block/redstone_lamp", "#5a3a20", "" },
      { "block/oak_door_top", "#9a7a4a", "clear" }, { "block/oak_door_bottom", "#8a6a3a", "clear" }, { "block/iron_door_top", "#c8c8c8", "clear" },
      { "block/iron_door_bottom", "#bcbcbc", "clear" },
      { "block/rail", "#8a7a5a", "clear" }, { "block/powered_rail_on", "#c8a03a", "clear" },
      { "block/repeater", "#a0a0a0", "" }, { "block/comparator", "#a0a0a0", "" }, { "block/redstone_dust_dot", "#d01010", "tintclear:#e01818" },
      { "block/redstone_torch", "#d02020", "clear" }, { "block/redstone_torch_off", "#6a3a2a", "clear" }, { "block/lever", "#7a5a32", "clear" },
      { "block/redstone_dust_line0", "#d01010", "tintclear:#e01818" },
    };

    private static Dictionary<string, int> extraIndex;

    // the tile of an extra block by its short name ("spruce_planks"), stone when unknown
    internal static int T(string name)
    {
      if (extraIndex == null)
      {
        extraIndex = new Dictionary<string, int>(StringComparer.Ordinal);
        for (int i = 0; i < ExtraTiles.GetLength(0); i++) extraIndex[ExtraTiles[i, 0].Substring("block/".Length)] = ExtraBase + i;
      }
      int t;
      return extraIndex.TryGetValue(name, out t) ? t : Stone;
    }

    private static void PaintExtras()
    {
      for (int i = 0; i < ExtraTiles.GetLength(0); i++)
      {
        Color c = Hex(ExtraTiles[i, 1]);
        bool glass = ExtraTiles[i, 2] == "clear" && ExtraTiles[i, 0].Contains("glass");
        Noise(ExtraBase + i, c, glass ? 0.04f : 0.12f, 500 + i * 7, 5);
        if (glass)
        {
          curTile = ExtraBase + i;
          for (int y = 0; y < Tile; y++)
            for (int x = 0; x < Tile; x++)
            {
              bool edge = x == 0 || y == 0 || x == Tile - 1 || y == Tile - 1;
              Set(x, y, new Color(c.r, c.g, c.b, edge ? 0.9f : 0.45f));
            }
        }
      }
    }

    internal static IEnumerable<string> McFiles()
    {
      for (int i = 0; i < ExtraTiles.GetLength(0); i++) yield return "textures/" + ExtraTiles[i, 0] + ".png";
      for (int i = 0; i < McTiles.GetLength(0); i++) yield return "textures/" + McTiles[i, 1] + ".png";
      yield return "textures/block/grass_block_side_overlay.png";
      foreach (string s in McItems) yield return "textures/" + s + ".png";
      foreach (string s in SbMobDefs.TextureFiles()) yield return s;
      foreach (string s in SbMcFx.TextureFiles()) yield return s;   // beds, armour, items, fireballs, explosions (SandBoxMcModels.cs)
      yield return "textures/environment/end_sky.png";   // the End portal (SbEndPortalArt)
    }

    internal static bool UsingMinecraft;

    private static void ApplyMinecraft()
    {
      SbMinecraft.LoadTextures(McFiles());
      Color grass = new Color(0.569f, 0.741f, 0.349f), leaves = new Color(0.467f, 0.671f, 0.184f), water = new Color(0.247f, 0.463f, 0.894f);
      var fields = typeof(SbBlockArt);
      int applied = 0;
      for (int i = 0; i < McTiles.GetLength(0); i++)
      {
        Color32[] px = SbMinecraft.Frame16("textures/" + McTiles[i, 1] + ".png");
        if (px == null) continue;
        var f = fields.GetField(McTiles[i, 0], System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public);
        if (f == null) continue;
        int tile = (int)f.GetValue(null);
        string mode = McTiles[i, 2];
        Color32[] overlay = mode == "side" ? SbMinecraft.Frame16("textures/block/grass_block_side_overlay.png") : null;
        curTile = tile;
        for (int y = 0; y < Tile; y++)
          for (int x = 0; x < Tile; x++)
          {
            Color c = px[y * 16 + x];
            if (mode == "grass") c = new Color(c.r * grass.r, c.g * grass.g, c.b * grass.b, 1f);
            else if (mode == "leaves") c = c.a < 0.5f ? new Color(0.12f, 0.2f, 0.05f, 1f) : new Color(c.r * leaves.r, c.g * leaves.g, c.b * leaves.b, 1f);
            else if (mode == "water") c = new Color(c.r * water.r, c.g * water.g, c.b * water.b, 0.7f);
            else if (mode != "clear") c.a = 1f;
            if (overlay != null)
            {
              Color o = overlay[y * 16 + x];
              if (o.a > 0.5f) c = new Color(o.r * grass.r, o.g * grass.g, o.b * grass.b, 1f);
            }
            Set(x, y, c);
          }
        applied++;
      }
      // the extra blocks
      for (int i = 0; i < ExtraTiles.GetLength(0); i++)
      {
        Color32[] px = SbMinecraft.Frame16("textures/" + ExtraTiles[i, 0] + ".png");
        if (px == null) continue;
        string mode = ExtraTiles[i, 2];
        bool leaf = mode.StartsWith("leaf:", StringComparison.Ordinal), tint = mode.StartsWith("tint:", StringComparison.Ordinal);
        // tinted and see-through (redstone dust)
        bool tintClear = mode.StartsWith("tintclear:", StringComparison.Ordinal);
        Color tc = leaf || tint ? Hex(mode.Substring(5)) : tintClear ? Hex(mode.Substring(10)) : Color.white;
        curTile = ExtraBase + i;
        for (int y = 0; y < Tile; y++)
          for (int x = 0; x < Tile; x++)
          {
            Color c = px[y * 16 + x];
            if (leaf) c = c.a < 0.5f ? new Color(tc.r * 0.25f, tc.g * 0.3f, tc.b * 0.2f, 1f) : new Color(c.r * tc.r, c.g * tc.g, c.b * tc.b, 1f);
            else if (tint) c = new Color(c.r * tc.r, c.g * tc.g, c.b * tc.b, 1f);
            else if (tintClear) c = new Color(c.r * tc.r, c.g * tc.g, c.b * tc.b, c.a);
            else if (mode != "clear") c.a = 1f;
            Set(x, y, c);
          }
        applied++;
      }
      UsingMinecraft = applied > 0;
    }

    // ---- painting helpers
    private static int curTile;
    private static void Set(int x, int y, Color c)
    {
      if (x < 0 || y < 0 || x >= Tile || y >= Tile) return;
      int tx = (curTile % PerRow) * Tile, ty = (curTile / PerRow) * Tile;
      // y=0 is the TOP row of the picture (easier to draw), texture rows go up.
      px[(ty + (Tile - 1 - y)) * Size + tx + x] = c;
    }
    private static Color Get(int x, int y)
    {
      int tx = (curTile % PerRow) * Tile, ty = (curTile / PerRow) * Tile;
      return px[(ty + (Tile - 1 - Mathf.Clamp(y, 0, 15))) * Size + tx + Mathf.Clamp(x, 0, 15)];
    }
    private static float H(int x, int y, int seed)
    {
      unchecked
      {
        uint h = (uint)(x * 374761393 + y * 668265263 + seed * 1274126177);
        h = (h ^ (h >> 13)) * 1274126177u;
        h ^= h >> 16;
        return (h & 0xFFFF) / 65535f;
      }
    }
    private static Color Shade(Color c, float f) { return new Color(Mathf.Clamp01(c.r * f), Mathf.Clamp01(c.g * f), Mathf.Clamp01(c.b * f), c.a); }
    private static Color Hex(string hex)
    {
      Color c; ColorUtility.TryParseHtmlString(hex, out c); return c;
    }
    private static void Noise(int tile, Color baseC, float amount, int seed, int levels = 4)
    {
      curTile = tile;
      for (int y = 0; y < Tile; y++)
        for (int x = 0; x < Tile; x++)
        {
          float n = Mathf.Floor(H(x, y, seed) * levels) / (levels - 1);
          Set(x, y, Shade(baseC, 1f - amount + n * amount * 2f));
        }
    }
    private static void Copy(int from, int to)
    {
      int fx = (from % PerRow) * Tile, fy = (from / PerRow) * Tile;
      int tx = (to % PerRow) * Tile, ty = (to / PerRow) * Tile;
      for (int y = 0; y < Tile; y++)
        for (int x = 0; x < Tile; x++)
          px[(ty + y) * Size + tx + x] = px[(fy + y) * Size + fx + x];
    }
    private static void Speckle(int tile, Color c, int seed, float chance, int clusters = 0)
    {
      curTile = tile;
      if (clusters > 0)
      {
        for (int k = 0; k < clusters; k++)
        {
          int cx = 2 + (int)(H(k, 1, seed) * 11), cy = 2 + (int)(H(k, 2, seed) * 11);
          for (int y = -1; y <= 1; y++)
            for (int x = -1; x <= 1; x++)
              if (H(cx + x, cy + y, seed + 7) < 0.72f)
                Set(cx + x, cy + y, Shade(c, 0.85f + H(cx + x, cy + y, seed + 9) * 0.3f));
        }
        return;
      }
      for (int y = 0; y < Tile; y++)
        for (int x = 0; x < Tile; x++)
          if (H(x, y, seed) < chance) Set(x, y, Shade(c, 0.85f + H(x, y, seed + 3) * 0.3f));
    }
    private static void Border(int tile, Color c, int w = 1)
    {
      curTile = tile;
      for (int i = 0; i < Tile; i++)
        for (int k = 0; k < w; k++)
        {
          Set(i, k, c); Set(i, Tile - 1 - k, c); Set(k, i, c); Set(Tile - 1 - k, i, c);
        }
    }
    private static void Metal(int tile, Color c, int seed)
    {
      curTile = tile;
      for (int y = 0; y < Tile; y++)
        for (int x = 0; x < Tile; x++)
        {
          float f = 1f + (H(x, y, seed) - 0.5f) * 0.08f;
          if (x == 0 || y == 0) f = 1.25f;
          else if (x == 15 || y == 15) f = 0.72f;
          else if ((x + y) % 7 == 0 && x > 2 && y > 2 && x < 13 && y < 13) f = 1.12f;
          Set(x, y, Shade(c, f));
        }
    }
    private static void Wool(int tile, Color c, int seed)
    {
      curTile = tile;
      for (int y = 0; y < Tile; y++)
        for (int x = 0; x < Tile; x++)
        {
          float f = 0.92f + H(x / 2, y, seed) * 0.12f + (((x + y * 3) % 5 == 0) ? -0.06f : 0f);
          Set(x, y, Shade(c, f));
        }
    }
    private static void Ore(int tile, Color c, int seed)
    {
      Copy(Stone, tile);
      Speckle(tile, c, seed, 0f, 4);
    }

    private static void PaintAll()
    {
      Color dirt = Hex("#866043"), grass = Hex("#6aaa40"), stone = Hex("#7d7d7d");

      Noise(Dirt, dirt, 0.16f, 11, 5);
      curTile = Dirt;
      for (int k = 0; k < 9; k++) Set((int)(H(k, 3, 12) * 16), (int)(H(k, 4, 12) * 16), Hex("#5e4330"));

      Noise(GrassTop, grass, 0.18f, 21, 5);
      Copy(Dirt, GrassSide);
      curTile = GrassSide;
      for (int x = 0; x < Tile; x++)
      {
        int depth = 3 + (int)(H(x, 0, 31) * 2.4f);
        for (int y = 0; y < depth; y++) Set(x, y, Shade(grass, 0.88f + H(x, y, 33) * 0.24f));
      }

      Noise(Stone, stone, 0.12f, 41, 5);
      curTile = Stone;
      for (int k = 0; k < 6; k++)
      {
        int sx = (int)(H(k, 9, 42) * 13), sy = (int)(H(k, 10, 42) * 15);
        for (int i = 0; i < 3; i++) Set(sx + i, sy, Shade(stone, 0.78f));
      }

      // Cobblestone: rounded stones with dark gaps.
      curTile = Cobble;
      for (int y = 0; y < Tile; y++)
        for (int x = 0; x < Tile; x++)
        {
          // nearest of a few seed points
          float best = 99f, second = 99f; int bi = 0;
          for (int k = 0; k < 9; k++)
          {
            float sx = (k % 3) * 5.5f + 2f + H(k, 1, 51) * 2f, sy = (k / 3) * 5.5f + 2f + H(k, 2, 51) * 2f;
            float d = Mathf.Min(Mathf.Abs(x - sx), 16 - Mathf.Abs(x - sx)) + Mathf.Min(Mathf.Abs(y - sy), 16 - Mathf.Abs(y - sy)) * 1.1f;
            if (d < best) { second = best; best = d; bi = k; }
            else if (d < second) second = d;
          }
          bool edge = second - best < 1.1f;
          float f = edge ? 0.55f : 0.86f + H(bi, 5, 52) * 0.3f - best * 0.03f;
          Set(x, y, Shade(stone, f));
        }
      Copy(Cobble, Mossy);
      curTile = Mossy;
      for (int y = 0; y < Tile; y++)
        for (int x = 0; x < Tile; x++)
          if (H(x / 2, y / 2, 61) < 0.38f) Set(x, y, Shade(Hex("#5b7a3a"), 0.85f + H(x, y, 62) * 0.3f));

      // Planks
      Color plank = Hex("#a0814d");
      curTile = Planks;
      for (int y = 0; y < Tile; y++)
        for (int x = 0; x < Tile; x++)
        {
          int row = y / 4;
          bool seam = y % 4 == 3 || ((x + (row % 2 == 0 ? 0 : 8)) % 16 == 0 && x != 0);
          float grain = H(x / 3, y, 71 + row) * 0.12f;
          Set(x, y, seam ? Shade(plank, 0.62f) : Shade(plank, 0.92f + grain));
        }

      Color bark = Hex("#6b5232");
      curTile = LogSide;
      for (int y = 0; y < Tile; y++)
        for (int x = 0; x < Tile; x++)
        {
          float stripe = (x % 4 == 0 || (x % 4 == 2 && H(x, y / 3, 81) < 0.4f)) ? 0.7f : 0.95f + H(x, y, 82) * 0.12f;
          Set(x, y, Shade(bark, stripe));
        }
      curTile = LogTop;
      for (int y = 0; y < Tile; y++)
        for (int x = 0; x < Tile; x++)
        {
          float d = Mathf.Max(Mathf.Abs(x - 7.5f), Mathf.Abs(y - 7.5f));
          Color c = d > 6.5f ? bark : Shade(Hex("#b8945f"), ((int)d % 2 == 0) ? 0.86f : 1f);
          Set(x, y, c);
        }

      // Leaves (opaque, "fast graphics")
      Color leaf = Hex("#3e8a2a");
      curTile = Leaves;
      for (int y = 0; y < Tile; y++)
        for (int x = 0; x < Tile; x++)
        {
          float n = H(x, y, 91);
          Set(x, y, n < 0.18f ? Shade(leaf, 0.35f) : Shade(leaf, 0.75f + n * 0.45f));
        }

      Noise(Sand, Hex("#dbcf9a"), 0.07f, 101, 4);
      curTile = Gravel;
      for (int y = 0; y < Tile; y++)
        for (int x = 0; x < Tile; x++)
        {
          float n = H(x / 2, y / 2, 111);
          Color c = n < 0.3f ? Hex("#6e6a69") : n < 0.65f ? Hex("#8a8584") : n < 0.9f ? Hex("#a29d9b") : Hex("#5a5352");
          Set(x, y, Shade(c, 0.95f + H(x, y, 112) * 0.1f));
        }

      // Glass: clear with a frame and two glints
      curTile = Glass;
      for (int y = 0; y < Tile; y++)
        for (int x = 0; x < Tile; x++)
          Set(x, y, new Color(0.75f, 0.9f, 0.95f, 0.10f));
      Border(Glass, new Color(0.86f, 0.95f, 1f, 0.95f));
      for (int i = 0; i < 4; i++) { Set(3 + i, 4 + i, new Color(1f, 1f, 1f, 0.75f)); Set(9 + i, 3 + i, new Color(1f, 1f, 1f, 0.55f)); }

      // Bricks
      Color brick = Hex("#965a48"), mortar = Hex("#b7a89f");
      curTile = Bricks;
      for (int y = 0; y < Tile; y++)
        for (int x = 0; x < Tile; x++)
        {
          int row = y / 4;
          bool m = y % 4 == 3 || ((x + (row % 2 == 0 ? 0 : 4)) % 8 == 7);
          Set(x, y, m ? Shade(mortar, 0.95f + H(x, y, 121) * 0.1f) : Shade(brick, 0.88f + H(x, y, 122) * 0.22f));
        }
      curTile = StoneBricks;
      for (int y = 0; y < Tile; y++)
        for (int x = 0; x < Tile; x++)
        {
          int row = y / 8;
          bool m = y % 8 == 7 || ((x + (row % 2 == 0 ? 0 : 8)) % 16 == 15);
          bool hi = y % 8 == 0 || ((x + (row % 2 == 0 ? 0 : 8)) % 16 == 0);
          Set(x, y, m ? Shade(stone, 0.55f) : hi ? Shade(stone, 1.12f) : Shade(stone, 0.92f + H(x, y, 131) * 0.12f));
        }

      // Bookshelf
      Copy(Planks, Bookshelf);
      curTile = Bookshelf;
      Color[] books = { Hex("#8c2b2b"), Hex("#2b4d8c"), Hex("#3c7a2e"), Hex("#8c6b2b"), Hex("#5e2b8c"), Hex("#2b7a7a") };
      for (int shelf = 0; shelf < 2; shelf++)
      {
        int y0 = 1 + shelf * 8;
        for (int x = 1; x < 15; x++)
        {
          Color b = books[(int)(H(x / 2, shelf, 141) * books.Length) % books.Length];
          int top = y0 + (int)(H(x / 2, shelf, 142) * 2f);
          for (int y = top; y < y0 + 6; y++) Set(x, y, Shade(b, x % 2 == 0 ? 1f : 0.82f));
        }
      }

      // Crafting table
      Copy(Planks, CraftTop);
      curTile = CraftTop;
      for (int i = 0; i < 16; i++) { Set(i, 0, Shade(plank, 0.6f)); Set(i, 15, Shade(plank, 0.6f)); Set(0, i, Shade(plank, 0.6f)); Set(15, i, Shade(plank, 0.6f)); }
      for (int i = 2; i < 14; i++) { Set(i, 5, Shade(plank, 0.55f)); Set(i, 10, Shade(plank, 0.55f)); Set(5, i, Shade(plank, 0.55f)); Set(10, i, Shade(plank, 0.55f)); }
      Copy(Planks, CraftSide);
      curTile = CraftSide;
      for (int x = 0; x < 16; x++) for (int y = 0; y < 3; y++) Set(x, y, Shade(Hex("#7a5a34"), 0.9f + H(x, y, 151) * 0.1f));
      for (int x = 3; x < 6; x++) for (int y = 5; y < 13; y++) Set(x, y, Shade(Hex("#8f8f8f"), 1f));   // saw
      for (int x = 9; x < 13; x++) for (int y = 6; y < 9; y++) Set(x, y, Shade(Hex("#a0a0a0"), 1f));  // hammer head
      for (int y = 9; y < 14; y++) Set(10, y, Hex("#6b4a2a"));
      Copy(CraftSide, CraftFront);

      // TNT
      curTile = TntSide;
      for (int y = 0; y < Tile; y++)
        for (int x = 0; x < Tile; x++)
        {
          bool band = y >= 5 && y <= 10;
          Color c = band ? Hex("#e8e8e8") : Shade(Hex("#d93a2a"), (x % 4 == 0) ? 0.78f : 0.95f + H(x, y, 161) * 0.1f);
          Set(x, y, c);
        }
      // "TNT" letters (3 px wide each)
      int[,] tnt = {
        {1,1,1,0,1,0,0,1,0,1,1,1},
        {0,1,0,0,1,1,0,1,0,0,1,0},
        {0,1,0,0,1,0,1,1,0,0,1,0},
        {0,1,0,0,1,0,0,1,0,0,1,0},
      };
      for (int r = 0; r < 4; r++) for (int c = 0; c < 12; c++) if (tnt[r, c] == 1) Set(2 + c, 6 + r, Hex("#222222"));
      curTile = TntTop;
      for (int y = 0; y < Tile; y++)
        for (int x = 0; x < Tile; x++)
        {
          float d = Vector2.Distance(new Vector2(x, y), new Vector2(7.5f, 7.5f));
          Set(x, y, d < 2f ? Hex("#3a3a3a") : Shade(Hex("#d93a2a"), ((x / 4 + y / 4) % 2 == 0) ? 0.92f : 0.8f));
        }
      Noise(TntBottom, Hex("#c0352a"), 0.08f, 171, 3);

      // Obsidian / bedrock
      curTile = Obsidian;
      for (int y = 0; y < Tile; y++)
        for (int x = 0; x < Tile; x++)
        {
          float n = H(x / 2, y / 2, 181);
          Color c = n < 0.7f ? Hex("#15101f") : n < 0.9f ? Hex("#2a1f3d") : Hex("#4a3a6a");
          Set(x, y, c);
        }
      curTile = Bedrock;
      for (int y = 0; y < Tile; y++)
        for (int x = 0; x < Tile; x++)
        {
          float n = H(x / 2, y / 2, 191);
          Set(x, y, n < 0.33f ? Hex("#2e2e2e") : n < 0.66f ? Hex("#575757") : Hex("#888888"));
        }

      Metal(Diamond, Hex("#5ce1d9"), 201);
      Metal(Gold, Hex("#f5d33a"), 202);
      Metal(Iron, Hex("#dcdcdc"), 203);
      Metal(Emerald, Hex("#2fcf5a"), 204);
      Metal(Redstone, Hex("#b0170a"), 205);
      Metal(Lapis, Hex("#1f45b0"), 206);
      Metal(Quartz, Hex("#ece6df"), 207);

      Ore(CoalOre, Hex("#222222"), 211);
      Ore(IronOre, Hex("#d8af93"), 212);
      Ore(DiamondOre, Hex("#5decf5"), 213);
      Ore(GoldOre, Hex("#fcee4b"), 214);
      Ore(EmeraldOre, Hex("#17dd62"), 215);

      curTile = Glowstone;
      for (int y = 0; y < Tile; y++)
        for (int x = 0; x < Tile; x++)
        {
          float n = H(x / 2, y / 2, 221);
          Color c = n < 0.25f ? Hex("#8a6a2a") : n < 0.55f ? Hex("#d6a64a") : n < 0.85f ? Hex("#ffd977") : Hex("#fff3c4");
          Set(x, y, c);
        }
      Noise(Netherrack, Hex("#6f2a2a"), 0.2f, 231, 5);
      Noise(SoulSand, Hex("#51402f"), 0.15f, 241, 4);
      curTile = SoulSand;
      for (int k = 0; k < 3; k++)
      {
        int fx = 2 + k * 5, fy = 3 + (k % 2) * 6;
        Set(fx, fy, Hex("#2a2018")); Set(fx + 2, fy, Hex("#2a2018")); Set(fx + 1, fy + 2, Hex("#2a2018"));
      }
      curTile = Ice;
      for (int y = 0; y < Tile; y++)
        for (int x = 0; x < Tile; x++)
          Set(x, y, Shade(Hex("#8fb5f5"), 0.95f + H(x, y / 4, 251) * 0.1f));
      for (int i = 0; i < 6; i++) { Set(2 + i, 10 - i, Hex("#d6e6ff")); Set(8 + i / 2, 13 - i, Hex("#c4d8ff")); }
      Noise(Snow, Hex("#f2f8f8"), 0.03f, 261, 3);

      // Pumpkin / jack o'lantern
      Color pumpkin = Hex("#d9821c");
      curTile = PumpkinSide;
      for (int y = 0; y < Tile; y++)
        for (int x = 0; x < Tile; x++)
          Set(x, y, Shade(pumpkin, (x % 4 == 0) ? 0.78f : 0.95f + H(x, y, 271) * 0.08f));
      curTile = PumpkinTop;
      for (int y = 0; y < Tile; y++)
        for (int x = 0; x < Tile; x++)
          Set(x, y, Shade(pumpkin, 0.9f + H(x, y, 272) * 0.1f));
      for (int y = 6; y < 10; y++) for (int x = 7; x < 9; x++) Set(x, y, Hex("#5a7a2a"));
      Copy(PumpkinSide, PumpkinFace);
      curTile = PumpkinFace;
      Color hole = Hex("#3a2008");
      Set(4, 5, hole); Set(5, 5, hole); Set(4, 6, hole); Set(5, 6, hole);
      Set(10, 5, hole); Set(11, 5, hole); Set(10, 6, hole); Set(11, 6, hole);
      for (int x = 3; x < 13; x++) { Set(x, 10, hole); if (x % 3 != 0) Set(x, 11, hole); }
      Copy(PumpkinFace, JackFace);
      curTile = JackFace;
      Color candle = Hex("#ffe36b");
      for (int y = 0; y < Tile; y++) for (int x = 0; x < Tile; x++) { Color o = Get(x, y); if (Mathf.Abs(o.r - hole.r) + Mathf.Abs(o.g - hole.g) + Mathf.Abs(o.b - hole.b) < 0.04f) Set(x, y, candle); }

      curTile = MelonSide;
      for (int y = 0; y < Tile; y++)
        for (int x = 0; x < Tile; x++)
          Set(x, y, Shade((x % 5 < 2) ? Hex("#a8c43a") : Hex("#7a9a22"), 0.95f + H(x, y, 281) * 0.1f));
      curTile = MelonTop;
      for (int y = 0; y < Tile; y++)
        for (int x = 0; x < Tile; x++)
          Set(x, y, Shade(((x + y) % 6 < 2) ? Hex("#a8c43a") : Hex("#7a9a22"), 0.95f + H(x, y, 282) * 0.1f));

      Wool(WoolWhite, Hex("#e9ecec"), 291);
      Wool(WoolRed, Hex("#a12722"), 292);
      Wool(WoolBlue, Hex("#35399d"), 293);
      Wool(WoolGreen, Hex("#546d1b"), 294);
      Wool(WoolYellow, Hex("#f8c527"), 295);
      Wool(WoolBlack, Hex("#141519"), 296);
      Wool(WoolPurple, Hex("#7a2aac"), 297);
      Wool(WoolOrange, Hex("#f07613"), 298);
      Wool(WoolPink, Hex("#ed8dac"), 299);
      Wool(WoolCyan, Hex("#158991"), 300);

      curTile = HaySide;
      for (int y = 0; y < Tile; y++)
        for (int x = 0; x < Tile; x++)
        {
          bool band = y == 3 || y == 4 || y == 11 || y == 12;
          Set(x, y, band ? Hex("#8a2a1a") : Shade(Hex("#c9a632"), (x % 3 == 0) ? 0.8f : 0.95f + H(x, y, 311) * 0.1f));
        }
      curTile = HayTop;
      for (int y = 0; y < Tile; y++)
        for (int x = 0; x < Tile; x++)
          Set(x, y, Shade(Hex("#c9a632"), 0.85f + H(x, y, 312) * 0.25f));

      curTile = Slime;
      for (int y = 0; y < Tile; y++)
        for (int x = 0; x < Tile; x++)
        {
          bool inner = x >= 4 && x <= 11 && y >= 4 && y <= 11;
          bool innerEdge = inner && (x == 4 || x == 11 || y == 4 || y == 11);
          Color c = innerEdge ? Hex("#4f9e3d") : inner ? Hex("#6fc95a") : Hex("#7fdc6a");
          Set(x, y, Shade(c, 0.95f + H(x, y, 321) * 0.08f));
        }
      Border(Slime, Hex("#5aa548"));

      curTile = SeaLantern;
      for (int y = 0; y < Tile; y++)
        for (int x = 0; x < Tile; x++)
        {
          bool line = x % 5 == 0 || y % 5 == 0;
          Set(x, y, line ? Hex("#a9d6cc") : Shade(Hex("#e0f5ef"), 0.95f + H(x, y, 331) * 0.05f));
        }
      curTile = Sponge;
      for (int y = 0; y < Tile; y++)
        for (int x = 0; x < Tile; x++)
          Set(x, y, H(x / 2, y / 2, 341) < 0.22f ? Hex("#9a8a24") : Shade(Hex("#d3c74c"), 0.95f + H(x, y, 342) * 0.08f));
      curTile = Magma;
      for (int y = 0; y < Tile; y++)
        for (int x = 0; x < Tile; x++)
        {
          bool crack = ((x * 3 + y * 5) % 11 == 0) || ((x * 7 + y * 2) % 13 == 0);
          Set(x, y, crack ? Hex("#ffa630") : Shade(Hex("#5a2412"), 0.85f + H(x, y, 351) * 0.3f));
        }

      // Furnace
      Copy(Cobble, FurnaceSide);
      Copy(StoneBricks, FurnaceTop);
      Copy(Cobble, FurnaceFront);
      curTile = FurnaceFront;
      for (int y = 8; y < 14; y++) for (int x = 4; x < 12; x++) Set(x, y, Hex("#1e1e1e"));
      for (int x = 5; x < 11; x++) { Set(x, 12, Hex("#ff8a1a")); if (x % 2 == 0) Set(x, 11, Hex("#ffd04a")); }
      for (int x = 3; x < 13; x++) Set(x, 4, Shade(stone, 0.55f));

      // Fluids (scrolled by the material)
      curTile = Water;
      for (int y = 0; y < Tile; y++)
        for (int x = 0; x < Tile; x++)
        {
          float n = Mathf.Sin((x + y * 0.5f) * 0.8f) * 0.5f + 0.5f;
          Set(x, y, new Color(0.18f + n * 0.08f, 0.35f + n * 0.12f, 0.85f + n * 0.1f, 0.62f));
        }
      curTile = Lava;
      for (int y = 0; y < Tile; y++)
        for (int x = 0; x < Tile; x++)
        {
          float n = H(x / 2, y / 2, 371);
          Color c = n < 0.35f ? Hex("#c4300a") : n < 0.75f ? Hex("#ef6a10") : Hex("#ffc23a");
          Set(x, y, c);
        }

      // Crate (used by objects too)
      Copy(Planks, Crate);
      Border(Crate, Shade(plank, 0.55f), 2);
      curTile = Crate;
      for (int i = 2; i < 14; i++) { Set(i, i, Shade(plank, 0.6f)); Set(i, i - 1, Shade(plank, 0.6f)); }

      Noise(Dummy, Hex("#888888"), 0.1f, 381, 3);

      // nether portal (swirly purple, see-through) and nether bricks
      curTile = NetherPortal;
      for (int y = 0; y < Tile; y++)
        for (int x = 0; x < Tile; x++)
        {
          float sw = Mathf.Sin((x * 0.7f + Mathf.Sin(y * 0.5f) * 3f)) * 0.5f + 0.5f;
          Set(x, y, new Color(0.45f + sw * 0.3f, 0.1f + sw * 0.15f, 0.85f + sw * 0.15f, 0.72f));
        }
      curTile = NetherBricks;
      for (int y = 0; y < Tile; y++)
        for (int x = 0; x < Tile; x++)
        {
          int row = y / 4;
          bool m = y % 4 == 3 || ((x + (row % 2 == 0 ? 0 : 4)) % 8 == 7);
          Set(x, y, m ? Hex("#1a0c0e") : Shade(Hex("#3c1a1e"), 0.85f + H(x, y, 391) * 0.3f));
        }

      PaintExtras();   // the extra blocks (their colour, until a pack has their picture)
      // the End: end stone, the portal frame (top, side, eye), the starry portal, fire
      Color endStone = Hex("#dbde9e");
      Noise(EndStone, endStone, 0.1f, 401, 4);
      Speckle(EndStone, Shade(endStone, 0.8f), 402, 0f, 5);
      curTile = EndFrameTop;
      for (int y = 0; y < Tile; y++)
        for (int x = 0; x < Tile; x++)
        {
          bool edge = x < 2 || y < 2 || x > 13 || y > 13;
          bool socket = x >= 4 && x <= 11 && y >= 4 && y <= 11;
          Color c = edge ? Hex("#d9dcb0") : socket ? Hex("#264a42") : Hex("#3d7a6d");
          Set(x, y, Shade(c, 0.88f + H(x, y, 411) * 0.24f));
        }
      curTile = EndFrameSide;
      for (int y = 0; y < Tile; y++)
        for (int x = 0; x < Tile; x++)
        {
          Color c = y < 3 ? new Color(0f, 0f, 0f, 0f) : y < 6 ? Hex("#3d7a6d") : Hex("#d9dcb0");
          if (c.a > 0f) c = Shade(c, 0.86f + H(x, y, 421) * 0.26f);
          Set(x, y, c);
        }
      curTile = EndFrameEye;
      for (int y = 0; y < Tile; y++)
        for (int x = 0; x < Tile; x++)
        {
          float d = Vector2.Distance(new Vector2(x, y), new Vector2(7.5f, 7.5f));
          Color c = d < 1.6f ? Hex("#0b1a14") : d < 3.2f ? Hex("#2f9a5f") : d < 4.2f ? Hex("#1d5e3b") : new Color(0f, 0f, 0f, 0f);
          Set(x, y, c);
        }
      curTile = EndPortal;
      for (int y = 0; y < Tile; y++)
        for (int x = 0; x < Tile; x++)
        {
          float h = H(x, y, 431);
          Color c = h > 0.94f ? Hex("#b8f5e6") : h > 0.88f ? Hex("#4d7d8c") : Shade(Hex("#0c1418"), 0.7f + H(x, y, 432) * 0.5f);
          Set(x, y, c);
        }
      curTile = Fire;
      for (int y = 0; y < Tile; y++)
        for (int x = 0; x < Tile; x++)
        {
          // flames: tall tongues, white-yellow at the bottom, red at the tips
          float tongue = 7f + 7f * Mathf.Abs(Mathf.Sin(x * 0.9f + H(x, 0, 441) * 2f));
          float hgt = Tile - y;   // height above the bottom edge (y = 0 is the top row)
          if (hgt > tongue) { Set(x, y, new Color(0f, 0f, 0f, 0f)); continue; }
          float t = hgt / tongue;
          Color c = t < 0.35f ? Hex("#fff1a0") : t < 0.7f ? Hex("#ffaa22") : Hex("#e2471a");
          Set(x, y, new Color(c.r, c.g, c.b, 1f));
        }
    }
  }

  // ---------------------------------------------------------------- materials
  internal static class SbMats
  {
    private static Material lit, glow, blockLit, blockGlow, blockClear, ghost, water, lava, particleAdd, particleAlpha, line;
    private static Texture2D softDot, smokeTex, ringTex;

    private static Shader Find(params string[] names)
    {
      foreach (string n in names)
      {
        Shader s = Shader.Find(n);
        if (s != null && s.isSupported) return s;
      }
      return Shader.Find("Sprites/Default");
    }

    private static Material Make(Shader s, string name, Texture tex, Color c)
    {
      Material m = new Material(s) { name = name, hideFlags = HideFlags.DontSave };
      if (tex != null)
      {
        if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", tex);
        if (m.HasProperty("_MainTex")) m.SetTexture("_MainTex", tex);
      }
      if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
      if (m.HasProperty("_Color")) m.SetColor("_Color", c);
      if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.18f);
      if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", 0f);
      return m;
    }

    internal static Material Lit
    {
      get
      {
        if (lit == null)
        {
          lit = Make(Find("Universal Render Pipeline/Lit", "Standard", "Unlit/Texture"), "Unknowns SandBox Lit", SbPalette.Texture, Color.white);
          // a little sheen so the smooth-shaded objects read as real surfaces
          if (lit.HasProperty("_Smoothness")) lit.SetFloat("_Smoothness", 0.34f);
          if (lit.HasProperty("_EnvironmentReflections")) lit.SetFloat("_EnvironmentReflections", 1f);
        }
        else if (SbPalette.Dirty) { var unused = SbPalette.Texture; }
        return lit;
      }
    }

    internal static Material Glow
    {
      get
      {
        if (glow == null) glow = Make(Find("Universal Render Pipeline/Unlit", "Unlit/Texture"), "Unknowns SandBox Glow", SbPalette.Texture, Color.white);
        return glow;
      }
    }

    internal static Material BlockLit
    {
      get
      {
        if (blockLit == null)
        {
          blockLit = Make(Find("Universal Render Pipeline/Lit", "Standard", "Unlit/Texture"), "Unknowns SandBox Blocks", SbBlockArt.Atlas, Color.white);
          if (blockLit.HasProperty("_Smoothness")) blockLit.SetFloat("_Smoothness", 0.05f);
        }
        return blockLit;
      }
    }

    internal static Material BlockGlow
    {
      get
      {
        if (blockGlow == null) blockGlow = Make(Find("Universal Render Pipeline/Unlit", "Unlit/Texture"), "Unknowns SandBox Glow Blocks", SbBlockArt.Atlas, Color.white);
        return blockGlow;
      }
    }

    // Glass, slime, ice: see-through. Sprites/Default blends alpha and draws both sides.
    internal static Material BlockClear
    {
      get
      {
        if (blockClear == null)
        {
          blockClear = Make(Find("Sprites/Default"), "Unknowns SandBox Clear Blocks", SbBlockArt.Atlas, Color.white);
          blockClear.renderQueue = 3001;
        }
        return blockClear;
      }
    }

    internal static Material Ghost
    {
      get
      {
        if (ghost == null)
        {
          ghost = Make(Find("Sprites/Default"), "Unknowns SandBox Ghost", null, new Color(0.4f, 0.9f, 1f, 0.35f));
          ghost.renderQueue = 3050;
        }
        return ghost;
      }
    }

    internal static Material Water
    {
      get
      {
        if (water == null)
        {
          water = Make(Find("Sprites/Default"), "Unknowns SandBox Water", SbBlockArt.Atlas, new Color(1f, 1f, 1f, 1f));
          water.renderQueue = 3002;
        }
        return water;
      }
    }

    internal static Material Lava
    {
      get
      {
        if (lava == null) lava = Make(Find("Universal Render Pipeline/Unlit", "Unlit/Texture"), "Unknowns SandBox Lava", SbBlockArt.Atlas, Color.white);
        return lava;
      }
    }

    internal static Material Line
    {
      get
      {
        if (line == null)
        {
          line = Make(Find("Sprites/Default"), "Unknowns SandBox Line", null, Color.white);
          line.renderQueue = 3100;
        }
        return line;
      }
    }

    internal static Texture2D SoftDot
    {
      get
      {
        if (softDot == null) softDot = MakeRadial(64, (d) => Mathf.Pow(Mathf.Clamp01(1f - d), 2.2f));
        return softDot;
      }
    }

    internal static Texture2D Ring
    {
      get
      {
        if (ringTex == null) ringTex = MakeRadial(128, (d) => Mathf.Clamp01(1f - Mathf.Abs(d - 0.82f) / 0.14f) * Mathf.Clamp01(1f - Mathf.Abs(d - 0.82f) / 0.14f));
        return ringTex;
      }
    }

    internal static Texture2D Smoke
    {
      get
      {
        if (smokeTex != null) return smokeTex;
        int s = 64;
        smokeTex = new Texture2D(s, s, TextureFormat.RGBA32, false) { name = "Unknowns SandBox Smoke", wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.DontSave };
        Color32[] p = new Color32[s * s];
        for (int y = 0; y < s; y++)
          for (int x = 0; x < s; x++)
          {
            float dx = (x - s * 0.5f + 0.5f) / (s * 0.5f), dy = (y - s * 0.5f + 0.5f) / (s * 0.5f);
            float d = Mathf.Sqrt(dx * dx + dy * dy);
            float n = Mathf.PerlinNoise(x * 0.12f, y * 0.12f) * 0.6f + Mathf.PerlinNoise(x * 0.31f + 7f, y * 0.31f) * 0.4f;
            float a = Mathf.Clamp01(1f - d) * (0.55f + n * 0.6f);
            p[y * s + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(a * a * 1.6f));
          }
        smokeTex.SetPixels32(p);
        smokeTex.Apply(false, false);
        return smokeTex;
      }
    }

    private static Texture2D MakeRadial(int s, Func<float, float> alpha)
    {
      Texture2D t = new Texture2D(s, s, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.DontSave, name = "Unknowns SandBox Radial" };
      Color32[] p = new Color32[s * s];
      for (int y = 0; y < s; y++)
        for (int x = 0; x < s; x++)
        {
          float dx = (x - s * 0.5f + 0.5f) / (s * 0.5f), dy = (y - s * 0.5f + 0.5f) / (s * 0.5f);
          float d = Mathf.Sqrt(dx * dx + dy * dy);
          p[y * s + x] = new Color(1f, 1f, 1f, d >= 1f ? 0f : alpha(d));
        }
      t.SetPixels32(p);
      t.Apply(false, false);
      return t;
    }

    // Particles. "Sprites/Default" takes the particle colour and texture alpha.
    internal static Material ParticleAlpha
    {
      get
      {
        if (particleAlpha == null)
        {
          // Sprites/Default: always in the game build and blends by itself. The URP
          // particle shader needs keywords that may be stripped (black squares).
          particleAlpha = Make(Find("Sprites/Default"), "Unknowns SandBox Smoke", Smoke, Color.white);
          ParticleSetup(particleAlpha, false);
        }
        return particleAlpha;
      }
    }

    internal static Material ParticleAdd
    {
      get
      {
        if (particleAdd == null)
        {
          particleAdd = Make(Find("Sprites/Default"), "Unknowns SandBox Glow Particles", SoftDot, Color.white);
          ParticleSetup(particleAdd, true);
        }
        return particleAdd;
      }
    }

    // Minecraft break bits: the block's own texture (one material per tile; the
    // texture follows a resource pack change)
    private static readonly Dictionary<int, Material> tileParticles = new Dictionary<int, Material>();

    internal static Material TileParticle(int tile)
    {
      Texture2D tex = SbBlockArt.TileTexture(tile);
      Material m;
      if (!tileParticles.TryGetValue(tile, out m) || m == null)
      {
        m = Make(Find("Sprites/Default"), "Unknowns SandBox Bits " + tile, tex, Color.white);
        ParticleSetup(m, false);
        tileParticles[tile] = m;
      }
      else if (m.mainTexture != tex)
      {
        if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", tex);
        if (m.HasProperty("_MainTex")) m.SetTexture("_MainTex", tex);
      }
      return m;
    }

    private static void ParticleSetup(Material m, bool additive)
    {
      m.renderQueue = 3010;
      if (m.shader != null && m.shader.name.StartsWith("Universal Render Pipeline/Particles", StringComparison.Ordinal))
      {
        // URP particles: transparent, blend chosen by the keyword-free properties
        // that exist on the shader (the variants with keywords may be stripped,
        // so only the blend factors are changed).
        if (m.HasProperty("_Surface")) m.SetFloat("_Surface", 1f);
        if (m.HasProperty("_SrcBlend")) m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        if (m.HasProperty("_DstBlend")) m.SetFloat("_DstBlend", additive ? (float)UnityEngine.Rendering.BlendMode.One : (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        if (m.HasProperty("_ZWrite")) m.SetFloat("_ZWrite", 0f);
        m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        m.SetOverrideTag("RenderType", "Transparent");
      }
    }

    internal static Material[] For(SbItem item)
    {
      if (item == null) return new[] { Lit };
      if (item.Kind == SbKind.Block)
      {
        if (item.BlockFx == SbBlockFx.Glow || item.BlockFx == SbBlockFx.Magma || item.BlockFx == SbBlockFx.EndPortal) return new[] { BlockGlow };
        if (item.BlockFx == SbBlockFx.Glass || item.BlockFx == SbBlockFx.Slime || item.BlockFx == SbBlockFx.Ice || item.BlockFx == SbBlockFx.Portal) return new[] { BlockClear };
        // shapes with see-through pixels (doors, rails, dust, levers, repeaters' torches)
        if (item.Shape == SbShape.Door || item.Shape == SbShape.Rail || item.Shape == SbShape.Dust || item.Shape == SbShape.Lever || item.Shape == SbShape.Repeater || item.Shape == SbShape.Comparator) return new[] { BlockClear };
        if (item.Mat != null) return new[] { item.Mat };
        return new[] { BlockLit };
      }
      if (item.Mat != null) return new[] { item.Mat };   // a Minecraft entity texture (shield, minecart, boat)
      return new[] { Lit, Glow };
    }
  }

  // ---------------------------------------------------------------- block mesh
  internal static partial class SbBlockMesh
  {
    private static readonly Dictionary<long, Mesh> cache = new Dictionary<long, Mesh>();

    // Unit cube (-0.5..0.5) with the given tile per face.
    internal static Mesh Get(int top, int side, int bottom, int front)
    {
      long key = (long)top | ((long)side << 16) | ((long)bottom << 32) | ((long)front << 48);
      Mesh m;
      if (cache.TryGetValue(key, out m) && m != null) return m;
      var v = new List<Vector3>(); var n = new List<Vector3>(); var uv = new List<Vector2>(); var t = new List<int>();
      void Face(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 nn, int tile)
      {
        // a = bottom-left, b = bottom-right, c = top-right, d = top-left as seen from outside
        Rect r = SbBlockArt.TileRect(tile);
        int i = v.Count;
        v.Add(a); v.Add(b); v.Add(c); v.Add(d);
        for (int k = 0; k < 4; k++) n.Add(nn);
        uv.Add(new Vector2(r.xMin, r.yMin)); uv.Add(new Vector2(r.xMax, r.yMin)); uv.Add(new Vector2(r.xMax, r.yMax)); uv.Add(new Vector2(r.xMin, r.yMax));
        Vector3 face = Vector3.Cross(b - a, c - a);
        if (Vector3.Dot(face, nn) >= 0f) { t.Add(i); t.Add(i + 1); t.Add(i + 2); t.Add(i); t.Add(i + 2); t.Add(i + 3); }
        else { t.Add(i); t.Add(i + 2); t.Add(i + 1); t.Add(i); t.Add(i + 3); t.Add(i + 2); }
      }
      const float h = 0.5f;
      // -Z is the "front" (faces the player who placed it)
      Face(new Vector3(h, -h, h), new Vector3(-h, -h, h), new Vector3(-h, h, h), new Vector3(h, h, h), Vector3.forward, side);
      Face(new Vector3(-h, -h, -h), new Vector3(h, -h, -h), new Vector3(h, h, -h), new Vector3(-h, h, -h), Vector3.back, front);
      Face(new Vector3(h, -h, -h), new Vector3(h, -h, h), new Vector3(h, h, h), new Vector3(h, h, -h), Vector3.right, side);
      Face(new Vector3(-h, -h, h), new Vector3(-h, -h, -h), new Vector3(-h, h, -h), new Vector3(-h, h, h), Vector3.left, side);
      Face(new Vector3(-h, h, -h), new Vector3(h, h, -h), new Vector3(h, h, h), new Vector3(-h, h, h), Vector3.up, top);
      Face(new Vector3(-h, -h, h), new Vector3(h, -h, h), new Vector3(h, -h, -h), new Vector3(-h, -h, -h), Vector3.down, bottom);
      m = new Mesh { name = "Unknowns SandBox Block", hideFlags = HideFlags.DontSave };
      m.SetVertices(v); m.SetNormals(n); m.SetUVs(0, uv); m.SetTriangles(t, 0);
      m.RecalculateBounds();
      cache[key] = m;
      return m;
    }

    private static Mesh frame, frameEye;

    // The End portal frame like Minecraft's: 13/16 tall (sides use the bottom 13 px
    // of their texture), and with an eye: an 8 x 3 x 8 px eye block on top.
    internal static Mesh Frame(bool eye)
    {
      Mesh cached = eye ? frameEye : frame;
      if (cached != null) return cached;
      var v = new List<Vector3>(); var n = new List<Vector3>(); var uv = new List<Vector2>(); var t = new List<int>();
      void Face(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 nn, Rect r)
      {
        int i = v.Count;
        v.Add(a); v.Add(b); v.Add(c); v.Add(d);
        for (int k = 0; k < 4; k++) n.Add(nn);
        uv.Add(new Vector2(r.xMin, r.yMin)); uv.Add(new Vector2(r.xMax, r.yMin)); uv.Add(new Vector2(r.xMax, r.yMax)); uv.Add(new Vector2(r.xMin, r.yMax));
        if (Vector3.Dot(Vector3.Cross(b - a, c - a), nn) >= 0f) { t.Add(i); t.Add(i + 1); t.Add(i + 2); t.Add(i); t.Add(i + 2); t.Add(i + 3); }
        else { t.Add(i); t.Add(i + 2); t.Add(i + 1); t.Add(i); t.Add(i + 3); t.Add(i + 2); }
      }
      // a box from lo to hi; side faces use sub (fraction of the tile from the bottom)
      void Box(Vector3 lo, Vector3 hi, Rect top, Rect side, Rect bottom)
      {
        Face(new Vector3(hi.x, lo.y, hi.z), new Vector3(lo.x, lo.y, hi.z), new Vector3(lo.x, hi.y, hi.z), new Vector3(hi.x, hi.y, hi.z), Vector3.forward, side);
        Face(new Vector3(lo.x, lo.y, lo.z), new Vector3(hi.x, lo.y, lo.z), new Vector3(hi.x, hi.y, lo.z), new Vector3(lo.x, hi.y, lo.z), Vector3.back, side);
        Face(new Vector3(hi.x, lo.y, lo.z), new Vector3(hi.x, lo.y, hi.z), new Vector3(hi.x, hi.y, hi.z), new Vector3(hi.x, hi.y, lo.z), Vector3.right, side);
        Face(new Vector3(lo.x, lo.y, hi.z), new Vector3(lo.x, lo.y, lo.z), new Vector3(lo.x, hi.y, lo.z), new Vector3(lo.x, hi.y, hi.z), Vector3.left, side);
        Face(new Vector3(lo.x, hi.y, lo.z), new Vector3(hi.x, hi.y, lo.z), new Vector3(hi.x, hi.y, hi.z), new Vector3(lo.x, hi.y, hi.z), Vector3.up, top);
        Face(new Vector3(lo.x, lo.y, hi.z), new Vector3(hi.x, lo.y, hi.z), new Vector3(hi.x, lo.y, lo.z), new Vector3(lo.x, lo.y, lo.z), Vector3.down, bottom);
      }
      Rect Sub(int tile, float x0, float y0, float x1, float y1)
      {
        // a pixel region of a tile, y from the BOTTOM of the picture (0..16)
        Rect r = SbBlockArt.TileRect(tile);
        return new Rect(r.xMin + r.width * x0 / 16f, r.yMin + r.height * y0 / 16f, r.width * (x1 - x0) / 16f, r.height * (y1 - y0) / 16f);
      }
      const float h = 0.5f, topY = -0.5f + 13f / 16f;
      Box(new Vector3(-h, -h, -h), new Vector3(h, topY, h), SbBlockArt.TileRect(SbBlockArt.EndFrameTop), Sub(SbBlockArt.EndFrameSide, 0f, 0f, 16f, 13f), SbBlockArt.TileRect(SbBlockArt.EndStone));
      if (eye)
      {
        const float e = 0.25f;
        Box(new Vector3(-e, topY, -e), new Vector3(e, h, e), Sub(SbBlockArt.EndFrameEye, 4f, 4f, 12f, 12f), Sub(SbBlockArt.EndFrameEye, 4f, 0f, 12f, 3f), Sub(SbBlockArt.EndFrameEye, 4f, 4f, 12f, 12f));
      }
      var m = new Mesh { name = eye ? "Unknowns End Frame (eye)" : "Unknowns End Frame", hideFlags = HideFlags.DontSave };
      m.SetVertices(v); m.SetNormals(n); m.SetUVs(0, uv); m.SetTriangles(t, 0);
      m.RecalculateBounds();
      if (eye) frameEye = m; else frame = m;
      return m;
    }
  }

  // ---------------------------------------------------------------- icons
  // A tiny software rasteriser: draws a model mesh (palette or atlas coloured)
  // into a Texture2D for the menu cards, without a camera.
  internal static class SbIcons
  {
    internal static Texture2D Render(Mesh mesh, bool atlas, int size, float yaw = -35f, float pitch = 24f, Color? tint = null, Texture2D custom = null)
    {
      int ss = size * 2;
      float[] depth = new float[ss * ss];
      Color[] col = new Color[ss * ss];
      for (int i = 0; i < depth.Length; i++) { depth[i] = float.MaxValue; col[i] = new Color(0f, 0f, 0f, 0f); }

      Vector3[] verts = mesh.vertices;
      Vector3[] norms = mesh.normals;
      Vector2[] uvs = mesh.uv;
      // looks down on the model from above (negative pitch, so the top face shows)
      Quaternion view = Quaternion.Euler(-pitch, 0f, 0f) * Quaternion.Euler(0f, yaw, 0f);
      Bounds b = mesh.bounds;
      Vector3[] pv = new Vector3[verts.Length];
      Vector3 min = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue), max = -min;
      for (int i = 0; i < verts.Length; i++)
      {
        pv[i] = view * (verts[i] - b.center);
        min = Vector3.Min(min, pv[i]); max = Vector3.Max(max, pv[i]);
      }
      float span = Mathf.Max(max.x - min.x, max.y - min.y);
      if (span < 1e-5f) span = 1f;
      float scale = ss * 0.84f / span;
      Vector2 mid = new Vector2((min.x + max.x) * 0.5f, (min.y + max.y) * 0.5f);
      Vector3 light = (view * new Vector3(0.45f, 0.8f, -0.35f)).normalized;
      Vector3 lightModel = new Vector3(0.45f, 0.8f, -0.35f).normalized;

      for (int sub = 0; sub < mesh.subMeshCount; sub++)
      {
        int[] tris = mesh.GetTriangles(sub);
        bool glow = !atlas && sub == 1;
        for (int t = 0; t < tris.Length; t += 3)
        {
          int ia = tris[t], ib = tris[t + 1], ic = tris[t + 2];
          Vector3 a = pv[ia], bb = pv[ib], c = pv[ic];
          Vector2 sa = new Vector2((a.x - mid.x) * scale + ss * 0.5f, (a.y - mid.y) * scale + ss * 0.5f);
          Vector2 sb = new Vector2((bb.x - mid.x) * scale + ss * 0.5f, (bb.y - mid.y) * scale + ss * 0.5f);
          Vector2 sc = new Vector2((c.x - mid.x) * scale + ss * 0.5f, (c.y - mid.y) * scale + ss * 0.5f);
          float area = (sb.x - sa.x) * (sc.y - sa.y) - (sb.y - sa.y) * (sc.x - sa.x);
          if (Mathf.Abs(area) < 1e-6f) continue;
          int x0 = Mathf.Max(0, (int)Mathf.Floor(Mathf.Min(sa.x, Mathf.Min(sb.x, sc.x))));
          int x1 = Mathf.Min(ss - 1, (int)Mathf.Ceil(Mathf.Max(sa.x, Mathf.Max(sb.x, sc.x))));
          int y0 = Mathf.Max(0, (int)Mathf.Floor(Mathf.Min(sa.y, Mathf.Min(sb.y, sc.y))));
          int y1 = Mathf.Min(ss - 1, (int)Mathf.Ceil(Mathf.Max(sa.y, Mathf.Max(sb.y, sc.y))));
          for (int y = y0; y <= y1; y++)
            for (int x = x0; x <= x1; x++)
            {
              float px = x + 0.5f, py = y + 0.5f;
              float w0 = ((sb.x - px) * (sc.y - py) - (sb.y - py) * (sc.x - px)) / area;
              float w1 = ((sc.x - px) * (sa.y - py) - (sc.y - py) * (sa.x - px)) / area;
              float w2 = 1f - w0 - w1;
              if (w0 < 0f || w1 < 0f || w2 < 0f) continue;
              float z = a.z * w0 + bb.z * w1 + c.z * w2;
              int idx = y * ss + x;
              if (z >= depth[idx]) continue;
              Vector2 uv = uvs[ia] * w0 + uvs[ib] * w1 + uvs[ic] * w2;
              Color texel = custom != null ? custom.GetPixelBilinear(uv.x, uv.y) : atlas ? (Color)SbBlockArt.Sample(uv) : (Color)SbPalette.Sample(uv);
              if (texel.a < 0.05f) continue;
              Vector3 nrm = (norms[ia] * w0 + norms[ib] * w1 + norms[ic] * w2).normalized;
              float lambert = Mathf.Clamp01(Vector3.Dot(nrm, lightModel));
              float shade = glow ? 1f : 0.42f + lambert * 0.68f;
              if (atlas)
              {
                // Minecraft-style face shading: top bright, sides darker.
                float up = Mathf.Abs(nrm.y), fx = Mathf.Abs(nrm.x);
                shade = up > 0.5f ? 1.05f : fx > 0.5f ? 0.80f : 0.64f;
              }
              Color o = new Color(Mathf.Clamp01(texel.r * shade), Mathf.Clamp01(texel.g * shade), Mathf.Clamp01(texel.b * shade), Mathf.Max(texel.a, atlas ? 0.55f : 1f));
              if (tint.HasValue) o *= tint.Value;
              depth[idx] = z;
              col[idx] = o;
            }
        }
      }
      // light outline so dark models read on dark cards
      Color[] outCol = new Color[size * size];
      for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
          Color sum = new Color(0, 0, 0, 0);
          for (int k = 0; k < 4; k++)
          {
            Color c = col[(y * 2 + (k >> 1)) * ss + x * 2 + (k & 1)];
            sum.r += c.r * c.a; sum.g += c.g * c.a; sum.b += c.b * c.a; sum.a += c.a;
          }
          if (sum.a > 0f) { sum.r /= sum.a; sum.g /= sum.a; sum.b /= sum.a; }
          sum.a *= 0.25f;
          outCol[y * size + x] = sum;
        }
      Color[] final = new Color[size * size];
      for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
          Color c = outCol[y * size + x];
          if (c.a < 0.5f)
          {
            float near = 0f;
            for (int dy = -1; dy <= 1; dy++)
              for (int dx = -1; dx <= 1; dx++)
              {
                int xx = x + dx, yy = y + dy;
                if (xx < 0 || yy < 0 || xx >= size || yy >= size) continue;
                near = Mathf.Max(near, outCol[yy * size + xx].a);
              }
            if (near > 0.5f)
            {
              Color edge = new Color(1f, 1f, 1f, 0.22f);
              c = Color.Lerp(edge, c, c.a);
              c.a = Mathf.Max(c.a, 0.22f);
            }
          }
          final[y * size + x] = c;
        }
      Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = "Unknowns SandBox Icon", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, hideFlags = HideFlags.DontSave };
      tex.SetPixels(final);
      tex.Apply(false, false);
      return tex;
    }
  }
}
