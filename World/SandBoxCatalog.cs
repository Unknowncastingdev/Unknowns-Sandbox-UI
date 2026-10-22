using System;
using System.Collections.Generic;
using UnityEngine;

namespace UnknownsSandbox
{
  internal enum SbTab { Minecraft = 0, Objects = 1, Weapons = 2 }
  internal enum SbKind { Block, Fluid, Prop, Gun, Melee, Explosive, Tool, Mob }
  internal enum SbBlockFx { None, Falls, Tnt, Glow, Glass, Slime, Ice, Sponge, Magma, Portal, EndPortal }
  // things that stick onto another object and move it (SandBoxAttach.cs)
  internal enum SbAttach { None, Thruster, Wheel, Balloon, Fire }
  internal enum SbFluid { None, Water, Lava }
  internal enum SbCol { Box, Sphere, Mesh }
  internal enum SbPropFx { None, Bouncy, Trampoline, Balloon, Spin, Fire, Light }
  internal enum SbAmmo { Bullet, Pellet, Sniper, Minigun, Laser, Rocket, Grenade, Flame, Melon, Potato, Banana, Snowball, Paint, Bubble, Confetti, Duck, RickPortal, AperturePortal, Gravity, Teleport, Build, Arrow, Fireball, GhastBall, WitherSkull, DragonBall, Knife, Boomerang }
  internal enum SbBlast { Normal, Tnt, Nuke, MiniNuke, Cluster, BlackHole, Firework, Sticky, Landmine, Barrel, Smoke, Flash, Mc }

  internal sealed class SbItem
  {
    internal string Id;
    internal string Name;
    internal string Desc;
    internal SbTab Tab;
    internal string Section;
    internal SbKind Kind;

    // blocks
    internal int Top, Side, Bottom, Front;
    internal SbBlockFx BlockFx;
    internal SbFluid Fluid;

    // models
    internal Action<SbMeshKit> Build;
    internal SbCol Col = SbCol.Box;
    internal bool DefaultAnchored = true;
    internal float Mass = 1f;
    internal float Bounce = 0.1f;
    internal SbPropFx PropFx;
    internal bool Ragdoll;
    internal SbAttach Attach;              // placed on one of your objects, it attaches to it
    internal SbMobType Mob;                // a Minecraft mob (SandBoxMobs.cs)
    internal SbVehicleSpec Vehicle;        // a drivable car (SandBoxVehicles.cs)
    internal bool Pearl;                   // an ender pearl: throw it, you land where it lands (SandBoxPearl.cs)
    internal SbTurretKind Turret;          // an auto-turret or sentry (SandBoxGadgets.cs)
    internal SbShape Shape;                // a slab, stairs, fence, door, lever ... (SandBoxRedstone.cs)
    internal int Back = -1;                // a block with its own back face, sides turned to its front (pistons, observers)
    internal bool SidesUpFront;            // its side textures' top edge faces its front (pistons)
    internal Material Mat;                 // its own textured material (Minecraft entity textures)
    internal bool PixelItem;               // made from its Minecraft item picture (rebuilt with a new pack)
    internal bool Hidden;                 // made by the game (portal, fire), not in the menu
    internal string Sound = "stone";      // Minecraft dig sound group
    internal Color LightColor = new Color(1f, 0.85f, 0.6f);

    // guns
    internal SbAmmo Ammo;
    internal float FireRate = 4f;
    internal bool Auto;
    internal int Pellets = 1;
    internal float Spread = 0.6f;
    internal float Speed = 60f;

    // explosives / melee
    internal SbBlast Blast;
    internal float Radius = 3f;
    internal float Fuse = 4f;
    internal bool Holdable;
    internal int MeleeTier;

    // runtime
    internal ushort Key;
    internal Mesh Mesh;
    internal bool HasGlow;
    internal Vector3 Grip;
    internal Vector3 Muzzle;
    internal Texture2D icon;

    internal bool IsHeldType { get { return Kind == SbKind.Gun || Kind == SbKind.Melee || ((Kind == SbKind.Explosive || Kind == SbKind.Tool) && Holdable); } }
    internal bool IsBlockLike { get { return Kind == SbKind.Block; } }
  }

  internal static class SbCatalog
  {
    internal static readonly List<SbItem> All = new List<SbItem>();
    internal static readonly Dictionary<ushort, SbItem> ByKey = new Dictionary<ushort, SbItem>();
    internal static readonly Dictionary<string, SbItem> ById = new Dictionary<string, SbItem>(StringComparer.Ordinal);
    private static bool built;

    internal static readonly string[] TabNames = { "MINECRAFT", "OBJECTS", "WEAPONS" };

    internal static Color C(string hex) { Color c; ColorUtility.TryParseHtmlString(hex, out c); return c; }

    internal static SbItem Get(string id) { SbItem i; return ById.TryGetValue(id, out i) ? i : null; }

    internal static List<SbItem> ForTab(SbTab tab)
    {
      Init();
      var list = new List<SbItem>();
      foreach (SbItem i in All) if (i.Tab == tab && !i.Hidden) list.Add(i);
      return list;
    }

    // a new texture pack: the items made from Minecraft item pictures (swords,
    // buckets, flint and steel, Eye of Ender) are rebuilt from the pack's pictures. The
    // new shape goes into the same Mesh, so ones already placed or held change too.
    internal static void RebuildPixelItems()
    {
      foreach (SbItem item in All)
      {
        bool pixel = item.Id.StartsWith("mc_sword_", StringComparison.Ordinal) || item.Id == "mc_water" || item.Id == "mc_lava" ||
                     item.Id == "mc_flint" || item.Id == "mc_endereye" || item.Id == "mc_enderpearl" || item.PixelItem;
        if (!pixel || item.Build == null || item.Mesh == null) continue;
        try
        {
          var kit = new SbMeshKit();
          item.Build(kit);
          Mesh fresh = kit.ToMesh("Unknowns SandBox " + item.Name);
          Mesh m = item.Mesh;
          m.Clear();
          m.indexFormat = fresh.indexFormat;
          m.SetVertices(fresh.vertices); m.SetNormals(fresh.normals); m.SetUVs(0, fresh.uv);
          m.subMeshCount = fresh.subMeshCount;
          for (int s = 0; s < fresh.subMeshCount; s++) m.SetTriangles(fresh.GetTriangles(s), s, false);
          m.RecalculateBounds();
          item.Grip = kit.HasGrip ? kit.Grip : Vector3.zero;
          item.Muzzle = kit.Muzzle;
          UnityEngine.Object.Destroy(fresh);
        }
        catch (Exception ex) { SbLog.Warn("item " + item.Id + ": " + ex.Message); }
      }
    }

    // a new texture pack: the menu pictures are drawn again when next shown
    internal static void ResetIcons()
    {
      foreach (SbItem i in All)
      {
        if (i.icon != null && i.icon != Texture2D.whiteTexture) UnityEngine.Object.Destroy(i.icon);
        i.icon = null;
      }
    }

    internal static Texture2D Icon(SbItem item)
    {
      if (item == null) return null;
      if (item.icon != null) return item.icon;
      Init();
      try
      {
        bool atlas = item.Kind == SbKind.Block;
        // blocks show their -Z front, props are made facing +Z, guns and swords from the side
        float yaw = item.Kind == SbKind.Gun || item.Kind == SbKind.Melee ? -100f : atlas || item.Kind == SbKind.Fluid ? -35f : 145f;
        Texture2D own = null;
        if (item.Mat != null) own = (item.Mat.HasProperty("_BaseMap") ? item.Mat.GetTexture("_BaseMap") : item.Mat.HasProperty("_MainTex") ? item.Mat.GetTexture("_MainTex") : null) as Texture2D;
        if (own != null && !own.isReadable) own = null;
        item.icon = SbIcons.Render(item.Mesh, atlas, 96, yaw, item.Kind == SbKind.Melee ? 10f : 24f, null, own);
      }
      catch (Exception ex)
      {
        SbLog.Warn("icon " + item.Id + ": " + ex.Message);
        item.icon = Texture2D.whiteTexture;
      }
      return item.icon;
    }

    // ---------------------------------------------------------------- build
    internal static void Init()
    {
      if (built) return;
      built = true;
      var atlasFirst = SbBlockArt.Atlas;   // also loads the real Minecraft textures (item sprites below)
      AddMinecraft();
      SbMobDefs.AddToCatalog(i => Add(i));   // Minecraft mobs (SandBoxMobs.cs)
      AddObjects();
      AddWeapons();
      ApplyKenneyModels();
      ApplyKenneyObjects();
      SbGunArt.Apply(id => All.Find(i => i.Id == id));   // realistic guns and bombs (SandBoxGunModels.cs)
      SbRealModels.Apply(id => All.Find(i => i.Id == id), i => Add(i));   // realistic downloaded props and guns (SandBoxModels.cs)
      SbMcModels.Apply(id => All.Find(i => i.Id == id));   // Minecraft textures on the Minecraft tab (SandBoxMcModels.cs)
      // the grid groups items by section, so keep each section together (stable)
      string[] order = { "BUILDING", "MACHINES", "VEHICLES", "FUN", "HOME", "NATURE", "FOOD", "HOLIDAY", "RACING" };
      var sorted = new List<SbItem>();
      foreach (SbTab tab in new[] { SbTab.Minecraft, SbTab.Objects, SbTab.Weapons })
      {
        var inTab = All.FindAll(i => i.Tab == tab);
        if (tab == SbTab.Objects)
        {
          foreach (string sec in order) sorted.AddRange(inTab.FindAll(i => i.Section == sec));
          sorted.AddRange(inTab.FindAll(i => Array.IndexOf(order, i.Section) < 0));
        }
        else if (tab == SbTab.Minecraft)
        {
          // the Minecraft tab in sections
          foreach (SbItem i in inTab) if (i.Section == "BLOCKS") i.Section = SectionFor(i.Id) ?? "SPECIAL";
          foreach (string sec in MinecraftSections) sorted.AddRange(inTab.FindAll(i => i.Section == sec));
          sorted.AddRange(inTab.FindAll(i => Array.IndexOf(MinecraftSections, i.Section) < 0));
        }
        else sorted.AddRange(inTab);
      }
      All.Clear();
      All.AddRange(sorted);

      foreach (SbItem item in All)
      {
        // stable 16 bit key from the id, so both ends agree without sending names
        uint h = 2166136261u;
        foreach (char ch in item.Id) { h ^= ch; h *= 16777619u; }
        ushort key = (ushort)((h ^ (h >> 16)) & 0xFFFF);
        while (key == 0 || ByKey.ContainsKey(key)) key++;
        item.Key = key;
        ByKey[key] = item;
        ById[item.Id] = item;

        try
        {
          if (item.Kind == SbKind.Block)
          {
            item.Mesh = item.Back >= 0 ? SbBlockMesh.Directional(item.Front, item.Back, item.Side, item.Top, item.SidesUpFront) :
              item.Shape == SbShape.Bed ? (SbMcModels.BedMesh(item) ?? SbShapes.MeshOf(item, false)) :
              item.Shape != SbShape.Cube ? SbShapes.MeshOf(item, false) :
              item.Id == "mc_endframe" || item.Id == "mc_endframe_eye" ? SbBlockMesh.Frame(item.Id == "mc_endframe_eye") : SbBlockMesh.Get(item.Top, item.Side, item.Bottom, item.Front);
          }
          else
          {
            var kit = new SbMeshKit();
            item.Build?.Invoke(kit);
            item.Mesh = kit.ToMesh("Unknowns SandBox " + item.Name);
            item.HasGlow = kit.HasGlow;
            item.Grip = kit.HasGrip ? kit.Grip : Vector3.zero;
            item.Muzzle = kit.Muzzle;
          }
        }
        catch (Exception ex)
        {
          SbLog.Warn("model " + item.Id + ": " + ex.Message);
          var kit = new SbMeshKit();
          kit.Box(Vector3.zero, Vector3.one * 0.3f, Color.magenta);
          item.Mesh = kit.ToMesh("Unknowns SandBox fallback");
        }
      }
      try { SbMcModels.AfterMeshes(id => All.Find(i => i.Id == id)); } catch (Exception ex) { SbLog.Warn("Minecraft models: " + ex.Message); }
      var unused = SbPalette.Texture;
    }

    private static SbItem Add(SbItem i) { All.Add(i); return i; }

    private static void Block(string id, string name, string desc, int all, SbBlockFx fx = SbBlockFx.None)
    {
      Add(new SbItem { Id = id, Name = name, Desc = desc, Tab = SbTab.Minecraft, Section = "BLOCKS", Kind = SbKind.Block, Top = all, Side = all, Bottom = all, Front = all, BlockFx = fx, Sound = SoundFor(id) });
    }

    private static void Block(string id, string name, string desc, int top, int side, int bottom, SbBlockFx fx = SbBlockFx.None, int front = -1)
    {
      Add(new SbItem { Id = id, Name = name, Desc = desc, Tab = SbTab.Minecraft, Section = "BLOCKS", Kind = SbKind.Block, Top = top, Side = side, Bottom = bottom, Front = front < 0 ? side : front, BlockFx = fx, Sound = SoundFor(id) });
    }

    // ---------------------------------------------------------------- more blocks, in sections
    internal static readonly string[] MinecraftSections =
    {
      "NATURAL", "WOOD", "STONE", "ORES & MINERALS", "COLOURED", "GLASS", "NETHER", "END", "LIGHT", "FUNCTIONAL", "SPECIAL",
      "SHAPES", "REDSTONE", "LIQUIDS", "TOOLS", "SWORDS", "COMBAT", "ITEMS", "TRANSPORT", "ANIMALS", "MOBS", "NETHER MOBS", "END MOBS", "BOSSES"
    };

    private static void B(string section, string id, string name, int top, int side, int bottom, SbBlockFx fx = SbBlockFx.None)
    {
      Add(new SbItem { Id = id, Name = name, Desc = name + ".", Tab = SbTab.Minecraft, Section = section, Kind = SbKind.Block, Top = top, Side = side, Bottom = bottom, Front = side, BlockFx = fx, Sound = SoundFor(id) });
    }

    private static void B(string section, string id, string name, string tile, SbBlockFx fx = SbBlockFx.None)
    {
      int t = SbBlockArt.T(tile);
      B(section, id, name, t, t, t, fx);
    }

    private static string Title(string s)
    {
      var sb = new System.Text.StringBuilder();
      bool up = true;
      foreach (char ch in s.Replace('_', ' '))
      {
        sb.Append(up ? char.ToUpperInvariant(ch) : ch);
        up = ch == ' ';
      }
      return sb.ToString();
    }

