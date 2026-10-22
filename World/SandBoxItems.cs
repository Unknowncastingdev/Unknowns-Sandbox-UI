using System;
using System.Collections.Generic;
using GorillaLocomotion;
using UnityEngine;

namespace UnknownsSandbox
{
  // Minecraft items.
  //  COMBAT: bow (hold the trigger to draw, let go to shoot; the longer, the further),
  //    shield (held up in front of you it stops arrows and fireballs), helmets and
  //    chestplates (trigger while holding one: you wear it, everyone sees it; less knockback).
  //  ITEMS: fishing rod (trigger casts, trigger again reels in: in water a fish bites now
  //    and then), potions of speed, jump boost, night vision and slow falling (trigger:
  //    drink), elytra (wear it, then fall with your arms spread out to glide).
  //  TRANSPORT: boats float on water blocks; minecarts ride along rails (powered rails
  //    boost them), both driven like the obby car.
  //  Speed, jumping, slow falling and gliding change how you move, so they only work
  //  where the ender pearl works (a SandBox lobby where everyone has the SandBox).
  internal enum SbArmorSlot { Head = 0, Chest = 1 }

  internal static class SbMcItems
  {
    private static Color C(string h) { return SbCatalog.C(h); }

    internal static readonly string[] ArmorMats = { "leather", "iron", "gold", "diamond", "netherite" };
    private static readonly string[] ArmorCols = { "#8a5a2a", "#d8d8d8", "#f2c230", "#5ae0e8", "#3a3638" };

    internal static Color ArmorColour(string id)
    {
      for (int i = 0; i < ArmorMats.Length; i++) if (id.Contains("_" + ArmorMats[i])) return C(ArmorCols[i]);
      return Color.grey;
    }

    // the helmet / chestplate shape (also worn on the rig, in its own units: a head is 0.24 m)
    internal static void Helmet(SbMeshKit k, Color c)
    {
      Color d = c * 0.8f; d.a = 1f;
      k.Box(new Vector3(0f, 0.11f, 0f), new Vector3(0.26f, 0.04f, 0.26f), c);
      k.Box(new Vector3(-0.12f, 0.03f, 0f), new Vector3(0.025f, 0.14f, 0.26f), d);
      k.Box(new Vector3(0.12f, 0.03f, 0f), new Vector3(0.025f, 0.14f, 0.26f), d);
      k.Box(new Vector3(0f, 0.03f, -0.12f), new Vector3(0.26f, 0.14f, 0.025f), d);
      k.Box(new Vector3(0f, 0.09f, 0.12f), new Vector3(0.26f, 0.05f, 0.025f), c);
    }

    internal static void Chestplate(SbMeshKit k, Color c)
    {
      Color d = c * 0.8f; d.a = 1f;
      k.Box(new Vector3(0f, 0f, 0.09f), new Vector3(0.34f, 0.36f, 0.04f), c);
      k.Box(new Vector3(0f, 0f, -0.09f), new Vector3(0.34f, 0.36f, 0.04f), d);
      k.Box(new Vector3(-0.17f, 0.02f, 0f), new Vector3(0.04f, 0.32f, 0.2f), d);
      k.Box(new Vector3(0.17f, 0.02f, 0f), new Vector3(0.04f, 0.32f, 0.2f), d);
      k.Box(new Vector3(-0.2f, 0.15f, 0f), new Vector3(0.1f, 0.06f, 0.22f), c);
      k.Box(new Vector3(0.2f, 0.15f, 0f), new Vector3(0.1f, 0.06f, 0.22f), c);
    }

    internal static void Elytra(SbMeshKit k)
    {
      Color a = C("#8a8a9a"), b = C("#6a5a8a");
      k.Box(new Vector3(-0.14f, -0.05f, -0.1f), new Vector3(0.22f, 0.5f, 0.02f), Quaternion.Euler(0f, 0f, -12f), a);
      k.Box(new Vector3(0.14f, -0.05f, -0.1f), new Vector3(0.22f, 0.5f, 0.02f), Quaternion.Euler(0f, 0f, 12f), a);
      k.Box(new Vector3(-0.14f, -0.25f, -0.11f), new Vector3(0.16f, 0.12f, 0.02f), Quaternion.Euler(0f, 0f, -12f), b);
      k.Box(new Vector3(0.14f, -0.25f, -0.11f), new Vector3(0.16f, 0.12f, 0.02f), Quaternion.Euler(0f, 0f, 12f), b);
    }

    private static void Potion(SbMeshKit k, Color liquid)
    {
      k.Sphere(new Vector3(0f, 0f, 0f), new Vector3(0.05f, 0.05f, 0.05f), new Color(0.85f, 0.92f, 1f), 12);
      k.Sphere(new Vector3(0f, -0.005f, 0f), new Vector3(0.044f, 0.04f, 0.044f), liquid, 10, true);
      k.Cyl(new Vector3(0f, 0.06f, 0f), 0.016f, 0.04f, Quaternion.identity, new Color(0.85f, 0.92f, 1f), 10);
      k.Cyl(new Vector3(0f, 0.085f, 0f), 0.014f, 0.015f, Quaternion.identity, C("#a07a4a"), 10);
      k.GripAt(new Vector3(0f, 0f, 0f)).MuzzleAt(new Vector3(0f, 0.09f, 0f));
    }

