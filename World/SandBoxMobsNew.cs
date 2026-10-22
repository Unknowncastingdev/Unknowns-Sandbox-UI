using UnityEngine;

namespace UnknownsSandbox
{
  // more Minecraft mobs, the Wither, the Ender Dragon and End crystals, built from
  // boxes in Minecraft's proportions (pixels, y up from the feet, +Z the front) and
  // Minecraft's colours. Their brains: SandBoxMobBrain.cs.
  internal static partial class SbMobDefs
  {
    // four legs at (+-x, front z / back z), each w x h x w
    private static void Legs4(Def d, float x, float zf, float zb, float w, float h, string col)
    {
      foreach (var leg in new[] { (Role.LegFR, x, zf), (Role.LegFL, -x - w, zf), (Role.LegBR, x, zb), (Role.LegBL, -x - w, zb) })
        P(d, leg.Item1, new Vector3(leg.Item2, 0f, leg.Item3), new Vector3(w, h, w), 0, 0, new Vector3(leg.Item2 + w * 0.5f, h, leg.Item3 + w * 0.5f), col);
    }

    private static Part G(Def d, Role role, Vector3 min, Vector3 size, Vector3 pivot, string col, bool glow = false)
    {
      Part p = P(d, role, min, size, 0, 0, pivot, col);
      p.Glow = glow;
      return p;
    }

    // the 12 edges of a box (the End crystal's glass cages)
    private static void Cage(Def d, Role role, Vector3 c, float size, float bar, string col)
    {
      float h = size * 0.5f;
      foreach (float a in new[] { -h, h })
        foreach (float b in new[] { -h, h })
        {
          G(d, role, c + new Vector3(-h, a - bar * 0.5f, b - bar * 0.5f), new Vector3(size, bar, bar), c, col, true);
          G(d, role, c + new Vector3(a - bar * 0.5f, -h, b - bar * 0.5f), new Vector3(bar, size, bar), c, col, true);
          G(d, role, c + new Vector3(a - bar * 0.5f, b - bar * 0.5f, -h), new Vector3(bar, bar, size), c, col, true);
        }
    }

