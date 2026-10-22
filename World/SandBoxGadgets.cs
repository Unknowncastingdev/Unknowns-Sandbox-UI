using System;
using System.Collections.Generic;
using GorillaLocomotion;
using UnityEngine;

namespace UnknownsSandbox
{
  // gadget guns and turrets.
  //  - Portal guns: Rick & Morty's (green swirls) and Aperture's (blue / orange). Each
  //    shot opens one end of your pair where it lands; walk, fall or throw things
  //    through one end and they come out of the other, keeping their speed. Everyone
  //    with SandBox sees everyone's portals and can use them (players only where the
  //    ender pearl may teleport: a SandBox lobby where everyone has the SandBox).
  //  - Gravity gun: hold the trigger to pull one of your things to the muzzle and carry
  //    it, let go to fire it away. A quick tap punts what you point at.
  //  - Teleport gun: you appear where it hits (same rule as the ender pearl).
  //  - Build gun: places the last block you picked on the tablet, up to 12 m away.
  //  - Auto-turret: shoots mobs and other players nearby, never the one who placed it.
  //    Sentry: the same, floating along beside you.
  internal enum SbTurretKind { None, Turret, Sentry }

  internal sealed partial class SandBoxWorld
  {
    // the block the build gun places (the tablet sets it when you pick a block)
    internal static string BuildGunBlock = "mc_stone";
    internal static int BuildGunSize = 5;

    private static bool IsGadget(SbAmmo a)
    {
      return a == SbAmmo.RickPortal || a == SbAmmo.AperturePortal || a == SbAmmo.Gravity || a == SbAmmo.Teleport || a == SbAmmo.Build;
    }

    // ================================================================ firing
    // true when the shot was a gadget's (handled here)
    private bool FireGadget(SbObject o, bool right)
    {
      SbItem it = o.Item;
      if (!IsGadget(it.Ammo)) return false;
      Vector3 muzzle = o.T.TransformPoint(it.Muzzle);
      Vector3 dir = o.T.forward;
      switch (it.Ammo)
      {
        case SbAmmo.RickPortal:
        case SbAmmo.AperturePortal:
          ShootPortal(it.Ammo == SbAmmo.RickPortal ? 0 : 1, muzzle, dir, right);
          break;
        case SbAmmo.Teleport:
          {
            Vector3 end, n; SbObject obj; VRRig rig;
            bool hit = Cast(muzzle, muzzle + dir * 80f, out end, out n, out obj, out rig);
            GadgetShotFx(it.Ammo, muzzle, hit ? end : muzzle + dir * 80f, n);
            SbNet.SendShot(it.Key, muzzle, dir, 0, o.Scale);
            if (!hit || rig != null) break;
            if (!PearlAllowed) { if (Time.time >= pearlToastAt) { pearlToastAt = Time.time + 6f; Toast?.Invoke("Teleport Gun", "Teleports only in a SandBox lobby where everyone has the SandBox"); } break; }
            try
            {
              GTPlayer p = GTPlayer.Instance;
              if (p != null)
              {
                p.TeleportTo(end + n * 0.4f + Vector3.up * 0.6f, p.transform.rotation, false, true);
                if (p.playerRigidBody != null) p.playerRigidBody.linearVelocity = Vector3.zero;
              }
            }
            catch { }
            break;
          }
        case SbAmmo.Build:
          {
            SbItem block = SbCatalog.Get(BuildGunBlock);
            if (block == null || block.Kind != SbKind.Block) block = SbCatalog.Get("mc_stone");
            if (block == null) break;
            var aim = new SbAim();
            ComputeAim(muzzle, dir, block, BuildGunSize, 1f, 0f, false, aim);
            GadgetShotFx(it.Ammo, muzzle, aim.Hit ? aim.HitPoint : muzzle + dir * 12f, aim.HitNormal);
            SbNet.SendShot(it.Key, muzzle, dir, 0, o.Scale);
            if (aim.Valid && aim.Hit) Place(aim, block, true);
            else SbFx.Play(SbSound.UiHover, muzzle, 0.4f, 0.6f);
            break;
          }
        case SbAmmo.Gravity:
          // a tap with nothing held: punt
          if (GravHeld(right) == null) PuntWithGravityGun(muzzle, dir, it, o.Scale);
          break;
      }
      Haptic?.Invoke(right, 0.35f, 0.06f);
      return true;
    }

    // another player's gadget shot: just what it looks like
    private void GadgetShotFx(SbAmmo a, Vector3 from, Vector3 to, Vector3 normal)
    {
      switch (a)
      {
        case SbAmmo.Teleport:
          Beam(from, to, new Color(0.75f, 0.35f, 1f));
          SbFx.Burst(to, 30, new Color(0.8f, 0.4f, 1f), new Color(0.4f, 0.2f, 0.9f), 0.6f, 2.2f, 0.03f, 0.07f, 0.4f, 0.8f, -0.2f, true, 0.2f);
          SbFx.Play(SbSound.Laser, from, 0.6f, 0.7f);
          break;
        case SbAmmo.Build:
          Beam(from, to, new Color(1f, 0.85f, 0.3f));
          SbFx.Play(SbSound.Laser, from, 0.4f, 1.5f);
          break;
        case SbAmmo.Gravity:
          Beam(from, to, new Color(1f, 0.6f, 0.15f));
          SbFx.Play(SbSound.Thunk, from, 0.8f, 0.7f);
          break;
      }
    }

    // ================================================================ gravity gun
    private SbObject gravL, gravR;
    private bool gravTrigL, gravTrigR;
    private float gravDownL, gravDownR;

    private SbObject GravHeld(bool right) { return right ? gravR : gravL; }