    internal static SbVehicleSpec BoatSpec()
    {
      var s = new SbVehicleSpec { Wheels = new Vector3[0], WheelRadius = 0.2f, Seat = new Vector3(0f, 0.3f, -0.2f), Steer = new Vector3(0f, 0.45f, 0.3f), Mass = 120f, Motor = 0f, TopSpeed = 7f, Boat = true };
      Color oak = C("#a07a4a"), dark = C("#7a5a32");
      s.Body = k =>
      {
        k.Box(new Vector3(0f, 0.06f, 0f), new Vector3(1.0f, 0.06f, 1.8f), dark);                       // floor
        k.Box(new Vector3(-0.5f, 0.22f, 0f), new Vector3(0.06f, 0.32f, 1.8f), oak);                    // sides
        k.Box(new Vector3(0.5f, 0.22f, 0f), new Vector3(0.06f, 0.32f, 1.8f), oak);
        k.Box(new Vector3(0f, 0.22f, 0.9f), new Vector3(1.0f, 0.32f, 0.06f), oak);
        k.Box(new Vector3(0f, 0.22f, -0.9f), new Vector3(1.0f, 0.32f, 0.06f), oak);
        k.Box(new Vector3(0f, 0.2f, -0.2f), new Vector3(0.94f, 0.04f, 0.3f), dark);                    // the seat plank
        k.Cyl(new Vector3(-0.6f, 0.3f, 0.1f), 0.02f, 0.9f, Quaternion.Euler(70f, 0f, 0f), dark, 8);   // paddles
        k.Cyl(new Vector3(0.6f, 0.3f, 0.1f), 0.02f, 0.9f, Quaternion.Euler(70f, 0f, 0f), dark, 8);
      };
      return s;
    }

    internal static SbVehicleSpec CartSpec()
    {
      var s = new SbVehicleSpec { Wheels = new Vector3[0], WheelRadius = 0.12f, Seat = new Vector3(0f, 0.22f, 0f), Steer = new Vector3(0f, 0.5f, 0.3f), Mass = 90f, Motor = 0f, TopSpeed = 9f, Cart = true };
      Color iron = C("#8a8e94"), dark = C("#4a4e54");
      s.Body = k =>
      {
        k.Box(new Vector3(0f, 0.12f, 0f), new Vector3(0.7f, 0.05f, 0.9f), dark);
        k.Box(new Vector3(-0.35f, 0.32f, 0f), new Vector3(0.05f, 0.4f, 0.9f), iron);
        k.Box(new Vector3(0.35f, 0.32f, 0f), new Vector3(0.05f, 0.4f, 0.9f), iron);
        k.Box(new Vector3(0f, 0.32f, 0.45f), new Vector3(0.7f, 0.4f, 0.05f), iron);
        k.Box(new Vector3(0f, 0.32f, -0.45f), new Vector3(0.7f, 0.4f, 0.05f), iron);
        foreach (float x in new[] { -0.3f, 0.3f })
          foreach (float z in new[] { -0.3f, 0.3f })
            k.Cyl(new Vector3(x, 0.08f, z), 0.08f, 0.06f, Quaternion.Euler(0f, 0f, 90f), C("#2a2a2e"), 12);
      };
      return s;
    }

