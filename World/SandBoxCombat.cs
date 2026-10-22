using System;
using System.Collections.Generic;
using UnityEngine;

namespace UnknownsSandbox
{
  // Holding things (grip), guns, swords, thrown bombs and projectiles.
  internal sealed partial class SandBoxWorld
  {

    private SbObject heldL, heldR;
    private bool gripWasL, gripWasR, trigWasL, trigWasR;
    private float nextFireL, nextFireR;
    private Vector3 lastTipL, lastTipR;
    private float swingSoundL, swingSoundR;
    private float recoilL, recoilR;
    private readonly Dictionary<long, float> hitCooldown = new Dictionary<long, float>();
    private readonly Vector3[] histL = new Vector3[8], histR = new Vector3[8];
    private readonly float[] histTL = new float[8], histTR = new float[8];
    private int histI;
    private readonly List<int> myDucks = new List<int>();

    internal bool IsHolding(bool right) { return right ? heldR != null : heldL != null; }
    internal SbObject HeldIn(bool right) { return right ? heldR : heldL; }

    internal static float GripValue(bool right)
    {
      try { var p = ControllerInputPoller.instance; return p == null ? 0f : (right ? p.rightControllerGripFloat : p.leftControllerGripFloat); } catch { return 0f; }
    }

    internal static float TriggerValue(bool right)
    {
      try { var p = ControllerInputPoller.instance; return p == null ? 0f : (right ? p.rightControllerIndexFloat : p.leftControllerIndexFloat); } catch { return 0f; }
    }

    private void UpdateHands(float dt)
    {
      histI = (histI + 1) % 8;
      for (int side = 0; side < 2; side++)
      {
        bool right = side == 1;
        Transform hand = HandProvider != null ? HandProvider(right) : null;
        if (hand == null) continue;
        (right ? histR : histL)[histI] = hand.position;
        (right ? histTR : histTL)[histI] = Time.time;

        bool grip = GripValue(right) > 0.6f;
        bool gripWas = right ? gripWasR : gripWasL;
        SbObject held = right ? heldR : heldL;
        if (held != null && (held.T == null || !objects.ContainsKey(held.Key))) { held = null; if (right) heldR = null; else heldL = null; }

        if (held == null && grip && !gripWas && Driving == null && !TryEnterVehicle(hand))
          TryGrab(right, hand);
        else if (held != null && !grip)
          Release(right, true);

        held = right ? heldR : heldL;
        if (held != null)
        {
          Follow(held, hand, right);
          UseHeld(held, right, dt);
        }
        else
        {
          if (right) trigWasR = TriggerValue(true) > 0.6f; else trigWasL = TriggerValue(false) > 0.6f;
        }
        if (right) gripWasR = grip; else gripWasL = grip;
        if (right) recoilR = Mathf.MoveTowards(recoilR, 0f, dt * 0.4f); else recoilL = Mathf.MoveTowards(recoilL, 0f, dt * 0.4f);
      }
      ProcessPendingBlasts();
    }

    private void TryGrab(bool right, Transform hand)
    {
      SbObject best = null;
      float bestD = 0.10f;
      foreach (SbObject o in List)
      {
        if (!o.Mine || o.IsCell || o.T == null || o.Held || o.Parent != null || o.Item.Vehicle != null) continue;   // stuck-on things move with their object; cars are driven
        Vector3 lp = o.T.InverseTransformPoint(hand.position);
        Bounds b = o.LocalBounds;
        Vector3 closest = b.ClosestPoint(lp);
        float d = Vector3.Distance(o.T.TransformPoint(closest), hand.position);
        if (o.Item.IsHeldType) d -= 0.04f;
        if (d < bestD) { bestD = d; best = o; }
      }
      if (best != null) Grab(best, right);
    }

    internal void Grab(SbObject o, bool right)
    {
      if (o == null || o.T == null) return;
      if (heldL == o) heldL = null;
      if (heldR == o) heldR = null;
      Transform hand = HandProvider != null ? HandProvider(right) : null;
      if (hand == null) return;
      o.Held = true; o.HeldRight = right; o.Stuck = false;
      if (o.Rb != null) { o.Rb.isKinematic = true; }
      if (o.Item.Kind == SbKind.Gun || o.Item.Kind == SbKind.Melee || o.Item.Id == "mc_bow" || o.Item.Id == "mc_fishing_rod" || o.Item.Id == "mc_shield")   // aimed like a gun
      {
        Vector3 dir = PointerDirProvider != null ? PointerDirProvider(right) : hand.forward;
        if (dir.sqrMagnitude < 1e-4f) dir = hand.forward;
        Vector3 up = Vector3.ProjectOnPlane(Vector3.up, dir);
        if (up.sqrMagnitude < 1e-4f) up = hand.up;
        Quaternion target = Quaternion.LookRotation(dir.normalized, up.normalized);
        if (o.Item.Kind == SbKind.Melee) target = target * Quaternion.Euler(-15f, 0f, 0f);
        o.HoldLocalRot = Quaternion.Inverse(hand.rotation) * target;
        o.HoldLocalPos = Quaternion.Inverse(hand.rotation) * (-(target * (o.Item.Grip * o.Scale)));
      }
      else
      {
        o.HoldLocalRot = Quaternion.Inverse(hand.rotation) * o.T.rotation;
        Vector3 off = o.T.position - hand.position;
        if (off.magnitude > 0.25f) off = off.normalized * 0.25f;
        o.HoldLocalPos = Quaternion.Inverse(hand.rotation) * off;
      }
      if (right) heldR = o; else heldL = o;
      if (right) trigWasR = TriggerValue(true) > 0.6f; else trigWasL = TriggerValue(false) > 0.6f;
      ApplySolid(o);
      Follow(o, hand, right);
      SendHoldState(o, right, true);
      Haptic?.Invoke(right, 0.35f, 0.05f);
      SbFx.Play(SbSound.UiClick, hand.position, 0.4f, 0.7f);
      if (o.Item.Kind == SbKind.Melee) { Vector3 rel = o.T.TransformPoint(o.Item.Muzzle) - RigOrigin(); if (right) lastTipR = rel; else lastTipL = rel; }
    }