    private void UpdateGravityGun(SbObject gun, bool right, bool trig, bool edge)
    {
      Vector3 muzzle = gun.T.TransformPoint(gun.Item.Muzzle);
      Vector3 dir = gun.T.forward;
      SbObject held = GravHeld(right);
      if (held != null && (held.T == null || !objects.ContainsKey(held.Key))) held = null;
      if (edge)
      {
        if (right) gravDownR = Time.time; else gravDownL = Time.time;
      }
      if (trig)
      {
        float down = right ? gravDownR : gravDownL;
        // held a moment: pull and carry
        if (held == null && Time.time - down > 0.15f)
        {
          float od; Vector3 on;
          SbObject t = RaycastObjects(new Ray(muzzle, dir), 30f, out od, out on, false, false, gun);
          if (t != null && (t.Mine || t.Generated) && !t.IsCell && !t.Held && t.Item.Vehicle == null && t.T != null)
          {
            held = t;
            if (t.Rb == null || t.Rb.isKinematic) MakeDynamic(t, Vector3.zero);
            SbFx.Play(SbSound.UiOpen, muzzle, 0.6f, 0.6f);
          }
        }
        if (held != null && held.Rb != null)
        {
          float reach = 0.6f + held.LocalBounds.extents.magnitude * held.Scale;
          Vector3 want = muzzle + dir * reach;
          Vector3 to = want - held.Center;
          held.Rb.linearVelocity = Vector3.ClampMagnitude(to * 14f, 25f);
          held.Rb.angularVelocity *= 0.85f;
          held.SentPos = Vector3.one * 99999f;
          SbNet.QueueMove(held);
          if (Time.frameCount % 3 == 0) SbFx.Burst(Vector3.Lerp(muzzle, held.Center, 0.5f), 2, new Color(1f, 0.7f, 0.2f), new Color(1f, 0.45f, 0.1f), 0.1f, 0.4f, 0.015f, 0.03f, 0.1f, 0.2f, 0f, true, 0.05f);
        }
      }
      else if (held != null)
      {
        // let go: fired away
        if (held.Rb != null) held.Rb.linearVelocity = dir * 18f;
        held.SentPos = Vector3.one * 99999f;
        SbNet.QueueMove(held);
        GadgetShotFx(SbAmmo.Gravity, muzzle, held.Center, Vector3.up);
        SbNet.SendShot(gun.Item.Key, muzzle, dir, 0, gun.Scale);
        Haptic?.Invoke(right, 0.6f, 0.08f);
        held = null;
      }
      else if (!trig && (right ? gravTrigR : gravTrigL))
      {
        // released quickly with nothing held: a punt
        float down = right ? gravDownR : gravDownL;
        if (Time.time - down <= 0.15f) FireGadget(gun, right);
      }
      if (right) { gravR = held; gravTrigR = trig; } else { gravL = held; gravTrigL = trig; }
    }

    private void PuntWithGravityGun(Vector3 muzzle, Vector3 dir, SbItem it, float scale)
    {
      Vector3 end, n; SbObject obj; VRRig rig;
      bool hit = Cast(muzzle, muzzle + dir * 15f, out end, out n, out obj, out rig);
      GadgetShotFx(SbAmmo.Gravity, muzzle, hit ? end : muzzle + dir * 15f, n);
      SbNet.SendShot(it.Key, muzzle, dir, 0, scale);
      if (hit) ApplyImpactHits(obj, rig, end, dir, 10f, false);
    }

    // ================================================================ portals
    private sealed class SbGunPortal
    {
      public int Owner;            // 0 = mine
      public int Kind;             // 0 Rick & Morty, 1 Aperture
      public int Slot;             // 0 / 1: the two ends
      public Vector3 Pos, Normal, Up;
      public GameObject Vis;
      public Transform Swirl;
      public float Open;
    }

    private readonly Dictionary<long, SbGunPortal> gunPortals = new Dictionary<long, SbGunPortal>();
    private readonly int[] nextPortalSlot = new int[2];
    private float gunPortalCooldown;
    private readonly Dictionary<SbObject, float> objPortalCooldown = new Dictionary<SbObject, float>();
    private static Mesh rickPortalMesh;
    private const float PortalW = 1.0f, PortalH = 1.6f;

    private static long PortalKey(int owner, int kind, int slot) { return ((long)owner << 8) | ((long)kind << 1) | (long)slot; }

    private void ShootPortal(int kind, Vector3 muzzle, Vector3 dir, bool right)
    {
      Vector3 end, n; SbObject obj; VRRig rig;
      bool hit = Cast(muzzle, muzzle + dir * 80f, out end, out n, out obj, out rig);
      Color c = kind == 0 ? new Color(0.45f, 1f, 0.3f) : (nextPortalSlot[kind] == 0 ? new Color(0.2f, 0.6f, 1f) : new Color(1f, 0.55f, 0.1f));
      Beam(muzzle, hit ? end : muzzle + dir * 80f, c);
      SbFx.Play(kind == 0 ? SbSound.Bubble : SbSound.Laser, muzzle, 0.8f, kind == 0 ? 0.6f : 1.3f);
      if (!hit || rig != null) return;
      int slot = nextPortalSlot[kind];
      nextPortalSlot[kind] = 1 - slot;
      // upright on walls, facing you on floors and ceilings
      Vector3 up = Mathf.Abs(Vector3.Dot(n, Vector3.up)) > 0.8f ? Vector3.ProjectOnPlane(-dir, n).normalized : Vector3.ProjectOnPlane(Vector3.up, n).normalized;
      if (up.sqrMagnitude < 0.01f) up = Vector3.forward;
      Vector3 pos = end + n * 0.02f;
      SetPortal(0, kind, slot, true, pos, n, up);
      SbNet.SendPortal(kind, slot, true, pos, n, up);
    }

