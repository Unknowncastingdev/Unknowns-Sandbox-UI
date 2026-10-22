using System;
using System.Collections.Generic;
using UnityEngine;

namespace UnknownsSandbox
{
  // Attachments: a thruster, wheel or balloon placed on one of your
  // objects sticks to it (it becomes a child of the object, its collider part of
  // the object's rigidbody) and the object turns into a physics object:
  //   thruster  pushes the object away from its nozzle (touch it: on / off)
  //   wheel     drives the object along the ground, and spins (touch it: on / off)
  //   balloon   lifts it: enough balloons and it flies
  // Fire from flint and steel can also stick to an object (it burns, then goes out).
  // The owner runs the physics; the others see the object move (SbNet Op.Attach
  // tells them what is stuck to what).
  internal sealed partial class SandBoxWorld
  {
    private const float ThrustG = 1.1f;        // one thruster: 1.1 x the object's weight (scaled with its size)
    private const float WheelDriveG = 0.45f;
    private const float WheelMaxSpeed = 6f;
    private const float BalloonLiftG = 0.6f;   // one balloon holds up 60 % of the object, two lift it
    private static readonly RaycastHit[] wheelHits = new RaycastHit[8];

    // ================================================================ placing
    // Turns the aim into "stick it here" when it points at one of your objects.
    internal void AdjustAttachAim(SbAim aim, SbItem item, float scale)
    {
      aim.AttachTo = null;
      if (item == null || item.Attach == SbAttach.None || item.Attach == SbAttach.Fire || !aim.Hit || aim.HitObj == null) return;
      SbObject t = aim.HitObj;
      while (t.Parent != null) t = t.Parent;
      if (!t.Mine || t.IsCell || t.T == null || t.Item.Ragdoll || t.Held || t.Item.Attach != SbAttach.None) return;
      Vector3 n = aim.HitNormal.sqrMagnitude > 1e-6f ? aim.HitNormal.normalized : Vector3.up;
      Bounds b = item.Mesh != null ? item.Mesh.bounds : new Bounds(Vector3.zero, Vector3.one * 0.3f);
      switch (item.Attach)
      {
        case SbAttach.Thruster:
          // the plate against the surface, thrust pointing into the object
          aim.Rot = Quaternion.LookRotation(-n, Mathf.Abs(n.y) > 0.9f ? t.T.forward : Vector3.up);
          aim.Pos = aim.HitPoint + n * (b.max.z * scale + 0.002f);
          break;
        case SbAttach.Wheel:
          {
            // the axle along the surface normal, rolling the way the object faces
            Vector3 f = Vector3.Cross(n, Vector3.up);
            if (f.sqrMagnitude < 1e-4f) f = Vector3.ProjectOnPlane(t.T.forward, n);
            f.Normalize();
            if (Vector3.Dot(f, t.T.forward) < 0f) f = -f;
            aim.Rot = Quaternion.LookRotation(f, Vector3.Cross(f, n));
            aim.Pos = aim.HitPoint + n * (b.extents.x * scale + 0.004f);
            break;
          }
        case SbAttach.Balloon:
          // tied on: the bottom of the string at the hit point
          aim.Rot = Quaternion.Euler(0f, t.T.eulerAngles.y, 0f);
          aim.Pos = aim.HitPoint - Vector3.up * (b.min.y * scale) + n * 0.01f;
          break;
      }
      aim.AttachTo = t;
      aim.Valid = MineCount < MaxMine;
    }

    // ================================================================ sticking
    internal void AttachTo(SbObject child, SbObject parent, bool broadcast, bool weld = false)
    {
      if (child == null || parent == null || child.T == null || parent.T == null || child == parent) return;
      if (child.Parent != null) child.Parent.Children.Remove(child);
      child.Parent = parent;
      if (!parent.Children.Contains(child)) parent.Children.Add(child);
      if (child.Rb != null) { Destroy(child.Rb); child.Rb = null; }
      child.Anchored = true;
      child.HasNet = false;
      child.T.SetParent(parent.T, true);
      child.AttachRot = child.T.localRotation;
      child.LastPos = child.T.position;
      child.Welded = weld;
      // fire just burns on it, a weld keeps the object as it was; the machines make it a physics object
      if (child.Item.Attach != SbAttach.Fire && !weld)
      {
        if (parent.Rb == null || parent.Anchored)
        {
          if (parent.Mine) MakeDynamic(parent, Vector3.zero);
          else { parent.HasNet = true; MakeDynamic(parent, Vector3.zero); }
        }
        if (parent.Rb != null)
        {
          parent.Rb.ResetCenterOfMass();
          parent.Rb.ResetInertiaTensor();
        }
      }
      ApplySolid(child);
      if (weld && parent.Rb != null) { parent.Rb.ResetCenterOfMass(); parent.Rb.ResetInertiaTensor(); }
      if (broadcast && child.Mine)
      {
        if (weld) SbNet.SendWeld(child.Id, parent.Id, child.T.localPosition, child.T.localRotation);
        else SbNet.SendAttach(child.Id, parent.Id, child.T.localPosition, child.T.localRotation, child.On);
      }
    }

