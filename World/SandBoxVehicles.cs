using System;
using System.Collections.Generic;
using GorillaLocomotion;
using UnityEngine;

namespace UnknownsSandbox
{
  // Drivable cars: a go-kart and an off-road buggy with real wheel physics
  // (WheelColliders), spinning wheels and an engine sound. Grip the steering wheel
  // (or the seat) of one of your cars to get in; the LEFT STICK drives (up / down =
  // gas / reverse, left / right = steer), B or Y gets you out. You ride along with
  // it. Others see the car move with you in it. Carrying you around only works where
  // solid objects do (modded lobbies, all Unknowns, offline).
  internal sealed class SbVehicleSpec
  {
    public Vector3[] Wheels;          // local wheel centres: front left, front right, rear left, rear right
    public float WheelRadius = 0.2f;
    public Vector3 Seat;              // where your body sits
    public Vector3 Steer;             // the steering wheel (grip here to get in)
    public float Mass = 160f;
    public float Motor = 260f;        // torque per rear wheel (N m)
    public float MaxSteer = 30f;
    public float TopSpeed = 12f;
    public float Suspension = 0.12f;  // how far each wheel can travel
    public bool Climber;              // climbs steps and kerbs, rights itself, sticks to slopes (the obby car)
    public bool Boat, Cart;           // floats on water / rolls along rails (SandBoxItems.cs)
    public Action<SbMeshKit> Body;    // the car without its wheels (they are separate, so they spin)
    public Action<SbMeshKit, float> Wheel;
    internal Mesh BodyMesh, WheelMesh;
  }

  internal static class SbVehicleArt
  {
    private static Color C(string h) { return SbCatalog.C(h); }

    // a round bar from a to b
    internal static void Bar(SbMeshKit k, Vector3 a, Vector3 b, float r, Color col, int seg = 10)
    {
      Vector3 d = b - a;
      if (d.sqrMagnitude < 1e-8f) return;
      k.Cyl((a + b) * 0.5f, r, d.magnitude, Quaternion.FromToRotation(Vector3.up, d), col, seg);
    }

    // a tyre with a rim and hub, axle along X
    internal static void Wheel(SbMeshKit k, Vector3 at, float r, float width, Color rim, bool knobby)
    {
      Quaternion side = Quaternion.Euler(0f, 0f, 90f);
      float thick = r * 0.32f;
      k.Torus(at, r - thick, thick, side, C("#1b1b1d"), 28, 10);
      k.Cyl(at, (r - thick) * 1.02f, width * 0.85f, side, C("#1b1b1d"), 24);
      if (knobby)
        for (int i = 0; i < 18; i++)
        {
          float a = i / 18f * Mathf.PI * 2f;
          k.Box(at + new Vector3(0f, Mathf.Cos(a), Mathf.Sin(a)) * (r * 0.97f), new Vector3(width * 0.9f, r * 0.12f, r * 0.16f), Quaternion.Euler(a * Mathf.Rad2Deg, 0f, 0f), C("#151517"));
        }
      k.Cyl(at, (r - thick) * 0.85f, width * 0.9f, side, rim, 20);
      k.Cyl(at, r * 0.18f, width, side, C("#3a3f46"), 12);
      for (int i = 0; i < 5; i++)
        k.Box(at, new Vector3(width * 0.95f, (r - thick) * 1.5f, r * 0.08f), Quaternion.Euler(i * 36f, 0f, 0f), C("#3a3f46"));
    }