    internal static void AddToCatalog(Action<SbItem> add)
    {
      Func<string, int> T = SbBlockArt.T;
      // ---- combat
      add(new SbItem
      {
        Id = "mc_bow", Name = "Bow", Desc = "Hold the trigger to draw, let go to shoot an arrow. The longer you draw, the further it flies.", Tab = SbTab.Minecraft, Section = "COMBAT",
        Kind = SbKind.Tool, Holdable = true, DefaultAnchored = true, Sound = "wood",
        Build = k =>
        {
          Color wood = C("#7a5a32");
          for (int i = -3; i <= 3; i++)
          {
            float a = i * 13f * Mathf.Deg2Rad;
            k.Box(new Vector3(0f, Mathf.Sin(a) * 0.32f, -Mathf.Cos(a) * 0.32f + 0.32f), new Vector3(0.025f, 0.1f, 0.03f), Quaternion.Euler(-i * 13f, 0f, 0f), wood);
          }
          k.Box(new Vector3(0f, 0f, -0.04f), new Vector3(0.004f, 0.56f, 0.004f), C("#e8e8e8"));
          k.Box(new Vector3(0f, 0f, 0.02f), new Vector3(0.03f, 0.08f, 0.035f), C("#4a3018"));
          k.GripAt(new Vector3(0f, 0f, 0.02f)).MuzzleAt(new Vector3(0f, 0f, 0.1f));
        }
      });
      add(new SbItem
      {
        Id = "mc_shield", Name = "Shield", Desc = "Hold it up in front of you: arrows and fireballs bounce off.", Tab = SbTab.Minecraft, Section = "COMBAT",
        Kind = SbKind.Tool, Holdable = true, DefaultAnchored = true, Sound = "wood",
        Build = k =>
        {
          k.Box(new Vector3(0f, 0.05f, 0.04f), new Vector3(0.38f, 0.56f, 0.03f), C("#a07a4a"));
          k.Box(new Vector3(0f, 0.05f, 0.058f), new Vector3(0.04f, 0.56f, 0.01f), C("#8a8e94"));
          k.Box(new Vector3(0f, 0.33f, 0.058f), new Vector3(0.38f, 0.03f, 0.01f), C("#8a8e94"));
          k.Box(new Vector3(0f, -0.23f, 0.058f), new Vector3(0.38f, 0.03f, 0.01f), C("#8a8e94"));
          k.Box(new Vector3(0f, 0.05f, -0.0f), new Vector3(0.04f, 0.12f, 0.05f), C("#3a2a1a"));
          k.GripAt(Vector3.zero).MuzzleAt(new Vector3(0f, 0.05f, 0.06f));
        }
      });
      for (int i = 0; i < ArmorMats.Length; i++)
      {
        string m = ArmorMats[i];
        Color col = C(ArmorCols[i]);
        string nm = char.ToUpperInvariant(m[0]) + m.Substring(1);
        add(new SbItem { Id = "mc_helmet_" + m, Name = nm + " Helmet", Desc = "Hold it and pull the trigger to wear it (again to take it off). Less knockback.", Tab = SbTab.Minecraft, Section = "COMBAT", Kind = SbKind.Tool, Holdable = true, DefaultAnchored = true, Sound = "metal", Build = k => Helmet(k, col) });
        add(new SbItem { Id = "mc_chestplate_" + m, Name = nm + " Chestplate", Desc = "Hold it and pull the trigger to wear it (again to take it off). Less knockback.", Tab = SbTab.Minecraft, Section = "COMBAT", Kind = SbKind.Tool, Holdable = true, DefaultAnchored = true, Sound = "metal", Build = k => Chestplate(k, col) });
      }
      // ---- items
      add(new SbItem
      {
        Id = "mc_fishing_rod", Name = "Fishing Rod", Desc = "Trigger casts, trigger again reels in. In water a fish bites now and then (the bobber dips): reel it in!", Tab = SbTab.Minecraft, Section = "ITEMS",
        Kind = SbKind.Tool, Holdable = true, DefaultAnchored = true, Sound = "wood",
        Build = k =>
        {
          k.Cyl(new Vector3(0f, 0f, 0.3f), 0.012f, 0.75f, Quaternion.Euler(90f, 0f, 0f), C("#7a5a32"), 8);
          k.Cyl(new Vector3(0f, -0.03f, 0.05f), 0.03f, 0.03f, Quaternion.Euler(0f, 0f, 90f), C("#8a8e94"), 12);
          k.GripAt(Vector3.zero).MuzzleAt(new Vector3(0f, 0f, 0.68f));
        }
      });
      foreach (var p in new[] { ("speed", "Potion of Swiftness", "#7cafc6"), ("jump", "Potion of Leaping", "#22ff4c"), ("night", "Potion of Night Vision", "#1f1fa1"), ("slowfall", "Potion of Slow Falling", "#f7f8e0") })
      {
        Color liquid = C(p.Item3);
        add(new SbItem { Id = "mc_potion_" + p.Item1, Name = p.Item2, Desc = "Trigger: drink it. Lasts 45 seconds.", Tab = SbTab.Minecraft, Section = "ITEMS", Kind = SbKind.Tool, Holdable = true, DefaultAnchored = true, Sound = "glass", Build = k => Potion(k, liquid) });
      }
      add(new SbItem { Id = "mc_elytra", Name = "Elytra", Desc = "Trigger: wear it. Then fall with your arms spread out wide to glide; lean your head to steer.", Tab = SbTab.Minecraft, Section = "ITEMS", Kind = SbKind.Tool, Holdable = true, DefaultAnchored = true, Sound = "wool", Build = Elytra });
      // ---- transport
      SbVehicleSpec boat = BoatSpec(), cart = CartSpec();
      add(new SbItem { Id = "mc_boat", Name = "Oak Boat", Desc = "Floats on water blocks. Grip the front to get in. LEFT STICK: paddle and steer, B / Y: get out.", Tab = SbTab.Minecraft, Section = "TRANSPORT", Kind = SbKind.Prop, Vehicle = boat, Col = SbCol.Box, DefaultAnchored = false, Mass = boat.Mass, Sound = "wood", Build = k => SbVehicleArt.Full(k, boat) });
      add(new SbItem { Id = "mc_minecart", Name = "Minecart", Desc = "Put it on rails and get in (grip the front). LEFT STICK: push forward or back. Powered rails boost it.", Tab = SbTab.Minecraft, Section = "TRANSPORT", Kind = SbKind.Prop, Vehicle = cart, Col = SbCol.Box, DefaultAnchored = false, Mass = cart.Mass, Sound = "metal", Build = k => SbVehicleArt.Full(k, cart) });
      add(new SbItem { Id = "mc_rail", Name = "Rail", Desc = "Minecarts ride along it. Lines up with the way you face; turns by itself at corners.", Tab = SbTab.Minecraft, Section = "TRANSPORT", Kind = SbKind.Block, Shape = SbShape.Rail, Top = T("rail"), Side = T("rail"), Bottom = T("rail"), Front = T("rail"), Sound = "metal" });
      add(new SbItem { Id = "mc_powered_rail", Name = "Powered Rail", Desc = "A booster: minecarts speed up on it.", Tab = SbTab.Minecraft, Section = "TRANSPORT", Kind = SbKind.Block, Shape = SbShape.Rail, Top = T("powered_rail_on"), Side = T("powered_rail_on"), Bottom = T("powered_rail_on"), Front = T("powered_rail_on"), Sound = "metal" });
    }
  }

