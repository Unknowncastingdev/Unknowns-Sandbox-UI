using System;
using UnityEngine;

namespace UnknownsSandbox
{
  // realistic gun and bomb models, built from many small parts in real
  // proportions (they replace the toy blaster-kit models). Guns point down +Z
  // with the hand (grip) at the origin; bombs sit centred on the origin.
  internal static class SbGunArt
  {
    private static Color C(string h) { return SbCatalog.C(h); }
    private static readonly Color Black = C("#1c1d20"), Polymer = C("#232427"), Metal = C("#2b2d31"), Gunmetal = C("#43474e"),
      Steel = C("#8e949c"), Bright = C("#b9bec5"), Wood = C("#6b4426"), WoodDark = C("#4a2e18"), Rubber = C("#141414"),
      Olive = C("#4b5320"), Tan = C("#b49a6b"), Brass = C("#c9a24a"), Red = C("#b52a22"), Orange = C("#e0661c");

    private static readonly Quaternion AlongZ = Quaternion.Euler(90f, 0f, 0f);   // Y parts (cylinders) along +Z

    // a barrel / tube along +Z with a dark bore at the front
    private static void Barrel(SbMeshKit k, Vector3 c, float r, float len, Color col, int seg = 14)
    {
      k.Tube(c, r, len, col, seg);
      k.Tube(c + Vector3.forward * (len * 0.5f + 0.0005f), r * 0.55f, 0.002f, Black, seg);
    }

    // a picatinny rail along +Z: a base strip and teeth
    private static void Rail(SbMeshKit k, Vector3 c, float len, Quaternion r)
    {
      k.Box(c, new Vector3(0.016f, 0.004f, len), r, Black);
      int n = Mathf.Max(2, Mathf.RoundToInt(len / 0.01f));
      for (int i = 0; i < n; i++)
      {
        float z = -len * 0.5f + (i + 0.5f) * len / n;
        k.Box(c + r * new Vector3(0f, 0.004f, z), new Vector3(0.02f, 0.004f, len / n * 0.55f), r, Metal);
      }
    }

    private static void TriggerGroup(SbMeshKit k, Vector3 at, float size = 1f)
    {
      k.Box(at + new Vector3(0f, -0.022f, 0.002f) * size, new Vector3(0.008f, 0.004f, 0.052f) * size, Black);
      k.Box(at + new Vector3(0f, -0.008f, 0.027f) * size, new Vector3(0.008f, 0.03f, 0.004f) * size, Black);
      k.Box(at + new Vector3(0f, -0.008f, 0.004f) * size, new Vector3(0.005f, 0.02f, 0.005f) * size, Quaternion.Euler(-15f, 0f, 0f), Steel);
    }

    private static void PistolGrip(SbMeshKit k, Vector3 at, Color col, float tilt = 18f)
    {
      Quaternion r = Quaternion.Euler(tilt, 0f, 0f);
      k.Box(at, new Vector3(0.028f, 0.1f, 0.042f), r, col);
      // stippling ridges down the front and back
      for (int i = 0; i < 5; i++)
      {
        Vector3 y = r * new Vector3(0f, -0.035f + i * 0.017f, 0.022f);
        k.Box(at + y, new Vector3(0.029f, 0.004f, 0.004f), r, Black);
      }
      k.Box(at + r * new Vector3(0f, -0.052f, -0.004f), new Vector3(0.031f, 0.008f, 0.05f), r, Black);
    }

    // ================================================================ guns
    internal static void Pistol(SbMeshKit k, Color frame, Color slide)
    {
      PistolGrip(k, new Vector3(0f, -0.02f, -0.008f), frame);
      k.Box(new Vector3(0f, 0.033f, 0.045f), new Vector3(0.026f, 0.026f, 0.14f), frame);              // frame
      Rail(k, new Vector3(0f, 0.018f, 0.09f), 0.04f, Quaternion.Euler(180f, 0f, 0f));                  // under rail
      k.Box(new Vector3(0f, 0.06f, 0.052f), new Vector3(0.028f, 0.03f, 0.186f), slide);               // slide
      for (int i = 0; i < 7; i++) k.Box(new Vector3(0f, 0.06f, -0.03f + i * 0.006f), new Vector3(0.0292f, 0.024f, 0.0018f), Black);
      k.Box(new Vector3(0.0142f, 0.066f, 0.075f), new Vector3(0.0015f, 0.012f, 0.032f), Black);       // ejection port
      Barrel(k, new Vector3(0f, 0.06f, 0.145f), 0.0075f, 0.004f, Gunmetal);
      k.Box(new Vector3(0f, 0.078f, 0.135f), new Vector3(0.004f, 0.006f, 0.006f), Black);              // front sight
      k.Box(new Vector3(0f, 0.078f, -0.03f), new Vector3(0.018f, 0.006f, 0.007f), Black);              // rear sight
      k.Box(new Vector3(0f, 0.0795f, 0.135f), new Vector3(0.002f, 0.0012f, 0.002f), C("#7dff6a"), true);
      TriggerGroup(k, new Vector3(0f, 0.025f, 0.04f));
      k.GripAt(Vector3.zero).MuzzleAt(new Vector3(0f, 0.06f, 0.152f));
    }

