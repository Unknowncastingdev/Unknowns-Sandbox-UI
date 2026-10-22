using UnityEngine;

namespace UnknownsSandbox
{
  // what the newer mobs can do (owner only, like the rest of the brain):
  // fly (bees, parrots, bats, ghasts, phantoms, vexes, the Wither), swim (squid, cod,
  // axolotls, turtles), get angry when hit (bees, polar bears, goats), shoot (skeletons,
  // pillagers: arrows; blazes: three fireballs; ghasts, the Wither, the dragon), and the
  // Ender Dragon's flight: circling the island, strafing you with fireballs and charging
  // at you, healed by the End crystals.
  internal sealed partial class SbMob
  {
    private bool angry;
    private float shotAt, burstAt, retreatUntil, dragonPhaseAt, dragonAngle;
    private int burstLeft, dragonPhase;
    private LineRenderer healBeam;

    private bool Angry { get { return def.Hostile || angry; } }

    internal float HealthFraction { get { return def.Hp > 0 ? Mathf.Clamp01(hp / def.Hp) : 0f; } }

    private bool BrainSpecial(Rigidbody rb, float scale, float now, Vector3 pos)
    {
      if (def.NoAi)
      {
        rb.useGravity = false;
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        return true;
      }
      if (def.Type == SbMobType.EnderDragon) { DragonBrain(rb, now, pos); return true; }
      if (def.Flies) { FlyBrain(rb, scale, now, pos); return true; }
      if (def.Swims)
      {
        if (world.WaterAt(pos + Vector3.up * 0.15f * scale)) { SwimBrain(rb, scale, now, pos); return true; }
        rb.useGravity = true;
        // fish and squid flop about on land
        if (def.Type == SbMobType.Cod || def.Type == SbMobType.Squid)
        {
          bool grounded = Physics.Raycast(pos + Vector3.up * 0.1f * scale, Vector3.down, 0.25f * scale, ~0, QueryTriggerInteraction.Ignore);
          if (grounded && now >= hopAt)
          {
            hopAt = now + Random.Range(0.6f, 1.4f);
            rb.linearVelocity = new Vector3(Random.Range(-1f, 1f), 2.5f, Random.Range(-1f, 1f));
            rb.angularVelocity = Random.insideUnitSphere * 4f;
          }
          return true;
        }
      }
      return false;
    }

    // ---------------------------------------------------------------- flying
    private void FlyBrain(Rigidbody rb, float scale, float now, Vector3 pos)
    {
      rb.useGravity = false;
      float dt = Time.fixedDeltaTime;
      Vector3 player;
      bool near = world.NearestPlayer(pos, Angry ? 30f : 5f, out player);
      chasing = Angry && near && now >= retreatUntil;
      Vector3 want;
      if (chasing)
      {
        if (def.KeepAway > 0f)
        {
          // hang back and shoot
          Vector3 away = Vector3.ProjectOnPlane(pos - player, Vector3.up);
          if (away.sqrMagnitude < 0.01f) away = transform.forward;
          want = player + away.normalized * def.KeepAway + Vector3.up * def.FlyHeight * 0.6f;
        }
        else
        {
          want = player + Vector3.down * 0.25f;
          // a phantom / vex / bee that reached you pulls up and comes round again
          if ((player - pos).sqrMagnitude < 1.2f * 1.2f)
          {
            retreatUntil = now + (def.Type == SbMobType.Phantom ? 3f : 1.5f);
            target = pos + Vector3.up * def.FlyHeight + Random.insideUnitSphere * 3f;
          }
        }
      }
      else
      {
        if (now >= decideAt || (target - pos).sqrMagnitude < 0.5f)
        {
          decideAt = now + Random.Range(2f, 6f);
          float ground = pos.y - 3f;
          RaycastHit hit;
          if (Physics.Raycast(pos + Vector3.up * 0.5f, Vector3.down, out hit, 40f, ~0, QueryTriggerInteraction.Ignore)) ground = hit.point.y;
          float a = Random.value * Mathf.PI * 2f, r = Random.Range(2f, 8f);
          target = new Vector3(pos.x + Mathf.Cos(a) * r, ground + def.FlyHeight * Random.Range(0.6f, 1.4f), pos.z + Mathf.Sin(a) * r);
        }
        want = target;
      }
      float speed = def.Speed * (chasing ? 1.4f : 1f);
      Vector3 v = Vector3.ClampMagnitude((want - pos) * 1.5f, speed);
      if (def.Type == SbMobType.Bat) v += Random.insideUnitSphere * 2.5f;
      rb.linearVelocity = Vector3.MoveTowards(rb.linearVelocity, v, 6f * dt);
      Vector3 face = chasing ? player - pos : rb.linearVelocity;
      face = Vector3.ProjectOnPlane(face, Vector3.up);
      if (face.sqrMagnitude > 0.01f) rb.MoveRotation(Quaternion.Slerp(rb.rotation, Quaternion.LookRotation(face.normalized, Vector3.up), 1f - Mathf.Exp(-5f * dt)));
      rb.angularVelocity = Vector3.zero;
      MobRanged(player, chasing, now, pos);
    }

