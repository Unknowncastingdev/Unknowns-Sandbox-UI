using System;
using System.Collections.Generic;
using GorillaLocomotion;
using UnityEngine;

namespace UnknownsSandbox
{
  // melee weapons and throwables (Weapons tab).
  //  MELEE: knife, war hammer (huge knockback), lightsaber (trigger: blade on / off, hums),
  //    chainsaw (hold the trigger: it cuts whatever the blade touches, no swing needed),
  //    whip (long reach, cracks on a fast swing), grappling hook (trigger shoots the hook,
  //    hold to be pulled to it; where the ender pearl works).
  //  THROWABLES: smoke grenade (a thick cloud), flashbang (blinds whoever looks at it),
  //    throwing knives and a boomerang (trigger throws; the boomerang comes back).
  //  Grenades and sticky bombs are in BOMBS & NUKES.
  internal sealed partial class SandBoxWorld
  {
    // ---------------------------------------------------------------- held melee extras
    // true when the melee weapon may hit this frame (a lightsaber with its blade off can't)
    private bool MeleeExtras(SbObject o, bool right, bool trig, bool edge, ref float speed, ref Vector3 dir, Vector3 tip, Vector3 hilt)
    {
      string id = o.Item.Id;
      if (id == "ml_lightsaber")
      {
        if (edge) { o.RedOn = !o.RedOn; SbFx.Play(o.RedOn ? SbSound.Laser : SbSound.UiClose, tip, 0.8f, o.RedOn ? 0.5f : 0.8f); }
        bool on = !o.RedOn;   // starts lit
        Transform blade = LightsaberBlade(o);
        if (blade != null && blade.gameObject.activeSelf != on) blade.gameObject.SetActive(on);
        if (on && Time.time >= o.LastBeep) { o.LastBeep = Time.time + 0.5f; SbFx.Play(SbSound.Laser, tip, 0.12f * SbFx.Volume, 0.25f, 6f); }
        return on;
      }
      if (id == "ml_chainsaw")
      {
        if (trig)
        {
          if (Time.time >= o.LastBeep) { o.LastBeep = Time.time + 0.22f; SbFx.Play(SbSound.Minigun, tip, 0.5f, 0.55f, 18f); }
          if (Time.frameCount % 3 == 0) SbFx.Burst(tip, 2, new Color(0.6f, 0.6f, 0.6f, 0.6f), new Color(0.3f, 0.3f, 0.3f, 0.4f), 0.2f, 0.6f, 0.02f, 0.04f, 0.2f, 0.4f, -0.1f, false, 0.03f);
          speed = Mathf.Max(speed, 6f);
          if (dir.sqrMagnitude < 0.5f) dir = (tip - hilt).normalized;
          Haptic?.Invoke(right, 0.15f, 0.03f);
        }
        return true;
      }
      if (id == "ml_whip" && speed > 9f && Time.time >= o.LastBeep)
      {
        o.LastBeep = Time.time + 0.4f;
        SbFx.Play(SbSound.Pop, tip, 1f, 1.8f, 30f);
        SbFx.Burst(tip, 6, Color.white, new Color(0.9f, 0.85f, 0.7f), 0.3f, 1.2f, 0.01f, 0.03f, 0.1f, 0.2f, 0f, true, 0.01f);
      }
      return true;
    }

    private static Transform LightsaberBlade(SbObject o)
    {
      if (o.T == null) return null;
      Transform b = o.T.Find("SB blade");
      if (b != null) return b;
      var g = new GameObject("SB blade");
      g.transform.SetParent(o.T, false);
      var k = new SbMeshKit();
      k.Cyl(new Vector3(0f, 0f, 0.52f), 0.016f, 0.82f, Quaternion.Euler(90f, 0f, 0f), new Color(0.95f, 1f, 1f), 12, true);
      k.Cyl(new Vector3(0f, 0f, 0.52f), 0.026f, 0.84f, Quaternion.Euler(90f, 0f, 0f), new Color(0.2f, 0.6f, 1f), 12, true);
      g.AddComponent<MeshFilter>().sharedMesh = k.ToMesh("SB lightsaber blade");
      g.AddComponent<MeshRenderer>().sharedMaterials = new[] { SbMats.Lit, SbMats.Glow };
      var l = g.AddComponent<Light>();
      l.type = LightType.Point; l.color = new Color(0.3f, 0.6f, 1f); l.range = 2f; l.intensity = 1.2f; l.shadows = LightShadows.None;
      return g.transform;
    }