    internal static void Rifle(SbMeshKit k)
    {
      PistolGrip(k, new Vector3(0f, -0.035f, -0.01f), Polymer, 22f);
      k.Box(new Vector3(0f, 0.018f, 0.04f), new Vector3(0.034f, 0.045f, 0.2f), Gunmetal);           // lower receiver
      k.Box(new Vector3(0f, 0.002f, 0.1f), new Vector3(0.03f, 0.03f, 0.07f), Gunmetal);              // mag well
      k.Box(new Vector3(0f, 0.058f, 0.06f), new Vector3(0.033f, 0.038f, 0.24f), Gunmetal);          // upper receiver
      k.Box(new Vector3(0.0168f, 0.06f, 0.06f), new Vector3(0.0015f, 0.014f, 0.05f), Black);         // ejection port
      k.Box(new Vector3(0f, 0.07f, -0.06f), new Vector3(0.022f, 0.008f, 0.02f), Black);              // charging handle
      Rail(k, new Vector3(0f, 0.08f, 0.06f), 0.22f, Quaternion.identity);
      // handguard: an octagon tube with rails
      k.Cyl(new Vector3(0f, 0.055f, 0.29f), 0.024f, 0.24f, AlongZ, Polymer, 8);
      for (int i = 0; i < 6; i++) k.Box(new Vector3(0.024f, 0.055f, 0.2f + i * 0.035f), new Vector3(0.004f, 0.01f, 0.02f), Black);
      for (int i = 0; i < 6; i++) k.Box(new Vector3(-0.024f, 0.055f, 0.2f + i * 0.035f), new Vector3(0.004f, 0.01f, 0.02f), Black);
      Barrel(k, new Vector3(0f, 0.055f, 0.47f), 0.008f, 0.12f, Metal);
      k.Box(new Vector3(0f, 0.077f, 0.43f), new Vector3(0.012f, 0.032f, 0.014f), Black);              // front sight block
      k.Tube(new Vector3(0f, 0.055f, 0.545f), 0.0115f, 0.036f, Black, 12);                           // flash hider
      for (int i = 0; i < 4; i++) k.Box(new Vector3(0f, 0.055f, 0.548f) + Quaternion.Euler(0f, 0f, i * 45f) * new Vector3(0f, 0.0116f, 0f), new Vector3(0.003f, 0.002f, 0.024f), Quaternion.Euler(0f, 0f, i * 45f), Metal);
      // curved magazine
      for (int i = 0; i < 4; i++)
        k.Box(new Vector3(0f, -0.025f - i * 0.022f, 0.105f + i * 0.008f), new Vector3(0.022f, 0.024f, 0.062f), Quaternion.Euler(-10f - i * 4f, 0f, 0f), Black);
      // stock: buffer tube and collapsible stock
      k.Tube(new Vector3(0f, 0.05f, -0.13f), 0.015f, 0.17f, Black, 14);
      k.Box(new Vector3(0f, 0.035f, -0.2f), new Vector3(0.036f, 0.075f, 0.1f), Polymer);
      k.Box(new Vector3(0f, 0.02f, -0.255f), new Vector3(0.038f, 0.11f, 0.012f), Rubber);
      // red dot sight
      k.Box(new Vector3(0f, 0.093f, 0.05f), new Vector3(0.026f, 0.012f, 0.05f), Black);
      k.Tube(new Vector3(0f, 0.112f, 0.05f), 0.016f, 0.045f, Black, 16);
      k.Tube(new Vector3(0f, 0.112f, 0.073f), 0.013f, 0.002f, C("#3a6a8a"), 16, true);
      k.Ball(new Vector3(0f, 0.112f, 0.076f), 0.0018f, C("#ff2a2a"), 6, true);
      TriggerGroup(k, new Vector3(0f, 0.012f, 0.03f));
      k.GripAt(Vector3.zero).MuzzleAt(new Vector3(0f, 0.055f, 0.565f));
    }

    internal static void Shotgun(SbMeshKit k)
    {
      // wooden stock running back from the hand
      k.Box(new Vector3(0f, -0.005f, -0.04f), new Vector3(0.03f, 0.05f, 0.08f), Quaternion.Euler(18f, 0f, 0f), Wood);
      k.Box(new Vector3(0f, 0.0f, -0.17f), new Vector3(0.036f, 0.075f, 0.22f), Quaternion.Euler(-5f, 0f, 0f), Wood);
      k.Box(new Vector3(0f, -0.01f, -0.285f), new Vector3(0.04f, 0.1f, 0.014f), Quaternion.Euler(-5f, 0f, 0f), Rubber);
      k.Box(new Vector3(0f, 0.035f, 0.055f), new Vector3(0.034f, 0.055f, 0.16f), Metal);             // receiver
      k.Box(new Vector3(0.0172f, 0.04f, 0.06f), new Vector3(0.0015f, 0.016f, 0.05f), Black);
      Barrel(k, new Vector3(0f, 0.052f, 0.42f), 0.011f, 0.57f, Gunmetal, 16);
      k.Tube(new Vector3(0f, 0.028f, 0.34f), 0.011f, 0.42f, Metal, 16);                              // magazine tube
      k.Tube(new Vector3(0f, 0.028f, 0.555f), 0.012f, 0.012f, Metal, 16);
      // ridged pump
      k.Tube(new Vector3(0f, 0.03f, 0.3f), 0.021f, 0.15f, WoodDark, 16);
      for (int i = 0; i < 9; i++) k.Torus(new Vector3(0f, 0.03f, 0.235f + i * 0.016f), 0.021f, 0.002f, AlongZ, Wood, 18, 4);
      k.Ball(new Vector3(0f, 0.066f, 0.7f), 0.0035f, Brass, 8);                                     // bead sight
      TriggerGroup(k, new Vector3(0f, 0.012f, 0.03f));
      k.GripAt(Vector3.zero).MuzzleAt(new Vector3(0f, 0.052f, 0.71f));
    }

