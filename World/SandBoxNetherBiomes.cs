using UnityEngine;

namespace UnknownsSandbox
{
  // the Nether like Minecraft's. Five biomes, each a few hundred blocks wide:
  //  - Nether Wastes: netherrack hills and big lava seas, magma by the shores, glowstone,
  //    gravel and soul sand patches, quartz and gold ore in the rock.
  //  - Crimson Forest: red nylium, crimson fungi (stems, wart blocks, shroomlights).
  //  - Warped Forest: blue-green nylium and warped fungi.
  //  - Soul Sand Valley: soul sand and soul soil flats with basalt pillars.
  //  - Basalt Deltas: jagged basalt and blackstone, magma, small lava pools.
  // Structures: Nether fortresses (nether brick bridges on pillars, crossing at a hall,
  // where blazes and wither skeletons live) and bastions (big blackstone castles with
  // gilded blackstone, home of piglin brutes). Everything comes from the lobby's seed,
  // so everyone in a lobby walks the same Nether.
  internal sealed partial class SandBoxWorld
  {
    private enum NBiome { Wastes, Crimson, Warped, SoulValley, Basalt }

    private const int FortressY = 36;     // the bridges' floor (high above the lava, through the hills)
    private const int StructRegion = 120; // one fortress or bastion (or none) per region

    private static uint Hash(int x, int z, int k)
    {
      unchecked
      {
        uint h = (uint)(x * 73856093) ^ (uint)(z * 19349663) ^ (uint)(k * 83492791);
        h ^= h >> 13; h *= 0x5bd1e995; h ^= h >> 15;
        return h;
      }
    }

    private NBiome BiomeAt(int x, int z)
    {
      if (Mathf.Max(Mathf.Abs(x), Mathf.Abs(z)) < 14) return NBiome.Wastes;   // around the arrival portal
      float t = NN(x, z, 0.0075f, 21), u = NN(x, z, 0.0075f, 22);
      if (t < 0.36f) return u < 0.5f ? NBiome.SoulValley : NBiome.Basalt;
      if (t > 0.62f) return u < 0.5f ? NBiome.Crimson : NBiome.Warped;
      return NBiome.Wastes;
    }

    // the floor (first air), the ceiling (first rock above) and the floor's top block
    private void NetherShape(int x, int z, out int floor, out int ceil, out NBiome biome)
    {
      int H = NetherHeight;
      biome = BiomeAt(x, z);
      // a taller Nether: hills, cliffs and terraces at many heights
      float f = 9f + 16f * NN(x, z, 0.025f, 1) + 4f * NN(x, z, 0.1f, 2);
      f = Mathf.Lerp(f, Mathf.Round(f / 5f) * 5f, 0.6f);   // terraces
      float c = H - 8f - 10f * NN(x, z, 0.05f, 3) - 18f * Mathf.Pow(NN(x, z, 0.25f, 4), 3f);
      switch (biome)
      {
        case NBiome.Wastes:
          {
            // the lava seas: wide basins well below the lava
            float sea = Mathf.InverseLerp(0.5f, 0.66f, NN(x, z, 0.018f, 9));
            f = Mathf.Lerp(f, 3f, sea);
            if (NN(x, z, 0.12f, 5) > 0.82f) f = c;   // pillars
            break;
          }
        case NBiome.Crimson:
        case NBiome.Warped:
          f = 13f + 9f * NN(x, z, 0.03f, 11) + 2f * NN(x, z, 0.15f, 12);
          break;
        case NBiome.SoulValley:
          f = 12f + 5f * NN(x, z, 0.04f, 13);
          if (NN(x, z, 0.2f, 14) > 0.8f) f = c;   // basalt pillars
          break;
        case NBiome.Basalt:
          f = 9f + 18f * Mathf.Abs(NN(x, z, 0.07f, 15) - 0.5f) * 2f + 4f * NN(x, z, 0.3f, 16);
          if (NN(x, z, 0.25f, 17) > 0.8f) f = Mathf.Min(c, f + 14f);   // jagged spires
          break;
      }
      int dc = Mathf.Max(Mathf.Abs(x), Mathf.Abs(z));
      if (dc < 8) { f = NetherFloorAtSpawn; c = Mathf.Max(c, NetherFloorAtSpawn + 18); }
      floor = Mathf.Clamp(Mathf.RoundToInt(f), 1, H);
      ceil = Mathf.Clamp(Mathf.RoundToInt(c), floor, H);
    }

