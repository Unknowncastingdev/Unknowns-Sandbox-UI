using System;
using System.Collections.Generic;
using GorillaLocomotion;
using UnityEngine;

namespace UnknownsSandbox
{
  // Minecraft shapes and redstone.
  //  Shapes: slabs, stairs, fences, walls, doors and beds, each a proper Minecraft shape
  //  (and collider) made of boxes with the block's texture cropped like Minecraft does.
  //  Redstone: levers (touch to flip), stone buttons (touch: on for a second) and
  //  pressure plates (on while a player or a thing stands on them) power the blocks
  //  around them, and through the block they sit on, like Minecraft. Powered: redstone
  //  lamps light up, note blocks play (touch one to change its note), pistons push the
  //  block above them up (sticky pistons pull it back), dispensers shoot, doors open and
  //  TNT is lit. Wooden doors also open when touched.
  //  Everyone works out the power from what they see, so it is the same for everyone;
  //  flipping a lever or a door is sent to the others (Op.Toggle).
  internal enum SbShape { Cube, Slab, Stairs, Fence, Wall, Door, Bed, Lever, Button, Plate, Skull, Rail, Dust, Repeater, Comparator, Torch }

  internal static class SbShapes
  {
    internal struct Box
    {
      public Vector3 Lo, Hi;
      public int Top, Side, Bottom;
      public Box(Vector3 lo, Vector3 hi, int top, int side, int bottom) { Lo = lo; Hi = hi; Top = top; Side = side; Bottom = bottom; }
    }

    private static readonly Dictionary<string, Mesh> meshes = new Dictionary<string, Mesh>(StringComparer.Ordinal);