    private static void AddMoreBlocks()
    {
      Func<string, int> T = SbBlockArt.T;
      // wood: planks and logs of every tree, the untinted leaves
      foreach (string w in new[] { "spruce", "birch", "jungle", "acacia", "dark_oak", "mangrove", "cherry" })
      {
        B("WOOD", "mc_" + w + "_planks", Title(w) + " Planks", w + "_planks");
        B("WOOD", "mc_" + w + "_log", Title(w) + " Log", T(w + "_log_top"), T(w + "_log"), T(w + "_log_top"));
      }
      B("WOOD", "mc_crimson_planks", "Crimson Planks", "crimson_planks");
      B("WOOD", "mc_crimson_stem", "Crimson Stem", T("crimson_stem_top"), T("crimson_stem"), T("crimson_stem_top"));
      B("WOOD", "mc_warped_planks", "Warped Planks", "warped_planks");
      B("WOOD", "mc_warped_stem", "Warped Stem", T("warped_stem_top"), T("warped_stem"), T("warped_stem_top"));
      foreach (string l in new[] { "spruce_leaves", "birch_leaves", "cherry_leaves", "azalea_leaves" }) B("WOOD", "mc_" + l, Title(l), l);

      // stone
      foreach (string s in new[] { "granite", "polished_granite", "diorite", "polished_diorite", "andesite", "polished_andesite", "cobbled_deepslate",
                                   "polished_deepslate", "deepslate_bricks", "deepslate_tiles", "tuff", "calcite", "smooth_stone", "mossy_stone_bricks",
                                   "cracked_stone_bricks", "chiseled_stone_bricks", "prismarine", "prismarine_bricks", "dark_prismarine", "mud_bricks",
                                   "packed_mud", "polished_blackstone_bricks", "quartz_bricks", "smooth_basalt", "dripstone_block", "terracotta" })
        B("STONE", "mc_" + s, Title(s), s);
      B("STONE", "mc_deepslate", "Deepslate", T("deepslate_top"), T("deepslate"), T("deepslate_top"));
      B("STONE", "mc_sandstone", "Sandstone", T("sandstone_top"), T("sandstone"), T("sandstone_bottom"));
      B("STONE", "mc_red_sandstone", "Red Sandstone", T("red_sandstone_top"), T("red_sandstone"), T("red_sandstone_bottom"));
      B("STONE", "mc_blackstone", "Blackstone", T("blackstone_top"), T("blackstone"), T("blackstone_top"));
      B("STONE", "mc_basalt", "Basalt", T("basalt_top"), T("basalt_side"), T("basalt_top"));
      B("STONE", "mc_chiseled_quartz", "Chiseled Quartz", T("chiseled_quartz_block_top"), T("chiseled_quartz_block"), T("chiseled_quartz_block_top"));

      // ores and minerals
      foreach (string s in new[] { "deepslate_coal_ore", "deepslate_iron_ore", "deepslate_gold_ore", "deepslate_diamond_ore", "deepslate_emerald_ore",
                                   "redstone_ore", "lapis_ore", "copper_ore", "copper_block", "exposed_copper", "weathered_copper", "oxidized_copper",
                                   "raw_iron_block", "raw_gold_block", "raw_copper_block", "coal_block", "netherite_block", "amethyst_block" })
        B("ORES & MINERALS", "mc_" + s, Title(s), s);

      // natural
      B("NATURAL", "mc_red_sand", "Red Sand", "red_sand", SbBlockFx.Falls);
      foreach (string s in new[] { "coarse_dirt", "rooted_dirt", "moss_block", "clay", "mud" }) B("NATURAL", "mc_" + s, Title(s), s);
      B("NATURAL", "mc_podzol", "Podzol", T("podzol_top"), T("podzol_side"), SbBlockArt.Dirt);
      B("NATURAL", "mc_mycelium", "Mycelium", T("mycelium_top"), T("mycelium_side"), SbBlockArt.Dirt);
      B("NATURAL", "mc_dirt_path", "Dirt Path", T("dirt_path_top"), T("dirt_path_side"), SbBlockArt.Dirt);
      B("NATURAL", "mc_packed_ice", "Packed Ice", "packed_ice", SbBlockFx.Ice);
      B("NATURAL", "mc_blue_ice", "Blue Ice", "blue_ice", SbBlockFx.Ice);

      // the 16 colours
      string[] colours = { "white", "orange", "magenta", "light_blue", "yellow", "lime", "pink", "gray", "light_gray", "cyan", "purple", "blue", "brown", "green", "red", "black" };
      foreach (string c in colours) B("COLOURED", "mc_" + c + "_concrete", Title(c) + " Concrete", c + "_concrete");
      foreach (string c in colours) B("COLOURED", "mc_" + c + "_terracotta", Title(c) + " Terracotta", c + "_terracotta");
      foreach (string c in new[] { "light_gray", "gray", "brown", "lime", "light_blue", "magenta" }) B("COLOURED", "mc_wool_" + c, Title(c) + " Wool", c + "_wool");
      foreach (string c in colours) B("GLASS", "mc_" + c + "_glass", Title(c) + " Stained Glass", c + "_stained_glass", SbBlockFx.Glass);

      // nether
      B("NETHER", "mc_crimson_nylium", "Crimson Nylium", T("crimson_nylium"), T("crimson_nylium_side"), SbBlockArt.Netherrack);
      B("NETHER", "mc_warped_nylium", "Warped Nylium", T("warped_nylium"), T("warped_nylium_side"), SbBlockArt.Netherrack);
      foreach (string s in new[] { "nether_wart_block", "warped_wart_block", "soul_soil", "red_nether_bricks", "gilded_blackstone", "nether_gold_ore", "nether_quartz_ore" })
        B("NETHER", "mc_" + s, Title(s), s);
      B("NETHER", "mc_ancient_debris", "Ancient Debris", T("ancient_debris_top"), T("ancient_debris_side"), T("ancient_debris_top"));
      B("NETHER", "mc_crying_obsidian", "Crying Obsidian", "crying_obsidian", SbBlockFx.Glow);

      // the end
      B("END", "mc_end_stone_bricks", "End Stone Bricks", "end_stone_bricks");
      B("END", "mc_purpur", "Purpur Block", "purpur_block");
      B("END", "mc_purpur_pillar", "Purpur Pillar", T("purpur_pillar_top"), T("purpur_pillar"), T("purpur_pillar_top"));

      // light
      B("LIGHT", "mc_shroomlight", "Shroomlight", "shroomlight", SbBlockFx.Glow);
      B("LIGHT", "mc_redstone_lamp", "Redstone Lamp", "redstone_lamp_on", SbBlockFx.Glow);
      foreach (string f in new[] { "ochre", "verdant", "pearlescent" })
        B("LIGHT", "mc_" + f + "_froglight", Title(f) + " Froglight", T(f + "_froglight_top"), T(f + "_froglight_side"), T(f + "_froglight_top"), SbBlockFx.Glow);

      // functional
      B("FUNCTIONAL", "mc_jukebox", "Jukebox", T("jukebox_top"), T("jukebox_side"), T("jukebox_side"));
      B("REDSTONE", "mc_note_block", "Note Block", "note_block");
      B("FUNCTIONAL", "mc_target", "Target", T("target_top"), T("target_side"), T("target_top"));
      B("FUNCTIONAL", "mc_barrel", "Barrel", T("barrel_top"), T("barrel_side"), T("barrel_top"));
      B("FUNCTIONAL", "mc_honey", "Honey Block", T("honey_block_top"), T("honey_block_side"), T("honey_block_top"), SbBlockFx.Slime);
      SbItem disp = new SbItem { Id = "mc_dispenser", Name = "Dispenser", Desc = "Dispenser.", Tab = SbTab.Minecraft, Section = "FUNCTIONAL", Kind = SbKind.Block,
        Top = SbBlockArt.FurnaceTop, Side = SbBlockArt.FurnaceSide, Bottom = SbBlockArt.FurnaceTop, Front = T("dispenser_front"), Sound = "stone" };
      disp.Section = "REDSTONE";
      Add(disp);
      AddEvenMoreBlocks();
      SbMcItems.AddToCatalog(i => Add(i));
      SbEffectItems.AddToCatalog(i => Add(i));   // firework rocket (SandBoxEffects.cs)   // bow, shield, armour, rod, potions, elytra, boat, minecart, rails (SandBoxItems.cs)
      SbShapes.AddToCatalog(i => Add(i));   // slabs, stairs, fences, walls, doors, beds and redstone (SandBoxRedstone.cs)
    }

    // a block with a front face (it faces you when placed)
    private static void F(string section, string id, string name, int top, int side, int bottom, int front, SbBlockFx fx = SbBlockFx.None)
    {
      Add(new SbItem { Id = id, Name = name, Desc = name + ".", Tab = SbTab.Minecraft, Section = section, Kind = SbKind.Block, Top = top, Side = side, Bottom = bottom, Front = front, BlockFx = fx, Sound = SoundFor(id) });
    }

    // even more blocks
    private static void AddEvenMoreBlocks()
    {
      Func<string, int> T = SbBlockArt.T;
      // wood
      B("WOOD", "mc_pale_oak_planks", "Pale Oak Planks", "pale_oak_planks");
      B("WOOD", "mc_pale_oak_log", "Pale Oak Log", T("pale_oak_log_top"), T("pale_oak_log"), T("pale_oak_log_top"));
      B("WOOD", "mc_bamboo_planks", "Bamboo Planks", "bamboo_planks");
      B("WOOD", "mc_bamboo_mosaic", "Bamboo Mosaic", "bamboo_mosaic");
      B("WOOD", "mc_bamboo_block", "Block of Bamboo", T("bamboo_block_top"), T("bamboo_block"), T("bamboo_block_top"));
      B("WOOD", "mc_stripped_bamboo_block", "Block of Stripped Bamboo", T("stripped_bamboo_block_top"), T("stripped_bamboo_block"), T("stripped_bamboo_block_top"));
      foreach (string w in new[] { "oak", "spruce", "birch", "jungle", "acacia", "dark_oak", "mangrove", "cherry", "pale_oak" })
        B("WOOD", "mc_stripped_" + w + "_log", "Stripped " + Title(w) + " Log", T("stripped_" + w + "_log_top"), T("stripped_" + w + "_log"), T("stripped_" + w + "_log_top"));
      foreach (string w in new[] { "crimson", "warped" })
        B("WOOD", "mc_stripped_" + w + "_stem", "Stripped " + Title(w) + " Stem", T("stripped_" + w + "_stem_top"), T("stripped_" + w + "_stem"), T("stripped_" + w + "_stem_top"));
      foreach (string l in new[] { "acacia_leaves", "dark_oak_leaves", "jungle_leaves", "mangrove_leaves", "pale_oak_leaves" }) B("WOOD", "mc_" + l, Title(l), l);

      // stone
      foreach (string s in new[] { "polished_blackstone", "chiseled_polished_blackstone", "cracked_polished_blackstone_bricks", "chiseled_deepslate",
                                   "cracked_deepslate_bricks", "cracked_deepslate_tiles", "tuff_bricks", "polished_tuff", "cut_sandstone",
                                   "chiseled_sandstone", "cut_red_sandstone", "chiseled_red_sandstone", "cracked_nether_bricks", "chiseled_nether_bricks",
                                   "resin_block", "resin_bricks", "chiseled_resin_bricks" })
        B("STONE", "mc_" + s, Title(s), s);
      B("STONE", "mc_chiseled_tuff", "Chiseled Tuff", T("chiseled_tuff_top"), T("chiseled_tuff"), T("chiseled_tuff_top"));
      B("STONE", "mc_chiseled_tuff_bricks", "Chiseled Tuff Bricks", T("chiseled_tuff_bricks_top"), T("chiseled_tuff_bricks"), T("chiseled_tuff_bricks_top"));
      B("STONE", "mc_quartz_pillar", "Quartz Pillar", T("quartz_pillar_top"), T("quartz_pillar"), T("quartz_pillar_top"));
      B("STONE", "mc_polished_basalt", "Polished Basalt", T("polished_basalt_top"), T("polished_basalt_side"), T("polished_basalt_top"));
      B("STONE", "mc_reinforced_deepslate", "Reinforced Deepslate", T("reinforced_deepslate_top"), T("reinforced_deepslate_side"), T("reinforced_deepslate_bottom"));

      // ores and minerals
      foreach (string s in new[] { "deepslate_copper_ore", "deepslate_lapis_ore", "deepslate_redstone_ore", "budding_amethyst" })
        B("ORES & MINERALS", "mc_" + s, Title(s), s);
      foreach (string age in new[] { "", "exposed_", "weathered_", "oxidized_" })
      {
        string n = age == "" ? "" : Title(age.TrimEnd('_')) + " ";
        B("ORES & MINERALS", "mc_" + age + "cut_copper", n + "Cut Copper", age + "cut_copper");
        B("ORES & MINERALS", "mc_" + age + "chiseled_copper", n + "Chiseled Copper", age + "chiseled_copper");
        B("ORES & MINERALS", "mc_" + age + "copper_bulb", n + "Copper Bulb", age + "copper_bulb", age == "" || age == "exposed_" ? SbBlockFx.Glow : SbBlockFx.None);
        B("GLASS", "mc_" + age + "copper_grate", n + "Copper Grate", age + "copper_grate", SbBlockFx.Glass);
      }

      // natural
      foreach (string s in new[] { "sculk", "pale_moss_block", "honeycomb_block", "wet_sponge", "red_mushroom_block", "brown_mushroom_block" })
        B("NATURAL", "mc_" + s, Title(s), s);
      B("NATURAL", "mc_mushroom_stem", "Mushroom Stem", T("mushroom_block_inside"), T("mushroom_stem"), T("mushroom_block_inside"));
      B("NATURAL", "mc_powder_snow", "Powder Snow", "powder_snow");
      B("NATURAL", "mc_farmland", "Farmland", T("farmland_moist"), SbBlockArt.Dirt, SbBlockArt.Dirt);
      B("NATURAL", "mc_snowy_grass", "Snowy Grass Block", T("powder_snow"), T("grass_block_snow"), SbBlockArt.Dirt);
      B("NATURAL", "mc_suspicious_sand", "Suspicious Sand", "suspicious_sand_0", SbBlockFx.Falls);
      B("NATURAL", "mc_suspicious_gravel", "Suspicious Gravel", "suspicious_gravel_0", SbBlockFx.Falls);
      B("NATURAL", "mc_muddy_mangrove_roots", "Muddy Mangrove Roots", T("muddy_mangrove_roots_top"), T("muddy_mangrove_roots_side"), T("muddy_mangrove_roots_top"));
      B("NATURAL", "mc_dried_kelp_block", "Dried Kelp Block", T("dried_kelp_top"), T("dried_kelp_side"), T("dried_kelp_bottom"));
      B("NATURAL", "mc_bone_block", "Bone Block", T("bone_block_top"), T("bone_block_side"), T("bone_block_top"));
      foreach (string c in new[] { "tube", "brain", "bubble", "fire", "horn" })
      {
        B("NATURAL", "mc_" + c + "_coral_block", Title(c) + " Coral Block", c + "_coral_block");
        B("NATURAL", "mc_dead_" + c + "_coral_block", "Dead " + Title(c) + " Coral Block", "dead_" + c + "_coral_block");
      }
      B("GLASS", "mc_tinted_glass", "Tinted Glass", "tinted_glass", SbBlockFx.Glass);

      // the 16 colours
      string[] colours = { "white", "orange", "magenta", "light_blue", "yellow", "lime", "pink", "gray", "light_gray", "cyan", "purple", "blue", "brown", "green", "red", "black" };
      foreach (string c in colours) B("COLOURED", "mc_" + c + "_concrete_powder", Title(c) + " Concrete Powder", c + "_concrete_powder", SbBlockFx.Falls);
      foreach (string c in colours) B("COLOURED", "mc_" + c + "_glazed_terracotta", Title(c) + " Glazed Terracotta", c + "_glazed_terracotta");

      // functional
      F("FUNCTIONAL", "mc_smithing_table", "Smithing Table", T("smithing_table_top"), T("smithing_table_side"), T("smithing_table_bottom"), T("smithing_table_front"));
      F("FUNCTIONAL", "mc_fletching_table", "Fletching Table", T("fletching_table_top"), T("fletching_table_side"), SbBlockArt.Planks, T("fletching_table_front"));
      F("FUNCTIONAL", "mc_cartography_table", "Cartography Table", T("cartography_table_top"), T("cartography_table_side3"), SbBlockArt.Planks, T("cartography_table_side1"));
      F("FUNCTIONAL", "mc_loom", "Loom", T("loom_top"), T("loom_side"), T("loom_bottom"), T("loom_front"));
      F("FUNCTIONAL", "mc_smoker", "Smoker", T("smoker_top"), T("smoker_side"), T("smoker_bottom"), T("smoker_front"));
      F("FUNCTIONAL", "mc_blast_furnace", "Blast Furnace", T("blast_furnace_top"), T("blast_furnace_side"), T("blast_furnace_top"), T("blast_furnace_front"));
      F("REDSTONE", "mc_observer", "Observer", T("observer_top"), T("observer_side"), T("observer_top"), T("observer_front"));
      // pistons face you when placed (the face pushes), like Minecraft
      F("REDSTONE", "mc_piston", "Piston", T("piston_side"), T("piston_side"), T("piston_side"), T("piston_top"));
      F("REDSTONE", "mc_sticky_piston", "Sticky Piston", T("piston_side"), T("piston_side"), T("piston_side"), T("piston_top_sticky"));
      B("FUNCTIONAL", "mc_lodestone", "Lodestone", T("lodestone_top"), T("lodestone_side"), T("lodestone_top"));
      F("FUNCTIONAL", "mc_bee_nest", "Bee Nest", T("bee_nest_top"), T("bee_nest_side"), T("bee_nest_bottom"), T("bee_nest_front"));
      F("FUNCTIONAL", "mc_beehive", "Beehive", T("beehive_end"), T("beehive_side"), T("beehive_end"), T("beehive_front"));
      F("FUNCTIONAL", "mc_chiseled_bookshelf", "Chiseled Bookshelf", T("chiseled_bookshelf_top"), T("chiseled_bookshelf_side"), T("chiseled_bookshelf_top"), T("chiseled_bookshelf_occupied"));
      F("FUNCTIONAL", "mc_dropper", "Dropper", SbBlockArt.FurnaceTop, SbBlockArt.FurnaceSide, SbBlockArt.FurnaceTop, T("dropper_front"));
      F("FUNCTIONAL", "mc_crafter", "Crafter", T("crafter_top"), T("crafter_east"), T("crafter_bottom"), T("crafter_north"));
      B("FUNCTIONAL", "mc_respawn_anchor", "Respawn Anchor", T("respawn_anchor_top_off"), T("respawn_anchor_side0"), T("respawn_anchor_bottom"));
      B("FUNCTIONAL", "mc_sculk_catalyst", "Sculk Catalyst", T("sculk_catalyst_top"), T("sculk_catalyst_side"), T("sculk_catalyst_bottom"));
      B("REDSTONE", "mc_redstone_lamp_off", "Redstone Lamp (off)", "redstone_lamp");
      B("FUNCTIONAL", "mc_creaking_heart", "Creaking Heart", T("creaking_heart_top"), T("creaking_heart"), T("creaking_heart_top"));
      F("FUNCTIONAL", "mc_vault", "Vault", T("vault_top"), T("vault_side_off"), T("vault_bottom"), T("vault_front_off"));

      // special
      F("SPECIAL", "mc_command_block", "Command Block", T("command_block_side"), T("command_block_side"), T("command_block_side"), T("command_block_front"));
      F("SPECIAL", "mc_repeating_command_block", "Repeating Command Block", T("repeating_command_block_side"), T("repeating_command_block_side"), T("repeating_command_block_side"), T("repeating_command_block_front"));
      B("SPECIAL", "mc_structure_block", "Structure Block", "structure_block");
      B("SPECIAL", "mc_jigsaw", "Jigsaw Block", T("jigsaw_top"), T("jigsaw_side"), T("jigsaw_bottom"));
      B("SPECIAL", "mc_spawner", "Monster Spawner", "spawner", SbBlockFx.Glass);
      B("SPECIAL", "mc_trial_spawner", "Trial Spawner", T("trial_spawner_top_inactive"), T("trial_spawner_side_inactive"), T("trial_spawner_bottom"), SbBlockFx.Glass);
    }

