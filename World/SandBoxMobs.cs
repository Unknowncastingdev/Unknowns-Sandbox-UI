using System;
using System.Collections.Generic;
using GorillaLocomotion;
using UnityEngine;

namespace UnknownsSandbox
{
  internal enum SbMobType
  {
    None, Sheep, Pig, Cow, Chicken, Zombie, Skeleton, Villager, IronGolem, Piglin, Enderman,
    // (new ones go on the end: the number is what goes over the network)
    Creeper, Spider, CaveSpider, Wolf, Ocelot, Mooshroom, BrownMooshroom, Husk, Drowned, Stray, WitherSkeleton,
    ZombieVillager, SnowGolem, Slime, MagmaCube, ZombifiedPiglin, PiglinBrute, Blaze, Silverfish, Endermite,

    Rabbit, Fox, Cat, Llama, Witch, Pillager, Vindicator, Evoker, Illusioner, Bogged, Hoglin, Zoglin, Shulker,
    // (SandBoxMobsMore.cs)
    Horse, Bee, Frog, Axolotl, Panda, PolarBear, Turtle, Goat, Camel, Squid, Cod, Parrot, Bat, Ghast, Strider, Phantom, Vex, Ravager,
    Wither, EnderDragon, EndCrystal
  }

  // Minecraft mobs. Each is built from boxes exactly like Minecraft's
  // models (same pixel sizes and texture layout), skinned with the real mob
  // texture from the player's own Minecraft install, else plain colours. They
  // walk around, look at you, make their sounds, can be hit, thrown and killed.
  // Zombies come after the nearest player, endermen teleport. Spawned from the
  // tablet they are yours (others see them move); the piglins in the Nether and
  // the endermen in the End are local (each player has their own).
  internal static partial class SbMobDefs
  {
    internal enum Role { None, Head, Body, LegL, LegR, ArmL, ArmR, LegFL, LegFR, LegBL, LegBR, WingL, WingR, Spin, Spin2 }

    internal sealed class Part
    {
      public Role Role;
      public Vector3 Min, Size, Pivot;
      public int U, V;
      public float Inflate;
      public bool RotX, Mirror, Tex2, Glow;
      public Quaternion BaseRot = Quaternion.identity;
      public Color Col;
    }

    internal sealed class Def
    {
      public SbMobType Type;
      public string Id, Name, Desc, Section;
      public string[] Tex, Tex2;
      public int TexW = 64, TexH = 32, Tex2W = 64, Tex2H = 32;
      public float Speed = 1f, Mass = 8f;
      public int Hp = 10;
      public string Say, Hurt, Death;
      public bool Quad, Biped, Hostile;
      public bool Hops, ZombieArms, SpiderLegs, Explodes;
      public string HopSound;   // the sound of a hop (else slime / magma cube)
      public float Scale = 1f;   // a bigger or smaller copy of the model (wither skeleton, cave spider)
      // flying, swimming, getting angry, shooting, bosses (SandBoxMobsMore.cs)
      public bool Flies, Swims, Angers, Boss, NoAi, BlastOnDeath, Hidden;
      public SbAmmo Shot;          // what it shoots (Bullet = nothing)
      public bool Shoots;
      public float ShotEvery = 2f, ShotRange = 14f, KeepAway;
      public float FlyHeight = 3f;
      public readonly List<Part> Parts = new List<Part>();
    }

    internal const float PixelScale = 0.7f / 16f;   // a Minecraft pixel in metres (a zombie is 1.4 m)
    internal static readonly Dictionary<SbMobType, Def> Defs = new Dictionary<SbMobType, Def>();

    private static Part P(Def d, Role role, Vector3 min, Vector3 size, int u, int v, Vector3 pivot, string col, bool mirror = false, float inflate = 0f)
    {
      var p = new Part { Role = role, Min = min, Size = size, U = u, V = v, Pivot = pivot, Col = SbCatalog.C(col), Mirror = mirror, Inflate = inflate };
      d.Parts.Add(p);
      return p;
    }

    private static Def D(SbMobType t, string id, string name, string desc, string section, int hp, float speed, float mass, string say, string hurt, string death, params string[] tex)
    {
      var d = new Def { Type = t, Id = id, Name = name, Desc = desc, Section = section, Hp = hp, Speed = speed, Mass = mass, Say = say, Hurt = hurt, Death = death, Tex = tex };
      Defs[t] = d;
      return d;
    }

    // all the file names to read from the Minecraft jar
    internal static IEnumerable<string> TextureFiles()
    {
      Init();
      foreach (Def d in Defs.Values)
      {
        if (d.Tex != null) foreach (string s in d.Tex) yield return "textures/" + s + ".png";
        if (d.Tex2 != null) foreach (string s in d.Tex2) yield return "textures/" + s + ".png";
      }
    }