    // ---------------------------------------------------------------- swimming
    private void SwimBrain(Rigidbody rb, float scale, float now, Vector3 pos)
    {
      rb.useGravity = false;
      float dt = Time.fixedDeltaTime;
      if (now >= decideAt || (target - pos).sqrMagnitude < 0.2f)
      {
        decideAt = now + Random.Range(2f, 5f);
        Vector3 t = pos + Random.insideUnitSphere * 3f * scale;
        target = world.WaterAt(t) ? t : pos;
      }
      Vector3 v = Vector3.ClampMagnitude(target - pos, def.Speed * 0.7f);
      rb.linearVelocity = Vector3.MoveTowards(rb.linearVelocity, v, 3f * dt);
      Vector3 face = Vector3.ProjectOnPlane(rb.linearVelocity, Vector3.up);
      if (face.sqrMagnitude > 0.01f) rb.MoveRotation(Quaternion.Slerp(rb.rotation, Quaternion.LookRotation(face.normalized, Vector3.up), 1f - Mathf.Exp(-3f * dt)));
    }

    // ---------------------------------------------------------------- shooting
    private void MobRanged(Vector3 player, bool chasingNow, float now, Vector3 pos)
    {
      if (!def.Shoots || !chasingNow || def.Shot == SbAmmo.Bullet) return;
      float dist = (player - pos).magnitude;
      if (dist > def.ShotRange) return;
      if (burstLeft > 0)
      {
        if (now < burstAt) return;
        burstLeft--;
        burstAt = now + 0.3f;
        FireAt(player, dist);
        return;
      }
      if (now < shotAt) return;
      shotAt = now + def.ShotEvery * Random.Range(0.8f, 1.2f);
      if (def.Shot == SbAmmo.Fireball) { burstLeft = 2; burstAt = now + 0.3f; }
      FireAt(player, dist);
    }

    private Vector3 Eye()
    {
      System.Collections.Generic.List<Transform> heads;
      if (parts.TryGetValue(SbMobDefs.Role.Head, out heads) && heads.Count > 0 && heads[0] != null) return heads[0].position;
      return obj.Center + Vector3.up * 0.3f * Mathf.Max(0.25f, obj.Scale);
    }

    private void FireAt(Vector3 player, float dist)
    {
      Vector3 eye = Eye();
      Vector3 aim = player + Vector3.down * 0.35f - eye;
      // arrows fly in an arc
      if (def.Shot == SbAmmo.Arrow) aim.y += aim.magnitude * 0.12f;
      aim += Random.insideUnitSphere * 0.04f * aim.magnitude;
      world.MobShoot(def.Shot, eye + aim.normalized * 0.4f, aim.normalized);
    }