    // ---------------------------------------------------------------- grappling hook
    private Vector3 grappleAt;
    private bool grappleOn, grappleRight;
    private SbObject grappleItem;
    private LineRenderer grappleLine;

    private bool UseGrapple(SbObject hook, bool right, bool trig, bool edge)
    {
      Vector3 muzzle = hook.T.TransformPoint(hook.Item.Muzzle);
      if (edge)
      {
        Vector3 end, n; SbObject obj; VRRig rig;
        if (Cast(muzzle, muzzle + hook.T.forward * 35f, out end, out n, out obj, out rig) && rig == null)
        {
          grappleAt = end; grappleOn = true; grappleRight = right; grappleItem = hook;
          SbFx.Play(SbSound.Thunk, end, 0.8f, 1.4f, 30f);
          SbFx.Sparks(end, n, new Color(1f, 0.9f, 0.6f), 8);
          if (!PearlAllowed) Toast?.Invoke("Grappling Hook", "Pulls you only in a SandBox lobby where everyone has the SandBox");
        }
        else SbFx.Play(SbSound.Swing, muzzle, 0.6f, 1.4f);
      }
      if (!trig && grappleOn && grappleRight == right) grappleOn = false;
      if (grappleLine == null)
      {
        var g = new GameObject("SB grapple line");
        g.transform.SetParent(SbFx.Root, false);
        grappleLine = g.AddComponent<LineRenderer>();
        grappleLine.useWorldSpace = true;
        grappleLine.positionCount = 2;
        grappleLine.startWidth = grappleLine.endWidth = 0.012f;
        grappleLine.sharedMaterial = new Material(SbMats.Line) { hideFlags = HideFlags.DontSave, color = new Color(0.25f, 0.22f, 0.2f, 1f) };
      }
      grappleLine.enabled = grappleOn;
      if (grappleOn) { grappleLine.SetPosition(0, muzzle); grappleLine.SetPosition(1, grappleAt); }
      return true;
    }

    // FixedUpdate: pulled along the rope
    private void ApplyGrapple()
    {
      if (!grappleOn) return;
      if (grappleItem == null || grappleItem.T == null || !grappleItem.Held) { grappleOn = false; if (grappleLine != null) grappleLine.enabled = false; return; }
      if (!PearlAllowed) return;
      try
      {
        GTPlayer p = GTPlayer.Instance;
        Rigidbody rb = p != null ? p.playerRigidBody : null;
        if (rb == null) return;
        Vector3 body = p.bodyCollider.transform.position;
        Vector3 to = grappleAt - body;
        if (to.magnitude < 1f) { rb.linearVelocity *= 0.8f; return; }
        rb.linearVelocity = Vector3.Lerp(rb.linearVelocity, to.normalized * 15f, 4f * Time.fixedDeltaTime);
      }
      catch { }
    }

    // ---------------------------------------------------------------- throwing knives and the boomerang
    private bool UseThrown(SbObject o, bool right, bool edge)
    {
      if (!edge) return true;
      SbItem proj = SbCatalog.Get(o.Item.Id == "th_boomerang" ? "proj_boomerang" : "proj_knife");
      if (proj == null) return true;
      if (o.Item.Id == "th_boomerang" && boomerangOut != null && projectiles.Contains(boomerangOut)) return true;   // one at a time
      Vector3 from = o.T.TransformPoint(o.Item.Muzzle);
      // thrown where you swing (the hand's speed) or straight ahead
      Vector3 hv = HandVelocity(right);
      Vector3 dir = hv.sqrMagnitude > 4f ? (hv.normalized + o.T.forward).normalized : o.T.forward;
      int seed = UnityEngine.Random.Range(1, int.MaxValue);
      Shoot(proj, from, dir, seed, true, 1f);
      SbNet.SendShot(proj.Key, from, dir, seed, 1f);
      if (o.Item.Id == "th_boomerang" && projectiles.Count > 0) { boomerangOut = projectiles[projectiles.Count - 1]; boomerangRight = right; }
      SbFx.Play(SbSound.Swing, from, 0.8f, o.Item.Id == "th_boomerang" ? 0.8f : 1.4f);
      Haptic?.Invoke(right, 0.3f, 0.05f);
      return true;
    }