    // the Roblox obby car, like the one in "Obby But You're In a Car": a pink
    // pickup truck with a boxy cab, an open bed you ride in, tall red tail lights and
    // black tyres on white rims. Big wheels, long suspension and a climb assist
    // (SbVehicle) take it over almost anything.
    internal static SbVehicleSpec ObbyCar()
    {
      var s = new SbVehicleSpec
      {
        Wheels = new[] { new Vector3(-0.72f, 0.38f, 0.82f), new Vector3(0.72f, 0.38f, 0.82f), new Vector3(-0.72f, 0.38f, -0.88f), new Vector3(0.72f, 0.38f, -0.88f) },
        WheelRadius = 0.38f, Seat = new Vector3(0f, 1.0f, -0.78f), Steer = new Vector3(0f, 1.2f, -0.32f), Mass = 260f, Motor = 900f, MaxSteer = 34f, TopSpeed = 15f,
        Suspension = 0.34f, Climber = true
      };
      Color pink = C("#f5b4d8"), pinkDark = C("#e396c0"), glass = C("#2a2d3a"), black = C("#1d1d22"), red = C("#e0242c"), white = C("#f4f4f4"), bed = C("#3a3a42");
      s.Body = k =>
      {
        // the lower body, the whole length
        k.Box(new Vector3(0f, 0.74f, 0f), new Vector3(1.36f, 0.42f, 2.7f), pink);
        // the hood and front
        k.Box(new Vector3(0f, 1.0f, 0.86f), new Vector3(1.36f, 0.1f, 0.98f), pink);
        k.Box(new Vector3(0f, 0.72f, 1.36f), new Vector3(1.3f, 0.3f, 0.04f), pinkDark);   // the bumper
        // the cab: a box with dark windows all round
        k.Box(new Vector3(0f, 1.3f, 0.06f), new Vector3(1.3f, 0.6f, 0.62f), pink);
        k.Box(new Vector3(0f, 1.62f, 0.06f), new Vector3(1.34f, 0.06f, 0.68f), pinkDark);   // the roof
        k.Box(new Vector3(0f, 1.34f, 0.375f), new Vector3(1.12f, 0.36f, 0.02f), glass);     // windscreen
        k.Box(new Vector3(0f, 1.36f, -0.255f), new Vector3(1.0f, 0.26f, 0.02f), glass);     // back window
        k.Box(new Vector3(-0.655f, 1.36f, 0.06f), new Vector3(0.02f, 0.3f, 0.48f), glass);  // side windows
        k.Box(new Vector3(0.655f, 1.36f, 0.06f), new Vector3(0.02f, 0.3f, 0.48f), glass);
        // the open bed: a dark floor, pink walls and the tailgate
        k.Box(new Vector3(0f, 0.96f, -0.82f), new Vector3(1.16f, 0.02f, 1.0f), bed);
        k.Box(new Vector3(-0.63f, 1.12f, -0.82f), new Vector3(0.1f, 0.34f, 1.04f), pink);
        k.Box(new Vector3(0.63f, 1.12f, -0.82f), new Vector3(0.1f, 0.34f, 1.04f), pink);
        k.Box(new Vector3(0f, 1.12f, -1.3f), new Vector3(1.36f, 0.34f, 0.1f), pink);
        k.Box(new Vector3(-0.63f, 1.3f, -0.82f), new Vector3(0.12f, 0.03f, 1.04f), pinkDark);   // rail caps
        k.Box(new Vector3(0.63f, 1.3f, -0.82f), new Vector3(0.12f, 0.03f, 1.04f), pinkDark);
        // tall red tail lights on the back corners, white headlights
        k.Box(new Vector3(-0.6f, 1.0f, -1.36f), new Vector3(0.12f, 0.46f, 0.02f), red, true);
        k.Box(new Vector3(0.6f, 1.0f, -1.36f), new Vector3(0.12f, 0.46f, 0.02f), red, true);
        k.Box(new Vector3(-0.46f, 0.88f, 1.36f), new Vector3(0.24f, 0.12f, 0.02f), white, true);
        k.Box(new Vector3(0.46f, 0.88f, 1.36f), new Vector3(0.24f, 0.12f, 0.02f), white, true);
        // black wheel arches
        foreach (Vector3 w in new[] { new Vector3(-0.69f, 0.38f, 0.82f), new Vector3(0.69f, 0.38f, 0.82f), new Vector3(-0.69f, 0.38f, -0.88f), new Vector3(0.69f, 0.38f, -0.88f) })
          k.Box(w + new Vector3(0f, 0.34f, 0f), new Vector3(0.04f, 0.1f, 0.9f), black);
        // a steering wheel on the cab's back wall, for whoever rides in the bed
        SbVehicleArt.Bar(k, new Vector3(0f, 1.2f, -0.26f), new Vector3(0f, 1.2f, -0.32f), 0.03f, black);
        k.Torus(new Vector3(0f, 1.2f, -0.34f), 0.15f, 0.025f, Quaternion.Euler(90f, 0f, 0f), black, 24, 8);
        // axles
        SbVehicleArt.Bar(k, new Vector3(-0.72f, 0.38f, 0.82f), new Vector3(0.72f, 0.38f, 0.82f), 0.04f, black);
        SbVehicleArt.Bar(k, new Vector3(-0.72f, 0.38f, -0.88f), new Vector3(0.72f, 0.38f, -0.88f), 0.04f, black);
      };
      s.Wheel = (k, r) => ObbyWheel(k, Vector3.zero, r);
      return s;
    }

