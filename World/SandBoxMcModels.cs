using System;
using System.Collections.Generic;
using UnityEngine;

namespace UnknownsSandbox
{
  // the Minecraft tab with Minecraft's own textures.
  //  - Pistons and observers: their own front, back and sides, the sides turned the way
  //    Minecraft turns them (a piston's sides point at its face).
  //  - Beds: two blocks long, with the real bed texture (entity/bed/<colour>); touch one
  //    to sleep (the night is skipped).
  //  - Items made from their item pictures (bow, fishing rod, potions with the liquid's
  //    colour, helmets and chestplates, elytra, firework rocket).
  //  - The shield, minecart and boat from their entity textures, and the armour you wear
  //    from Minecraft's armour textures.
  // Without Minecraft's textures everything keeps its plain-colour look.
  internal static class SbMcModels
  {
    private static readonly Dictionary<string, Material> mats = new Dictionary<string, Material>(StringComparer.Ordinal);

    internal static Material EntityMaterial(string[] names, int w, int h, Color tint, out Vector2 size)
    {
      size = new Vector2(w, h);
      Material m = SbMobDefs.TextureMaterial(names, out size, w, h);
      if (m == null) return null;
      if (tint == Color.white) return m;
      string key = m.name + "#" + ColorUtility.ToHtmlStringRGB(tint);
      Material t;
      if (!mats.TryGetValue(key, out t) || t == null)
      {
        t = new Material(m) { name = key, hideFlags = HideFlags.DontSave };
        if (t.HasProperty("_BaseColor")) t.SetColor("_BaseColor", tint);
        if (t.HasProperty("_Color")) t.SetColor("_Color", tint);
        mats[key] = t;
      }
      return t;
    }

    // ---------------------------------------------------------------- textured boxes
    // one box with Minecraft's box texture layout, turned and moved (pixels -> scale)
    private static void Box(List<CombineInstance> into, int u, int v, float w, float h, float d, Vector2 tex, Vector3 min, Quaternion rot, Vector3 at, float scale, bool mirror = false)
    {
      var p = new SbMobDefs.Part { Min = min, Size = new Vector3(w, h, d), U = u, V = v, Pivot = Vector3.zero, Mirror = mirror };
      Mesh m = SbMobDefs.PartMesh(p, tex);
      into.Add(new CombineInstance { mesh = m, transform = Matrix4x4.TRS(at * scale, rot, Vector3.one * scale) });
    }

    private static Mesh Combine(List<CombineInstance> parts, string name)
    {
      var mesh = new Mesh { name = name, hideFlags = HideFlags.DontSave };
      mesh.CombineMeshes(parts.ToArray(), true, true);
      foreach (CombineInstance c in parts) UnityEngine.Object.Destroy(c.mesh);
      mesh.RecalculateBounds();
      return mesh;
    }

    // ---------------------------------------------------------------- beds
    internal static string BedColour(SbItem it) { return it.Id.StartsWith("mc_bed_", StringComparison.Ordinal) ? it.Id.Substring("mc_bed_".Length) : "red"; }

    // the bed in cell units: the foot in its own cell, the head in the next one (+Z), like
    // Minecraft's (mattress 16 x 16 x 6 per half on 3-pixel legs)
    internal static Mesh BedMesh(SbItem it)
    {
      Vector2 tex;
      Material m = EntityMaterial(new[] { "entity/bed/" + BedColour(it) }, 64, 64, Color.white, out tex);
      if (m == null) return null;
      it.Mat = m;
      var parts = new List<CombineInstance>();
      const float s = 1f / 16f;
      Quaternion lie = Quaternion.Euler(-90f, 0f, 0f);
      // the box is defined standing (16 wide, 16 long, 6 deep) and laid down
      Box(parts, 0, 0, 16f, 16f, 6f, tex, new Vector3(-8f, -16f, -6f), lie, new Vector3(0f, 1f, 8f), s);   // head (next cell)
      Box(parts, 0, 22, 16f, 16f, 6f, tex, new Vector3(-8f, 0f, -6f), lie, new Vector3(0f, 1f, 8f), s);    // foot (this cell)
      // legs at the four corners
      int[] lu = { 50, 50, 50, 50 }, lv = { 0, 6, 12, 18 };
      Vector3[] at = { new Vector3(-8f, -8f, -8f), new Vector3(5f, -8f, -8f), new Vector3(-8f, -8f, 21f), new Vector3(5f, -8f, 21f) };
      for (int i = 0; i < 4; i++) Box(parts, lu[i], lv[i], 3f, 3f, 3f, tex, Vector3.zero, Quaternion.identity, at[i], s);
      return Combine(parts, "Unknowns Bed " + BedColour(it));
    }