    // the boxes of a shape (cell units, -0.5 .. 0.5, -Z faces whoever placed it); state:
    // a door open, a lever / button / plate on
    internal static List<Box> Boxes(SbItem it, bool state)
    {
      int t = it.Top, s = it.Side, b = it.Bottom;
      var l = new List<Box>();
      switch (it.Shape)
      {
        case SbShape.Slab:
          l.Add(new Box(new Vector3(-0.5f, -0.5f, -0.5f), new Vector3(0.5f, 0f, 0.5f), t, s, b));
          break;
        case SbShape.Stairs:
          l.Add(new Box(new Vector3(-0.5f, -0.5f, -0.5f), new Vector3(0.5f, 0f, 0.5f), t, s, b));
          l.Add(new Box(new Vector3(-0.5f, 0f, 0f), new Vector3(0.5f, 0.5f, 0.5f), t, s, b));
          break;
        case SbShape.Fence:
          l.Add(new Box(new Vector3(-0.125f, -0.5f, -0.125f), new Vector3(0.125f, 0.5f, 0.125f), t, s, b));
          foreach (float y in new[] { -0.125f, 0.19f })
          {
            l.Add(new Box(new Vector3(-0.5f, y, -0.0625f), new Vector3(0.5f, y + 0.1875f, 0.0625f), t, s, b));
            l.Add(new Box(new Vector3(-0.0625f, y, -0.5f), new Vector3(0.0625f, y + 0.1875f, 0.5f), t, s, b));
          }
          break;
        case SbShape.Wall:
          l.Add(new Box(new Vector3(-0.25f, -0.5f, -0.25f), new Vector3(0.25f, 0.5f, 0.25f), t, s, b));
          l.Add(new Box(new Vector3(-0.5f, -0.5f, -0.1875f), new Vector3(0.5f, 0.375f, 0.1875f), t, s, b));
          l.Add(new Box(new Vector3(-0.1875f, -0.5f, -0.5f), new Vector3(0.1875f, 0.375f, 0.5f), t, s, b));
          break;
        case SbShape.Door:
          // two blocks tall; closed across the back of its cell, open along its left side
          if (!state)
          {
            l.Add(new Box(new Vector3(-0.5f, -0.5f, 0.3125f), new Vector3(0.5f, 0.5f, 0.5f), s, b, s));
            l.Add(new Box(new Vector3(-0.5f, 0.5f, 0.3125f), new Vector3(0.5f, 1.5f, 0.5f), s, t, s));
          }
          else
          {
            l.Add(new Box(new Vector3(-0.5f, -0.5f, -0.5f), new Vector3(-0.3125f, 0.5f, 0.5f), s, b, s));
            l.Add(new Box(new Vector3(-0.5f, 0.5f, -0.5f), new Vector3(-0.3125f, 1.5f, 0.5f), s, t, s));
          }
          break;
        case SbShape.Bed:
          // planks frame and legs, the coloured blanket, a white pillow at the back
          l.Add(new Box(new Vector3(-0.5f, -0.3125f, -0.5f), new Vector3(0.5f, -0.0625f, 0.5f), t, t, SbBlockArt.Planks));
          foreach (float x in new[] { -0.5f, 0.3125f })
            foreach (float z in new[] { -0.5f, 0.3125f })
              l.Add(new Box(new Vector3(x, -0.5f, z), new Vector3(x + 0.1875f, -0.3125f, z + 0.1875f), SbBlockArt.Planks, SbBlockArt.Planks, SbBlockArt.Planks));
          l.Add(new Box(new Vector3(-0.4375f, -0.0625f, 0.125f), new Vector3(0.4375f, 0.0625f, 0.4375f), b, b, b));
          break;
        case SbShape.Lever:
          l.Add(new Box(new Vector3(-0.1875f, -0.5f, -0.25f), new Vector3(0.1875f, -0.3125f, 0.25f), SbBlockArt.Cobble, SbBlockArt.Cobble, SbBlockArt.Cobble));
          {
            // the handle leans back (off) or forward (on)
            float z0 = state ? -0.1875f : 0.0625f;
            l.Add(new Box(new Vector3(-0.0625f, -0.3125f, -0.0625f), new Vector3(0.0625f, -0.0625f, 0.0625f), SbBlockArt.Planks, SbBlockArt.Planks, SbBlockArt.Planks));
            l.Add(new Box(new Vector3(-0.0625f, -0.0625f, z0), new Vector3(0.0625f, 0.125f, z0 + 0.125f), SbBlockArt.Planks, SbBlockArt.Planks, SbBlockArt.Planks));
          }
          break;
        case SbShape.Button:
          l.Add(new Box(new Vector3(-0.1875f, -0.5f, -0.125f), new Vector3(0.1875f, state ? -0.4375f : -0.375f, 0.125f), t, s, b));
          break;
        case SbShape.Plate:
          l.Add(new Box(new Vector3(-0.4375f, -0.5f, -0.4375f), new Vector3(0.4375f, state ? -0.46875f : -0.4375f, 0.4375f), t, s, b));
          break;
        case SbShape.Dust:
          l.Add(new Box(new Vector3(-0.5f, -0.5f, -0.5f), new Vector3(0.5f, -0.47f, 0.5f), t, s, b));
          break;
        case SbShape.Repeater:
        case SbShape.Comparator:
          {
            // a smooth stone slab 2/16 high with redstone torches (lit when on)
            l.Add(new Box(new Vector3(-0.5f, -0.5f, -0.5f), new Vector3(0.5f, -0.375f, 0.5f), t, s, b));
            int torch = state ? SbBlockArt.Redstone : SbBlockArt.T("redstone_lamp");
            float[] zs = it.Shape == SbShape.Repeater ? new[] { -0.25f, 0.15f } : new[] { -0.25f, 0.3f, 0.3f };
            float[] xs = it.Shape == SbShape.Repeater ? new[] { 0f, 0f } : new[] { 0f, -0.2f, 0.2f };
            for (int i = 0; i < zs.Length; i++)
              l.Add(new Box(new Vector3(xs[i] - 0.06f, -0.375f, zs[i] - 0.06f), new Vector3(xs[i] + 0.06f, -0.15f, zs[i] + 0.06f), torch, torch, torch));
            break;
          }
        case SbShape.Torch:
          {
            // a redstone torch: a stick with a glowing red tip (dark when off)
            int tip = state ? SbBlockArt.Redstone : SbBlockArt.T("redstone_lamp");
            l.Add(new Box(new Vector3(-0.0625f, -0.5f, -0.0625f), new Vector3(0.0625f, 0.0625f, 0.0625f), SbBlockArt.Planks, SbBlockArt.Planks, SbBlockArt.Planks));
            l.Add(new Box(new Vector3(-0.09375f, 0.0625f, -0.09375f), new Vector3(0.09375f, 0.25f, 0.09375f), tip, tip, tip));
            break;
          }
        case SbShape.Rail:
          l.Add(new Box(new Vector3(-0.5f, -0.5f, -0.5f), new Vector3(0.5f, -0.4375f, 0.5f), t, s, b));
          break;
        case SbShape.Skull:
          l.Add(new Box(new Vector3(-0.25f, -0.5f, -0.25f), new Vector3(0.25f, 0f, 0.25f), t, s, b));
          // the dark eye holes and mouth on the front
          l.Add(new Box(new Vector3(-0.19f, -0.25f, -0.26f), new Vector3(-0.06f, -0.16f, -0.25f), SbBlockArt.Obsidian, SbBlockArt.Obsidian, SbBlockArt.Obsidian));
          l.Add(new Box(new Vector3(0.06f, -0.25f, -0.26f), new Vector3(0.19f, -0.16f, -0.25f), SbBlockArt.Obsidian, SbBlockArt.Obsidian, SbBlockArt.Obsidian));
          break;
        default:
          l.Add(new Box(new Vector3(-0.5f, -0.5f, -0.5f), new Vector3(0.5f, 0.5f, 0.5f), t, s, b));
          break;
      }
      return l;
    }

    internal static Mesh MeshOf(SbItem it, bool state)
    {
      string key = it.Id + (state ? "#1" : "#0");
      Mesh m;
      if (meshes.TryGetValue(key, out m) && m != null) return m;
      var v = new List<Vector3>(); var n = new List<Vector3>(); var uv = new List<Vector2>(); var tri = new List<int>();
      foreach (Box bx in Boxes(it, state)) AddBox(bx, v, n, uv, tri);
      m = new Mesh { name = "Unknowns SandBox " + it.Name + (state ? " on" : ""), hideFlags = HideFlags.DontSave };
      m.SetVertices(v); m.SetNormals(n); m.SetUVs(0, uv); m.SetTriangles(tri, 0);
      m.RecalculateBounds();
      meshes[key] = m;
      return m;
    }