    // ================================================================ welding
    // The weld tool: trigger on one of your objects, then on another, and the second
    // is welded onto the first (it becomes part of it: they move, fall and get thrown
    // as one). Trigger on two pieces that are welded together and they come apart.
    private SbObject weldFirst;
    private LineRenderer weldBox;

    private static SbObject RootOf(SbObject o) { while (o != null && o.Parent != null) o = o.Parent; return o; }

    private static bool Weldable(SbObject o)
    {
      return o != null && o.Mine && o.T != null && !o.IsCell && o.Mob == null && !o.Item.Ragdoll && o.Item.Id != "mc_fire" && !o.Held;
    }

    internal void UseWeld(Vector3 origin, Vector3 dir)
    {
      var ray = new Ray(origin, dir.normalized);
      float od; Vector3 on;
      SbObject hit = RaycastObjects(ray, 10f, out od, out on, false, false);
      RaycastHit wh;
      if (hit != null && RaycastWorld(ray, od, out wh) && wh.distance < od - 0.01f) hit = null;
      Vector3 at = hit != null ? origin + ray.direction * od : origin + ray.direction * 2f;
      if (hit == null || !Weldable(hit) || !Weldable(RootOf(hit)))
      {
        if (hit != null && !hit.Mine) Toast?.Invoke("Weld", "You can only weld your own objects");
        else if (hit != null && hit.IsCell) Toast?.Invoke("Weld", "Blocks are already fixed to the grid: weld objects");
        SbFx.Play(SbSound.UiHover, at, 0.4f, 0.5f);
        return;
      }
      SbObject root = RootOf(hit);
      if (weldFirst == null || !objects.ContainsKey(weldFirst.Key))
      {
        weldFirst = root;
        SbFx.Play(SbSound.UiClick, at, 0.6f, 1.3f);
        return;
      }
      if (root == weldFirst)
      {
        // a welded piece of the selected one: take it off; the selected one itself: deselect
        if (hit != root && hit.Welded) { Unweld(hit, true); WeldFx(at, false); }
        else SbFx.Play(SbSound.UiClose, at, 0.5f, 1.2f);
        weldFirst = null;
        return;
      }
      AttachTo(root, weldFirst, true, true);
      WeldFx(at, true);
      weldFirst = null;
    }

    private static void WeldFx(Vector3 at, bool joined)
    {
      SbFx.Burst(at, joined ? 40 : 18, new Color(1f, 0.9f, 0.5f), new Color(1f, 0.45f, 0.1f), 0.8f, 3.2f, 0.004f, 0.012f, 0.2f, 0.6f, 1.2f, true, 0.02f);
      SbFx.Burst(at, 1, new Color(0.7f, 0.85f, 1f), Color.white, 0f, 0f, 0.12f, 0.2f, 0.12f, 0.15f, 0f, true, 0.01f);
      SbFx.Play(joined ? SbSound.Sizzle : SbSound.Break, at, 0.7f, joined ? 1.4f : 1.2f);
    }

    // a welded piece comes off and is its own object again
    internal void Unweld(SbObject child, bool broadcast)
    {
      if (child == null || child.Parent == null || child.T == null) return;
      SbObject parent = child.Parent;
      bool moving = parent.Rb != null && !parent.Anchored;
      parent.Children.Remove(child);
      child.Parent = null;
      child.Welded = false;
      child.T.SetParent(objRoot, true);
      if (moving)
      {
        Vector3 v = parent.Rb != null ? parent.Rb.linearVelocity : Vector3.zero;
        MakeDynamic(child, v);
        if (!child.Mine) child.HasNet = true;
      }
      if (parent.Rb != null) { parent.Rb.ResetCenterOfMass(); parent.Rb.ResetInertiaTensor(); }
      ApplySolid(child);
      if (broadcast && child.Mine) SbNet.SendWeld(child.Id, -1, child.T.position, child.T.rotation);
    }