    // ---------------------------------------------------------------- the shield, minecart and boat
    internal static Mesh ShieldMesh(SbItem it)
    {
      Vector2 tex;
      Material m = EntityMaterial(new[] { "entity/shield_base_nopattern", "entity/shield/base_nopattern" }, 64, 64, Color.white, out tex);
      if (m == null) return null;
      it.Mat = m;
      var parts = new List<CombineInstance>();
      const float s = 0.026f;
      // the board faces +Z (towards what it blocks), the handle behind it
      Box(parts, 0, 0, 12f, 22f, 1f, tex, new Vector3(-6f, -11f, 0f), Quaternion.identity, new Vector3(0f, 2f, 1.2f), s);
      Box(parts, 26, 0, 2f, 6f, 6f, tex, new Vector3(-1f, -3f, -5f), Quaternion.identity, new Vector3(0f, 0f, 1.2f), s);
      return Combine(parts, "Unknowns Shield");
    }

    internal static Mesh MinecartMesh(SbItem it)
    {
      Vector2 tex;
      Material m = EntityMaterial(new[] { "entity/minecart" }, 64, 32, Color.white, out tex);
      if (m == null) return null;
      it.Mat = m;
      var parts = new List<CombineInstance>();
      const float s = 0.044f;
      Quaternion lie = Quaternion.Euler(90f, 0f, 0f);
      Box(parts, 0, 10, 20f, 16f, 2f, tex, new Vector3(-10f, -8f, -1f), lie, new Vector3(0f, 3f, 0f), s);                       // the floor
      Box(parts, 0, 0, 16f, 8f, 2f, tex, new Vector3(-8f, 0f, -1f), Quaternion.Euler(0f, 90f, 0f), new Vector3(-9f, 3f, 0f), s);   // sides
      Box(parts, 0, 0, 16f, 8f, 2f, tex, new Vector3(-8f, 0f, -1f), Quaternion.Euler(0f, 90f, 0f), new Vector3(9f, 3f, 0f), s);
      Box(parts, 0, 0, 16f, 8f, 2f, tex, new Vector3(-8f, 0f, -1f), Quaternion.identity, new Vector3(0f, 3f, -9f), s);
      Box(parts, 0, 0, 16f, 8f, 2f, tex, new Vector3(-8f, 0f, -1f), Quaternion.identity, new Vector3(0f, 3f, 9f), s);
      return Combine(parts, "Unknowns Minecart");
    }

    internal static Mesh BoatMesh(SbItem it)
    {
      Vector2 tex;
      Material m = EntityMaterial(new[] { "entity/boat/oak" }, 128, 64, Color.white, out tex);
      if (m == null) return null;
      it.Mat = m;
      var parts = new List<CombineInstance>();
      const float s = 0.064f;
      Quaternion lie = Quaternion.Euler(90f, 90f, 0f);   // laid down, long side along Z (our boats go +Z)
      Box(parts, 0, 0, 28f, 16f, 3f, tex, new Vector3(-14f, -8f, -1.5f), lie, new Vector3(0f, 1.5f, 0f), s);
      Box(parts, 0, 19, 18f, 6f, 2f, tex, new Vector3(-9f, 0f, -1f), Quaternion.identity, new Vector3(0f, 3f, -14f), s);   // back
      Box(parts, 0, 27, 16f, 6f, 2f, tex, new Vector3(-8f, 0f, -1f), Quaternion.identity, new Vector3(0f, 3f, 14f), s);    // front
      Box(parts, 0, 35, 28f, 6f, 2f, tex, new Vector3(-14f, 0f, -1f), Quaternion.Euler(0f, 90f, 0f), new Vector3(-8f, 3f, 0f), s);
      Box(parts, 0, 43, 28f, 6f, 2f, tex, new Vector3(-14f, 0f, -1f), Quaternion.Euler(0f, 90f, 0f), new Vector3(8f, 3f, 0f), s);
      return Combine(parts, "Unknowns Boat");
    }