  // ==================================================================== the world side
  internal sealed partial class SandBoxWorld
  {
    private float bowChargeL, bowChargeR;
    private bool bowDrawingL, bowDrawingR;
    private readonly string[] armorOn = new string[2];          // your helmet / chestplate (item id)
    private readonly GameObject[] armorVis = new GameObject[2];
    private bool elytraOn;
    private readonly Dictionary<long, GameObject> remoteArmor = new Dictionary<long, GameObject>();   // (actor << 8 | slot)
    private float speedUntil, jumpUntil, nightUntil, slowFallUntil;
    private float baseMaxJump = -1f, baseJumpMult = -1f;
    private bool potionApplied;
    private bool nightOn;
    private Color nightSavedAmbient;
    private float nightSavedIntensity;

    // ================================================================ held items
    private bool UseMcItem(SbObject o, bool right, bool edge)
    {
      string id = o.Item.Id;
      bool trig = TriggerValue(right) > 0.6f;
      if (id == "mc_bow") { UpdateBow(o, right, trig); return true; }
      if (id == "ml_grapple") return UseGrapple(o, right, trig, edge);   // SandBoxMelee.cs
      if (id == "th_knives" || id == "th_boomerang") return UseThrown(o, right, edge);
      if (id == "mc_firework_rocket") { if (edge) UseFirework(o, right); return true; }   // SandBoxEffects.cs
      if (id == "mc_shield") return true;   // held up: SandBoxBosses.MobShotHitsMe checks it
      if (id == "mc_fishing_rod") { if (edge) CastOrReel(o); return true; }
      if (id.StartsWith("mc_potion_", StringComparison.Ordinal)) { if (edge) Drink(o, right); return true; }
      if (id.StartsWith("mc_helmet_", StringComparison.Ordinal)) { if (edge) Wear(o, right, SbArmorSlot.Head); return true; }
      if (id.StartsWith("mc_chestplate_", StringComparison.Ordinal) || id == "mc_elytra") { if (edge) Wear(o, right, SbArmorSlot.Chest); return true; }
      return false;
    }

    // ---------------------------------------------------------------- bow
    private void UpdateBow(SbObject bow, bool right, bool trig)
    {
      bool drawing = right ? bowDrawingR : bowDrawingL;
      float charge = right ? bowChargeR : bowChargeL;
      if (trig)
      {
        if (!drawing) SbFx.PlayMc("bow_pull", SbSound.Swing, bow.T.position, 0.5f, 0.8f, 12f);
        drawing = true;
        charge = Mathf.Min(1f, charge + Time.deltaTime);
        if (Mathf.Repeat(Time.time, 0.2f) < Time.deltaTime) Haptic?.Invoke(right, 0.05f + charge * 0.15f, 0.02f);
      }
      else if (drawing)
      {
        drawing = false;
        if (charge > 0.12f)
        {
          SbItem arrow = SbCatalog.Get("proj_arrow");
          if (arrow != null)
          {
            Vector3 from = bow.T.TransformPoint(bow.Item.Muzzle);
            int seed = UnityEngine.Random.Range(1, int.MaxValue);
            shotSpeed = 0.35f + charge * 0.9f;
            try { Shoot(arrow, from, bow.T.forward, seed, true, 1f); }
            finally { shotSpeed = 1f; }
            SbNet.SendShot(arrow.Key, from, bow.T.forward, seed, 1f);
            SbFx.PlayMc("bow_shoot", SbSound.Swing, from, 0.9f, 0.9f + charge * 0.3f, 20f);
            Haptic?.Invoke(right, 0.5f * charge + 0.2f, 0.06f);
          }
        }
        charge = 0f;
      }
      if (right) { bowDrawingR = drawing; bowChargeR = charge; } else { bowDrawingL = drawing; bowChargeL = charge; }
    }

    // arrow speed (the bow's draw), applied by Shoot
    private static float shotSpeed = 1f;

    // ---------------------------------------------------------------- shield
    // a mob shot coming at you, stopped by a shield you hold up in front of you
    internal bool ShieldBlocks(Vector3 shotDir, Vector3 at)
    {
      foreach (SbObject s in new[] { heldL, heldR })
      {
        if (s == null || s.T == null || s.Item.Id != "mc_shield") continue;
        // the shield's face towards the shot, and near its path
        if (Vector3.Dot(s.T.forward, -shotDir) < 0.3f) continue;
        if ((s.T.position - at).sqrMagnitude > 0.7f * 0.7f) continue;
        SbFx.PlayMc("shield_block", SbSound.Thunk, s.T.position, 1f, 1f, 16f);
        SbFx.Sparks(s.T.position + s.T.forward * 0.05f, s.T.forward, new Color(1f, 0.9f, 0.6f), 12);
        Haptic?.Invoke(s == heldR, 0.6f, 0.08f);
        return true;
      }
      return false;
    }