    private void SendHoldState(SbObject o, bool right, bool holding)
    {
      try
      {
        VRRig me = GorillaTagger.Instance != null ? GorillaTagger.Instance.offlineVRRig : null;
        Transform bone = me != null ? (right ? me.rightHandTransform : me.leftHandTransform) : null;
        if (bone == null) bone = HandProvider != null ? HandProvider(right) : null;
        if (bone == null) return;
        SbNet.SendHold(o.Id, right, holding, bone.InverseTransformPoint(o.T.position), Quaternion.Inverse(bone.rotation) * o.T.rotation);
      }
      catch { }
    }

    private void Follow(SbObject o, Transform hand, bool right)
    {
      Quaternion rot = hand.rotation * o.HoldLocalRot;
      float recoil = right ? recoilR : recoilL;
      o.T.rotation = rot * Quaternion.Euler(-recoil * 120f, 0f, 0f);
      o.T.position = hand.position + hand.rotation * o.HoldLocalPos - rot * Vector3.forward * recoil;
    }

    private Vector3 HandVelocity(bool right)
    {
      Vector3[] h = right ? histR : histL;
      float[] ht = right ? histTR : histTL;
      int now = histI, then = (histI + 8 - 4) % 8;
      float dt = ht[now] - ht[then];
      if (dt < 0.005f) return Vector3.zero;
      return (h[now] - h[then]) / dt;
    }

    internal void Release(bool right, bool throwIt)
    {
      SbObject o = right ? heldR : heldL;
      if (right) heldR = null; else heldL = null;
      if (o == null || o.T == null) return;
      o.Held = false;
      SendHoldState(o, right, false);
      // anchored things stay where you let go (move them around); bombs and loose things are thrown
      bool keepFloating = o.Anchored && o.Item.Kind != SbKind.Explosive && !o.Item.Pearl;
      if (o.Item.Pearl && throwIt) o.PearlThrownAt = Time.time;   // an ender pearl flies until it lands (SandBoxPearl.cs)
      if (!keepFloating)
      {
        Vector3 v = throwIt ? HandVelocity(right) * 1.25f : Vector3.zero;
        MakeDynamic(o, v);
        if (o.Rb != null) o.Rb.angularVelocity = UnityEngine.Random.insideUnitSphere * Mathf.Min(8f, v.magnitude);
      }
      ApplySolid(o);
      o.SentPos = Vector3.one * 99999f;
      SbNet.QueueMove(o);
      Haptic?.Invoke(right, 0.2f, 0.03f);
    }

    private void UseHeld(SbObject o, bool right, float dt)
    {
      bool trig = TriggerValue(right) > 0.6f;
      bool was = right ? trigWasR : trigWasL;
      bool edge = trig && !was;
      if (right) trigWasR = trig; else trigWasL = trig;
      SbItem it = o.Item;
      switch (it.Kind)
      {
        case SbKind.Gun:
          {
            if (it.Ammo == SbAmmo.Gravity) { UpdateGravityGun(o, right, trig, edge); break; }
            float next = right ? nextFireR : nextFireL;
            if ((it.Auto ? trig : edge) && Time.time >= next)
            {
              if (right) nextFireR = Time.time + 1f / Mathf.Max(0.2f, it.FireRate);
              else nextFireL = Time.time + 1f / Mathf.Max(0.2f, it.FireRate);
              Fire(o, right);
            }
            break;
          }
        case SbKind.Explosive:
          if (edge && !o.Primed)
          {
            Prime(o, it.Fuse, true);
            SbFx.Play(SbSound.UiClick, o.T.position, 0.7f, 0.6f);
            Haptic?.Invoke(right, 0.4f, 0.06f);
          }
          break;
        case SbKind.Melee:
          Swing(o, right, dt, trig, edge);
          break;
        case SbKind.Tool:
          if (UseMcItem(o, right, edge)) break;   // bow, rod, potions, armour (SandBoxItems.cs)
          if (it.Pearl) { if (edge) ThrowPearl(o, right); }
          else UseHeldTool(o, right, edge);
          break;
      }
    }

    // ================================================================ guns
    private void Fire(SbObject o, bool right)
    {
      if (FireGadget(o, right)) return;
      SbItem it = o.Item;
      Vector3 muzzle = o.T.TransformPoint(it.Muzzle);
      Vector3 dir = o.T.forward;
      int seed = UnityEngine.Random.Range(1, int.MaxValue);
      Shoot(it, muzzle, dir, seed, true, o.Scale);
      SbNet.SendShot(it.Key, muzzle, dir, seed, o.Scale);
      float kick = it.Ammo == SbAmmo.Sniper || it.Ammo == SbAmmo.Rocket || it.Ammo == SbAmmo.Melon ? 0.06f :
                   it.Ammo == SbAmmo.Pellet || it.Ammo == SbAmmo.Potato ? 0.045f : it.Ammo == SbAmmo.Minigun ? 0.008f : 0.02f;
      if (right) recoilR = Mathf.Min(0.08f, recoilR + kick); else recoilL = Mathf.Min(0.08f, recoilL + kick);
      Haptic?.Invoke(right, Mathf.Clamp01(kick * 12f + 0.15f), it.Ammo == SbAmmo.Minigun ? 0.03f : 0.07f);
    }

    internal void NetShot(int actor, ushort key, Vector3 origin, Vector3 dir, int seed, float scale)
    {
      SbItem it;
      if (!SbCatalog.ByKey.TryGetValue(key, out it)) return;
      Shoot(it, origin, dir, seed, false, scale);
    }

    private sealed class SbProj
    {
      public SbAmmo Ammo;
      public Vector3 Pos, Vel;
      public float Life, Grav, Drag, Fuse = -1f;
      public int Bounces;
      public bool Local;
      public GameObject Vis;
      public Color Col = Color.white;
      public float Spin;
      public float SmokeT;
      public bool FromMob;   // a mob's arrow or fireball: it can hit you
      public Vector3 Home;     // where a boomerang comes back to
    }
    private readonly List<SbProj> projectiles = new List<SbProj>();
    private readonly Dictionary<SbAmmo, Mesh> ammoMeshes = new Dictionary<SbAmmo, Mesh>();