    internal static void ResetMeshes()
    {
      foreach (Mesh m in meshes.Values) if (m != null) UnityEngine.Object.Destroy(m);
      meshes.Clear();
    }

    // one box; each face shows the part of the tile it covers (like Minecraft's UVs)
    private static void AddBox(Box bx, List<Vector3> v, List<Vector3> n, List<Vector2> uv, List<int> tri)
    {
      Vector3 lo = bx.Lo, hi = bx.Hi;
      void Face(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 nn, int tile, float u0, float v0, float u1, float v1)
      {
        Rect r = SbBlockArt.TileRect(tile);
        int i = v.Count;
        v.Add(a); v.Add(b); v.Add(c); v.Add(d);
        for (int k = 0; k < 4; k++) n.Add(nn);
        uv.Add(new Vector2(r.xMin + r.width * u0, r.yMin + r.height * v0));
        uv.Add(new Vector2(r.xMin + r.width * u1, r.yMin + r.height * v0));
        uv.Add(new Vector2(r.xMin + r.width * u1, r.yMin + r.height * v1));
        uv.Add(new Vector2(r.xMin + r.width * u0, r.yMin + r.height * v1));
        if (Vector3.Dot(Vector3.Cross(b - a, c - a), nn) >= 0f) { tri.Add(i); tri.Add(i + 1); tri.Add(i + 2); tri.Add(i); tri.Add(i + 2); tri.Add(i + 3); }
        else { tri.Add(i); tri.Add(i + 2); tri.Add(i + 1); tri.Add(i); tri.Add(i + 3); tri.Add(i + 2); }
      }
      float X(float x) { return Mathf.Clamp01(x + 0.5f); }
      // a door's upper half shows the top of its texture: heights above 0.5 wrap round
      float Y(float y) { return Mathf.Clamp01(y > 0.5f + 1e-4f ? y - 0.5f : y + 0.5f); }
      float y0 = Y(lo.y), y1 = hi.y > 0.5f + 1e-4f ? Y(hi.y) : Mathf.Clamp01(hi.y + 0.5f);
      if (y1 <= y0) y1 = Mathf.Min(1f, y0 + (hi.y - lo.y));
      Face(new Vector3(hi.x, lo.y, hi.z), new Vector3(lo.x, lo.y, hi.z), new Vector3(lo.x, hi.y, hi.z), new Vector3(hi.x, hi.y, hi.z), Vector3.forward, bx.Side, 1f - X(hi.x), y0, 1f - X(lo.x), y1);
      Face(new Vector3(lo.x, lo.y, lo.z), new Vector3(hi.x, lo.y, lo.z), new Vector3(hi.x, hi.y, lo.z), new Vector3(lo.x, hi.y, lo.z), Vector3.back, bx.Side, X(lo.x), y0, X(hi.x), y1);
      Face(new Vector3(hi.x, lo.y, lo.z), new Vector3(hi.x, lo.y, hi.z), new Vector3(hi.x, hi.y, hi.z), new Vector3(hi.x, hi.y, lo.z), Vector3.right, bx.Side, X(lo.z), y0, X(hi.z), y1);
      Face(new Vector3(lo.x, lo.y, hi.z), new Vector3(lo.x, lo.y, lo.z), new Vector3(lo.x, hi.y, lo.z), new Vector3(lo.x, hi.y, hi.z), Vector3.left, bx.Side, 1f - X(hi.z), y0, 1f - X(lo.z), y1);
      Face(new Vector3(lo.x, hi.y, lo.z), new Vector3(hi.x, hi.y, lo.z), new Vector3(hi.x, hi.y, hi.z), new Vector3(lo.x, hi.y, hi.z), Vector3.up, bx.Top, X(lo.x), X(lo.z), X(hi.x), X(hi.z));
      Face(new Vector3(lo.x, lo.y, hi.z), new Vector3(hi.x, lo.y, hi.z), new Vector3(hi.x, lo.y, lo.z), new Vector3(lo.x, lo.y, lo.z), Vector3.down, bx.Bottom, X(lo.x), X(lo.z), X(hi.x), X(hi.z));
    }

