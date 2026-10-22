using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace UnknownsSandbox
{
  // Real blocks for the Nether and the End. Each dimension is a grid of
  // blocks kept in 32 x 32 column chunks; a chunk's mesh only has the faces that
  // touch air (hidden faces are culled), so it stays light. Blocks can be broken
  // (grip + trigger, like your own objects) and everyone in the lobby sees it:
  // the change is sent to them, and to anyone joining later.
  internal enum SbVox : byte
  {
    Air = 0, Netherrack = 1, SoulSand = 2, Magma = 3, Glowstone = 4, EndStone = 5, Obsidian = 6, Bedrock = 7,
    // the Minecraft Nether's biomes and structures (SandBoxNetherGen.cs)
    CrimsonNylium = 8, WarpedNylium = 9, CrimsonStem = 10, WarpedStem = 11, NetherWart = 12, WarpedWart = 13, Shroomlight = 14,
    Basalt = 15, Blackstone = 16, SoulSoil = 17, NetherBricks = 18, RedNetherBricks = 19, PolishedBlackstoneBricks = 20,
    GildedBlackstone = 21, QuartzOre = 22, NetherGoldOre = 23, Gravel = 24, CrackedBlackstoneBricks = 25, ChiseledBlackstone = 26,
    CryingObsidian = 27, GoldBlock = 28   // ruined portals
  }

  // Gorilla Tag keeps a copy of every collider mesh's triangles the first time a
  // hand touches it (GTPlayer.meshTrianglesDict, for surface sliding). A mesh rebuilt in
  // place (a broken block, flowing water) then has new triangles but the game reads the
  // old copy: IndexOutOfRangeException every frame and hand collision stops working.
  // So whenever the SandBox changes or drops a collider mesh, the game's copy goes too.
  internal static class SbGameMeshCache
  {
    private static System.Reflection.FieldInfo field;
    private static bool looked;

    internal static void Forget(Mesh m)
    {
      if (m == null) return;
      try
      {
        if (!looked)
        {
          looked = true;
          field = typeof(GorillaLocomotion.GTPlayer).GetField("meshTrianglesDict", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public);
          if (field == null) SbLog.Warn("GTPlayer.meshTrianglesDict not found");
        }
        var p = GorillaLocomotion.GTPlayer.Instance;
        if (field == null || p == null) return;
        var dict = field.GetValue(p) as Dictionary<Mesh, int[]>;
        if (dict != null) dict.Remove(m);
      }
      catch { }
    }
  }

  internal sealed class SbVoxelChunk : MonoBehaviour
  {
    internal SbVoxelDim Dim;
    internal int Cx, Cz;
  }

  internal sealed class SbVoxelDim
  {
    internal const int N = 32;

    internal readonly byte Id;
    internal readonly Vector3 Origin;
    internal readonly float S;
    internal readonly int H;
    private readonly Action<int, int, byte[]> fill;          // one column of blocks, bottom to top
    private readonly Func<int, int, float> lavaAt;          // lava surface height (blocks) for a column, or -1
    internal Transform Root;
    internal Material Rock, Lava;
    internal int Layer;

    private sealed class Chunk
    {
      public int cx, cz;
      public GameObject go;
      public byte[] vox;                 // (N+2) x (N+2) columns of H, with a one column border
      public Mesh mesh, lavaMesh;
      public MeshCollider col;
    }

    private readonly Dictionary<long, Chunk> chunks = new Dictionary<long, Chunk>();
    private readonly Dictionary<long, byte> edits = new Dictionary<long, byte>();
    private readonly Dictionary<long, HashSet<long>> editsByChunk = new Dictionary<long, HashSet<long>>();
    private readonly Dictionary<long, byte> mine = new Dictionary<long, byte>();
    private readonly byte[] column;

    internal SbVoxelDim(byte id, Vector3 origin, float size, int height, Action<int, int, byte[]> fillColumn, Func<int, int, float> lava)
    {
      Id = id; Origin = origin; S = size; H = height; fill = fillColumn; lavaAt = lava;
      column = new byte[height];
    }

    // ---------------------------------------------------------------- keys
    private static long ChunkKey(int cx, int cz) { return ((long)cx << 32) ^ (uint)cz; }
    private static long PosKey(int x, int y, int z) { return ((long)(x + 1048576) << 29) | ((long)(z + 1048576) << 8) | (long)(y & 0xFF); }
    private static void Unpack(long k, out int x, out int y, out int z)
    {
      y = (int)(k & 0xFF);
      z = (int)((k >> 8) & 0x1FFFFF) - 1048576;
      x = (int)((k >> 29) & 0x1FFFFF) - 1048576;
    }
    private static int FloorDiv(int a, int b) { return a >= 0 ? a / b : -((-a + b - 1) / b); }

    internal void WorldToCell(Vector3 p, out int x, out int y, out int z)
    {
      Vector3 l = (p - Origin) / S;
      x = Mathf.FloorToInt(l.x); y = Mathf.FloorToInt(l.y); z = Mathf.FloorToInt(l.z);
    }

    internal Vector3 CellCentre(int x, int y, int z) { return Origin + new Vector3((x + 0.5f) * S, (y + 0.5f) * S, (z + 0.5f) * S); }

    // ---------------------------------------------------------------- blocks
    internal byte Get(int x, int y, int z)
    {
      if (y < 0 || y >= H) return (byte)SbVox.Bedrock;
      byte e;
      if (edits.TryGetValue(PosKey(x, y, z), out e)) return e;
      Chunk c;
      int cx = FloorDiv(x, N), cz = FloorDiv(z, N);
      if (chunks.TryGetValue(ChunkKey(cx, cz), out c))
        return c.vox[((x - cx * N + 1) * (N + 2) + (z - cz * N + 1)) * H + y];
      fill(x, z, column);
      return column[y];
    }

    // returns false when nothing changed (or bedrock)
    internal bool Set(int x, int y, int z, byte b, bool byMe)
    {
      if (y < 0 || y >= H) return false;
      byte cur = Get(x, y, z);
      if (cur == b || cur == (byte)SbVox.Bedrock) return false;
      long pk = PosKey(x, y, z);
      edits[pk] = b;
      if (byMe) mine[pk] = b;
      long ck = ChunkKey(FloorDiv(x, N), FloorDiv(z, N));
      HashSet<long> set;
      if (!editsByChunk.TryGetValue(ck, out set)) { set = new HashSet<long>(); editsByChunk[ck] = set; }
      set.Add(pk);
      // the chunk (and a neighbour when it is on the edge) gets a new mesh
      int lx = x - FloorDiv(x, N) * N, lz = z - FloorDiv(z, N) * N;
      int cx = FloorDiv(x, N), cz = FloorDiv(z, N);
      Rebuild(cx, cz);
      if (lx == 0) Rebuild(cx - 1, cz);
      if (lx == N - 1) Rebuild(cx + 1, cz);
      if (lz == 0) Rebuild(cx, cz - 1);
      if (lz == N - 1) Rebuild(cx, cz + 1);
      return true;
    }

    internal void ClearEdits()
    {
      edits.Clear(); editsByChunk.Clear(); mine.Clear();
      foreach (Chunk c in new List<Chunk>(chunks.Values)) Rebuild(c.cx, c.cz);
    }

    internal int MyEditCount { get { return mine.Count; } }

    // my edits, packed for the network in batches
    internal IEnumerable<byte[]> PackMine()
    {
      var list = new List<KeyValuePair<long, byte>>(mine);
      for (int i = 0; i < list.Count; i += 200)
      {
        int start = i, n = Mathf.Min(200, list.Count - i);
        yield return Pack(list, start, n);
      }
    }

    internal byte[] PackOne(int x, int y, int z, byte b)
    {
      return Pack(new List<KeyValuePair<long, byte>> { new KeyValuePair<long, byte>(PosKey(x, y, z), b) }, 0, 1);
    }

    private byte[] Pack(List<KeyValuePair<long, byte>> list, int start, int n)
    {
      using (var ms = new MemoryStream())
      using (var w = new BinaryWriter(ms))
      {
        w.Write(Id);
        w.Write((ushort)n);
        for (int k = 0; k < n; k++)
        {
          int x, y, z;
          Unpack(list[start + k].Key, out x, out y, out z);
          w.Write(x); w.Write((byte)y); w.Write(z); w.Write(list[start + k].Value);
        }
        w.Flush();
        return ms.ToArray();
      }
    }

    internal static void Unpack(BinaryReader r, Func<byte, SbVoxelDim> dimFor)
    {
      byte id = r.ReadByte();
      int n = r.ReadUInt16();
      SbVoxelDim d = dimFor(id);
      for (int k = 0; k < n; k++)
      {
        int x = r.ReadInt32(); int y = r.ReadByte(); int z = r.ReadInt32(); byte b = r.ReadByte();
        if (d != null && b <= (byte)SbVox.Bedrock && b != (byte)SbVox.Bedrock) d.Set(x, y, z, b, false);
      }
    }

    // ---------------------------------------------------------------- chunks
    internal bool Has(int cx, int cz) { return chunks.ContainsKey(ChunkKey(cx, cz)); }

    internal void ChunkOf(Vector3 p, out int cx, out int cz)
    {
      int x, y, z;
      WorldToCell(p, out x, out y, out z);
      cx = FloorDiv(x, N); cz = FloorDiv(z, N);
    }

    // builds missing chunks within `radius` of p, nearest first, at most `budget`
    internal void BuildNear(Vector3 p, int radius, int budget)
    {
      int pcx, pcz;
      ChunkOf(p, out pcx, out pcz);
      for (int ring = 0; ring <= radius && budget > 0; ring++)
        for (int dx = -ring; dx <= ring && budget > 0; dx++)
          for (int dz = -ring; dz <= ring && budget > 0; dz++)
          {
            if (Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dz)) != ring) continue;
            if (Has(pcx + dx, pcz + dz)) continue;
            Rebuild(pcx + dx, pcz + dz, true);
            budget--;
          }
    }

    internal void BuildRange(int cx0, int cz0, int cx1, int cz1)
    {
      for (int cx = cx0; cx <= cx1; cx++)
        for (int cz = cz0; cz <= cz1; cz++)
          Rebuild(cx, cz, true);
    }

    internal void DropFar(Vector3 p, int keep)
    {
      int pcx, pcz;
      ChunkOf(p, out pcx, out pcz);
      List<long> drop = null;
      foreach (KeyValuePair<long, Chunk> kv in chunks)
        if (Mathf.Max(Mathf.Abs(kv.Value.cx - pcx), Mathf.Abs(kv.Value.cz - pcz)) > keep) (drop = drop ?? new List<long>()).Add(kv.Key);
      if (drop == null) return;
      foreach (long k in drop) { Free(chunks[k]); chunks.Remove(k); }
    }

    internal void DestroyAll()
    {
      foreach (Chunk c in chunks.Values) Free(c);
      chunks.Clear();
    }

    private static void Free(Chunk c)
    {
      if (c.mesh != null) { SbGameMeshCache.Forget(c.mesh); UnityEngine.Object.Destroy(c.mesh); }
      if (c.lavaMesh != null) UnityEngine.Object.Destroy(c.lavaMesh);
      if (c.go != null) UnityEngine.Object.Destroy(c.go);
    }

    // (re)builds one chunk: only when it already exists, unless create
    private void Rebuild(int cx, int cz, bool create = false)
    {
      long key = ChunkKey(cx, cz);
      Chunk c;
      if (!chunks.TryGetValue(key, out c))
      {
        if (!create) return;
        c = new Chunk { cx = cx, cz = cz };
        chunks[key] = c;
      }
      FillChunk(c);
      MeshChunk(c);
    }

    private void FillChunk(Chunk c)
    {
      int W = N + 2;
      if (c.vox == null) c.vox = new byte[W * W * H];
      int x0 = c.cx * N - 1, z0 = c.cz * N - 1;
      for (int i = 0; i < W; i++)
        for (int j = 0; j < W; j++)
        {
          fill(x0 + i, z0 + j, column);
          Array.Copy(column, 0, c.vox, (i * W + j) * H, H);
        }
      // the edits in this chunk and its neighbours (the border columns)
      for (int dx = -1; dx <= 1; dx++)
        for (int dz = -1; dz <= 1; dz++)
        {
          HashSet<long> set;
          if (!editsByChunk.TryGetValue(ChunkKey(c.cx + dx, c.cz + dz), out set)) continue;
          foreach (long pk in set)
          {
            int x, y, z;
            Unpack(pk, out x, out y, out z);
            int i = x - x0, j = z - z0;
            if (i < 0 || j < 0 || i >= W || j >= W || y < 0 || y >= H) continue;
            c.vox[(i * W + j) * H + y] = edits[pk];
          }
        }
    }

    // ---------------------------------------------------------------- meshing
    private static int TopTile(byte b)
    {
      switch ((SbVox)b)
      {
        case SbVox.SoulSand: return SbBlockArt.SoulSand;
        case SbVox.Magma: return SbBlockArt.Magma;
        case SbVox.Glowstone: return SbBlockArt.Glowstone;
        case SbVox.EndStone: return SbBlockArt.EndStone;
        case SbVox.Obsidian: return SbBlockArt.Obsidian;
        case SbVox.Bedrock: return SbBlockArt.Bedrock;
        case SbVox.CrimsonNylium: return SbBlockArt.T("crimson_nylium");
        case SbVox.WarpedNylium: return SbBlockArt.T("warped_nylium");
        case SbVox.CrimsonStem: return SbBlockArt.T("crimson_stem_top");
        case SbVox.WarpedStem: return SbBlockArt.T("warped_stem_top");
        case SbVox.NetherWart: return SbBlockArt.T("nether_wart_block");
        case SbVox.WarpedWart: return SbBlockArt.T("warped_wart_block");
        case SbVox.Shroomlight: return SbBlockArt.T("shroomlight");
        case SbVox.Basalt: return SbBlockArt.T("basalt_top");
        case SbVox.Blackstone: return SbBlockArt.T("blackstone_top");
        case SbVox.SoulSoil: return SbBlockArt.T("soul_soil");
        case SbVox.NetherBricks: return SbBlockArt.NetherBricks;
        case SbVox.RedNetherBricks: return SbBlockArt.T("red_nether_bricks");
        case SbVox.PolishedBlackstoneBricks: return SbBlockArt.T("polished_blackstone_bricks");
        case SbVox.GildedBlackstone: return SbBlockArt.T("gilded_blackstone");
        case SbVox.QuartzOre: return SbBlockArt.T("nether_quartz_ore");
        case SbVox.NetherGoldOre: return SbBlockArt.T("nether_gold_ore");
        case SbVox.Gravel: return SbBlockArt.Gravel;
        case SbVox.CrackedBlackstoneBricks: return SbBlockArt.T("cracked_polished_blackstone_bricks");
        case SbVox.ChiseledBlackstone: return SbBlockArt.T("chiseled_polished_blackstone");
        case SbVox.CryingObsidian: return SbBlockArt.T("crying_obsidian");
        case SbVox.GoldBlock: return SbBlockArt.Gold;
        default: return SbBlockArt.Netherrack;
      }
    }

    // the sides of blocks that look different there (nylium, stems, basalt, blackstone)
    internal static int SideTile(byte b)
    {
      switch ((SbVox)b)
      {
        case SbVox.CrimsonNylium: return SbBlockArt.T("crimson_nylium_side");
        case SbVox.WarpedNylium: return SbBlockArt.T("warped_nylium_side");
        case SbVox.CrimsonStem: return SbBlockArt.T("crimson_stem");
        case SbVox.WarpedStem: return SbBlockArt.T("warped_stem");
        case SbVox.Basalt: return SbBlockArt.T("basalt_side");
        case SbVox.Blackstone: return SbBlockArt.T("blackstone");
      }
      return TopTile(b);
    }

    private static int BottomTile(byte b)
    {
      if (b == (byte)SbVox.CrimsonNylium || b == (byte)SbVox.WarpedNylium) return SbBlockArt.Netherrack;
      return TopTile(b);
    }

    internal static int AnyTile(byte b) { return TopTile(b); }

    private static bool Glows(byte b) { return b == (byte)SbVox.Glowstone || b == (byte)SbVox.Magma || b == (byte)SbVox.Shroomlight; }

    internal static string SoundOf(byte b)
    {
      switch ((SbVox)b)
      {
        case SbVox.SoulSand: return "sand";
        case SbVox.Glowstone: case SbVox.Shroomlight: return "glass";
        case SbVox.SoulSoil: case SbVox.Gravel: return "sand";
        case SbVox.CrimsonStem: case SbVox.WarpedStem: return "wood";
        case SbVox.NetherWart: case SbVox.WarpedWart: return "wool";
        default: return "stone";
      }
    }

    private static readonly List<Vector3> mv = new List<Vector3>(), mn = new List<Vector3>(), lv = new List<Vector3>(), ln = new List<Vector3>();
    private static readonly List<Vector2> mu = new List<Vector2>(), lu = new List<Vector2>();
    private static readonly List<int> rockT = new List<int>(), glowT = new List<int>(), lavaT = new List<int>();

    private void MeshChunk(Chunk c)
    {
      int W = N + 2;
      mv.Clear(); mn.Clear(); mu.Clear(); rockT.Clear(); glowT.Clear();
      lv.Clear(); ln.Clear(); lu.Clear(); lavaT.Clear();
      byte[] v = c.vox;
      for (int i = 1; i <= N; i++)
        for (int j = 1; j <= N; j++)
        {
          int baseIdx = (i * W + j) * H;
          int x = c.cx * N + i - 1, z = c.cz * N + j - 1;
          for (int y = 0; y < H; y++)
          {
            byte b = v[baseIdx + y];
            if (b == 0) continue;
            int tile = TopTile(b), side = SideTile(b), bottom = BottomTile(b);
            bool g = Glows(b);
            // a face only where the neighbour is air (the world's bottom and top count as solid)
            if (y + 1 < H && v[baseIdx + y + 1] == 0) Face(x, y, z, 4, tile, g);
            if (y > 0 && v[baseIdx + y - 1] == 0) Face(x, y, z, 5, bottom, g);
            if (v[((i + 1) * W + j) * H + y] == 0) Face(x, y, z, 0, side, g);
            if (v[((i - 1) * W + j) * H + y] == 0) Face(x, y, z, 1, side, g);
            if (v[(i * W + j + 1) * H + y] == 0) Face(x, y, z, 2, side, g);
            if (v[(i * W + j - 1) * H + y] == 0) Face(x, y, z, 3, side, g);
          }
          if (lavaAt != null)
          {
            float ly = lavaAt(x, z);
            if (ly >= 0f)
            {
              float yy = ly * S - 0.08f, x0 = x * S, x1 = x0 + S, z0 = z * S, z1 = z0 + S;
              int li = lv.Count;
              lv.Add(new Vector3(x0, yy, z0)); lv.Add(new Vector3(x1, yy, z0)); lv.Add(new Vector3(x1, yy, z1)); lv.Add(new Vector3(x0, yy, z1));
              for (int k = 0; k < 4; k++) ln.Add(Vector3.up);
              lu.Add(new Vector2(x, z)); lu.Add(new Vector2(x + 1, z)); lu.Add(new Vector2(x + 1, z + 1)); lu.Add(new Vector2(x, z + 1));
              lavaT.Add(li); lavaT.Add(li + 2); lavaT.Add(li + 1); lavaT.Add(li); lavaT.Add(li + 3); lavaT.Add(li + 2);
            }
          }
        }

      if (c.go == null)
      {
        c.go = new GameObject((Id == 1 ? "nether" : "end") + " chunk " + c.cx + "," + c.cz);
        c.go.transform.SetParent(Root, false);
        c.go.layer = Layer;
        c.go.AddComponent<MeshFilter>();
        c.go.AddComponent<MeshRenderer>().sharedMaterials = new[] { Rock, SbMats.BlockGlow };
        var tag = c.go.AddComponent<SbVoxelChunk>();
        tag.Dim = this; tag.Cx = c.cx; tag.Cz = c.cz;
      }
      if (c.mesh == null) c.mesh = new Mesh { name = "SB voxel chunk", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32, hideFlags = HideFlags.DontSave };
      SbGameMeshCache.Forget(c.mesh);   // new triangles: the game must not use its old copy
      c.mesh.Clear();
      c.mesh.SetVertices(mv); c.mesh.SetNormals(mn); c.mesh.SetUVs(0, mu);
      c.mesh.subMeshCount = 2;
      c.mesh.SetTriangles(rockT, 0); c.mesh.SetTriangles(glowT, 1);
      c.mesh.RecalculateBounds();
      c.go.GetComponent<MeshFilter>().sharedMesh = c.mesh;
      if (c.col == null) c.col = c.go.AddComponent<MeshCollider>();
      c.col.sharedMesh = null;
      if (mv.Count > 0) c.col.sharedMesh = c.mesh;

      if (lv.Count > 0 && Lava != null)
      {
        Transform lt = c.go.transform.Find("lava");
        if (lt == null)
        {
          var lg = new GameObject("lava");
          lg.transform.SetParent(c.go.transform, false);
          lg.AddComponent<MeshFilter>();
          lg.AddComponent<MeshRenderer>().sharedMaterial = Lava;
          lt = lg.transform;
        }
        if (c.lavaMesh == null) c.lavaMesh = new Mesh { name = "SB voxel lava", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32, hideFlags = HideFlags.DontSave };
        c.lavaMesh.Clear();
        c.lavaMesh.SetVertices(lv); c.lavaMesh.SetNormals(ln); c.lavaMesh.SetUVs(0, lu); c.lavaMesh.SetTriangles(lavaT, 0);
        c.lavaMesh.RecalculateBounds();
        lt.GetComponent<MeshFilter>().sharedMesh = c.lavaMesh;
      }
    }

    // one block face: 0 +X, 1 -X, 2 +Z, 3 -Z, 4 top, 5 bottom
    private void Face(int x, int y, int z, int dir, int tile, bool glow)
    {
      float x0 = x * S, x1 = x0 + S, y0 = y * S, y1 = y0 + S, z0 = z * S, z1 = z0 + S;
      Vector3 a, b, cc, d, n;
      switch (dir)
      {
        case 0: a = new Vector3(x1, y0, z0); b = new Vector3(x1, y0, z1); cc = new Vector3(x1, y1, z1); d = new Vector3(x1, y1, z0); n = Vector3.right; break;
        case 1: a = new Vector3(x0, y0, z1); b = new Vector3(x0, y0, z0); cc = new Vector3(x0, y1, z0); d = new Vector3(x0, y1, z1); n = Vector3.left; break;
        case 2: a = new Vector3(x1, y0, z1); b = new Vector3(x0, y0, z1); cc = new Vector3(x0, y1, z1); d = new Vector3(x1, y1, z1); n = Vector3.forward; break;
        case 3: a = new Vector3(x0, y0, z0); b = new Vector3(x1, y0, z0); cc = new Vector3(x1, y1, z0); d = new Vector3(x0, y1, z0); n = Vector3.back; break;
        case 4: a = new Vector3(x0, y1, z0); b = new Vector3(x1, y1, z0); cc = new Vector3(x1, y1, z1); d = new Vector3(x0, y1, z1); n = Vector3.up; break;
        default: a = new Vector3(x0, y0, z1); b = new Vector3(x1, y0, z1); cc = new Vector3(x1, y0, z0); d = new Vector3(x0, y0, z0); n = Vector3.down; break;
      }
      Rect r = SbBlockArt.TileRect(tile);
      int i = mv.Count;
      mv.Add(a); mv.Add(b); mv.Add(cc); mv.Add(d);
      for (int k = 0; k < 4; k++) mn.Add(n);
      mu.Add(new Vector2(r.xMin, r.yMin)); mu.Add(new Vector2(r.xMax, r.yMin)); mu.Add(new Vector2(r.xMax, r.yMax)); mu.Add(new Vector2(r.xMin, r.yMax));
      List<int> t = glow ? glowT : rockT;
      if (Vector3.Dot(Vector3.Cross(b - a, cc - a), n) >= 0f) { t.Add(i); t.Add(i + 1); t.Add(i + 2); t.Add(i); t.Add(i + 2); t.Add(i + 3); }
      else { t.Add(i); t.Add(i + 2); t.Add(i + 1); t.Add(i); t.Add(i + 3); t.Add(i + 2); }
    }
  }

  // ==================================================================== world side
  internal sealed partial class SandBoxWorld
  {
    private SbVoxelDim netherDim, endDim;

    // (made on demand: edits from others are kept even before you go there)
    private SbVoxelDim DimFor(byte id) { return id == 1 ? EnsureNetherDim() : id == 2 ? EnsureEndDim() : null; }

    // grip + trigger on the Nether / End ground: the block breaks (for everyone)
    internal bool BreakTerrain(Vector3 origin, Vector3 dir)
    {
      RaycastHit hit;
      if (!Physics.Raycast(origin, dir.normalized, out hit, 8f, ~0, QueryTriggerInteraction.Ignore)) return false;
      SbVoxelChunk tag = hit.collider != null ? hit.collider.GetComponent<SbVoxelChunk>() : null;
      if (tag == null || tag.Dim == null) return false;
      SbVoxelDim d = tag.Dim;
      int x, y, z;
      d.WorldToCell(hit.point - hit.normal * d.S * 0.5f, out x, out y, out z);
      byte was = d.Get(x, y, z);
      if (was == (byte)SbVox.Bedrock)
      {
        SbFx.Play(SbSound.UiHover, hit.point, 0.4f, 0.5f);
        return true;
      }
      if (!d.Set(x, y, z, (byte)SbVox.Air, true)) return false;
      Vector3 c = d.CellCentre(x, y, z);
      SbFx.McBreak(c, SbVoxelDim.SideTile(was), d.S, Color.white);
      SbFx.PlayMc(SbVoxelDim.SoundOf(was), SbSound.Break, c, 1f);
      SbNet.SendTerrain(d.PackOne(x, y, z, (byte)SbVox.Air));
      return true;
    }

    internal void NetTerrain(byte[] data)
    {
      try
      {
        using (var ms = new MemoryStream(data))
        using (var r = new BinaryReader(ms))
          SbVoxelDim.Unpack(r, DimFor);
      }
      catch (Exception ex) { SbLog.Warn("terrain: " + ex.Message); }
    }

    internal IEnumerable<byte[]> MyTerrainEdits()
    {
      if (netherDim != null) foreach (byte[] b in netherDim.PackMine()) yield return b;
      if (endDim != null) foreach (byte[] b in endDim.PackMine()) yield return b;
    }

    // a new lobby: its own Nether, nobody's broken blocks
    private void ResetTerrainEdits()
    {
      if (netherDim != null) netherDim.ClearEdits();
      if (endDim != null) endDim.ClearEdits();
    }
  }
}