    // kept for the old callers (mob ground, lava): the shape only
    private void NetherColumn(int x, int z, out int floor, out int ceil, out int floorTile, out bool glowCeil)
    {
      NBiome b;
      NetherShape(x, z, out floor, out ceil, out b);
      floorTile = SbBlockArt.Netherrack;
      glowCeil = false;
    }

    private byte TopBlock(int x, int z, int floor, NBiome biome)
    {
      switch (biome)
      {
        case NBiome.Crimson: return (byte)SbVox.CrimsonNylium;
        case NBiome.Warped: return (byte)SbVox.WarpedNylium;
        case NBiome.SoulValley: return NN(x, z, 0.3f, 6) > 0.45f ? (byte)SbVox.SoulSand : (byte)SbVox.SoulSoil;
        case NBiome.Basalt:
          if (floor <= NetherLava + 1 && NN(x, z, 0.4f, 8) > 0.5f) return (byte)SbVox.Magma;
          return NN(x, z, 0.25f, 18) > 0.55f ? (byte)SbVox.Blackstone : (byte)SbVox.Basalt;
        default:
          if (floor <= NetherLava + 1 && NN(x, z, 0.5f, 8) > 0.42f) return (byte)SbVox.Magma;
          float p = NN(x, z, 0.15f, 6);
          if (p > 0.74f) return (byte)SbVox.SoulSand;
          if (p < 0.2f) return (byte)SbVox.Gravel;
          return (byte)SbVox.Netherrack;
      }
    }

    private byte FillerBlock(NBiome biome)
    {
      switch (biome)
      {
        case NBiome.SoulValley: return (byte)SbVox.SoulSoil;
        case NBiome.Basalt: return (byte)SbVox.Basalt;
        default: return (byte)SbVox.Netherrack;
      }
    }

    // ---------------------------------------------------------------- one column
    private void FillNether(int x, int z, byte[] col)
    {
      int H = NetherHeight;
      int f, c; NBiome biome;
      NetherShape(x, z, out f, out c, out biome);
      byte top = TopBlock(x, z, f, biome), fill = FillerBlock(biome);
      bool pillar = f >= c;
      bool glow = (biome == NBiome.Wastes || biome == NBiome.Crimson) && NN(x, z, 0.2f, 7) > 0.74f;
      for (int y = 0; y < H; y++)
      {
        byte b;
        if (y == 0 || y == H - 1) b = (byte)SbVox.Bedrock;
        else if (pillar) b = biome == NBiome.SoulValley || biome == NBiome.Basalt ? (byte)SbVox.Basalt : (byte)SbVox.Netherrack;
        else if (y < f) b = y == f - 1 ? top : fill;
        else if (y >= c) b = glow && (y == c || y == c + 1) ? (byte)SbVox.Glowstone : (byte)SbVox.Netherrack;
        else b = 0;
        // ores in the netherrack
        if (b == (byte)SbVox.Netherrack && (y < f - 1 || y > c))
        {
          uint h = Hash(x, z, y * 31 + netherSeed);
          if (h % 61u == 0u) b = (byte)SbVox.QuartzOre;
          else if (h % 113u == 1u) b = (byte)SbVox.NetherGoldOre;
        }
        col[y] = b;
      }
      if (!pillar && biome != NBiome.Crimson && biome != NBiome.Warped) AddLedge(x, z, f, c, top, fill, col);
      if (!pillar && (biome == NBiome.Crimson || biome == NBiome.Warped)) AddFungi(x, z, col, biome);
      if (!pillar) AddRuinedPortal(x, z, col);
      AddStructures(x, z, f, col);
    }

    // rock shelves high over the ground (overhangs and upper levels, like the
    // Minecraft Nether's), only where there is room above and below
    private void AddLedge(int x, int z, int f, int c, byte top, byte fill, byte[] col)
    {
      float s = NN(x, z, 0.045f, 31);
      if (s < 0.63f) return;
      int shelfTop = f + 9 + (int)(14f * NN(x, z, 0.02f, 32));
      int thick = 2 + (int)((s - 0.63f) * 14f);
      if (shelfTop > c - 6 || shelfTop - thick < f + 5) return;
      for (int y = shelfTop - thick; y < shelfTop; y++) col[y] = y == shelfTop - 1 ? top : fill;
    }

    // ruined portals: a broken obsidian frame (some crying obsidian), gold blocks
    // and magma round its foot, scattered over the Nether
    private const int PortalRegion = 56;