    private static bool built;
    internal static void Init()
    {
      if (built) return;
      built = true;
      Def d;

      // ---- farm animals (Minecraft 64 x 32 layouts), +Z is the front, y up from the feet
      d = D(SbMobType.Sheep, "mob_sheep", "Sheep", "Baa. Wanders and eats grass.", "ANIMALS", 8, 1f, 8f, "sheep_say", "sheep_say", "sheep_say", "entity/sheep/sheep");
      d.Tex2 = new[] { "entity/sheep/sheep_fur", "entity/sheep/sheep_wool" };   // (newer Minecraft: sheep_wool)
      d.Quad = true;
      foreach (var leg in new[] { (Role.LegFR, 1f, 3f), (Role.LegFL, -5f, 3f), (Role.LegBR, 1f, -7f), (Role.LegBL, -5f, -7f) })
      {
        P(d, leg.Item1, new Vector3(leg.Item2, 0f, leg.Item3), new Vector3(4f, 12f, 4f), 0, 16, new Vector3(leg.Item2 + 2f, 12f, leg.Item3 + 2f), "#d8b8a0");
        P(d, leg.Item1, new Vector3(leg.Item2, 6f, leg.Item3), new Vector3(4f, 6f, 4f), 0, 16, new Vector3(leg.Item2 + 2f, 12f, leg.Item3 + 2f), "#ececec", false, 0.5f).Tex2 = true;
      }
      P(d, Role.Body, new Vector3(-4f, -8f, -3f), new Vector3(8f, 16f, 6f), 28, 8, new Vector3(0f, 15f, 0f), "#d8b8a0").RotX = true;
      var fur = P(d, Role.Body, new Vector3(-4f, -8f, -3f), new Vector3(8f, 16f, 6f), 28, 8, new Vector3(0f, 15f, 0f), "#ececec", false, 1.75f);
      fur.RotX = true; fur.Tex2 = true;
      P(d, Role.Head, new Vector3(-3f, 16f, 6f), new Vector3(6f, 6f, 8f), 0, 0, new Vector3(0f, 19f, 7f), "#d8b8a0");
      P(d, Role.Head, new Vector3(-3f, 16f, 6f), new Vector3(6f, 6f, 6f), 0, 0, new Vector3(0f, 19f, 7f), "#ececec", false, 0.6f).Tex2 = true;

      d = D(SbMobType.Pig, "mob_pig", "Pig", "Oink.", "ANIMALS", 10, 1f, 8f, "pig_say", "pig_say", "pig_death", "entity/pig/pig", "entity/pig/pig_temperate", "entity/pig/temperate_pig");
      d.Quad = true;
      foreach (var leg in new[] { (Role.LegFR, 1f, 3f), (Role.LegFL, -5f, 3f), (Role.LegBR, 1f, -7f), (Role.LegBL, -5f, -7f) })
        P(d, leg.Item1, new Vector3(leg.Item2, 0f, leg.Item3), new Vector3(4f, 6f, 4f), 0, 16, new Vector3(leg.Item2 + 2f, 6f, leg.Item3 + 2f), "#f0a0a0");
      P(d, Role.Body, new Vector3(-5f, -8f, -4f), new Vector3(10f, 16f, 8f), 28, 8, new Vector3(0f, 10f, 0f), "#f0a8a8").RotX = true;
      P(d, Role.Head, new Vector3(-4f, 8f, 6f), new Vector3(8f, 8f, 8f), 0, 0, new Vector3(0f, 12f, 6f), "#f0a8a8");
      P(d, Role.Head, new Vector3(-2f, 9f, 14f), new Vector3(4f, 3f, 1f), 16, 16, new Vector3(0f, 12f, 6f), "#e07878");

      d = D(SbMobType.Cow, "mob_cow", "Cow", "Moo.", "ANIMALS", 10, 0.9f, 12f, "cow_say", "cow_hurt", "cow_hurt", "entity/cow/cow", "entity/cow/cow_temperate", "entity/cow/temperate_cow");
      d.Quad = true;
      foreach (var leg in new[] { (Role.LegFR, 2f, 4f), (Role.LegFL, -6f, 4f), (Role.LegBR, 2f, -8f), (Role.LegBL, -6f, -8f) })
        P(d, leg.Item1, new Vector3(leg.Item2, 0f, leg.Item3), new Vector3(4f, 12f, 4f), 0, 16, new Vector3(leg.Item2 + 2f, 12f, leg.Item3 + 2f), "#3a2a1e");
      P(d, Role.Body, new Vector3(-6f, -9f, -5f), new Vector3(12f, 18f, 10f), 18, 4, new Vector3(0f, 17f, 0f), "#4a3626").RotX = true;
      P(d, Role.Head, new Vector3(-4f, 16f, 8f), new Vector3(8f, 8f, 6f), 0, 0, new Vector3(0f, 20f, 8f), "#4a3626");
      P(d, Role.Head, new Vector3(-5f, 22f, 10f), new Vector3(1f, 3f, 1f), 22, 0, new Vector3(0f, 20f, 8f), "#d8d0c0");
      P(d, Role.Head, new Vector3(4f, 22f, 10f), new Vector3(1f, 3f, 1f), 22, 0, new Vector3(0f, 20f, 8f), "#d8d0c0");

      d = D(SbMobType.Chicken, "mob_chicken", "Chicken", "Bawk.", "ANIMALS", 4, 0.9f, 1.5f, "chicken_say", "chicken_hurt", "chicken_hurt", "entity/chicken", "entity/chicken/chicken_temperate", "entity/chicken/temperate_chicken");
      d.Quad = true;
      P(d, Role.LegFR, new Vector3(0f, 0f, -1f), new Vector3(3f, 5f, 3f), 26, 0, new Vector3(1.5f, 5f, 0.5f), "#e8b030");
      P(d, Role.LegFL, new Vector3(-3f, 0f, -1f), new Vector3(3f, 5f, 3f), 26, 0, new Vector3(-1.5f, 5f, 0.5f), "#e8b030");
      P(d, Role.Body, new Vector3(-3f, -4f, -3f), new Vector3(6f, 8f, 6f), 0, 9, new Vector3(0f, 8f, 0f), "#f4f4f4").RotX = true;
      P(d, Role.WingR, new Vector3(3f, 7f, -3f), new Vector3(1f, 4f, 6f), 24, 13, new Vector3(3f, 11f, 0f), "#e8e8e8");
      P(d, Role.WingL, new Vector3(-4f, 7f, -3f), new Vector3(1f, 4f, 6f), 24, 13, new Vector3(-3f, 11f, 0f), "#e8e8e8", true);
      P(d, Role.Head, new Vector3(-2f, 9f, 3f), new Vector3(4f, 6f, 3f), 0, 0, new Vector3(0f, 9f, 4f), "#f4f4f4");
      P(d, Role.Head, new Vector3(-2f, 12f, 6f), new Vector3(4f, 2f, 2f), 14, 0, new Vector3(0f, 9f, 4f), "#f0a020");
      P(d, Role.Head, new Vector3(-1f, 10f, 6f), new Vector3(2f, 2f, 2f), 14, 4, new Vector3(0f, 9f, 4f), "#e02020");

      // ---- people (64 x 64 humanoid layouts)
      d = D(SbMobType.Zombie, "mob_zombie", "Zombie", "Groans and comes after the nearest player.", "MOBS", 20, 0.9f, 10f, "zombie_say", "zombie_hurt", "zombie_death", "entity/zombie/zombie");
      d.TexH = 64; d.Biped = true; d.Hostile = true; d.ZombieArms = true;
      Humanoid(d, "#6a9a5a", "#2e8fa0", "#3a3a8a", 4f, true);

      d = D(SbMobType.Skeleton, "mob_skeleton", "Skeleton", "Rattles around.", "MOBS", 20, 1f, 8f, "skeleton_say", "skeleton_hurt", "skeleton_death", "entity/skeleton/skeleton");
      d.Biped = true;
      P(d, Role.Head, new Vector3(-4f, 24f, -4f), new Vector3(8f, 8f, 8f), 0, 0, new Vector3(0f, 24f, 0f), "#c8c8c8");
      P(d, Role.Body, new Vector3(-4f, 12f, -2f), new Vector3(8f, 12f, 4f), 16, 16, new Vector3(0f, 12f, 0f), "#b8b8b8");
      P(d, Role.ArmR, new Vector3(4f, 12f, -1f), new Vector3(2f, 12f, 2f), 40, 16, new Vector3(5f, 22f, 0f), "#c8c8c8");
      P(d, Role.ArmL, new Vector3(-6f, 12f, -1f), new Vector3(2f, 12f, 2f), 40, 16, new Vector3(-5f, 22f, 0f), "#c8c8c8", true);
      P(d, Role.LegR, new Vector3(1f, 0f, -1f), new Vector3(2f, 12f, 2f), 0, 16, new Vector3(2f, 12f, 0f), "#c8c8c8");
      P(d, Role.LegL, new Vector3(-3f, 0f, -1f), new Vector3(2f, 12f, 2f), 0, 16, new Vector3(-2f, 12f, 0f), "#c8c8c8", true);

      d = D(SbMobType.Villager, "mob_villager", "Villager", "Hmm. Wanders, and stops to look at you.", "MOBS", 20, 0.8f, 10f, "villager_say", "villager_hurt", "villager_death", "entity/villager/villager");
      d.TexH = 64; d.Biped = true;
      P(d, Role.Head, new Vector3(-4f, 24f, -4f), new Vector3(8f, 10f, 8f), 0, 0, new Vector3(0f, 24f, 0f), "#b98f6b");
      P(d, Role.Head, new Vector3(-1f, 25f, 4f), new Vector3(2f, 4f, 2f), 24, 0, new Vector3(0f, 24f, 0f), "#a87e5a");
      P(d, Role.Body, new Vector3(-4f, 12f, -3f), new Vector3(8f, 12f, 6f), 16, 20, new Vector3(0f, 12f, 0f), "#6b4a2a");
      P(d, Role.Body, new Vector3(-4f, 6f, -3f), new Vector3(8f, 18f, 6f), 0, 38, new Vector3(0f, 12f, 0f), "#5a3c22", false, 0.5f);
      // the crossed arms, tipped forward
      Quaternion tilt = Quaternion.Euler(-43f, 0f, 0f);
      foreach (var a in new[] { (new Vector3(-4f, -6f, -2f), new Vector3(8f, 4f, 4f), 40, 38), (new Vector3(-8f, -6f, -2f), new Vector3(4f, 8f, 4f), 44, 22), (new Vector3(4f, -6f, -2f), new Vector3(4f, 8f, 4f), 44, 22) })
      {
        Part arm = P(d, Role.None, new Vector3(0f, 21f, 1f) + a.Item1, a.Item2, a.Item3, a.Item4, new Vector3(0f, 21f, 1f), "#b98f6b");
        arm.BaseRot = tilt;
      }
      P(d, Role.LegR, new Vector3(0f, 0f, -2f), new Vector3(4f, 12f, 4f), 0, 22, new Vector3(2f, 12f, 0f), "#4a3020");
      P(d, Role.LegL, new Vector3(-4f, 0f, -2f), new Vector3(4f, 12f, 4f), 0, 22, new Vector3(-2f, 12f, 0f), "#4a3020", true);

      d = D(SbMobType.IronGolem, "mob_golem", "Iron Golem", "Big, slow and gentle. Protects the village.", "MOBS", 100, 0.6f, 60f, "golem_step", "golem_hurt", "golem_death", "entity/iron_golem/iron_golem");
      d.TexW = 128; d.TexH = 128; d.Biped = true;
      P(d, Role.LegR, new Vector3(1.5f, 0f, -2.5f), new Vector3(6f, 16f, 5f), 37, 0, new Vector3(4.5f, 16f, 0f), "#c9c3b9");
      P(d, Role.LegL, new Vector3(-7.5f, 0f, -2.5f), new Vector3(6f, 16f, 5f), 60, 0, new Vector3(-4.5f, 16f, 0f), "#c9c3b9");
      P(d, Role.Body, new Vector3(-4.5f, 16f, -3f), new Vector3(9f, 5f, 6f), 0, 70, new Vector3(0f, 16f, 0f), "#bdb7ad", false, 0.5f);
      P(d, Role.Body, new Vector3(-9f, 21f, -5.5f), new Vector3(18f, 12f, 11f), 0, 40, new Vector3(0f, 21f, 0f), "#c9c3b9");
      P(d, Role.Head, new Vector3(-4f, 33f, -4f), new Vector3(8f, 10f, 8f), 0, 0, new Vector3(0f, 33f, 0f), "#c9c3b9");
      P(d, Role.Head, new Vector3(-1f, 34f, 4f), new Vector3(2f, 4f, 2f), 24, 0, new Vector3(0f, 33f, 0f), "#b9b3a9");
      P(d, Role.ArmR, new Vector3(9f, 4f, -3f), new Vector3(4f, 30f, 6f), 60, 21, new Vector3(11f, 31f, 0f), "#c9c3b9");
      P(d, Role.ArmL, new Vector3(-13f, 4f, -3f), new Vector3(4f, 30f, 6f), 60, 58, new Vector3(-11f, 31f, 0f), "#c9c3b9");

      // ---- the Nether and the End
      d = D(SbMobType.Piglin, "mob_piglin", "Piglin", "Snorts around the Nether.", "NETHER MOBS", 16, 1f, 10f, "piglin_say", "piglin_hurt", "piglin_death", "entity/piglin/piglin");
      d.TexH = 64; d.Biped = true;
      P(d, Role.Head, new Vector3(-5f, 24f, -4f), new Vector3(10f, 8f, 8f), 0, 0, new Vector3(0f, 24f, 0f), "#e8a59a");
      P(d, Role.Head, new Vector3(-2f, 24f, 4f), new Vector3(4f, 4f, 1f), 31, 1, new Vector3(0f, 24f, 0f), "#d88a80");
      P(d, Role.Body, new Vector3(-4f, 12f, -2f), new Vector3(8f, 12f, 4f), 16, 16, new Vector3(0f, 12f, 0f), "#6b4a2a");
      P(d, Role.ArmR, new Vector3(4f, 12f, -2f), new Vector3(4f, 12f, 4f), 40, 16, new Vector3(6f, 22f, 0f), "#e8a59a");
      P(d, Role.ArmL, new Vector3(-8f, 12f, -2f), new Vector3(4f, 12f, 4f), 32, 48, new Vector3(-6f, 22f, 0f), "#e8a59a");
      P(d, Role.LegR, new Vector3(0f, 0f, -2f), new Vector3(4f, 12f, 4f), 0, 16, new Vector3(2f, 12f, 0f), "#5a3c22");
      P(d, Role.LegL, new Vector3(-4f, 0f, -2f), new Vector3(4f, 12f, 4f), 16, 48, new Vector3(-2f, 12f, 0f), "#5a3c22");

      d = D(SbMobType.Enderman, "mob_enderman", "Enderman", "Tall and quiet. Teleports around.", "END MOBS", 40, 1.1f, 12f, "enderman_say", "enderman_hurt", "enderman_death", "entity/enderman/enderman");
      d.Biped = true;
      P(d, Role.Head, new Vector3(-4f, 38f, -4f), new Vector3(8f, 8f, 8f), 0, 0, new Vector3(0f, 38f, 0f), "#161616");
      Part eyeR = P(d, Role.Head, new Vector3(1f, 41f, 4f), new Vector3(2f, 1f, 0.2f), 0, 0, new Vector3(0f, 38f, 0f), "#e08cff");
      Part eyeL = P(d, Role.Head, new Vector3(-3f, 41f, 4f), new Vector3(2f, 1f, 0.2f), 0, 0, new Vector3(0f, 38f, 0f), "#e08cff");
      eyeR.Glow = eyeL.Glow = true;
      P(d, Role.Body, new Vector3(-4f, 26f, -2f), new Vector3(8f, 12f, 4f), 32, 16, new Vector3(0f, 26f, 0f), "#121212");
      P(d, Role.ArmR, new Vector3(4f, 8f, -1f), new Vector3(2f, 30f, 2f), 56, 0, new Vector3(5f, 37f, 0f), "#141414");
      P(d, Role.ArmL, new Vector3(-6f, 8f, -1f), new Vector3(2f, 30f, 2f), 56, 0, new Vector3(-5f, 37f, 0f), "#141414", true);
      P(d, Role.LegR, new Vector3(1f, 0f, -1f), new Vector3(2f, 28f, 2f), 56, 0, new Vector3(2f, 28f, 0f), "#141414");
      P(d, Role.LegL, new Vector3(-3f, 0f, -1f), new Vector3(2f, 28f, 2f), 56, 0, new Vector3(-2f, 28f, 0f), "#141414", true);
      InitMore();   // the rest of the mobs
    }

    // a box given the way Minecraft's model code gives it: the pivot and the box
    // offset in Minecraft's model space (y down from 24, -Z the front) and the turn in
    // radians (applied X, then Y, then Z, like Minecraft)
    private static Part M(Def d, Role role, float px, float py, float pz, float bx, float by, float bz, float w, float h, float dd,
                          int u, int v, string col, bool mirror = false, float inflate = 0f, float rx = 0f, float ry = 0f, float rz = 0f)
    {
      Vector3 max = new Vector3(px + bx + w, py + by + h, pz + bz + dd);
      Part p = P(d, role, new Vector3(-max.x, 24f - max.y, -max.z), new Vector3(w, h, dd), u, v, new Vector3(-px, 24f - py, -pz), col, mirror, inflate);
      if (rx != 0f || ry != 0f || rz != 0f)
        p.BaseRot = Quaternion.AngleAxis(rz * Mathf.Rad2Deg, Vector3.forward) * Quaternion.AngleAxis(ry * Mathf.Rad2Deg, Vector3.up) * Quaternion.AngleAxis(rx * Mathf.Rad2Deg, Vector3.right);
      return p;
    }

    // the same model as another mob (a different skin)
    private static void Copy(Def from, Def to, string recolour = null)
    {
      foreach (Part p in from.Parts)
        to.Parts.Add(new Part
        {
          Role = p.Role, Min = p.Min, Size = p.Size, Pivot = p.Pivot, U = p.U, V = p.V, Inflate = p.Inflate, RotX = p.RotX, Mirror = p.Mirror,
          Tex2 = p.Tex2, Glow = p.Glow, BaseRot = p.BaseRot, Col = recolour != null && !p.Glow ? SbCatalog.C(recolour) : p.Col
        });
      to.TexW = from.TexW; to.TexH = from.TexH; to.Quad = from.Quad; to.Biped = from.Biped;
    }