    private static SbSound ShotSound(SbAmmo a)
    {
      switch (a)
      {
        case SbAmmo.Pellet: return SbSound.Shotgun;
        case SbAmmo.Sniper: return SbSound.Sniper;
        case SbAmmo.Minigun: return SbSound.Minigun;
        case SbAmmo.Laser: return SbSound.Laser;
        case SbAmmo.Rocket: return SbSound.Rocket;
        case SbAmmo.Flame: return SbSound.Flame;
        case SbAmmo.Confetti: return SbSound.Confetti;
        case SbAmmo.Bubble: return SbSound.Bubble;
        case SbAmmo.Grenade:
        case SbAmmo.Melon:
        case SbAmmo.Potato:
        case SbAmmo.Banana:
        case SbAmmo.Snowball:
        case SbAmmo.Duck:
        case SbAmmo.Paint: return SbSound.Thunk;
        default: return SbSound.Shot;
      }
    }

    private void Shoot(SbItem it, Vector3 origin, Vector3 dir, int seed, bool local, float scale)
    {
      var rnd = new System.Random(seed);
      SbAmmo a = it.Ammo;
      if (IsGadget(a))
      {
        // another player's gadget shot (portals come by their own message)
        if (!local && a != SbAmmo.RickPortal && a != SbAmmo.AperturePortal)
        {
          Vector3 ge, gn; SbObject go; VRRig gr;
          float reach = a == SbAmmo.Build ? 12f : a == SbAmmo.Gravity ? 15f : 80f;
          bool gh = Cast(origin, origin + dir * reach, out ge, out gn, out go, out gr);
          GadgetShotFx(a, origin, gh ? ge : origin + dir * reach, gn);
        }
        return;
      }
      SbFx.Play(ShotSound(a), origin, a == SbAmmo.Minigun ? 0.55f : 0.85f, a == SbAmmo.Paint ? 1.6f : a == SbAmmo.Snowball ? 1.4f : 1f);
      bool bulletish = a == SbAmmo.Bullet || a == SbAmmo.Pellet || a == SbAmmo.Sniper || a == SbAmmo.Minigun;
      if (bulletish) SbFx.Burst(origin, 6, new Color(1f, 0.85f, 0.4f), new Color(1f, 0.6f, 0.2f), 0.5f, 2f, 0.04f, 0.09f, 0.04f, 0.08f, 0f, true, 0.005f, dir, 20f);

      if (a == SbAmmo.Laser)
      {
        Vector3 end; Vector3 n; SbObject obj; VRRig rig;
        bool hit = Cast(origin, origin + dir * 60f, out end, out n, out obj, out rig);
        if (!hit) end = origin + dir * 60f;
        Beam(origin, end, new Color(0.3f, 0.95f, 1f));
        if (hit)
        {
          SbFx.Sparks(end, n, new Color(0.4f, 0.95f, 1f), 16);
          if (rig != null) Blood(end, dir);
          if (local) ApplyImpactHits(obj, rig, end, dir, 2.5f, true);
        }
        return;
      }
      if (a == SbAmmo.Flame)
      {
        SbFx.Burst(origin, 10, new Color(1f, 0.85f, 0.35f), new Color(1f, 0.3f, 0.05f), 6f, 9f, 0.06f, 0.16f, 0.35f, 0.55f, -0.3f, true, 0.02f, dir, 9f, 0f, 2.2f);
        if (local) ConeHits(origin, dir, 4.5f, 20f, 0.6f, true);
        return;
      }
      if (a == SbAmmo.Confetti)
      {
        for (int i = 0; i < 4; i++)
        {
          Color c = Color.HSVToRGB((float)rnd.NextDouble(), 0.8f, 1f);
          SbFx.Burst(origin, 40, c, Color.HSVToRGB((float)rnd.NextDouble(), 0.7f, 1f), 3f, 7f, 0.02f, 0.04f, 1.5f, 2.5f, 0.35f, false, 0.03f, dir, 25f, 2.5f, 1f);
        }
        if (local) ConeHits(origin, dir, 3f, 30f, 2f, false);
        return;
      }

      int count = Mathf.Max(1, it.Pellets);
      for (int i = 0; i < count; i++)
      {
        float yaw = ((float)rnd.NextDouble() - 0.5f) * 2f * it.Spread;
        float pitch = ((float)rnd.NextDouble() - 0.5f) * 2f * it.Spread;
        Vector3 d = Quaternion.AngleAxis(yaw, Vector3.up) * Quaternion.AngleAxis(pitch, Vector3.Cross(dir, Vector3.up).sqrMagnitude > 1e-4f ? Vector3.Cross(dir, Vector3.up).normalized : Vector3.right) * dir;
        var p = new SbProj { Ammo = a, Pos = origin, Vel = d * it.Speed * shotSpeed * (0.9f + (float)rnd.NextDouble() * 0.2f), Local = local, Life = 3f };
        switch (a)
        {
          case SbAmmo.Grenade: p.Grav = 1f; p.Fuse = 1.5f; break;
          case SbAmmo.Melon: case SbAmmo.Potato: case SbAmmo.Snowball: case SbAmmo.Duck: p.Grav = 1f; break;
          case SbAmmo.Banana: p.Grav = 0.6f; p.Spin = 720f; break;
          case SbAmmo.Paint: p.Grav = 0.4f; p.Col = Color.HSVToRGB((float)rnd.NextDouble(), 0.85f, 1f); break;
          case SbAmmo.Bubble: p.Grav = -0.03f; p.Drag = 1.2f; p.Life = 4f + (float)rnd.NextDouble() * 2f; p.Vel += new Vector3((float)rnd.NextDouble() - 0.5f, (float)rnd.NextDouble() * 0.5f, (float)rnd.NextDouble() - 0.5f); break;
          case SbAmmo.Rocket: p.Life = 5f; break;
          // arrows (bows, skeletons) and fireballs (blazes, ghasts, the Wither, the dragon)
          case SbAmmo.Arrow: p.Grav = 1f; p.Life = 4f; break;
          case SbAmmo.Fireball: case SbAmmo.WitherSkull: p.Life = 4f; break;
          case SbAmmo.GhastBall: case SbAmmo.DragonBall: p.Life = 7f; break;
          case SbAmmo.Knife: p.Grav = 1f; p.Life = 4f; p.Spin = -900f; break;
          case SbAmmo.Boomerang: p.Life = 3f; p.Spin = 1440f; p.Home = origin; break;
        }
        p.FromMob = mobShooting;
        p.Vis = MakeProjectileVisual(p, (float)rnd.NextDouble(), scale);
        projectiles.Add(p);
      }
    }