    internal void NetPortal(int actor, int kind, int slot, bool on, Vector3 pos, Vector3 normal, Vector3 up)
    {
      if (kind < 0 || kind > 1 || slot < 0 || slot > 1) return;
      SetPortal(actor, kind, slot, on, pos, normal.normalized, up.normalized);
    }

    private void SetPortal(int owner, int kind, int slot, bool on, Vector3 pos, Vector3 normal, Vector3 up)
    {
      long key = PortalKey(owner, kind, slot);
      SbGunPortal p;
      gunPortals.TryGetValue(key, out p);
      if (!on)
      {
        if (p != null && p.Vis != null) Destroy(p.Vis);
        gunPortals.Remove(key);
        return;
      }
      if (p == null) { p = new SbGunPortal { Owner = owner, Kind = kind, Slot = slot }; gunPortals[key] = p; }
      p.Pos = pos; p.Normal = normal; p.Up = up; p.Open = 0f;
      if (p.Vis == null) p.Vis = MakePortalVisual(kind, slot, out p.Swirl);
      p.Vis.transform.SetPositionAndRotation(pos, Quaternion.LookRotation(normal, up));
      Color c = kind == 0 ? new Color(0.45f, 1f, 0.3f) : (slot == 0 ? new Color(0.2f, 0.6f, 1f) : new Color(1f, 0.55f, 0.1f));
      SbFx.Burst(pos, 40, c, Color.white, 0.5f, 2f, 0.03f, 0.08f, 0.3f, 0.7f, 0f, true, 0.4f, normal, 60f);
      SbFx.Play(SbSound.Bubble, pos, 0.7f, kind == 0 ? 0.5f : 1.1f);
    }

    // a player joined: your open portals again (everyone gets them; harmless)
    internal void ResendMyPortals()
    {
      foreach (SbGunPortal p in gunPortals.Values)
        if (p.Owner == 0) SbNet.SendPortal(p.Kind, p.Slot, true, p.Pos, p.Normal, p.Up);
    }

    // removes another player's portals when they leave
    internal void ClearPortalsOf(int actor)
    {
      foreach (long k in new List<long>(gunPortals.Keys))
      {
        SbGunPortal p = gunPortals[k];
        if (p.Owner != actor) continue;
        if (p.Vis != null) Destroy(p.Vis);
        gunPortals.Remove(k);
      }
    }

    private GameObject MakePortalVisual(int kind, int slot, out Transform swirl)
    {
      var go = new GameObject(kind == 0 ? "SB Rick Portal" : "SB Aperture Portal");
      go.transform.SetParent(SbFx.Root, false);
      var mats = new[] { SbMats.Lit, SbMats.Glow };
      if (kind == 0)
      {
        // a green, swirling disc with a bright rim
        if (rickPortalMesh == null)
        {
          var k = new SbMeshKit();
          k.Cyl(Vector3.zero, 0.5f, 0.02f, Quaternion.Euler(90f, 0f, 0f), new Color(0.18f, 0.55f, 0.12f), 32, true);
          k.Torus(Vector3.zero, 0.5f, 0.05f, Quaternion.Euler(90f, 0f, 0f), new Color(0.75f, 1f, 0.45f), 32, 8, true);
          rickPortalMesh = k.ToMesh("Unknowns Rick Portal");
        }
        var disc = new GameObject("disc");
        disc.transform.SetParent(go.transform, false);
        disc.transform.localScale = new Vector3(PortalW, PortalH, 1f);
        disc.AddComponent<MeshFilter>().sharedMesh = rickPortalMesh;
        disc.AddComponent<MeshRenderer>().sharedMaterials = mats;
        // the swirl: arms that spin
        var sw = new GameObject("swirl");
        sw.transform.SetParent(go.transform, false);
        sw.transform.localPosition = new Vector3(0f, 0f, 0.015f);
        sw.transform.localScale = new Vector3(PortalW, PortalH, 1f);
        var ks = new SbMeshKit();
        for (int arm = 0; arm < 4; arm++)
          for (int i = 0; i < 14; i++)
          {
            float t = i / 14f;
            float a = arm * Mathf.PI * 0.5f + t * Mathf.PI * 1.6f;
            float r = 0.06f + t * 0.4f;
            ks.Ball(new Vector3(Mathf.Cos(a) * r, Mathf.Sin(a) * r, 0f), 0.03f + t * 0.025f, Color.Lerp(new Color(0.95f, 1f, 0.7f), new Color(0.35f, 0.9f, 0.2f), t), 6, true);
          }
        sw.AddComponent<MeshFilter>().sharedMesh = ks.ToMesh("Unknowns Rick Swirl");
        sw.AddComponent<MeshRenderer>().sharedMaterials = mats;
        swirl = sw.transform;
      }
      else
      {
        // an oval ring of light around a dark, glowing middle (blue or orange)
        Color c = slot == 0 ? new Color(0.2f, 0.6f, 1f) : new Color(1f, 0.55f, 0.1f);
        var k = new SbMeshKit();
        k.Cyl(Vector3.zero, 0.48f, 0.01f, Quaternion.Euler(90f, 0f, 0f), c * 0.35f, 32, true);
        k.Torus(Vector3.zero, 0.5f, 0.035f, Quaternion.Euler(90f, 0f, 0f), c, 40, 8, true);
        k.Torus(Vector3.zero, 0.44f, 0.012f, Quaternion.Euler(90f, 0f, 0f), Color.Lerp(c, Color.white, 0.6f), 40, 6, true);
        var disc = new GameObject("ring");
        disc.transform.SetParent(go.transform, false);
        disc.transform.localScale = new Vector3(PortalW, PortalH, 1f);
        disc.AddComponent<MeshFilter>().sharedMesh = k.ToMesh("Unknowns Aperture Portal");
        disc.AddComponent<MeshRenderer>().sharedMaterials = mats;
        swirl = disc.transform;
      }
      var light = go.AddComponent<Light>();
      light.type = LightType.Point;
      light.range = 3f;
      light.intensity = 1.6f;
      light.color = kind == 0 ? new Color(0.45f, 1f, 0.3f) : (slot == 0 ? new Color(0.2f, 0.6f, 1f) : new Color(1f, 0.55f, 0.1f));
      return go;
    }