    // ---------------------------------------------------------------- the Ender Dragon
    private void DragonBrain(Rigidbody rb, float now, Vector3 pos)
    {
      rb.useGravity = false;
      float dt = Time.fixedDeltaTime;
      Vector3 centre = world.DragonCentre;
      Vector3 player;
      bool near = world.NearestPlayer(pos, 140f, out player);
      if (now >= dragonPhaseAt)
      {
        dragonPhase = dragonPhase != 0 || !near ? 0 : (Random.value < 0.55f ? 1 : 2);
        dragonPhaseAt = now + (dragonPhase == 0 ? Random.Range(8f, 14f) : dragonPhase == 1 ? 6f : 5f);
        if (dragonPhase != 0) SbFx.PlayMc("enderdragon_growl", SbSound.Suck, pos, 1f, 1f, 120f);
      }
      Vector3 want;
      float speed = def.Speed;
      if (dragonPhase == 1 && near)
      {
        // strafe: hang off to one side of you and spit fireballs
        Vector3 side = Vector3.ProjectOnPlane(pos - player, Vector3.up);
        if (side.sqrMagnitude < 0.01f) side = Vector3.forward;
        want = player + side.normalized * 18f + Vector3.up * 10f;
        if (now >= shotAt)
        {
          shotAt = now + 2.2f;
          Vector3 eye = Eye();
          world.MobShoot(SbAmmo.DragonBall, eye, (player - eye).normalized);
        }
      }
      else if (dragonPhase == 2 && near)
      {
        // charge straight at you
        want = player;
        speed = def.Speed * 1.7f;
        if ((Eye() - player).sqrMagnitude < 5f * 5f)
        {
          world.KnockLocalPlayerAt(player, Eye(), 4f, 0.7f);
          dragonPhase = 0;
          dragonPhaseAt = now + Random.Range(8f, 12f);
        }
      }
      else
      {
        // circle the island
        dragonAngle += dt * 0.32f;
        want = centre + new Vector3(Mathf.Cos(dragonAngle) * 26f, 16f + Mathf.Sin(dragonAngle * 2.3f) * 4f, Mathf.Sin(dragonAngle) * 26f);
      }
      Vector3 to = want - pos;
      rb.linearVelocity = Vector3.MoveTowards(rb.linearVelocity, to.sqrMagnitude > 1f ? to.normalized * speed : to, 9f * dt);
      Vector3 face = rb.linearVelocity.sqrMagnitude > 0.5f ? rb.linearVelocity : transform.forward;
      rb.MoveRotation(Quaternion.Slerp(rb.rotation, Quaternion.LookRotation(face.normalized, Vector3.up), 1f - Mathf.Exp(-2.5f * dt)));
      rb.angularVelocity = Vector3.zero;

      // the crystals heal it (a beam from the nearest)
      Vector3 crystal;
      if (world.NearestCrystal(pos, 70f, out crystal))
      {
        hp = Mathf.Min(def.Hp, hp + 1.5f * dt);
        if (healBeam == null)
        {
          var g = new GameObject("heal beam");
          g.transform.SetParent(transform, false);
          healBeam = g.AddComponent<LineRenderer>();
          healBeam.useWorldSpace = true;
          healBeam.positionCount = 2;
          healBeam.startWidth = 0.25f; healBeam.endWidth = 0.15f;
          healBeam.sharedMaterial = new Material(SbMats.Line) { hideFlags = HideFlags.DontSave, color = new Color(1f, 0.55f, 0.95f, 0.9f) };
          healBeam.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }
        healBeam.enabled = true;
        healBeam.SetPosition(0, crystal);
        healBeam.SetPosition(1, obj.Center);
      }
      else if (healBeam != null) healBeam.enabled = false;
    }

    // ---------------------------------------------------------------- everyone: wings and the boss bar
    private void BodyExtras()
    {
      if (def.Flies)
      {
        float rate = def.Type == SbMobType.Bee ? 50f : def.Type == SbMobType.EnderDragon ? 2.2f : def.Type == SbMobType.Ghast || def.Type == SbMobType.Wither ? 0f : 14f;
        float amp = def.Type == SbMobType.Bee ? 30f : def.Type == SbMobType.EnderDragon ? 28f : 45f;
        float flap = rate > 0f ? Mathf.Sin(Time.time * rate) * amp : 0f;
        AnimNow(SbMobDefs.Role.WingL, Quaternion.Euler(0f, 0f, -flap));
        AnimNow(SbMobDefs.Role.WingR, Quaternion.Euler(0f, 0f, flap));
        if (def.Type == SbMobType.Ghast)
        {
          float sway = Mathf.Sin(Time.time * 1.5f) * 10f;
          AnimNow(SbMobDefs.Role.LegL, Quaternion.Euler(sway, 0f, 0f));
          AnimNow(SbMobDefs.Role.LegR, Quaternion.Euler(-sway, 0f, 0f));
        }
      }
      if (def.Boss && Simulated) world.ReportBoss(def.Name, HealthFraction, transform.position);
    }

    private void AnimNow(SbMobDefs.Role role, Quaternion q)
    {
      System.Collections.Generic.List<Transform> l;
      if (!parts.TryGetValue(role, out l)) return;
      foreach (Transform t in l)
      {
        if (t == null) continue;
        Quaternion b;
        if (!baseRot.TryGetValue(t, out b)) b = Quaternion.identity;
        t.localRotation = b * q;
      }
    }
  }
}