    private GameObject MakeProjectileVisual(SbProj p, float r01, float scale)
    {
      Mesh mesh;
      if (!ammoMeshes.TryGetValue(p.Ammo, out mesh))
      {
        var k = new SbMeshKit();
        switch (p.Ammo)
        {
          case SbAmmo.Rocket:
            k.Tube(Vector3.zero, 0.035f, 0.3f, new Color(0.35f, 0.4f, 0.3f));
            k.Cone(new Vector3(0f, 0f, 0.2f), 0.035f, 0.1f, Quaternion.Euler(90f, 0f, 0f), new Color(0.75f, 0.75f, 0.75f));
            k.Ball(new Vector3(0f, 0f, -0.16f), 0.03f, new Color(1f, 0.7f, 0.2f), 8, true);
            break;
          case SbAmmo.Grenade: k.Sphere(Vector3.zero, new Vector3(0.035f, 0.035f, 0.05f), new Color(0.3f, 0.36f, 0.2f), 10); break;
          case SbAmmo.Melon:
            k.Sphere(Vector3.zero, new Vector3(0.14f, 0.13f, 0.17f), new Color(0.25f, 0.55f, 0.2f), 14);
            for (int i = 0; i < 6; i++) k.Sphere(Vector3.zero, new Vector3(0.142f, 0.132f, 0.172f), Quaternion.Euler(0f, 0f, i * 30f), new Color(0.15f, 0.35f, 0.12f), 4);
            break;
          case SbAmmo.Potato: k.Sphere(Vector3.zero, new Vector3(0.04f, 0.035f, 0.055f), new Color(0.69f, 0.54f, 0.3f), 9); break;
          case SbAmmo.Banana:
            for (int i = 0; i < 6; i++) { float t = (i - 2.5f) / 2.5f; k.Sphere(new Vector3(0f, t * t * 0.05f, t * 0.08f), Vector3.one * 0.025f, new Color(1f, 0.88f, 0.3f), 6); }
            break;
          case SbAmmo.Snowball: k.Ball(Vector3.zero, 0.05f, Color.white, 10); break;
          case SbAmmo.Duck:
            {
              SbItem duck = SbCatalog.Get("ob_duck");
              mesh = duck != null ? duck.Mesh : null;
              break;
            }
          case SbAmmo.Bubble: k.Ball(Vector3.zero, 0.5f, new Color(0.8f, 0.95f, 1f), 12, true); break;
          case SbAmmo.Paint: k.Ball(Vector3.zero, 0.02f, Color.white, 8, true); break;
          case SbAmmo.Arrow:
            k.Box(Vector3.zero, new Vector3(0.012f, 0.012f, 0.5f), new Color(0.55f, 0.42f, 0.26f));
            k.Cone(new Vector3(0f, 0f, 0.27f), 0.02f, 0.05f, Quaternion.Euler(90f, 0f, 0f), new Color(0.6f, 0.6f, 0.62f), 4);
            k.Box(new Vector3(0f, 0f, -0.22f), new Vector3(0.002f, 0.05f, 0.08f), new Color(0.9f, 0.9f, 0.9f));
            k.Box(new Vector3(0f, 0f, -0.22f), new Vector3(0.05f, 0.002f, 0.08f), new Color(0.9f, 0.9f, 0.9f));
            break;
          case SbAmmo.Fireball: k.Ball(Vector3.zero, 0.09f, new Color(1f, 0.55f, 0.1f), 10, true); break;
          case SbAmmo.GhastBall: k.Ball(Vector3.zero, 0.22f, new Color(1f, 0.45f, 0.05f), 12, true); break;
          case SbAmmo.WitherSkull: k.Box(Vector3.zero, Vector3.one * 0.2f, new Color(0.12f, 0.12f, 0.14f)); k.Box(new Vector3(0f, 0.03f, 0.101f), new Vector3(0.12f, 0.03f, 0.002f), new Color(0.8f, 0.85f, 1f), true); break;
          case SbAmmo.DragonBall: k.Ball(Vector3.zero, 0.3f, new Color(0.75f, 0.2f, 0.95f), 12, true); break;
          case SbAmmo.Knife:
            k.Box(new Vector3(0f, 0f, 0.05f), new Vector3(0.006f, 0.03f, 0.14f), new Color(0.82f, 0.84f, 0.88f));
            k.Box(new Vector3(0f, 0f, -0.06f), new Vector3(0.014f, 0.024f, 0.08f), new Color(0.15f, 0.15f, 0.17f));
            break;
          case SbAmmo.Boomerang:
            k.Box(new Vector3(0.07f, 0f, 0f), new Vector3(0.16f, 0.012f, 0.04f), Quaternion.Euler(0f, 30f, 0f), new Color(0.75f, 0.45f, 0.2f));
            k.Box(new Vector3(-0.07f, 0f, 0f), new Vector3(0.16f, 0.012f, 0.04f), Quaternion.Euler(0f, -30f, 0f), new Color(0.75f, 0.45f, 0.2f));
            break;
          default: k.Sphere(Vector3.zero, new Vector3(0.008f, 0.008f, 0.07f), new Color(1f, 0.85f, 0.4f), 6, true); break;
        }
        if (mesh == null) mesh = k.ToMesh("SB ammo " + p.Ammo);
        ammoMeshes[p.Ammo] = mesh;
      }
      var go = new GameObject("SB Shot");
      go.transform.SetParent(SbFx.Root, false);
      go.transform.position = p.Pos;
      go.AddComponent<MeshFilter>().sharedMesh = mesh;
      var mr = go.AddComponent<MeshRenderer>();
      mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
      if (p.Ammo == SbAmmo.Bubble || p.Ammo == SbAmmo.Paint)
      {
        var m = new Material(SbMats.Line) { hideFlags = HideFlags.DontSave };
        m.color = p.Ammo == SbAmmo.Bubble ? new Color(0.75f, 0.95f, 1f, 0.35f) : p.Col;
        mr.sharedMaterial = m;
      }
      else mr.sharedMaterials = new[] { SbMats.Lit, SbMats.Glow };
      float s = 1f;
      if (p.Ammo == SbAmmo.Bubble) s = 0.08f + r01 * 0.1f;
      else if (p.Ammo == SbAmmo.Duck) s = 0.6f;
      go.transform.localScale = Vector3.one * s * Mathf.Clamp(scale, 0.5f, 2.5f);
      // Minecraft's fireballs are the fire charge picture (SandBoxMcModels.cs)
      if (p.Ammo == SbAmmo.Fireball || p.Ammo == SbAmmo.GhastBall) SbMcFx.Fireball(go, p.Ammo == SbAmmo.GhastBall ? 0.75f : 0.3f);
      bool fire = p.Ammo == SbAmmo.Fireball || p.Ammo == SbAmmo.GhastBall || p.Ammo == SbAmmo.DragonBall || p.Ammo == SbAmmo.WitherSkull;
      bool tracer = p.Ammo == SbAmmo.Bullet || p.Ammo == SbAmmo.Pellet || p.Ammo == SbAmmo.Sniper || p.Ammo == SbAmmo.Minigun || p.Ammo == SbAmmo.Rocket || fire;
      if (tracer)
      {
        var tr = go.AddComponent<TrailRenderer>();
        tr.time = p.Ammo == SbAmmo.Rocket || fire ? 0.35f : 0.05f;
        tr.startWidth = p.Ammo == SbAmmo.Rocket ? 0.06f : fire ? (p.Ammo == SbAmmo.Fireball ? 0.12f : 0.3f) : 0.012f;
        tr.endWidth = 0f;
        tr.sharedMaterial = SbMats.Line;
        tr.startColor = p.Ammo == SbAmmo.DragonBall ? new Color(0.8f, 0.3f, 1f, 0.8f) : p.Ammo == SbAmmo.WitherSkull ? new Color(0.3f, 0.3f, 0.35f, 0.7f) : p.Ammo == SbAmmo.Rocket || fire ? new Color(1f, 0.6f, 0.2f, 0.8f) : new Color(1f, 0.9f, 0.5f, 0.9f);
        tr.endColor = new Color(1f, 1f, 1f, 0f);
        tr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
      }
      return go;
    }