    internal static void Sniper(SbMeshKit k)
    {
      // long olive stock with thumbhole
      k.Box(new Vector3(0f, -0.01f, -0.02f), new Vector3(0.03f, 0.06f, 0.06f), Quaternion.Euler(15f, 0f, 0f), Olive);
      k.Box(new Vector3(0f, 0.01f, -0.19f), new Vector3(0.036f, 0.085f, 0.26f), Quaternion.Euler(-4f, 0f, 0f), Olive);
      k.Box(new Vector3(0f, 0.045f, -0.16f), new Vector3(0.03f, 0.022f, 0.1f), Olive);                 // cheek rest
      k.Box(new Vector3(0f, 0.0f, -0.33f), new Vector3(0.04f, 0.11f, 0.014f), Rubber);
      k.Box(new Vector3(0f, 0.025f, 0.2f), new Vector3(0.04f, 0.035f, 0.3f), Olive);                  // fore-end
      k.Tube(new Vector3(0f, 0.05f, 0.08f), 0.017f, 0.2f, Metal, 16);                                  // action
      // bolt handle
      k.Cyl(new Vector3(0.03f, 0.05f, -0.005f), 0.004f, 0.04f, Quaternion.Euler(0f, 0f, 90f), Steel, 8);
      k.Ball(new Vector3(0.052f, 0.05f, -0.005f), 0.008f, Steel, 10);
      Barrel(k, new Vector3(0f, 0.05f, 0.5f), 0.01f, 0.62f, Metal, 16);
      // muzzle brake
      k.Tube(new Vector3(0f, 0.05f, 0.83f), 0.015f, 0.05f, Black, 12);
      for (int i = 0; i < 3; i++) k.Box(new Vector3(0f, 0.05f, 0.815f + i * 0.014f), new Vector3(0.032f, 0.008f, 0.005f), Metal);
      // scope: tube, bells, turrets, lenses
      k.Box(new Vector3(0f, 0.075f, 0.06f), new Vector3(0.014f, 0.012f, 0.012f), Black);
      k.Box(new Vector3(0f, 0.075f, 0.16f), new Vector3(0.014f, 0.012f, 0.012f), Black);
      k.Tube(new Vector3(0f, 0.105f, 0.11f), 0.014f, 0.22f, Black, 18);
      k.Frustum(new Vector3(0f, 0.105f, 0.245f), 0.014f, 0.024f, 0.05f, AlongZ, Black, 18);
      k.Frustum(new Vector3(0f, 0.105f, -0.025f), 0.021f, 0.016f, 0.05f, AlongZ, Black, 18);
      k.Tube(new Vector3(0f, 0.105f, 0.271f), 0.022f, 0.002f, C("#2f5a7a"), 18, true);
      k.Tube(new Vector3(0f, 0.105f, -0.051f), 0.016f, 0.002f, C("#2f5a7a"), 18, true);
      k.Cyl(new Vector3(0f, 0.125f, 0.11f), 0.009f, 0.014f, Quaternion.identity, Black, 12);
      k.Cyl(new Vector3(0.02f, 0.105f, 0.11f), 0.009f, 0.014f, Quaternion.Euler(0f, 0f, 90f), Black, 12);
      // folded bipod
      k.Box(new Vector3(0f, 0.005f, 0.33f), new Vector3(0.03f, 0.012f, 0.02f), Black);
      k.Cyl(new Vector3(0.012f, 0.002f, 0.42f), 0.004f, 0.17f, AlongZ, Steel, 8);
      k.Cyl(new Vector3(-0.012f, 0.002f, 0.42f), 0.004f, 0.17f, AlongZ, Steel, 8);
      // box magazine
      k.Box(new Vector3(0f, -0.005f, 0.07f), new Vector3(0.026f, 0.05f, 0.06f), Black);
      TriggerGroup(k, new Vector3(0f, 0.005f, 0.02f));
      k.GripAt(Vector3.zero).MuzzleAt(new Vector3(0f, 0.05f, 0.86f));
    }

    internal static void Minigun(SbMeshKit k)
    {
      // rear housing and motor, carry handle, the six barrels in their clamps
      k.Box(new Vector3(0f, 0.04f, 0.03f), new Vector3(0.11f, 0.11f, 0.16f), Gunmetal);
      k.Tube(new Vector3(0f, 0.04f, -0.07f), 0.045f, 0.06f, Metal, 18);
      k.Box(new Vector3(0f, 0.115f, 0.03f), new Vector3(0.02f, 0.012f, 0.13f), Black);
      k.Box(new Vector3(0f, 0.105f, -0.03f), new Vector3(0.02f, 0.03f, 0.012f), Black);
      k.Box(new Vector3(0f, 0.105f, 0.09f), new Vector3(0.02f, 0.03f, 0.012f), Black);
      PistolGrip(k, new Vector3(0f, -0.04f, 0f), Rubber, 5f);
      k.Tube(new Vector3(0f, 0.04f, 0.13f), 0.05f, 0.04f, Metal, 20);
      for (int i = 0; i < 6; i++)
      {
        float a = i / 6f * Mathf.PI * 2f;
        Vector3 off = new Vector3(Mathf.Cos(a) * 0.03f, Mathf.Sin(a) * 0.03f, 0f);
        Barrel(k, new Vector3(0f, 0.04f, 0.37f) + off, 0.008f, 0.46f, Metal, 12);
      }
      k.Torus(new Vector3(0f, 0.04f, 0.33f), 0.038f, 0.006f, AlongZ, Steel, 20, 6);
      k.Torus(new Vector3(0f, 0.04f, 0.58f), 0.038f, 0.006f, AlongZ, Steel, 20, 6);
      // ammo box and feed chute
      k.Box(new Vector3(-0.1f, 0.0f, 0.02f), new Vector3(0.07f, 0.09f, 0.12f), Olive);
      k.Box(new Vector3(-0.1f, 0.047f, 0.02f), new Vector3(0.072f, 0.006f, 0.122f), C("#3a4119"));
      for (int i = 0; i < 6; i++) k.Box(new Vector3(-0.062f + i * 0.004f, 0.03f + i * 0.004f, 0.02f), new Vector3(0.012f, 0.008f, 0.03f), Brass);
      k.GripAt(Vector3.zero).MuzzleAt(new Vector3(0f, 0.04f, 0.6f));
    }