    // the obby car's wheel: a fat black tyre on a white rim
    internal static void ObbyWheel(SbMeshKit k, Vector3 at, float r)
    {
      Quaternion side = Quaternion.Euler(0f, 0f, 90f);
      k.Cyl(at, r, 0.28f, side, C("#1d1d22"), 24);
      k.Cyl(at, r * 0.62f, 0.3f, side, C("#f4f4f4"), 20);
      k.Cyl(at, r * 0.2f, 0.32f, side, C("#c9c9cf"), 10);
    }

    // the whole car (menu icon and preview): the body with its wheels on
    internal static void Full(SbMeshKit k, SbVehicleSpec s)
    {
      s.Body(k);
      foreach (Vector3 w in s.Wheels)
      {
        if (s.Climber) ObbyWheel(k, w, s.WheelRadius);
        else SbVehicleArt.Wheel(k, w, s.WheelRadius, s.WheelRadius > 0.25f ? 0.24f : 0.14f, C("#c9ced6"), s.WheelRadius > 0.25f);
      }
    }
  }

  internal sealed partial class SandBoxWorld
  {
    internal SbObject Driving { get; private set; }
    private bool exitWas;

    private void SetupVehicle(SbObject o)
    {
      SbVehicleSpec spec = o.Item.Vehicle;
      if (spec == null || o.Go == null) return;
      if (spec.BodyMesh == null) { var k = new SbMeshKit(); spec.Body(k); spec.BodyMesh = k.ToMesh("Unknowns " + o.Item.Name + " body"); }
      if (spec.WheelMesh == null && spec.Wheel != null) { var k = new SbMeshKit(); spec.Wheel(k, spec.WheelRadius); spec.WheelMesh = k.ToMesh("Unknowns wheel"); }
      var mf = o.Go.GetComponent<MeshFilter>();
      if (mf != null) mf.sharedMesh = spec.BodyMesh;
      // the body collider sits above the wheels (they hold the car up)
      foreach (Collider c in o.Cols)
      {
        var bc = c as BoxCollider;
        if (bc == null) continue;
        Bounds b = spec.BodyMesh.bounds;
        float bottom = spec.Wheels.Length > 0 ? spec.Wheels[0].y + spec.WheelRadius * 0.15f : b.min.y;
        bc.center = new Vector3(b.center.x, (Mathf.Max(b.min.y, bottom) + b.max.y) * 0.5f, b.center.z);
        bc.size = new Vector3(b.size.x * 0.92f, b.max.y - Mathf.Max(b.min.y, bottom), b.size.z);
      }
      if (o.Anchored) MakeDynamic(o, Vector3.zero);
      var v = o.Go.AddComponent<SbVehicle>();
      v.Init(this, o, spec);
    }