    // ---------------------------------------------------------------- catalog
    internal static void AddToCatalog(Action<SbItem> add)
    {
      Func<string, int> T = SbBlockArt.T;
      var materials = new[]
      {
        new { Id = "oak", Name = "Oak", Tile = SbBlockArt.Planks, Wood = true },
        new { Id = "spruce", Name = "Spruce", Tile = T("spruce_planks"), Wood = true },
        new { Id = "birch", Name = "Birch", Tile = T("birch_planks"), Wood = true },
        new { Id = "stone", Name = "Stone", Tile = SbBlockArt.Stone, Wood = false },
        new { Id = "cobble", Name = "Cobblestone", Tile = SbBlockArt.Cobble, Wood = false },
        new { Id = "stonebrick", Name = "Stone Brick", Tile = SbBlockArt.StoneBricks, Wood = false },
        new { Id = "brick", Name = "Brick", Tile = SbBlockArt.Bricks, Wood = false },
        new { Id = "smoothstone", Name = "Smooth Stone", Tile = T("smooth_stone"), Wood = false },
        new { Id = "sandstone", Name = "Sandstone", Tile = T("sandstone"), Wood = false },
      };
      foreach (var m in materials)
      {
        string sound = m.Wood ? "wood" : "stone";
        add(new SbItem { Id = "mc_" + m.Id + "_slab", Name = m.Name + " Slab", Desc = "Half a block.", Tab = SbTab.Minecraft, Section = "SHAPES", Kind = SbKind.Block, Shape = SbShape.Slab, Top = m.Tile, Side = m.Tile, Bottom = m.Tile, Front = m.Tile, Sound = sound });
        add(new SbItem { Id = "mc_" + m.Id + "_stairs", Name = m.Name + " Stairs", Desc = "Steps up away from you.", Tab = SbTab.Minecraft, Section = "SHAPES", Kind = SbKind.Block, Shape = SbShape.Stairs, Top = m.Tile, Side = m.Tile, Bottom = m.Tile, Front = m.Tile, Sound = sound });
        if (m.Wood)
          add(new SbItem { Id = "mc_" + m.Id + "_fence", Name = m.Name + " Fence", Desc = "A fence post with rails.", Tab = SbTab.Minecraft, Section = "SHAPES", Kind = SbKind.Block, Shape = SbShape.Fence, Top = m.Tile, Side = m.Tile, Bottom = m.Tile, Front = m.Tile, Sound = sound });
        else
          add(new SbItem { Id = "mc_" + m.Id + "_wall", Name = m.Name + " Wall", Desc = "A wall with a post.", Tab = SbTab.Minecraft, Section = "SHAPES", Kind = SbKind.Block, Shape = SbShape.Wall, Top = m.Tile, Side = m.Tile, Bottom = m.Tile, Front = m.Tile, Sound = sound });
      }
      add(new SbItem { Id = "mc_oak_door", Name = "Oak Door", Desc = "Touch it to open or close it, or power it with redstone.", Tab = SbTab.Minecraft, Section = "REDSTONE", Kind = SbKind.Block, Shape = SbShape.Door, Top = T("oak_door_top"), Side = SbBlockArt.Planks, Bottom = T("oak_door_bottom"), Front = SbBlockArt.Planks, Sound = "wood" });
      add(new SbItem { Id = "mc_iron_door", Name = "Iron Door", Desc = "Opens only with redstone (a lever, button or pressure plate next to it).", Tab = SbTab.Minecraft, Section = "REDSTONE", Kind = SbKind.Block, Shape = SbShape.Door, Top = T("iron_door_top"), Side = SbBlockArt.Iron, Bottom = T("iron_door_bottom"), Front = SbBlockArt.Iron, Sound = "metal" });
      foreach (string col in new[] { "red", "blue", "lime", "yellow", "purple", "white", "black", "pink" })
        add(new SbItem { Id = "mc_bed_" + col, Name = char.ToUpperInvariant(col[0]) + col.Substring(1) + " Bed", Desc = "A Minecraft bed, two blocks long. Touch it to sleep: morning comes.", Tab = SbTab.Minecraft, Section = "SHAPES", Kind = SbKind.Block, Shape = SbShape.Bed, Top = T(col + "_wool"), Side = T(col + "_wool"), Bottom = T("white_wool"), Front = T(col + "_wool"), Sound = "wool" });
      // wiring (SandBoxRedstone2.cs)
      add(new SbItem { Id = "mc_redstone_dust", Name = "Redstone Dust", Desc = "Carries power along a line (15 blocks), up and down one step too.", Tab = SbTab.Minecraft, Section = "REDSTONE", Kind = SbKind.Block, Shape = SbShape.Dust, Top = SbBlockArt.Redstone, Side = SbBlockArt.Redstone, Bottom = SbBlockArt.Redstone, Front = SbBlockArt.Redstone, Sound = "stone" });
      add(new SbItem { Id = "mc_repeater", Name = "Redstone Repeater", Desc = "Power in at the back, out at the front (away from you when placed), full strength. Touch it to change the delay (1 - 4).", Tab = SbTab.Minecraft, Section = "REDSTONE", Kind = SbKind.Block, Shape = SbShape.Repeater, Top = SbBlockArt.T("smooth_stone"), Side = SbBlockArt.T("smooth_stone"), Bottom = SbBlockArt.T("smooth_stone"), Front = SbBlockArt.T("smooth_stone"), Sound = "stone" });
      add(new SbItem { Id = "mc_comparator", Name = "Redstone Comparator", Desc = "Power in at the back goes out at the front. Touch it for subtract mode: off while a side is powered.", Tab = SbTab.Minecraft, Section = "REDSTONE", Kind = SbKind.Block, Shape = SbShape.Comparator, Top = SbBlockArt.T("smooth_stone"), Side = SbBlockArt.T("smooth_stone"), Bottom = SbBlockArt.T("smooth_stone"), Front = SbBlockArt.T("smooth_stone"), Sound = "stone" });
      add(new SbItem { Id = "mc_lever", Name = "Lever", Desc = "Touch it to switch it on or off. Powers the blocks around it.", Tab = SbTab.Minecraft, Section = "REDSTONE", Kind = SbKind.Block, Shape = SbShape.Lever, Top = SbBlockArt.Cobble, Side = SbBlockArt.Cobble, Bottom = SbBlockArt.Cobble, Front = SbBlockArt.Cobble, Sound = "stone" });
      add(new SbItem { Id = "mc_redstone_torch", Name = "Redstone Torch", Desc = "Always on, like a lever left on. Turns off while the block under it is powered (a NOT gate).", Tab = SbTab.Minecraft, Section = "REDSTONE", Kind = SbKind.Block, Shape = SbShape.Torch, Top = SbBlockArt.Planks, Side = SbBlockArt.Planks, Bottom = SbBlockArt.Planks, Front = SbBlockArt.Planks, Sound = "wood" });
      add(new SbItem { Id = "mc_button", Name = "Stone Button", Desc = "Touch it: on for a second.", Tab = SbTab.Minecraft, Section = "REDSTONE", Kind = SbKind.Block, Shape = SbShape.Button, Top = SbBlockArt.Stone, Side = SbBlockArt.Stone, Bottom = SbBlockArt.Stone, Front = SbBlockArt.Stone, Sound = "stone" });
      add(new SbItem { Id = "mc_oak_plate", Name = "Oak Pressure Plate", Desc = "On while a player or a thing stands on it.", Tab = SbTab.Minecraft, Section = "REDSTONE", Kind = SbKind.Block, Shape = SbShape.Plate, Top = SbBlockArt.Planks, Side = SbBlockArt.Planks, Bottom = SbBlockArt.Planks, Front = SbBlockArt.Planks, Sound = "wood" });
      add(new SbItem { Id = "mc_stone_plate", Name = "Stone Pressure Plate", Desc = "On while a player or a thing stands on it.", Tab = SbTab.Minecraft, Section = "REDSTONE", Kind = SbKind.Block, Shape = SbShape.Plate, Top = SbBlockArt.Stone, Side = SbBlockArt.Stone, Bottom = SbBlockArt.Stone, Front = SbBlockArt.Stone, Sound = "stone" });
    }
  }