    // the section of a block that is listed under BLOCKS
    private static string SectionFor(string id)
    {
      switch (id)
      {
        case "mc_grass": case "mc_dirt": case "mc_sand": case "mc_gravel": case "mc_snow": case "mc_ice": return "NATURAL";
        case "mc_planks": case "mc_log": case "mc_leaves": return "WOOD";
        case "mc_stone": case "mc_cobble": case "mc_mossy": case "mc_bricks": case "mc_stonebricks": case "mc_quartz": return "STONE";
        case "mc_glass": return "GLASS";
        case "mc_netherrack": case "mc_netherbricks": case "mc_soulsand": case "mc_magma": return "NETHER";
        case "mc_endstone": case "mc_endframe": return "END";
        case "mc_glowstone": case "mc_sealantern": case "mc_jack": return "LIGHT";
        case "mc_craft": case "mc_furnace": case "mc_bookshelf": case "mc_pumpkin": case "mc_melon": case "mc_hay": return "FUNCTIONAL";
        case "mc_tnt": case "mc_slime": case "mc_sponge": case "mc_obsidian": case "mc_bedrock": return "SPECIAL";
      }
      if (id.StartsWith("mc_wool", StringComparison.Ordinal)) return "COLOURED";
      if (id.EndsWith("ore", StringComparison.Ordinal) || id == "mc_diamond" || id == "mc_gold" || id == "mc_iron" || id == "mc_emerald" ||
          id == "mc_redstone" || id == "mc_lapis") return "ORES & MINERALS";
      return null;
    }

    // Minecraft's block sound groups
    private static string SoundFor(string id)
    {
      // the new blocks
      if (id.Contains("glass")) return "glass";
      if (id.Contains("planks") || id.EndsWith("_log", StringComparison.Ordinal) || id.Contains("stem") || id.Contains("barrel") || id.Contains("note_block") || id.Contains("jukebox")) return "wood";
      if (id.Contains("leaves") || id.Contains("nylium") || id.Contains("moss") || id.Contains("podzol") || id.Contains("mycelium") || id.Contains("wart") || id.Contains("target")) return "grass";
      if (id == "mc_red_sand" || id.Contains("soul_soil")) return "sand";
      if (id.Contains("clay") || id.Contains("mud") || id.Contains("path") || id.Contains("coarse") || id.Contains("rooted")) return "gravel";
      if (id.Contains("froglight") || id.Contains("shroomlight")) return "wood";
      if (id.Contains("honey")) return "slime";
      if (id.Contains("packed_ice") || id.Contains("blue_ice")) return "glass";
      if (id.Contains("wool")) return "cloth";
      if (id.Contains("planks") || id.Contains("log") || id.Contains("bookshelf") || id.Contains("craft") || id.Contains("pumpkin") || id.Contains("jack") || id.Contains("melon")) return "wood";
      if (id == "mc_gravel" || id == "mc_dirt") return "gravel";
      if (id == "mc_sand" || id == "mc_soulsand") return "sand";
      if (id == "mc_grass" || id == "mc_leaves" || id == "mc_hay" || id == "mc_sponge" || id == "mc_tnt") return "grass";
      if (id == "mc_snow") return "snow";
      if (id == "mc_glass" || id == "mc_ice" || id == "mc_glowstone" || id == "mc_sealantern") return "glass";
      if (id == "mc_slime") return "slime";
      return "stone";
    }

    // ---------------------------------------------------------------- Kenney object models
    // Furniture / Nature / Food / Holiday / Racing kits (www.kenney.nl, CC0).
    private static void ApplyKenneyObjects()
    {
      // existing items get the real models (same ids, so saved/networked objects keep working)
      Action<string, string, string, float, bool, float> swap = (id, model, section, size, byHeight, yaw) =>
      {
        SbItem it = All.Find(i => i.Id == id);
        if (it == null || !SbKenney.Has(model)) return;
        it.Build = k => SbKenney.Fit(k, model, size, byHeight, yaw);
        it.Section = section;
      };
      swap("ob_chair", "furniture-kit/chair", "HOME", 0.95f, true, 180f);
      swap("ob_table", "furniture-kit/table", "HOME", 0.8f, true, 0f);
      swap("ob_couch", "furniture-kit/loungeSofa", "HOME", 0.85f, true, 180f);
      swap("ob_tv", "furniture-kit/televisionVintage", "HOME", 0.7f, true, 180f);
      swap("ob_toilet", "furniture-kit/toilet", "HOME", 0.85f, true, 180f);
      swap("ob_crate", "furniture-kit/cardboardBoxClosed", "HOME", 0.5f, false, 0f);
      swap("ob_lamp", "racing-kit/lightPostModern", "RACING", 2.6f, true, 180f);
      swap("ob_campfire", "nature-kit/campfire_stones", "NATURE", 0.7f, false, 0f);
      swap("ob_tree", "nature-kit/tree_oak", "NATURE", 3.2f, true, 0f);
      swap("ob_rock", "nature-kit/rock_largeA", "NATURE", 1.0f, false, 0f);
      swap("ob_pillar", "nature-kit/statue_column", "NATURE", 1.8f, true, 0f);
      swap("ob_snowman", "holiday-kit/snowman-hat", "HOLIDAY", 1.4f, true, 180f);
      swap("ob_present", "holiday-kit/present-a-cube", "HOLIDAY", 0.45f, false, 0f);
      swap("ob_banana", "food-kit/banana", "FOOD", 0.28f, false, 0f);
      swap("ob_barrel", "food-kit/barrel", "FOOD", 0.8f, true, 0f);
      swap("ob_cone_traffic", "racing-kit/pylon", "RACING", 0.6f, true, 0f);

      // new ones
      Action<string, string, string, string, string, float, bool, float, bool, SbCol, float, SbPropFx> add =
        (id, name, desc, section, model, size, byHeight, yaw, anchored, col, mass, fx) =>
        {
          if (!SbKenney.Has(model)) return;
          Add(new SbItem
          {
            Id = id, Name = name, Desc = desc, Tab = SbTab.Objects, Section = section, Kind = SbKind.Prop,
            Build = k => SbKenney.Fit(k, model, size, byHeight, yaw), Col = col, DefaultAnchored = anchored, Mass = mass, PropFx = fx
          });
        };
      add("ob_teddy", "Teddy Bear", "Soft and cuddly.", "HOME", "furniture-kit/bear", 0.5f, true, 180f, false, SbCol.Box, 0.4f, SbPropFx.None);
      add("ob_bed", "Double Bed", "Nap time.", "HOME", "furniture-kit/bedDouble", 2.1f, false, 180f, true, SbCol.Box, 30f, SbPropFx.None);
      add("ob_fridge", "Fridge", "Big and cold.", "HOME", "furniture-kit/kitchenFridgeLarge", 2.0f, true, 180f, true, SbCol.Box, 60f, SbPropFx.None);
      add("ob_bookcase", "Bookcase", "Full of books.", "HOME", "furniture-kit/bookcaseOpen", 1.9f, true, 180f, true, SbCol.Box, 30f, SbPropFx.None);
      add("ob_plant", "Potted Plant", "Green.", "HOME", "furniture-kit/pottedPlant", 0.9f, true, 0f, false, SbCol.Box, 3f, SbPropFx.None);
      add("ob_laptop", "Laptop", "Gaming laptop.", "HOME", "furniture-kit/laptop", 0.35f, false, 180f, false, SbCol.Box, 1f, SbPropFx.None);
      add("ob_speaker", "Speaker", "Loud.", "HOME", "furniture-kit/speaker", 1.0f, true, 180f, true, SbCol.Box, 8f, SbPropFx.None);
      add("ob_floorlamp", "Floor Lamp", "Lights up.", "HOME", "furniture-kit/lampRoundFloor", 1.7f, true, 0f, true, SbCol.Box, 4f, SbPropFx.Light);
      add("ob_bathtub", "Bathtub", "Splish splash.", "HOME", "furniture-kit/bathtub", 1.7f, false, 180f, true, SbCol.Box, 40f, SbPropFx.None);
      add("ob_washer", "Washing Machine", "Spin cycle.", "HOME", "furniture-kit/washer", 0.9f, true, 180f, true, SbCol.Box, 40f, SbPropFx.Spin);
      add("ob_palm", "Palm Tree", "Tropical.", "NATURE", "nature-kit/tree_palmTall", 4.5f, true, 0f, true, SbCol.Box, 50f, SbPropFx.None);
      add("ob_pine", "Pine Tree", "Tall pine.", "NATURE", "nature-kit/tree_pineTallA_detailed", 5f, true, 0f, true, SbCol.Box, 50f, SbPropFx.None);
      add("ob_tent", "Tent", "Camping.", "NATURE", "nature-kit/tent_detailedOpen", 2.2f, false, 180f, true, SbCol.Box, 20f, SbPropFx.None);
      add("ob_canoe", "Canoe", "Paddle not included.", "NATURE", "nature-kit/canoe", 2.4f, false, 0f, false, SbCol.Box, 20f, SbPropFx.None);
      add("ob_mushroom", "Giant Mushroom", "Bouncy in a modded lobby.", "NATURE", "nature-kit/mushroom_redTall", 1.4f, true, 0f, true, SbCol.Box, 5f, SbPropFx.Trampoline);
      add("ob_cactus", "Cactus", "Don't hug it.", "NATURE", "nature-kit/cactus_tall", 2f, true, 0f, true, SbCol.Box, 10f, SbPropFx.None);
      add("ob_log", "Log", "A big log.", "NATURE", "nature-kit/log_large", 1.6f, false, 0f, false, SbCol.Box, 15f, SbPropFx.None);
      add("ob_fence", "Fence", "Keep out.", "NATURE", "nature-kit/fence_simple", 1.4f, false, 0f, true, SbCol.Box, 5f, SbPropFx.None);
      add("ob_bridge", "Wooden Bridge", "Cross it.", "NATURE", "nature-kit/bridge_wood", 2.4f, false, 0f, true, SbCol.Box, 30f, SbPropFx.None);
      add("ob_stonehead", "Stone Head", "Ancient.", "NATURE", "nature-kit/statue_head", 1.6f, true, 180f, true, SbCol.Box, 80f, SbPropFx.None);
      add("ob_sign", "Sign", "Look here.", "NATURE", "nature-kit/sign", 1.2f, true, 180f, true, SbCol.Box, 3f, SbPropFx.None);
      add("ob_watermelon", "Watermelon", "Juicy. Rolls.", "FOOD", "food-kit/watermelon", 0.4f, false, 0f, false, SbCol.Sphere, 4f, SbPropFx.None);
      add("ob_burger", "Burger", "Double cheese.", "FOOD", "food-kit/burger-cheese-double", 0.3f, false, 0f, false, SbCol.Box, 0.5f, SbPropFx.None);
      add("ob_pizza", "Pizza", "Pepperoni.", "FOOD", "food-kit/pizza", 0.5f, false, 0f, false, SbCol.Box, 0.8f, SbPropFx.None);
      add("ob_donut", "Donut", "Sprinkles!", "FOOD", "food-kit/donut-sprinkles", 0.22f, false, 0f, false, SbCol.Box, 0.2f, SbPropFx.None);
      add("ob_cake", "Birthday Cake", "Make a wish.", "FOOD", "food-kit/cake-birthday", 0.45f, false, 0f, true, SbCol.Box, 2f, SbPropFx.None);
      add("ob_icecream", "Ice Cream", "Cone.", "FOOD", "food-kit/ice-cream", 0.3f, true, 0f, false, SbCol.Box, 0.2f, SbPropFx.None);
      add("ob_hotdog", "Hot Dog", "With mustard.", "FOOD", "food-kit/hot-dog", 0.3f, false, 0f, false, SbCol.Box, 0.3f, SbPropFx.None);
      add("ob_taco", "Taco", "Crunchy.", "FOOD", "food-kit/taco", 0.25f, false, 0f, false, SbCol.Box, 0.2f, SbPropFx.None);
      add("ob_apple", "Apple", "Red.", "FOOD", "food-kit/apple", 0.12f, false, 0f, false, SbCol.Sphere, 0.2f, SbPropFx.None);
      add("ob_soda", "Soda Can", "Fizzy.", "FOOD", "food-kit/soda-can", 0.15f, true, 0f, false, SbCol.Box, 0.3f, SbPropFx.None);
      add("ob_xmastree", "Christmas Tree", "Decorated.", "HOLIDAY", "holiday-kit/tree-decorated", 2.4f, true, 0f, true, SbCol.Box, 20f, SbPropFx.Light);
      add("ob_reindeer", "Reindeer", "Dasher? Dancer?", "HOLIDAY", "holiday-kit/reindeer", 1.6f, true, 180f, true, SbCol.Box, 60f, SbPropFx.None);
      add("ob_nutcracker", "Nutcracker", "Crack.", "HOLIDAY", "holiday-kit/nutcracker", 1.5f, true, 180f, true, SbCol.Box, 10f, SbPropFx.None);
      add("ob_sled", "Sled", "Wheee.", "HOLIDAY", "holiday-kit/sled", 1.4f, false, 0f, false, SbCol.Box, 8f, SbPropFx.None);
      add("ob_train", "Toy Train", "Choo choo.", "HOLIDAY", "holiday-kit/train-locomotive", 1.4f, false, 0f, true, SbCol.Box, 20f, SbPropFx.None);
      add("ob_candycane", "Candy Cane", "Minty.", "HOLIDAY", "holiday-kit/candy-cane-red", 1.0f, true, 0f, true, SbCol.Box, 2f, SbPropFx.None);
      add("ob_lantern", "Lantern", "Warm light.", "HOLIDAY", "holiday-kit/lantern", 0.45f, true, 0f, true, SbCol.Box, 1f, SbPropFx.Light);
      add("ob_racecar_red", "Race Car (Red)", "Vroom.", "RACING", "racing-kit/raceCarRed", 2.6f, false, 0f, false, SbCol.Box, 80f, SbPropFx.None);
      add("ob_racecar_green", "Race Car (Green)", "Vroom vroom.", "RACING", "racing-kit/raceCarGreen", 2.6f, false, 0f, false, SbCol.Box, 80f, SbPropFx.None);
      add("ob_barrier", "Barrier", "Red and white.", "RACING", "racing-kit/barrierRed", 2f, false, 0f, true, SbCol.Box, 20f, SbPropFx.None);
      add("ob_flag", "Checkered Flag", "Finish line.", "RACING", "racing-kit/flagCheckers", 2.6f, true, 0f, true, SbCol.Box, 5f, SbPropFx.None);
      add("ob_ramp_race", "Stunt Ramp", "Launch off it.", "RACING", "racing-kit/ramp", 3f, false, 0f, true, SbCol.Mesh, 50f, SbPropFx.None);
    }