    private void AddRuinedPortal(int x, int z, byte[] col)
    {
      int rx = Mathf.FloorToInt(x / (float)PortalRegion), rz = Mathf.FloorToInt(z / (float)PortalRegion);
      uint h = Hash(rx, rz, 9191 + netherSeed);
      if (h % 3u != 0u) return;
      int cx = rx * PortalRegion + 12 + (int)(h / 3u % 32u), cz = rz * PortalRegion + 12 + (int)(h / 96u % 32u);
      if (Mathf.Abs(cx) < 24 && Mathf.Abs(cz) < 24) return;   // not by the arrival portal
      int dx = x - cx, dz = z - cz;
      if (Mathf.Abs(dx) > 4 || Mathf.Abs(dz) > 4) return;
      int cf, cc; NBiome cb;
      NetherShape(cx, cz, out cf, out cc, out cb);
      if (cf >= cc || cc - cf < 9) return;
      int y0 = Mathf.Max(cf, NetherLava + 1);
      bool alongX = (h & 1u) == 0u;
      int u = alongX ? dx : dz, w = alongX ? dz : dx;
      // the ground round it: netherrack, magma and gold
      uint g = Hash(x, z, 5150 + netherSeed);
      if (Mathf.Abs(dx) + Mathf.Abs(dz) <= 5 && g % 3u != 0u && y0 - 1 > 0)
        col[y0 - 1] = g % 7u == 0u ? (byte)SbVox.Magma : g % 5u == 0u ? (byte)SbVox.GildedBlackstone : (byte)SbVox.Netherrack;
      if (Mathf.Abs(dx) + Mathf.Abs(dz) <= 4 && g % 11u == 0u && col[y0] == 0) col[y0] = (byte)SbVox.GoldBlock;
      // the frame: 4 wide, 5 tall (outside), standing across u
      if (w != 0 || u < -1 || u > 2) return;
      bool side = u == -1 || u == 2;
      int tall = 5 - (int)(h / 3072u % 3u);   // often its top is broken off
      for (int ly = 0; ly < 5; ly++)
      {
        if (!(side || ly == 0 || ly == 4)) continue;
        if (ly >= tall && !(ly == 0)) continue;
        uint k = Hash(x, z, ly * 17 + 333 + netherSeed);
        if (k % 4u == 0u) continue;   // missing blocks
        int y = y0 + ly;
        if (y >= NetherHeight - 1) break;
        col[y] = k % 5u == 0u ? (byte)SbVox.CryingObsidian : (byte)SbVox.Obsidian;
      }
    }

    // big crimson / warped fungi: a stem, a cap of wart blocks with shroomlights under it
    private void AddFungi(int x, int z, byte[] col, NBiome biome)
    {
      byte stem = biome == NBiome.Crimson ? (byte)SbVox.CrimsonStem : (byte)SbVox.WarpedStem;
      byte wart = biome == NBiome.Crimson ? (byte)SbVox.NetherWart : (byte)SbVox.WarpedWart;
      // taller, with many heights (5 - 16 blocks); the tall ones have wider, deeper caps
      for (int dx = -3; dx <= 3; dx++)
        for (int dz = -3; dz <= 3; dz++)
        {
          int tx = x + dx, tz = z + dz;
          uint h = Hash(tx, tz, 777 + netherSeed);
          if (h % 29u != 0u) continue;
          int height = 5 + (int)(h / 29u % 12u);
          int R = height >= 10 ? 3 : 2;
          int ax = Mathf.Abs(dx), az = Mathf.Abs(dz);
          if (ax > R || az > R) continue;
          int tf, tc; NBiome tb;
          NetherShape(tx, tz, out tf, out tc, out tb);
          if (tb != biome || tc - tf < height + 4) continue;
          int top = tf + height;
          if (dx == 0 && dz == 0)
            for (int y = tf; y < top && y < NetherHeight - 1; y++) col[y] = stem;
          // the cap: R wide (no corners) down its sides, one narrower layer on top
          int depth = height >= 10 ? 4 : 2;
          for (int y = top - depth; y <= top + 1 && y < NetherHeight - 1; y++)
          {
            bool upper = y == top + 1;
            if (upper ? (ax > R - 1 || az > R - 1) : (ax == R && az == R)) continue;
            // under the top it is a hollow skirt (the stem shows inside)
            if (!upper && y < top - 1 && ax < R && az < R && !(dx == 0 && dz == 0)) continue;
            if (col[y] != 0 && col[y] != wart) continue;
            col[y] = (!upper && y <= top - 1 && Hash(x, z, y + 99) % 6u == 0u) ? (byte)SbVox.Shroomlight : wart;
          }
        }
    }