    private static void InitMore()
    {
      Def d;
      const float PI = Mathf.PI;

      // ---- animals
      d = D(SbMobType.Mooshroom, "mob_mooshroom", "Mooshroom", "A red mushroom cow.", "ANIMALS", 10, 0.9f, 12f, "cow_say", "cow_hurt", "cow_hurt", "entity/cow/red_mooshroom");
      Copy(Defs[SbMobType.Cow], d, "#a8221c");
      d = D(SbMobType.BrownMooshroom, "mob_mooshroom_brown", "Brown Mooshroom", "A brown mushroom cow.", "ANIMALS", 10, 0.9f, 12f, "cow_say", "cow_hurt", "cow_hurt", "entity/cow/brown_mooshroom");
      Copy(Defs[SbMobType.Cow], d, "#8a6440");

      d = D(SbMobType.Wolf, "mob_wolf", "Wolf", "Pads around, tail up.", "ANIMALS", 8, 1.3f, 6f, "wolf_say", "wolf_hurt", "wolf_death", "entity/wolf/wolf");
      d.Quad = true;
      M(d, Role.Head, -1f, 13.5f, -7f, -2f, -3f, -2f, 6f, 6f, 4f, 0, 0, "#d8d4cc");
      M(d, Role.Head, -1f, 13.5f, -7f, -2f, -5f, 0f, 2f, 2f, 1f, 16, 14, "#c8c4bc");
      M(d, Role.Head, -1f, 13.5f, -7f, 2f, -5f, 0f, 2f, 2f, 1f, 16, 14, "#c8c4bc");
      M(d, Role.Head, -1f, 13.5f, -7f, -0.5f, 0f, -5f, 3f, 3f, 4f, 0, 10, "#d8d4cc");
      M(d, Role.Body, 0f, 14f, 2f, -3f, -2f, -3f, 6f, 9f, 6f, 18, 14, "#d0ccc4", false, 0f, PI / 2f);
      M(d, Role.Body, -1f, 14f, -3f, -3f, -3f, -3f, 8f, 6f, 7f, 21, 0, "#d8d4cc", false, 0f, PI / 2f);
      M(d, Role.LegBR, -2.5f, 16f, 7f, 0f, 0f, -1f, 2f, 8f, 2f, 0, 18, "#c8c4bc");
      M(d, Role.LegBL, 0.5f, 16f, 7f, 0f, 0f, -1f, 2f, 8f, 2f, 0, 18, "#c8c4bc");
      M(d, Role.LegFR, -2.5f, 16f, -4f, 0f, 0f, -1f, 2f, 8f, 2f, 0, 18, "#c8c4bc");
      M(d, Role.LegFL, 0.5f, 16f, -4f, 0f, 0f, -1f, 2f, 8f, 2f, 0, 18, "#c8c4bc");
      M(d, Role.None, -1f, 12f, 8f, 0f, 0f, -1f, 2f, 8f, 2f, 9, 18, "#d0ccc4", false, 0f, PI / 5f);

      d = D(SbMobType.Ocelot, "mob_ocelot", "Ocelot", "A shy jungle cat.", "ANIMALS", 10, 1.4f, 3f, "cat_say", "cat_hurt", "cat_hurt", "entity/cat/ocelot");
      d.Quad = true;
      M(d, Role.Head, 0f, 15f, -9f, -2.5f, -2f, -3f, 5f, 4f, 5f, 0, 0, "#e0b050");
      M(d, Role.Head, 0f, 15f, -9f, -1.5f, 0f, -4f, 3f, 2f, 2f, 0, 24, "#e8c070");
      M(d, Role.Head, 0f, 15f, -9f, -2f, -3f, 0f, 1f, 1f, 2f, 0, 10, "#c09040");
      M(d, Role.Head, 0f, 15f, -9f, 1f, -3f, 0f, 1f, 1f, 2f, 6, 10, "#c09040");
      M(d, Role.Body, 0f, 12f, -10f, -2f, 3f, -8f, 4f, 16f, 6f, 20, 0, "#d8a848", false, 0f, PI / 2f);
      M(d, Role.None, 0f, 15f, 8f, -0.5f, 0f, 0f, 1f, 8f, 1f, 0, 15, "#d8a848", false, 0f, 0.9f);
      M(d, Role.None, 0f, 20f, 14f, -0.5f, 0f, 0f, 1f, 8f, 1f, 4, 15, "#d8a848", false, 0f, 1.73f);
      M(d, Role.LegBL, 1.1f, 18f, 5f, -1f, 0f, 1f, 2f, 6f, 2f, 8, 13, "#d8a848");
      M(d, Role.LegBR, -1.1f, 18f, 5f, -1f, 0f, 1f, 2f, 6f, 2f, 8, 13, "#d8a848");
      M(d, Role.LegFL, 1.2f, 14.1f, -5f, -1f, 0f, 0f, 2f, 10f, 2f, 40, 0, "#d8a848");
      M(d, Role.LegFR, -1.2f, 14.1f, -5f, -1f, 0f, 0f, 2f, 10f, 2f, 40, 0, "#d8a848");

      d = D(SbMobType.SnowGolem, "mob_snow_golem", "Snow Golem", "A snowman with a pumpkin head.", "MOBS", 4, 0.6f, 8f, "step_snow", "step_snow", "step_snow", "entity/snow_golem");
      d.TexH = 64;
      M(d, Role.Body, 0f, 24f, 0f, -6f, -12f, -6f, 12f, 12f, 12f, 0, 36, "#f4f8fa", false, -0.5f);
      M(d, Role.Body, 0f, 13f, 0f, -5f, -10f, -5f, 10f, 10f, 10f, 0, 16, "#f4f8fa", false, -0.5f);
      M(d, Role.Head, 0f, 4f, 0f, -4f, -8f, -4f, 8f, 8f, 8f, 0, 0, "#e08a20", false, -0.5f);
      Part armR = P(d, Role.None, new Vector3(4f, 17f, -1f), new Vector3(12f, 2f, 2f), 32, 0, new Vector3(5f, 18f, 0f), "#6a4a2a", false, -0.5f);
      armR.BaseRot = Quaternion.AngleAxis(35f, Vector3.forward);
      Part armL = P(d, Role.None, new Vector3(-16f, 17f, -1f), new Vector3(12f, 2f, 2f), 32, 0, new Vector3(-5f, 18f, 0f), "#6a4a2a", true, -0.5f);
      armL.BaseRot = Quaternion.AngleAxis(-35f, Vector3.forward);

      // ---- monsters
      d = D(SbMobType.Creeper, "mob_creeper", "Creeper", "Sneaks up on you... and blows up.", "MOBS", 20, 0.9f, 8f, "creeper_say", "creeper_say", "creeper_death", "entity/creeper/creeper");
      d.Quad = true; d.Hostile = true; d.Explodes = true;
      M(d, Role.Head, 0f, 6f, 0f, -4f, -8f, -4f, 8f, 8f, 8f, 0, 0, "#5ec05a");
      M(d, Role.Body, 0f, 6f, 0f, -4f, 0f, -2f, 8f, 12f, 4f, 16, 16, "#52b04e");
      M(d, Role.LegBR, -2f, 18f, 4f, -2f, 0f, -2f, 4f, 6f, 4f, 0, 16, "#4aa046");
      M(d, Role.LegBL, 2f, 18f, 4f, -2f, 0f, -2f, 4f, 6f, 4f, 0, 16, "#4aa046");
      M(d, Role.LegFR, -2f, 18f, -4f, -2f, 0f, -2f, 4f, 6f, 4f, 0, 16, "#4aa046");
      M(d, Role.LegFL, 2f, 18f, -4f, -2f, 0f, -2f, 4f, 6f, 4f, 0, 16, "#4aa046");

      d = D(SbMobType.Spider, "mob_spider", "Spider", "Eight legs, red eyes, comes after you.", "MOBS", 16, 1.4f, 10f, "spider_say", "spider_say", "spider_death", "entity/spider/spider");
      Spider(d);
      d = D(SbMobType.CaveSpider, "mob_cave_spider", "Cave Spider", "A small, quick spider.", "MOBS", 12, 1.6f, 4f, "spider_say", "spider_say", "spider_death", "entity/spider/cave_spider");
      Spider(d);
      d.Scale = 0.7f;
      foreach (Part p in d.Parts) p.Col = SbCatalog.C(p.Role == Role.Head ? "#1e4a52" : "#163a40");

      d = D(SbMobType.Husk, "mob_husk", "Husk", "A dried-out desert zombie.", "MOBS", 20, 0.9f, 10f, "husk_say", "husk_hurt", "husk_death", "entity/zombie/husk");
      d.TexH = 64; d.Biped = true; d.Hostile = true; d.ZombieArms = true; d.Scale = 1.0625f;
      Humanoid(d, "#9a8a5e", "#7a6a48", "#5a4a32", 4f, true);
      d = D(SbMobType.Drowned, "mob_drowned", "Drowned", "A soggy zombie from the sea.", "MOBS", 20, 0.9f, 10f, "drowned_say", "drowned_hurt", "drowned_death", "entity/zombie/drowned");
      d.TexH = 64; d.Biped = true; d.Hostile = true; d.ZombieArms = true;
      Humanoid(d, "#4aa8a0", "#3a8a6a", "#5a5aa0", 4f, true);

      d = D(SbMobType.ZombieVillager, "mob_zombie_villager", "Zombie Villager", "Hrrm. A villager that got bitten.", "MOBS", 20, 0.85f, 10f, "zvillager_say", "zvillager_hurt", "zvillager_death", "entity/zombie_villager/zombie_villager");
      d.TexH = 64; d.Biped = true; d.Hostile = true; d.ZombieArms = true;
      M(d, Role.Head, 0f, 0f, 0f, -4f, -10f, -4f, 8f, 10f, 8f, 0, 0, "#5a8a4a");
      M(d, Role.Head, 0f, 0f, 0f, -1f, -3f, -6f, 2f, 4f, 2f, 24, 0, "#4a7a3a");
      M(d, Role.Body, 0f, 0f, 0f, -4f, 0f, -3f, 8f, 12f, 6f, 16, 20, "#6b4a2a");
      M(d, Role.Body, 0f, 0f, 0f, -4f, 0f, -3f, 8f, 18f, 6f, 0, 38, "#5a3c22", false, 0.5f);
      M(d, Role.ArmR, -5f, 2f, 0f, -3f, -2f, -2f, 4f, 12f, 4f, 44, 22, "#5a8a4a");
      M(d, Role.ArmL, 5f, 2f, 0f, -1f, -2f, -2f, 4f, 12f, 4f, 44, 22, "#5a8a4a", true);
      M(d, Role.LegR, -2f, 12f, 0f, -2f, 0f, -2f, 4f, 12f, 4f, 0, 22, "#4a3020");
      M(d, Role.LegL, 2f, 12f, 0f, -2f, 0f, -2f, 4f, 12f, 4f, 0, 22, "#4a3020", true);

      d = D(SbMobType.Stray, "mob_stray", "Stray", "A frozen skeleton from the snow.", "MOBS", 20, 1f, 8f, "stray_say", "stray_hurt", "stray_death", "entity/skeleton/stray");
      Copy(Defs[SbMobType.Skeleton], d, "#a8b8bc");

      d = D(SbMobType.Slime, "mob_slime", "Slime", "Boing. Hops after you.", "MOBS", 16, 1.1f, 6f, "slime", "slime", "slime", "entity/slime/slime");
      d.Hops = true; d.Hostile = true; d.Scale = 1.6f;
      M(d, Role.Body, 0f, 0f, 0f, -3f, 17f, -3f, 6f, 6f, 6f, 0, 16, "#6cc04c");
      M(d, Role.None, 0f, 0f, 0f, -3.25f, 18f, -3.5f, 2f, 2f, 2f, 32, 0, "#1c3a14");
      M(d, Role.None, 0f, 0f, 0f, 1.25f, 18f, -3.5f, 2f, 2f, 2f, 32, 4, "#1c3a14");
      M(d, Role.None, 0f, 0f, 0f, 0f, 21f, -3.5f, 1f, 1f, 1f, 32, 8, "#1c3a14");

      d = D(SbMobType.Silverfish, "mob_silverfish", "Silverfish", "A little grey bug. Scuttles at you.", "MOBS", 8, 1.3f, 1.5f, "silverfish_say", "silverfish_hurt", "silverfish_death", "entity/silverfish");
      d.Hostile = true;
      Segments(d, new[] { new Vector3(3f, 2f, 2f), new Vector3(4f, 3f, 2f), new Vector3(6f, 4f, 3f), new Vector3(3f, 3f, 3f), new Vector3(2f, 2f, 3f), new Vector3(2f, 1f, 2f), new Vector3(1f, 1f, 2f) },
               new[] { 0, 0, 0, 4, 0, 9, 0, 16, 0, 22, 11, 0, 13, 4 }, "#8a8e94");

      // ---- the Nether
      d = D(SbMobType.ZombifiedPiglin, "mob_zombified_piglin", "Zombified Piglin", "Shuffles around. Leave it alone.", "NETHER MOBS", 20, 0.9f, 10f, "zpig_say", "zpig_hurt", "zpig_death", "entity/piglin/zombified_piglin");
      Copy(Defs[SbMobType.Piglin], d);
      d.ZombieArms = true;
      d = D(SbMobType.PiglinBrute, "mob_piglin_brute", "Piglin Brute", "A tough piglin guard. Comes after you.", "NETHER MOBS", 50, 1f, 12f, "brute_say", "brute_hurt", "brute_death", "entity/piglin/piglin_brute");
      Copy(Defs[SbMobType.Piglin], d);
      d.Hostile = true;
      d = D(SbMobType.WitherSkeleton, "mob_wither_skeleton", "Wither Skeleton", "A tall, black skeleton from the fortress.", "NETHER MOBS", 20, 1f, 12f, "wskeleton_say", "wskeleton_hurt", "wskeleton_death", "entity/skeleton/wither_skeleton");
      Copy(Defs[SbMobType.Skeleton], d, "#2a2a2a");
      d.Hostile = true; d.Scale = 1.2f;

      d = D(SbMobType.Blaze, "mob_blaze", "Blaze", "Floats on spinning rods of fire.", "NETHER MOBS", 20, 0.8f, 6f, "blaze_say", "blaze_hurt", "blaze_death", "entity/blaze");
      d.Hostile = true;
      M(d, Role.Head, 0f, 0f, 0f, -4f, -4f, -4f, 8f, 8f, 8f, 0, 0, "#f0c030");
      foreach (var ring in new[] { (9f, 26f, 0f, Role.Spin), (7f, 22f, 45f, Role.Spin2), (5f, 13f, 20f, Role.Spin) })
        for (int i = 0; i < 4; i++)
        {
          float a = (ring.Item3 + i * 90f) * Mathf.Deg2Rad;
          float cx = Mathf.Cos(a) * ring.Item1, cz = Mathf.Sin(a) * ring.Item1;
          P(d, ring.Item4, new Vector3(cx - 1f, ring.Item2 - 8f, cz - 1f), new Vector3(2f, 8f, 2f), 0, 16, new Vector3(0f, ring.Item2 - 4f, 0f), "#f0a020");
        }

      d = D(SbMobType.MagmaCube, "mob_magma_cube", "Magma Cube", "A hot, bouncy cube.", "NETHER MOBS", 16, 1.1f, 8f, "magma_say", "magma_say", "magma_say", "entity/slime/magmacube");
      d.Hops = true; d.Hostile = true; d.Scale = 1.4f;
      for (int i = 0; i < 8; i++)
      {
        int u = 0, v = i;
        if (i == 2) { u = 24; v = 10; } else if (i == 3) { u = 24; v = 19; }
        M(d, Role.Body, 0f, 0f, 0f, -4f, 16f + i, -4f, 8f, 1f, 8f, u, v, i == 3 || i == 4 ? "#f0a020" : "#4a1a10");
      }
      M(d, Role.Body, 0f, 0f, 0f, -2f, 18f, -2f, 4f, 4f, 4f, 0, 16, "#ff8a20");

      // ---- the End
      d = D(SbMobType.Endermite, "mob_endermite", "Endermite", "A tiny purple bug from the End.", "END MOBS", 8, 1.3f, 1.5f, "silverfish_say", "silverfish_hurt", "silverfish_death", "entity/endermite");
      d.Hostile = true;
      Segments(d, new[] { new Vector3(4f, 3f, 2f), new Vector3(6f, 4f, 5f), new Vector3(3f, 3f, 1f), new Vector3(1f, 2f, 1f) },
               new[] { 0, 0, 0, 5, 0, 14, 0, 18 }, "#3a2a4a");
      InitMore2();
      InitNew();   // SandBoxMobsNew.cs
    }