    // ---------------------------------------------------------------- Kenney gun models
    private static void ApplyKenneyModels()
    {
      Action<string, string, float, Action<SbMeshKit, Bounds, Vector3>> gun = (id, model, len, extra) =>
      {
        SbItem it = All.Find(i => i.Id == id);
        if (it == null || !SbKenney.Has(model)) return;
        it.Build = k =>
        {
          Vector3 mz;
          Bounds b = SbKenney.Add(k, model, len, out mz);
          extra?.Invoke(k, b, mz);
        };
      };
      gun("gun_pistol", "blaster-a", 0.28f, null);
      gun("gun_rifle", "blaster-d", 0.62f, null);
      gun("gun_shotgun", "blaster-l", 0.5f, null);
      gun("gun_sniper", "blaster-e", 0.85f, null);
      gun("gun_minigun", "blaster-o", 0.5f, null);
      gun("gun_laser", "blaster-i", 0.3f, (k, b, mz) => k.Torus(mz, 0.03f, 0.006f, Quaternion.Euler(90f, 0f, 0f), C("#29f0ff"), 14, 4, true));
      gun("gun_rocket", "blaster-h", 0.75f, (k, b, mz) => k.Cone(mz + Vector3.forward * 0.03f, 0.04f, 0.09f, Quaternion.Euler(90f, 0f, 0f), C("#c8c8c8"), 10));
      gun("gun_grenadelauncher", "blaster-b", 0.45f, null);
      gun("gun_flame", "blaster-q", 0.55f, (k, b, mz) => k.Ball(mz, 0.012f, C("#4ab0ff"), 6, true));
      gun("gun_melon", "blaster-r", 0.62f, (k, b, mz) =>
      {
        Vector3 top = new Vector3(0f, b.max.y + 0.06f, b.center.z);
        k.Sphere(top, new Vector3(0.08f, 0.07f, 0.1f), C("#3a9a2a"), 12);
        for (int i = 0; i < 4; i++) k.Sphere(top, new Vector3(0.081f, 0.071f, 0.101f), Quaternion.Euler(0f, 0f, i * 45f), C("#215a18"), 4);
      });
      gun("gun_potato", "blaster-m", 0.55f, (k, b, mz) => k.Sphere(mz + Vector3.forward * 0.02f, new Vector3(0.035f, 0.03f, 0.045f), C("#b08a4a"), 8));
      gun("gun_banana", "blaster-k", 0.34f, (k, b, mz) =>
      {
        for (int i = 0; i < 6; i++) { float t = (i - 2.5f) / 2.5f; k.Sphere(new Vector3(0f, b.max.y + 0.03f + t * t * 0.03f, b.center.z + t * 0.07f), Vector3.one * 0.022f, C(i == 0 || i == 5 ? "#7a5a20" : "#ffe14d"), 6); }
      });
      gun("gun_snowball", "blaster-j", 0.36f, (k, b, mz) => k.Ball(new Vector3(0f, b.max.y + 0.045f, b.center.z), 0.045f, Color.white, 10));
      gun("gun_paint", "blaster-c", 0.4f, (k, b, mz) => k.Sphere(new Vector3(0f, b.max.y + 0.05f, b.center.z - 0.03f), new Vector3(0.045f, 0.055f, 0.06f), C("#ff3df5"), 10));
      gun("gun_bubble", "blaster-g", 0.36f, (k, b, mz) => k.Torus(mz + Vector3.forward * 0.02f, 0.05f, 0.008f, Quaternion.Euler(90f, 0f, 0f), C("#8affef"), 18, 4, true));
      gun("gun_confetti", "blaster-n", 0.38f, (k, b, mz) => k.Frustum(mz + Vector3.forward * 0.04f, 0.025f, 0.06f, 0.08f, Quaternion.Euler(90f, 0f, 0f), C("#ff4bd8"), 12));
      gun("gun_duck", "blaster-p", 0.58f, (k, b, mz) =>
      {
        Vector3 top = new Vector3(0f, b.max.y + 0.05f, b.center.z);
        k.Ball(top, 0.05f, C("#ffd21f"), 10);
        k.Sphere(top + new Vector3(0f, -0.01f, 0.05f), new Vector3(0.02f, 0.01f, 0.03f), C("#ff8a1f"), 6);
      });
      SbItem gren = All.Find(i => i.Id == "bomb_grenade");
      if (gren != null && SbKenney.Has("grenade-b")) gren.Build = k => SbKenney.AddCentered(k, "grenade-b", 0.13f);
      SbItem sticky = All.Find(i => i.Id == "bomb_sticky");
      if (sticky != null && SbKenney.Has("grenade-a")) sticky.Build = k => { SbKenney.AddCentered(k, "grenade-a", 0.13f); k.Sphere(new Vector3(0f, -0.065f, 0f), new Vector3(0.05f, 0.015f, 0.05f), C("#9aff6a"), 10); };
    }

    // ---------------------------------------------------------------- pixel sprites
    // Extrudes a 16 x 16 pixel picture (rows top to bottom) into boxes. '.' = empty.
    private static void Voxels(SbMeshKit k, string[] rows, Dictionary<char, Color> pal, float px, float depth, Quaternion rot, Vector3 origin)
    {
      int h = rows.Length;
      for (int r = 0; r < h; r++)
      {
        string row = rows[r];
        int c = 0;
        while (c < row.Length)
        {
          char ch = row[c];
          if (ch == '.' || !pal.ContainsKey(ch)) { c++; continue; }
          int start = c;
          while (c < row.Length && row[c] == ch) c++;
          int len = c - start;
          float cx = (start + len * 0.5f) * px;
          float cy = (h - r - 0.5f) * px;
          Vector3 local = new Vector3(cx, cy, 0f);
          k.Box(origin + rot * local, new Vector3(len * px, px, depth), rot, pal[ch]);
        }
      }
    }

    private static string[] SwordSprite()
    {
      char[,] g = new char[16, 16];
      for (int r = 0; r < 16; r++) for (int c = 0; c < 16; c++) g[r, c] = '.';
      for (int r = 0; r < 16; r++)
        for (int c = 0; c < 16; c++)
        {
          int s = c + r, d = c - r;
          if (r >= 1 && r <= 11 && d >= -5 && d <= 13 && s >= 14 && s <= 16) g[r, c] = s == 14 ? 'h' : s == 15 ? 'b' : 'e';
          if ((d == -6 || d == -7) && s >= 11 && s <= 19) g[r, c] = 'g';
          if (d <= -8 && d >= -12 && (s == 15 || s == 16)) g[r, c] = s == 15 ? 'w' : 'd';
          if (d == -13 && s >= 14 && s <= 17) g[r, c] = 'g';
        }
      // outline
      char[,] o = (char[,])g.Clone();
      for (int r = 0; r < 16; r++)
        for (int c = 0; c < 16; c++)
        {
          if (g[r, c] != '.') continue;
          bool n = (r > 0 && g[r - 1, c] != '.') || (r < 15 && g[r + 1, c] != '.') || (c > 0 && g[r, c - 1] != '.') || (c < 15 && g[r, c + 1] != '.');
          if (n) o[r, c] = 'o';
        }
      string[] rows = new string[16];
      for (int r = 0; r < 16; r++)
      {
        var sb = new System.Text.StringBuilder(16);
        for (int c = 0; c < 16; c++) sb.Append(o[r, c]);
        rows[r] = sb.ToString();
      }
      return rows;
    }

    private static string[] BucketSprite()
    {
      return new[]
      {
        "................",
        "................",
        "....oooooooo....",
        "...o........o...",
        "..o..........o..",
        "..oiiiiiiiiiio..",
        "..offfffffffho..",
        "..offfffffffho..",
        "...offffffffo...",
        "...oiiiiiiiho...",
        "...oiiiiiiiho...",
        "....oiiiiiho....",
        "....oiiiiiho....",
        ".....oooooo.....",
        "................",
        "................",
      };
    }

    // ---------------------------------------------------------------- minecraft
    private static void AddMinecraft()
    {
      Block("mc_grass", "Grass Block", "The classic. Snaps to the grid like Minecraft.", SbBlockArt.GrassTop, SbBlockArt.GrassSide, SbBlockArt.Dirt);
      Block("mc_dirt", "Dirt", "Plain dirt.", SbBlockArt.Dirt);
      Block("mc_stone", "Stone", "Smooth stone.", SbBlockArt.Stone);
      Block("mc_cobble", "Cobblestone", "Made when water meets flowing lava.", SbBlockArt.Cobble);
      Block("mc_mossy", "Mossy Cobblestone", "Old and green.", SbBlockArt.Mossy);
      Block("mc_planks", "Oak Planks", "Wooden planks.", SbBlockArt.Planks);
      Block("mc_log", "Oak Log", "A tree trunk.", SbBlockArt.LogTop, SbBlockArt.LogSide, SbBlockArt.LogTop);
      Block("mc_leaves", "Oak Leaves", "Leafy.", SbBlockArt.Leaves);
      Block("mc_sand", "Sand", "Falls when nothing is under it.", SbBlockArt.Sand, SbBlockFx.Falls);
      Block("mc_gravel", "Gravel", "Falls when nothing is under it.", SbBlockArt.Gravel, SbBlockFx.Falls);
      Block("mc_glass", "Glass", "See-through.", SbBlockArt.Glass, SbBlockFx.Glass);
      Block("mc_bricks", "Bricks", "Red bricks.", SbBlockArt.Bricks);
      Block("mc_stonebricks", "Stone Bricks", "Castle walls.", SbBlockArt.StoneBricks);
      Block("mc_bookshelf", "Bookshelf", "Full of books.", SbBlockArt.Planks, SbBlockArt.Bookshelf, SbBlockArt.Planks);
      Block("mc_craft", "Crafting Table", "Cannot craft, looks good.", SbBlockArt.CraftTop, SbBlockArt.CraftSide, SbBlockArt.Planks, SbBlockFx.None, SbBlockArt.CraftFront);
      Block("mc_furnace", "Furnace", "Lit and toasty.", SbBlockArt.FurnaceTop, SbBlockArt.FurnaceSide, SbBlockArt.FurnaceTop, SbBlockFx.None, SbBlockArt.FurnaceFront);
      Block("mc_tnt", "TNT", "Poke it, shoot it, or blow it up. 4 s fuse.", SbBlockArt.TntTop, SbBlockArt.TntSide, SbBlockArt.TntBottom, SbBlockFx.Tnt);
      Block("mc_obsidian", "Obsidian", "Blast proof. Made when water hits a lava source.", SbBlockArt.Obsidian);
      Block("mc_bedrock", "Bedrock", "Blast proof.", SbBlockArt.Bedrock);
      Block("mc_diamond", "Diamond Block", "Shiny.", SbBlockArt.Diamond);
      Block("mc_gold", "Gold Block", "Shiny.", SbBlockArt.Gold);
      Block("mc_iron", "Iron Block", "Solid metal.", SbBlockArt.Iron);
      Block("mc_emerald", "Emerald Block", "Villagers would love it.", SbBlockArt.Emerald);
      Block("mc_redstone", "Redstone Block", "Always powers the blocks and dust around it.", SbBlockArt.Redstone);
      Block("mc_lapis", "Lapis Block", "Deep blue.", SbBlockArt.Lapis);
      Block("mc_quartz", "Quartz Block", "Clean white.", SbBlockArt.Quartz);
      Block("mc_coalore", "Coal Ore", "Ore.", SbBlockArt.CoalOre);
      Block("mc_ironore", "Iron Ore", "Ore.", SbBlockArt.IronOre);
      Block("mc_goldore", "Gold Ore", "Ore.", SbBlockArt.GoldOre);
      Block("mc_diamondore", "Diamond Ore", "Ore.", SbBlockArt.DiamondOre);
      Block("mc_emeraldore", "Emerald Ore", "Ore.", SbBlockArt.EmeraldOre);
      Block("mc_glowstone", "Glowstone", "Glows.", SbBlockArt.Glowstone, SbBlockFx.Glow);
      Block("mc_sealantern", "Sea Lantern", "Glows.", SbBlockArt.SeaLantern, SbBlockFx.Glow);
      Block("mc_jack", "Jack o'Lantern", "Glows. Faces you.", SbBlockArt.PumpkinTop, SbBlockArt.PumpkinSide, SbBlockArt.PumpkinTop, SbBlockFx.Glow, SbBlockArt.JackFace);
      Block("mc_pumpkin", "Pumpkin", "Carved. Faces you.", SbBlockArt.PumpkinTop, SbBlockArt.PumpkinSide, SbBlockArt.PumpkinTop, SbBlockFx.None, SbBlockArt.PumpkinFace);
      Block("mc_melon", "Melon", "Juicy.", SbBlockArt.MelonTop, SbBlockArt.MelonSide, SbBlockArt.MelonTop);
      Block("mc_netherrack", "Netherrack", "From the Nether.", SbBlockArt.Netherrack);
      Block("mc_netherbricks", "Nether Bricks", "Fortress bricks.", SbBlockArt.NetherBricks);
      Block("mc_soulsand", "Soul Sand", "Spooky.", SbBlockArt.SoulSand);
      Block("mc_magma", "Magma Block", "Glows hot.", SbBlockArt.Magma, SbBlockFx.Magma);
      Block("mc_ice", "Ice", "Slippery in a modded lobby.", SbBlockArt.Ice, SbBlockFx.Ice);
      Block("mc_snow", "Snow Block", "Cold.", SbBlockArt.Snow);
      Block("mc_slime", "Slime Block", "Bouncy in a modded lobby.", SbBlockArt.Slime, SbBlockFx.Slime);
      Block("mc_sponge", "Sponge", "Soaks up water nearby.", SbBlockArt.Sponge, SbBlockFx.Sponge);
      Block("mc_hay", "Hay Bale", "Soft landing.", SbBlockArt.HayTop, SbBlockArt.HaySide, SbBlockArt.HayTop);
      Block("mc_wool_white", "White Wool", "Wool.", SbBlockArt.WoolWhite);
      Block("mc_wool_red", "Red Wool", "Wool.", SbBlockArt.WoolRed);
      Block("mc_wool_orange", "Orange Wool", "Wool.", SbBlockArt.WoolOrange);
      Block("mc_wool_yellow", "Yellow Wool", "Wool.", SbBlockArt.WoolYellow);
      Block("mc_wool_green", "Green Wool", "Wool.", SbBlockArt.WoolGreen);
      Block("mc_wool_cyan", "Cyan Wool", "Wool.", SbBlockArt.WoolCyan);
      Block("mc_wool_blue", "Blue Wool", "Wool.", SbBlockArt.WoolBlue);
      Block("mc_wool_purple", "Purple Wool", "Wool.", SbBlockArt.WoolPurple);
      Block("mc_wool_pink", "Pink Wool", "Wool.", SbBlockArt.WoolPink);
      Block("mc_wool_black", "Black Wool", "Wool.", SbBlockArt.WoolBlack);
      AddMoreBlocks();

      // Liquids
      Color iron = C("#c8c8c8"), ironDark = C("#7a7a7a"), outline = C("#2a2a2a");
      Add(new SbItem
      {
        Id = "mc_water", Name = "Water Bucket", Desc = "Places a water source. Flows like Minecraft water (7 blocks out, falls down). Swim in it.",
        Tab = SbTab.Minecraft, Section = "LIQUIDS", Kind = SbKind.Fluid, Fluid = SbFluid.Water,
        Build = k => Bucket(k, "item/water_bucket", C("#3f76e4"))
      });
      Add(new SbItem
      {
        Id = "mc_lava", Name = "Lava Bucket", Desc = "Places a lava source. Flows slowly (3 blocks out). Water + lava makes obsidian or cobblestone.",
        Tab = SbTab.Minecraft, Section = "LIQUIDS", Kind = SbKind.Fluid, Fluid = SbFluid.Lava,
        Build = k => Bucket(k, "item/lava_bucket", C("#ef6a10"))
      });

      // Flint and steel: lights TNT, fire and nether portals (inside an obsidian frame)
      // holdable too (EQUIP): touch something with it to set it on fire
      Add(new SbItem
      {
        Id = "mc_flint", Name = "Flint and Steel", Tab = SbTab.Minecraft, Section = "TOOLS", Kind = SbKind.Tool, Holdable = true, DefaultAnchored = true,
        Desc = "Equip it and touch something to set it on fire (or pull the trigger), or point and pull the trigger. Lights TNT, and fire inside an obsidian frame (2 wide x 3 tall inside or bigger) lights a Nether portal. Portals only take you to the Nether in a modded lobby or when everyone in the room has the SandBox.",
        Build = k =>
        {
          // held flat like Minecraft's item, the steel's striking edge at the tip
          Color32[] mc = SbMinecraft.Frame16("textures/item/flint_and_steel.png");
          if (mc != null) k.Pixels(mc, 0.012f, 0.012f, Quaternion.Euler(0f, 0f, 0f), new Vector3(-0.096f, -0.096f, 0f));
          else
          {
            k.Box(new Vector3(-0.03f, -0.02f, 0f), new Vector3(0.05f, 0.07f, 0.03f), C("#3a3a3a"));
            k.Sphere(new Vector3(-0.03f, 0.02f, 0f), new Vector3(0.03f, 0.025f, 0.018f), C("#555555"), 8);
            k.Torus(new Vector3(0.035f, 0.01f, 0f), 0.035f, 0.007f, Quaternion.Euler(90f, 0f, 0f), C("#bdbdbd"), 14, 4);
          }
          k.GripAt(new Vector3(0f, -0.06f, 0f)).MuzzleAt(new Vector3(0.05f, 0.07f, 0f));
        }
      });
      Add(new SbItem
      {
        Id = "mc_endereye", Name = "Eye of Ender", Tab = SbTab.Minecraft, Section = "TOOLS", Kind = SbKind.Tool, Holdable = true, DefaultAnchored = true,
        Desc = "Touch an End Portal Frame with it (equipped, or point and pull the trigger) to put an eye in. Fill all 12 frames of a ring (5 x 5, no corners, all facing in) and the End portal opens: jump in to go to the End.",
        Build = k =>
        {
          Color32[] mc = SbMinecraft.Frame16("textures/item/ender_eye.png");
          if (mc != null) k.Pixels(mc, 0.01f, 0.012f, Quaternion.identity, new Vector3(-0.08f, -0.08f, 0f));
          else
          {
            k.Ball(Vector3.zero, 0.05f, C("#1f6b4a"), 14);
            k.Sphere(new Vector3(0f, 0f, 0.035f), new Vector3(0.03f, 0.03f, 0.02f), C("#5fdc9a"), 12);
            k.Sphere(new Vector3(0f, 0f, 0.05f), new Vector3(0.012f, 0.02f, 0.008f), C("#0b1a14"), 8);
          }
          k.GripAt(new Vector3(0f, -0.04f, 0f)).MuzzleAt(new Vector3(0f, 0.06f, 0f));
        }
      });
      // ender pearls (SandBoxPearl.cs)
      Add(new SbItem
      {
        Id = "mc_enderpearl", Name = "Ender Pearl", Tab = SbTab.Minecraft, Section = "TOOLS", Kind = SbKind.Tool, Holdable = true, Pearl = true,
        DefaultAnchored = false, Mass = 0.3f, Col = SbCol.Sphere,
        Desc = "Throw it (let go, pull the trigger while holding it, or point and pull the trigger) and you teleport to where it lands. Only in a SandBox lobby where everyone has the SandBox.",
        Build = k =>
        {
          Color32[] mc = SbMinecraft.Frame16("textures/item/ender_pearl.png");
          if (mc != null) k.Pixels(mc, 0.009f, 0.009f, Quaternion.identity, new Vector3(-0.072f, -0.072f, 0f));
          else
          {
            k.Ball(Vector3.zero, 0.05f, C("#0b3d34"), 16);
            k.Sphere(new Vector3(0.012f, 0.015f, 0.03f), new Vector3(0.02f, 0.02f, 0.012f), C("#2f9a7a"), 10);
          }
          k.GripAt(Vector3.zero).MuzzleAt(new Vector3(0f, 0.05f, 0f));
        }
      });
      // the End
      Block("mc_endstone", "End Stone", "From the End.", SbBlockArt.EndStone);
      Block("mc_endframe", "End Portal Frame", "Put an Eye of Ender in each of 12 frames (a 5 x 5 ring without corners) to open an End portal.",
        SbBlockArt.EndFrameTop, SbBlockArt.EndFrameSide, SbBlockArt.EndStone);
      Add(new SbItem { Id = "mc_endframe_eye", Name = "End Portal Frame", Desc = "", Tab = SbTab.Minecraft, Section = "BLOCKS", Kind = SbKind.Block, Hidden = true,
        Top = SbBlockArt.EndFrameTop, Side = SbBlockArt.EndFrameSide, Bottom = SbBlockArt.EndStone, Front = SbBlockArt.EndFrameSide, Sound = "stone" });
      Add(new SbItem { Id = "mc_endportal", Name = "End Portal", Desc = "", Tab = SbTab.Minecraft, Section = "TOOLS", Kind = SbKind.Block, Hidden = true,
        Top = SbBlockArt.EndPortal, Side = SbBlockArt.EndPortal, Bottom = SbBlockArt.EndPortal, Front = SbBlockArt.EndPortal, BlockFx = SbBlockFx.EndPortal, Sound = "glass" });
      // made by the game, never in the menu
      Add(new SbItem { Id = "mc_portal", Name = "Nether Portal", Desc = "", Tab = SbTab.Minecraft, Section = "TOOLS", Kind = SbKind.Block, Hidden = true,
        Top = SbBlockArt.NetherPortal, Side = SbBlockArt.NetherPortal, Bottom = SbBlockArt.NetherPortal, Front = SbBlockArt.NetherPortal, BlockFx = SbBlockFx.Portal, Sound = "glass" });
      Add(new SbItem { Id = "mc_fire", Name = "Fire", Desc = "", Tab = SbTab.Minecraft, Section = "TOOLS", Kind = SbKind.Prop, Hidden = true, PropFx = SbPropFx.Fire,
        Build = k => k.Ball(Vector3.zero, 0.02f, C("#ff8a1a"), 6, true) });

      // Swords: 16 px pixel sprite extruded, blade along +Z
      Sword("mc_sword_wood", "Wooden Sword", C("#9c7a45"), C("#c19a5b"), C("#6b5230"), 1);
      Sword("mc_sword_stone", "Stone Sword", C("#8a8a8a"), C("#b0b0b0"), C("#5a5a5a"), 2);
      Sword("mc_sword_iron", "Iron Sword", C("#d8d8d8"), C("#ffffff"), C("#9a9a9a"), 3);
      Sword("mc_sword_gold", "Golden Sword", C("#f4d03f"), C("#fff3a0"), C("#c79a1a"), 3);
      Sword("mc_sword_diamond", "Diamond Sword", C("#4fe0d5"), C("#b7fff9"), C("#1f9c94"), 4);
      Sword("mc_sword_netherite", "Netherite Sword", C("#4a4048"), C("#6e6470"), C("#2a2228"), 5);
    }