    private static void InitNew()
    {
      Def d;

      // ---- animals
      d = D(SbMobType.Horse, "mob_horse", "Horse", "Gallops around the fields.", "ANIMALS", 26, 1.8f, 40f, "horse_say", "horse_hurt", "horse_death");
      d.Quad = true;
      Legs4(d, 1f, 6f, -10f, 4f, 11f, "#6a4224");
      P(d, Role.Body, new Vector3(-5f, 11f, -11f), new Vector3(10f, 10f, 22f), 0, 0, new Vector3(0f, 16f, 0f), "#7a4f2a");
      P(d, Role.None, new Vector3(-1.5f, 12f, -14f), new Vector3(3f, 9f, 3f), 0, 0, new Vector3(0f, 20f, -11f), "#3a2412").BaseRot = Quaternion.Euler(25f, 0f, 0f);
      P(d, Role.Head, new Vector3(-2.5f, 18f, 7f), new Vector3(5f, 10f, 6f), 0, 0, new Vector3(0f, 22f, 10f), "#7a4f2a");
      P(d, Role.Head, new Vector3(-3f, 26f, 8f), new Vector3(6f, 5f, 7f), 0, 0, new Vector3(0f, 22f, 10f), "#7a4f2a");
      P(d, Role.Head, new Vector3(-2f, 25f, 14f), new Vector3(4f, 4f, 4f), 0, 0, new Vector3(0f, 22f, 10f), "#5a3a1e");
      P(d, Role.Head, new Vector3(-1f, 21f, 6.5f), new Vector3(2f, 10f, 2f), 0, 0, new Vector3(0f, 22f, 10f), "#3a2412");
      P(d, Role.Head, new Vector3(-2.5f, 31f, 9f), new Vector3(1.5f, 2.5f, 1f), 0, 0, new Vector3(0f, 22f, 10f), "#7a4f2a");
      P(d, Role.Head, new Vector3(1f, 31f, 9f), new Vector3(1.5f, 2.5f, 1f), 0, 0, new Vector3(0f, 22f, 10f), "#7a4f2a");
      G(d, Role.Head, new Vector3(-3.1f, 28f, 12f), new Vector3(0.2f, 1f, 1f), new Vector3(0f, 22f, 10f), "#141414");
      G(d, Role.Head, new Vector3(2.9f, 28f, 12f), new Vector3(0.2f, 1f, 1f), new Vector3(0f, 22f, 10f), "#141414");

      d = D(SbMobType.Bee, "mob_bee", "Bee", "Buzzes about. Stings when you hit it.", "ANIMALS", 10, 1.6f, 0.6f, "bee_say", "bee_hurt", "bee_death");
      d.Flies = true; d.Angers = true; d.FlyHeight = 1.6f;
      P(d, Role.Body, new Vector3(-3.5f, 2f, -5f), new Vector3(7f, 7f, 10f), 0, 0, new Vector3(0f, 5f, 0f), "#e8b830");
      P(d, Role.Body, new Vector3(-3.6f, 1.9f, -2f), new Vector3(7.2f, 7.2f, 1.5f), 0, 0, new Vector3(0f, 5f, 0f), "#3a2410");
      P(d, Role.Body, new Vector3(-3.6f, 1.9f, -4.5f), new Vector3(7.2f, 7.2f, 1.5f), 0, 0, new Vector3(0f, 5f, 0f), "#3a2410");
      P(d, Role.Body, new Vector3(-0.5f, 4.5f, -6f), new Vector3(1f, 1f, 1f), 0, 0, new Vector3(0f, 5f, 0f), "#2a2a2a");
      P(d, Role.Head, new Vector3(-3.6f, 5f, 4.6f), new Vector3(2f, 3f, 0.5f), 0, 0, new Vector3(0f, 5f, 4f), "#141414");
      P(d, Role.Head, new Vector3(1.6f, 5f, 4.6f), new Vector3(2f, 3f, 0.5f), 0, 0, new Vector3(0f, 5f, 4f), "#141414");
      P(d, Role.Head, new Vector3(-2f, 9f, 3f), new Vector3(0.5f, 3f, 0.5f), 0, 0, new Vector3(0f, 5f, 4f), "#2a2a2a");
      P(d, Role.Head, new Vector3(1.5f, 9f, 3f), new Vector3(0.5f, 3f, 0.5f), 0, 0, new Vector3(0f, 5f, 4f), "#2a2a2a");
      P(d, Role.WingL, new Vector3(-9f, 9f, -2f), new Vector3(8f, 0.3f, 6f), 0, 0, new Vector3(-1f, 9f, 0f), "#dfeaf2");
      P(d, Role.WingR, new Vector3(1f, 9f, -2f), new Vector3(8f, 0.3f, 6f), 0, 0, new Vector3(1f, 9f, 0f), "#dfeaf2");

      d = D(SbMobType.Frog, "mob_frog", "Frog", "Ribbit. Hops about.", "ANIMALS", 10, 1f, 1f, "frog_say", "frog_hurt", "frog_death");
      d.Hops = true; d.HopSound = "frog_step";
      P(d, Role.Body, new Vector3(-3.5f, 1f, -4.5f), new Vector3(7f, 3f, 9f), 0, 0, new Vector3(0f, 2f, 0f), "#7a9a3a");
      P(d, Role.Body, new Vector3(-3.4f, 0.9f, -3f), new Vector3(6.8f, 1f, 7f), 0, 0, new Vector3(0f, 2f, 0f), "#d8d0a0");
      P(d, Role.Head, new Vector3(-3.5f, 4f, -1.5f), new Vector3(7f, 3f, 7f), 0, 0, new Vector3(0f, 4f, 2f), "#7a9a3a");
      P(d, Role.Head, new Vector3(-3.5f, 7f, 2f), new Vector3(3f, 2f, 3f), 0, 0, new Vector3(0f, 4f, 2f), "#7a9a3a");
      P(d, Role.Head, new Vector3(0.5f, 7f, 2f), new Vector3(3f, 2f, 3f), 0, 0, new Vector3(0f, 4f, 2f), "#7a9a3a");
      P(d, Role.Head, new Vector3(-2.5f, 7.5f, 4.6f), new Vector3(1.2f, 1.2f, 0.5f), 0, 0, new Vector3(0f, 4f, 2f), "#141414");
      P(d, Role.Head, new Vector3(1.3f, 7.5f, 4.6f), new Vector3(1.2f, 1.2f, 0.5f), 0, 0, new Vector3(0f, 4f, 2f), "#141414");
      Legs4(d, 2f, 2f, -4f, 2f, 2f, "#5a7a2a");

      d = D(SbMobType.Axolotl, "mob_axolotl", "Axolotl", "Swims in water, waddles on land.", "ANIMALS", 14, 0.8f, 1f, "axolotl_say", "axolotl_hurt", "axolotl_death");
      d.Swims = true;
      P(d, Role.Body, new Vector3(-4f, 1f, -5f), new Vector3(8f, 4f, 10f), 0, 0, new Vector3(0f, 3f, 0f), "#f2a0c8");
      P(d, Role.None, new Vector3(-0.25f, 1.5f, -12f), new Vector3(0.5f, 4f, 7f), 0, 0, new Vector3(0f, 3f, -5f), "#e890b8");
      P(d, Role.Head, new Vector3(-4f, 1f, 5f), new Vector3(8f, 5f, 5f), 0, 0, new Vector3(0f, 3f, 5f), "#f2a0c8");
      P(d, Role.Head, new Vector3(-7f, 3f, 7f), new Vector3(3f, 4f, 1f), 0, 0, new Vector3(0f, 3f, 5f), "#d8305a");
      P(d, Role.Head, new Vector3(4f, 3f, 7f), new Vector3(3f, 4f, 1f), 0, 0, new Vector3(0f, 3f, 5f), "#d8305a");
      P(d, Role.Head, new Vector3(-3f, 6f, 7f), new Vector3(6f, 2f, 1f), 0, 0, new Vector3(0f, 3f, 5f), "#d8305a");
      P(d, Role.Head, new Vector3(-3f, 3.5f, 10.01f), new Vector3(1f, 1f, 0.2f), 0, 0, new Vector3(0f, 3f, 5f), "#141414");
      P(d, Role.Head, new Vector3(2f, 3.5f, 10.01f), new Vector3(1f, 1f, 0.2f), 0, 0, new Vector3(0f, 3f, 5f), "#141414");
      Legs4(d, 4f, 2f, -4f, 2f, 1f, "#e890b8");

      d = D(SbMobType.Panda, "mob_panda", "Panda", "Lazy, munches bamboo.", "ANIMALS", 20, 0.6f, 60f, "panda_say", "panda_hurt", "panda_death");
      d.Quad = true;
      Legs4(d, 1f, 3f, -9f, 6f, 9f, "#1e1e1e");
      P(d, Role.Body, new Vector3(-6.5f, 9f, -9.5f), new Vector3(13f, 12f, 19f), 0, 0, new Vector3(0f, 15f, 0f), "#f2f2f2");
      P(d, Role.Body, new Vector3(-6.6f, 8.9f, 2f), new Vector3(13.2f, 12.2f, 5f), 0, 0, new Vector3(0f, 15f, 0f), "#1e1e1e");
      P(d, Role.Head, new Vector3(-4.5f, 12f, 9f), new Vector3(9f, 8f, 6f), 0, 0, new Vector3(0f, 15f, 9f), "#f2f2f2");
      P(d, Role.Head, new Vector3(-3.5f, 15f, 14.6f), new Vector3(2.5f, 2.5f, 0.6f), 0, 0, new Vector3(0f, 15f, 9f), "#1e1e1e");
      P(d, Role.Head, new Vector3(1f, 15f, 14.6f), new Vector3(2.5f, 2.5f, 0.6f), 0, 0, new Vector3(0f, 15f, 9f), "#1e1e1e");
      P(d, Role.Head, new Vector3(-2f, 12f, 15f), new Vector3(4f, 3f, 2f), 0, 0, new Vector3(0f, 15f, 9f), "#e8e8e8");
      P(d, Role.Head, new Vector3(-5.5f, 20f, 10f), new Vector3(3f, 2f, 2f), 0, 0, new Vector3(0f, 15f, 9f), "#1e1e1e");
      P(d, Role.Head, new Vector3(2.5f, 20f, 10f), new Vector3(3f, 2f, 2f), 0, 0, new Vector3(0f, 15f, 9f), "#1e1e1e");

      d = D(SbMobType.PolarBear, "mob_polar_bear", "Polar Bear", "Friendly, unless you hit it.", "ANIMALS", 30, 1.1f, 80f, "polar_bear_say", "polar_bear_hurt", "polar_bear_death");
      d.Quad = true; d.Angers = true;
      Legs4(d, 1.5f, 5f, -11f, 5.5f, 10f, "#e6e6de");
      P(d, Role.Body, new Vector3(-7f, 10f, -12f), new Vector3(14f, 13f, 22f), 0, 0, new Vector3(0f, 16f, 0f), "#f2f2ec");
      P(d, Role.Head, new Vector3(-3.5f, 15f, 10f), new Vector3(7f, 7f, 7f), 0, 0, new Vector3(0f, 18f, 10f), "#f2f2ec");
      P(d, Role.Head, new Vector3(-2.5f, 15f, 17f), new Vector3(5f, 3f, 3f), 0, 0, new Vector3(0f, 18f, 10f), "#d8d8d0");
      P(d, Role.Head, new Vector3(-1f, 17f, 19.6f), new Vector3(2f, 1f, 0.5f), 0, 0, new Vector3(0f, 18f, 10f), "#1e1e1e");
      P(d, Role.Head, new Vector3(-4.5f, 21f, 12f), new Vector3(2f, 2f, 1f), 0, 0, new Vector3(0f, 18f, 10f), "#f2f2ec");
      P(d, Role.Head, new Vector3(2.5f, 21f, 12f), new Vector3(2f, 2f, 1f), 0, 0, new Vector3(0f, 18f, 10f), "#f2f2ec");
      P(d, Role.Head, new Vector3(-3f, 19f, 17.01f), new Vector3(1f, 1f, 0.2f), 0, 0, new Vector3(0f, 18f, 10f), "#141414");
      P(d, Role.Head, new Vector3(2f, 19f, 17.01f), new Vector3(1f, 1f, 0.2f), 0, 0, new Vector3(0f, 18f, 10f), "#141414");

      d = D(SbMobType.Turtle, "mob_turtle", "Turtle", "Slow and steady.", "ANIMALS", 30, 0.35f, 30f, "turtle_say", "turtle_hurt", "turtle_death");
      d.Swims = true;
      P(d, Role.Body, new Vector3(-9.5f, 3f, -10f), new Vector3(19f, 7f, 20f), 0, 0, new Vector3(0f, 5f, 0f), "#3a7a40");
      P(d, Role.Body, new Vector3(-7f, 10f, -7f), new Vector3(14f, 2f, 14f), 0, 0, new Vector3(0f, 5f, 0f), "#2e6234");
      P(d, Role.Body, new Vector3(-8f, 1f, -9f), new Vector3(16f, 2f, 18f), 0, 0, new Vector3(0f, 5f, 0f), "#c8c088");
      P(d, Role.Head, new Vector3(-3f, 2f, 10f), new Vector3(6f, 5f, 6f), 0, 0, new Vector3(0f, 4f, 10f), "#78a858");
      P(d, Role.Head, new Vector3(-3.1f, 5f, 13f), new Vector3(0.2f, 1f, 1f), 0, 0, new Vector3(0f, 4f, 10f), "#141414");
      P(d, Role.Head, new Vector3(2.9f, 5f, 13f), new Vector3(0.2f, 1f, 1f), 0, 0, new Vector3(0f, 4f, 10f), "#141414");
      P(d, Role.LegFL, new Vector3(-14f, 1f, 4f), new Vector3(6f, 1f, 4f), 0, 0, new Vector3(-8f, 2f, 6f), "#78a858");
      P(d, Role.LegFR, new Vector3(8f, 1f, 4f), new Vector3(6f, 1f, 4f), 0, 0, new Vector3(8f, 2f, 6f), "#78a858");
      P(d, Role.LegBL, new Vector3(-12f, 1f, -8f), new Vector3(4f, 1f, 4f), 0, 0, new Vector3(-8f, 2f, -6f), "#78a858");
      P(d, Role.LegBR, new Vector3(8f, 1f, -8f), new Vector3(4f, 1f, 4f), 0, 0, new Vector3(8f, 2f, -6f), "#78a858");

      d = D(SbMobType.Goat, "mob_goat", "Goat", "Climbs and rams.", "ANIMALS", 10, 1.3f, 20f, "goat_say", "goat_hurt", "goat_death");
      d.Quad = true; d.Angers = true;
      Legs4(d, 1f, 4f, -7f, 3f, 10f, "#d8d2c2");
      P(d, Role.Body, new Vector3(-4.5f, 10f, -8f), new Vector3(9f, 11f, 16f), 0, 0, new Vector3(0f, 15f, 0f), "#e6e2d6");
      P(d, Role.Head, new Vector3(-2.5f, 16f, 7f), new Vector3(5f, 7f, 9f), 0, 0, new Vector3(0f, 19f, 8f), "#e6e2d6");
      P(d, Role.Head, new Vector3(-2.5f, 22f, 9f), new Vector3(1.5f, 6f, 1.5f), 0, 0, new Vector3(0f, 19f, 8f), "#a09a8a");
      P(d, Role.Head, new Vector3(1f, 22f, 9f), new Vector3(1.5f, 6f, 1.5f), 0, 0, new Vector3(0f, 19f, 8f), "#a09a8a");
      P(d, Role.Head, new Vector3(-0.5f, 13f, 14f), new Vector3(1f, 3f, 1f), 0, 0, new Vector3(0f, 19f, 8f), "#c8c2b2");
      P(d, Role.Head, new Vector3(-2.6f, 20f, 13f), new Vector3(0.2f, 1f, 1f), 0, 0, new Vector3(0f, 19f, 8f), "#141414");
      P(d, Role.Head, new Vector3(2.4f, 20f, 13f), new Vector3(0.2f, 1f, 1f), 0, 0, new Vector3(0f, 19f, 8f), "#141414");

      d = D(SbMobType.Camel, "mob_camel", "Camel", "Tall, sandy and calm.", "ANIMALS", 32, 1.1f, 80f, "camel_say", "camel_hurt", "camel_death");
      d.Quad = true;
      Legs4(d, 2f, 7f, -12f, 5f, 21f, "#c49a5a");
      P(d, Role.Body, new Vector3(-7.5f, 20f, -14f), new Vector3(15f, 12f, 27f), 0, 0, new Vector3(0f, 26f, 0f), "#d4aa6a");
      P(d, Role.Body, new Vector3(-4.5f, 32f, -7f), new Vector3(9f, 5f, 11f), 0, 0, new Vector3(0f, 26f, 0f), "#c49a5a");
      P(d, Role.Head, new Vector3(-3.5f, 27f, 12f), new Vector3(7f, 16f, 6f), 0, 0, new Vector3(0f, 32f, 14f), "#d4aa6a");
      P(d, Role.Head, new Vector3(-3.5f, 40f, 13f), new Vector3(7f, 8f, 15f), 0, 0, new Vector3(0f, 32f, 14f), "#d4aa6a");
      P(d, Role.Head, new Vector3(-3.6f, 44f, 22f), new Vector3(0.2f, 1.5f, 1.5f), 0, 0, new Vector3(0f, 32f, 14f), "#141414");
      P(d, Role.Head, new Vector3(3.4f, 44f, 22f), new Vector3(0.2f, 1.5f, 1.5f), 0, 0, new Vector3(0f, 32f, 14f), "#141414");

      d = D(SbMobType.Squid, "mob_squid", "Squid", "Floats about in water.", "ANIMALS", 10, 0.7f, 6f, "squid_say", "squid_hurt", "squid_death");
      d.Swims = true;
      P(d, Role.Body, new Vector3(-6f, 8f, -6f), new Vector3(12f, 16f, 12f), 0, 0, new Vector3(0f, 16f, 0f), "#2a3a5a");
      P(d, Role.Head, new Vector3(-4f, 12f, 6.01f), new Vector3(2f, 2f, 0.3f), 0, 0, new Vector3(0f, 12f, 6f), "#f2f2f2");
      P(d, Role.Head, new Vector3(2f, 12f, 6.01f), new Vector3(2f, 2f, 0.3f), 0, 0, new Vector3(0f, 12f, 6f), "#f2f2f2");
      for (int i = 0; i < 8; i++)
      {
        float a = i / 8f * Mathf.PI * 2f;
        Vector3 at = new Vector3(Mathf.Cos(a) * 5f - 1f, 0f, Mathf.Sin(a) * 5f - 1f);
        P(d, i % 2 == 0 ? Role.LegL : Role.LegR, at, new Vector3(2f, 8f, 2f), 0, 0, at + new Vector3(1f, 8f, 1f), "#24324e");
      }

      d = D(SbMobType.Cod, "mob_cod", "Cod", "A fish. Flops on land.", "ANIMALS", 3, 1.2f, 0.4f, "cod_flop", "cod_hurt", "cod_death");
      d.Swims = true;
      P(d, Role.Body, new Vector3(-1f, 0f, -4f), new Vector3(2f, 4f, 7f), 0, 0, new Vector3(0f, 2f, 0f), "#b09a70");
      P(d, Role.Head, new Vector3(-1f, 0.2f, 3f), new Vector3(2f, 3.5f, 3f), 0, 0, new Vector3(0f, 2f, 3f), "#a08a60");
      P(d, Role.Head, new Vector3(-1.1f, 2f, 5f), new Vector3(0.2f, 0.8f, 0.8f), 0, 0, new Vector3(0f, 2f, 3f), "#141414");
      P(d, Role.Head, new Vector3(0.9f, 2f, 5f), new Vector3(0.2f, 0.8f, 0.8f), 0, 0, new Vector3(0f, 2f, 3f), "#141414");
      P(d, Role.None, new Vector3(-0.05f, 0f, -8f), new Vector3(0.1f, 4f, 4f), 0, 0, new Vector3(0f, 2f, -4f), "#c8b488");
      P(d, Role.Body, new Vector3(-0.05f, 4f, -2f), new Vector3(0.1f, 1.5f, 4f), 0, 0, new Vector3(0f, 2f, 0f), "#c8b488");

      d = D(SbMobType.Parrot, "mob_parrot", "Parrot", "Squawks and flutters about.", "ANIMALS", 6, 1.3f, 0.5f, "parrot_say", "parrot_hurt", "parrot_death");
      d.Flies = true; d.FlyHeight = 2.5f;
      P(d, Role.Body, new Vector3(-1.5f, 3f, -1.5f), new Vector3(3f, 6f, 3f), 0, 0, new Vector3(0f, 6f, 0f), "#e03a2a").BaseRot = Quaternion.Euler(-15f, 0f, 0f);
      P(d, Role.Head, new Vector3(-1f, 9f, -1f), new Vector3(2f, 3f, 2.5f), 0, 0, new Vector3(0f, 9f, 0f), "#e03a2a");
      P(d, Role.Head, new Vector3(-0.5f, 9.5f, 1.5f), new Vector3(1f, 1.5f, 1f), 0, 0, new Vector3(0f, 9f, 0f), "#2a2a2a");
      P(d, Role.Head, new Vector3(-1.5f, 12f, -0.5f), new Vector3(3f, 1f, 2f), 0, 0, new Vector3(0f, 9f, 0f), "#f2c230");
      P(d, Role.WingL, new Vector3(-2.5f, 4f, -1.5f), new Vector3(1f, 5f, 3f), 0, 0, new Vector3(-1.5f, 8.5f, 0f), "#3a6ad8");
      P(d, Role.WingR, new Vector3(1.5f, 4f, -1.5f), new Vector3(1f, 5f, 3f), 0, 0, new Vector3(1.5f, 8.5f, 0f), "#3a6ad8");
      P(d, Role.None, new Vector3(-1.5f, 0.5f, -2.5f), new Vector3(3f, 4f, 1f), 0, 0, new Vector3(0f, 4f, -1.5f), "#3a6ad8");
      P(d, Role.LegL, new Vector3(-1f, 0f, -0.5f), new Vector3(0.6f, 3f, 0.6f), 0, 0, new Vector3(-0.7f, 3f, 0f), "#3a3a3a");
      P(d, Role.LegR, new Vector3(0.4f, 0f, -0.5f), new Vector3(0.6f, 3f, 0.6f), 0, 0, new Vector3(0.7f, 3f, 0f), "#3a3a3a");

      d = D(SbMobType.Bat, "mob_bat", "Bat", "Squeaks and flutters in the dark.", "MOBS", 6, 2f, 0.3f, "bat_say", "bat_hurt", "bat_death");
      d.Flies = true; d.FlyHeight = 3f;
      P(d, Role.Body, new Vector3(-1.5f, 4f, -1f), new Vector3(3f, 5f, 2f), 0, 0, new Vector3(0f, 7f, 0f), "#4a3a2a");
      P(d, Role.Head, new Vector3(-2f, 9f, -1.5f), new Vector3(4f, 3f, 3f), 0, 0, new Vector3(0f, 9f, 0f), "#4a3a2a");
      P(d, Role.Head, new Vector3(-2f, 12f, -0.5f), new Vector3(1f, 2f, 1f), 0, 0, new Vector3(0f, 9f, 0f), "#3a2a1e");
      P(d, Role.Head, new Vector3(1f, 12f, -0.5f), new Vector3(1f, 2f, 1f), 0, 0, new Vector3(0f, 9f, 0f), "#3a2a1e");
      P(d, Role.WingL, new Vector3(-10f, 4f, -0.25f), new Vector3(8.5f, 6f, 0.5f), 0, 0, new Vector3(-1.5f, 8f, 0f), "#3a2a1e");
      P(d, Role.WingR, new Vector3(1.5f, 4f, -0.25f), new Vector3(8.5f, 6f, 0.5f), 0, 0, new Vector3(1.5f, 8f, 0f), "#3a2a1e");

      d = D(SbMobType.Strider, "mob_strider", "Strider", "Walks on lava, shivers on land.", "NETHER MOBS", 20, 0.8f, 20f, "strider_say", "strider_hurt", "strider_death");
      d.Biped = true;
      P(d, Role.Body, new Vector3(-8f, 16f, -8f), new Vector3(16f, 14f, 16f), 0, 0, new Vector3(0f, 22f, 0f), "#9a3a3a");
      for (int i = 0; i < 6; i++)
        P(d, Role.Head, new Vector3(-7f + i * 2.6f, 30f, -1f), new Vector3(1f, 6f, 1f), 0, 0, new Vector3(0f, 30f, 0f), "#d8b8a8").BaseRot = Quaternion.Euler(0f, 0f, (i - 2.5f) * 12f);
      G(d, Role.Head, new Vector3(-5f, 24f, 8.01f), new Vector3(3f, 2f, 0.2f), new Vector3(0f, 24f, 8f), "#141414");
      G(d, Role.Head, new Vector3(2f, 24f, 8.01f), new Vector3(3f, 2f, 0.2f), new Vector3(0f, 24f, 8f), "#141414");
      P(d, Role.LegL, new Vector3(-6f, 0f, -2f), new Vector3(4f, 16f, 4f), 0, 0, new Vector3(-4f, 16f, 0f), "#6a4a5a");
      P(d, Role.LegR, new Vector3(2f, 0f, -2f), new Vector3(4f, 16f, 4f), 0, 0, new Vector3(4f, 16f, 0f), "#6a4a5a");

      // ---- monsters
      d = D(SbMobType.Ghast, "mob_ghast", "Ghast", "Floats high and spits exploding fireballs.", "NETHER MOBS", 10, 0.8f, 30f, "ghast_say", "ghast_hurt", "ghast_death", "entity/ghast/ghast");
      d.Flies = true; d.Hostile = true; d.Shoots = true; d.Shot = SbAmmo.GhastBall; d.ShotEvery = 3f; d.ShotRange = 40f; d.KeepAway = 14f; d.FlyHeight = 9f; d.Scale = 4f;
      P(d, Role.Body, new Vector3(-8f, 8f, -8f), new Vector3(16f, 16f, 16f), 0, 0, new Vector3(0f, 16f, 0f), "#f0f0f0");
      P(d, Role.Head, new Vector3(-6f, 17f, 8.01f), new Vector3(3f, 1f, 0.2f), 0, 0, new Vector3(0f, 16f, 8f), "#5a5a5a").Tex2 = true;   // the face: drawn on only without the texture (it has its own)
      P(d, Role.Head, new Vector3(3f, 17f, 8.01f), new Vector3(3f, 1f, 0.2f), 0, 0, new Vector3(0f, 16f, 8f), "#5a5a5a").Tex2 = true;   // the face: drawn on only without the texture (it has its own)
      P(d, Role.Head, new Vector3(-3f, 12f, 8.01f), new Vector3(6f, 2f, 0.2f), 0, 0, new Vector3(0f, 16f, 8f), "#5a5a5a").Tex2 = true;   // the face: drawn on only without the texture (it has its own)
      for (int i = 0; i < 9; i++)
      {
        float x = -6f + (i % 3) * 5f, z = -6f + (i / 3) * 5f, len = 7f + (i * 37 % 6);
        P(d, i % 2 == 0 ? Role.LegL : Role.LegR, new Vector3(x, 8f - len, z), new Vector3(2f, len, 2f), 0, 0, new Vector3(x + 1f, 8f, z + 1f), "#e6e6e6");
      }

      d = D(SbMobType.Phantom, "mob_phantom", "Phantom", "Swoops down at you from the sky.", "MOBS", 20, 2.4f, 4f, "phantom_say", "phantom_hurt", "phantom_death");
      d.Flies = true; d.Hostile = true; d.FlyHeight = 7f; d.Scale = 1.4f;
      P(d, Role.Body, new Vector3(-2.5f, 8f, -4.5f), new Vector3(5f, 3f, 9f), 0, 0, new Vector3(0f, 9f, 0f), "#3a4a7a");
      P(d, Role.Head, new Vector3(-3.5f, 8f, 4.5f), new Vector3(7f, 3f, 5f), 0, 0, new Vector3(0f, 9f, 4.5f), "#3a4a7a");
      G(d, Role.Head, new Vector3(-3f, 10f, 9.51f), new Vector3(2f, 1f, 0.2f), new Vector3(0f, 9f, 4.5f), "#6aff4a", true);
      G(d, Role.Head, new Vector3(1f, 10f, 9.51f), new Vector3(2f, 1f, 0.2f), new Vector3(0f, 9f, 4.5f), "#6aff4a", true);
      P(d, Role.WingL, new Vector3(-15f, 9f, -4f), new Vector3(12.5f, 1f, 8f), 0, 0, new Vector3(-2.5f, 9.5f, 0f), "#2e3c66");
      P(d, Role.WingR, new Vector3(2.5f, 9f, -4f), new Vector3(12.5f, 1f, 8f), 0, 0, new Vector3(2.5f, 9.5f, 0f), "#2e3c66");
      P(d, Role.None, new Vector3(-1.5f, 8.5f, -10.5f), new Vector3(3f, 2f, 6f), 0, 0, new Vector3(0f, 9f, -4.5f), "#3a4a7a");

      d = D(SbMobType.Vex, "mob_vex", "Vex", "A tiny flying spirit with a sword. Flies through walls.", "MOBS", 14, 2f, 0.5f, "vex_say", "vex_hurt", "vex_death");
      d.TexH = 64; d.Biped = true; d.Flies = true; d.Hostile = true; d.FlyHeight = 1.5f; d.Scale = 0.5f;
      Humanoid(d, "#a8b8c8", "#8a9ab0", "#8a9ab0", 4f, true);
      P(d, Role.WingL, new Vector3(-12f, 14f, -3f), new Vector3(10f, 10f, 0.5f), 0, 0, new Vector3(-2f, 22f, -2f), "#e8f0ff");
      P(d, Role.WingR, new Vector3(2f, 14f, -3f), new Vector3(10f, 10f, 0.5f), 0, 0, new Vector3(2f, 22f, -2f), "#e8f0ff");
      P(d, Role.ArmR, new Vector3(5f, 0f, 2f), new Vector3(1f, 12f, 1f), 0, 0, new Vector3(6f, 22f, 0f), "#c8ccd4");

      d = D(SbMobType.Ravager, "mob_ravager", "Ravager", "A huge beast that charges at you.", "MOBS", 100, 1.2f, 200f, "ravager_say", "ravager_hurt", "ravager_death");
      d.Quad = true; d.Hostile = true; d.Scale = 1.3f;
      Legs4(d, 3f, 6f, -12f, 7f, 16f, "#4a4a44");
      P(d, Role.Body, new Vector3(-7f, 16f, -14f), new Vector3(14f, 16f, 26f), 0, 0, new Vector3(0f, 24f, 0f), "#5a5a52");
      P(d, Role.Head, new Vector3(-8f, 14f, 12f), new Vector3(16f, 18f, 14f), 0, 0, new Vector3(0f, 24f, 12f), "#5a5a52");
      P(d, Role.Head, new Vector3(-8f, 10f, 20f), new Vector3(16f, 4f, 6f), 0, 0, new Vector3(0f, 24f, 12f), "#3a3a34");
      P(d, Role.Head, new Vector3(-11f, 30f, 14f), new Vector3(3f, 8f, 3f), 0, 0, new Vector3(0f, 24f, 12f), "#d8d0b8");
      P(d, Role.Head, new Vector3(8f, 30f, 14f), new Vector3(3f, 8f, 3f), 0, 0, new Vector3(0f, 24f, 12f), "#d8d0b8");
      G(d, Role.Head, new Vector3(-6f, 24f, 26.01f), new Vector3(3f, 2f, 0.2f), new Vector3(0f, 24f, 12f), "#d8d040", true);
      G(d, Role.Head, new Vector3(3f, 24f, 26.01f), new Vector3(3f, 2f, 0.2f), new Vector3(0f, 24f, 12f), "#d8d040", true);

      // ---- bosses
      d = D(SbMobType.Wither, "mob_wither", "Wither", "A three-headed boss that shoots exploding skulls. Made from 4 soul sand and 3 wither skeleton skulls.", "BOSSES", 300, 1f, 60f, "wither_say", "wither_hurt", "wither_death");
      d.Flies = true; d.Hostile = true; d.Boss = true; d.Shoots = true; d.Shot = SbAmmo.WitherSkull; d.ShotEvery = 1.4f; d.ShotRange = 30f; d.KeepAway = 7f; d.FlyHeight = 4f; d.Scale = 1.8f;
      P(d, Role.Head, new Vector3(-4f, 30f, -4f), new Vector3(8f, 8f, 8f), 0, 0, new Vector3(0f, 30f, 0f), "#1e1e1e");
      P(d, Role.Head, new Vector3(-13f, 27f, -3f), new Vector3(6f, 6f, 6f), 0, 0, new Vector3(-10f, 27f, 0f), "#1e1e1e");
      P(d, Role.Head, new Vector3(7f, 27f, -3f), new Vector3(6f, 6f, 6f), 0, 0, new Vector3(10f, 27f, 0f), "#1e1e1e");
      foreach (float hx in new[] { -2.5f, 1f }) G(d, Role.Head, new Vector3(hx, 34f, 4.01f), new Vector3(1.5f, 1.5f, 0.2f), new Vector3(0f, 30f, 0f), "#d8e8ff", true);
      P(d, Role.Body, new Vector3(-10f, 25f, -1.5f), new Vector3(20f, 3f, 3f), 0, 0, new Vector3(0f, 20f, 0f), "#2a2a2a");
      P(d, Role.Body, new Vector3(-1.5f, 13f, -0.5f), new Vector3(3f, 12f, 3f), 0, 0, new Vector3(0f, 20f, 0f), "#2a2a2a");
      for (int i = 0; i < 3; i++) P(d, Role.Body, new Vector3(-4.5f, 15f + i * 3f, -0.5f), new Vector3(9f, 1.5f, 3f), 0, 0, new Vector3(0f, 20f, 0f), "#2a2a2a");
      P(d, Role.None, new Vector3(-1.5f, 5f, -1f), new Vector3(3f, 8f, 3f), 0, 0, new Vector3(0f, 13f, 0f), "#2a2a2a").BaseRot = Quaternion.Euler(20f, 0f, 0f);

      d = D(SbMobType.EnderDragon, "mob_ender_dragon", "Ender Dragon", "The boss of the End. Lives on the End island, healed by the End crystals.", "BOSSES", 200, 9f, 400f, "enderdragon_growl", "enderdragon_hurt", "enderdragon_death");
      d.Flies = true; d.Hostile = true; d.Boss = true; d.Shoots = true; d.Shot = SbAmmo.DragonBall; d.ShotEvery = 7f; d.ShotRange = 60f; d.Scale = 1.6f;
      d.Hidden = true;
      const string scale = "#1a1a1e", belly = "#2a2a30", spike = "#4a4a52";
      P(d, Role.Body, new Vector3(-12f, 20f, -16f), new Vector3(24f, 24f, 64f), 0, 0, new Vector3(0f, 32f, 0f), scale);
      for (int i = 0; i < 4; i++) P(d, Role.Body, new Vector3(-1f, 44f, -10f + i * 14f), new Vector3(2f, 6f, 8f), 0, 0, new Vector3(0f, 32f, 0f), spike);
      for (int i = 0; i < 3; i++)
      {
        P(d, Role.Body, new Vector3(-5f, 26f, 48f + i * 10f), new Vector3(10f, 10f, 10f), 0, 0, new Vector3(0f, 32f, 0f), scale);
        P(d, Role.Body, new Vector3(-1f, 36f, 50f + i * 10f), new Vector3(2f, 4f, 6f), 0, 0, new Vector3(0f, 32f, 0f), spike);
      }
      for (int i = 0; i < 6; i++)
      {
        float sz = 10f - i;
        P(d, Role.None, new Vector3(-sz * 0.5f, 28f, -16f - (i + 1) * 10f), new Vector3(sz, sz, 10f), 0, 0, new Vector3(0f, 32f, -16f), scale);
        P(d, Role.None, new Vector3(-1f, 28f + sz, -14f - (i + 1) * 10f), new Vector3(2f, 3f, 5f), 0, 0, new Vector3(0f, 32f, -16f), spike);
      }
      P(d, Role.Head, new Vector3(-8f, 22f, 78f), new Vector3(16f, 16f, 16f), 0, 0, new Vector3(0f, 30f, 78f), scale);
      P(d, Role.Head, new Vector3(-6f, 22f, 94f), new Vector3(12f, 5f, 16f), 0, 0, new Vector3(0f, 30f, 78f), scale);
      P(d, Role.Head, new Vector3(-6f, 17f, 94f), new Vector3(12f, 4f, 16f), 0, 0, new Vector3(0f, 30f, 78f), belly);
      P(d, Role.Head, new Vector3(-5f, 38f, 80f), new Vector3(2f, 4f, 6f), 0, 0, new Vector3(0f, 30f, 78f), spike);
      P(d, Role.Head, new Vector3(3f, 38f, 80f), new Vector3(2f, 4f, 6f), 0, 0, new Vector3(0f, 30f, 78f), spike);
      G(d, Role.Head, new Vector3(-8.1f, 31f, 86f), new Vector3(0.2f, 2f, 5f), new Vector3(0f, 30f, 78f), "#d84aff", true);
      G(d, Role.Head, new Vector3(7.9f, 31f, 86f), new Vector3(0.2f, 2f, 5f), new Vector3(0f, 30f, 78f), "#d84aff", true);
      // the wings: a bone and a skin
      P(d, Role.WingL, new Vector3(-68f, 40f, 4f), new Vector3(56f, 4f, 4f), 0, 0, new Vector3(-12f, 42f, 6f), scale);
      P(d, Role.WingL, new Vector3(-68f, 41f, -36f), new Vector3(56f, 1f, 40f), 0, 0, new Vector3(-12f, 42f, 6f), belly);
      P(d, Role.WingR, new Vector3(12f, 40f, 4f), new Vector3(56f, 4f, 4f), 0, 0, new Vector3(12f, 42f, 6f), scale);
      P(d, Role.WingR, new Vector3(12f, 41f, -36f), new Vector3(56f, 1f, 40f), 0, 0, new Vector3(12f, 42f, 6f), belly);
      Legs4(d, 6f, 30f, -10f, 7f, 20f, scale);

      d = D(SbMobType.EndCrystal, "mob_end_crystal", "End Crystal", "Heals the Ender Dragon. Hit or shoot it: it blows up.", "BOSSES", 1, 0f, 50f, "glass", "glass", "glass");
      d.NoAi = true; d.BlastOnDeath = true; d.Hidden = true;
      P(d, Role.None, new Vector3(-6f, 0f, -6f), new Vector3(12f, 4f, 12f), 0, 0, new Vector3(0f, 2f, 0f), "#3a3a3a");
      G(d, Role.Spin, new Vector3(-2.5f, 13.5f, -2.5f), new Vector3(5f, 5f, 5f), new Vector3(0f, 16f, 0f), "#ff55d8", true);
      Cage(d, Role.Spin, new Vector3(0f, 16f, 0f), 9f, 0.6f, "#e8b8ff");
      Cage(d, Role.Spin2, new Vector3(0f, 16f, 0f), 12f, 0.6f, "#c890ff");

      // the archers and the blaze shoot (SandBoxMobBrain.cs)
      foreach (SbMobType t in new[] { SbMobType.Skeleton, SbMobType.Stray, SbMobType.Bogged, SbMobType.Pillager })
      {
        Def a;
        if (!Defs.TryGetValue(t, out a)) continue;
        a.Shoots = true; a.Shot = SbAmmo.Arrow; a.ShotEvery = 2.2f; a.ShotRange = 16f;
      }
      Def blaze;
      if (Defs.TryGetValue(SbMobType.Blaze, out blaze)) { blaze.Shoots = true; blaze.Shot = SbAmmo.Fireball; blaze.ShotEvery = 4f; blaze.ShotRange = 18f; blaze.Flies = true; blaze.FlyHeight = 2.5f; blaze.KeepAway = 6f; }
    }
  }
}