    // Minecraft's turn (radians, X then Y then Z) as a rotation
    private static Quaternion McRot(float rx, float ry, float rz)
    {
      return Quaternion.AngleAxis(rz * Mathf.Rad2Deg, Vector3.forward) * Quaternion.AngleAxis(ry * Mathf.Rad2Deg, Vector3.up) * Quaternion.AngleAxis(rx * Mathf.Rad2Deg, Vector3.right);
    }

    // a box on a turned parent part (Minecraft's child parts): the parent's pivot and turn,
    // then the child's offset and turn inside it
    private static Part MChild(Def d, Role role, Vector3 parentPivot, Quaternion parentRot, Vector3 offset, Quaternion rot,
                               float bx, float by, float bz, float w, float h, float dd, int u, int v, string col, bool mirror = false, float inflate = 0f)
    {
      Vector3 pv = parentPivot + parentRot * offset;
      Part p = M(d, role, pv.x, pv.y, pv.z, bx, by, bz, w, h, dd, u, v, col, mirror, inflate);
      p.BaseRot = parentRot * rot;
      return p;
    }

    // the illagers (64 x 64): a villager-like head and robe, legs, and either free arms or crossed ones
    private static void Illager(Def d, bool crossed, string skin, string robe)
    {
      d.TexH = 64; d.Biped = true; d.Hostile = true;
      M(d, Role.Head, 0f, 0f, 0f, -4f, -10f, -4f, 8f, 10f, 8f, 0, 0, skin);
      M(d, Role.Head, 0f, 0f, 0f, -1f, -3f, -6f, 2f, 4f, 2f, 24, 0, skin);
      M(d, Role.Body, 0f, 0f, 0f, -4f, 0f, -3f, 8f, 12f, 6f, 16, 20, robe);
      M(d, Role.Body, 0f, 0f, 0f, -4f, 0f, -3f, 8f, 20f, 6f, 0, 38, robe, false, 0.5f);
      if (crossed)
      {
        M(d, Role.None, 0f, 3f, -1f, -8f, -2f, -2f, 4f, 8f, 4f, 44, 22, skin, false, 0f, -0.75f);
        M(d, Role.None, 0f, 3f, -1f, 4f, -2f, -2f, 4f, 8f, 4f, 44, 22, skin, true, 0f, -0.75f);
        M(d, Role.None, 0f, 3f, -1f, -4f, 2f, -2f, 8f, 4f, 4f, 40, 38, skin, false, 0f, -0.75f);
      }
      else
      {
        M(d, Role.ArmR, -5f, 2f, 0f, -3f, -2f, -2f, 4f, 12f, 4f, 40, 46, skin);
        M(d, Role.ArmL, 5f, 2f, 0f, -1f, -2f, -2f, 4f, 12f, 4f, 40, 46, skin, true);
      }
      M(d, Role.LegR, -2f, 12f, 0f, -2f, 0f, -2f, 4f, 12f, 4f, 0, 22, "#2a2a30");
      M(d, Role.LegL, 2f, 12f, 0f, -2f, 0f, -2f, 4f, 12f, 4f, 0, 22, "#2a2a30", true);
    }

    // Minecraft's hoglin (128 x 64)
    private static void Hoglin(Def d, string col)
    {
      d.TexW = 128; d.TexH = 64; d.Quad = true; d.Hostile = true;
      Vector3 hp = new Vector3(0f, 2f, -12f);
      Quaternion hr = McRot(0.87266f, 0f, 0f);
      MChild(d, Role.Head, hp, hr, Vector3.zero, Quaternion.identity, -7f, -3f, -19f, 14f, 6f, 19f, 61, 1, col);
      MChild(d, Role.Head, hp, hr, new Vector3(-6f, -2f, -3f), McRot(0f, 0f, -0.6981f), -6f, -1f, -2f, 6f, 1f, 4f, 1, 1, col);
      MChild(d, Role.Head, hp, hr, new Vector3(6f, -2f, -3f), McRot(0f, 0f, 0.6981f), 0f, -1f, -2f, 6f, 1f, 4f, 1, 6, col);
      MChild(d, Role.Head, hp, hr, new Vector3(-7f, 2f, -12f), Quaternion.identity, -1f, -11f, -1f, 2f, 11f, 2f, 10, 13, "#e8e0c8");
      MChild(d, Role.Head, hp, hr, new Vector3(7f, 2f, -12f), Quaternion.identity, -1f, -11f, -1f, 2f, 11f, 2f, 1, 13, "#e8e0c8");
      M(d, Role.Body, 0f, 7f, 0f, -8f, -7f, -13f, 16f, 14f, 26f, 1, 1, col);
      M(d, Role.Body, 0f, -7f, -5f, 0f, 0f, -9f, 0f, 10f, 19f, 90, 33, col, false, 0.001f);
      M(d, Role.LegFR, -4f, 10f, -8.5f, -3f, 0f, -3f, 6f, 14f, 6f, 66, 42, col);
      M(d, Role.LegFL, 4f, 10f, -8.5f, -3f, 0f, -3f, 6f, 14f, 6f, 41, 42, col);
      M(d, Role.LegBR, -5f, 13f, 10f, -2.5f, 0f, -2.5f, 5f, 11f, 5f, 21, 45, col);
      M(d, Role.LegBL, 5f, 13f, 10f, -2.5f, 0f, -2.5f, 5f, 11f, 5f, 0, 45, col);
    }