    private SbGunPortal OtherEnd(SbGunPortal p)
    {
      SbGunPortal o;
      return gunPortals.TryGetValue(PortalKey(p.Owner, p.Kind, 1 - p.Slot), out o) ? o : null;
    }

    // inside the opening of p (local x / y within the oval, a little in front of it)
    private static bool InPortal(SbGunPortal p, Vector3 point, float front)
    {
      Vector3 d = point - p.Pos;
      float z = Vector3.Dot(d, p.Normal);
      if (z < -0.15f || z > front) return false;
      Vector3 right = Vector3.Cross(p.Up, p.Normal);
      float x = Vector3.Dot(d, right) / (PortalW * 0.5f), y = Vector3.Dot(d, p.Up) / (PortalH * 0.5f);
      return x * x + y * y < 1f;
    }

    // from p's side to q's: positions and directions turned so leaving q is like entering p
    private static Quaternion PortalTurn(SbGunPortal p, SbGunPortal q)
    {
      Quaternion a = Quaternion.LookRotation(-p.Normal, p.Up);
      Quaternion b = Quaternion.LookRotation(q.Normal, q.Up);
      return b * Quaternion.Inverse(a);
    }

    private void UpdateGunPortals(float dt)
    {
      if (gunPortals.Count == 0) return;
      float now = Time.time;
      foreach (SbGunPortal p in gunPortals.Values)
      {
        if (p.Vis == null) continue;
        p.Open = Mathf.MoveTowards(p.Open, 1f, dt * 4f);
        float s = 1f - Mathf.Pow(1f - p.Open, 3f);
        p.Vis.transform.localScale = Vector3.one * Mathf.Max(0.01f, s);
        if (p.Swirl != null) p.Swirl.Rotate(0f, 0f, (p.Kind == 0 ? -220f : 25f) * dt, Space.Self);
      }

      // you
      if (now >= gunPortalCooldown && PearlAllowed)
      {
        try
        {
          GTPlayer pl = GTPlayer.Instance;
          if (pl != null && pl.bodyCollider != null)
          {
            Vector3 body = pl.bodyCollider.transform.position;
            Vector3 vel = pl.playerRigidBody != null ? pl.playerRigidBody.linearVelocity : Vector3.zero;
            foreach (SbGunPortal p in gunPortals.Values)
            {
              if (p.Open < 0.8f || !InPortal(p, body, 0.45f) || Vector3.Dot(vel, p.Normal) > 0.5f) continue;
              SbGunPortal q = OtherEnd(p);
              if (q == null) continue;
              Quaternion turn = PortalTurn(p, q);
              Vector3 offset = turn * (body - p.Pos);
              Vector3 dest = q.Pos + q.Normal * 0.7f + Vector3.ProjectOnPlane(offset, q.Normal);
              Vector3 fwd = turn * pl.transform.forward;
              Vector3 flat = Vector3.ProjectOnPlane(fwd, Vector3.up);
              Quaternion look = flat.sqrMagnitude > 1e-4f ? Quaternion.LookRotation(flat.normalized) : pl.transform.rotation;
              pl.TeleportTo(dest, look, false, true);
              // out at least as fast as you went in, straight out of the other end
              Vector3 outVel = turn * vel;
              float speed = Mathf.Max(outVel.magnitude, 2.5f);
              if (pl.playerRigidBody != null) pl.playerRigidBody.linearVelocity = Vector3.Lerp(outVel.normalized, q.Normal, 0.35f).normalized * speed;
              gunPortalCooldown = now + 0.5f;
              SbFx.Play(SbSound.Bubble, dest, 0.8f, p.Kind == 0 ? 0.45f : 1.2f);
              Haptic?.Invoke(true, 0.5f, 0.08f); Haptic?.Invoke(false, 0.5f, 0.08f);
              break;
            }
          }
        }
        catch (Exception ex) { SbLog.Throttled("SandBox portal", ex); }
      }

      // your loose things
      foreach (SbObject o in List)
      {
        if (!o.Mine || o.T == null || o.Rb == null || o.Rb.isKinematic || o.Held || o.Parent != null) continue;
        float until;
        if (objPortalCooldown.TryGetValue(o, out until) && now < until) continue;
        Vector3 c = o.Center;
        foreach (SbGunPortal p in gunPortals.Values)
        {
          if (p.Open < 0.8f || !InPortal(p, c, 0.35f) || Vector3.Dot(o.Rb.linearVelocity, p.Normal) > 0f) continue;
          SbGunPortal q = OtherEnd(p);
          if (q == null) continue;
          Quaternion turn = PortalTurn(p, q);
          Vector3 dest = q.Pos + q.Normal * (0.3f + o.LocalBounds.extents.magnitude * o.Scale) + Vector3.ProjectOnPlane(turn * (c - p.Pos), q.Normal);
          o.T.position += dest - c;
          o.T.rotation = turn * o.T.rotation;
          o.Rb.position = o.T.position;
          o.Rb.linearVelocity = turn * o.Rb.linearVelocity;
          o.SentPos = Vector3.one * 99999f;
          SbNet.QueueMove(o);
          objPortalCooldown[o] = now + 0.4f;
          break;
        }
      }
      if (objPortalCooldown.Count > 200) objPortalCooldown.Clear();
    }