    // ---------------------------------------------------------------- worn armour
    private static readonly Dictionary<string, Color> leather = new Dictionary<string, Color> { { "leather", new Color(0.63f, 0.4f, 0.25f) } };

    internal static Mesh WornArmor(string id, int slot, out Material mat)
    {
      mat = null;
      string m = id.Contains("_netherite") ? "netherite" : id.Contains("_diamond") ? "diamond" : id.Contains("_gold") ? "gold" : id.Contains("_iron") ? "iron" : "leather";
      Vector2 tex;
      if (id == "mc_elytra")
      {
        mat = EntityMaterial(new[] { "entity/elytra", "entity/equipment/wings/elytra" }, 64, 32, Color.white, out tex);
        if (mat == null) return null;
        var w = new List<CombineInstance>();
        Box(w, 22, 0, 10f, 20f, 2f, tex, new Vector3(-10f, -20f, 0f), Quaternion.Euler(0f, 0f, -15f), new Vector3(0f, 2f, -3f), 0.022f);
        Box(w, 22, 0, 10f, 20f, 2f, tex, new Vector3(0f, -20f, 0f), Quaternion.Euler(0f, 0f, 15f), new Vector3(0f, 2f, -3f), 0.022f, true);
        return Combine(w, "Unknowns Elytra worn");
      }
      string file = m == "gold" ? "golden" : m;
      Color tint = m == "leather" ? leather["leather"] : Color.white;
      mat = EntityMaterial(new[] { "models/armor/" + file + "_layer_1", "entity/equipment/humanoid/" + file }, 64, 32, tint, out tex);
      if (mat == null) return null;
      var parts = new List<CombineInstance>();
      if (slot == 0) Box(parts, 0, 0, 8f, 8f, 8f, tex, new Vector3(-4f, -4f, -4f), Quaternion.identity, new Vector3(0f, 1.5f, 0f), 0.034f);   // the helmet round the head
      else
      {
        Box(parts, 16, 16, 8f, 12f, 4f, tex, new Vector3(-4f, -6f, -2f), Quaternion.identity, Vector3.zero, 0.04f);         // the chest
        Box(parts, 40, 16, 4f, 6f, 4f, tex, new Vector3(-2f, -3f, -2f), Quaternion.identity, new Vector3(-6.5f, 3.5f, 0f), 0.04f);  // shoulders
        Box(parts, 40, 16, 4f, 6f, 4f, tex, new Vector3(-2f, -3f, -2f), Quaternion.identity, new Vector3(6.5f, 3.5f, 0f), 0.04f, true);
      }
      return Combine(parts, "Unknowns armour " + id);
    }

    // ---------------------------------------------------------------- item pictures
    internal static Color32[] ItemPixels(string name) { return SbMinecraft.Frame16("textures/item/" + name + ".png"); }

    // a potion: the bottle with the liquid's colour in its overlay
    internal static Color32[] PotionPixels(Color liquid)
    {
      Color32[] bottle = ItemPixels("potion"), overlay = ItemPixels("potion_overlay");
      if (bottle == null) return null;
      var px = new Color32[256];
      for (int i = 0; i < 256; i++)
      {
        Color32 b = bottle[i];
        if (overlay != null && overlay[i].a >= 128)
        {
          Color o = overlay[i];
          px[i] = new Color(o.r * liquid.r, o.g * liquid.g, o.b * liquid.b, 1f);
        }
        else px[i] = b;
      }
      return px;
    }

    internal static Color32[] Tinted(Color32[] px, Color tint)
    {
      if (px == null) return null;
      var o = new Color32[px.Length];
      for (int i = 0; i < px.Length; i++) { Color c = px[i]; o[i] = new Color(c.r * tint.r, c.g * tint.g, c.b * tint.b, c.a); }
      return o;
    }

    // an item picture as a thin 3D item, held at (gripPx) and aimed along +Z with (tipPx)
    internal static bool PixelItem(SbMeshKit k, Color32[] px, float size, Vector2 gripPx, Vector2 tipPx)
    {
      if (px == null) return false;
      // the picture stands in the YZ plane: pixel x -> +Z, pixel up -> +Y; grip at the origin
      Quaternion rot = Quaternion.Euler(0f, -90f, 0f);
      Vector3 origin = -(rot * new Vector3(gripPx.x * size, (16f - gripPx.y) * size, 0f));
      k.Pixels(px, size, size, rot, origin);
      k.GripAt(Vector3.zero).MuzzleAt(origin + rot * new Vector3(tipPx.x * size, (16f - tipPx.y) * size, 0f));
      return true;
    }