    private static void InitMore2()
    {
      Def d;
      const float PI = Mathf.PI;

      // ---- animals
      d = D(SbMobType.Rabbit, "mob_rabbit", "Rabbit", "Hops around, nose twitching.", "ANIMALS", 3, 1.2f, 1f, "rabbit_say", "rabbit_hurt", "rabbit_death", "entity/rabbit/brown");
      d.Hops = true; d.HopSound = "rabbit_hop";
      M(d, Role.None, 3f, 17.5f, 3.7f, -1f, 5.5f, -3.7f, 2f, 1f, 7f, 26, 24, "#8a6a4a");
      M(d, Role.None, -3f, 17.5f, 3.7f, -1f, 5.5f, -3.7f, 2f, 1f, 7f, 8, 24, "#8a6a4a");
      M(d, Role.None, 3f, 17.5f, 3.7f, -1f, 0f, 0f, 2f, 4f, 5f, 30, 15, "#8a6a4a", false, 0f, -0.349f);
      M(d, Role.None, -3f, 17.5f, 3.7f, -1f, 0f, 0f, 2f, 4f, 5f, 16, 15, "#8a6a4a", false, 0f, -0.349f);
      M(d, Role.Body, 0f, 19f, 8f, -3f, -2f, -10f, 6f, 5f, 10f, 0, 0, "#9a7a5a", false, 0f, -0.349f);
      M(d, Role.None, 3f, 17f, -1f, -1f, 0f, -1f, 2f, 7f, 2f, 8, 15, "#8a6a4a", false, 0f, -0.192f);
      M(d, Role.None, -3f, 17f, -1f, -1f, 0f, -1f, 2f, 7f, 2f, 0, 15, "#8a6a4a", false, 0f, -0.192f);
      M(d, Role.Head, 0f, 16f, -1f, -2.5f, -4f, -5f, 5f, 4f, 5f, 32, 0, "#9a7a5a");
      M(d, Role.Head, 0f, 16f, -1f, -2.5f, -9f, -1f, 2f, 5f, 1f, 52, 0, "#9a7a5a", false, 0f, 0f, -0.262f);
      M(d, Role.Head, 0f, 16f, -1f, 0.5f, -9f, -1f, 2f, 5f, 1f, 58, 0, "#9a7a5a", false, 0f, 0f, 0.262f);
      M(d, Role.Head, 0f, 16f, -1f, -0.5f, -2.5f, -5.5f, 1f, 1f, 1f, 32, 9, "#e0a0a0");
      M(d, Role.None, 0f, 20f, 7f, -1.5f, -1.5f, 0f, 3f, 3f, 2f, 52, 6, "#e8e8e8", false, 0f, -0.349f);

      d = D(SbMobType.Fox, "mob_fox", "Fox", "Sly and quick.", "ANIMALS", 10, 1.4f, 4f, "fox_say", "fox_hurt", "fox_death", "entity/fox/fox");
      d.TexW = 48; d.TexH = 32; d.Quad = true;
      M(d, Role.Head, -1f, 16.5f, -3f, -3f, -2f, -5f, 8f, 6f, 6f, 1, 5, "#e07a30");
      M(d, Role.Head, -1f, 16.5f, -3f, -3f, -4f, -4f, 2f, 2f, 1f, 8, 1, "#e07a30");
      M(d, Role.Head, -1f, 16.5f, -3f, 3f, -4f, -4f, 2f, 2f, 1f, 15, 1, "#e07a30");
      M(d, Role.Head, -1f, 16.5f, -3f, -1f, 2.01f, -8f, 4f, 2f, 3f, 6, 18, "#f0e8e0");
      M(d, Role.Body, 0f, 16f, -6f, -3f, 3.999f, -3.5f, 6f, 11f, 6f, 24, 15, "#d87028", false, 0f, PI / 2f);
      M(d, Role.LegBR, -5f, 17.5f, 7f, 2f, 0.5f, -1f, 2f, 6f, 2f, 13, 24, "#3a2a20");
      M(d, Role.LegBL, -1f, 17.5f, 7f, 2f, 0.5f, -1f, 2f, 6f, 2f, 4, 24, "#3a2a20");
      M(d, Role.LegFR, -5f, 17.5f, 0f, 2f, 0.5f, -1f, 2f, 6f, 2f, 13, 24, "#3a2a20");
      M(d, Role.LegFL, -1f, 17.5f, 0f, 2f, 0.5f, -1f, 2f, 6f, 2f, 4, 24, "#3a2a20");
      MChild(d, Role.None, new Vector3(0f, 16f, -6f), McRot(PI / 2f, 0f, 0f), new Vector3(-4f, 15f, -1f), McRot(-0.05236f, 0f, 0f),
             2f, 0f, -1f, 4f, 9f, 5f, 30, 0, "#e07a30");

      d = D(SbMobType.Cat, "mob_cat", "Cat", "A tabby cat. Meow.", "ANIMALS", 10, 1.3f, 3f, "cat_say", "cat_hurt", "cat_hurt", "entity/cat/tabby", "entity/cat/black");
      Copy(Defs[SbMobType.Ocelot], d, "#8a7a6a");
      d.Scale = 0.8f;

      d = D(SbMobType.Llama, "mob_llama", "Llama", "Fluffy and a little grumpy.", "ANIMALS", 20, 0.9f, 20f, "llama_say", "llama_hurt", "llama_death", "entity/llama/brown", "entity/llama/creamy");
      d.TexW = 128; d.TexH = 64; d.Quad = true;
      M(d, Role.Head, 0f, 7f, -6f, -2f, -14f, -10f, 4f, 4f, 9f, 0, 0, "#8a6a4a");
      M(d, Role.Head, 0f, 7f, -6f, -4f, -16f, -6f, 8f, 18f, 6f, 0, 14, "#9a7a5a");
      M(d, Role.Head, 0f, 7f, -6f, -4f, -19f, -4f, 3f, 3f, 2f, 17, 0, "#8a6a4a");
      M(d, Role.Head, 0f, 7f, -6f, 1f, -19f, -4f, 3f, 3f, 2f, 17, 0, "#8a6a4a");
      M(d, Role.Body, 0f, 5f, 2f, -6f, -10f, -7f, 12f, 18f, 10f, 29, 0, "#9a7a5a", false, 0f, PI / 2f);
      M(d, Role.LegBR, -3.5f, 10f, 6f, -2f, 0f, -2f, 4f, 14f, 4f, 29, 29, "#8a6a4a");
      M(d, Role.LegBL, 3.5f, 10f, 6f, -2f, 0f, -2f, 4f, 14f, 4f, 29, 29, "#8a6a4a");
      M(d, Role.LegFR, -3.5f, 10f, -5f, -2f, 0f, -2f, 4f, 14f, 4f, 29, 29, "#8a6a4a");
      M(d, Role.LegFL, 3.5f, 10f, -5f, -2f, 0f, -2f, 4f, 14f, 4f, 29, 29, "#8a6a4a");

      // ---- monsters
      d = D(SbMobType.Witch, "mob_witch", "Witch", "Cackles and comes after you.", "MOBS", 26, 0.8f, 10f, "witch_say", "witch_hurt", "witch_death", "entity/witch");
      Copy(Defs[SbMobType.Villager], d, "#6a4a7a");
      d.TexH = 128; d.Hostile = true;
      M(d, Role.Head, 0f, 0f, 0f, -5f, -10.03f, -5f, 10f, 2f, 10f, 0, 64, "#2a2a2a");
      M(d, Role.Head, 0f, 0f, 0f, -3.25f, -14.03f, -3f, 7f, 4f, 7f, 0, 76, "#2a2a2a");
      M(d, Role.Head, 0f, 0f, 0f, -1.5f, -18.03f, -1f, 4f, 4f, 4f, 0, 87, "#2a2a2a");
      M(d, Role.Head, 0f, 0f, 0f, 0.25f, -20.03f, 1f, 1f, 2f, 1f, 0, 95, "#2a2a2a", false, 0.25f);

      d = D(SbMobType.Pillager, "mob_pillager", "Pillager", "A raider. Comes after you.", "MOBS", 24, 1f, 10f, "pillager_say", "pillager_hurt", "pillager_death", "entity/illager/pillager");
      Illager(d, false, "#a0a0a0", "#5a4a3a");
      d = D(SbMobType.Vindicator, "mob_vindicator", "Vindicator", "A grumpy raider with its arms up.", "MOBS", 24, 1.1f, 10f, "vindicator_say", "vindicator_hurt", "vindicator_death", "entity/illager/vindicator");
      Illager(d, false, "#a0a0a0", "#3a3a40");
      d.ZombieArms = true;
      d = D(SbMobType.Evoker, "mob_evoker", "Evoker", "A robed raider, arms folded.", "MOBS", 24, 0.9f, 10f, "evoker_say", "evoker_hurt", "evoker_death", "entity/illager/evoker");
      Illager(d, true, "#a0a0a0", "#2a2a30");
      d = D(SbMobType.Illusioner, "mob_illusioner", "Illusioner", "A blue-robed raider.", "MOBS", 32, 0.9f, 10f, "evoker_say", "evoker_hurt", "evoker_death", "entity/illager/illusioner");
      Illager(d, true, "#a0a0a0", "#2a4a8a");

      d = D(SbMobType.Bogged, "mob_bogged", "Bogged", "A mossy swamp skeleton.", "MOBS", 16, 1f, 8f, "skeleton_say", "skeleton_hurt", "skeleton_death", "entity/skeleton/bogged");
      Copy(Defs[SbMobType.Skeleton], d, "#7a8a5a");
      d.Hostile = true;

      // ---- the Nether
      d = D(SbMobType.Hoglin, "mob_hoglin", "Hoglin", "A big tusked beast. Charges at you.", "NETHER MOBS", 40, 1f, 30f, "hoglin_say", "hoglin_hurt", "hoglin_death", "entity/hoglin/hoglin");
      Hoglin(d, "#b8705a");
      d = D(SbMobType.Zoglin, "mob_zoglin", "Zoglin", "A zombified hoglin. Angry at everything.", "NETHER MOBS", 40, 1.1f, 30f, "zoglin_say", "zoglin_hurt", "zoglin_death", "entity/hoglin/zoglin");
      Hoglin(d, "#c8807a");

      // ---- the End
      d = D(SbMobType.Shulker, "mob_shulker", "Shulker", "A box that hides a little head. Stays put.", "END MOBS", 30, 0f, 40f, "shulker_say", "shulker_hurt", "shulker_death", "entity/shulker/shulker");
      d.TexH = 64;
      M(d, Role.Body, 0f, 24f, 0f, -8f, -16f, -8f, 16f, 12f, 16f, 0, 0, "#9a6a9a");
      M(d, Role.Body, 0f, 24f, 0f, -8f, -8f, -8f, 16f, 8f, 16f, 0, 28, "#8a5a8a");
      M(d, Role.Head, 0f, 12f, 0f, -3f, 0f, -3f, 6f, 6f, 6f, 0, 52, "#d8e8a0");
    }

    // Minecraft's spider (64 x 32): the head, a neck, the body and 8 legs
    private static void Spider(Def d)
    {
      const float PI = Mathf.PI;
      d.Quad = true; d.Hostile = true; d.SpiderLegs = true;
      M(d, Role.Head, 0f, 15f, -3f, -4f, -4f, -8f, 8f, 8f, 8f, 32, 4, "#3a3028");
      M(d, Role.Body, 0f, 15f, 0f, -3f, -3f, -3f, 6f, 6f, 6f, 0, 0, "#2e2620");
      M(d, Role.Body, 0f, 15f, 9f, -5f, -4f, -6f, 10f, 8f, 12f, 0, 12, "#2e2620");
      // (z, turn, tilt) from the back legs to the front; legs on the same beat share a role
      var legs = new[] { (2f, PI / 4f, PI / 4f, Role.LegBR, Role.LegFL), (1f, PI / 8f, 0.58f, Role.LegFR, Role.LegBL),
                         (0f, -PI / 8f, 0.58f, Role.LegBR, Role.LegFL), (-1f, -PI / 4f, PI / 4f, Role.LegFR, Role.LegBL) };
      foreach (var l in legs)
      {
        M(d, l.Item4, -4f, 15f, l.Item1, -15f, -1f, -1f, 16f, 2f, 2f, 18, 0, "#2a221c", false, 0f, 0f, l.Item2, -l.Item3);
        M(d, l.Item5, 4f, 15f, l.Item1, -1f, -1f, -1f, 16f, 2f, 2f, 18, 0, "#2a221c", true, 0f, 0f, -l.Item2, l.Item3);
      }
    }

    // a bug made of boxes one behind the other (silverfish, endermite); uvs in pairs
    private static void Segments(Def d, Vector3[] sizes, int[] uvs, string col)
    {
      float z = -3.5f;
      for (int i = 0; i < sizes.Length; i++)
      {
        Vector3 s = sizes[i];
        M(d, i == 0 ? Role.Head : Role.Body, 0f, 24f - s.y, z, -s.x * 0.5f, 0f, -s.z * 0.5f, s.x, s.y, s.z, uvs[i * 2], uvs[i * 2 + 1], col);
        if (i < sizes.Length - 1) z += (s.z + sizes[i + 1].z) * 0.5f;
      }
    }

    private static void Humanoid(Def d, string skin, string shirt, string pants, float armW, bool wide)
    {
      P(d, Role.Head, new Vector3(-4f, 24f, -4f), new Vector3(8f, 8f, 8f), 0, 0, new Vector3(0f, 24f, 0f), skin);
      P(d, Role.Body, new Vector3(-4f, 12f, -2f), new Vector3(8f, 12f, 4f), 16, 16, new Vector3(0f, 12f, 0f), shirt);
      P(d, Role.ArmR, new Vector3(4f, 12f, -2f), new Vector3(armW, 12f, 4f), 40, 16, new Vector3(6f, 22f, 0f), skin);
      P(d, Role.ArmL, new Vector3(-4f - armW, 12f, -2f), new Vector3(armW, 12f, 4f), 32, 48, new Vector3(-6f, 22f, 0f), skin);
      P(d, Role.LegR, new Vector3(0f, 0f, -2f), new Vector3(4f, 12f, 4f), 0, 16, new Vector3(2f, 12f, 0f), pants);
      P(d, Role.LegL, new Vector3(-4f, 0f, -2f), new Vector3(4f, 12f, 4f), 16, 48, new Vector3(-2f, 12f, 0f), pants);
    }