    private void Beam(Vector3 a, Vector3 b, Color c)
    {
      var go = new GameObject("SB Beam");
      go.transform.SetParent(SbFx.Root, false);
      var lr = go.AddComponent<LineRenderer>();
      lr.useWorldSpace = true;
      lr.positionCount = 2;
      lr.SetPosition(0, a); lr.SetPosition(1, b);
      lr.startWidth = 0.025f; lr.endWidth = 0.015f;
      var m = new Material(SbMats.Line) { hideFlags = HideFlags.DontSave, color = c };
      lr.sharedMaterial = m;
      lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
      var f = go.AddComponent<SbFade>();
      f.mat = m; f.life = 0.15f; f.baseColor = c;
    }

    // segment cast against the world, every object and remote players
    private bool Cast(Vector3 a, Vector3 b, out Vector3 point, out Vector3 normal, out SbObject obj, out VRRig rig)
    {
      point = b; normal = Vector3.up; obj = null; rig = null;
      Vector3 d = b - a;
      float len = d.magnitude;
      if (len < 1e-5f) return false;
      var ray = new Ray(a, d / len);
      float best = len;
      bool hit = false;
      RaycastHit wh;
      if (RaycastWorld(ray, len, out wh) && wh.distance < best) { best = wh.distance; point = wh.point; normal = wh.normal; hit = true; }
      float od; Vector3 on;
      SbObject o = RaycastObjects(ray, best, out od, out on, false, false);
      if (o != null && od < best) { best = od; point = a + ray.direction * od; normal = on; obj = o; hit = true; }
      foreach (VRRig r in Rigs())
      {
        if (r == null || r.isOfflineVRRig) continue;
        float sc = 1f;
        try { sc = r.scaleFactor; } catch { }
        Vector3 head = r.headMesh != null ? r.headMesh.transform.position : r.transform.position;
        Vector3 body = r.transform.position;
        float t;
        if (RaySphere(ray, head, 0.2f * sc, out t) && t < best) { best = t; point = a + ray.direction * t; normal = -ray.direction; rig = r; obj = null; hit = true; }
        if (RaySphere(ray, body, 0.28f * sc, out t) && t < best) { best = t; point = a + ray.direction * t; normal = -ray.direction; rig = r; obj = null; hit = true; }
      }
      return hit;
    }

    private static bool RaySphere(Ray r, Vector3 c, float rad, out float t)
    {
      t = 0f;
      Vector3 oc = r.origin - c;
      float b = Vector3.Dot(oc, r.direction);
      float cc = oc.sqrMagnitude - rad * rad;
      float disc = b * b - cc;
      if (disc < 0f) return false;
      float sq = Mathf.Sqrt(disc);
      t = -b - sq;
      if (t < 0f) t = -b + sq;
      return t >= 0f;
    }

    // like Minecraft, a fireball flies through the mobs that shoot fireballs (a blaze
    // was hitting itself with its own shot, which starts inside its body)
    private static bool FireproofAgainst(SbProj p, SbObject obj)
    {
      if (obj == null || obj.Mob == null) return false;
      if (p.Ammo != SbAmmo.Fireball && p.Ammo != SbAmmo.GhastBall && p.Ammo != SbAmmo.WitherSkull && p.Ammo != SbAmmo.DragonBall) return false;
      SbMobType t = obj.Item.Mob;
      return t == SbMobType.Blaze || t == SbMobType.Ghast || t == SbMobType.Wither || t == SbMobType.EnderDragon || t == SbMobType.MagmaCube;
    }