    // ---------------------------------------------------------------- after the catalog is made
    internal static void Apply(Func<string, SbItem> get)
    {
      Func<string, int> T = SbBlockArt.T;
      // pistons: front = face, back = bottom, sides turned to the face
      foreach (var p in new[] { ("mc_piston", "piston_top"), ("mc_sticky_piston", "piston_top_sticky") })
      {
        SbItem it = get(p.Item1);
        if (it == null) continue;
        it.Front = T(p.Item2); it.Back = T("piston_bottom"); it.Side = T("piston_side"); it.Top = T("piston_side"); it.SidesUpFront = true;
      }
      SbItem obs = get("mc_observer");
      if (obs != null) { obs.Front = T("observer_front"); obs.Back = T("observer_back"); obs.Side = T("observer_side"); obs.Top = T("observer_top"); obs.SidesUpFront = false; }
      // repeaters and comparators: their own top
      SbItem rep = get("mc_repeater"); if (rep != null) rep.Top = T("repeater");
      SbItem cmp = get("mc_comparator"); if (cmp != null) cmp.Top = T("comparator");
      SbItem dust = get("mc_redstone_dust"); if (dust != null) { int d = T("redstone_dust_dot"); dust.Top = dust.Side = dust.Bottom = dust.Front = d; }

      // items from their pictures (kept as they are without Minecraft)
      Swap(get("mc_bow"), k => PixelItem(k, ItemPixels("bow"), 0.035f, new Vector2(7f, 9f), new Vector2(14f, 2f)));
      Swap(get("mc_fishing_rod"), k => PixelItem(k, ItemPixels("fishing_rod"), 0.045f, new Vector2(2f, 14f), new Vector2(14f, 2f)));
      Swap(get("mc_firework_rocket"), k => PixelItem(k, ItemPixels("firework_rocket"), 0.025f, new Vector2(4f, 12f), new Vector2(12f, 4f)));
      Swap(get("mc_elytra"), k => PixelItem(k, ItemPixels("elytra"), 0.03f, new Vector2(8f, 8f), new Vector2(8f, 2f)));
      foreach (var p in new[] { ("speed", "#7cafc6"), ("jump", "#22ff4c"), ("night", "#1f1fa1"), ("slowfall", "#f7f8e0") })
      {
        Color liquid = SbCatalog.C(p.Item2);
        Swap(get("mc_potion_" + p.Item1), k => PixelItem(k, PotionPixels(liquid), 0.022f, new Vector2(8f, 10f), new Vector2(8f, 2f)));
      }
      foreach (string m in SbMcItems.ArmorMats)
      {
        string file = m == "gold" ? "golden" : m;
        Color tint = m == "leather" ? leather["leather"] : Color.white;
        Swap(get("mc_helmet_" + m), k => PixelItem(k, Tinted(ItemPixels(file + "_helmet"), tint), 0.025f, new Vector2(8f, 10f), new Vector2(8f, 3f)));
        Swap(get("mc_chestplate_" + m), k => PixelItem(k, Tinted(ItemPixels(file + "_chestplate"), tint), 0.028f, new Vector2(8f, 10f), new Vector2(8f, 3f)));
      }
      // rails: thin, see-through
      foreach (string id in new[] { "mc_rail", "mc_powered_rail" }) { SbItem r = get(id); if (r != null) r.PixelItem = false; }
    }

    // the item's model from its picture when the picture is there; its own model otherwise
    private static void Swap(SbItem it, Func<SbMeshKit, bool> build)
    {
      if (it == null) return;
      Action<SbMeshKit> old = it.Build;
      it.PixelItem = true;
      it.Build = k => { if (!build(k) && old != null) old(k); };
    }

    // ---------------------------------------------------------------- redstone dust
    // Minecraft's dust: a dot in the middle, a line to each side it joins (a lone
    // join runs straight through), flat on the floor of its cell. Cached per join mask.
    private static readonly Mesh[] dustMeshes = new Mesh[16];

    internal static void ResetDust()
    {
      for (int i = 0; i < dustMeshes.Length; i++) { if (dustMeshes[i] != null) UnityEngine.Object.Destroy(dustMeshes[i]); dustMeshes[i] = null; }
    }