    // grip near the steering wheel or seat of one of your cars: in
    private bool TryEnterVehicle(Transform hand)
    {
      if (Driving != null || hand == null) return false;
      foreach (SbObject o in List)
      {
        if (o.Item.Vehicle == null || o.T == null || o.Held) continue;
        SbVehicleSpec s = o.Item.Vehicle;
        float reach = 0.3f * Mathf.Max(0.6f, o.Scale);
        bool near = Vector3.Distance(hand.position, o.T.TransformPoint(s.Steer)) < reach ||
                    Vector3.Distance(hand.position, o.T.TransformPoint(s.Seat + Vector3.up * 0.2f)) < reach * 1.3f;
        if (!near) continue;
        if (!o.Mine) { Toast?.Invoke("Car", "That car belongs to another player"); return true; }
        if (!SolidAllowed) { Toast?.Invoke("Car", "Driving works in modded lobbies, or when everyone in the room has the SandBox"); return true; }
        Driving = o;
        lastSeatYawSet = false;
        SetSeated(o, true);
        try
        {
          GTPlayer p = GTPlayer.Instance;
          if (p != null) p.TeleportTo(o.T.TransformPoint(s.Seat) + o.T.up * 0.32f * Mathf.Max(0.6f, o.Scale), Quaternion.LookRotation(Vector3.ProjectOnPlane(o.T.forward, Vector3.up).normalized + Vector3.forward * 1e-4f), false, true);
        }
        catch { }
        exitWas = true;
        Haptic?.Invoke(true, 0.4f, 0.08f); Haptic?.Invoke(false, 0.4f, 0.08f);
        SbFx.Play(SbSound.UiOpen, o.T.position, 0.6f, 0.8f);
        Toast?.Invoke("Car", "LEFT STICK: drive and steer   |   B / Y: get out");
        return true;
      }
      return false;
    }

    internal void ExitVehicle()
    {
      if (Driving == null) return;
      SbObject car = Driving;
      Driving = null;
      SetSeated(car, false);
      try
      {
        GTPlayer p = GTPlayer.Instance;
        if (p != null && p.playerRigidBody != null && car.T != null)
        {
          // out to the side of the car, a little hop
          p.TeleportTo(car.T.position + car.T.right * (car.LocalBounds.extents.x * car.Scale + 0.6f) + Vector3.up * 0.9f, p.transform.rotation, false, true);
          p.playerRigidBody.linearVelocity = (car.Rb != null ? car.Rb.linearVelocity : Vector3.zero) + Vector3.up * 1.5f;
        }
      }
      catch { }
      SbFx.Play(SbSound.UiClose, car.T != null ? car.T.position : Vector3.zero, 0.5f, 0.8f);
    }

    // in the seat, Gorilla Tag's hand movement is off (so hands touching the car or the
    // ground can't shove you out) and the car doesn't collide with you
    private void SetSeated(SbObject car, bool seated)
    {
      try { GTPlayer p = GTPlayer.Instance; if (p != null) p.disableMovement = seated; } catch { }
      if (car == null || car.T == null) return;
      if (seated)
      {
        EnsureLayers();
        int ghost = ghostLayer >= 0 ? ghostLayer : 2;
        foreach (Collider c in car.Go.GetComponentsInChildren<Collider>())
          if (!(c is WheelCollider) && c.gameObject.layer != ghost) c.gameObject.layer = ghost;
      }
      else ApplySolid(car);
    }