    internal static void Laser(SbMeshKit k)
    {
      PistolGrip(k, new Vector3(0f, -0.02f, -0.008f), C("#e8e8ec"), 15f);
      k.Box(new Vector3(0f, 0.045f, 0.06f), new Vector3(0.04f, 0.045f, 0.2f), C("#d6d8de"));
      k.Box(new Vector3(0f, 0.07f, 0.04f), new Vector3(0.03f, 0.012f, 0.14f), C("#2a2c31"));
      for (int i = 0; i < 4; i++) k.Torus(new Vector3(0f, 0.045f, 0.18f + i * 0.025f), 0.02f, 0.004f, AlongZ, C("#29f0ff"), 20, 6, true);
      k.Tube(new Vector3(0f, 0.045f, 0.22f), 0.014f, 0.11f, C("#3a3c42"), 16);
      k.Frustum(new Vector3(0f, 0.045f, 0.285f), 0.016f, 0.01f, 0.02f, AlongZ, C("#2a2c31"), 16);
      k.Tube(new Vector3(0f, 0.045f, 0.296f), 0.008f, 0.003f, C("#29f0ff"), 14, true);
      k.Box(new Vector3(0.021f, 0.05f, 0.05f), new Vector3(0.002f, 0.012f, 0.08f), C("#29f0ff"), true);
      k.Box(new Vector3(-0.021f, 0.05f, 0.05f), new Vector3(0.002f, 0.012f, 0.08f), C("#29f0ff"), true);
      TriggerGroup(k, new Vector3(0f, 0.02f, 0.035f));
      k.GripAt(Vector3.zero).MuzzleAt(new Vector3(0f, 0.045f, 0.3f));
    }

    internal static void RocketLauncher(SbMeshKit k, Color tube, bool rocketLoaded)
    {
      // a tube on the shoulder: grip under it, sight on the side, a rocket in the front
      k.Tube(new Vector3(0f, 0.07f, 0.12f), 0.042f, 0.9f, tube, 22);
      k.Frustum(new Vector3(0f, 0.07f, -0.36f), 0.052f, 0.042f, 0.06f, AlongZ, tube, 22, false, false);
      k.Tube(new Vector3(0f, 0.07f, -0.39f), 0.051f, 0.004f, Black, 22);
      k.Torus(new Vector3(0f, 0.07f, 0.2f), 0.043f, 0.006f, AlongZ, Black, 22, 6);
      k.Torus(new Vector3(0f, 0.07f, -0.1f), 0.043f, 0.006f, AlongZ, Black, 22, 6);
      k.Box(new Vector3(0f, 0.03f, 0.25f), new Vector3(0.03f, 0.05f, 0.03f), Black);                // front grip
      PistolGrip(k, new Vector3(0f, -0.005f, -0.01f), Black, 12f);
      k.Box(new Vector3(0f, 0.022f, 0.0f), new Vector3(0.03f, 0.03f, 0.06f), Black);
      k.Box(new Vector3(0.05f, 0.1f, 0.0f), new Vector3(0.012f, 0.04f, 0.07f), Black);                // optical sight
      k.Tube(new Vector3(0.05f, 0.125f, 0.0f), 0.01f, 0.08f, Black, 12);
      TriggerGroup(k, new Vector3(0f, 0.02f, 0.03f));
      if (rocketLoaded)
      {
        k.Tube(new Vector3(0f, 0.07f, 0.6f), 0.035f, 0.06f, Olive, 18);
        k.Frustum(new Vector3(0f, 0.07f, 0.69f), 0.042f, 0.012f, 0.12f, AlongZ, Olive, 18);
        k.Cone(new Vector3(0f, 0.07f, 0.765f), 0.012f, 0.03f, AlongZ, Steel, 12);
      }
      k.GripAt(Vector3.zero).MuzzleAt(new Vector3(0f, 0.07f, rocketLoaded ? 0.79f : 0.58f));
    }