    private static string McSword(string id)
    {
      switch (id)
      {
        case "mc_sword_wood": return "item/wooden_sword";
        case "mc_sword_stone": return "item/stone_sword";
        case "mc_sword_iron": return "item/iron_sword";
        case "mc_sword_gold": return "item/golden_sword";
        case "mc_sword_diamond": return "item/diamond_sword";
        default: return "item/netherite_sword";
      }
    }

    private static void Bucket(SbMeshKit k, string mcItem, Color fill)
    {
      Color iron = C("#c8c8c8"), ironDark = C("#7a7a7a"), outline = C("#2a2a2a");
      Color32[] mc = SbMinecraft.Frame16("textures/" + mcItem + ".png");
      if (mc != null) k.Pixels(mc, 0.03f, 0.03f, Quaternion.identity, new Vector3(-0.24f, -0.24f, 0f));
      else Voxels(k, BucketSprite(), new Dictionary<char, Color> { { 'o', outline }, { 'i', iron }, { 'h', ironDark }, { 'f', fill } }, 0.03f, 0.03f, Quaternion.identity, new Vector3(-0.24f, -0.24f, 0f));
    }

    private static void Sword(string id, string name, Color blade, Color hi, Color edge, int tier)
    {
      Add(new SbItem
      {
        Id = id, Name = name, Desc = "Grab it with grip and swing. Knocks objects (and, in modded lobbies, Unknowns players).",
        Tab = SbTab.Minecraft, Section = "SWORDS", Kind = SbKind.Melee, MeleeTier = tier, DefaultAnchored = true, Holdable = true,
        Build = k =>
        {
          var pal = new Dictionary<char, Color>
          {
            { 'b', blade }, { 'h', hi }, { 'e', edge }, { 'g', C("#4a3a2a") }, { 'w', C("#7a5a34") }, { 'd', C("#54391f") }, { 'o', C("#1e1a16") }
          };
          float px = 0.045f;
          // blade axis (pixel +x, +y) -> model +Z, sprite plane -> YZ plane (flat side faces X)
          Quaternion rot = Quaternion.AngleAxis(90f, Vector3.forward) * Quaternion.Euler(90f, 0f, 0f) * Quaternion.Euler(0f, 0f, -45f);
          // grip pixel: handle centre around c=2.5, r=12.5 -> sprite pos (2.5+0.5, 16-12.5-0.5)
          Vector3 gripLocal = new Vector3(3f * px, 3f * px, 0f);
          Vector3 origin = -(rot * gripLocal);
          Color32[] mc = SbMinecraft.Frame16("textures/" + McSword(id) + ".png");
          if (mc != null) k.Pixels(mc, px, px, rot, origin);
          else Voxels(k, SwordSprite(), pal, px, px, rot, origin);
          k.GripAt(Vector3.zero);
          k.MuzzleAt(rot * new Vector3(15f * px, 15f * px, 0f) + origin);
        }
      });
    }

    // ---------------------------------------------------------------- objects
    private static void Prop(string id, string name, string desc, string section, Action<SbMeshKit> build, SbCol col = SbCol.Box, bool anchored = true, SbPropFx fx = SbPropFx.None, float mass = 1f, float bounce = 0.1f)
    {
      Add(new SbItem { Id = id, Name = name, Desc = desc, Tab = SbTab.Objects, Section = section, Kind = SbKind.Prop, Build = build, Col = col, DefaultAnchored = anchored, PropFx = fx, Mass = mass, Bounce = bounce });
    }