    // ================================================================ turrets
    private float turretScanAt;
    private readonly Dictionary<SbObject, float> turretFireAt = new Dictionary<SbObject, float>();
    private readonly Dictionary<SbObject, Vector3> turretTarget = new Dictionary<SbObject, Vector3>();

    private void UpdateTurrets(float dt)
    {
      bool scan = Time.time >= turretScanAt;
      if (scan) turretScanAt = Time.time + 0.25f;
      SbItem rifle = null;
      foreach (SbObject o in List)
      {
        if (!o.Mine || o.T == null || o.Item.Turret == SbTurretKind.None || o.Held) continue;
        if (o.Item.Turret == SbTurretKind.Sentry) FollowOwner(o, dt);
        Vector3 muzzle = o.T.TransformPoint(o.Item.Muzzle);
        if (scan) turretTarget[o] = FindTurretTarget(o, muzzle);
        Vector3 aim;
        if (!turretTarget.TryGetValue(o, out aim) || aim == Vector3.zero) continue;
        // turn to the target (the sentry also tips up and down)
        Vector3 to = aim - o.T.position;
        Vector3 flat = o.Item.Turret == SbTurretKind.Turret ? Vector3.ProjectOnPlane(to, Vector3.up) : to;
        if (flat.sqrMagnitude > 1e-4f)
        {
          o.T.rotation = Quaternion.RotateTowards(o.T.rotation, Quaternion.LookRotation(flat.normalized, Vector3.up), 240f * dt);
          if (o.Rb != null && o.Rb.isKinematic) o.Rb.rotation = o.T.rotation;
        }
        float next;
        turretFireAt.TryGetValue(o, out next);
        if (Time.time < next) continue;
        Vector3 dir = (aim - muzzle).normalized;
        Vector3 face = o.Item.Turret == SbTurretKind.Turret ? Vector3.ProjectOnPlane(dir, Vector3.up) : dir;
        if (face.sqrMagnitude > 1e-4f && Vector3.Angle(o.T.forward, face) > 12f) continue;
        turretFireAt[o] = Time.time + (o.Item.Turret == SbTurretKind.Turret ? 0.16f : 0.3f);
        if (rifle == null) rifle = SbCatalog.Get("gun_rifle");
        if (rifle == null) continue;
        int seed = UnityEngine.Random.Range(1, int.MaxValue);
        Shoot(rifle, muzzle, dir, seed, true, 1f);
        SbNet.SendShot(rifle.Key, muzzle, dir, seed, 1f);
        o.SentPos = Vector3.one * 99999f;
        SbNet.QueueMove(o);
      }
      if (turretFireAt.Count > 100) { turretFireAt.Clear(); turretTarget.Clear(); }
    }

    // the nearest mob or other player in sight (never you), zero when none
    private Vector3 FindTurretTarget(SbObject t, Vector3 muzzle)
    {
      const float range = 22f;
      Vector3 best = Vector3.zero;
      float bestD = range * range;
      foreach (SbObject m in List)
      {
        if (m.Mob == null || m.T == null || m == t) continue;
        Vector3 c = m.Center;
        float d = (c - muzzle).sqrMagnitude;
        if (d < bestD && ClearShot(muzzle, c, m)) { bestD = d; best = c; }
      }
      foreach (VRRig r in Rigs())
      {
        if (r == null || r.isOfflineVRRig || !r.gameObject.activeInHierarchy) continue;
        Vector3 c = r.transform.position;
        float d = (c - muzzle).sqrMagnitude;
        if (d < bestD && ClearShot(muzzle, c, null)) { bestD = d; best = c; }
      }
      return best;
    }

    private bool ClearShot(Vector3 from, Vector3 to, SbObject target)
    {
      RaycastHit wh;
      Vector3 d = to - from;
      float len = d.magnitude;
      if (len < 0.05f) return true;
      if (RaycastWorld(new Ray(from, d / len), len - 0.3f, out wh)) return false;
      float od; Vector3 on;
      SbObject o = RaycastObjects(new Ray(from, d / len), len - 0.3f, out od, out on, false, false);
      return o == null || o == target || o.Mob != null || o.Item.Turret != SbTurretKind.None;
    }

    // the sentry floats beside your head
    private void FollowOwner(SbObject o, float dt)
    {
      Transform head = HeadProvider != null ? HeadProvider() : null;
      if (head == null) return;
      Vector3 flat = Vector3.ProjectOnPlane(head.forward, Vector3.up);
      if (flat.sqrMagnitude < 1e-4f) flat = Vector3.forward;
      flat.Normalize();
      Vector3 right = Vector3.Cross(Vector3.up, flat);
      Vector3 want = head.position + right * 0.6f - flat * 0.25f + Vector3.up * (0.3f + Mathf.Sin(Time.time * 2f) * 0.05f);
      if (o.Rb != null && !o.Rb.isKinematic) { o.Rb.isKinematic = true; }
      o.T.position = Vector3.Lerp(o.T.position, want, 1f - Mathf.Exp(-5f * dt));
      if (o.Rb != null) o.Rb.position = o.T.position;
      turretTarget.TryGetValue(o, out Vector3 aim);
      if (aim == Vector3.zero) o.T.rotation = Quaternion.Slerp(o.T.rotation, Quaternion.LookRotation(flat), 1f - Mathf.Exp(-4f * dt));
      o.SentPos = Vector3.one * 99999f;
      if (Time.frameCount % 6 == 0) SbNet.QueueMove(o);
    }

    // ================================================================ every frame
    private void UpdateGadgets(float dt)
    {
      try { UpdateGunPortals(dt); } catch (Exception ex) { SbLog.Throttled("SandBox portals", ex); }
      try { UpdateTurrets(dt); } catch (Exception ex) { SbLog.Throttled("SandBox turrets", ex); }
    }
  }