  // ==================================================================== the world side
  internal sealed partial class SandBoxWorld
  {
    private float redstoneAt;
    private readonly HashSet<long> powered = new HashSet<long>();
    private SbObject touchL, touchR;
    private float touchAtL, touchAtR;
    private static readonly Dictionary<int, AudioClip> noteClips = new Dictionary<int, AudioClip>();

    internal static bool IsRedstoneSource(SbItem it) { return it.Shape == SbShape.Lever || it.Shape == SbShape.Button || it.Shape == SbShape.Plate; }

    private static bool IsRedstoneUser(SbItem it)
    {
      return it.Shape == SbShape.Door || it.Id == "mc_redstone_lamp_off" || it.Id == "mc_note_block" || it.Id == "mc_piston" || it.Id == "mc_sticky_piston" ||
             it.Id == "mc_dispenser" || it.BlockFx == SbBlockFx.Tnt;
    }

    // the shape's colliders (cell units)
    internal static void AddShapeColliders(SbObject o, GameObject cg, bool state)
    {
      foreach (Collider c in cg.GetComponents<Collider>()) UnityEngine.Object.DestroyImmediate(c);
      if (o.Item.Shape == SbShape.Plate || o.Item.Shape == SbShape.Button || o.Item.Shape == SbShape.Rail || o.Item.Shape == SbShape.Dust || o.Item.Shape == SbShape.Torch) return;   // walked over / through, like Minecraft
      if (o.Item.Shape == SbShape.Bed && o.Item.Mat != null)
      {
        // the textured bed is two blocks long (this cell and the next along +Z)
        var bed = cg.AddComponent<BoxCollider>();
        bed.center = new Vector3(0f, -0.21875f, 0.5f);
        bed.size = new Vector3(1f, 0.5625f, 2f);
        return;
      }
      foreach (SbShapes.Box b in SbShapes.Boxes(o.Item, state))
      {
        var bc = cg.AddComponent<BoxCollider>();
        bc.center = (b.Lo + b.Hi) * 0.5f;
        bc.size = Vector3.Max(b.Hi - b.Lo, Vector3.one * 0.02f);
      }
    }