    // ---------------------------------------------------------------- fishing
    private GameObject bobber;
    private LineRenderer fishLine;
    private SbObject bobberRod;
    private Vector3 bobberVel;
    private bool bobberLanded, fishBiting;
    private float biteAt, biteUntil;
    private float bobberRestY = float.MinValue;

    private void CastOrReel(SbObject rod)
    {
      if (bobber != null)
      {
        // reel in: a fish if one was biting
        if (fishBiting)
        {
          SbItem cod = SbCatalog.Get("mob_cod");
          Transform hand = HandProvider != null ? HandProvider(rod.HeldRight) : null;
          if (cod != null && hand != null && MineCount < MaxMine)
          {
            SbObject f = Create(0, nextId++, cod, bobber.transform.position, Quaternion.identity, 1f, false, 0, Vector3Int.zero);
            if (f != null)
            {
              if (f.Rb != null) f.Rb.linearVelocity = (hand.position - bobber.transform.position) * 1.6f + Vector3.up * 3f;
              SbNet.QueueSpawn(f);
              undo.Add(f.Id);
            }
          }
          SbFx.PlayMc("fishing_retrieve", SbSound.Splash, bobber.transform.position, 1f, 1f, 16f);
          Toast?.Invoke("Fishing", "You caught a cod!");
        }
        Destroy(bobber);
        bobber = null;
        if (fishLine != null) fishLine.enabled = false;
        return;
      }
      Vector3 tip = rod.T.TransformPoint(rod.Item.Muzzle);
      bobber = new GameObject("SB bobber");
      bobber.transform.SetParent(SbFx.Root, false);
      bobber.transform.position = tip;
      var k = new SbMeshKit();
      k.Ball(new Vector3(0f, 0.02f, 0f), 0.03f, new Color(0.9f, 0.15f, 0.15f), 10);
      k.Ball(new Vector3(0f, -0.01f, 0f), 0.03f, Color.white, 10);
      bobber.AddComponent<MeshFilter>().sharedMesh = k.ToMesh("SB bobber");
      bobber.AddComponent<MeshRenderer>().sharedMaterials = new[] { SbMats.Lit, SbMats.Glow };
      bobberVel = rod.T.forward * 9f + Vector3.up * 2f;
      bobberRod = rod;
      bobberLanded = false; fishBiting = false;
      if (fishLine == null)
      {
        var g = new GameObject("SB fishing line");
        g.transform.SetParent(SbFx.Root, false);
        fishLine = g.AddComponent<LineRenderer>();
        fishLine.useWorldSpace = true;
        fishLine.positionCount = 2;
        fishLine.startWidth = fishLine.endWidth = 0.004f;
        fishLine.sharedMaterial = new Material(SbMats.Line) { hideFlags = HideFlags.DontSave, color = new Color(0.9f, 0.9f, 0.9f, 0.9f) };
      }
      fishLine.enabled = true;
      SbFx.PlayMc("fishing_throw", SbSound.Swing, tip, 0.8f, 1f, 16f);
    }

    private void UpdateFishing(float dt)
    {
      if (bobber == null) return;
      if (bobberRod == null || bobberRod.T == null || !bobberRod.Held) { Destroy(bobber); bobber = null; if (fishLine != null) fishLine.enabled = false; return; }
      Vector3 p = bobber.transform.position;
      bool water = WaterAt(p);
      if (!bobberLanded)
      {
        bobberVel += Physics.gravity * dt;
        Vector3 next = p + bobberVel * dt;
        RaycastHit hit;
        if (water) { bobberRestY = float.MinValue; bobberLanded = true; biteAt = Time.time + UnityEngine.Random.Range(3f, 10f); SbFx.Play(SbSound.Splash, p, 0.5f, 1.4f); }
        else if (Physics.Linecast(p, next, out hit, ~0, QueryTriggerInteraction.Ignore)) { bobberLanded = true; next = hit.point + hit.normal * 0.03f; biteAt = float.MaxValue; }
        bobber.transform.position = next;
      }
      else if (water)
      {
        // bob on the water; a bite pulls it under for a moment
        float now = Time.time;
        if (!fishBiting && now >= biteAt) { fishBiting = true; biteUntil = now + 1.2f; SbFx.Play(SbSound.Splash, p, 0.8f, 1f); Haptic?.Invoke(bobberRod.HeldRight, 0.6f, 0.15f); }
        if (fishBiting && now > biteUntil) { fishBiting = false; biteAt = now + UnityEngine.Random.Range(4f, 10f); }
        if (bobberRestY == float.MinValue) bobberRestY = p.y;
        bobber.transform.position = new Vector3(p.x, bobberRestY + (fishBiting ? -0.12f : Mathf.Sin(now * 3f) * 0.02f), p.z);
      }
      if (fishLine != null)
      {
        fishLine.SetPosition(0, bobberRod.T.TransformPoint(bobberRod.Item.Muzzle));
        fishLine.SetPosition(1, bobber.transform.position);
      }
    }