    // ---------------------------------------------------------------- fortresses and bastions
    // the structure in a region: 0 none, 1 fortress, 2 bastion, and where its centre is
    private int StructureIn(int rx, int rz, out int cx, out int cz)
    {
      uint h = Hash(rx, rz, 4242 + netherSeed);
      cx = rx * StructRegion + StructRegion / 2 + (int)(h % 21u) - 10;
      cz = rz * StructRegion + StructRegion / 2 + (int)(h / 21u % 21u) - 10;
      // never on the arrival portal
      if (Mathf.Abs(cx) < 40 && Mathf.Abs(cz) < 40) return 0;
      uint k = h / 441u % 5u;
      return k < 2 ? 1 : k < 4 ? 2 : 0;
    }

    private void AddStructures(int x, int z, int floor, byte[] col)
    {
      int rx = Mathf.FloorToInt(x / (float)StructRegion), rz = Mathf.FloorToInt(z / (float)StructRegion);
      int cx, cz;
      int kind = StructureIn(rx, rz, out cx, out cz);
      if (kind == 1) Fortress(x - cx, z - cz, floor, col);
      else if (kind == 2) Bastion(x - cx, z - cz, x, z, cx, cz, col);
    }

    // two long nether brick bridges crossing at a hall, on pillars down to the ground / lava
    private void Fortress(int dx, int dz, int floor, byte[] col)
    {
      int Y = FortressY, H = NetherHeight;
      const int arm = 44;
      byte brick = (byte)SbVox.NetherBricks;
      bool hall = Mathf.Abs(dx) <= 7 && Mathf.Abs(dz) <= 7;
      if (hall)
      {
        bool edge = Mathf.Abs(dx) == 7 || Mathf.Abs(dz) == 7;
        bool door = (Mathf.Abs(dx) <= 1 || Mathf.Abs(dz) <= 1) && edge;
        for (int y = Y - 1; y <= Y + 7 && y < H - 1; y++)
        {
          if (y == Y - 1 || y == Y || y == Y + 7) { col[y] = brick; continue; }
          col[y] = edge && !(door && y <= Y + 3) ? brick : (byte)0;
        }
        // the blaze spawner corner (red bricks) and pillars below
        if (Mathf.Abs(dx) <= 1 && Mathf.Abs(dz) <= 1) col[Y + 1] = (byte)SbVox.RedNetherBricks;
        if ((Mathf.Abs(dx) == 6 || Mathf.Abs(dx) == 0) && (Mathf.Abs(dz) == 6 || Mathf.Abs(dz) == 0))
          for (int y = Mathf.Max(1, floor - 1); y < Y - 1; y++) col[y] = brick;
        return;
      }
      int along, across;
      if (Mathf.Abs(dz) <= 2 && Mathf.Abs(dx) <= arm) { along = dx; across = Mathf.Abs(dz); }
      else if (Mathf.Abs(dx) <= 2 && Mathf.Abs(dz) <= arm) { along = dz; across = Mathf.Abs(dx); }
      else return;
      // the deck, low walls with gaps, open above
      col[Y] = brick;
      col[Y - 1] = across <= 1 ? brick : col[Y - 1];
      for (int y = Y + 1; y <= Y + 5 && y < H - 1; y++) col[y] = 0;
      if (across == 2)
      {
        col[Y + 1] = brick;
        if (((along % 4) + 4) % 4 != 0) col[Y + 2] = brick;
      }
      // pillars every 10 blocks, down to the floor (through the lava)
      if (((along % 10) + 10) % 10 == 0 && across <= 1)
        for (int y = Mathf.Max(1, floor - 1); y < Y - 1; y++) col[y] = brick;
    }