    internal static void GrenadeLauncher(SbMeshKit k)
    {
      // a six-round revolver launcher
      PistolGrip(k, new Vector3(0f, -0.03f, -0.01f), Black, 18f);
      k.Box(new Vector3(0f, 0.03f, 0.04f), new Vector3(0.04f, 0.04f, 0.12f), Gunmetal);
      k.Tube(new Vector3(0f, 0.06f, 0.14f), 0.075f, 0.12f, Metal, 24);
      for (int i = 0; i < 6; i++)
      {
        float a = i / 6f * Mathf.PI * 2f;
        k.Tube(new Vector3(Mathf.Cos(a) * 0.048f, 0.06f + Mathf.Sin(a) * 0.048f, 0.2f), 0.02f, 0.002f, Black, 14);
      }
      Barrel(k, new Vector3(0f, 0.11f, 0.3f), 0.024f, 0.2f, Gunmetal, 18);
      Rail(k, new Vector3(0f, 0.14f, 0.28f), 0.14f, Quaternion.identity);
      k.Box(new Vector3(0f, 0.04f, 0.28f), new Vector3(0.025f, 0.04f, 0.04f), Black);
      // folding stock
      k.Box(new Vector3(0f, 0.04f, -0.12f), new Vector3(0.012f, 0.02f, 0.2f), Black);
      k.Box(new Vector3(0f, 0.01f, -0.22f), new Vector3(0.04f, 0.09f, 0.014f), Rubber);
      TriggerGroup(k, new Vector3(0f, 0.015f, 0.03f));
      k.GripAt(Vector3.zero).MuzzleAt(new Vector3(0f, 0.11f, 0.405f));
    }

    internal static void Flamethrower(SbMeshKit k)
    {
      PistolGrip(k, new Vector3(0f, -0.025f, -0.01f), Black, 15f);
      k.Box(new Vector3(0f, 0.03f, 0.06f), new Vector3(0.04f, 0.045f, 0.18f), Gunmetal);
      Barrel(k, new Vector3(0f, 0.045f, 0.3f), 0.02f, 0.3f, Metal, 16);
      for (int i = 0; i < 8; i++) k.Torus(new Vector3(0f, 0.045f, 0.2f + i * 0.025f), 0.022f, 0.004f, AlongZ, Steel, 16, 4);
      k.Frustum(new Vector3(0f, 0.045f, 0.47f), 0.02f, 0.03f, 0.04f, AlongZ, Black, 16);
      k.Ball(new Vector3(0f, 0.025f, 0.47f), 0.006f, C("#4ab0ff"), 8, true);                      // pilot light
      // two fuel tanks on the side
      k.Cyl(new Vector3(-0.05f, 0.03f, 0.04f), 0.028f, 0.2f, AlongZ, Red, 18);
      k.Cyl(new Vector3(-0.05f, 0.03f, -0.07f), 0.028f, 0.02f, AlongZ, Bright, 18);
      k.Cyl(new Vector3(0.05f, 0.03f, 0.04f), 0.022f, 0.16f, AlongZ, Bright, 18);
      k.Torus(new Vector3(-0.05f, 0.03f, 0.14f), 0.02f, 0.004f, AlongZ, Black, 14, 4);
      k.Box(new Vector3(0f, 0.035f, 0.18f), new Vector3(0.03f, 0.05f, 0.03f), Black);
      TriggerGroup(k, new Vector3(0f, 0.015f, 0.03f));
      k.GripAt(Vector3.zero).MuzzleAt(new Vector3(0f, 0.045f, 0.5f));
    }

    internal static void PvcLauncher(SbMeshKit k)
    {
      // potato cannon: white PVC barrel, chamber, fittings and a BBQ igniter
      Color pvc = C("#e9e9e4"), fit = C("#d8d8d2");
      Barrel(k, new Vector3(0f, 0.06f, 0.33f), 0.022f, 0.5f, pvc, 18);
      k.Frustum(new Vector3(0f, 0.06f, 0.06f), 0.045f, 0.024f, 0.05f, AlongZ, fit, 18);
      k.Tube(new Vector3(0f, 0.06f, -0.06f), 0.045f, 0.2f, pvc, 20);
      k.Tube(new Vector3(0f, 0.06f, -0.165f), 0.048f, 0.012f, fit, 20);
      k.Tube(new Vector3(0f, 0.06f, 0.035f), 0.048f, 0.012f, fit, 20);
      k.Cyl(new Vector3(0f, 0.11f, -0.1f), 0.008f, 0.014f, Quaternion.identity, Red, 10);
      PistolGrip(k, new Vector3(0f, -0.005f, -0.01f), Black, 10f);
      k.Box(new Vector3(0f, 0.02f, 0f), new Vector3(0.03f, 0.03f, 0.05f), Black);
      k.GripAt(Vector3.zero).MuzzleAt(new Vector3(0f, 0.06f, 0.585f));
    }

    internal static void PaintballMarker(SbMeshKit k, Color body, Color hopper)
    {
      PistolGrip(k, new Vector3(0f, -0.02f, -0.008f), Black, 15f);
      k.Box(new Vector3(0f, 0.035f, 0.06f), new Vector3(0.034f, 0.05f, 0.16f), body);
      Barrel(k, new Vector3(0f, 0.05f, 0.26f), 0.012f, 0.24f, Metal, 16);
      for (int i = 0; i < 4; i++) k.Box(new Vector3(0f, 0.05f, 0.3f + i * 0.03f), new Vector3(0.026f, 0.003f, 0.006f), Black);
      k.Tube(new Vector3(0f, 0.035f, -0.08f), 0.025f, 0.13f, Bright, 18);                            // air tank
      k.Sphere(new Vector3(0f, 0.12f, 0.04f), new Vector3(0.05f, 0.05f, 0.07f), hopper, 18);
      k.Cyl(new Vector3(0f, 0.075f, 0.06f), 0.012f, 0.03f, Quaternion.identity, Black, 12);
      TriggerGroup(k, new Vector3(0f, 0.015f, 0.035f));
      k.GripAt(Vector3.zero).MuzzleAt(new Vector3(0f, 0.05f, 0.385f));
    }