    // every frame: the driver's controls; out when B / Y is pressed or the car is gone
    private void UpdateDriving()
    {
      if (Driving == null) return;
      if (Driving.T == null || !objects.ContainsKey(Driving.Key) || !SolidAllowed) { ExitVehicle(); return; }
      bool exit = false;
      try { var ip = ControllerInputPoller.instance; exit = ip != null && (ip.rightControllerSecondaryButton || ip.leftControllerSecondaryButton); } catch { }
      if (exit && !exitWas) { ExitVehicle(); exitWas = exit; return; }
      exitWas = exit;
      // keep it so (the game, or the solid / see-through switch, may change it back)
      SetSeated(Driving, true);
      try
      {
        GTPlayer p = GTPlayer.Instance;
        if (p == null) return;
        // turn with the car
        float yaw = Driving.T.eulerAngles.y;
        float d = Mathf.DeltaAngle(lastSeatYaw, yaw);
        if (lastSeatYawSet && Mathf.Abs(d) > 0.01f && Mathf.Abs(d) < 45f) p.Turn(d);
        lastSeatYaw = yaw; lastSeatYawSet = true;
        if ((p.bodyCollider.transform.position - Driving.T.TransformPoint(Driving.Item.Vehicle.Seat)).sqrMagnitude > 9f) ExitVehicle();
      }
      catch { }
    }
    private float lastSeatYaw;
    private bool lastSeatYawSet;

    // FixedUpdate: drive the car and carry you in its seat
    private void ApplyDriving()
    {
      if (Driving == null || Driving.T == null || Driving.Rb == null) return;
      SbVehicle v = Driving.Go != null ? Driving.Go.GetComponent<SbVehicle>() : null;
      Vector2 stick = Vector2.zero;
      try { var ip = ControllerInputPoller.instance; if (ip != null) stick = ip.leftControllerPrimary2DAxis; } catch { }
      if (v != null) v.Drive(Mathf.Abs(stick.y) > 0.12f ? stick.y : 0f, Mathf.Abs(stick.x) > 0.12f ? stick.x : 0f);
      try
      {
        GTPlayer p = GTPlayer.Instance;
        Rigidbody prb = p != null ? p.playerRigidBody : null;
        if (prb == null) return;
        Vector3 seat = Driving.T.TransformPoint(Driving.Item.Vehicle.Seat) + Driving.T.up * 0.32f * Mathf.Max(0.6f, Driving.Scale);
        Vector3 body = p.bodyCollider.transform.position;
        prb.linearVelocity = Driving.Rb.GetPointVelocity(seat) + Vector3.ClampMagnitude((seat - body) * 12f, 8f);
      }
      catch { }
    }
  }

  // ==================================================================== one car
  internal sealed partial class SbVehicle : MonoBehaviour
  {
    private SandBoxWorld world;
    private SbObject obj;
    private SbVehicleSpec spec;
    private WheelCollider[] wheels;
    private Transform[] visuals;
    private float[] spin;
    private float throttle, steer, steerNow;
    private AudioSource engine;
    private Vector3 lastPos;