  // ==================================================================== catalog
  internal static partial class SbGadgetArt
  {
    private static Color C(string h) { return SbCatalog.C(h); }

    // the special guns modelled on the real ones (from the show and the games),
    // part by part. +Z is the barrel, the hand at the origin.

    // a claw: from its root on a ring (radius rad, turned `a` degrees round the barrel) out
    // and forward, then bent back in towards the middle
    private static void Claw(SbMeshKit k, Vector3 centre, float a, float rad, float len, float thick, Color col, Color inner)
    {
      Quaternion r = Quaternion.Euler(0f, 0f, a);
      Vector3 root = centre + r * new Vector3(0f, rad, 0f);
      Vector3 mid = root + r * (Quaternion.Euler(-14f, 0f, 0f) * new Vector3(0f, 0f, len * 0.55f));
      k.Box((root + mid) * 0.5f, new Vector3(thick * 1.6f, thick, len * 0.58f), r * Quaternion.Euler(-14f, 0f, 0f), col);
      Vector3 tip = mid + r * (Quaternion.Euler(32f, 0f, 0f) * new Vector3(0f, 0f, len * 0.45f));
      k.Box((mid + tip) * 0.5f, new Vector3(thick * 1.3f, thick * 0.9f, len * 0.48f), r * Quaternion.Euler(32f, 0f, 0f), col);
      k.Box((root + mid) * 0.5f + r * new Vector3(0f, -thick * 0.55f, 0f), new Vector3(thick * 1.1f, thick * 0.25f, len * 0.5f), r * Quaternion.Euler(-14f, 0f, 0f), inner);
    }

    // Rick's portal gun (Rick and Morty): a pale grey pistol body, the green portal-fluid
    // vial standing up on top at the back in a metal cage, a stubby nose with a green
    // bulb, and the dimension dial and counter on its sides
    internal static void RickGun(SbMeshKit k)
    {
      Color grey = C("#d7dbde"), mid = C("#a9afb5"), dark = C("#3b3f44"), green = C("#7dff4a"), fluid = C("#3fe83a"), glass = C("#cfffd0");
      // body: a rounded block, a bit longer than a hand
      k.Box(new Vector3(0f, 0.055f, 0.035f), new Vector3(0.066f, 0.06f, 0.15f), grey);
      k.Sphere(new Vector3(0f, 0.055f, 0.105f), new Vector3(0.033f, 0.03f, 0.03f), grey, 16);
      k.Sphere(new Vector3(0f, 0.055f, -0.04f), new Vector3(0.033f, 0.03f, 0.022f), grey, 16);
      k.Box(new Vector3(0f, 0.027f, 0.035f), new Vector3(0.07f, 0.008f, 0.15f), mid);                 // the seam round its middle
      // the handle with a grey trigger guard
      k.Box(new Vector3(0f, -0.005f, -0.015f), new Vector3(0.04f, 0.1f, 0.045f), Quaternion.Euler(-10f, 0f, 0f), grey);
      k.Box(new Vector3(0f, 0.008f, 0.035f), new Vector3(0.01f, 0.03f, 0.04f), mid);
      k.Box(new Vector3(0f, 0.025f, 0.02f), new Vector3(0.008f, 0.022f, 0.008f), dark);                 // trigger
      // the nose: a short metal collar and the green bulb
      k.Frustum(new Vector3(0f, 0.055f, 0.14f), 0.026f, 0.022f, 0.03f, Quaternion.Euler(90f, 0f, 0f), mid, 18);
      k.Torus(new Vector3(0f, 0.055f, 0.152f), 0.023f, 0.004f, Quaternion.Euler(90f, 0f, 0f), dark, 18, 6);
      k.Sphere(new Vector3(0f, 0.055f, 0.158f), new Vector3(0.019f, 0.019f, 0.016f), green, 14, true);
      // the vial on top at the back: a glass tube of green fluid between two metal caps, in a cage
      Vector3 vial = new Vector3(0f, 0.125f, -0.005f);
      k.Cyl(vial + Vector3.down * 0.045f, 0.03f, 0.012f, Quaternion.identity, mid, 18);
      k.Cyl(vial, 0.024f, 0.075f, Quaternion.identity, glass, 18);
      k.Cyl(vial + Vector3.down * 0.006f, 0.02f, 0.06f, Quaternion.identity, fluid, 16, true);
      k.Cyl(vial + Vector3.up * 0.043f, 0.027f, 0.012f, Quaternion.identity, mid, 18);
      k.Sphere(vial + Vector3.up * 0.05f, new Vector3(0.018f, 0.01f, 0.018f), mid, 12);
      for (int i = 0; i < 4; i++)
      {
        Vector3 p = vial + Quaternion.Euler(0f, 45f + i * 90f, 0f) * new Vector3(0f, 0f, 0.028f);
        k.Box(p, new Vector3(0.005f, 0.08f, 0.005f), Quaternion.Euler(0f, 45f + i * 90f, 0f), dark);
      }
      // the dimension dial (right) and the counter screen (left)
      k.Cyl(new Vector3(0.037f, 0.06f, 0.05f), 0.016f, 0.01f, Quaternion.Euler(0f, 0f, 90f), dark, 16);
      k.Box(new Vector3(0.043f, 0.06f, 0.05f), new Vector3(0.003f, 0.004f, 0.022f), grey);
      k.Box(new Vector3(-0.034f, 0.062f, 0.045f), new Vector3(0.004f, 0.022f, 0.05f), dark);
      k.Box(new Vector3(-0.036f, 0.062f, 0.045f), new Vector3(0.002f, 0.014f, 0.04f), C("#9dff6a"), true);
      k.GripAt(Vector3.zero).MuzzleAt(new Vector3(0f, 0.055f, 0.18f));
    }