    internal void NetWeld(int actor, int id, int parentId, Vector3 lp, Quaternion lr)
    {
      SbObject c;
      if (!objects.TryGetValue(((long)actor << 32) | (uint)id, out c) || c.T == null) return;
      if (parentId < 0)
      {
        Unweld(c, false);
        c.T.SetPositionAndRotation(lp, lr);
        return;
      }
      SbObject p;
      if (!objects.TryGetValue(((long)actor << 32) | (uint)parentId, out p) || p.T == null) return;
      if (c.Parent != p) AttachTo(c, p, false, true);
      c.T.localPosition = Vector3.ClampMagnitude(lp, 50f);
      c.T.localRotation = lr;
      c.AttachRot = lr;
    }

    // the outline around the first object you picked
    private void UpdateWeldHighlight()
    {
      bool show = weldFirst != null && weldFirst.T != null && objects.ContainsKey(weldFirst.Key);
      if (!show) weldFirst = null;
      if (weldBox == null)
      {
        if (!show) return;
        var g = new GameObject("Unknowns Weld Selection");
        g.transform.SetParent(transform, false);
        weldBox = g.AddComponent<LineRenderer>();
        weldBox.useWorldSpace = true;
        weldBox.sharedMaterial = SbMats.Line;
        weldBox.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        weldBox.positionCount = 16;
      }
      weldBox.enabled = show;
      if (!show) return;
      Bounds b = new Bounds(weldFirst.T.position, Vector3.zero);
      bool first = true;
      foreach (Renderer r in weldFirst.Go.GetComponentsInChildren<Renderer>())
      {
        if (r is ParticleSystemRenderer || r is LineRenderer) continue;
        if (first) { b = r.bounds; first = false; } else b.Encapsulate(r.bounds);
      }
      b.Expand(0.03f);
      Vector3 mn = b.min, mx = b.max;
      Vector3[] p =
      {
        new Vector3(mn.x, mn.y, mn.z), new Vector3(mx.x, mn.y, mn.z), new Vector3(mx.x, mn.y, mx.z), new Vector3(mn.x, mn.y, mx.z), new Vector3(mn.x, mn.y, mn.z),
        new Vector3(mn.x, mx.y, mn.z), new Vector3(mx.x, mx.y, mn.z), new Vector3(mx.x, mx.y, mx.z), new Vector3(mn.x, mx.y, mx.z), new Vector3(mn.x, mx.y, mn.z),
        new Vector3(mn.x, mx.y, mx.z), new Vector3(mn.x, mn.y, mx.z), new Vector3(mx.x, mn.y, mx.z), new Vector3(mx.x, mx.y, mx.z), new Vector3(mx.x, mx.y, mn.z), new Vector3(mx.x, mn.y, mn.z)
      };
      weldBox.SetPositions(p);
      float pulse = 0.7f + 0.3f * Mathf.Sin(Time.time * 8f);
      weldBox.widthMultiplier = 0.006f;
      weldBox.startColor = weldBox.endColor = new Color(1f, 0.6f * pulse + 0.2f, 0.15f, 0.95f);
    }

    // a stuck thing being removed (or its parent being removed): unhook it
    private void DetachForRemove(SbObject o, bool broadcast)
    {
      if (o.Parent != null) { o.Parent.Children.Remove(o); o.Parent = null; }
      if (o.Children.Count == 0) return;
      var kids = new List<SbObject>(o.Children);
      o.Children.Clear();
      foreach (SbObject c in kids) { c.Parent = null; Remove(c, broadcast, false); }
    }

    internal void NetAttach(int actor, int id, int parentId, Vector3 lp, Quaternion lr, bool on)
    {
      SbObject c, p;
      if (!objects.TryGetValue(((long)actor << 32) | (uint)id, out c) || c.T == null) return;
      if (!objects.TryGetValue(((long)actor << 32) | (uint)parentId, out p) || p.T == null) return;
      if (c.Parent != p) AttachTo(c, p, false);
      c.T.localPosition = Vector3.ClampMagnitude(lp, 20f);
      c.AttachRot = lr;
      c.T.localRotation = lr;
      c.On = on;
    }