    // ================================================================ bombs
    internal static void Grenade(SbMeshKit k, bool sticky)
    {
      Color body = sticky ? C("#3d4a2e") : Olive;
      k.Sphere(Vector3.zero, new Vector3(0.032f, 0.036f, 0.032f), body, 22);
      for (int i = 0; i < 3; i++) k.Torus(new Vector3(0f, -0.018f + i * 0.018f, 0f), 0.031f - Mathf.Abs(i - 1) * 0.005f, 0.0015f, Quaternion.identity, C("#3a4119"), 22, 4);
      k.Cyl(new Vector3(0f, 0.04f, 0f), 0.011f, 0.014f, Quaternion.identity, Steel, 14);              // fuze
      k.Box(new Vector3(0.012f, 0.03f, 0f), new Vector3(0.008f, 0.05f, 0.012f), Quaternion.Euler(0f, 0f, -12f), Steel);   // spoon
      k.Torus(new Vector3(-0.016f, 0.046f, 0f), 0.009f, 0.0015f, Quaternion.Euler(0f, 0f, 90f), Bright, 14, 4);         // pin ring
      if (sticky) k.Sphere(new Vector3(0f, -0.03f, 0f), new Vector3(0.03f, 0.012f, 0.03f), C("#9aff6a"), 16);
    }

    internal static void Dynamite(SbMeshKit k)
    {
      for (int i = 0; i < 3; i++)
      {
        float a = i / 3f * Mathf.PI * 2f;
        Vector3 c = new Vector3(Mathf.Cos(a) * 0.017f, 0f, Mathf.Sin(a) * 0.017f);
        k.Cyl(c, 0.016f, 0.2f, Quaternion.identity, C("#c0281e"), 18);
        k.Cyl(c + Vector3.up * 0.1f, 0.0162f, 0.003f, Quaternion.identity, C("#8a1a14"), 18);
      }
      k.Torus(new Vector3(0f, 0.04f, 0f), 0.034f, 0.004f, Quaternion.identity, C("#2a2a2a"), 22, 4);
      k.Torus(new Vector3(0f, -0.04f, 0f), 0.034f, 0.004f, Quaternion.identity, C("#2a2a2a"), 22, 4);
      k.Cyl(new Vector3(0.006f, 0.125f, 0f), 0.002f, 0.05f, Quaternion.Euler(0f, 0f, -15f), C("#d8c9a0"), 6);
      k.Ball(new Vector3(0.013f, 0.15f, 0f), 0.004f, Orange, 6, true);
    }

    internal static void C4(SbMeshKit k)
    {
      k.Box(Vector3.zero, new Vector3(0.18f, 0.05f, 0.11f), C("#d6d0b8"));
      for (int i = 0; i < 3; i++) k.Box(new Vector3(-0.06f + i * 0.06f, 0f, 0f), new Vector3(0.002f, 0.051f, 0.111f), C("#b9b29a"));
      k.Box(new Vector3(0.02f, 0.035f, 0f), new Vector3(0.06f, 0.02f, 0.04f), Black);               // detonator
      k.Box(new Vector3(0.02f, 0.046f, 0.008f), new Vector3(0.04f, 0.002f, 0.016f), C("#ff3a2a"), true);
      k.Ball(new Vector3(-0.005f, 0.046f, -0.01f), 0.003f, C("#3aff4a"), 6, true);
      k.Cyl(new Vector3(-0.04f, 0.03f, 0.02f), 0.002f, 0.08f, Quaternion.Euler(0f, 0f, 80f), Red, 6);
      k.Cyl(new Vector3(-0.04f, 0.03f, 0.03f), 0.002f, 0.08f, Quaternion.Euler(0f, 0f, 75f), C("#2a6aff"), 6);
      k.Box(new Vector3(0f, 0.0f, 0.056f), new Vector3(0.19f, 0.012f, 0.002f), Black);               // tape
    }

    internal static void Landmine(SbMeshKit k)
    {
      k.Cyl(Vector3.zero, 0.15f, 0.06f, Quaternion.identity, Olive, 32);
      k.Torus(new Vector3(0f, 0.03f, 0f), 0.15f, 0.006f, Quaternion.identity, C("#3a4119"), 32, 6);
      k.Cyl(new Vector3(0f, 0.034f, 0f), 0.06f, 0.012f, Quaternion.identity, C("#5a6230"), 24);
      k.Cyl(new Vector3(0f, 0.042f, 0f), 0.025f, 0.008f, Quaternion.identity, Black, 18);
      for (int i = 0; i < 8; i++) k.Ball(new Vector3(Mathf.Cos(i * 0.785f) * 0.11f, 0.031f, Mathf.Sin(i * 0.785f) * 0.11f), 0.006f, Steel, 6);
    }