    internal static Mesh DustMesh(int mask)
    {
      mask &= 15;
      if (dustMeshes[mask] != null) return dustMeshes[mask];
      var v = new List<Vector3>(); var n = new List<Vector3>(); var uv = new List<Vector2>(); var t = new List<int>();
      const float y = -0.49f;
      // a quad on the floor: x0..x1, z0..z1 (cell units), the tile part u0..u1, v0..v1, turned k x 90 degrees
      void Quad(int tile, float x0, float x1, float z0, float z1, float u0, float u1, float v0, float v1, int k)
      {
        Rect r = SbBlockArt.TileRect(tile);
        Quaternion q = Quaternion.Euler(0f, k * 90f, 0f);
        int i = v.Count;
        Vector3[] p = { new Vector3(x0, y, z0), new Vector3(x0, y, z1), new Vector3(x1, y, z1), new Vector3(x1, y, z0) };
        Vector2[] w = { new Vector2(u0, v0), new Vector2(u0, v1), new Vector2(u1, v1), new Vector2(u1, v0) };
        for (int j = 0; j < 4; j++)
        {
          v.Add(q * p[j]); n.Add(Vector3.up);
          uv.Add(new Vector2(Mathf.Lerp(r.xMin, r.xMax, w[j].x), Mathf.Lerp(r.yMin, r.yMax, w[j].y)));
        }
        // facing up (the SandBox's winding: cross(b - a, c - a) points out)
        if (Vector3.Dot(Vector3.Cross(v[i + 1] - v[i], v[i + 2] - v[i]), Vector3.up) > 0f) { t.Add(i); t.Add(i + 1); t.Add(i + 2); t.Add(i); t.Add(i + 2); t.Add(i + 3); }
        else { t.Add(i); t.Add(i + 2); t.Add(i + 1); t.Add(i); t.Add(i + 3); t.Add(i + 2); }
      }
      int line = SbBlockArt.T("redstone_dust_line0"), dot = SbBlockArt.T("redstone_dust_dot");
      int sides = mask;
      // one join: the line runs on through the other side too
      if (sides == 1 || sides == 4) sides = 5;
      else if (sides == 2 || sides == 8) sides = 10;
      // the line texture runs along V (+Z); its half towards +Z, turned for each side
      for (int i = 0; i < 4; i++)
        if ((sides & (1 << i)) != 0) Quad(line, -0.5f, 0.5f, 0f, 0.5f, 0f, 1f, 0.5f, 1f, i);
      // the dot where lines meet or turn (and alone)
      if (sides != 5 && sides != 10) Quad(dot, -0.5f, 0.5f, -0.5f, 0.5f, 0f, 1f, 0f, 1f, 0);
      var m = new Mesh { name = "Unknowns redstone dust " + mask, hideFlags = HideFlags.DontSave };
      m.SetVertices(v); m.SetNormals(n); m.SetUVs(0, uv); m.SetTriangles(t, 0);
      m.RecalculateBounds();
      dustMeshes[mask] = m;
      return m;
    }

    // after the meshes are made: the ones with entity textures (they need the textures loaded)
    internal static void AfterMeshes(Func<string, SbItem> get)
    {
      SbItem dust = get("mc_redstone_dust");
      if (dust != null) { ResetDust(); dust.Mesh = DustMesh(0); }
      SbItem shield = get("mc_shield");
      if (shield != null) { Mesh m = ShieldMesh(shield); if (m != null) shield.Mesh = m; }
      SbItem cart = get("mc_minecart");
      if (cart != null && cart.Vehicle != null) { Mesh m = MinecartMesh(cart); if (m != null) cart.Vehicle.BodyMesh = cart.Mesh = m; }
      SbItem boat = get("mc_boat");
      if (boat != null && boat.Vehicle != null) { Mesh m = BoatMesh(boat); if (m != null) boat.Vehicle.BodyMesh = boat.Mesh = m; }
    }
  }