    internal IEnumerable<SbObject> MyAttachments()
    {
      foreach (SbObject o in List) if (o.Mine && o.Parent != null && o.T != null) yield return o;
    }

    // ================================================================ visuals (on Create)
    private void SetupAttachVisual(SbObject o)
    {
      if (o.Go == null) return;
      if (o.Item.Attach == SbAttach.Thruster)
      {
        o.Flame = SbFx.Burst(o.T.position, 1, Color.white, Color.white, 0f, 0f, 0f, 0f, 0.1f, 0.1f, 0f, true);
        if (o.Flame != null)
        {
          o.Flame.transform.SetParent(o.T, false);
          o.Flame.transform.localPosition = new Vector3(0f, 0f, -0.2f);
          o.Flame.transform.localRotation = Quaternion.LookRotation(Vector3.back);
          var main = o.Flame.main;
          main.loop = true;
          main.stopAction = ParticleSystemStopAction.None;
          main.simulationSpace = ParticleSystemSimulationSpace.World;
          main.startLifetime = new ParticleSystem.MinMaxCurve(0.12f, 0.25f);
          main.startSpeed = new ParticleSystem.MinMaxCurve(3f, 5f);
          main.startSize = new ParticleSystem.MinMaxCurve(0.06f * o.Scale, 0.13f * o.Scale);
          main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.9f, 0.5f), new Color(1f, 0.45f, 0.1f));
          main.gravityModifier = 0f;
          main.maxParticles = 120;
          var sh = o.Flame.shape; sh.shapeType = ParticleSystemShapeType.Cone; sh.angle = 6f; sh.radius = 0.04f * o.Scale;
          var em = o.Flame.emission; em.rateOverTime = 90f;
          o.Flame.Play();
        }
        o.Loop = o.Go.AddComponent<AudioSource>();
        o.Loop.clip = SbFx.Clip(SbSound.Flame);
        o.Loop.loop = true;
        o.Loop.spatialBlend = 1f;
        o.Loop.rolloffMode = AudioRolloffMode.Linear;
        o.Loop.minDistance = 0.6f;
        o.Loop.maxDistance = 14f;
        o.Loop.dopplerLevel = 0f;
        o.Loop.volume = 0f;
        o.Loop.pitch = 0.8f;
        if (o.Loop.clip != null) o.Loop.Play();
      }
      else if (o.Item.Id == "mc_fire")
      {
        var mr = o.Go.GetComponent<MeshRenderer>();
        if (mr != null) mr.enabled = false;   // the little ball: the Minecraft flames below are the fire
        o.Go.AddComponent<SbFireFx>();
      }
    }

    // ================================================================ every frame
    private void UpdateAttachments(float dt)
    {
      Transform lh = HandProvider != null ? HandProvider(false) : null;
      Transform rh = HandProvider != null ? HandProvider(true) : null;
      float now = Time.time;
      for (int i = List.Count - 1; i >= 0; i--)
      {
        if (i >= List.Count) continue;
        SbObject o = List[i];
        if (o.T == null || o.Parent == null) continue;
        SbAttach kind = o.Item.Attach;
        if (kind != SbAttach.Thruster && kind != SbAttach.Wheel) continue;

        // touch to switch on / off (your own)
        if (o.Mine && !o.Parent.Held && now >= o.ToggleAt)
        {
          bool l = Touching(o, lh), r = Touching(o, rh);
          if (l || r)
          {
            o.ToggleAt = now + 0.7f;
            o.On = !o.On;
            SbFx.Play(SbSound.UiClick, o.T.position, 0.6f, o.On ? 1.2f : 0.8f);
            Haptic?.Invoke(r, 0.3f, 0.04f);
            SbNet.SendAttach(o.Id, o.Parent.Id, o.T.localPosition, o.AttachRot, o.On);
          }
        }

        if (kind == SbAttach.Thruster)
        {
          if (o.Flame != null)
          {
            var em = o.Flame.emission;
            em.enabled = o.On;
          }
          if (o.Loop != null) o.Loop.volume = Mathf.MoveTowards(o.Loop.volume, o.On ? 0.35f * SbFx.Volume : 0f, dt * 2f);
        }
        else
        {
          // the wheel spins with how fast it rolls (also on the others' screens)
          Vector3 pos = o.T.position;
          Vector3 vel = dt > 1e-5f ? (pos - o.LastPos) / dt : Vector3.zero;
          o.LastPos = pos;
          Vector3 roll = o.Parent.T.rotation * (o.AttachRot * Vector3.forward);
          float radius = Mathf.Max(0.05f, 0.22f * o.Scale);
          o.SpinAngle = Mathf.Repeat(o.SpinAngle + Vector3.Dot(vel, roll) / radius * Mathf.Rad2Deg * dt, 360f);
          o.T.localRotation = o.AttachRot * Quaternion.Euler(o.SpinAngle, 0f, 0f);
        }
      }
    }

    // ================================================================ physics (FixedUpdate)
    private void ApplyAttachForces()
    {
      float g = Physics.gravity.magnitude;
      for (int i = 0; i < List.Count; i++)
      {
        SbObject o = List[i];
        SbObject p = o.Parent;
        if (p == null || !o.Mine || o.T == null || p.Rb == null || p.Rb.isKinematic || p.Held) continue;
        Rigidbody rb = p.Rb;
        // forces scale with the object's weight, so a thruster moves a crate and a car alike
        float weight = Mathf.Max(rb.mass, 1.5f) * g;
        float size = Mathf.Clamp(o.Scale, 0.25f, 4f);
        switch (o.Item.Attach)
        {
          case SbAttach.Thruster:
            if (o.On) rb.AddForceAtPosition(o.T.forward * weight * ThrustG * size, o.T.position, ForceMode.Force);
            break;
          case SbAttach.Balloon:
            rb.AddForceAtPosition(Vector3.up * weight * BalloonLiftG * size * size, o.T.position, ForceMode.Force);
            // balloons slow things down so they float rather than shoot up
            rb.AddForce(-rb.linearVelocity * 0.6f * rb.mass, ForceMode.Force);
            break;
          case SbAttach.Wheel:
            {
              float radius = Mathf.Max(0.05f, 0.22f * o.Scale);
              if (!WheelGrounded(o.T.position, radius, rb)) break;
              Vector3 roll = p.T.rotation * (o.AttachRot * Vector3.forward);
              Vector3 axle = p.T.rotation * (o.AttachRot * Vector3.right);
              Vector3 at = o.T.position;
              at.y = rb.worldCenterOfMass.y;   // pushing at the centre's height: no wheelies
              Vector3 v = rb.GetPointVelocity(o.T.position);
              if (o.On)
              {
                float speed = Vector3.Dot(v, roll);
                float push = Mathf.Clamp01(1f - speed / WheelMaxSpeed);
                rb.AddForceAtPosition(roll * weight * WheelDriveG * push, at, ForceMode.Force);
              }
              // grip: wheels don't slide sideways
              float side = Vector3.Dot(v, axle);
              rb.AddForceAtPosition(-axle * side * rb.mass * 4f, at, ForceMode.Force);
              break;
            }
        }
      }
    }

    private static bool WheelGrounded(Vector3 c, float radius, Rigidbody self)
    {
      int n = Physics.RaycastNonAlloc(c, Vector3.down, wheelHits, radius * 1.35f, ~0, QueryTriggerInteraction.Ignore);
      for (int i = 0; i < n; i++)
        if (wheelHits[i].collider != null && wheelHits[i].rigidbody != self) return true;
      return false;
    }

    // ================================================================ fire on things
    // Sets one of the objects on fire (flint and steel touched it): a Minecraft fire
    // stuck to it, sized to it, burning for a while. Touching it again keeps it burning.
    internal void IgniteObject(SbObject target, Vector3 point)
    {
      if (target == null || target.T == null) return;
      while (target.Parent != null) target = target.Parent;
      if (target.Item.Id == "mc_fire") return;
      if (target.Item.BlockFx == SbBlockFx.Tnt || target.Item.Kind == SbKind.Explosive)
      {
        if (target.Mine) Prime(target, target.Item.Kind == SbKind.Block ? 4f : target.Item.Fuse, true);
        else if (target.Owner > 0) SbNet.SendHitObject(target.Owner, target.Id, Vector3.zero, point, true);
        return;
      }
      foreach (SbObject c in target.Children)
        if (c.Item.Id == "mc_fire") { c.FuseLeft = 10f; return; }
      SbItem fire = SbCatalog.Get("mc_fire");
      if (fire == null || MineCount >= MaxMine) return;
      float size = target.IsCell ? target.BlockSize : Mathf.Clamp(target.LocalBounds.size.magnitude * target.Scale * 0.45f, 0.3f, 3f);
      Vector3 top = target.Center + Vector3.up * (target.IsCell ? target.BlockSize * 0.5f : target.LocalBounds.extents.y * target.Scale * 0.6f);
      Vector3 at = Vector3.Lerp(point, top, 0.5f);
      SbObject f = Create(0, nextId++, fire, at, Quaternion.identity, size, true, 0, Vector3Int.zero);
      if (f == null) return;
      f.FuseLeft = 10f;
      SbNet.QueueSpawn(f);
      SbFx.PlayMc("ignite", SbSound.Fuse, point, 1f, 1f);
      // cells can't hold children (their objects never move): fire on a block just sits on it
      if (!target.IsCell && target.Mine) AttachTo(f, target, true);
    }
  }

  // Minecraft fire: two crossed quads of the fire texture that flicker, flip and
  // sway (a lit fire also has the SandBox flame particles and light).
  internal sealed class SbFireFx : MonoBehaviour
  {
    private Transform[] quads;
    private float t, flipAt;
    private bool flip;
    private static Mesh quadMesh, quadMeshFlip;

    private static Mesh Quad(bool mirrored)
    {
      Rect r = SbBlockArt.TileRect(SbBlockArt.Fire);
      var m = new Mesh { name = mirrored ? "Unknowns Fire (flip)" : "Unknowns Fire", hideFlags = HideFlags.DontSave };
      m.vertices = new[] { new Vector3(-0.5f, 0f, 0f), new Vector3(0.5f, 0f, 0f), new Vector3(0.5f, 1f, 0f), new Vector3(-0.5f, 1f, 0f) };
      float a = mirrored ? r.xMax : r.xMin, b = mirrored ? r.xMin : r.xMax;
      m.uv = new[] { new Vector2(a, r.yMin), new Vector2(b, r.yMin), new Vector2(b, r.yMax), new Vector2(a, r.yMax) };
      m.normals = new[] { Vector3.back, Vector3.back, Vector3.back, Vector3.back };
      m.triangles = new[] { 0, 2, 1, 0, 3, 2 };
      m.RecalculateBounds();
      return m;
    }

    private void Start()
    {
      if (quadMesh == null) quadMesh = Quad(false);
      if (quadMeshFlip == null) quadMeshFlip = Quad(true);
      quads = new Transform[2];
      for (int i = 0; i < 2; i++)
      {
        var go = new GameObject("fire " + i);
        go.transform.SetParent(transform, false);
        go.transform.localPosition = new Vector3(0f, -0.25f, 0f);
        go.transform.localRotation = Quaternion.Euler(0f, 45f + i * 90f, 0f);
        go.AddComponent<MeshFilter>().sharedMesh = quadMesh;
        var mr = go.AddComponent<MeshRenderer>();
        mr.sharedMaterial = SbMats.BlockClear;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        quads[i] = go.transform;
      }
      t = UnityEngine.Random.value * 10f;
    }

    private void Update()
    {
      if (quads == null) return;
      t += Time.deltaTime;
      if (t >= flipAt)
      {
        flipAt = t + UnityEngine.Random.Range(0.08f, 0.16f);
        flip = !flip;
        foreach (Transform q in quads) if (q != null) q.GetComponent<MeshFilter>().sharedMesh = flip ? quadMeshFlip : quadMesh;
      }
      for (int i = 0; i < quads.Length; i++)
      {
        if (quads[i] == null) continue;
        float h = 0.5f * (0.9f + 0.15f * Mathf.Sin(t * 13f + i * 2f) + 0.05f * Mathf.Sin(t * 29f));
        quads[i].localScale = new Vector3(0.5f * (1f + 0.04f * Mathf.Sin(t * 17f + i)), h, 1f);
        quads[i].localRotation = Quaternion.Euler(Mathf.Sin(t * 3f + i) * 4f, 45f + i * 90f, 0f);
      }
    }
  }
}