    private SbProj boomerangOut;
    private bool boomerangRight;

    // the boomerang out and back: true when it is caught or gone
    private bool BoomerangHome(SbProj p, float dt)
    {
      p.Grav = 0f;
      bool back = p.Bounces >= 90 || p.Life < 1.6f;
      if (!back) { p.Vel = Quaternion.AngleAxis(70f * dt, Vector3.up) * p.Vel; return false; }
      Vector3 home = p.Home;
      if (p.Local)
      {
        Transform hand = HandProvider != null ? HandProvider(boomerangRight) : null;
        if (hand != null) home = hand.position;
      }
      Vector3 to = home - p.Pos;
      if (to.magnitude < 0.45f)
      {
        if (p.Local) { SbFx.Play(SbSound.UiClick, home, 0.7f, 1.2f); Haptic?.Invoke(boomerangRight, 0.4f, 0.05f); }
        if (boomerangOut == p) boomerangOut = null;
        return true;
      }
      p.Vel = Vector3.MoveTowards(p.Vel, to.normalized * 14f, 40f * dt);
      if (p.Life <= 0.05f) p.Life = 0.05f;   // it always comes back
      return false;
    }

    // ---------------------------------------------------------------- smoke and flash
    private void SmokeCloud(Vector3 at, float radius)
    {
      SbFx.Play(SbSound.Suck, at, 0.9f, 0.6f, 40f);
      for (int i = 0; i < 6; i++)
      {
        ParticleSystem ps = SbFx.Burst(at + Vector3.up * 0.3f, 60, new Color(0.82f, 0.82f, 0.84f, 0.85f), new Color(0.6f, 0.6f, 0.62f, 0.7f), 0.4f, 1.6f, 0.6f, 1.4f, 8f, 12f, -0.01f, false, radius * 0.5f, null, 0f, 0.8f, 2.2f, true);
        if (ps != null) { var main = ps.main; main.startDelay = i * 0.4f; }
      }
    }

    private float flashUntil, flashStart;
    private GameObject flashQuad;

    private void Flashbang(Vector3 at, float radius)
    {
      SbFx.Flash(at, Color.white, 8f, radius * 3f, 0.4f);
      SbFx.Play(SbSound.Pop, at, 1f, 0.5f, 60f);
      SbFx.Burst(at, 40, Color.white, new Color(1f, 1f, 0.8f), 3f, 8f, 0.03f, 0.08f, 0.2f, 0.4f, 0f, true, 0.1f);
      // blinded when you are near and it was in front of you (and not behind a wall)
      Transform head = HeadProvider != null ? HeadProvider() : null;
      if (head == null) return;
      Vector3 to = at - head.position;
      float d = to.magnitude;
      if (d > radius * 4f) return;
      RaycastHit wh;
      if (RaycastWorld(new Ray(head.position, to / Mathf.Max(0.01f, d)), d - 0.2f, out wh)) return;
      float facing = Vector3.Dot(head.forward, to / Mathf.Max(0.01f, d));
      float strength = Mathf.Clamp01(1f - d / (radius * 4f)) * Mathf.Clamp01(facing * 0.8f + 0.4f);
      if (strength < 0.15f) return;
      flashStart = Time.time;
      flashUntil = Time.time + 1f + 3f * strength;
    }