    // ---------------------------------------------------------------- potions
    private void Drink(SbObject potion, bool right)
    {
      string kind = potion.Item.Id.Substring("mc_potion_".Length);
      float until = Time.time + 45f;
      bool moves = kind != "night";
      if (moves && !PearlAllowed) { Toast?.Invoke("Potion", "Speed, jumping and slow falling only work in a SandBox lobby where everyone has the SandBox"); return; }
      switch (kind)
      {
        case "speed": speedUntil = until; break;
        case "jump": jumpUntil = until; break;
        case "night": nightUntil = until; break;
        case "slowfall": slowFallUntil = until; break;
      }
      SbFx.PlayMc("drink", SbSound.Bubble, potion.T.position, 1f, 1f, 12f);
      SbFx.Burst(potion.T.position, 20, potion.Item.Id == "mc_potion_night" ? new Color(0.2f, 0.2f, 0.8f) : Color.white, Color.white, 0.2f, 0.8f, 0.02f, 0.05f, 0.4f, 0.8f, -0.1f, true, 0.1f);
      Toast?.Invoke("Potion", potion.Item.Name + ": 45 seconds");
      Release(right, false);
      Remove(potion, true, false);
    }

    private void UpdatePotions(float dt)
    {
      float now = Time.time;
      try
      {
        GTPlayer p = GTPlayer.Instance;
        if (p != null)
        {
          bool fast = now < speedUntil && PearlAllowed, jump = now < jumpUntil && PearlAllowed;
          // the game's own values while no potion works (other things may change them)
          if (!potionApplied) { baseMaxJump = p.maxJumpSpeed; baseJumpMult = p.jumpMultiplier; }
          if (fast || jump || potionApplied)
          {
            p.maxJumpSpeed = baseMaxJump * (fast ? 1.45f : 1f);
            p.jumpMultiplier = baseJumpMult * (jump ? 1.5f : fast ? 1.15f : 1f);
            potionApplied = fast || jump;
          }
        }
      }
      catch { }
      UpdatePotionHud();
      // night vision: the whole scene lit
      bool night = now < nightUntil;
      if (night && !nightOn)
      {
        nightOn = true;
        nightSavedAmbient = RenderSettings.ambientLight;
        nightSavedIntensity = RenderSettings.ambientIntensity;
        RenderSettings.ambientLight = new Color(0.85f, 0.85f, 0.9f);
        RenderSettings.ambientIntensity = Mathf.Max(1.4f, nightSavedIntensity);
      }
      else if (!night && nightOn)
      {
        nightOn = false;
        RenderSettings.ambientLight = nightSavedAmbient;
        RenderSettings.ambientIntensity = nightSavedIntensity;
      }
    }

    // FixedUpdate: slow falling and the elytra
    private void ApplyMcMovement()
    {
      ApplyFireworkBoost();
      ApplyGrapple();
      if (!PearlAllowed) return;
      try
      {
        GTPlayer p = GTPlayer.Instance;
        Rigidbody rb = p != null ? p.playerRigidBody : null;
        if (rb == null) return;
        Vector3 v = rb.linearVelocity;
        if (Time.time < slowFallUntil && v.y < -1.2f) rb.linearVelocity = new Vector3(v.x, Mathf.MoveTowards(v.y, -1.2f, 30f * Time.fixedDeltaTime), v.z);
        if (elytraOn && v.y < -2f)
        {
          Transform lh = HandProvider != null ? HandProvider(false) : null, rh = HandProvider != null ? HandProvider(true) : null;
          Transform head = HeadProvider != null ? HeadProvider() : null;
          if (lh != null && rh != null && head != null && (lh.position - rh.position).magnitude > 1.1f * p.scale)
          {
            // gliding: the fall turns into speed along where you look
            Vector3 look = head.forward;
            Vector3 flat = Vector3.ProjectOnPlane(look, Vector3.up).normalized;
            float speed = Mathf.Clamp(v.magnitude, 6f, 30f);
            Vector3 want = (flat + Vector3.up * Mathf.Clamp(look.y, -0.9f, 0.3f)).normalized * speed;
            want.y = Mathf.Max(want.y, -4f);
            rb.linearVelocity = Vector3.Lerp(v, want, 2.5f * Time.fixedDeltaTime);
          }
        }
      }
      catch { }
    }

    // ---------------------------------------------------------------- armour
    private void Wear(SbObject item, bool right, SbArmorSlot slot)
    {
      int s = (int)slot;
      string id = item.Item.Id;
      bool off = armorOn[s] == id;
      armorOn[s] = off ? null : id;
      if (slot == SbArmorSlot.Chest) elytraOn = !off && id == "mc_elytra";
      ShowMyArmor();
      SbNet.SendArmor(s, off ? (ushort)0 : item.Item.Key);
      SbFx.PlayMc(id.Contains("leather") ? "armor_leather" : id == "mc_elytra" ? "armor_elytra" : "armor_iron", SbSound.Place, item.T.position, 0.9f, 1f, 12f);
      Toast?.Invoke("Armour", off ? "Took off the " + item.Item.Name : "Wearing the " + item.Item.Name);
      if (!off) { Release(right, false); Remove(item, true, false); }
    }