    // ---------------------------------------------------------------- catalog
    internal static void AddToCatalog(Action<SbItem> add)
    {
      Init();
      foreach (Def d in Defs.Values)
      {
        Def def = d;
        add(new SbItem
        {
          Id = d.Id, Name = d.Name, Desc = d.Desc + " Hit it to hurt it.", Tab = SbTab.Minecraft, Section = d.Section, Kind = SbKind.Mob, Mob = d.Type,
          Hidden = d.Hidden,
          DefaultAnchored = false, Mass = d.Mass, Col = SbCol.Box,
          Build = k => BuildPalette(k, def)
        });
      }
    }

    // the plain-coloured model (the menu icon, the placement preview, and the mob when there is no Minecraft)
    internal static void BuildPalette(SbMeshKit k, Def d)
    {
      foreach (Part p in d.Parts)
      {
        Vector3 size = p.Size + Vector3.one * p.Inflate * 2f;
        Vector3 c = p.Min + p.Size * 0.5f;
        Quaternion r = p.RotX ? Quaternion.Euler(90f, 0f, 0f) : Quaternion.identity;
        Vector3 at = p.RotX ? p.Pivot + r * c : p.Pivot + p.BaseRot * (c - p.Pivot);
        if (p.RotX) at = p.Pivot + r * c;
        k.Box(at * PixelScale * d.Scale, size * PixelScale * d.Scale, (p.RotX ? r : p.BaseRot), p.Col, p.Glow);
      }
    }

    // ---------------------------------------------------------------- textures
    private static readonly Dictionary<string, Material> mats = new Dictionary<string, Material>();

    // the first of the candidate textures the player's Minecraft has, as a material
    internal static Material TextureMaterial(string[] names, out Vector2 texSize, int declW, int declH)
    {
      texSize = new Vector2(declW, declH);
      if (names == null) return null;
      foreach (string n in names)
      {
        Texture2D tex = SbMinecraft.Image("textures/" + n + ".png");
        if (tex == null) continue;
        // the layout is in the model's pixels; a texture twice as big has twice the pixels
        float per = tex.width / (float)declW;
        texSize = new Vector2(declW, tex.height / Mathf.Max(0.01f, per));
        Material m;
        if (!mats.TryGetValue(n, out m) || m == null)
        {
          m = new Material(SbMats.Lit) { name = "Unknowns Mob " + n, hideFlags = HideFlags.DontSave };
          if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", tex);
          if (m.HasProperty("_MainTex")) m.SetTexture("_MainTex", tex);
          if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", Color.white);
          if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.05f);
          if (m.HasProperty("_AlphaClip")) { m.SetFloat("_AlphaClip", 1f); m.SetFloat("_Cutoff", 0.5f); m.EnableKeyword("_ALPHATEST_ON"); }
          mats[n] = m;
        }
        return m;
      }
      return null;
    }

    // a new texture pack: the mob materials take its skins (new mobs get them too)
    internal static void RefreshTextures()
    {
      foreach (KeyValuePair<string, Material> kv in mats)
      {
        if (kv.Value == null) continue;
        Texture2D tex = SbMinecraft.Image("textures/" + kv.Key + ".png");
        if (tex == null) continue;
        if (kv.Value.HasProperty("_BaseMap")) kv.Value.SetTexture("_BaseMap", tex);
        if (kv.Value.HasProperty("_MainTex")) kv.Value.SetTexture("_MainTex", tex);
      }
    }

    // one box with Minecraft's texture layout (u, v in the model's pixels; texture
    // rows from the top), its vertices relative to the part's pivot
    internal static Mesh PartMesh(Part p, Vector2 tex)
    {
      var v = new List<Vector3>(); var n = new List<Vector3>(); var uv = new List<Vector2>(); var t = new List<int>();
      float w = p.Size.x, h = p.Size.y, dd = p.Size.z;
      Vector3 lo = p.Min - Vector3.one * p.Inflate, hi = p.Min + p.Size + Vector3.one * p.Inflate;
      if (p.RotX)
      {
        // defined like Minecraft's (w, h, d) then turned 90 degrees about X around the pivot
        lo = p.Min - Vector3.one * p.Inflate; hi = p.Min + p.Size + Vector3.one * p.Inflate;
      }
      Vector2 UV(float px, float py) { return new Vector2(px / tex.x, 1f - py / tex.y); }
      void Face(Vector3 bl, Vector3 br, Vector3 tr, Vector3 tl, float u0, float v0, float uw, float vh, Vector3 nrm)
      {
        float a = p.Mirror ? u0 + uw : u0, b = p.Mirror ? u0 : u0 + uw;
        int i = v.Count;
        v.Add(bl); v.Add(br); v.Add(tr); v.Add(tl);
        for (int k = 0; k < 4; k++) n.Add(nrm);
        uv.Add(UV(a, v0 + vh)); uv.Add(UV(b, v0 + vh)); uv.Add(UV(b, v0)); uv.Add(UV(a, v0));
        if (Vector3.Dot(Vector3.Cross(br - bl, tr - bl), nrm) >= 0f) { t.Add(i); t.Add(i + 1); t.Add(i + 2); t.Add(i); t.Add(i + 2); t.Add(i + 3); }
        else { t.Add(i); t.Add(i + 2); t.Add(i + 1); t.Add(i); t.Add(i + 3); t.Add(i + 2); }
      }
      float x0 = lo.x, y0 = lo.y, z0 = lo.z, x1 = hi.x, y1 = hi.y, z1 = hi.z;
      int U = p.U, V = p.V;
      // front +Z, right side +X (Minecraft's first strip), left -X, back -Z
      Face(new Vector3(x1, y0, z1), new Vector3(x0, y0, z1), new Vector3(x0, y1, z1), new Vector3(x1, y1, z1), U + dd, V + dd, w, h, Vector3.forward);
      Face(new Vector3(x1, y0, z0), new Vector3(x1, y0, z1), new Vector3(x1, y1, z1), new Vector3(x1, y1, z0), U, V + dd, dd, h, Vector3.right);
      Face(new Vector3(x0, y0, z1), new Vector3(x0, y0, z0), new Vector3(x0, y1, z0), new Vector3(x0, y1, z1), U + dd + w, V + dd, dd, h, Vector3.left);
      Face(new Vector3(x0, y0, z0), new Vector3(x1, y0, z0), new Vector3(x1, y1, z0), new Vector3(x0, y1, z0), U + dd + w + dd, V + dd, w, h, Vector3.back);
      Face(new Vector3(x1, y1, z1), new Vector3(x0, y1, z1), new Vector3(x0, y1, z0), new Vector3(x1, y1, z0), U + dd, V, w, dd, Vector3.up);
      Face(new Vector3(x1, y0, z1), new Vector3(x0, y0, z1), new Vector3(x0, y0, z0), new Vector3(x1, y0, z0), U + dd + w, V, w, dd, Vector3.down);
      // relative to the pivot (and turned for the lying bodies)
      Quaternion r = p.RotX ? Quaternion.Euler(90f, 0f, 0f) : Quaternion.identity;
      for (int i = 0; i < v.Count; i++)
      {
        v[i] = p.RotX ? r * v[i] : v[i] - p.Pivot;
        n[i] = r * n[i];
      }
      var m = new Mesh { name = "Unknowns Mob part", hideFlags = HideFlags.DontSave };
      m.SetVertices(v); m.SetNormals(n); m.SetUVs(0, uv); m.SetTriangles(t, 0);
      m.RecalculateBounds();
      return m;
    }