    internal static void Cluster(SbMeshKit k)
    {
      k.Cyl(Vector3.zero, 0.06f, 0.28f, Quaternion.identity, C("#5b5f66"), 24);
      k.Cone(new Vector3(0f, 0.17f, 0f), 0.06f, 0.06f, Quaternion.identity, C("#5b5f66"), 24);
      for (int i = 0; i < 4; i++)
        k.Box(new Vector3(0f, -0.16f, 0f), new Vector3(0.004f, 0.06f, 0.13f), Quaternion.Euler(0f, i * 45f, 0f), C("#3d4046"));
      k.Torus(new Vector3(0f, 0.06f, 0f), 0.061f, 0.003f, Quaternion.identity, C("#ffd33a"), 24, 4);
      for (int i = 0; i < 5; i++) k.Ball(new Vector3(Mathf.Cos(i * 1.257f) * 0.061f, -0.04f, Mathf.Sin(i * 1.257f) * 0.061f), 0.012f, C("#ffd33a"), 10);
    }

    internal static void Firework(SbMeshKit k)
    {
      k.Cyl(Vector3.zero, 0.035f, 0.22f, Quaternion.identity, C("#d4284a"), 20);
      for (int i = 0; i < 5; i++) k.Torus(new Vector3(0f, -0.08f + i * 0.04f, 0f), 0.036f, 0.0035f, Quaternion.identity, C("#ffd33a"), 20, 4);
      k.Cone(new Vector3(0f, 0.15f, 0f), 0.04f, 0.08f, Quaternion.identity, C("#2a6aff"), 20);
      k.Cyl(new Vector3(0f, -0.3f, 0.03f), 0.005f, 0.4f, Quaternion.identity, C("#c9a46a"), 6);
      k.Cyl(new Vector3(0f, -0.125f, 0f), 0.0025f, 0.04f, Quaternion.identity, C("#2a2a2a"), 6);
    }

    internal static void Barrel(SbMeshKit k)
    {
      Color red = C("#b8261d");
      k.Cyl(Vector3.zero, 0.29f, 0.88f, Quaternion.identity, red, 32);
      foreach (float y in new[] { -0.44f, -0.15f, 0.15f, 0.44f }) k.Torus(new Vector3(0f, y, 0f), 0.292f, 0.012f, Quaternion.identity, C("#8a1a14"), 32, 6);
      k.Cyl(new Vector3(0.14f, 0.445f, 0f), 0.03f, 0.012f, Quaternion.identity, Steel, 14);
      k.Cyl(new Vector3(-0.15f, 0.445f, 0.05f), 0.016f, 0.012f, Quaternion.identity, Steel, 12);
      // hazard label
      k.Box(new Vector3(0f, 0f, 0.29f), new Vector3(0.24f, 0.24f, 0.004f), C("#ffcc22"));
      k.Box(new Vector3(0f, 0f, 0.292f), new Vector3(0.12f, 0.12f, 0.004f), Quaternion.Euler(0f, 0f, 45f), Black);
      k.Box(new Vector3(0f, 0.01f, 0.294f), new Vector3(0.02f, 0.06f, 0.004f), C("#ffcc22"));
    }

    internal static void BlackHole(SbMeshKit k)
    {
      k.Ball(Vector3.zero, 0.09f, C("#0d0d12"), 26);
      k.Torus(Vector3.zero, 0.11f, 0.01f, Quaternion.Euler(70f, 0f, 20f), C("#8a3aff"), 28, 6, true);
      k.Torus(Vector3.zero, 0.125f, 0.006f, Quaternion.Euler(-30f, 40f, 0f), C("#3ad8ff"), 28, 6, true);
      for (int i = 0; i < 4; i++) k.Box(Quaternion.Euler(0f, i * 90f, 0f) * new Vector3(0.088f, 0f, 0f), new Vector3(0.02f, 0.05f, 0.03f), Quaternion.Euler(0f, i * 90f, 0f), C("#3d4046"));
    }

    internal static void Nuke(SbMeshKit k, float size, Color body)
    {
      // a fat bomb: rounded body, box tail fin, bolted seam
      float s = size;
      k.Sphere(Vector3.zero, new Vector3(0.36f, 0.36f, 0.5f) * s, body, 28);
      k.Torus(new Vector3(0f, 0f, 0.05f * s), 0.355f * s, 0.012f * s, AlongZ, C("#6a6230"), 28, 6);
      for (int i = 0; i < 16; i++)
      {
        float a = i / 16f * Mathf.PI * 2f;
        k.Ball(new Vector3(Mathf.Cos(a) * 0.36f * s, Mathf.Sin(a) * 0.36f * s, 0.05f * s), 0.01f * s, Steel, 6);
      }
      k.Frustum(new Vector3(0f, 0f, -0.55f * s), 0.22f * s, 0.12f * s, 0.2f * s, Quaternion.Euler(-90f, 0f, 0f), body, 24);
      // the box fin: four plates and the ring
      for (int i = 0; i < 4; i++)
        k.Box(new Vector3(0f, 0f, -0.72f * s) + Quaternion.Euler(0f, 0f, i * 90f) * new Vector3(0f, 0.17f * s, 0f), new Vector3(0.36f * s, 0.008f * s, 0.28f * s), Quaternion.Euler(0f, 0f, i * 90f), C("#6a6230"));
      k.Box(new Vector3(0f, 0f, -0.72f * s), new Vector3(0.008f * s, 0.34f * s, 0.28f * s), C("#6a6230"));
      k.Box(new Vector3(0f, 0f, -0.72f * s), new Vector3(0.34f * s, 0.008f * s, 0.28f * s), C("#6a6230"));
      // nose fuze and a warning band
      k.Cyl(new Vector3(0f, 0f, 0.5f * s), 0.05f * s, 0.04f * s, AlongZ, Steel, 18);
      k.Torus(new Vector3(0f, 0f, 0.3f * s), 0.29f * s, 0.02f * s, AlongZ, C("#ffcc22"), 28, 6);
    }