    // how much less knockback your armour gives (0 .. 0.5)
    internal float ArmorProtection
    {
      get
      {
        float p = 0f;
        foreach (string a in armorOn)
        {
          if (a == null || a == "mc_elytra") continue;
          p += a.Contains("netherite") ? 0.25f : a.Contains("diamond") ? 0.22f : a.Contains("iron") ? 0.17f : a.Contains("gold") ? 0.12f : 0.08f;
        }
        return Mathf.Min(0.5f, p);
      }
    }

    private void ShowMyArmor()
    {
      VRRig me = null;
      try { me = GorillaTagger.Instance != null ? GorillaTagger.Instance.offlineVRRig : null; } catch { }
      for (int s = 0; s < 2; s++)
      {
        if (armorVis[s] != null) Destroy(armorVis[s]);
        armorVis[s] = armorOn[s] != null && me != null ? MakeArmor(me, s, SbCatalog.Get(armorOn[s])) : null;
      }
    }

    private static GameObject MakeArmor(VRRig rig, int slot, SbItem item)
    {
      if (rig == null || item == null) return null;
      Transform bone = slot == 0 ? (rig.headMesh != null ? rig.headMesh.transform : null) : rig.transform;
      if (bone == null) return null;
      var g = new GameObject("SB armour " + item.Name);
      g.transform.SetParent(bone, false);
      // Minecraft's armour textures when the player has them (SandBoxMcModels.cs)
      Material worn = null;
      Mesh textured = null;
      try { textured = SbMcModels.WornArmor(item.Id, slot, out worn); } catch { textured = null; }
      if (textured != null && worn != null)
      {
        g.AddComponent<MeshFilter>().sharedMesh = textured;
        g.AddComponent<MeshRenderer>().sharedMaterial = worn;
      }
      else
      {
        var k = new SbMeshKit();
        if (item.Id == "mc_elytra") Elytra(k);
        else if (slot == 0) SbMcItems.Helmet(k, SbMcItems.ArmorColour(item.Id));
        else SbMcItems.Chestplate(k, SbMcItems.ArmorColour(item.Id));
        g.AddComponent<MeshFilter>().sharedMesh = k.ToMesh("SB armour");
        g.AddComponent<MeshRenderer>().sharedMaterials = new[] { SbMats.Lit, SbMats.Glow };
      }
      // in the rig's own space: the helmet on the head, the chestplate round the body
      float sc = 1f / Mathf.Max(0.05f, bone.lossyScale.x) * Mathf.Max(0.2f, rig.scaleFactor);
      g.transform.localScale = Vector3.one * sc;
      g.transform.localPosition = slot == 0 ? new Vector3(0f, 0.03f, 0f) * sc : new Vector3(0f, -0.08f, 0f) * sc;
      g.layer = bone.gameObject.layer;   // hidden in your own headset like your own head
      return g;
    }

    private static void Elytra(SbMeshKit k) { SbMcItems.Elytra(k); }

    internal void NetArmor(int actor, int slot, ushort key)
    {
      long k = ((long)actor << 8) | (uint)slot;
      GameObject old;
      if (remoteArmor.TryGetValue(k, out old) && old != null) Destroy(old);
      remoteArmor.Remove(k);
      SbItem it;
      if (key == 0 || !SbCatalog.ByKey.TryGetValue(key, out it)) return;
      GameObject g = MakeArmor(RigOf(actor), slot, it);
      if (g != null) remoteArmor[k] = g;
    }

    internal void ResendMyArmor()
    {
      for (int s = 0; s < 2; s++)
      {
        SbItem it = armorOn[s] != null ? SbCatalog.Get(armorOn[s]) : null;
        if (it != null) SbNet.SendArmor(s, it.Key);
      }
    }

    internal void ClearArmorOf(int actor)
    {
      for (int s = 0; s < 2; s++)
      {
        long k = ((long)actor << 8) | (uint)s;
        GameObject old;
        if (remoteArmor.TryGetValue(k, out old) && old != null) Destroy(old);
        remoteArmor.Remove(k);
      }
    }

    // ---------------------------------------------------------------- rails (for minecarts)
    // the rail under p: its centre and the way it runs (along X or Z)
    internal bool RailAt(Vector3 p, out Vector3 centre, out Vector3 axis, out bool powered, out float size)
    {
      centre = Vector3.zero; axis = Vector3.forward; powered = false; size = 1f;
      for (int s = 0; s < BlockSizes.Length; s++)
      {
        float bs = BlockSizes[s];
        foreach (float dy in new[] { 0f, -0.45f })
        {
          Vector3Int c = CellOf(p + Vector3.up * dy * bs, bs);
          SbObject r;
          if (!grid.TryGetValue(Pack(s, c.x, c.y, c.z), out r) || r == null || r.Item.Shape != SbShape.Rail || r.T == null) continue;
          centre = CellCenter(c, bs) + Vector3.down * bs * 0.45f;
          Vector3 f = r.T.forward;
          axis = Mathf.Abs(f.x) > Mathf.Abs(f.z) ? Vector3.right : Vector3.forward;
          powered = r.Item.Id == "mc_powered_rail";
          size = bs;
          return true;
        }
      }
      return false;
    }