    // a lever / door / plate changed: its look and (doors) its colliders
    private void SetShapeState(SbObject o, bool state)
    {
      if (o.ShapeState == state || o.Go == null) return;
      o.ShapeState = state;
      var mf = o.Go.GetComponent<MeshFilter>();
      if (mf != null) mf.sharedMesh = SbShapes.MeshOf(o.Item, state);
      if (o.Item.Shape == SbShape.Door && o.ColGo != null)
      {
        foreach (Collider c in o.ColGo.GetComponents<Collider>()) { o.Cols.Remove(c); myColliders.Remove(c); }
        AddShapeColliders(o, o.ColGo, state);
        foreach (Collider c in o.ColGo.GetComponents<Collider>()) { o.Cols.Add(c); myColliders.Add(c); }
        ApplySolid(o);
        SbFx.PlayMc(o.Item.Id == "mc_iron_door" ? "metal" : "wood", SbSound.Place, o.Center, 0.8f, state ? 1.1f : 0.9f);
      }
    }

    // ================================================================ every frame
    private void UpdateRedstone(float dt)
    {
      UpdateRedstoneTouches();
      if (Time.time < redstoneAt) return;
      redstoneAt = Time.time + 0.1f;
      float now = Time.time;

      // the sources and what they power
      powered.Clear();
      foreach (SbObject o in List)
      {
        if (!o.IsCell || o.T == null) continue;
        // a redstone block always powers what is next to it
        if (o.Item.Id == "mc_redstone") { PowerNeighbours(o.SizeIdx, o.Cell); continue; }
        // a redstone torch (its state from the last step): the four sides, and up through the block above
        if (o.Item.Shape == SbShape.Torch)
        {
          SetShapeState(o, o.RedOn);
          if (!o.RedOn) continue;
          foreach (Vector3Int d in Dirs6) if (d.y >= 0) powered.Add(Pack(o.SizeIdx, o.Cell.x + d.x, o.Cell.y + d.y, o.Cell.z + d.z));
          PowerNeighbours(o.SizeIdx, o.Cell + Vector3Int.up);
          continue;
        }
        if (!IsRedstoneSource(o.Item)) continue;
        bool on;
        if (o.Item.Shape == SbShape.Lever) on = o.RedOn;
        else if (o.Item.Shape == SbShape.Button) on = now < o.OnUntil;
        else on = PlatePressed(o);
        SetShapeState(o, on);
        if (!on) continue;
        // everything around it, and around the block it sits on (strong power)
        PowerAround(o.SizeIdx, o.Cell, 1);
        PowerAround(o.SizeIdx, o.Cell + Vector3Int.down, 1);
      }
      // what repeaters, comparators and observers put out, then along the redstone dust
      PowerFromDiodesAndDust();

      foreach (SbObject o in new List<SbObject>(List))
      {
        if (!o.IsCell || o.T == null || !IsRedstoneUser(o.Item)) continue;
        bool p = powered.Contains(Pack(o.SizeIdx, o.Cell.x, o.Cell.y, o.Cell.z));
        // a door's top half counts too
        if (!p && o.Item.Shape == SbShape.Door) p = powered.Contains(Pack(o.SizeIdx, o.Cell.x, o.Cell.y + 1, o.Cell.z));
        bool rising = p && !o.Powered, falling = !p && o.Powered;
        o.Powered = p;
        if (o.Item.Shape == SbShape.Door) { SetShapeState(o, o.RedOn ^ p); continue; }
        if (!rising && !falling) continue;
        string id = o.Item.Id;
        if (id == "mc_redstone_lamp_off") LampLit(o, p);
        else if (id == "mc_note_block") { if (rising) PlayNote(o); }
        else if (id == "mc_piston" || id == "mc_sticky_piston") PistonMove(o, rising);
        else if (id == "mc_dispenser") { if (rising && o.Mine) Dispense(o); }
        else if (rising && o.Item.BlockFx == SbBlockFx.Tnt && (o.Mine || o.Generated) && !o.Primed) Prime(o, 4f, true);
      }
      UpdateDiodes(now);
      // torches go off while the block they stand on is powered (seen next step)
      foreach (SbObject o in List)
        if (o.IsCell && o.T != null && o.Item.Shape == SbShape.Torch)
        {
          bool on = !PoweredAt(o.SizeIdx, o.Cell + Vector3Int.down);
          o.RedOn = on;
        }
    }

    private void PowerAround(int size, Vector3Int c, int r)
    {
      for (int x = -r; x <= r; x++)
        for (int y = -r; y <= r; y++)
          for (int z = -r; z <= r; z++)
            powered.Add(Pack(size, c.x + x, c.y + y, c.z + z));
    }

    // a player (anyone), a mob or a loose thing on the plate
    private bool PlatePressed(SbObject plate)
    {
      float s = plate.BlockSize;
      Vector3 c = plate.T.position + Vector3.down * (s * 0.45f);
      bool On(Vector3 p) { Vector3 d = p - c; return Mathf.Abs(d.x) < s * 0.48f && Mathf.Abs(d.z) < s * 0.48f && d.y > -0.05f && d.y < 0.6f; }
      try
      {
        GTPlayer pl = GTPlayer.Instance;
        if (pl != null && pl.bodyCollider != null)
        {
          Vector3 feet = pl.bodyCollider.transform.position + Vector3.down * 0.45f * pl.scale;
          if (On(feet)) return true;
        }
      }
      catch { }
      foreach (VRRig r in Rigs())
      {
        if (r == null || r.isOfflineVRRig) continue;
        if (On(r.transform.position + Vector3.down * 0.45f * Mathf.Max(0.2f, r.scaleFactor))) return true;
      }
      foreach (SbObject o in List)
        if (!o.IsCell && o.T != null && o != plate && On(o.T.position)) return true;
      return false;
    }