    // a bastion: a blackstone castle 19 x 19, four floors, battlements and a gold room
    private void Bastion(int dx, int dz, int x, int z, int cx, int cz, byte[] col)
    {
      const int R = 9;
      int ax = Mathf.Abs(dx), az = Mathf.Abs(dz);
      if (ax > R || az > R) return;
      int H = NetherHeight;
      // it stands on the ground at its centre (above the lava)
      int cf, cc; NBiome cb;
      NetherShape(cx, cz, out cf, out cc, out cb);
      int y0 = Mathf.Clamp(Mathf.Max(cf, NetherLava + 2), 2, H - 18);
      bool wall = ax == R || az == R;
      bool door = wall && (ax <= 1 || az <= 1);
      byte bricks = (byte)SbVox.PolishedBlackstoneBricks;
      for (int y = 1; y < H - 1; y++)
      {
        int ly = y - y0;
        byte b = col[y];
        if (ly < 0) b = (byte)SbVox.Blackstone;                     // the foundation
        else if (ly <= 14)
        {
          if (wall) b = (door && ly >= 1 && ly <= 3) ? (byte)0 : (Hash(x, z, y) % 7u == 0u ? (byte)SbVox.CrackedBlackstoneBricks : bricks);
          else if (ly == 0) b = ax <= 2 && az <= 2 ? (byte)SbVox.GildedBlackstone : bricks;   // the treasure room floor
          else if (ly == 5 || ly == 10) b = (ax <= 3 && az <= 3) ? (byte)0 : (byte)SbVox.Blackstone;   // floors round an open middle
          else if (ly == 14) b = bricks;                                  // the roof
          else if ((ax == 5 && az == 5) && ly < 14) b = (byte)SbVox.ChiseledBlackstone;   // inner columns
          else b = 0;
          if (ly == 4 && ax <= 1 && az <= 1) b = (byte)SbVox.GildedBlackstone;
        }
        else if (ly == 15 && wall && ((ax + az) % 2 == 0)) b = bricks;   // battlements
        col[y] = b;
      }
    }

    // ---------------------------------------------------------------- lava
    private float NetherLavaAt(int x, int z)
    {
      int f, c; NBiome b;
      NetherShape(x, z, out f, out c, out b);
      return f < NetherLava && c > f ? NetherLava : -1f;
    }

    // ---------------------------------------------------------------- for the natural mobs
    // the Nether mobs that live around a point (by biome and structure)
    internal SbMobType[] NetherMobsAt(Vector3 p)
    {
      float s = NetherBlock;
      int x = Mathf.FloorToInt((p.x - NetherOrigin.x) / s), z = Mathf.FloorToInt((p.z - NetherOrigin.z) / s);
      int rx = Mathf.FloorToInt(x / (float)StructRegion), rz = Mathf.FloorToInt(z / (float)StructRegion);
      int cx, cz;
      int kind = StructureIn(rx, rz, out cx, out cz);
      // blazes and wither skeletons only on the fortress itself (its hall and bridges)
      int fx = Mathf.Abs(x - cx), fz = Mathf.Abs(z - cz);
      bool onFortress = kind == 1 && ((fx <= 7 && fz <= 7) || (fz <= 2 && fx <= 44) || (fx <= 2 && fz <= 44));
      bool near = Mathf.Abs(x - cx) < 50 && Mathf.Abs(z - cz) < 50;
      if (onFortress) return new[] { SbMobType.Blaze, SbMobType.WitherSkeleton, SbMobType.Blaze, SbMobType.WitherSkeleton, SbMobType.MagmaCube };
      if (near && kind == 2) return new[] { SbMobType.PiglinBrute, SbMobType.Piglin, SbMobType.Hoglin };
      switch (BiomeAt(x, z))
      {
        case NBiome.Crimson: return new[] { SbMobType.Hoglin, SbMobType.Piglin, SbMobType.Strider };
        case NBiome.Warped: return new[] { SbMobType.Enderman, SbMobType.Strider };
        case NBiome.SoulValley: return new[] { SbMobType.Skeleton, SbMobType.Ghast, SbMobType.Strider };
        case NBiome.Basalt: return new[] { SbMobType.MagmaCube, SbMobType.Ghast };
        default: return new[] { SbMobType.ZombifiedPiglin, SbMobType.Piglin, SbMobType.MagmaCube, SbMobType.Ghast, SbMobType.Strider };
      }
    }

    internal static readonly SbMobType[] AllNetherMobs =
    {
      SbMobType.Piglin, SbMobType.ZombifiedPiglin, SbMobType.MagmaCube, SbMobType.Blaze, SbMobType.WitherSkeleton, SbMobType.PiglinBrute,
      SbMobType.Hoglin, SbMobType.Strider, SbMobType.Enderman, SbMobType.Skeleton, SbMobType.Ghast
    };
  }
}