    internal bool HasRail(Vector3 p, float size)
    {
      Vector3 c, a; bool pw; float s;
      return RailAt(p, out c, out a, out pw, out s);
    }

    // ================================================================ every frame
    private void UpdateMcItems(float dt)
    {
      try { UpdateFishing(dt); } catch (Exception ex) { SbLog.Throttled("SandBox fishing", ex); }
      try { UpdatePotions(dt); } catch (Exception ex) { SbLog.Throttled("SandBox potions", ex); }
      try { UpdateFlashOverlay(); } catch (Exception ex) { SbLog.Throttled("SandBox flashbang", ex); }
      if (Time.frameCount % 30 == 0) foreach (SbObject o in List) if (o.Item.Id == "ml_lightsaber" && o.T != null && o.T.Find("SB blade") == null) LightsaberBlade(o);
    }
  }

  // ==================================================================== boats and minecarts
  internal sealed partial class SbVehicle
  {
    private Vector3 cartAxis = Vector3.forward;
    private float cartSpeed;

    private void BoatPhysics()
    {
      Rigidbody rb = obj.Rb;
      bool driven = world.Driving == obj;
      Vector3 pos = transform.position;
      bool under = world.WaterAt(pos + Vector3.up * 0.15f), wet = under || world.WaterAt(pos + Vector3.down * 0.25f);
      if (wet)
      {
        // floats: pushed up while under the surface, gravity cancelled on it
        rb.AddForce(-Physics.gravity * rb.mass * (under ? 1.6f : 1f), ForceMode.Force);
        Vector3 v = rb.linearVelocity;
        rb.linearVelocity = new Vector3(v.x * 0.98f, v.y * 0.85f, v.z * 0.98f);
        if (driven)
        {
          Vector3 fwd = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
          rb.AddForce(fwd * throttle * rb.mass * 6f, ForceMode.Force);
          rb.angularVelocity = new Vector3(0f, steer * 1.6f, 0f);
        }
        else rb.angularVelocity *= 0.9f;
        // stays level
        Quaternion level = Quaternion.LookRotation(Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized + Vector3.forward * 1e-4f, Vector3.up);
        rb.MoveRotation(Quaternion.Slerp(rb.rotation, level, 1f - Mathf.Exp(-6f * Time.fixedDeltaTime)));
      }
    }

    private void CartPhysics()
    {
      Rigidbody rb = obj.Rb;
      bool driven = world.Driving == obj;
      Vector3 centre, axis; bool powered; float size;
      if (!world.RailAt(transform.position, out centre, out axis, out powered, out size))
      {
        rb.useGravity = true;
        cartSpeed = Vector3.Dot(rb.linearVelocity, cartAxis);
        return;
      }
      rb.useGravity = false;
      // a new rail direction: keep going the way it was heading (turning at corners)
      if (Mathf.Abs(Vector3.Dot(axis, cartAxis)) < 0.5f)
      {
        Vector3 heading = rb.linearVelocity.sqrMagnitude > 0.1f ? rb.linearVelocity : transform.forward;
        cartSpeed = Mathf.Abs(cartSpeed) * Mathf.Sign(Vector3.Dot(heading, axis) + 1e-4f);
        cartAxis = axis;
      }
      // the end of the line: is there a rail to the side? (a corner)
      Vector3 ahead = centre + cartAxis * Mathf.Sign(cartSpeed) * size + Vector3.up * size * 0.45f;
      if (Mathf.Abs(cartSpeed) > 0.1f && !world.HasRail(ahead, size))
      {
        Vector3 side = Vector3.Cross(Vector3.up, cartAxis);
        if (world.HasRail(centre + side * size + Vector3.up * size * 0.45f, size)) { cartAxis = side; cartSpeed = Mathf.Abs(cartSpeed); }
        else if (world.HasRail(centre - side * size + Vector3.up * size * 0.45f, size)) { cartAxis = -side; cartSpeed = Mathf.Abs(cartSpeed); }
        else cartSpeed = 0f;   // the end of the track
      }
      float dt = Time.fixedDeltaTime;
      if (driven) cartSpeed += throttle * 6f * dt;
      if (powered) cartSpeed = Mathf.MoveTowards(cartSpeed, (cartSpeed >= 0f ? 1f : -1f) * 14f, 18f * dt);
      else cartSpeed = Mathf.MoveTowards(cartSpeed, 0f, 0.6f * dt);
      cartSpeed = Mathf.Clamp(cartSpeed, -16f, 16f);
      // on the rail's line
      Vector3 p = transform.position;
      Vector3 along = Vector3.Project(p - centre, cartAxis);
      Vector3 snapped = centre + along + Vector3.up * 0.05f;
      rb.MovePosition(Vector3.Lerp(p, snapped, 0.5f) + cartAxis * cartSpeed * dt);
      rb.linearVelocity = cartAxis * cartSpeed;
      Vector3 face = cartAxis * (cartSpeed >= 0f ? 1f : -1f);
      rb.MoveRotation(Quaternion.Slerp(rb.rotation, Quaternion.LookRotation(face, Vector3.up), 1f - Mathf.Exp(-10f * dt)));
      rb.angularVelocity = Vector3.zero;
    }
  }
}