    // Aperture Science Handheld Portal Device (Portal 2): a long glossy white shell over a
    // black core, a black collar with three white claws round the emitter, the light tube
    // on top glowing the portal's colour, and the grip under the back
    internal static void ApertureGun(SbMeshKit k)
    {
      Color white = C("#f4f5f6"), off = C("#d9dce0"), black = C("#1e2023"), grey = C("#5b6067"), blue = C("#3fa7ff"), core = C("#bfe6ff");
      // the black core and grip
      k.Box(new Vector3(0f, 0.045f, 0.02f), new Vector3(0.07f, 0.05f, 0.3f), black);
      k.Box(new Vector3(0f, -0.02f, -0.06f), new Vector3(0.042f, 0.11f, 0.05f), Quaternion.Euler(-14f, 0f, 0f), black);
      k.Box(new Vector3(0f, 0.012f, -0.005f), new Vector3(0.009f, 0.026f, 0.012f), grey);   // trigger
      // the white shell: one long rounded piece over the top and sides, and a rounded back
      k.Sphere(new Vector3(0f, 0.075f, -0.01f), new Vector3(0.068f, 0.06f, 0.2f), white, 24);
      k.Sphere(new Vector3(0f, 0.06f, -0.15f), new Vector3(0.058f, 0.058f, 0.06f), white, 20);
      k.Box(new Vector3(0f, 0.04f, -0.01f), new Vector3(0.124f, 0.012f, 0.24f), off);       // the lower edge of the shell
      // the window on the top: the light tube (blue / orange portal colour)
      k.Cyl(new Vector3(0f, 0.132f, 0f), 0.014f, 0.17f, Quaternion.Euler(90f, 0f, 0f), core, 14, true);
      k.Torus(new Vector3(0f, 0.132f, -0.085f), 0.016f, 0.004f, Quaternion.Euler(90f, 0f, 0f), black, 14, 6);
      k.Torus(new Vector3(0f, 0.132f, 0.085f), 0.016f, 0.004f, Quaternion.Euler(90f, 0f, 0f), black, 14, 6);
      k.Box(new Vector3(0.05f, 0.06f, 0.08f), new Vector3(0.004f, 0.008f, 0.06f), blue, true);
      k.Box(new Vector3(-0.05f, 0.06f, 0.08f), new Vector3(0.004f, 0.008f, 0.06f), blue, true);
      // the front: the black collar, the emitter and its three claws
      Vector3 front = new Vector3(0f, 0.065f, 0.2f);
      k.Frustum(front, 0.058f, 0.05f, 0.04f, Quaternion.Euler(90f, 0f, 0f), black, 22);
      k.Torus(front + Vector3.forward * 0.02f, 0.052f, 0.006f, Quaternion.Euler(90f, 0f, 0f), grey, 22, 6);
      k.Frustum(front + Vector3.forward * 0.035f, 0.026f, 0.016f, 0.04f, Quaternion.Euler(90f, 0f, 0f), grey, 16);
      k.Ball(front + Vector3.forward * 0.06f, 0.016f, blue, 12, true);
      foreach (float a in new[] { 0f, 120f, 240f }) Claw(k, front + Vector3.forward * 0.015f, a, 0.045f, 0.11f, 0.014f, white, off);
      k.GripAt(Vector3.zero).MuzzleAt(front + Vector3.forward * 0.08f);
    }

    // the Zero Point Energy Field Manipulator (Half-Life 2): a dark gunmetal body with the
    // orange panels, a carry handle on top, the round capacitor at the back, and three big
    // claws round the glowing emitter; a pistol grip and a fore grip under it
    internal static void GravityGun(SbMeshKit k)
    {
      Color orange = C("#e8761a"), dark = C("#33363a"), darker = C("#1f2124"), steel = C("#8f959c"), glow = C("#ffd08a"), hazard = C("#151515");
      // body
      k.Box(new Vector3(0f, 0.06f, 0.05f), new Vector3(0.1f, 0.085f, 0.3f), dark);
      k.Box(new Vector3(0f, 0.06f, 0.05f), new Vector3(0.106f, 0.05f, 0.22f), orange);           // the orange side panels
      for (int i = 0; i < 4; i++)                                                                    // their dark stripes
      {
        k.Box(new Vector3(0.0535f, 0.06f, -0.03f + i * 0.05f), new Vector3(0.002f, 0.05f, 0.012f), Quaternion.Euler(30f, 0f, 0f), hazard);
        k.Box(new Vector3(-0.0535f, 0.06f, -0.03f + i * 0.05f), new Vector3(0.002f, 0.05f, 0.012f), Quaternion.Euler(30f, 0f, 0f), hazard);
      }
      k.Box(new Vector3(0f, 0.106f, 0.05f), new Vector3(0.08f, 0.01f, 0.24f), orange);          // top plate
      // the carry handle
      k.Box(new Vector3(0f, 0.14f, 0.05f), new Vector3(0.018f, 0.014f, 0.17f), darker);
      k.Box(new Vector3(0f, 0.124f, -0.03f), new Vector3(0.018f, 0.03f, 0.016f), darker);
      k.Box(new Vector3(0f, 0.124f, 0.13f), new Vector3(0.018f, 0.03f, 0.016f), darker);
      // the capacitor at the back, with its rings
      k.Cyl(new Vector3(0f, 0.06f, -0.13f), 0.048f, 0.07f, Quaternion.Euler(90f, 0f, 0f), steel, 20);
      for (int i = 0; i < 3; i++) k.Torus(new Vector3(0f, 0.06f, -0.155f + i * 0.025f), 0.05f, 0.005f, Quaternion.Euler(90f, 0f, 0f), darker, 20, 6);
      k.Cyl(new Vector3(0f, 0.06f, -0.17f), 0.03f, 0.01f, Quaternion.Euler(90f, 0f, 0f), orange, 16);
      // grips
      k.Box(new Vector3(0f, -0.02f, -0.02f), new Vector3(0.042f, 0.11f, 0.05f), Quaternion.Euler(-12f, 0f, 0f), darker);
      k.Box(new Vector3(0f, -0.005f, 0.15f), new Vector3(0.035f, 0.08f, 0.035f), darker);
      // the front: emitter cone and three claws
      Vector3 front = new Vector3(0f, 0.06f, 0.2f);
      k.Frustum(front + Vector3.forward * 0.01f, 0.055f, 0.035f, 0.05f, Quaternion.Euler(90f, 0f, 0f), darker, 20);
      k.Frustum(front + Vector3.forward * 0.045f, 0.03f, 0.018f, 0.03f, Quaternion.Euler(90f, 0f, 0f), steel, 16);
      k.Ball(front + Vector3.forward * 0.065f, 0.02f, glow, 12, true);
      foreach (float a in new[] { 0f, 120f, 240f }) Claw(k, front + Vector3.forward * 0.02f, a, 0.05f, 0.16f, 0.02f, steel, darker);
      k.GripAt(Vector3.zero).MuzzleAt(front + Vector3.forward * 0.09f);
    }