    private void UpdateFlashOverlay()
    {
      Camera cam = Camera.main;
      float left = flashUntil - Time.time;
      if (left <= 0f || cam == null) { if (flashQuad != null && flashQuad.activeSelf) flashQuad.SetActive(false); return; }
      if (flashQuad == null)
      {
        flashQuad = GameObject.CreatePrimitive(PrimitiveType.Quad);
        flashQuad.name = "SB flashbang";
        Collider c = flashQuad.GetComponent<Collider>(); if (c != null) Destroy(c);
        Renderer r = flashQuad.GetComponent<Renderer>();
        r.sharedMaterial = new Material(SbMats.Line) { hideFlags = HideFlags.DontSave, renderQueue = 4001 };
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
      }
      if (flashQuad.transform.parent != cam.transform) flashQuad.transform.SetParent(cam.transform, false);
      if (!flashQuad.activeSelf) flashQuad.SetActive(true);
      flashQuad.transform.localPosition = new Vector3(0f, 0f, Mathf.Max(0.07f, cam.nearClipPlane * 1.6f));
      flashQuad.transform.localRotation = Quaternion.identity;
      flashQuad.transform.localScale = Vector3.one * 1.5f;
      float total = Mathf.Max(0.1f, flashUntil - flashStart);
      float a = Mathf.Clamp01(left / Mathf.Min(1.2f, total));
      flashQuad.GetComponent<Renderer>().sharedMaterial.color = new Color(1f, 1f, 1f, a);
    }
  }

  // ==================================================================== the items
  internal static class SbMeleeItems
  {
    private static Color C(string h) { return SbCatalog.C(h); }

    private static SbItem Melee(string id, string name, string desc, int tier, Action<SbMeshKit> build)
    {
      return new SbItem { Id = id, Name = name, Desc = desc, Tab = SbTab.Weapons, Section = "MELEE", Kind = SbKind.Melee, MeleeTier = tier, DefaultAnchored = true, Holdable = true, Mass = 1.5f, Build = build };
    }