    private void UpdateProjectiles(float dt)
    {
      for (int i = projectiles.Count - 1; i >= 0; i--)
      {
        SbProj p = projectiles[i];
        p.Life -= dt;
        if (p.Ammo == SbAmmo.Boomerang && BoomerangHome(p, dt)) { if (p.Vis != null) Destroy(p.Vis); projectiles.RemoveAt(i); continue; }
        p.Vel += Physics.gravity * p.Grav * dt;
        if (p.Drag > 0f) p.Vel *= Mathf.Exp(-p.Drag * dt);
        Vector3 next = p.Pos + p.Vel * dt;
        Vector3 point, normal; SbObject obj; VRRig rig;
        bool kill = false;
        // a mob's shot can hit you (your own shots never do)
        Vector3 meAt;
        if (p.FromMob && SegmentHitsMe(p.Pos, next, out meAt))
        {
          MobShotHitsMe(p, meAt);
          kill = true;
        }
        else if (Cast(p.Pos, next, out point, out normal, out obj, out rig) && !FireproofAgainst(p, obj))
        {
          if (p.Ammo == SbAmmo.Boomerang)
          {
            // a boomerang hits what it meets and turns back
            if (p.Local && p.Bounces < 90) ApplyImpactHits(obj, rig, point, p.Vel.normalized, AmmoPower(p.Ammo), false);
            SbFx.Play(SbSound.Thunk, point, 0.5f, 1.3f);
            p.Bounces = 99;
            p.Pos = point + normal * 0.05f;
            p.Vel = Vector3.Reflect(p.Vel, normal) * 0.6f;
          }
          else if (p.Ammo == SbAmmo.Grenade && p.Bounces < 4 && rig == null)
          {
            p.Bounces++;
            p.Vel = Vector3.Reflect(p.Vel, normal) * 0.45f;
            p.Pos = point + normal * 0.02f;
            SbFx.Play(SbSound.Thunk, point, 0.4f, 1.6f);
          }
          else
          {
            Impact(p, point, normal, obj, rig);
            kill = true;
          }
        }
        else p.Pos = next;

        if (!kill && p.Fuse > 0f)
        {
          p.Fuse -= dt;
          if (p.Fuse <= 0f) { if (p.Local) Explode(p.Pos, 2.2f, SbBlast.Normal, true); kill = true; }
        }
        if (!kill && p.Life <= 0f)
        {
          if (p.Ammo == SbAmmo.Bubble) { SbFx.Burst(p.Pos, 6, new Color(0.8f, 0.95f, 1f), Color.white, 0.3f, 1f, 0.01f, 0.03f, 0.2f, 0.4f, 0f, true); SbFx.Play(SbSound.Bubble, p.Pos, 0.4f); }
          else if (p.Ammo == SbAmmo.Rocket && p.Local) Explode(p.Pos, 2.2f, SbBlast.Normal, true);
          kill = true;
        }
        if (kill)
        {
          if (p.Vis != null)
          {
            var mr = p.Vis.GetComponent<MeshRenderer>();
            if (mr != null && (p.Ammo == SbAmmo.Bubble || p.Ammo == SbAmmo.Paint) && mr.sharedMaterial != null) Destroy(mr.sharedMaterial);
            Destroy(p.Vis);
          }
          projectiles.RemoveAt(i);
          continue;
        }
        if (p.Vis != null)
        {
          p.Vis.transform.position = p.Pos;
          if (p.Vel.sqrMagnitude > 0.01f) p.Vis.transform.rotation = Quaternion.LookRotation(p.Vel);
          if (p.Spin != 0f) p.Vis.transform.rotation *= Quaternion.Euler(0f, 0f, Time.time * p.Spin);
        }
        if (p.Ammo == SbAmmo.Rocket)
        {
          p.SmokeT -= dt;
          if (p.SmokeT <= 0f)
          {
            p.SmokeT = 0.03f;
            SbFx.Burst(p.Pos, 2, new Color(0.8f, 0.8f, 0.8f, 0.6f), new Color(0.6f, 0.6f, 0.6f, 0.5f), 0.1f, 0.4f, 0.08f, 0.16f, 0.6f, 1.1f, -0.05f, false, 0.02f, null, 0f, 0f, 2f, true);
          }
        }
      }
    }

    private static float AmmoPower(SbAmmo a)
    {
      switch (a)
      {
        case SbAmmo.Bullet: return 2f;
        case SbAmmo.Pellet: return 1.4f;
        case SbAmmo.Minigun: return 1.3f;
        case SbAmmo.Sniper: return 14f;
        case SbAmmo.Melon: return 9f;
        case SbAmmo.Potato: return 6f;
        case SbAmmo.Banana: return 4f;
        case SbAmmo.Snowball: return 2.5f;
        case SbAmmo.Paint: return 0.8f;
        case SbAmmo.Duck: return 3f;
        case SbAmmo.Arrow: return 4f;
        case SbAmmo.Fireball: return 3f;
        case SbAmmo.Knife: return 6f;
        case SbAmmo.Boomerang: return 4f;
        default: return 0f;
      }
    }