    // ---------------------------------------------------------------- touching levers, buttons, doors, note blocks
    private void UpdateRedstoneTouches()
    {
      for (int side = 0; side < 2; side++)
      {
        bool right = side == 1;
        Transform hand = HandProvider != null ? HandProvider(right) : null;
        if (hand == null || (right ? heldR : heldL) != null) continue;
        SbObject touched = null;
        foreach (SbObject o in List)
        {
          if (!o.IsCell || o.T == null) continue;
          SbItem it = o.Item;
          bool usable = it.Shape == SbShape.Lever || it.Shape == SbShape.Button || it.Id == "mc_oak_door" || it.Id == "mc_note_block" || it.Shape == SbShape.Repeater || it.Shape == SbShape.Comparator || it.Shape == SbShape.Bed;
          if (!usable) continue;
          Vector3 lp = o.T.InverseTransformPoint(hand.position);
          Bounds b = it.Shape == SbShape.Bed && it.Mesh != null ? it.Mesh.bounds : SbShapes.MeshOf(it, o.ShapeState).bounds;
          if (it.Shape == SbShape.Cube) b = new Bounds(Vector3.zero, Vector3.one);
          b.Expand(0.12f / Mathf.Max(0.05f, o.BlockSize));
          if (b.Contains(lp)) { touched = o; break; }
        }
        SbObject last = right ? touchR : touchL;
        float lastAt = right ? touchAtR : touchAtL;
        if (touched != null && touched != last && Time.time - lastAt > 0.35f)
        {
          UseRedstoneBlock(touched);
          Haptic?.Invoke(right, 0.3f, 0.04f);
          if (right) touchAtR = Time.time; else touchAtL = Time.time;
        }
        if (right) touchR = touched; else touchL = touched;
      }
    }

    private void UseRedstoneBlock(SbObject o)
    {
      SbItem it = o.Item;
      if (it.Shape == SbShape.Lever)
      {
        o.RedOn = !o.RedOn;
        SbFx.Play(SbSound.UiClick, o.Center, 0.7f, o.RedOn ? 0.9f : 0.7f);
        SbNet.SendToggle(o.Owner, o.Id, o.RedOn);
        redstoneAt = 0f;
      }
      else if (it.Shape == SbShape.Button)
      {
        o.OnUntil = Time.time + 1f;
        SbFx.Play(SbSound.UiClick, o.Center, 0.6f, 1.2f);
        SbNet.SendToggle(o.Owner, o.Id, true);
        redstoneAt = 0f;
      }
      else if (it.Shape == SbShape.Door)
      {
        o.RedOn = !o.RedOn;
        SbNet.SendToggle(o.Owner, o.Id, o.RedOn);
        redstoneAt = 0f;
      }
      else if (it.Shape == SbShape.Repeater || it.Shape == SbShape.Comparator)
      {
        // a repeater's delay (1 - 4), a comparator's mode (compare / subtract)
        o.Note = it.Shape == SbShape.Repeater ? o.Note % 4 + 1 : (o.Note == 0 ? 1 : 0);
        SbNet.SendToggle(o.Owner, o.Id, true, o.Note);
        SbFx.Play(SbSound.UiClick, o.Center, 0.5f, it.Shape == SbShape.Repeater ? 0.8f + o.Note * 0.1f : 1.2f);
      }
      else if (it.Shape == SbShape.Bed)
      {
        // sleep: the night is skipped (for you), morning comes
        SbFx.PlayMc("wool", SbSound.UiClick, o.Center, 0.6f, 0.8f, 8f);
        try
        {
          if (BetterDayNightManager.instance != null) BetterDayNightManager.instance.SetTimeOfDay(3, true);
          Toast?.Invoke("Bed", "You slept. Good morning!");
        }
        catch { }
      }
      else if (it.Id == "mc_note_block")
      {
        o.Note = (o.Note + 1) % 25;
        SbNet.SendToggle(o.Owner, o.Id, true, o.Note);
        PlayNote(o);
      }
    }

    // someone flipped a lever / door, pressed a button or tuned a note block (owner 0 = yours)
    internal void NetToggle(int sender, int owner, int id, bool on, int note)
    {
      int localOwner = owner == LocalActorNumber ? 0 : owner;
      SbObject o;
      if (!objects.TryGetValue(((long)localOwner << 32) | (uint)id, out o) || o == null) return;
      if (o.Item.Shape == SbShape.Button) { if (on) o.OnUntil = Time.time + 1f; }
      else if (o.Item.Id == "mc_note_block") { o.Note = Mathf.Clamp(note, 0, 24); PlayNote(o); }
      else if (o.Item.Shape == SbShape.Repeater || o.Item.Shape == SbShape.Comparator) o.Note = note;
      else o.RedOn = on;
      redstoneAt = 0f;
    }