    internal void Init(SandBoxWorld w, SbObject o, SbVehicleSpec s)
    {
      world = w; obj = o; spec = s;
      int n = s.Wheels.Length;
      visuals = new Transform[n];
      spin = new float[n];
      for (int i = 0; i < n; i++)
      {
        var g = new GameObject("wheel " + i);
        g.transform.SetParent(transform, false);
        g.transform.localPosition = s.Wheels[i];
        g.AddComponent<MeshFilter>().sharedMesh = s.WheelMesh;
        g.AddComponent<MeshRenderer>().sharedMaterials = SbMats.For(o.Item);
        visuals[i] = g.transform;
      }
      if (o.Rb != null)
      {
        o.Rb.mass = s.Mass * Mathf.Pow(Mathf.Clamp(o.Scale, 0.5f, 2f), 3f);
        o.Rb.centerOfMass = new Vector3(0f, s.Wheels.Length > 0 ? s.Wheels[0].y : s.Seat.y * 0.5f, 0f);
        o.Rb.linearDamping = 0.05f;
        o.Rb.angularDamping = 0.6f;
      }
      if (o.Mine)
      {
        // real wheels: suspension, grip, motor
        wheels = new WheelCollider[n];
        for (int i = 0; i < n; i++)
        {
          var g = new GameObject("wheel collider " + i);
          g.transform.SetParent(transform, false);
          g.transform.localPosition = s.Wheels[i] + Vector3.up * 0.08f;
          var wc = g.AddComponent<WheelCollider>();
          wc.radius = s.WheelRadius;
          wc.suspensionDistance = s.Suspension;
          wc.mass = s.Climber ? 14f : 8f;
          wc.wheelDampingRate = 0.6f;
          var spring = wc.suspensionSpring;
          spring.spring = s.Mass * (s.Climber ? 70f : 90f); spring.damper = s.Mass * (s.Climber ? 11f : 9f); spring.targetPosition = 0.45f;
          wc.suspensionSpring = spring;
          var fwd = wc.forwardFriction; fwd.stiffness = s.Climber ? 2.6f : 1.6f; wc.forwardFriction = fwd;
          var side = wc.sidewaysFriction; side.stiffness = s.Climber ? 2.2f : 1.8f; wc.sidewaysFriction = side;
          wheels[i] = wc;
        }
      }
      engine = gameObject.AddComponent<AudioSource>();
      engine.clip = SbFx.Clip(SbSound.Minigun);
      engine.loop = true;
      engine.spatialBlend = 1f;
      engine.rolloffMode = AudioRolloffMode.Linear;
      engine.minDistance = 1f; engine.maxDistance = 20f;
      engine.dopplerLevel = 0f;
      engine.volume = 0f;
      if (engine.clip != null) engine.Play();
      lastPos = transform.position;
    }

    internal void Drive(float gas, float turn) { throttle = gas; steer = turn; }

    private void FixedUpdate()
    {
      if (obj != null && obj.Rb != null && !obj.Rb.isKinematic && obj.Mine && (spec.Boat || spec.Cart)) { if (spec.Boat) BoatPhysics(); else CartPhysics(); return; }   // SandBoxItems.cs
      if (wheels == null || obj == null || obj.Rb == null || obj.Rb.isKinematic) return;
      bool driven = world.Driving == obj;
      float gas = driven ? throttle : 0f;
      float speed = Vector3.Dot(obj.Rb.linearVelocity, transform.forward);
      steerNow = Mathf.MoveTowards(steerNow, (driven ? steer : 0f) * spec.MaxSteer * Mathf.Lerp(1f, 0.45f, Mathf.Abs(speed) / spec.TopSpeed), 120f * Time.fixedDeltaTime);
      float scale = Mathf.Pow(Mathf.Clamp(obj.Scale, 0.5f, 2f), 3f);
      for (int i = 0; i < wheels.Length; i++)
      {
        WheelCollider wc = wheels[i];
        if (wc == null) continue;
        bool front = i < 2;
        if (front) wc.steerAngle = steerNow;
        // braking: stick against the way you roll, or nobody driving
        bool braking = (gas > 0.1f && speed < -0.5f) || (gas < -0.1f && speed > 0.5f);
        float torque = Mathf.Abs(speed) < spec.TopSpeed || Mathf.Sign(gas) != Mathf.Sign(speed) ? gas * spec.Motor * scale : 0f;
        // the obby car drives all four wheels
        wc.motorTorque = braking || (front && !spec.Climber) ? 0f : torque;
        wc.brakeTorque = braking ? spec.Motor * 3f * scale : (Mathf.Abs(gas) < 0.05f ? (driven ? 30f : 400f) * scale : 0f);
      }
      if (spec.Climber) Climb(gas, speed, scale);
    }