    private void Impact(SbProj p, Vector3 point, Vector3 normal, SbObject obj, VRRig rig)
    {
      Vector3 dir = p.Vel.sqrMagnitude > 1e-4f ? p.Vel.normalized : -normal;
      // players bleed, bullets leave holes (SandBoxEffects.cs)
      if (rig != null) Blood(point, dir);
      else if (Bulletish(p.Ammo)) BulletHole(point, normal, obj != null && obj.T != null && !obj.IsCell ? obj.T : null);
      switch (p.Ammo)
      {
        // arrows stick in, fireballs burn, ghast / wither / dragon fireballs blow up
        case SbAmmo.Arrow:
          SbFx.Play(SbSound.Thunk, point, 0.6f, 1.5f);
          if (rig == null && obj == null) StuckArrow(point, dir);
          break;
        case SbAmmo.Knife:
          SbFx.Play(SbSound.Thunk, point, 0.7f, 1.7f);
          if (rig == null && obj == null) StuckArrow(point, dir, SbAmmo.Knife);
          break;
        case SbAmmo.Fireball:
          SbFx.Burst(point, 20, new Color(1f, 0.75f, 0.2f), new Color(1f, 0.3f, 0.05f), 1f, 3f, 0.04f, 0.1f, 0.3f, 0.6f, -0.2f, true, 0.1f, normal, 60f);
          SbFx.Play(SbSound.Flame, point, 0.8f, 1.2f);
          if (p.Local && obj == null && rig == null) StartFireAt(point + normal * 0.05f);
          break;
        case SbAmmo.GhastBall: if (p.Local) Explode(point + normal * 0.1f, 2.4f, SbBlast.Mc, true); return;
        case SbAmmo.WitherSkull: if (p.Local) Explode(point + normal * 0.1f, 1.8f, SbBlast.Mc, true); return;
        case SbAmmo.DragonBall:
          SbFx.Burst(point, 60, new Color(0.8f, 0.3f, 1f), new Color(0.4f, 0.1f, 0.6f), 0.5f, 2.5f, 0.1f, 0.3f, 1.5f, 3f, -0.05f, false, 1.2f, Vector3.up, 80f, 0.5f, 1.5f, true);
          SbFx.Play(SbSound.Suck, point, 1f, 0.7f);
          if (p.Local) KnockLocalPlayer(point, 3f, 1f);
          return;
        case SbAmmo.Bullet:
        case SbAmmo.Pellet:
        case SbAmmo.Minigun:
          SbFx.Sparks(point, normal, new Color(1f, 0.85f, 0.5f), 8);
          SbFx.Burst(point, 4, new Color(0.6f, 0.58f, 0.55f, 0.6f), new Color(0.4f, 0.4f, 0.4f, 0.5f), 0.2f, 0.6f, 0.04f, 0.09f, 0.3f, 0.6f, -0.05f, false, 0.01f, normal, 30f, 0f, 1.6f, true);
          break;
        case SbAmmo.Sniper:
          SbFx.Sparks(point, normal, new Color(1f, 0.9f, 0.6f), 26);
          SbFx.Play(SbSound.Hit, point, 0.8f, 1.4f);
          break;
        case SbAmmo.Rocket:
          if (p.Local) Explode(point + normal * 0.08f, 2.4f, SbBlast.Normal, true);
          break;
        case SbAmmo.Melon:
          SbFx.Splat(point, normal, new Color(0.95f, 0.2f, 0.25f), new Color(0.3f, 0.6f, 0.2f), 1.6f);
          SbFx.Debris(point, new Color(0.25f, 0.55f, 0.2f), 0.4f);
          SbFx.Play(SbSound.Splat, point, 1f, 0.9f);
          if (p.Local) SbFxDecal(point, normal, new Color(0.9f, 0.15f, 0.2f, 0.9f), 0.45f);
          break;
        case SbAmmo.Potato:
          SbFx.Burst(point, 12, new Color(0.75f, 0.6f, 0.35f), new Color(0.95f, 0.9f, 0.7f), 1f, 3f, 0.02f, 0.05f, 0.4f, 0.8f, 1.2f, false, 0.02f, normal, 60f);
          SbFx.Play(SbSound.Pop, point, 1f, 0.8f);
          break;
        case SbAmmo.Banana:
          SbFx.Splat(point, normal, new Color(1f, 0.9f, 0.35f), new Color(1f, 0.97f, 0.8f), 0.8f);
          SbFx.Play(SbSound.Splat, point, 0.7f, 1.4f);
          break;
        case SbAmmo.Snowball:
          SbFx.Burst(point, 22, Color.white, new Color(0.85f, 0.92f, 1f), 0.6f, 2.2f, 0.03f, 0.07f, 0.4f, 0.8f, 0.8f, false, 0.03f, normal, 70f);
          SbFx.Play(SbSound.Puff, point, 0.8f, 1.2f);
          break;
        case SbAmmo.Paint:
          SbFx.Splat(point, normal, p.Col, p.Col * 0.8f, 0.6f);
          SbFxDecal(point, normal, p.Col, 0.22f);
          SbFx.Play(SbSound.Splat, point, 0.45f, 1.8f);
          break;
        case SbAmmo.Bubble:
          SbFx.Burst(point, 6, new Color(0.8f, 0.95f, 1f), Color.white, 0.3f, 1f, 0.01f, 0.03f, 0.2f, 0.4f, 0f, true);
          SbFx.Play(SbSound.Bubble, point, 0.5f);
          return;
        case SbAmmo.Duck:
          SbFx.Play(SbSound.Squeak, point, 0.9f);
          if (p.Local)
          {
            SbItem duck = SbCatalog.Get("ob_duck");
            if (duck != null)
            {
              SbObject d = Create(0, nextId++, duck, point + normal * 0.08f, Quaternion.LookRotation(Vector3.ProjectOnPlane(-dir, Vector3.up).sqrMagnitude > 1e-4f ? Vector3.ProjectOnPlane(-dir, Vector3.up) : Vector3.forward), 0.6f, false, 0, Vector3Int.zero);
              if (d != null)
              {
                if (d.Rb != null) d.Rb.linearVelocity = Vector3.Reflect(p.Vel, normal) * 0.2f;
                SbNet.QueueSpawn(d);
                undo.Add(d.Id);
                myDucks.Add(d.Id);
                while (myDucks.Count > 25)
                {
                  SbObject old;
                  if (objects.TryGetValue(myDucks[0], out old)) Remove(old, true, false);
                  myDucks.RemoveAt(0);
                }
              }
            }
          }
          break;
        case SbAmmo.Grenade:
          if (p.Local) Explode(point + normal * 0.05f, 2.2f, SbBlast.Normal, true);
          return;
      }
      if (!p.Local) return;
      bool ignite = p.Ammo == SbAmmo.Bullet || p.Ammo == SbAmmo.Pellet || p.Ammo == SbAmmo.Sniper || p.Ammo == SbAmmo.Minigun;
      ApplyImpactHits(obj, rig, point, dir, AmmoPower(p.Ammo), ignite);
    }

    private static void SbFxDecal(Vector3 p, Vector3 n, Color c, float s) { SbFx.PaintDecal(p, n, c, s); }

    private void ApplyImpactHits(SbObject obj, VRRig rig, Vector3 point, Vector3 dir, float power, bool ignite)
    {
      if (obj != null)
      {
        if (obj.Mine || obj.Generated) ApplyHitToMine(obj, dir * power, point, ignite);
        else if (obj.Owner > 0) SbNet.SendHitObject(obj.Owner, obj.Id, dir * power, point, ignite);
      }
      if (rig != null && power > 0.5f)
      {
        int actor = ActorOf(rig);
        if (actor > 0) SbNet.SendHitPlayer(actor, (dir + Vector3.up * 0.3f).normalized * Mathf.Min(power * 0.9f, 12f));
      }
    }