    private static int LocalActorNumber
    {
      get { try { return Photon.Pun.PhotonNetwork.LocalPlayer != null ? Photon.Pun.PhotonNetwork.LocalPlayer.ActorNumber : -1; } catch { return -1; } }
    }

    // ---------------------------------------------------------------- what power does
    private void LampLit(SbObject o, bool lit)
    {
      if (o.Go == null) return;
      SbItem on = SbCatalog.Get("mc_redstone_lamp");
      var mf = o.Go.GetComponent<MeshFilter>();
      var mr = o.Go.GetComponent<MeshRenderer>();
      if (mf != null) mf.sharedMesh = lit && on != null ? on.Mesh : o.Item.Mesh;
      if (mr != null) mr.sharedMaterials = SbMats.For(lit && on != null ? on : o.Item);
      if (lit) { if (o.Light == null) AddLampLight(o); }
      else if (o.Light != null) { Destroy(o.Light.gameObject); o.Light = null; lightCount = Mathf.Max(0, lightCount - 1); }
      SbFx.Play(SbSound.UiClick, o.Center, 0.25f, lit ? 1.4f : 1f);
    }

    private void AddLampLight(SbObject o)
    {
      if (lightCount >= 24) return;
      var lg = new GameObject("lamp light");
      lg.transform.SetParent(o.T, false);
      var l = lg.AddComponent<Light>();
      l.type = LightType.Point;
      l.color = new Color(1f, 0.75f, 0.45f);
      l.range = 6f * o.BlockSize;
      l.intensity = 1.6f;
      o.Light = l;
      lightCount++;
    }

    // a harp note: Minecraft's two octaves (F#3 up to F#5), one per touch
    private void PlayNote(SbObject o)
    {
      int note = Mathf.Clamp(o.Note, 0, 24);
      AudioClip clip;
      if (!noteClips.TryGetValue(note, out clip) || clip == null)
      {
        const int rate = 44100;
        float freq = 185f * Mathf.Pow(2f, note / 12f);
        int len = (int)(rate * 0.9f);
        var data = new float[len];
        for (int i = 0; i < len; i++)
        {
          float t = i / (float)rate;
          float env = Mathf.Exp(-t * 5f) * Mathf.Clamp01(t * 400f);
          data[i] = (Mathf.Sin(2f * Mathf.PI * freq * t) * 0.7f + Mathf.Sin(4f * Mathf.PI * freq * t) * 0.2f + Mathf.Sin(6f * Mathf.PI * freq * t) * 0.08f) * env * 0.6f;
        }
        int pos = 0;
        clip = AudioClip.Create("Unknowns SandBox note " + note, len, 1, rate, false, delegate(float[] buf)
        {
          int count = Math.Max(0, Math.Min(data.Length - pos, buf.Length));
          if (count > 0) Array.Copy(data, pos, buf, 0, count);
          if (count < buf.Length) Array.Clear(buf, count, buf.Length - count);
          pos += count;
        }, delegate(int newPos) { pos = Mathf.Clamp(newPos, 0, data.Length); });
        noteClips[note] = clip;
      }
      SbFx.PlayClip(clip, o.Center, 0.8f * SbFx.Volume, 1f, 24f);
      // the coloured note that floats up
      SbFx.Burst(o.Center + Vector3.up * o.BlockSize * 0.6f, 1, Color.HSVToRGB(note / 24f, 0.9f, 1f), Color.HSVToRGB(note / 24f, 0.9f, 1f), 0.4f, 0.6f, 0.08f, 0.1f, 0.6f, 0.8f, -0.05f, false, 0.01f, Vector3.up, 5f);
    }

    private static bool IsImmovable(SbObject b)
    {
      string id = b.Item.Id;
      return id == "mc_obsidian" || id == "mc_bedrock" || b.Item.BlockFx == SbBlockFx.Portal || b.Item.BlockFx == SbBlockFx.EndPortal || id.StartsWith("mc_endframe", StringComparison.Ordinal);
    }

    // moves one of your blocks to another cell (removed and placed again, so everyone sees it)
    private void MoveCell(SbObject b, Vector3Int to)
    {
      SbItem item = b.Item;
      int size = b.SizeIdx;
      Quaternion rot = b.T != null ? b.T.rotation : Quaternion.identity;
      bool mine = b.Mine;
      Remove(b, true, false);
      if (!mine) return;
      SbObject n = Create(0, nextId++, item, CellCenter(to, BlockSizes[size]), rot, 1f, true, size, to);
      if (n == null) return;
      undo.Add(n.Id);
      SbNet.QueueSpawn(n);
    }

    // a dispenser shoots out of its front
    private void Dispense(SbObject d)
    {
      SbItem ball = SbCatalog.Get("gun_snowball");
      if (ball == null || d.T == null) return;
      Vector3 dir = -d.T.forward;
      Vector3 from = d.Center + dir * d.BlockSize * 0.6f;
      int seed = UnityEngine.Random.Range(1, int.MaxValue);
      Shoot(ball, from, dir, seed, true, 1f);
      SbNet.SendShot(ball.Key, from, dir, seed, 1f);
    }
  }
}