    // the obby car's helpers. A step or kerb in front while you give gas lifts the
    // nose up onto it; on the ground it is pressed down (it sticks to slopes and ramps);
    // tipped over it turns itself back upright
    private void Climb(float gas, float speed, float scale)
    {
      Rigidbody rb = obj.Rb;
      float s = Mathf.Clamp(obj.Scale, 0.5f, 2f);
      int grounded = 0;
      for (int i = 0; i < wheels.Length; i++) if (wheels[i] != null && wheels[i].isGrounded) grounded++;
      if (grounded > 0) rb.AddForce(-transform.up * rb.mass * 6f * (grounded / 4f), ForceMode.Force);
      if (Mathf.Abs(gas) > 0.1f)
      {
        Vector3 dir = transform.forward * Mathf.Sign(gas);
        Vector3 low = transform.position + transform.up * (spec.WheelRadius * 0.4f * s) + dir * (spec.Wheels[0].z * s + spec.WheelRadius * s);
        RaycastHit hit;
        if (Physics.Raycast(low, dir, out hit, 0.6f * s, ~0, QueryTriggerInteraction.Ignore) && !hit.collider.transform.IsChildOf(transform) &&
            Vector3.Dot(hit.normal, Vector3.up) < 0.6f)
        {
          // how tall is it? a free spot above the step means it can be climbed
          Vector3 over = low + transform.up * (1.0f * s);
          if (!Physics.Raycast(over, dir, 0.8f * s, ~0, QueryTriggerInteraction.Ignore))
          {
            rb.AddForce(transform.up * rb.mass * 14f + dir * rb.mass * 4f, ForceMode.Force);
            rb.AddTorque(-transform.right * Mathf.Sign(gas) * rb.mass * 1.4f, ForceMode.Force);
          }
        }
      }
      // upright again: a gentle turn back when tipped past 50 degrees, faster when it is stuck
      float tilt = Vector3.Angle(transform.up, Vector3.up);
      if (tilt > 50f)
      {
        Vector3 axis = Vector3.Cross(transform.up, Vector3.up);
        rb.AddTorque(axis.normalized * rb.mass * (grounded == 0 && rb.linearVelocity.sqrMagnitude < 1f ? 9f : 3f), ForceMode.Force);
        if (tilt > 120f && rb.linearVelocity.sqrMagnitude < 0.5f) rb.AddForce(Vector3.up * rb.mass * 12f, ForceMode.Force);
      }
    }

    private void Update()
    {
      if (obj == null || obj.T == null) return;
      float dt = Mathf.Max(1e-4f, Time.deltaTime);
      Vector3 vel = (transform.position - lastPos) / dt;
      lastPos = transform.position;
      float speed = Vector3.Dot(vel, transform.forward);
      for (int i = 0; i < visuals.Length; i++)
      {
        if (visuals[i] == null) continue;
        Vector3 p; Quaternion q;
        if (wheels != null && wheels[i] != null && wheels[i].enabled && wheels[i].gameObject.activeInHierarchy)
        {
          wheels[i].GetWorldPose(out p, out q);
          visuals[i].SetPositionAndRotation(p, q);
        }
        else
        {
          // the others: roll them with the car
          spin[i] = Mathf.Repeat(spin[i] + speed / Mathf.Max(0.05f, spec.WheelRadius * obj.Scale) * Mathf.Rad2Deg * dt, 360f);
          visuals[i].localRotation = Quaternion.Euler(0f, i < 2 ? steerNow : 0f, 0f) * Quaternion.Euler(spin[i], 0f, 0f);
        }
      }
      if (engine != null)
      {
        float load = Mathf.Clamp01(Mathf.Abs(speed) / spec.TopSpeed);
        bool on = world.Driving == obj || Mathf.Abs(speed) > 0.5f;
        engine.volume = Mathf.MoveTowards(engine.volume, on ? (0.12f + 0.18f * load) * SbFx.Volume : 0f, dt * 0.8f);
        engine.pitch = 0.25f + 0.55f * load + (world.Driving == obj ? 0.1f * Mathf.Abs(throttle) : 0f);
      }
    }
  }
}