    private void ConeHits(Vector3 origin, Vector3 dir, float range, float angle, float push, bool ignite)
    {
      foreach (SbObject o in new List<SbObject>(List))
      {
        if (o.Held || o.HeldRemote) continue;
        Vector3 to = o.Center - origin;
        float d = to.magnitude;
        if (d > range || d < 0.01f) continue;
        if (Vector3.Angle(dir, to) > angle) continue;
        if (o.Mine || o.Generated) ApplyHitToMine(o, to / d * push, o.Center, ignite);
        else if (o.Owner > 0 && (ignite || push > 0.5f)) SbNet.SendHitObject(o.Owner, o.Id, to / d * push, o.Center, ignite);
      }
    }

    // ================================================================ swords
    // where your body is (it moves with you: walking, swinging, flying, teleports)
    private static Vector3 RigOrigin()
    {
      try
      {
        var p = GorillaLocomotion.GTPlayer.Instance;
        if (p != null && p.bodyCollider != null) return p.bodyCollider.transform.position;
      }
      catch { }
      return Vector3.zero;
    }

    private void Swing(SbObject o, bool right, float dt, bool trig = false, bool edge = false)
    {
      Vector3 tip = o.T.TransformPoint(o.Item.Muzzle);
      Vector3 hilt = o.T.position;
      // measured against your own body, not the world: walking, swinging on
      // the map or flying with a sword in hand is not a swing, only moving the sword is
      Vector3 rel = tip - RigOrigin();
      Vector3 last = right ? lastTipR : lastTipL;
      Vector3 vel = (rel - last) / Mathf.Max(0.001f, dt);
      if (right) lastTipR = rel; else lastTipL = rel;
      float speed = vel.magnitude;
      if (speed > 30f) return; // teleport / first frame
      if (speed > 3f && Time.time >= (right ? swingSoundR : swingSoundL))
      {
        if (right) swingSoundR = Time.time + 0.35f; else swingSoundL = Time.time + 0.35f;
        SbFx.PlayMc("sweep", SbSound.Swing, tip, Mathf.Clamp01(speed / 8f), 1f);
      }
      // the lightsaber's blade, the chainsaw, the whip (SandBoxMelee.cs)
      Vector3 mdir = speed > 0.01f ? vel / speed : Vector3.zero;
      if (!MeleeExtras(o, right, trig, edge, ref speed, ref mdir, tip, hilt)) return;
      if (speed < 2f) return;
      int tier = Mathf.Max(1, o.Item.MeleeTier);
      Vector3 dir = mdir.sqrMagnitude > 0.5f ? mdir.normalized : vel / Mathf.Max(0.01f, vel.magnitude);

      foreach (SbObject other in new List<SbObject>(List))
      {
        if (other == o || other.Held || other.HeldRemote || other.T == null) continue;
        bool touched = false; Vector3 at = tip;
        for (int i = 1; i <= 4 && !touched; i++)
        {
          Vector3 pnt = Vector3.Lerp(hilt, tip, i / 4f);
          Vector3 lp = other.T.InverseTransformPoint(pnt);
          Bounds b = other.IsCell ? new Bounds(Vector3.zero, Vector3.one) : other.LocalBounds;
          b.Expand(0.04f / Mathf.Max(0.01f, other.T.lossyScale.x));
          if (b.Contains(lp)) { touched = true; at = pnt; }
        }
        if (!touched) continue;
        float cd;
        if (hitCooldown.TryGetValue(other.Key, out cd) && Time.time < cd) continue;
        hitCooldown[other.Key] = Time.time + 0.3f;
        Crit(at, dir);
        Haptic?.Invoke(right, 0.5f, 0.06f);
        Vector3 imp = dir * Mathf.Min(speed, 10f) * 0.5f * tier;
        if (other.Mine || other.Generated) ApplyHitToMine(other, imp, at, false);
        else if (other.Owner > 0) SbNet.SendHitObject(other.Owner, other.Id, imp, at, false);
      }

      foreach (VRRig r in Rigs())
      {
        if (r == null || r.isOfflineVRRig) continue;
        Vector3 head = r.headMesh != null ? r.headMesh.transform.position : r.transform.position;
        Vector3 body = r.transform.position;
        float dh = DistSeg(head, hilt, tip), db = DistSeg(body, hilt, tip);
        if (dh > 0.22f && db > 0.3f) continue;
        int actor = ActorOf(r);
        if (actor <= 0) continue;
        long key = -1000L - actor;
        float cd;
        if (hitCooldown.TryGetValue(key, out cd) && Time.time < cd) continue;
        hitCooldown[key] = Time.time + 0.5f;
        Vector3 at = dh < db ? head : body;
        Crit(at, dir);
        Blood(at, dir);
        Haptic?.Invoke(right, 0.7f, 0.08f);
        SbNet.SendHitPlayer(actor, (dir + Vector3.up * 0.45f).normalized * (2.5f + tier * 1.3f));
      }
    }

    private void Crit(Vector3 at, Vector3 dir)
    {
      SbFx.PlayMc("strong", SbSound.Hit, at, 0.9f);
      SbFx.Burst(at, 14, new Color(1f, 1f, 0.75f), new Color(0.9f, 0.85f, 1f), 1f, 3f, 0.03f, 0.06f, 0.3f, 0.6f, 0.6f, true, 0.05f, dir, 50f);
    }

    private static float DistSeg(Vector3 p, Vector3 a, Vector3 b)
    {
      Vector3 ab = b - a;
      float t = Mathf.Clamp01(Vector3.Dot(p - a, ab) / Mathf.Max(1e-6f, ab.sqrMagnitude));
      return Vector3.Distance(p, a + ab * t);
    }

    private void ProcessPendingBlasts()
    {
      for (int i = pendingBlasts.Count - 1; i >= 0; i--)
      {
        if (Time.time < pendingBlasts[i].at) continue;
        var b = pendingBlasts[i];
        pendingBlasts.RemoveAt(i);
        Explode(b.pos, b.radius, b.kind, true);
      }
    }
  }
}