    private static void AddObjects()
    {
      // the weld tool (SandBoxAttach.cs)
      Add(new SbItem
      {
        Id = "ob_weld", Name = "Weld Tool", Tab = SbTab.Objects, Section = "MACHINES", Kind = SbKind.Tool,
        Desc = "Point at one of your objects and pull the trigger, then at another: the second is welded onto the first and they move as one. Do it on two welded pieces to weld them apart.",
        Build = k =>
        {
          Color body = C("#2b2d31"), steel = C("#9aa0a8"), red = C("#c0281e");
          k.Cyl(new Vector3(0f, 0f, -0.04f), 0.025f, 0.16f, Quaternion.Euler(90f, 0f, 0f), body, 18);
          for (int i = 0; i < 5; i++) k.Torus(new Vector3(0f, 0f, -0.1f + i * 0.025f), 0.026f, 0.003f, Quaternion.Euler(90f, 0f, 0f), C("#151517"), 18, 4);
          k.Cyl(new Vector3(0f, 0f, 0.06f), 0.03f, 0.04f, Quaternion.Euler(90f, 0f, 0f), steel, 18);
          k.Cyl(new Vector3(0f, -0.01f, 0.13f), 0.008f, 0.1f, Quaternion.Euler(70f, 0f, 0f), C("#c9a24a"), 10);
          k.Frustum(new Vector3(0f, -0.03f, 0.185f), 0.012f, 0.008f, 0.03f, Quaternion.Euler(70f, 0f, 0f), C("#c9a24a"), 12);
          k.Ball(new Vector3(0f, -0.036f, 0.2f), 0.006f, C("#7fd4ff"), 8, true);
          k.Box(new Vector3(0f, -0.04f, -0.02f), new Vector3(0.012f, 0.03f, 0.02f), red);
          k.Cyl(new Vector3(0f, 0f, -0.14f), 0.006f, 0.05f, Quaternion.Euler(90f, 0f, 0f), C("#151517"), 8);
        }
      });
      // drivable cars (SandBoxVehicles.cs)
      // the Roblox obby car replaces the go-kart and buggy
      SbVehicleSpec obby = SbVehicleArt.ObbyCar();
      Add(new SbItem
      {
        Id = "ob_obbycar", Name = "Obby Car", Tab = SbTab.Objects, Section = "VEHICLES", Kind = SbKind.Prop, Vehicle = obby,
        Desc = "The Roblox obby car (the pink pickup from Obby But You're In a Car): ride in the bed. Climbs steps, kerbs and ramps and turns itself back over. Grip the steering wheel to get in. LEFT STICK: gas and steering, B / Y: get out. In modded lobbies or when everyone has the SandBox.",
        Col = SbCol.Box, DefaultAnchored = false, Mass = obby.Mass, Build = k => SbVehicleArt.Full(k, obby)
      });
      Color concrete = C("#b9bcc2"), neon = C("#38e8ff");
      Prop("ob_cube", "Cube", "A plain block.", "BUILDING", k => k.Box(Vector3.zero, Vector3.one * 0.5f, concrete));
      Prop("ob_sphere", "Sphere", "A ball. Turn anchor off to let it roll.", "BUILDING", k => k.Ball(Vector3.zero, 0.25f, concrete, 18), SbCol.Sphere);
      Prop("ob_cylinder", "Cylinder", "A round pillar.", "BUILDING", k => k.Cyl(Vector3.zero, 0.25f, 0.5f, Quaternion.identity, concrete, 18), SbCol.Mesh);
      Prop("ob_ramp", "Ramp", "Slide down it.", "BUILDING", k => k.Wedge(Vector3.zero, new Vector3(0.8f, 0.5f, 1.0f), Quaternion.identity, concrete), SbCol.Mesh);
      Prop("ob_stairs", "Stairs", "Four steps.", "BUILDING", k =>
      {
        for (int i = 0; i < 4; i++)
        {
          float h = (i + 1) * 0.125f;
          k.Box(new Vector3(0f, -0.25f + h * 0.5f, 0.1875f - i * 0.125f), new Vector3(0.8f, h, 0.125f), i % 2 == 0 ? concrete : C("#a8abb2"));
        }
      });
      Prop("ob_platform", "Platform", "A wide flat floor.", "BUILDING", k =>
      {
        k.Box(Vector3.zero, new Vector3(1.6f, 0.08f, 1.6f), C("#7f8794"));
        k.Box(new Vector3(0f, 0.041f, 0f), new Vector3(1.5f, 0.002f, 1.5f), C("#5d6470"));
      });
      Prop("ob_wall", "Wall", "Tall and flat.", "BUILDING", k => k.Box(Vector3.zero, new Vector3(1.6f, 1.2f, 0.1f), C("#a9adb5")));
      Prop("ob_pillar", "Pillar", "Classic column.", "BUILDING", k =>
      {
        k.Box(new Vector3(0f, -0.72f, 0f), new Vector3(0.36f, 0.08f, 0.36f), C("#e4ded2"));
        k.Cyl(Vector3.zero, 0.13f, 1.36f, Quaternion.identity, C("#ece6da"), 16);
        k.Box(new Vector3(0f, 0.72f, 0f), new Vector3(0.36f, 0.08f, 0.36f), C("#e4ded2"));
      });
      Prop("ob_arch", "Arch", "Walk through it.", "BUILDING", k =>
      {
        Color c = C("#c9b79a");
        k.Box(new Vector3(-0.5f, 0f, 0f), new Vector3(0.2f, 1.2f, 0.25f), c);
        k.Box(new Vector3(0.5f, 0f, 0f), new Vector3(0.2f, 1.2f, 0.25f), c);
        k.Box(new Vector3(0f, 0.7f, 0f), new Vector3(1.2f, 0.2f, 0.25f), c);
      });
      Prop("ob_cone", "Cone", "Pointy.", "BUILDING", k => k.Cone(Vector3.zero, 0.25f, 0.5f, Quaternion.identity, concrete, 18), SbCol.Mesh);
      Prop("ob_ring", "Ring", "A big hoop to fly through.", "BUILDING", k => k.Torus(Vector3.zero, 0.6f, 0.06f, Quaternion.Euler(90f, 0f, 0f), neon, 28, 8, true), SbCol.Mesh, true, SbPropFx.Light);
      Prop("ob_neonbar", "Neon Bar", "A glowing light strip.", "BUILDING", k => k.Box(Vector3.zero, new Vector3(1f, 0.05f, 0.05f), C("#ff3df5"), true), SbCol.Box, true, SbPropFx.Light);

      Prop("ob_beachball", "Beach Ball", "Light and bouncy.", "FUN", k =>
      {
        Color[] cs = { C("#ff4b4b"), C("#ffffff"), C("#3d8bff"), C("#ffd83d"), C("#ffffff"), C("#3ddc6b") };
        for (int i = 0; i < 6; i++) k.Lune(Vector3.zero, 0.3f, i * 60f, (i + 1) * 60f, cs[i], 4, 12);
        k.Sphere(new Vector3(0f, 0.29f, 0f), new Vector3(0.05f, 0.02f, 0.05f), C("#ffffff"), 8);
        k.Sphere(new Vector3(0f, -0.29f, 0f), new Vector3(0.05f, 0.02f, 0.05f), C("#ffffff"), 8);
      }, SbCol.Sphere, false, SbPropFx.Bouncy, 0.15f, 0.85f);
      Prop("ob_bouncyball", "Bouncy Ball", "Super bouncy.", "FUN", k => k.Ball(Vector3.zero, 0.12f, C("#ff2d7a"), 14), SbCol.Sphere, false, SbPropFx.Bouncy, 0.2f, 0.95f);
      Prop("ob_basketball", "Basketball", "Bounces.", "FUN", k =>
      {
        k.Ball(Vector3.zero, 0.15f, C("#e8752a"), 16);
        k.Torus(Vector3.zero, 0.151f, 0.006f, Quaternion.identity, C("#2a1a10"), 20, 4);
        k.Torus(Vector3.zero, 0.151f, 0.006f, Quaternion.Euler(90f, 0f, 0f), C("#2a1a10"), 20, 4);
      }, SbCol.Sphere, false, SbPropFx.Bouncy, 0.6f, 0.75f);
      Prop("ob_trampoline", "Trampoline", "Big jumps in a modded lobby.", "FUN", k =>
      {
        k.Cyl(new Vector3(0f, 0.12f, 0f), 0.75f, 0.04f, Quaternion.identity, C("#202020"), 24);
        k.Torus(new Vector3(0f, 0.14f, 0f), 0.78f, 0.05f, Quaternion.identity, C("#2d7cff"), 28, 8);
        for (int i = 0; i < 6; i++)
        {
          float a = i * 60f * Mathf.Deg2Rad;
          k.Cyl(new Vector3(Mathf.Cos(a) * 0.72f, -0.05f, Mathf.Sin(a) * 0.72f), 0.03f, 0.34f, Quaternion.identity, C("#9aa0a8"), 8);
        }
      }, SbCol.Mesh, true, SbPropFx.Trampoline);
      Prop("ob_bouncepad", "Bounce Pad", "Glowing launch pad.", "FUN", k =>
      {
        k.Cyl(Vector3.zero, 0.4f, 0.08f, Quaternion.identity, C("#2a2f3a"), 24);
        k.Cyl(new Vector3(0f, 0.045f, 0f), 0.3f, 0.02f, Quaternion.identity, C("#5dff7a"), 24, true);
        k.Cone(new Vector3(0f, 0.08f, 0f), 0.12f, 0.06f, Quaternion.identity, C("#d0ffd8"), 12, true);
      }, SbCol.Mesh, true, SbPropFx.Trampoline);
      Prop("ob_balloon", "Balloon", "Floats away unless anchored.", "FUN", k =>
      {
        k.Sphere(new Vector3(0f, 0.2f, 0f), new Vector3(0.2f, 0.25f, 0.2f), C("#ff3b5c"), 14);
        k.Cone(new Vector3(0f, -0.07f, 0f), 0.03f, 0.04f, Quaternion.Euler(180f, 0f, 0f), C("#d82a46"), 8);
        k.Cyl(new Vector3(0f, -0.35f, 0f), 0.004f, 0.5f, Quaternion.identity, C("#f0f0f0"), 4);
      }, SbCol.Sphere, false, SbPropFx.Balloon, 0.05f, 0.3f);
      SbItem balloon = All.Find(i => i.Id == "ob_balloon");
      if (balloon != null)
      {
        balloon.Attach = SbAttach.Balloon;
        balloon.Desc = "Floats away unless anchored. Place it on one of your objects to tie it on: enough balloons make it fly.";
      }

      // machines: place them on one of your objects and they stick to it
      Add(new SbItem
      {
        Id = "ob_thruster", Name = "Thruster", Tab = SbTab.Objects, Section = "MACHINES", Kind = SbKind.Prop, Attach = SbAttach.Thruster,
        Desc = "Place it on one of your objects: it pushes the object away from the nozzle. Touch it to switch it on or off.",
        Col = SbCol.Box, DefaultAnchored = true, Mass = 2f,
        Build = k =>
        {
          // along +Z: the mounting plate at +Z (against the object), the nozzle at -Z
          Color metal = C("#5c6370"), dark = C("#2b2f36"), plate = C("#9aa1ab"), stripe = C("#ffcc33");
          k.Box(new Vector3(0f, 0f, 0.165f), new Vector3(0.24f, 0.24f, 0.04f), plate);
          for (int i = 0; i < 4; i++)
          {
            float a = (45f + i * 90f) * Mathf.Deg2Rad;
            k.Cyl(new Vector3(Mathf.Cos(a) * 0.09f, Mathf.Sin(a) * 0.09f, 0.19f), 0.012f, 0.012f, Quaternion.Euler(90f, 0f, 0f), dark, 8);
          }
          k.Tube(new Vector3(0f, 0f, 0.03f), 0.085f, 0.24f, metal, 18);
          k.Torus(new Vector3(0f, 0f, 0.09f), 0.087f, 0.008f, Quaternion.Euler(90f, 0f, 0f), stripe, 20, 6);
          k.Torus(new Vector3(0f, 0f, -0.03f), 0.087f, 0.008f, Quaternion.Euler(90f, 0f, 0f), stripe, 20, 6);
          k.Frustum(new Vector3(0f, 0f, -0.14f), 0.07f, 0.1f, 0.1f, Quaternion.Euler(-90f, 0f, 0f), dark, 18, false, false);
          k.Torus(new Vector3(0f, 0f, -0.185f), 0.098f, 0.01f, Quaternion.Euler(90f, 0f, 0f), metal, 20, 6);
          k.Cyl(new Vector3(0f, 0f, -0.12f), 0.06f, 0.01f, Quaternion.Euler(90f, 0f, 0f), C("#ff8a2a"), 18, true);
        }
      });
      Add(new SbItem
      {
        Id = "ob_wheel", Name = "Wheel", Tab = SbTab.Objects, Section = "MACHINES", Kind = SbKind.Prop, Attach = SbAttach.Wheel,
        Desc = "Place it on the side of one of your objects: it drives the object along the ground. Put one on each side for a car. Touch it to switch it on or off.",
        Col = SbCol.Sphere, DefaultAnchored = true, Mass = 3f,
        Build = k =>
        {
          // the axle along X (towards the object it is on)
          Color rubber = C("#1e1e20"), rim = C("#c9ced6"), hub = C("#3a3f46");
          Quaternion side = Quaternion.Euler(0f, 0f, 90f);
          k.Torus(Vector3.zero, 0.16f, 0.06f, side, rubber, 28, 10);
          for (int i = 0; i < 16; i++)
          {
            float a = i / 16f * Mathf.PI * 2f;
            k.Box(new Vector3(0f, Mathf.Cos(a) * 0.218f, Mathf.Sin(a) * 0.218f), new Vector3(0.1f, 0.018f, 0.03f), Quaternion.Euler(a * Mathf.Rad2Deg, 0f, 0f), rubber);
          }
          k.Cyl(Vector3.zero, 0.115f, 0.07f, side, rim, 24);
          k.Cyl(Vector3.zero, 0.045f, 0.1f, side, hub, 16);
          for (int i = 0; i < 5; i++)
          {
            float a = i / 5f * 360f;
            k.Box(new Vector3(0.036f, 0f, 0f), new Vector3(0.012f, 0.2f, 0.022f), Quaternion.Euler(a, 0f, 0f), hub);
          }
        }
      });
      Prop("ob_disco", "Disco Ball", "Spins and sparkles.", "FUN", k =>
      {
        for (int y = 0; y < 8; y++)
          for (int x = 0; x < 14; x++)
          {
            float lat = (y + 0.5f) / 8f * Mathf.PI - Mathf.PI * 0.5f, lon = x / 14f * Mathf.PI * 2f;
            Vector3 n = new Vector3(Mathf.Cos(lat) * Mathf.Cos(lon), Mathf.Sin(lat), Mathf.Cos(lat) * Mathf.Sin(lon));
            float shade = ((x + y) % 3 == 0) ? 1f : ((x * 7 + y) % 4 == 0 ? 0.55f : 0.8f);
            k.Box(n * 0.25f, new Vector3(0.1f * Mathf.Cos(lat) + 0.02f, 0.09f, 0.02f), Quaternion.LookRotation(n), new Color(shade, shade, shade * 1.05f), (x + y) % 5 == 0);
          }
        k.Cyl(new Vector3(0f, 0.4f, 0f), 0.006f, 0.3f, Quaternion.identity, C("#888888"), 4);
      }, SbCol.Sphere, true, SbPropFx.Spin);
      Prop("ob_duck", "Rubber Duck", "Squeak.", "FUN", k =>
      {
        Color y = C("#ffd21f");
        k.Sphere(Vector3.zero, new Vector3(0.16f, 0.12f, 0.2f), y, 14);
        k.Sphere(new Vector3(0f, 0.06f, -0.17f), new Vector3(0.07f, 0.06f, 0.08f), Quaternion.Euler(-35f, 0f, 0f), y, 10);
        k.Ball(new Vector3(0f, 0.17f, 0.09f), 0.095f, y, 14);
        k.Sphere(new Vector3(0f, 0.15f, 0.2f), new Vector3(0.055f, 0.022f, 0.06f), C("#ff8a1f"), 10);
        k.Ball(new Vector3(0.05f, 0.2f, 0.165f), 0.016f, C("#111111"), 6);
        k.Ball(new Vector3(-0.05f, 0.2f, 0.165f), 0.016f, C("#111111"), 6);
        k.Sphere(new Vector3(0.13f, 0.02f, -0.01f), new Vector3(0.03f, 0.06f, 0.1f), Quaternion.Euler(0f, 0f, -20f), C("#f0c010"), 8);
        k.Sphere(new Vector3(-0.13f, 0.02f, -0.01f), new Vector3(0.03f, 0.06f, 0.1f), Quaternion.Euler(0f, 0f, 20f), C("#f0c010"), 8);
      }, SbCol.Box, false, SbPropFx.None, 0.2f, 0.5f);
      Prop("ob_banana", "Banana", "Monke snack.", "FUN", k =>
      {
        for (int i = 0; i < 7; i++)
        {
          float t = (i - 3) / 3f;
          k.Sphere(new Vector3(0f, t * t * 0.09f, t * 0.15f), new Vector3(0.045f, 0.045f, 0.04f), C(i == 0 || i == 6 ? "#7a5a20" : "#ffe14d"), 8);
        }
      }, SbCol.Box, false, SbPropFx.None, 0.2f, 0.3f);
      Add(new SbItem
      {
        Id = "ob_ragdoll", Name = "Gorilla Ragdoll", Desc = "A floppy Gorilla Tag gorilla that looks like you (or the player who spawned it). Grab it, throw it, blow it up.",
        Tab = SbTab.Objects, Section = "FUN", Kind = SbKind.Prop, Ragdoll = true, DefaultAnchored = false, Mass = 3f, Col = SbCol.Box,
        Build = k =>
        {
          Color fur = C("#7a4a2a"), face = C("#d8b48a");
          k.Sphere(new Vector3(0f, 0f, 0f), new Vector3(0.22f, 0.26f, 0.18f), fur, 14);
          k.Sphere(new Vector3(0f, 0.36f, 0.02f), new Vector3(0.16f, 0.15f, 0.15f), fur, 14);
          k.Sphere(new Vector3(0f, 0.34f, 0.13f), new Vector3(0.1f, 0.08f, 0.05f), face, 10);
          k.Ball(new Vector3(0.05f, 0.39f, 0.15f), 0.02f, C("#111111"), 6);
          k.Ball(new Vector3(-0.05f, 0.39f, 0.15f), 0.02f, C("#111111"), 6);
          k.Cyl(new Vector3(0.3f, 0.02f, 0.05f), 0.055f, 0.5f, Quaternion.Euler(0f, 0f, 55f), fur, 8);
          k.Cyl(new Vector3(-0.3f, 0.02f, 0.05f), 0.055f, 0.5f, Quaternion.Euler(0f, 0f, -55f), fur, 8);
        }
      });
      Prop("ob_cone_traffic", "Traffic Cone", "Orange and white.", "FUN", k =>
      {
        k.Box(new Vector3(0f, -0.26f, 0f), new Vector3(0.36f, 0.04f, 0.36f), C("#ff6a00"));
        k.Frustum(new Vector3(0f, -0.10f, 0f), 0.13f, 0.10f, 0.28f, Quaternion.identity, C("#ff6a00"), 14);
        k.Frustum(new Vector3(0f, 0.09f, 0f), 0.10f, 0.075f, 0.10f, Quaternion.identity, C("#f5f5f5"), 14);
        k.Frustum(new Vector3(0f, 0.21f, 0f), 0.075f, 0.02f, 0.14f, Quaternion.identity, C("#ff6a00"), 14);
      }, SbCol.Box, false, SbPropFx.None, 0.6f);
      Prop("ob_crate", "Crate", "Wooden crate.", "FUN", k =>
      {
        Color w = C("#a77d43"), d = C("#7a5a2e");
        k.Box(Vector3.zero, Vector3.one * 0.5f, w);
        for (int s = -1; s <= 1; s += 2)
        {
          k.Box(new Vector3(0f, s * 0.22f, 0.252f), new Vector3(0.5f, 0.06f, 0.01f), d);
          k.Box(new Vector3(0f, s * 0.22f, -0.252f), new Vector3(0.5f, 0.06f, 0.01f), d);
          k.Box(new Vector3(0.252f, s * 0.22f, 0f), new Vector3(0.01f, 0.06f, 0.5f), d);
          k.Box(new Vector3(-0.252f, s * 0.22f, 0f), new Vector3(0.01f, 0.06f, 0.5f), d);
        }
        k.Box(new Vector3(0f, 0f, 0.253f), new Vector3(0.62f, 0.06f, 0.01f), Quaternion.Euler(0f, 0f, 45f), d);
        k.Box(new Vector3(0f, 0f, -0.253f), new Vector3(0.62f, 0.06f, 0.01f), Quaternion.Euler(0f, 0f, -45f), d);
      }, SbCol.Box, false, SbPropFx.None, 2f);
      Prop("ob_barrel", "Barrel", "Rolls if knocked over.", "FUN", k =>
      {
        k.Cyl(Vector3.zero, 0.22f, 0.6f, Quaternion.identity, C("#8a5a2a"), 16);
        k.Torus(new Vector3(0f, 0.2f, 0f), 0.222f, 0.012f, Quaternion.identity, C("#3a3a3a"), 18, 4);
        k.Torus(new Vector3(0f, -0.2f, 0f), 0.222f, 0.012f, Quaternion.identity, C("#3a3a3a"), 18, 4);
      }, SbCol.Mesh, false, SbPropFx.None, 3f);
      Prop("ob_tire", "Tire", "Rubber.", "FUN", k =>
      {
        k.Torus(Vector3.zero, 0.24f, 0.1f, Quaternion.Euler(90f, 0f, 0f), C("#1b1b1b"), 22, 10);
        k.Cyl(Vector3.zero, 0.15f, 0.08f, Quaternion.Euler(90f, 0f, 0f), C("#a0a0a0"), 14);
      }, SbCol.Mesh, false, SbPropFx.Bouncy, 1f, 0.6f);
      Prop("ob_lamp", "Lamp Post", "Lights up the night.", "FUN", k =>
      {
        Color m = C("#2b2f36");
        k.Cyl(new Vector3(0f, -0.9f, 0f), 0.12f, 0.08f, Quaternion.identity, m, 12);
        k.Cyl(Vector3.zero, 0.035f, 1.8f, Quaternion.identity, m, 10);
        k.Box(new Vector3(0f, 0.95f, 0f), new Vector3(0.22f, 0.22f, 0.22f), C("#ffe9a8"), true);
        k.Cone(new Vector3(0f, 1.12f, 0f), 0.2f, 0.12f, Quaternion.identity, m, 4);
      }, SbCol.Box, true, SbPropFx.Light);
      Prop("ob_campfire", "Campfire", "Crackles and glows.", "FUN", k =>
      {
        for (int i = 0; i < 5; i++)
          k.Cyl(new Vector3(0f, -0.05f, 0f), 0.04f, 0.5f, Quaternion.Euler(80f, i * 36f, 0f), C("#5a3a1e"), 8);
        for (int i = 0; i < 8; i++)
        {
          float a = i * 45f * Mathf.Deg2Rad;
          k.Sphere(new Vector3(Mathf.Cos(a) * 0.3f, -0.08f, Mathf.Sin(a) * 0.3f), new Vector3(0.07f, 0.05f, 0.07f), C("#6e6e6e"), 8);
        }
        k.Cone(new Vector3(0f, 0.1f, 0f), 0.13f, 0.32f, Quaternion.identity, C("#ff8a1a"), 10, true);
        k.Cone(new Vector3(0f, 0.08f, 0f), 0.07f, 0.24f, Quaternion.identity, C("#ffe46b"), 10, true);
      }, SbCol.Box, true, SbPropFx.Fire);
      Prop("ob_tree", "Tree", "Blocky tree.", "FUN", k =>
      {
        k.Box(new Vector3(0f, -0.2f, 0f), new Vector3(0.22f, 1.1f, 0.22f), C("#6b4b2b"));
        k.Box(new Vector3(0f, 0.55f, 0f), new Vector3(0.9f, 0.6f, 0.9f), C("#3f8f2f"));
        k.Box(new Vector3(0f, 0.95f, 0f), new Vector3(0.5f, 0.3f, 0.5f), C("#4aa337"));
      });
      Prop("ob_rock", "Rock", "Just a rock.", "FUN", k =>
      {
        k.Sphere(Vector3.zero, new Vector3(0.35f, 0.22f, 0.3f), C("#7b7b80"), 7);
        k.Sphere(new Vector3(0.15f, 0.1f, 0.05f), new Vector3(0.18f, 0.14f, 0.16f), C("#8a8a90"), 6);
      }, SbCol.Box, false, SbPropFx.None, 4f);
      Prop("ob_snowman", "Snowman", "Do you wanna build one?", "FUN", k =>
      {
        Color s = C("#f4f8ff");
        k.Ball(new Vector3(0f, -0.25f, 0f), 0.28f, s, 14);
        k.Ball(new Vector3(0f, 0.14f, 0f), 0.2f, s, 14);
        k.Ball(new Vector3(0f, 0.43f, 0f), 0.14f, s, 12);
        k.Cone(new Vector3(0f, 0.43f, 0.18f), 0.025f, 0.12f, Quaternion.Euler(90f, 0f, 0f), C("#ff7a1a"), 8);
        k.Ball(new Vector3(0.05f, 0.48f, 0.12f), 0.015f, C("#111111"), 6);
        k.Ball(new Vector3(-0.05f, 0.48f, 0.12f), 0.015f, C("#111111"), 6);
        k.Cyl(new Vector3(0f, 0.6f, 0f), 0.1f, 0.14f, Quaternion.identity, C("#1a1a1a"), 12);
        k.Cyl(new Vector3(0f, 0.53f, 0f), 0.15f, 0.02f, Quaternion.identity, C("#1a1a1a"), 12);
      });
      Prop("ob_chair", "Chair", "Take a seat.", "FUN", k =>
      {
        Color w = C("#b07a45");
        k.Box(new Vector3(0f, 0f, 0f), new Vector3(0.45f, 0.05f, 0.45f), w);
        k.Box(new Vector3(0f, 0.28f, -0.2f), new Vector3(0.45f, 0.5f, 0.05f), w);
        for (int x = -1; x <= 1; x += 2) for (int z = -1; z <= 1; z += 2)
            k.Box(new Vector3(x * 0.19f, -0.22f, z * 0.19f), new Vector3(0.05f, 0.42f, 0.05f), w);
      }, SbCol.Box, false, SbPropFx.None, 2f);
      Prop("ob_table", "Table", "Flat top.", "FUN", k =>
      {
        Color w = C("#8c5a30");
        k.Box(new Vector3(0f, 0.2f, 0f), new Vector3(1.0f, 0.06f, 0.6f), w);
        for (int x = -1; x <= 1; x += 2) for (int z = -1; z <= 1; z += 2)
            k.Box(new Vector3(x * 0.44f, -0.1f, z * 0.24f), new Vector3(0.06f, 0.56f, 0.06f), w);
      }, SbCol.Box, false, SbPropFx.None, 4f);
      Prop("ob_tv", "Old TV", "Static included.", "FUN", k =>
      {
        k.Box(Vector3.zero, new Vector3(0.6f, 0.45f, 0.4f), C("#4a3a2a"));
        k.Box(new Vector3(-0.05f, 0f, 0.201f), new Vector3(0.4f, 0.32f, 0.01f), C("#7fb0d8"), true);
        k.Ball(new Vector3(0.22f, 0.08f, 0.205f), 0.025f, C("#c0c0c0"), 6);
        k.Ball(new Vector3(0.22f, -0.04f, 0.205f), 0.025f, C("#c0c0c0"), 6);
        k.Cyl(new Vector3(-0.08f, 0.35f, 0f), 0.006f, 0.35f, Quaternion.Euler(0f, 0f, 30f), C("#bbbbbb"), 4);
        k.Cyl(new Vector3(0.08f, 0.35f, 0f), 0.006f, 0.35f, Quaternion.Euler(0f, 0f, -30f), C("#bbbbbb"), 4);
      }, SbCol.Box, false, SbPropFx.None, 3f);
      Prop("ob_arcade", "Arcade Machine", "Insert coin.", "FUN", k =>
      {
        Color body = C("#2a1b5a");
        k.Box(new Vector3(0f, 0f, 0f), new Vector3(0.6f, 1.6f, 0.6f), body);
        k.Box(new Vector3(0f, 0.3f, 0.26f), new Vector3(0.48f, 0.36f, 0.1f), Quaternion.Euler(-12f, 0f, 0f), C("#29e3ff"), true);
        k.Box(new Vector3(0f, -0.02f, 0.35f), new Vector3(0.56f, 0.06f, 0.2f), C("#1a1a1a"));
        k.Ball(new Vector3(-0.12f, 0.04f, 0.38f), 0.03f, C("#ff2d55"), 8);
        k.Ball(new Vector3(0.05f, 0.03f, 0.38f), 0.022f, C("#ffe02d"), 8);
        k.Ball(new Vector3(0.13f, 0.03f, 0.38f), 0.022f, C("#2dff6a"), 8);
        k.Box(new Vector3(0f, 0.72f, 0.2f), new Vector3(0.58f, 0.14f, 0.22f), C("#ff3df5"), true);
      }, SbCol.Box, true, SbPropFx.Light);
      Prop("ob_couch", "Couch", "Comfy.", "FUN", k =>
      {
        Color f = C("#b8423a");
        k.Box(new Vector3(0f, -0.1f, 0f), new Vector3(1.4f, 0.3f, 0.7f), f);
        k.Box(new Vector3(0f, 0.2f, -0.28f), new Vector3(1.4f, 0.4f, 0.15f), f);
        k.Box(new Vector3(-0.65f, 0.05f, 0f), new Vector3(0.15f, 0.3f, 0.7f), f);
        k.Box(new Vector3(0.65f, 0.05f, 0f), new Vector3(0.15f, 0.3f, 0.7f), f);
        k.Box(new Vector3(-0.3f, 0.07f, 0.05f), new Vector3(0.56f, 0.06f, 0.5f), C("#c9554c"));
        k.Box(new Vector3(0.3f, 0.07f, 0.05f), new Vector3(0.56f, 0.06f, 0.5f), C("#c9554c"));
      }, SbCol.Box, true);
      Prop("ob_toilet", "Toilet", "Why not.", "FUN", k =>
      {
        Color w = C("#f2f4f7");
        k.Frustum(new Vector3(0f, -0.15f, 0.05f), 0.13f, 0.18f, 0.3f, Quaternion.identity, w, 14);
        k.Torus(new Vector3(0f, 0.02f, 0.06f), 0.16f, 0.035f, Quaternion.identity, w, 18, 6);
        k.Box(new Vector3(0f, 0.2f, -0.17f), new Vector3(0.36f, 0.4f, 0.14f), w);
        k.Box(new Vector3(0.1f, 0.38f, -0.09f), new Vector3(0.06f, 0.02f, 0.03f), C("#b0b0b0"));
      }, SbCol.Box, false, SbPropFx.None, 3f);
      Prop("ob_present", "Present", "A gift box.", "FUN", k =>
      {
        k.Box(Vector3.zero, Vector3.one * 0.4f, C("#e53b5c"));
        k.Box(Vector3.zero, new Vector3(0.41f, 0.41f, 0.08f), C("#ffd83d"));
        k.Box(Vector3.zero, new Vector3(0.08f, 0.41f, 0.41f), C("#ffd83d"));
        k.Torus(new Vector3(0.06f, 0.24f, 0f), 0.06f, 0.015f, Quaternion.Euler(0f, 0f, 70f), C("#ffd83d"), 12, 4);
        k.Torus(new Vector3(-0.06f, 0.24f, 0f), 0.06f, 0.015f, Quaternion.Euler(0f, 0f, -70f), C("#ffd83d"), 12, 4);
      }, SbCol.Box, false, SbPropFx.None, 0.8f);
    }