    // ================================================================ apply
    internal static void Apply(Func<string, SbItem> get)
    {
      Action<string, Action<SbMeshKit>> set = (id, b) => { SbItem it = get(id); if (it != null) it.Build = b; };

      set("gun_pistol", k => Pistol(k, Polymer, Gunmetal));
      set("gun_rifle", Rifle);
      set("gun_shotgun", Shotgun);
      set("gun_sniper", Sniper);
      set("gun_minigun", Minigun);
      set("gun_laser", Laser);
      set("gun_rocket", k => RocketLauncher(k, Olive, true));
      set("gun_grenadelauncher", GrenadeLauncher);
      set("gun_flame", Flamethrower);
      // the fun guns: real guns carrying their fun
      set("gun_melon", k =>
      {
        RocketLauncher(k, C("#2f7a2a"), false);
        Vector3 top = new Vector3(0f, 0.07f, 0.66f);
        k.Sphere(top, new Vector3(0.05f, 0.045f, 0.065f), C("#3a9a2a"), 20);
        for (int i = 0; i < 6; i++) k.Sphere(top, new Vector3(0.0505f, 0.0455f, 0.0655f), Quaternion.Euler(0f, 0f, i * 30f), C("#215a18"), 6);
        k.MuzzleAt(new Vector3(0f, 0.07f, 0.73f));
      });
      set("gun_potato", k =>
      {
        PvcLauncher(k);
        k.Sphere(new Vector3(0f, 0.06f, 0.6f), new Vector3(0.02f, 0.018f, 0.026f), C("#b08a4a"), 12);
      });
      set("gun_banana", k =>
      {
        Pistol(k, C("#e8c21c"), C("#ffe14d"));
        for (int i = 0; i < 7; i++) { float t = (i - 3f) / 3f; k.Sphere(new Vector3(0f, 0.1f + t * t * 0.02f, 0.05f + t * 0.06f), new Vector3(0.014f, 0.014f, 0.016f), C(i == 0 || i == 6 ? "#7a5a20" : "#ffe14d"), 10); }
      });
      set("gun_snowball", k =>
      {
        PaintballMarker(k, C("#cfe8ff"), C("#f4f8ff"));
        k.Ball(new Vector3(0f, 0.17f, 0.04f), 0.03f, Color.white, 16);
      });
      set("gun_paint", k => PaintballMarker(k, C("#2a2c31"), C("#ff3df5")));
      set("gun_bubble", k =>
      {
        Pistol(k, C("#3ad8c8"), C("#8affef"));
        k.Torus(new Vector3(0f, 0.06f, 0.18f), 0.04f, 0.005f, Quaternion.Euler(90f, 0f, 0f), C("#8affef"), 24, 6, true);
        k.Cyl(new Vector3(0f, 0.06f, 0.16f), 0.004f, 0.03f, Quaternion.Euler(90f, 0f, 0f), C("#3ad8c8"), 8);
        k.Sphere(new Vector3(0f, -0.02f, 0.06f), new Vector3(0.025f, 0.035f, 0.025f), C("#bff8ff"), 16);
        k.MuzzleAt(new Vector3(0f, 0.06f, 0.19f));
      });
      set("gun_confetti", k =>
      {
        PistolGrip(k, new Vector3(0f, -0.02f, -0.008f), Black, 15f);
        k.Box(new Vector3(0f, 0.03f, 0.04f), new Vector3(0.035f, 0.04f, 0.1f), C("#2a2c31"));
        k.Frustum(new Vector3(0f, 0.05f, 0.2f), 0.025f, 0.075f, 0.24f, AlongZ, C("#ff4bd8"), 24);
        for (int i = 0; i < 6; i++) k.Torus(new Vector3(0f, 0.05f, 0.1f + i * 0.04f), 0.029f + i * 0.009f, 0.003f, AlongZ, C(i % 2 == 0 ? "#ffd33a" : "#3ad8ff"), 24, 4);
        k.GripAt(Vector3.zero).MuzzleAt(new Vector3(0f, 0.05f, 0.32f));
      });
      set("gun_duck", k =>
      {
        RocketLauncher(k, C("#2f6aff"), false);
        Vector3 top = new Vector3(0f, 0.13f, 0.1f);
        k.Sphere(top, new Vector3(0.05f, 0.04f, 0.06f), C("#ffd21f"), 16);
        k.Ball(top + new Vector3(0f, 0.045f, 0.035f), 0.03f, C("#ffd21f"), 14);
        k.Sphere(top + new Vector3(0f, 0.04f, 0.07f), new Vector3(0.015f, 0.007f, 0.02f), C("#ff8a1f"), 10);
      });

      set("bomb_grenade", k => Grenade(k, false));
      set("bomb_sticky", k => Grenade(k, true));
      set("bomb_dynamite", Dynamite);
      set("bomb_c4", C4);
      set("bomb_landmine", Landmine);
      set("bomb_cluster", Cluster);
      set("bomb_firework", Firework);
      set("bomb_barrel", Barrel);
      set("bomb_blackhole", BlackHole);
      set("bomb_mininuke", k => Nuke(k, 0.45f, C("#7a7a3a")));
      set("bomb_nuke", k => Nuke(k, 1f, C("#8a8a40")));
    }
  }
}