  internal static partial class SbBlockMesh
  {
    // a block with its own front (-Z) and back (+Z); sides and top / bottom either
    // upright (observer) or turned so their top edge points at the front (piston)
    internal static Mesh Directional(int front, int back, int side, int top, bool sidesUpFront)
    {
      var v = new List<Vector3>(); var n = new List<Vector3>(); var uv = new List<Vector2>(); var t = new List<int>();
      void Face(Vector3 N, Vector3 V, int tile)
      {
        Vector3 U = Vector3.Cross(N, V);
        Rect r = SbBlockArt.TileRect(tile);
        int i = v.Count;
        Vector3 c = N * 0.5f;
        Vector3[] corners = { c - U * 0.5f - V * 0.5f, c + U * 0.5f - V * 0.5f, c + U * 0.5f + V * 0.5f, c - U * 0.5f + V * 0.5f };
        Vector2[] uvs = { new Vector2(r.xMin, r.yMin), new Vector2(r.xMax, r.yMin), new Vector2(r.xMax, r.yMax), new Vector2(r.xMin, r.yMax) };
        for (int k = 0; k < 4; k++) { v.Add(corners[k]); n.Add(N); uv.Add(uvs[k]); }
        if (Vector3.Dot(Vector3.Cross(corners[1] - corners[0], corners[2] - corners[0]), N) >= 0f) { t.Add(i); t.Add(i + 1); t.Add(i + 2); t.Add(i); t.Add(i + 2); t.Add(i + 3); }
        else { t.Add(i); t.Add(i + 2); t.Add(i + 1); t.Add(i); t.Add(i + 3); t.Add(i + 2); }
      }
      Vector3 toFront = Vector3.back;
      Face(Vector3.back, Vector3.up, front);
      Face(Vector3.forward, Vector3.up, back);
      Face(Vector3.left, sidesUpFront ? toFront : Vector3.up, side);
      Face(Vector3.right, sidesUpFront ? toFront : Vector3.up, side);
      Face(Vector3.up, toFront, sidesUpFront ? side : top);
      Face(Vector3.down, toFront, sidesUpFront ? side : top);
      var m = new Mesh { name = "Unknowns SandBox directional block", hideFlags = HideFlags.DontSave };
      m.SetVertices(v); m.SetNormals(n); m.SetUVs(0, uv); m.SetTriangles(t, 0);
      m.RecalculateBounds();
      return m;
    }
  }

  // Minecraft's fireballs (the fire charge picture, always facing you) and its
  // explosion (the 16 explosion pictures played as puffs), with the explode sound.
  internal static class SbMcFx
  {
    internal static IEnumerable<string> TextureFiles()
    {
      yield return "textures/item/fire_charge.png";
      for (int i = 0; i < 16; i++) yield return "textures/particle/explosion_" + i + ".png";
      foreach (string c in new[] { "white", "orange", "magenta", "light_blue", "yellow", "lime", "pink", "gray", "light_gray", "cyan", "purple", "blue", "brown", "green", "red", "black" })
        yield return "textures/entity/bed/" + c + ".png";
      foreach (string s in new[] { "entity/shield_base_nopattern", "entity/shield/base_nopattern", "entity/minecart", "entity/boat/oak", "entity/elytra", "entity/equipment/wings/elytra",
                                   "item/bow", "item/fishing_rod", "item/firework_rocket", "item/elytra", "item/potion", "item/potion_overlay" })
        yield return "textures/" + s + ".png";
      foreach (string m in SbMcItems.ArmorMats)
      {
        string file = m == "gold" ? "golden" : m;
        yield return "textures/models/armor/" + file + "_layer_1.png";
        yield return "textures/entity/equipment/humanoid/" + file + ".png";
        yield return "textures/item/" + file + "_helmet.png";
        yield return "textures/item/" + file + "_chestplate.png";
      }
    }

    private static Material fireMat, blastMat;
    private static bool fireTried, blastTried;

    internal static void Reset()
    {
      if (fireMat != null) UnityEngine.Object.Destroy(fireMat);
      if (blastMat != null) UnityEngine.Object.Destroy(blastMat);
      fireMat = blastMat = null;
      fireTried = blastTried = false;
    }

    private static Material Sprite(Texture2D tex, string name)
    {
      var m = new Material(SbMats.TileParticle(0)) { name = name, hideFlags = HideFlags.DontSave };
      if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", tex);
      if (m.HasProperty("_MainTex")) m.SetTexture("_MainTex", tex);
      m.color = Color.white;
      return m;
    }