    // the same box in plain colour (no Minecraft texture)
    internal static Mesh PalettePartMesh(Part p)
    {
      var k = new SbMeshKit();
      Vector3 size = p.Size + Vector3.one * p.Inflate * 2f;
      Vector3 c = p.Min + p.Size * 0.5f;
      if (p.RotX) k.Box(Quaternion.Euler(90f, 0f, 0f) * c, size, Quaternion.Euler(90f, 0f, 0f), p.Col, p.Glow);
      else k.Box(c - p.Pivot, size, p.Col, p.Glow);
      return k.ToMesh("Unknowns Mob part (plain)");
    }
  }

  // ==================================================================== world side
  internal sealed partial class SandBoxWorld
  {
    private readonly HashSet<int> naturalMobs = new HashSet<int>();   // ids of your natural (not placed) mobs

    private SbObject CreateMob(SbObject o, long key, Vector3 pos, Quaternion rot, float scale)
    {
      SbMobDefs.Init();
      SbMobDefs.Def d;
      if (!SbMobDefs.Defs.TryGetValue(o.Item.Mob, out d)) return null;
      var go = new GameObject("SB Mob " + d.Name);
      go.transform.SetParent(objRoot, false);
      o.Go = go; o.T = go.transform;
      Vector3 fwd = Vector3.ProjectOnPlane(rot * Vector3.forward, Vector3.up);
      o.T.SetPositionAndRotation(pos, fwd.sqrMagnitude > 1e-4f ? Quaternion.LookRotation(fwd.normalized, Vector3.up) : Quaternion.identity);
      float sc = Mathf.Clamp(scale, 0.25f, 4f);
      o.T.localScale = Vector3.one * SbMobDefs.PixelScale * d.Scale * sc;

      Vector2 texSize, tex2Size;
      Material main = SbMobDefs.TextureMaterial(d.Tex, out texSize, d.TexW, d.TexH);
      Material second = SbMobDefs.TextureMaterial(d.Tex2, out tex2Size, d.Tex2W, d.Tex2H);
      var parts = new Dictionary<SbMobDefs.Role, List<Transform>>();
      var renderers = new List<Renderer>();
      Bounds b = new Bounds(Vector3.zero, Vector3.zero);
      bool first = true;
      foreach (SbMobDefs.Part p in d.Parts)
      {
        var pg = new GameObject(p.Role.ToString());
        pg.transform.SetParent(o.T, false);
        pg.transform.localPosition = p.RotX ? p.Pivot : p.Pivot;
        pg.transform.localRotation = p.BaseRot;
        Material m = p.Glow ? SbMats.Glow : p.Tex2 ? second : main;
        Mesh mesh;
        if (m != null && !p.Glow) mesh = SbMobDefs.PartMesh(p, p.Tex2 ? tex2Size : texSize);
        else
        {
          // no texture: plain colours (the fur keeps the sheep white)
          if (p.Tex2 && second == null && main != null) { UnityEngine.Object.Destroy(pg); continue; }
          mesh = SbMobDefs.PalettePartMesh(p);
          m = p.Glow ? SbMats.Glow : SbMats.Lit;
        }
        pg.AddComponent<MeshFilter>().sharedMesh = mesh;
        var mr = pg.AddComponent<MeshRenderer>();
        mr.sharedMaterial = m;
        renderers.Add(mr);
        if (p.Role != SbMobDefs.Role.None && p.Role != SbMobDefs.Role.Body)
        {
          List<Transform> l;
          if (!parts.TryGetValue(p.Role, out l)) { l = new List<Transform>(); parts[p.Role] = l; }
          l.Add(pg.transform);
        }
        if (!p.Glow)
        {
          Bounds pb = new Bounds(p.RotX ? p.Pivot + Quaternion.Euler(90f, 0f, 0f) * (p.Min + p.Size * 0.5f) : p.Min + p.Size * 0.5f,
                                 p.RotX ? new Vector3(p.Size.x, p.Size.z, p.Size.y) : p.Size);
          if (!p.RotX && p.BaseRot != Quaternion.identity)
          {
            // a turned box (lying bodies, spider legs) takes the room it takes once turned
            Vector3 e = p.Size * 0.5f;
            Vector3 ax = p.BaseRot * new Vector3(e.x, 0f, 0f), ay = p.BaseRot * new Vector3(0f, e.y, 0f), az = p.BaseRot * new Vector3(0f, 0f, e.z);
            Vector3 ext = new Vector3(Mathf.Abs(ax.x) + Mathf.Abs(ay.x) + Mathf.Abs(az.x), Mathf.Abs(ax.y) + Mathf.Abs(ay.y) + Mathf.Abs(az.y), Mathf.Abs(ax.z) + Mathf.Abs(ay.z) + Mathf.Abs(az.z));
            pb = new Bounds(p.Pivot + p.BaseRot * (p.Min + e - p.Pivot), ext * 2f);
          }
          if (first) { b = pb; first = false; } else b.Encapsulate(pb);
        }
      }
      o.LocalBounds = b;

      // a capsule for people, a box for animals; slippery so they slide along walls
      var cg = new GameObject("col");
      cg.transform.SetParent(o.T, false);
      o.ColGo = cg;
      Collider col;
      if (d.Biped)
      {
        var cc = cg.AddComponent<CapsuleCollider>();
        cc.center = new Vector3(0f, b.size.y * 0.5f, 0f);
        cc.height = b.size.y;
        cc.radius = Mathf.Min(Mathf.Max(b.extents.x * 0.6f, b.extents.z), b.size.y * 0.5f);
        col = cc;
      }
      else
      {
        var bc = cg.AddComponent<BoxCollider>();
        bc.center = b.center; bc.size = b.size;
        col = bc;
      }
      col.sharedMaterial = new PhysicsMaterial("SB Mob") { dynamicFriction = 0.1f, staticFriction = 0.1f, frictionCombine = PhysicsMaterialCombine.Minimum };
      o.Cols.Add(col); myColliders.Add(col);

      MakeDynamic(o, Vector3.zero);
      if (o.Rb != null)
      {
        o.Rb.constraints = RigidbodyConstraints.FreezeRotation;
        o.Rb.linearDamping = 0.2f;
      }
      o.Go.AddComponent<SbRelay>().Obj = o;
      var mob = o.Go.AddComponent<SbMob>();
      mob.Init(this, o, d, parts, renderers);
      o.Mob = mob;
      objects[key] = o;
      List.Add(o);
      if (o.Mine) MineCount++;
      if (!ShowOthers && !o.Mine && !o.Generated) o.Go.SetActive(false);
      ApplySolid(o);
      if (PopNext) SbPopIn.Play(o.T);
      return o;
    }

    // the nearest player (you or anyone else) within `max` of p
    internal bool NearestPlayer(Vector3 p, float max, out Vector3 at)
    {
      at = Vector3.zero;
      float best = max * max;
      bool found = false;
      try
      {
        GTPlayer me = GTPlayer.Instance;
        if (me != null && me.headCollider != null)
        {
          Vector3 h = me.headCollider.transform.position;
          float d = (h - p).sqrMagnitude;
          if (d < best) { best = d; at = h; found = true; }
        }
        foreach (VRRig r in Rigs())
        {
          if (r == null || r.isOfflineVRRig || !r.gameObject.activeInHierarchy) continue;
          Vector3 h = r.headMesh != null ? r.headMesh.transform.position : r.transform.position;
          float d = (h - p).sqrMagnitude;
          if (d < best) { best = d; at = h; found = true; }
        }
      }
      catch { }
      return found;
    }

    // ---------------------------------------------------------------- natural mobs
    // piglins around you in the Nether, endermen on the End island (networked: everyone sees them)
    private float nextNaturalMob;

    private void UpdateNaturalMobs(Vector3 head)
    {
      if (Time.time < nextNaturalMob) return;
      nextNaturalMob = Time.time + 1.5f;
      SbMobType want = InNether ? SbMobType.Piglin : InEnd ? SbMobType.Enderman : SbMobType.None;
      // the Nether also has zombified piglins and magma cubes
      // the Nether's mobs by biome and structure (SandBoxNetherBiomes.cs)
      SbMobType[] kinds = InNether ? AllNetherMobs : new[] { want };
      // natural mobs are yours and networked, so everyone sees the same ones;
      // the cap counts everyone's mobs around you, so two players don't make double
      int count = 0;
      for (int i = List.Count - 1; i >= 0; i--)
      {
        SbObject o = List[i];
        if (o.Mob == null || o.T == null) continue;
        bool mineNatural = o.Mine && naturalMobs.Contains(o.Id);
        // yours: left the dimension, or far behind: gone (for everyone)
        if (mineNatural && (Array.IndexOf(kinds, o.Item.Mob) < 0 || want == SbMobType.None || (o.T.position - head).sqrMagnitude > 70f * 70f)) { naturalMobs.Remove(o.Id); Remove(o, true, false); continue; }
        if (Array.IndexOf(kinds, o.Item.Mob) >= 0 && want != SbMobType.None && (o.T.position - head).sqrMagnitude < 45f * 45f) count++;
      }
      int max = want == SbMobType.Piglin ? 9 : want == SbMobType.Enderman ? 5 : 0;
      if (InNether) { SbMobType[] here = NetherMobsAt(head); want = here[UnityEngine.Random.Range(0, here.Length)]; }
      if (count >= max || MineCount >= MaxMine) return;
      SbItem item = null;
      foreach (SbItem it in SbCatalog.All) if (it.Kind == SbKind.Mob && it.Mob == want) { item = it; break; }
      if (item == null) return;
      // somewhere 12 - 35 m away, on the ground
      for (int tries = 0; tries < 6; tries++)
      {
        float a = UnityEngine.Random.value * Mathf.PI * 2f, r = UnityEngine.Random.Range(12f, 35f);
        Vector3 p = head + new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r);
        float gy;
        if (InNether)
        {
          // the mob is the one that lives where it appears (fortress mobs only on
          // the fortress, standing on its deck)
          SbMobType[] there = NetherMobsAt(p);
          SbMobType kind = there[UnityEngine.Random.Range(0, there.Length)];
          item = null;
          foreach (SbItem it in SbCatalog.All) if (it.Kind == SbKind.Mob && it.Mob == kind) { item = it; break; }
          if (item == null) continue;
          if (kind == SbMobType.Blaze || kind == SbMobType.WitherSkeleton) p.y = NetherOrigin.y + (FortressY + 1) * NetherBlock + 0.05f;
          else
          {
            if (!NetherGround(p, out gy)) continue;
            p.y = gy + 0.05f;
          }
        }
        else
        {
          RaycastHit hit;
          if (!Physics.Raycast(new Vector3(p.x, EndOrigin.y + 60f, p.z), Vector3.down, out hit, 80f, ~0, QueryTriggerInteraction.Ignore)) continue;
          p = hit.point + Vector3.up * 0.05f;
        }
        // only where the ground is built (a collider under it)
        if (!Physics.Raycast(p + Vector3.up * 0.5f, Vector3.down, 1.5f, ~0, QueryTriggerInteraction.Ignore)) continue;
        SbObject m = Create(0, nextId++, item, p, Quaternion.Euler(0f, UnityEngine.Random.value * 360f, 0f), 1f, false, 0, Vector3Int.zero);
        if (m != null) { naturalMobs.Add(m.Id); SbNet.QueueSpawn(m); }
        return;
      }
    }

    private void UpdateMobsWorld()
    {
      GTPlayer p = null;
      try { p = GTPlayer.Instance; } catch { }
      if (p == null || p.headCollider == null) return;
      UpdateNaturalMobs(p.headCollider.transform.position);
    }

    internal void KillMob(SbObject o)
    {
      if (o == null || o.T == null) return;
      Vector3 c = o.Center;
      SbFx.Burst(c, 22, new Color(0.9f, 0.9f, 0.9f, 0.9f), new Color(0.6f, 0.6f, 0.6f, 0.7f), 0.3f, 1.2f, 0.06f, 0.14f, 0.5f, 0.9f, -0.05f, false, 0.25f, Vector3.up, 60f, 2f, 0.6f, true);
      Remove(o, true, false);
    }
  }

  // ==================================================================== one mob
  internal sealed partial class SbMob : MonoBehaviour
  {
    private SandBoxWorld world;
    private SbObject obj;
    private SbMobDefs.Def def;
    private Dictionary<SbMobDefs.Role, List<Transform>> parts;
    private List<Renderer> renderers;
    private readonly Dictionary<Transform, Quaternion> baseRot = new Dictionary<Transform, Quaternion>();
    private float hp, phase, sayAt, decideAt, hurtFlash, tpAt, stepAt, knockAt;
    private float hopAt, fuseAt;
    private Vector3 target, lastPos, vel;
    private bool walking, chasing, flashing;
    private float headYaw;
    private MaterialPropertyBlock block;

    private bool Simulated { get { return obj != null && (obj.Mine || obj.Generated); } }

    internal void Init(SandBoxWorld w, SbObject o, SbMobDefs.Def d, Dictionary<SbMobDefs.Role, List<Transform>> p, List<Renderer> r)
    {
      world = w; obj = o; def = d; parts = p; renderers = r;
      hp = d.Hp;
      foreach (List<Transform> l in p.Values) foreach (Transform t in l) baseRot[t] = t.localRotation;
      sayAt = Time.time + UnityEngine.Random.Range(2f, 8f);
      decideAt = Time.time + UnityEngine.Random.Range(0.5f, 2f);
      tpAt = Time.time + UnityEngine.Random.Range(8f, 20f);
      lastPos = transform.position;
      target = transform.position;
      block = new MaterialPropertyBlock();
    }

    // ---------------------------------------------------------------- damage
    internal void Hurt(Vector3 impulse, Vector3 point)
    {
      Damage(Mathf.Clamp(impulse.magnitude * 1.5f, 2f, 20f), point);
    }

    internal void Damage(float amount, Vector3 point)
    {
      if (obj == null || !Simulated || hp <= 0f) return;
      hp -= amount;
      hurtFlash = 0.3f;
      if (def.Angers) angry = true;   // bees, polar bears and goats fight back
      SbFx.PlayMc(def.Hurt, SbSound.Hit, transform.position, 1f, UnityEngine.Random.Range(0.9f, 1.1f), 16f);
      // run away (animals) / turn on whoever hit it
      decideAt = 0f;
      if (hp <= 0f)
      {
        SbFx.PlayMc(def.Death, SbSound.Hit, transform.position, 1f, 1f, 18f);
        world.OnMobDeath(obj, def);
        world.KillMob(obj);
      }
    }

    // ---------------------------------------------------------------- brain (owner only)
    private void FixedUpdate()
    {
      if (!Simulated || obj.Rb == null || obj.Rb.isKinematic || obj.Held) return;
      Rigidbody rb = obj.Rb;
      float scale = Mathf.Max(0.25f, obj.Scale);
      float now = Time.time;
      Vector3 pos = transform.position;
      // flying, swimming, the dragon, still things (SandBoxMobBrain.cs)
      if (BrainSpecial(rb, scale, now, pos)) return;

      // what to do next
      Vector3 player;
      bool near = world.NearestPlayer(pos, Angry ? 16f : 4f, out player);
      chasing = Angry && near;
      if (chasing) { target = player; walking = (player - pos).sqrMagnitude > 1.2f * 1.2f * scale * scale; }
      // archers stop to shoot
      MobRanged(player, chasing, now, pos);
      if (def.Shoots && chasing && (player - pos).sqrMagnitude < 7f * 7f * scale * scale) walking = false;
      else if (now >= decideAt)
      {
        decideAt = now + UnityEngine.Random.Range(3f, 9f);
        walking = UnityEngine.Random.value < 0.6f || hurtFlash > 0f;
        float a = UnityEngine.Random.value * Mathf.PI * 2f, r = UnityEngine.Random.Range(2f, 7f) * scale;
        target = pos + new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r);
      }
      // villagers stop to look at you
      if (def.Type == SbMobType.Villager && near) walking = false;

      Vector3 to = Vector3.ProjectOnPlane(target - pos, Vector3.up);
      if (to.magnitude < 0.3f * scale && !chasing) walking = false;
      bool grounded = Physics.Raycast(pos + Vector3.up * 0.15f * scale, Vector3.down, 0.3f * scale, ~0, QueryTriggerInteraction.Ignore);
      float speed = def.Speed * scale * (chasing ? 1.5f : 1f) * (hurtFlash > 0f && !def.Hostile ? 2f : 1f);
      Vector3 v = rb.linearVelocity;
      if (walking && to.sqrMagnitude > 1e-4f && def.Hops)
        rb.MoveRotation(Quaternion.Slerp(rb.rotation, Quaternion.LookRotation(to.normalized, Vector3.up), 1f - Mathf.Exp(-6f * Time.fixedDeltaTime)));
      else if (walking && to.sqrMagnitude > 1e-4f)
      {
        Vector3 dir = to.normalized;
        Vector3 hv = Vector3.MoveTowards(new Vector3(v.x, 0f, v.z), dir * speed, 8f * Time.fixedDeltaTime);
        rb.linearVelocity = new Vector3(hv.x, v.y, hv.z);
        Quaternion face = Quaternion.LookRotation(dir, Vector3.up);
        rb.MoveRotation(Quaternion.Slerp(rb.rotation, face, 1f - Mathf.Exp(-6f * Time.fixedDeltaTime)));
        // blocked by a step: hop up
        if (grounded && new Vector3(v.x, 0f, v.z).magnitude < speed * 0.25f &&
            Physics.Raycast(pos + Vector3.up * 0.2f * scale, dir, 0.5f * scale, ~0, QueryTriggerInteraction.Ignore))
          rb.linearVelocity = new Vector3(hv.x, 4.2f * Mathf.Sqrt(scale), hv.z);
      }
      else if (grounded)
      {
        rb.linearVelocity = new Vector3(v.x * 0.85f, v.y, v.z * 0.85f);
      }
      // slimes and magma cubes get about by hopping
      if (def.Hops)
      {
        if (grounded)
        {
          rb.linearVelocity = new Vector3(v.x * 0.6f, rb.linearVelocity.y, v.z * 0.6f);
          if (walking && to.sqrMagnitude > 1e-4f && now >= hopAt)
          {
            hopAt = now + UnityEngine.Random.Range(chasing ? 0.6f : 1.2f, chasing ? 1.1f : 2.5f);
            Vector3 dir = to.normalized;
            rb.linearVelocity = dir * speed * 1.6f + Vector3.up * (def.Type == SbMobType.MagmaCube ? 5.5f : 4.5f) * Mathf.Sqrt(scale);
            SbFx.PlayMc(def.HopSound ?? (def.Type == SbMobType.MagmaCube ? "magma_jump" : "step_slime"), SbSound.Squeak, pos, 0.6f, UnityEngine.Random.Range(0.9f, 1.1f), 14f);
          }
        }
      }

      // creepers hiss when they reach you, then blow up
      if (def.Explodes)
      {
        float dist = chasing ? (player - pos).magnitude : 99f;
        if (fuseAt <= 0f && dist < 2.4f * scale)
        {
          fuseAt = now + 1.5f;
          SbFx.PlayMc("creeper_fuse", SbSound.Suck, pos, 1f, 1f, 18f);
        }
        else if (fuseAt > 0f && dist > 5f * scale) fuseAt = 0f;
        if (fuseAt > 0f)
        {
          walking = false;
          rb.linearVelocity = new Vector3(v.x * 0.8f, rb.linearVelocity.y, v.z * 0.8f);
          if (now >= fuseAt)
          {
            hp = 0f;
            Vector3 c = obj.Center;
            world.KillMob(obj);
            world.Explode(c, 3f * scale, SbBlast.Tnt, true);
            return;
          }
        }
      }

      // chickens flutter down
      if (def.Type == SbMobType.Chicken && !grounded && v.y < -1.5f) rb.linearVelocity = new Vector3(v.x, -1.5f, v.z);

      // endermen teleport now and then
      if (def.Type == SbMobType.Enderman && now >= tpAt)
      {
        tpAt = now + UnityEngine.Random.Range(8f, 22f);
        float a = UnityEngine.Random.value * Mathf.PI * 2f, r = UnityEngine.Random.Range(3f, 9f);
        Vector3 to2 = pos + new Vector3(Mathf.Cos(a) * r, 4f, Mathf.Sin(a) * r);
        RaycastHit hit;
        if (Physics.Raycast(to2, Vector3.down, out hit, 12f, ~0, QueryTriggerInteraction.Ignore))
        {
          SbFx.Burst(pos + Vector3.up, 24, new Color(0.7f, 0.3f, 1f), new Color(0.3f, 0.05f, 0.5f), 0.3f, 1.2f, 0.01f, 0.03f, 0.4f, 0.9f, 0f, true, 0.4f);
          SbFx.PlayMc("enderman_tp", SbSound.Suck, pos, 0.8f, 1f, 18f);
          rb.position = hit.point + Vector3.up * 0.05f;
          rb.linearVelocity = Vector3.zero;
          SbFx.Burst(hit.point + Vector3.up, 24, new Color(0.7f, 0.3f, 1f), new Color(0.3f, 0.05f, 0.5f), 0.3f, 1.2f, 0.01f, 0.03f, 0.4f, 0.9f, 0f, true, 0.4f);
        }
      }
    }

    // ---------------------------------------------------------------- body (everyone)
    private void Update()
    {
      if (obj == null || obj.T == null) return;
      BodyExtras();   // wings, the boss bar (SandBoxMobBrain.cs)
      float dt = Mathf.Max(1e-4f, Time.deltaTime);
      Vector3 pos = transform.position;
      Vector3 frameVel = (pos - lastPos) / dt;
      lastPos = pos;
      vel = Vector3.Lerp(vel, frameVel, 1f - Mathf.Exp(-10f * dt));
      float scale = Mathf.Max(0.25f, obj.Scale);
      float speed = new Vector3(vel.x, 0f, vel.z).magnitude / scale;
      phase += speed * dt * (def.Quad ? 7f : 5.5f);
      float swing = Mathf.Clamp01(speed / Mathf.Max(0.3f, def.Speed)) * (def.Type == SbMobType.IronGolem ? 25f : 38f);
      float s = Mathf.Sin(phase) * swing;

      Anim(SbMobDefs.Role.LegL, Quaternion.Euler(s, 0f, 0f));
      Anim(SbMobDefs.Role.LegR, Quaternion.Euler(-s, 0f, 0f));
      if (def.SpiderLegs)
      {
        // spider legs sweep forward and back (about the up axis), the two sets in turn
        float k = s * 0.6f;
        Anim(SbMobDefs.Role.LegFL, Quaternion.Euler(0f, k, 0f), true);
        Anim(SbMobDefs.Role.LegBR, Quaternion.Euler(0f, k, 0f), true);
        Anim(SbMobDefs.Role.LegFR, Quaternion.Euler(0f, -k, 0f), true);
        Anim(SbMobDefs.Role.LegBL, Quaternion.Euler(0f, -k, 0f), true);
      }
      else
      {
        Anim(SbMobDefs.Role.LegFL, Quaternion.Euler(s, 0f, 0f));
        Anim(SbMobDefs.Role.LegBR, Quaternion.Euler(s, 0f, 0f));
        Anim(SbMobDefs.Role.LegFR, Quaternion.Euler(-s, 0f, 0f));
        Anim(SbMobDefs.Role.LegBL, Quaternion.Euler(-s, 0f, 0f));
      }
      // the blaze's rods go round
      Anim(SbMobDefs.Role.Spin, Quaternion.Euler(0f, Mathf.Repeat(Time.time * 140f, 360f), 0f));
      Anim(SbMobDefs.Role.Spin2, Quaternion.Euler(0f, Mathf.Repeat(-Time.time * 110f, 360f), 0f));
      if (def.ZombieArms)
      {
        // arms out in front
        Anim(SbMobDefs.Role.ArmL, Quaternion.Euler(-90f + Mathf.Sin(Time.time * 2f) * 4f, 0f, 0f));
        Anim(SbMobDefs.Role.ArmR, Quaternion.Euler(-90f - Mathf.Sin(Time.time * 2f) * 4f, 0f, 0f));
      }
      else
      {
        Anim(SbMobDefs.Role.ArmL, Quaternion.Euler(-s * 0.8f, 0f, 0f));
        Anim(SbMobDefs.Role.ArmR, Quaternion.Euler(s * 0.8f, 0f, 0f));
      }
      float flap = def.Type == SbMobType.Chicken && Mathf.Abs(vel.y) > 0.5f ? Mathf.Sin(Time.time * 40f) * 60f : 0f;
      Anim(SbMobDefs.Role.WingL, Quaternion.Euler(0f, 0f, -flap));
      Anim(SbMobDefs.Role.WingR, Quaternion.Euler(0f, 0f, flap));

      // the head turns to the nearest player close by
      Vector3 player;
      float wantYaw = 0f, wantPitch = 0f;
      if (world.NearestPlayer(pos, 6f * scale, out player))
      {
        Vector3 local = transform.InverseTransformPoint(player);
        wantYaw = Mathf.Clamp(Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg, -60f, 60f);
        wantPitch = Mathf.Clamp(-Mathf.Atan2(local.y - 24f, new Vector2(local.x, local.z).magnitude) * Mathf.Rad2Deg, -30f, 30f);
      }
      headYaw = Mathf.LerpAngle(headYaw, wantYaw, 1f - Mathf.Exp(-5f * dt));
      Anim(SbMobDefs.Role.Head, Quaternion.Euler(wantPitch * 0.5f, headYaw, 0f));

      // sounds
      if (Time.time >= sayAt)
      {
        sayAt = Time.time + UnityEngine.Random.Range(def.Hostile ? 4f : 6f, def.Hostile ? 9f : 16f);
        if (def.Type != SbMobType.IronGolem)
          SbFx.PlayMc(def.Say, SbSound.Squeak, pos + Vector3.up * 0.8f * scale, 0.8f, UnityEngine.Random.Range(0.85f, 1.15f), 16f);
      }
      if (def.Type == SbMobType.IronGolem && speed > 0.3f && Time.time >= stepAt)
      {
        stepAt = Time.time + 0.7f;
        SbFx.PlayMc("golem_step", SbSound.Thunk, pos, 0.7f, 1f, 16f);
      }

      // zombies push you away when they reach you
      if (Angry && !def.Explodes && !def.NoAi && Time.time >= knockAt)
      {
        try
        {
          GTPlayer me = GTPlayer.Instance;
          if (me != null && (me.bodyCollider.transform.position - (pos + Vector3.up * 0.7f * scale)).sqrMagnitude < 0.8f * 0.8f * scale * scale)
          {
            knockAt = Time.time + 1.2f;
            world.KnockLocalPlayer(pos, 1.2f * scale, 0.35f);
            SbFx.PlayMc(def.Say, SbSound.Hit, pos, 0.8f, 1.2f, 16f);
          }
        }
        catch { }
      }

      // red flash when hurt
      if (hurtFlash > 0f)
      {
        hurtFlash = Mathf.Max(0f, hurtFlash - dt);
        flashing = true;
        Color c = Color.Lerp(Color.white, new Color(1f, 0.35f, 0.35f), hurtFlash / 0.3f);
        block.SetColor("_BaseColor", c);
        block.SetColor("_Color", c);
        foreach (Renderer r in renderers) if (r != null) r.SetPropertyBlock(block);
      }
      else if (fuseAt > 0f)
      {
        // a lit creeper blinks white
        flashing = true;
        Color c = Mathf.Repeat(Time.time, 0.3f) < 0.15f ? new Color(2.2f, 2.2f, 2.2f) : Color.white;
        block.SetColor("_BaseColor", c);
        block.SetColor("_Color", c);
        foreach (Renderer r in renderers) if (r != null) r.SetPropertyBlock(block);
      }
      else if (flashing)
      {
        flashing = false;
        foreach (Renderer r in renderers) if (r != null) r.SetPropertyBlock(null);
      }
    }

    private void Anim(SbMobDefs.Role role, Quaternion q, bool aboutParent = false)
    {
      List<Transform> l;
      if (!parts.TryGetValue(role, out l)) return;
      foreach (Transform t in l)
      {
        if (t == null) continue;
        Quaternion b;
        if (!baseRot.TryGetValue(t, out b)) b = Quaternion.identity;
        t.localRotation = Quaternion.Slerp(t.localRotation, aboutParent ? q * b : b * q, 1f - Mathf.Exp(-18f * Time.deltaTime));
      }
    }
  }
}