    // ---------------------------------------------------------------- weapons
    private static void Gun(string id, string name, string desc, string section, SbAmmo ammo, float rate, bool auto, Action<SbMeshKit> build, int pellets = 1, float spread = 0.6f, float speed = 60f)
    {
      Add(new SbItem { Id = id, Name = name, Desc = desc, Tab = SbTab.Weapons, Section = section, Kind = SbKind.Gun, Ammo = ammo, FireRate = rate, Auto = auto, Build = build, Pellets = pellets, Spread = spread, Speed = speed, Holdable = true, DefaultAnchored = true, Mass = 1.5f });
    }

    private static void Bomb(string id, string name, string desc, SbBlast blast, float radius, float fuse, bool holdable, Action<SbMeshKit> build, SbCol col = SbCol.Box)
    {
      Add(new SbItem { Id = id, Name = name, Desc = desc, Tab = SbTab.Weapons, Section = "BOMBS & NUKES", Kind = SbKind.Explosive, Blast = blast, Radius = radius, Fuse = fuse, Holdable = holdable, Build = build, Col = col, DefaultAnchored = !holdable, Mass = 1f });
    }

    private static void AddWeapons()
    {
      Color gunMetal = C("#2c2f35"), gunDark = C("#1a1c20"), gunLight = C("#4a4f58");

      Gun("gun_pistol", "Pistol", "Semi-auto. Pushes objects.", "GUNS", SbAmmo.Bullet, 6f, false, k =>
      {
        k.Box(new Vector3(0f, 0.07f, 0.07f), new Vector3(0.035f, 0.045f, 0.2f), gunMetal);
        k.Box(new Vector3(0f, 0.04f, 0.06f), new Vector3(0.032f, 0.025f, 0.17f), gunDark);
        k.Box(new Vector3(0f, -0.01f, 0f), new Vector3(0.03f, 0.1f, 0.045f), Quaternion.Euler(-14f, 0f, 0f), gunDark);
        k.Box(new Vector3(0f, 0.097f, 0.16f), new Vector3(0.008f, 0.01f, 0.01f), gunLight);
        k.GripAt(Vector3.zero).MuzzleAt(new Vector3(0f, 0.07f, 0.18f));
      });
      Gun("gun_rifle", "Assault Rifle", "Full auto.", "GUNS", SbAmmo.Bullet, 11f, true, k =>
      {
        k.Box(new Vector3(0f, 0.05f, 0.12f), new Vector3(0.045f, 0.07f, 0.38f), gunMetal);
        k.Tube(new Vector3(0f, 0.06f, 0.4f), 0.012f, 0.22f, gunDark);
        k.Box(new Vector3(0f, -0.04f, 0.18f), new Vector3(0.03f, 0.12f, 0.06f), Quaternion.Euler(12f, 0f, 0f), gunDark);
        k.Box(new Vector3(0f, -0.02f, 0f), new Vector3(0.03f, 0.1f, 0.04f), Quaternion.Euler(-14f, 0f, 0f), gunDark);
        k.Box(new Vector3(0f, 0.04f, -0.13f), new Vector3(0.04f, 0.08f, 0.18f), gunLight);
        k.Box(new Vector3(0f, 0.1f, 0.12f), new Vector3(0.02f, 0.025f, 0.1f), gunDark);
        k.GripAt(Vector3.zero).MuzzleAt(new Vector3(0f, 0.06f, 0.51f));
      }, 1, 1.2f, 80f);
      Gun("gun_shotgun", "Shotgun", "Eight pellets. Big push.", "GUNS", SbAmmo.Pellet, 1.4f, false, k =>
      {
        k.Tube(new Vector3(0f, 0.06f, 0.25f), 0.018f, 0.55f, gunMetal);
        k.Tube(new Vector3(0f, 0.03f, 0.22f), 0.015f, 0.44f, gunDark);
        k.Box(new Vector3(0f, 0.04f, 0.27f), new Vector3(0.05f, 0.04f, 0.14f), C("#6b4a2a"));
        k.Box(new Vector3(0f, 0.02f, -0.12f), new Vector3(0.04f, 0.07f, 0.24f), Quaternion.Euler(8f, 0f, 0f), C("#6b4a2a"));
        k.Box(new Vector3(0f, -0.02f, 0f), new Vector3(0.03f, 0.09f, 0.04f), Quaternion.Euler(-14f, 0f, 0f), gunDark);
        k.GripAt(Vector3.zero).MuzzleAt(new Vector3(0f, 0.06f, 0.53f));
      }, 8, 5f, 55f);
      Gun("gun_sniper", "Sniper Rifle", "One shot, huge knockback.", "GUNS", SbAmmo.Sniper, 0.8f, false, k =>
      {
        k.Box(new Vector3(0f, 0.04f, 0.1f), new Vector3(0.045f, 0.06f, 0.42f), C("#3a4a32"));
        k.Tube(new Vector3(0f, 0.05f, 0.55f), 0.011f, 0.5f, gunDark);
        k.Tube(new Vector3(0f, 0.11f, 0.12f), 0.022f, 0.22f, gunDark);
        k.Box(new Vector3(0f, -0.02f, 0f), new Vector3(0.03f, 0.1f, 0.04f), Quaternion.Euler(-14f, 0f, 0f), gunDark);
        k.Box(new Vector3(0f, 0.03f, -0.2f), new Vector3(0.04f, 0.09f, 0.22f), C("#3a4a32"));
        k.GripAt(Vector3.zero).MuzzleAt(new Vector3(0f, 0.05f, 0.8f));
      }, 1, 0.05f, 180f);
      Gun("gun_minigun", "Minigun", "Brrrrrt.", "GUNS", SbAmmo.Minigun, 24f, true, k =>
      {
        for (int i = 0; i < 6; i++)
        {
          float a = i * 60f * Mathf.Deg2Rad;
          k.Tube(new Vector3(Mathf.Cos(a) * 0.03f, 0.06f + Mathf.Sin(a) * 0.03f, 0.3f), 0.009f, 0.45f, gunDark, 6);
        }
        k.Tube(new Vector3(0f, 0.06f, 0.05f), 0.06f, 0.16f, gunMetal, 14);
        k.Box(new Vector3(0f, -0.04f, 0.02f), new Vector3(0.1f, 0.08f, 0.14f), C("#5a4a2a"));
        k.Box(new Vector3(0f, 0.15f, 0.02f), new Vector3(0.02f, 0.08f, 0.1f), gunLight);
        k.GripAt(Vector3.zero).MuzzleAt(new Vector3(0f, 0.06f, 0.53f));
      }, 1, 2.4f, 90f);
      Gun("gun_laser", "Laser Blaster", "Pew pew. Instant beam.", "GUNS", SbAmmo.Laser, 5f, false, k =>
      {
        k.Box(new Vector3(0f, 0.06f, 0.08f), new Vector3(0.05f, 0.06f, 0.24f), C("#e8ecf2"));
        k.Tube(new Vector3(0f, 0.06f, 0.24f), 0.022f, 0.1f, C("#2a2f3a"));
        k.Torus(new Vector3(0f, 0.06f, 0.2f), 0.03f, 0.007f, Quaternion.Euler(90f, 0f, 0f), C("#29f0ff"), 14, 4, true);
        k.Torus(new Vector3(0f, 0.06f, 0.25f), 0.03f, 0.007f, Quaternion.Euler(90f, 0f, 0f), C("#29f0ff"), 14, 4, true);
        k.Box(new Vector3(0f, 0.095f, 0.05f), new Vector3(0.02f, 0.012f, 0.12f), C("#29f0ff"), true);
        k.Box(new Vector3(0f, -0.01f, 0f), new Vector3(0.035f, 0.1f, 0.045f), Quaternion.Euler(-12f, 0f, 0f), C("#2a2f3a"));
        k.GripAt(Vector3.zero).MuzzleAt(new Vector3(0f, 0.06f, 0.3f));
      });
      Gun("gun_rocket", "Rocket Launcher", "Explodes on impact. Rocket jumps in modded lobbies.", "GUNS", SbAmmo.Rocket, 0.9f, false, k =>
      {
        k.Tube(new Vector3(0f, 0.1f, 0.12f), 0.065f, 0.95f, C("#4c5a3a"), 14);
        k.Cone(new Vector3(0f, 0.1f, 0.62f), 0.045f, 0.1f, Quaternion.Euler(90f, 0f, 0f), C("#b8b8b8"), 10);
        k.Box(new Vector3(0f, 0f, 0f), new Vector3(0.035f, 0.1f, 0.045f), Quaternion.Euler(-12f, 0f, 0f), gunDark);
        k.Box(new Vector3(0.07f, 0.13f, 0.1f), new Vector3(0.03f, 0.05f, 0.1f), gunDark);
        k.GripAt(Vector3.zero).MuzzleAt(new Vector3(0f, 0.1f, 0.62f));
      }, 1, 0.3f, 22f);
      Gun("gun_grenadelauncher", "Grenade Launcher", "Bouncy grenades, 1.5 s fuse.", "GUNS", SbAmmo.Grenade, 1.5f, false, k =>
      {
        k.Tube(new Vector3(0f, 0.07f, 0.2f), 0.035f, 0.32f, gunMetal, 12);
        k.Cyl(new Vector3(0f, 0.04f, 0.04f), 0.07f, 0.12f, Quaternion.Euler(90f, 0f, 0f), gunDark, 12);
        k.Box(new Vector3(0f, -0.02f, 0f), new Vector3(0.035f, 0.1f, 0.045f), Quaternion.Euler(-14f, 0f, 0f), gunDark);
        k.GripAt(Vector3.zero).MuzzleAt(new Vector3(0f, 0.07f, 0.37f));
      }, 1, 0.5f, 16f);
      Gun("gun_flame", "Flamethrower", "Sets TNT and bombs off.", "GUNS", SbAmmo.Flame, 20f, true, k =>
      {
        k.Tube(new Vector3(0f, 0.06f, 0.2f), 0.02f, 0.4f, gunLight);
        k.Cyl(new Vector3(0f, 0.0f, 0.12f), 0.05f, 0.25f, Quaternion.Euler(90f, 0f, 0f), C("#c43a2a"), 12);
        k.Cone(new Vector3(0f, 0.06f, 0.42f), 0.03f, 0.05f, Quaternion.Euler(-90f, 0f, 0f), gunDark, 10);
        k.Ball(new Vector3(0f, 0.04f, 0.43f), 0.008f, C("#4ab0ff"), 6, true);
        k.Box(new Vector3(0f, -0.04f, 0f), new Vector3(0.035f, 0.09f, 0.04f), Quaternion.Euler(-14f, 0f, 0f), gunDark);
        k.GripAt(Vector3.zero).MuzzleAt(new Vector3(0f, 0.06f, 0.44f));
      }, 1, 6f, 9f);

      // fun guns
      Gun("gun_melon", "Melon Launcher", "Launches watermelons. SPLAT.", "FUN GUNS", SbAmmo.Melon, 1.2f, false, k =>
      {
        k.Tube(new Vector3(0f, 0.08f, 0.2f), 0.08f, 0.6f, C("#3c8a2a"), 14);
        k.Torus(new Vector3(0f, 0.08f, 0.5f), 0.08f, 0.012f, Quaternion.Euler(90f, 0f, 0f), C("#2a6a1a"), 16, 4);
        k.Sphere(new Vector3(0f, 0.2f, 0.05f), new Vector3(0.08f, 0.07f, 0.1f), C("#3a9a2a"), 12);
        k.Box(new Vector3(0f, -0.02f, 0f), new Vector3(0.04f, 0.1f, 0.05f), Quaternion.Euler(-14f, 0f, 0f), C("#2a2a2a"));
        k.GripAt(Vector3.zero).MuzzleAt(new Vector3(0f, 0.08f, 0.5f));
      }, 1, 0.8f, 24f);
      Gun("gun_potato", "Potato Launcher", "Classic PVC spud gun. BONK.", "FUN GUNS", SbAmmo.Potato, 1.6f, false, k =>
      {
        Color pvc = C("#eceff2");
        k.Tube(new Vector3(0f, 0.08f, 0.32f), 0.03f, 0.6f, pvc, 12);
        k.Tube(new Vector3(0f, 0.08f, -0.02f), 0.06f, 0.2f, pvc, 14);
        k.Frustum(new Vector3(0f, 0.08f, 0.1f), 0.06f, 0.03f, 0.06f, Quaternion.Euler(90f, 0f, 0f), pvc, 14);
        k.Box(new Vector3(0.05f, 0.11f, -0.02f), new Vector3(0.03f, 0.03f, 0.04f), C("#ff3a2a"));
        k.Sphere(new Vector3(0f, 0.08f, 0.63f), new Vector3(0.03f, 0.03f, 0.04f), C("#b08a4a"), 8);
        k.Box(new Vector3(0f, -0.02f, 0f), new Vector3(0.04f, 0.1f, 0.05f), Quaternion.Euler(-14f, 0f, 0f), C("#d0d4d8"));
        k.GripAt(Vector3.zero).MuzzleAt(new Vector3(0f, 0.08f, 0.64f));
      }, 1, 1f, 30f);
      Gun("gun_banana", "Banana Blaster", "For monkes.", "FUN GUNS", SbAmmo.Banana, 3f, false, k =>
      {
        k.Box(new Vector3(0f, 0.06f, 0.1f), new Vector3(0.06f, 0.07f, 0.3f), C("#ffd83d"));
        k.Tube(new Vector3(0f, 0.06f, 0.3f), 0.03f, 0.12f, C("#7a5a20"), 10);
        k.Box(new Vector3(0f, 0.12f, 0.08f), new Vector3(0.03f, 0.05f, 0.14f), C("#7a5a20"));
        k.Box(new Vector3(0f, -0.02f, 0f), new Vector3(0.04f, 0.1f, 0.05f), Quaternion.Euler(-14f, 0f, 0f), C("#7a5a20"));
        k.GripAt(Vector3.zero).MuzzleAt(new Vector3(0f, 0.06f, 0.36f));
      }, 1, 1f, 26f);
      Gun("gun_snowball", "Snowball Gun", "Fires snowballs.", "FUN GUNS", SbAmmo.Snowball, 5f, true, k =>
      {
        k.Box(new Vector3(0f, 0.06f, 0.1f), new Vector3(0.06f, 0.07f, 0.28f), C("#7ec8ff"));
        k.Tube(new Vector3(0f, 0.06f, 0.28f), 0.028f, 0.1f, C("#3a7ac0"), 10);
        k.Ball(new Vector3(0f, 0.15f, 0.05f), 0.05f, C("#ffffff"), 10);
        k.Box(new Vector3(0f, -0.02f, 0f), new Vector3(0.04f, 0.1f, 0.05f), Quaternion.Euler(-14f, 0f, 0f), C("#3a7ac0"));
        k.GripAt(Vector3.zero).MuzzleAt(new Vector3(0f, 0.06f, 0.33f));
      }, 1, 2f, 30f);
      Gun("gun_paint", "Paintball Gun", "Leaves colourful splats on everything.", "FUN GUNS", SbAmmo.Paint, 7f, true, k =>
      {
        k.Box(new Vector3(0f, 0.06f, 0.12f), new Vector3(0.05f, 0.07f, 0.3f), C("#2a2a2a"));
        k.Tube(new Vector3(0f, 0.07f, 0.35f), 0.015f, 0.2f, C("#5a5a5a"));
        k.Sphere(new Vector3(0f, 0.15f, 0.08f), new Vector3(0.05f, 0.06f, 0.07f), C("#ff3df5"), 10);
        k.Cyl(new Vector3(0f, 0.0f, -0.1f), 0.03f, 0.2f, Quaternion.Euler(90f, 0f, 0f), C("#3a8aff"), 10);
        k.Box(new Vector3(0f, -0.02f, 0f), new Vector3(0.035f, 0.1f, 0.045f), Quaternion.Euler(-14f, 0f, 0f), C("#111111"));
        k.GripAt(Vector3.zero).MuzzleAt(new Vector3(0f, 0.07f, 0.45f));
      }, 1, 1.5f, 40f);
      Gun("gun_bubble", "Bubble Gun", "Floaty bubbles.", "FUN GUNS", SbAmmo.Bubble, 8f, true, k =>
      {
        k.Box(new Vector3(0f, 0.06f, 0.08f), new Vector3(0.06f, 0.08f, 0.2f), C("#ff8ad0"));
        k.Torus(new Vector3(0f, 0.06f, 0.22f), 0.05f, 0.008f, Quaternion.Euler(90f, 0f, 0f), C("#8affef"), 18, 4, true);
        k.Cyl(new Vector3(0f, 0.13f, 0.04f), 0.03f, 0.06f, Quaternion.identity, C("#bdf2ff"), 10);
        k.Box(new Vector3(0f, -0.02f, 0f), new Vector3(0.04f, 0.1f, 0.05f), Quaternion.Euler(-14f, 0f, 0f), C("#ff8ad0"));
        k.GripAt(Vector3.zero).MuzzleAt(new Vector3(0f, 0.06f, 0.23f));
      }, 2, 10f, 3f);
      Gun("gun_confetti", "Confetti Cannon", "Party time.", "FUN GUNS", SbAmmo.Confetti, 2f, false, k =>
      {
        k.Frustum(new Vector3(0f, 0.06f, 0.15f), 0.03f, 0.07f, 0.26f, Quaternion.Euler(90f, 0f, 0f), C("#ff4bd8"), 14);
        k.Torus(new Vector3(0f, 0.06f, 0.28f), 0.07f, 0.008f, Quaternion.Euler(90f, 0f, 0f), C("#ffe14d"), 16, 4);
        k.Box(new Vector3(0f, -0.02f, 0f), new Vector3(0.04f, 0.1f, 0.05f), Quaternion.Euler(-14f, 0f, 0f), C("#4bd0ff"));
        k.GripAt(Vector3.zero).MuzzleAt(new Vector3(0f, 0.06f, 0.29f));
      });
      Gun("gun_duck", "Duck Launcher", "Launches rubber ducks that stay around.", "FUN GUNS", SbAmmo.Duck, 2.5f, false, k =>
      {
        k.Tube(new Vector3(0f, 0.08f, 0.2f), 0.06f, 0.45f, C("#ffd21f"), 14);
        k.Ball(new Vector3(0f, 0.17f, 0.05f), 0.05f, C("#ffd21f"), 10);
        k.Sphere(new Vector3(0f, 0.16f, 0.1f), new Vector3(0.02f, 0.01f, 0.03f), C("#ff8a1f"), 6);
        k.Box(new Vector3(0f, -0.02f, 0f), new Vector3(0.04f, 0.1f, 0.05f), Quaternion.Euler(-14f, 0f, 0f), C("#3a7ac0"));
        k.GripAt(Vector3.zero).MuzzleAt(new Vector3(0f, 0.08f, 0.43f));
      }, 1, 1f, 14f);

      // bombs
      SbBossItems.AddToCatalog(i => Add(i));
      SbMeleeItems.AddToCatalog(i => Add(i));   // melee and throwables (SandBoxMelee.cs)   // mob shots and the wither skull (SandBoxBosses.cs)
      // gadget guns and turrets (SandBoxGadgets.cs)
      Gun("gun_portal_rick", "Rick & Morty Portal Gun", "Each shot opens one end of a green portal pair. Walk, fall or throw things through one and come out the other. Everyone with SandBox sees them.", "GADGETS", SbAmmo.RickPortal, 2.5f, false, SbGadgetArt.RickGun);
      Gun("gun_portal", "Portal Gun", "Aperture Science: shots open the blue, then the orange portal. Momentum is kept: speedy thing goes in, speedy thing comes out.", "GADGETS", SbAmmo.AperturePortal, 2.5f, false, SbGadgetArt.ApertureGun);
      Gun("gun_gravity", "Gravity Gun", "Hold the trigger to pull one of your things to you and carry it, let go to launch it. Tap to punt what you point at.", "GADGETS", SbAmmo.Gravity, 3f, false, SbGadgetArt.GravityGun);
      Gun("gun_teleport", "Teleport Gun", "You appear where it hits (in a SandBox lobby where everyone has the SandBox).", "GADGETS", SbAmmo.Teleport, 1.2f, false, SbGadgetArt.TeleportGun);
      Gun("gun_build", "Build Gun", "Places the block you last picked on the Minecraft tab, up to 12 m away.", "GADGETS", SbAmmo.Build, 6f, true, SbGadgetArt.BuildGun);
      Add(new SbItem { Id = "turret_auto", Name = "Auto-Turret", Desc = "Place it: it shoots mobs and other players that come close. Never you.", Tab = SbTab.Weapons, Section = "TURRETS", Kind = SbKind.Prop, Turret = SbTurretKind.Turret, Build = SbGadgetArt.Turret, DefaultAnchored = true, Mass = 20f });
      Add(new SbItem { Id = "turret_sentry", Name = "Sentry Drone", Desc = "Floats beside you and shoots mobs and other players nearby. Never you.", Tab = SbTab.Weapons, Section = "TURRETS", Kind = SbKind.Prop, Turret = SbTurretKind.Sentry, Build = SbGadgetArt.Sentry, DefaultAnchored = true, Mass = 3f, Col = SbCol.Sphere });

      Bomb("bomb_grenade", "Grenade", "Grab it, press trigger to pull the pin, throw. 3 s.", SbBlast.Normal, 2.5f, 3f, true, k =>
      {
        k.Sphere(Vector3.zero, new Vector3(0.045f, 0.06f, 0.045f), C("#4a5a2a"), 12);
        k.Cyl(new Vector3(0f, 0.065f, 0f), 0.018f, 0.025f, Quaternion.identity, C("#8a8a8a"), 8);
        k.Box(new Vector3(0.025f, 0.04f, 0f), new Vector3(0.012f, 0.08f, 0.02f), Quaternion.Euler(0f, 0f, -12f), C("#9a9a9a"));
        k.Torus(new Vector3(-0.025f, 0.075f, 0f), 0.015f, 0.003f, Quaternion.Euler(0f, 0f, 90f), C("#d0d0d0"), 10, 4);
      }, SbCol.Sphere);
      Bomb("bomb_sticky", "Sticky Bomb", "Sticks to whatever it hits. Trigger to arm, throw.", SbBlast.Sticky, 2.5f, 3f, true, k =>
      {
        k.Ball(Vector3.zero, 0.06f, C("#2a8a3a"), 12);
        k.Sphere(new Vector3(0f, -0.04f, 0f), new Vector3(0.06f, 0.025f, 0.06f), C("#9aff6a"), 10);
        k.Ball(new Vector3(0f, 0.055f, 0f), 0.012f, C("#ff3a2a"), 6, true);
      }, SbCol.Sphere);
      Bomb("bomb_dynamite", "Dynamite", "Light the fuse with trigger, then throw.", SbBlast.Normal, 3f, 3.5f, true, k =>
      {
        for (int i = 0; i < 3; i++) k.Cyl(new Vector3((i - 1) * 0.035f, 0f, 0f), 0.017f, 0.18f, Quaternion.identity, C("#c4302a"), 10);
        k.Box(new Vector3(0f, 0.03f, 0f), new Vector3(0.11f, 0.02f, 0.04f), C("#2a2a2a"));
        k.Cyl(new Vector3(0f, 0.11f, 0f), 0.003f, 0.05f, Quaternion.Euler(0f, 0f, 20f), C("#d8c8a0"), 4);
      });
      Bomb("bomb_c4", "C4", "Place it. Touch it or press DETONATE ALL.", SbBlast.Normal, 4f, 0.2f, false, k =>
      {
        k.Box(Vector3.zero, new Vector3(0.2f, 0.06f, 0.12f), C("#d8cfa8"));
        k.Box(new Vector3(0f, 0.035f, 0f), new Vector3(0.08f, 0.012f, 0.05f), C("#1a1a1a"));
        k.Box(new Vector3(0f, 0.042f, 0f), new Vector3(0.06f, 0.004f, 0.03f), C("#ff2a2a"), true);
        k.Box(new Vector3(0.06f, 0.04f, 0.04f), new Vector3(0.006f, 0.01f, 0.1f), C("#2a6aff"));
        k.Box(new Vector3(0.07f, 0.04f, 0.04f), new Vector3(0.006f, 0.01f, 0.1f), C("#ff3a2a"));
      });
      Bomb("bomb_landmine", "Landmine", "Goes off when anything comes close.", SbBlast.Landmine, 3f, 0.4f, false, k =>
      {
        k.Cyl(Vector3.zero, 0.12f, 0.04f, Quaternion.identity, C("#4a4a3a"), 16);
        k.Cyl(new Vector3(0f, 0.025f, 0f), 0.04f, 0.015f, Quaternion.identity, C("#7a7a6a"), 12);
        k.Ball(new Vector3(0f, 0.035f, 0f), 0.008f, C("#ff2a2a"), 6, true);
      });
      Bomb("bomb_cluster", "Cluster Bomb", "Splits into five smaller blasts.", SbBlast.Cluster, 2.5f, 3f, true, k =>
      {
        k.Ball(Vector3.zero, 0.08f, C("#3a3a42"), 12);
        for (int i = 0; i < 6; i++)
        {
          Vector3 d = Quaternion.Euler(i * 55f, i * 97f, 0f) * Vector3.up;
          k.Ball(d * 0.08f, 0.03f, C("#ff7a1a"), 8);
        }
      }, SbCol.Sphere);
      Bomb("bomb_firework", "Firework", "Touch it: it launches and bursts in the sky.", SbBlast.Firework, 1f, 1.2f, false, k =>
      {
        k.Cyl(Vector3.zero, 0.04f, 0.3f, Quaternion.identity, C("#e53b5c"), 12);
        k.Cone(new Vector3(0f, 0.2f, 0f), 0.045f, 0.1f, Quaternion.identity, C("#ffd83d"), 12);
        k.Cyl(new Vector3(0f, -0.35f, 0f), 0.006f, 0.45f, Quaternion.identity, C("#c4a070"), 4);
      });
      Bomb("bomb_barrel", "Explosive Barrel", "Shoot it.", SbBlast.Barrel, 3.5f, 0.05f, false, k =>
      {
        k.Cyl(Vector3.zero, 0.22f, 0.6f, Quaternion.identity, C("#c4302a"), 16);
        k.Torus(new Vector3(0f, 0.2f, 0f), 0.222f, 0.012f, Quaternion.identity, C("#3a3a3a"), 18, 4);
        k.Torus(new Vector3(0f, -0.2f, 0f), 0.222f, 0.012f, Quaternion.identity, C("#3a3a3a"), 18, 4);
        k.Box(new Vector3(0f, 0f, 0.222f), new Vector3(0.18f, 0.16f, 0.005f), C("#ffd83d"));
        k.Cone(new Vector3(0f, 0.01f, 0.226f), 0.05f, 0.08f, Quaternion.Euler(-90f, 0f, 0f), C("#1a1a1a"), 3);
      }, SbCol.Mesh);
      Bomb("bomb_blackhole", "Black Hole Bomb", "Pulls everything in, then pops.", SbBlast.BlackHole, 6f, 2f, true, k =>
      {
        k.Ball(Vector3.zero, 0.07f, C("#0a0612"), 14);
        k.Torus(Vector3.zero, 0.1f, 0.012f, Quaternion.Euler(70f, 0f, 20f), C("#b44aff"), 20, 6, true);
        k.Torus(Vector3.zero, 0.09f, 0.006f, Quaternion.Euler(-30f, 40f, 0f), C("#4af0ff"), 20, 4, true);
      }, SbCol.Sphere);
      Bomb("bomb_mininuke", "Mini Nuke", "A small mushroom cloud.", SbBlast.MiniNuke, 9f, 5f, true, k =>
      {
        k.Sphere(Vector3.zero, new Vector3(0.08f, 0.08f, 0.14f), C("#e8c22a"), 14);
        k.Cone(new Vector3(0f, 0f, -0.18f), 0.06f, 0.08f, Quaternion.Euler(-90f, 0f, 0f), C("#2a2a2a"), 4);
        k.Torus(Vector3.zero, 0.081f, 0.01f, Quaternion.Euler(90f, 0f, 0f), C("#1a1a1a"), 18, 4);
        k.Ball(new Vector3(0f, 0.08f, 0f), 0.02f, C("#1a1a1a"), 6);
      });
      Bomb("bomb_nuke", "Nuke", "Touch it to start the countdown. 10 s. Huge flash, mushroom cloud, shockwave. Clears everything nearby.", SbBlast.Nuke, 28f, 10f, false, k =>
      {
        Color y = C("#e8c22a"), d = C("#1e1e1e");
        k.Sphere(Vector3.zero, new Vector3(0.32f, 0.32f, 0.62f), y, 20);
        k.Torus(new Vector3(0f, 0f, 0.15f), 0.32f, 0.025f, Quaternion.Euler(90f, 0f, 0f), d, 24, 6);
        k.Torus(new Vector3(0f, 0f, -0.15f), 0.32f, 0.025f, Quaternion.Euler(90f, 0f, 0f), d, 24, 6);
        k.Cyl(new Vector3(0f, 0f, -0.68f), 0.12f, 0.18f, Quaternion.Euler(90f, 0f, 0f), d, 14);
        for (int i = 0; i < 4; i++)
          k.Box(new Vector3(0f, 0f, -0.72f), new Vector3(0.62f, 0.03f, 0.22f), Quaternion.Euler(0f, 0f, i * 45f), d);
        // radiation trefoil on both sides
        for (int side = -1; side <= 1; side += 2)
        {
          k.Ball(new Vector3(side * 0.3f, 0f, 0f), 0.03f, d, 8);
          for (int i = 0; i < 3; i++)
          {
            Quaternion r = Quaternion.AngleAxis(i * 120f, Vector3.right);
            k.Box(new Vector3(side * 0.29f, 0f, 0f) + r * new Vector3(0f, 0.1f, 0f), new Vector3(0.04f, 0.09f, 0.1f), r, d);
          }
        }
        k.Ball(new Vector3(0.0f, 0.3f, 0.0f), 0.04f, C("#ff2a2a"), 8, true);
      }, SbCol.Box);
    }
  }
}