    internal static void AddToCatalog(Action<SbItem> add)
    {
      Color steel = C("#c8ccd2"), dark = C("#1e2024"), wood = C("#6a4a2a"), grip = C("#2a2a2e");
      add(Melee("ml_knife", "Combat Knife", "Quick and light. Swing it.", 2, k =>
      {
        k.Box(new Vector3(0f, 0f, 0.13f), new Vector3(0.006f, 0.035f, 0.17f), steel);
        k.Wedge(new Vector3(0f, 0f, 0.23f), new Vector3(0.006f, 0.035f, 0.04f), Quaternion.identity, steel);
        k.Box(new Vector3(0f, 0f, 0.035f), new Vector3(0.02f, 0.05f, 0.012f), dark);
        k.Box(new Vector3(0f, 0f, -0.03f), new Vector3(0.022f, 0.028f, 0.11f), grip);
        k.GripAt(Vector3.zero).MuzzleAt(new Vector3(0f, 0f, 0.25f));
      }));
      add(Melee("ml_hammer", "War Hammer", "Heavy: sends things (and Unknowns players) flying.", 7, k =>
      {
        k.Cyl(new Vector3(0f, 0f, 0.25f), 0.016f, 0.62f, Quaternion.Euler(90f, 0f, 0f), wood, 10);
        k.Box(new Vector3(0f, 0f, 0.56f), new Vector3(0.24f, 0.11f, 0.11f), C("#5a5e66"));
        k.Box(new Vector3(0.13f, 0f, 0.56f), new Vector3(0.03f, 0.13f, 0.13f), C("#3a3e46"));
        k.Box(new Vector3(-0.13f, 0f, 0.56f), new Vector3(0.03f, 0.13f, 0.13f), C("#3a3e46"));
        k.GripAt(Vector3.zero).MuzzleAt(new Vector3(0f, 0f, 0.6f));
      }));
      add(Melee("ml_lightsaber", "Lightsaber", "Trigger: the blade on or off. It hums.", 5, k =>
      {
        k.Cyl(new Vector3(0f, 0f, 0.02f), 0.02f, 0.22f, Quaternion.Euler(90f, 0f, 0f), C("#b8bcc4"), 16);
        for (int i = 0; i < 4; i++) k.Torus(new Vector3(0f, 0f, -0.06f + i * 0.03f), 0.021f, 0.004f, Quaternion.Euler(90f, 0f, 0f), dark, 16, 4);
        k.Cyl(new Vector3(0f, 0f, 0.12f), 0.024f, 0.02f, Quaternion.Euler(90f, 0f, 0f), dark, 16);
        k.Box(new Vector3(0.02f, 0f, 0.05f), new Vector3(0.008f, 0.01f, 0.02f), C("#e03030"), true);
        k.GripAt(Vector3.zero).MuzzleAt(new Vector3(0f, 0f, 0.92f));
      }));
      add(Melee("ml_chainsaw", "Chainsaw", "Hold the trigger: it cuts whatever the blade touches.", 3, k =>
      {
        k.Box(new Vector3(0f, 0.02f, -0.02f), new Vector3(0.1f, 0.12f, 0.2f), C("#e86a1a"));
        k.Box(new Vector3(0f, 0.09f, -0.06f), new Vector3(0.03f, 0.04f, 0.14f), grip);
        k.Box(new Vector3(0f, 0.01f, 0.25f), new Vector3(0.012f, 0.06f, 0.36f), C("#9aa0a8"));
        for (int i = 0; i < 12; i++) k.Box(new Vector3(0f, 0.045f, 0.09f + i * 0.03f), new Vector3(0.016f, 0.008f, 0.012f), dark);
        k.Cyl(new Vector3(0f, 0.01f, 0.43f), 0.03f, 0.012f, Quaternion.Euler(0f, 0f, 90f), C("#9aa0a8"), 14);
        k.GripAt(Vector3.zero).MuzzleAt(new Vector3(0f, 0.01f, 0.44f));
      }));
      add(Melee("ml_whip", "Whip", "Long reach. A fast swing cracks.", 2, k =>
      {
        k.Cyl(new Vector3(0f, 0f, 0f), 0.016f, 0.18f, Quaternion.Euler(90f, 0f, 0f), C("#4a2e1a"), 10);
        for (int i = 0; i < 16; i++)
        {
          float t = i / 15f;
          k.Ball(new Vector3(0f, -Mathf.Sin(t * 2.5f) * 0.12f * t, 0.12f + t * 1.5f), Mathf.Lerp(0.012f, 0.005f, t), C("#6a4024"), 6);
        }
        k.GripAt(Vector3.zero).MuzzleAt(new Vector3(0f, -0.07f, 1.62f));
      }));
      add(new SbItem
      {
        Id = "ml_grapple", Name = "Grappling Hook", Desc = "Trigger shoots the hook into what you point at; hold it to be pulled there (where the ender pearl works).", Tab = SbTab.Weapons, Section = "MELEE",
        Kind = SbKind.Tool, Holdable = true, DefaultAnchored = true, Mass = 1.5f,
        Build = k =>
        {
          k.Box(new Vector3(0f, 0.03f, 0.05f), new Vector3(0.05f, 0.06f, 0.2f), C("#3a3e46"));
          k.Box(new Vector3(0f, -0.02f, -0.02f), new Vector3(0.035f, 0.1f, 0.045f), Quaternion.Euler(-12f, 0f, 0f), grip);
          k.Cyl(new Vector3(0f, 0.03f, 0.17f), 0.02f, 0.06f, Quaternion.Euler(90f, 0f, 0f), steel, 10);
          for (int i = 0; i < 3; i++) k.Box(new Vector3(0f, 0.03f, 0.21f) + Quaternion.Euler(0f, 0f, i * 120f) * new Vector3(0f, 0.025f, 0f), new Vector3(0.008f, 0.03f, 0.01f), Quaternion.Euler(0f, 0f, i * 120f) * Quaternion.Euler(-40f, 0f, 0f), steel);
          k.GripAt(Vector3.zero).MuzzleAt(new Vector3(0f, 0.03f, 0.23f));
        }
      });
      // ---- throwables
      add(new SbItem
      {
        Id = "th_smoke", Name = "Smoke Grenade", Desc = "Trigger pulls the pin, throw it: a thick cloud of smoke for a while.", Tab = SbTab.Weapons, Section = "THROWABLES",
        Kind = SbKind.Explosive, Blast = SbBlast.Smoke, Radius = 3f, Fuse = 2f, Holdable = true, DefaultAnchored = false, Mass = 1f,
        Build = k =>
        {
          k.Cyl(Vector3.zero, 0.035f, 0.12f, Quaternion.identity, C("#5a6a4a"), 14);
          k.Cyl(new Vector3(0f, 0.07f, 0f), 0.02f, 0.02f, Quaternion.identity, C("#2a2a2a"), 10);
          k.Torus(new Vector3(0.03f, 0.08f, 0f), 0.012f, 0.003f, Quaternion.identity, steel, 10, 4);
          k.Box(new Vector3(0f, 0.01f, 0.036f), new Vector3(0.05f, 0.03f, 0.002f), C("#e8e8e8"));
        }
      });
      add(new SbItem
      {
        Id = "th_flashbang", Name = "Flashbang", Desc = "Trigger pulls the pin, throw it: anyone looking at it is blinded for a moment.", Tab = SbTab.Weapons, Section = "THROWABLES",
        Kind = SbKind.Explosive, Blast = SbBlast.Flash, Radius = 4f, Fuse = 1.8f, Holdable = true, DefaultAnchored = false, Mass = 1f,
        Build = k =>
        {
          k.Cyl(Vector3.zero, 0.032f, 0.12f, Quaternion.identity, C("#2e3238"), 14);
          for (int i = 0; i < 3; i++) k.Torus(new Vector3(0f, -0.04f + i * 0.04f, 0f), 0.033f, 0.004f, Quaternion.identity, C("#c8a030"), 14, 4);
          k.Cyl(new Vector3(0f, 0.07f, 0f), 0.018f, 0.02f, Quaternion.identity, C("#5a5a5a"), 10);
          k.Torus(new Vector3(0.03f, 0.08f, 0f), 0.012f, 0.003f, Quaternion.identity, steel, 10, 4);
        }
      });
      add(new SbItem
      {
        Id = "th_knives", Name = "Throwing Knives", Desc = "Trigger throws a knife (more where it came from). They stick where they land.", Tab = SbTab.Weapons, Section = "THROWABLES",
        Kind = SbKind.Tool, Holdable = true, DefaultAnchored = true, Mass = 0.5f,
        Build = k =>
        {
          k.Box(new Vector3(0f, 0f, 0.06f), new Vector3(0.005f, 0.03f, 0.13f), steel);
          k.Box(new Vector3(0f, 0f, -0.04f), new Vector3(0.012f, 0.022f, 0.07f), dark);
          k.GripAt(Vector3.zero).MuzzleAt(new Vector3(0f, 0f, 0.13f));
        }
      });
      add(new SbItem
      {
        Id = "th_boomerang", Name = "Boomerang", Desc = "Trigger throws it: it hits what it meets and comes back to your hand.", Tab = SbTab.Weapons, Section = "THROWABLES",
        Kind = SbKind.Tool, Holdable = true, DefaultAnchored = true, Mass = 0.5f,
        Build = k =>
        {
          k.Box(new Vector3(0.07f, 0f, 0.03f), new Vector3(0.16f, 0.012f, 0.04f), Quaternion.Euler(0f, 30f, 0f), C("#b06a2a"));
          k.Box(new Vector3(-0.07f, 0f, 0.03f), new Vector3(0.16f, 0.012f, 0.04f), Quaternion.Euler(0f, -30f, 0f), C("#b06a2a"));
          k.GripAt(new Vector3(0.12f, 0f, 0f)).MuzzleAt(new Vector3(0f, 0f, 0.1f));
        }
      });
      // the projectiles they throw (hidden; sent like any shot so everyone sees them)
      add(new SbItem { Id = "proj_knife", Name = "proj_knife", Desc = "", Tab = SbTab.Weapons, Section = "GUNS", Kind = SbKind.Gun, Hidden = true, Ammo = SbAmmo.Knife, Speed = 18f, Spread = 0f, Pellets = 1, Build = k => k.Box(Vector3.zero, Vector3.one * 0.05f, Color.white) });
      add(new SbItem { Id = "proj_boomerang", Name = "proj_boomerang", Desc = "", Tab = SbTab.Weapons, Section = "GUNS", Kind = SbKind.Gun, Hidden = true, Ammo = SbAmmo.Boomerang, Speed = 13f, Spread = 0f, Pellets = 1, Build = k => k.Box(Vector3.zero, Vector3.one * 0.05f, Color.white) });
    }
  }
}