    // the fireball: a fire charge picture that keeps facing the camera (false = no texture)
    internal static bool Fireball(GameObject shot, float size)
    {
      if (!fireTried)
      {
        fireTried = true;
        Texture2D t = SbMinecraft.Image("textures/item/fire_charge.png");
        if (t != null) fireMat = Sprite(t, "Unknowns Fire Charge");
      }
      if (fireMat == null || shot == null) return false;
      var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
      q.name = "fire charge";
      Collider c = q.GetComponent<Collider>(); if (c != null) UnityEngine.Object.Destroy(c);
      q.transform.SetParent(shot.transform, false);
      q.transform.localScale = Vector3.one * size;
      Renderer r = q.GetComponent<Renderer>();
      r.sharedMaterial = fireMat;
      r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
      q.AddComponent<SbFaceCamera>();
      Renderer own = shot.GetComponent<MeshRenderer>();
      if (own != null) own.enabled = false;
      return true;
    }

    // the explosion pictures in a 4 x 4 sheet
    private static Material BlastMaterial()
    {
      if (blastTried) return blastMat;
      blastTried = true;
      var frames = new Color32[16][];
      for (int i = 0; i < 16; i++) { frames[i] = SbMinecraft.Frame16("textures/particle/explosion_" + i + ".png"); if (frames[i] == null) return null; }
      var sheet = new Texture2D(64, 64, TextureFormat.RGBA32, false) { name = "Unknowns Mc Explosion", filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.DontSave };
      var px = new Color32[64 * 64];
      for (int i = 0; i < 16; i++)
      {
        // the particle sheet runs left to right, top to bottom; textures start at the bottom
        int cx = i % 4, cy = 3 - i / 4;
        for (int y = 0; y < 16; y++)
          for (int x = 0; x < 16; x++)
            px[(cy * 16 + y) * 64 + cx * 16 + x] = frames[i][y * 16 + x];
      }
      sheet.SetPixels32(px);
      sheet.Apply(false, false);
      blastMat = Sprite(sheet, "Unknowns Mc Explosion");
      return blastMat;
    }

    // false = no Minecraft textures (the normal blast is used)
    internal static bool Explosion(Vector3 pos, float radius)
    {
      Material m = BlastMaterial();
      if (m == null) return false;
      try
      {
        var go = new GameObject("SB Mc Explosion");
        go.transform.SetParent(SbFx.Root, false);
        go.transform.position = pos;
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        float r = Mathf.Clamp(radius, 0.8f, 8f);
        var main = ps.main;
        main.duration = 0.35f;
        main.loop = false;
        main.playOnAwake = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.75f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0f, 0.3f);
        main.startSize = new ParticleSystem.MinMaxCurve(r * 0.55f, r * 0.95f);
        main.startColor = new ParticleSystem.MinMaxGradient(Color.white, new Color(0.85f, 0.85f, 0.85f));
        main.gravityModifier = 0f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 40;
        main.stopAction = ParticleSystemStopAction.Destroy;
        var em = ps.emission;
        em.enabled = true;
        em.rateOverTime = 0f;
        // Minecraft: a big puff in the middle and many around it, over a few ticks
        em.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)1), new ParticleSystem.Burst(0f, (short)8, (short)12, 3, 0.08f) });
        var sh = ps.shape;
        sh.enabled = true;
        sh.shapeType = ParticleSystemShapeType.Sphere;
        sh.radius = r * 0.55f;
        var tsa = ps.textureSheetAnimation;
        tsa.enabled = true;
        tsa.mode = ParticleSystemAnimationMode.Grid;
        tsa.numTilesX = 4;
        tsa.numTilesY = 4;
        tsa.animation = ParticleSystemAnimationType.WholeSheet;
        tsa.frameOverTime = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0f, 1f, 0.999f));
        tsa.cycleCount = 1;
        var pr = go.GetComponent<ParticleSystemRenderer>();
        pr.sharedMaterial = m;
        pr.renderMode = ParticleSystemRenderMode.Billboard;
        pr.alignment = ParticleSystemRenderSpace.View;
        pr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        pr.receiveShadows = false;
        ps.Play();
        return true;
      }
      catch { return false; }
    }
  }

  internal sealed class SbFaceCamera : MonoBehaviour
  {
    private void LateUpdate()
    {
      Camera c = Camera.main;
      if (c == null) return;
      transform.rotation = Quaternion.LookRotation(transform.position - c.transform.position, c.transform.up);
    }
  }
}