    internal static void TeleportGun(SbMeshKit k)
    {
      Color purple = C("#8a3cff"), dark = C("#1f1a2b");
      k.Box(new Vector3(0f, 0.05f, 0.05f), new Vector3(0.05f, 0.06f, 0.22f), dark);
      k.Box(new Vector3(0f, 0.0f, -0.03f), new Vector3(0.035f, 0.1f, 0.045f), Quaternion.Euler(-12f, 0f, 0f), dark);
      for (int i = 0; i < 3; i++) k.Torus(new Vector3(0f, 0.05f, 0.08f + i * 0.04f), 0.035f, 0.006f, Quaternion.Euler(90f, 0f, 0f), purple, 16, 4, true);
      k.Ball(new Vector3(0f, 0.05f, 0.18f), 0.02f, C("#d6b6ff"), 10, true);
      k.GripAt(Vector3.zero).MuzzleAt(new Vector3(0f, 0.05f, 0.2f));
    }

    internal static void BuildGun(SbMeshKit k)
    {
      Color yellow = C("#f2c230"), dark = C("#2b2d31");
      k.Box(new Vector3(0f, 0.05f, 0.04f), new Vector3(0.06f, 0.07f, 0.2f), yellow);
      k.Box(new Vector3(0f, 0.0f, -0.04f), new Vector3(0.035f, 0.1f, 0.045f), Quaternion.Euler(-12f, 0f, 0f), dark);
      k.Box(new Vector3(0f, 0.05f, 0.17f), new Vector3(0.05f, 0.05f, 0.05f), C("#8a6a3a"));        // a little block in the nose
      k.Box(new Vector3(0f, 0.095f, 0.04f), new Vector3(0.03f, 0.02f, 0.1f), dark);
      k.GripAt(Vector3.zero).MuzzleAt(new Vector3(0f, 0.05f, 0.2f));
    }

    internal static void Turret(SbMeshKit k)
    {
      Color base_ = C("#3a3f46"), body = C("#d8dadd"), dark = C("#1f2124"), red = C("#ff3a2a");
      k.Cyl(new Vector3(0f, -0.2f, 0f), 0.22f, 0.08f, Quaternion.identity, base_, 20);       // tripod plate
      for (int i = 0; i < 3; i++)
      {
        float a = i * 120f;
        k.Box(new Vector3(0f, -0.1f, 0f) + Quaternion.Euler(0f, a, 0f) * new Vector3(0f, 0f, 0.12f), new Vector3(0.03f, 0.22f, 0.03f), Quaternion.Euler(0f, a, 0f) * Quaternion.Euler(25f, 0f, 0f), dark);
      }
      k.Sphere(new Vector3(0f, 0.08f, 0f), new Vector3(0.14f, 0.12f, 0.16f), body, 18);
      k.Tube(new Vector3(-0.05f, 0.08f, 0.2f), 0.02f, 0.18f, dark);
      k.Tube(new Vector3(0.05f, 0.08f, 0.2f), 0.02f, 0.18f, dark);
      k.Ball(new Vector3(0f, 0.12f, 0.14f), 0.025f, red, 10, true);   // the eye
      k.MuzzleAt(new Vector3(0f, 0.08f, 0.32f));
    }

    internal static void Sentry(SbMeshKit k)
    {
      Color body = C("#2e3238"), light = C("#62e3ff");
      k.Sphere(Vector3.zero, new Vector3(0.12f, 0.09f, 0.14f), body, 18);
      k.Torus(Vector3.zero, 0.15f, 0.01f, Quaternion.identity, light, 24, 4, true);
      for (int i = 0; i < 4; i++)
      {
        Vector3 p = Quaternion.Euler(0f, 45f + i * 90f, 0f) * new Vector3(0f, 0.04f, 0.18f);
        k.Cyl(p, 0.05f, 0.01f, Quaternion.identity, C("#55595f"), 14);   // rotors
      }
      k.Tube(new Vector3(0f, -0.03f, 0.13f), 0.015f, 0.1f, C("#15171a"));
      k.Ball(new Vector3(0f, 0.02f, 0.12f), 0.02f, light, 10, true);
      k.MuzzleAt(new Vector3(0f, -0.03f, 0.2f));
    }
  }
}
