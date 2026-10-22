using Photon.Pun;
using UnityEngine;

namespace UnknownsSandbox
{
  // The SandBox pointer and placement preview. SbInteract asks for them each frame
  // (SbDrawPointer / SbShowGhost, or nothing = hidden); SbPointerTick draws them:
  // both pop in and fade out, the pointer end and the preview glide instead of
  // jumping, and the pointer glows while the trigger (or place button) is held.
  public partial class SandboxPlugin
  {
    // ---------------------------------------------------------------- pointer state
    private bool sbPtrWant, sbPtrUi, sbPtrDelete;
    private Vector3 sbPtrFrom, sbPtrTarget, sbPtrTo;
    private float sbPtrVis, sbPtrPress;
    private LineRenderer sbPtrCore, sbPtrGlow;
    private GameObject sbPtrDot, sbPtrHalo;
    private Material sbPtrDotMat, sbPtrHaloMat;

    // ---------------------------------------------------------------- preview state
    private bool sbGhostWant, sbGhostValid, sbGhostCell, sbGhostHit;
    private SbItem sbGhostItem, sbGhostShown;
    private Vector3 sbGhostTargetPos, sbGhostPos, sbGhostHitPoint, sbGhostNormal = Vector3.up;
    private Quaternion sbGhostTargetRot = Quaternion.identity, sbGhostRot = Quaternion.identity;
    private float sbGhostTargetScale = 1f, sbGhostScale = 1f, sbGhostVis;
    private GameObject sbGhost, sbGhostRing;
    private MeshFilter sbGhostMf;
    private MeshRenderer sbGhostMr;
    private Material sbGhostMat, sbGhostRingMat;
    private LineRenderer sbGhostBox;

    private static float SbPop(float x)
    {
      // ease out with a small overshoot, for things appearing
      x = Mathf.Clamp01(x);
      const float c1 = 1.4f, c3 = c1 + 1f;
      return 1f + c3 * Mathf.Pow(x - 1f, 3f) + c1 * Mathf.Pow(x - 1f, 2f);
    }

    private void SbDrawPointer(Vector3 from, Vector3 to, bool ui)
    {
      sbPtrWant = true;
      sbPtrFrom = from;
      sbPtrTarget = to;
      sbPtrUi = ui;
    }

    private void SbHideWorldPointer()
    {
      sbPtrWant = false;
      sbGhostWant = false;
    }

    private void SbHideGhost() { sbGhostWant = false; }

    private void SbShowGhost(SbAim aim, SbItem item)
    {
      if (!sbGhostOn || item == null || aim == null) { sbGhostWant = false; return; }
      sbGhostWant = true;
      sbGhostItem = item;
      sbGhostValid = aim.Valid;
      sbGhostCell = item.Kind == SbKind.Block || item.Kind == SbKind.Fluid;
      sbGhostTargetPos = aim.Pos;
      sbGhostTargetRot = aim.Rot;
      sbGhostTargetScale = sbGhostCell ? aim.Scale * 1.002f : aim.Scale;
      sbGhostHit = aim.Hit;
      sbGhostHitPoint = aim.HitPoint;
      sbGhostNormal = aim.HitNormal.sqrMagnitude > 1e-4f ? aim.HitNormal.normalized : Vector3.up;
    }

    private void SbPointerTick()
    {
      float dt = Mathf.Min(Time.unscaledDeltaTime, 0.1f);
      SbStyle st = SbStyles.Get(sbStyleIdx);
      SbTickPointer(st, dt);
      SbTickGhost(st, dt);
    }

    // ================================================================ pointer
    private void SbEnsurePointer()
    {
      if (sbPtrCore != null) return;
      sbPtrGlow = SbMakeLine("Unknowns_SandBoxPointerGlow", 3099);
      sbPtrCore = SbMakeLine("Unknowns_SandBoxPointer", 3101);

      sbPtrDot = GameObject.CreatePrimitive(PrimitiveType.Sphere);
      sbPtrDot.name = "Unknowns_SandBoxPointerDot";
      var col = sbPtrDot.GetComponent<Collider>(); if (col != null) Destroy(col);
      var r = sbPtrDot.GetComponent<Renderer>();
      sbPtrDotMat = new Material(SbMats.Line) { hideFlags = HideFlags.DontSave, renderQueue = 3102 };
      r.sharedMaterial = sbPtrDotMat;
      r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

      // a soft halo behind the dot, always facing you
      sbPtrHalo = GameObject.CreatePrimitive(PrimitiveType.Quad);
      sbPtrHalo.name = "Unknowns_SandBoxPointerHalo";
      col = sbPtrHalo.GetComponent<Collider>(); if (col != null) Destroy(col);
      sbPtrHaloMat = new Material(SbMats.Line) { hideFlags = HideFlags.DontSave, renderQueue = 3100 };
      if (sbPtrHaloMat.HasProperty("_MainTex")) sbPtrHaloMat.SetTexture("_MainTex", SbMats.SoftDot);
      var hr = sbPtrHalo.GetComponent<Renderer>();
      hr.sharedMaterial = sbPtrHaloMat;
      hr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
      sbPtrDot.SetActive(false);
      sbPtrHalo.SetActive(false);
    }

    private static LineRenderer SbMakeLine(string name, int queue)
    {
      var go = new GameObject(name);
      var lr = go.AddComponent<LineRenderer>();
      lr.useWorldSpace = true;
      lr.positionCount = 2;
      lr.sharedMaterial = SbMats.Line;
      lr.numCapVertices = 4;
      lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
      lr.enabled = false;
      return lr;
    }

    private void SbTickPointer(SbStyle st, float dt)
    {
      bool wasHidden = sbPtrVis <= 0.001f;
      sbPtrVis = Mathf.MoveTowards(sbPtrVis, sbPtrWant ? 1f : 0f, dt / (sbPtrWant ? 0.16f : 0.12f));
      bool pressed = sbTriggerWas || sbPlaceWas;
      sbPtrPress = Mathf.MoveTowards(sbPtrPress, pressed && sbPtrWant ? 1f : 0f, dt * (pressed ? 10f : 5f));
      if (sbPtrVis <= 0.001f)
      {
        if (sbPtrCore != null && sbPtrCore.enabled)
        {
          sbPtrCore.enabled = sbPtrGlow.enabled = false;
          sbPtrDot.SetActive(false);
          sbPtrHalo.SetActive(false);
        }
        return;
      }
      SbEnsurePointer();
      if (sbPtrWant)
      {
        // the end glides; a fresh pointer or a big jump starts where it points
        if (wasHidden || (sbPtrTarget - sbPtrTo).sqrMagnitude > 1.5f * 1.5f) sbPtrTo = sbPtrTarget;
        else sbPtrTo = Vector3.Lerp(sbPtrTo, sbPtrTarget, 1f - Mathf.Exp(-32f * dt));
      }
      float pop = sbPtrWant ? SbPop(sbPtrVis) : sbPtrVis;
      float press = sbPtrPress;
      Color a = Color.Lerp(st.Accent, Color.white, 0.3f * press);
      Color b = sbPtrDelete ? new Color(1f, 0.28f, 0.22f) : Color.Lerp(st.Accent2, Color.white, 0.25f * press);
      // the line itself fades in from the hand: fully drawn once it has popped in
      Vector3 to = Vector3.Lerp(sbPtrFrom, sbPtrTo, Mathf.Clamp01(sbPtrVis * 1.3f));

      sbPtrCore.enabled = sbPtrGlow.enabled = true;
      sbPtrCore.SetPosition(0, sbPtrFrom); sbPtrCore.SetPosition(1, to);
      sbPtrGlow.SetPosition(0, sbPtrFrom); sbPtrGlow.SetPosition(1, to);
      float wk = pop * (1f + 0.45f * press);
      sbPtrCore.startWidth = 0.0032f * wk;
      sbPtrCore.endWidth = (sbPtrUi ? 0.0018f : 0.0026f) * wk;
      sbPtrCore.startColor = new Color(a.r, a.g, a.b, 0.95f * sbPtrVis);
      sbPtrCore.endColor = new Color(b.r, b.g, b.b, (sbPtrUi ? 0.9f : 0.55f) * sbPtrVis);
      float gw = pop * (1f + 0.9f * press);
      sbPtrGlow.startWidth = 0.012f * gw;
      sbPtrGlow.endWidth = 0.008f * gw;
      sbPtrGlow.startColor = new Color(a.r, a.g, a.b, (0.08f + 0.22f * press) * sbPtrVis);
      sbPtrGlow.endColor = new Color(b.r, b.g, b.b, (0.03f + 0.14f * press) * sbPtrVis);

      if (!sbPtrDot.activeSelf) sbPtrDot.SetActive(true);
      if (!sbPtrHalo.activeSelf) sbPtrHalo.SetActive(true);
      float ds = (sbPtrUi ? 0.009f : 0.015f) * pop * (1f + 0.3f * press);
      sbPtrDot.transform.position = to;
      sbPtrDot.transform.localScale = Vector3.one * ds;
      sbPtrDotMat.color = new Color(Mathf.Lerp(b.r, 1f, 0.4f), Mathf.Lerp(b.g, 1f, 0.4f), Mathf.Lerp(b.b, 1f, 0.4f), 0.95f * sbPtrVis);
      Transform head = this.GetTabletHead();
      Vector3 look = head != null ? to - head.position : sbPtrTo - sbPtrFrom;
      if (look.sqrMagnitude > 1e-6f) sbPtrHalo.transform.rotation = Quaternion.LookRotation(look);
      sbPtrHalo.transform.position = to;
      float breathe = 1f + 0.06f * Mathf.Sin(Time.unscaledTime * 5f);
      sbPtrHalo.transform.localScale = Vector3.one * (sbPtrUi ? 0.04f : 0.07f) * pop * (1f + 0.7f * press) * breathe;
      sbPtrHaloMat.color = new Color(b.r, b.g, b.b, (0.28f + 0.42f * press) * sbPtrVis);
    }

    // ================================================================ preview
    private void SbEnsureGhost()
    {
      if (sbGhost != null) return;
      sbGhost = new GameObject("Unknowns_SandBoxGhost");
      sbGhostMf = sbGhost.AddComponent<MeshFilter>();
      sbGhostMr = sbGhost.AddComponent<MeshRenderer>();
      sbGhostMat = new Material(SbMats.Ghost) { hideFlags = HideFlags.DontSave };
      sbGhostMr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
      var bx = new GameObject("box");
      bx.transform.SetParent(sbGhost.transform, false);
      sbGhostBox = bx.AddComponent<LineRenderer>();
      sbGhostBox.useWorldSpace = false;
      sbGhostBox.sharedMaterial = SbMats.Line;
      sbGhostBox.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
      sbGhostBox.numCornerVertices = 2;
      Vector3[] path =
      {
        new Vector3(-0.5f, -0.5f, -0.5f), new Vector3(0.5f, -0.5f, -0.5f), new Vector3(0.5f, -0.5f, 0.5f), new Vector3(-0.5f, -0.5f, 0.5f), new Vector3(-0.5f, -0.5f, -0.5f),
        new Vector3(-0.5f, 0.5f, -0.5f), new Vector3(0.5f, 0.5f, -0.5f), new Vector3(0.5f, 0.5f, 0.5f), new Vector3(-0.5f, 0.5f, 0.5f), new Vector3(-0.5f, 0.5f, -0.5f),
        new Vector3(-0.5f, 0.5f, 0.5f), new Vector3(-0.5f, -0.5f, 0.5f), new Vector3(0.5f, -0.5f, 0.5f), new Vector3(0.5f, 0.5f, 0.5f), new Vector3(0.5f, 0.5f, -0.5f), new Vector3(0.5f, -0.5f, -0.5f)
      };
      sbGhostBox.positionCount = path.Length;
      sbGhostBox.SetPositions(path);

      // a soft ring on the surface under objects
      sbGhostRing = GameObject.CreatePrimitive(PrimitiveType.Quad);
      sbGhostRing.name = "Unknowns_SandBoxGhostRing";
      var col = sbGhostRing.GetComponent<Collider>(); if (col != null) Destroy(col);
      sbGhostRingMat = new Material(SbMats.Line) { hideFlags = HideFlags.DontSave, renderQueue = 3060 };
      if (sbGhostRingMat.HasProperty("_MainTex")) sbGhostRingMat.SetTexture("_MainTex", SbMats.Ring);
      var rr = sbGhostRing.GetComponent<Renderer>();
      rr.sharedMaterial = sbGhostRingMat;
      rr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
      sbGhost.SetActive(false);
      sbGhostRing.SetActive(false);
    }

    private void SbTickGhost(SbStyle st, float dt)
    {
      if (sbGhostWant && sbGhostItem != sbGhostShown)
      {
        // a different item pops in fresh
        sbGhostShown = sbGhostItem;
        sbGhostVis = 0f;
      }
      bool fresh = sbGhostVis <= 0.001f && sbGhostWant;
      sbGhostVis = Mathf.MoveTowards(sbGhostVis, sbGhostWant ? 1f : 0f, dt / (sbGhostWant ? 0.22f : 0.12f));
      if (sbGhostVis <= 0.001f)
      {
        if (sbGhost != null && sbGhost.activeSelf) { sbGhost.SetActive(false); sbGhostRing.SetActive(false); }
        if (!sbGhostWant) sbGhostShown = null;
        return;
      }
      if (sbGhostShown == null) return;
      SbEnsureGhost();
      if (!sbGhost.activeSelf) sbGhost.SetActive(true);

      if (fresh)
      {
        sbGhostPos = sbGhostTargetPos;
        sbGhostRot = sbGhostTargetRot;
        sbGhostScale = sbGhostTargetScale;
      }
      else if (sbGhostWant)
      {
        float k = 1f - Mathf.Exp(-26f * dt);
        if ((sbGhostTargetPos - sbGhostPos).sqrMagnitude > 4f) sbGhostPos = sbGhostTargetPos;
        else sbGhostPos = Vector3.Lerp(sbGhostPos, sbGhostTargetPos, k);
        sbGhostRot = Quaternion.Slerp(sbGhostRot, sbGhostTargetRot, 1f - Mathf.Exp(-20f * dt));
        sbGhostScale = Mathf.Lerp(sbGhostScale, sbGhostTargetScale, 1f - Mathf.Exp(-18f * dt));
      }

      SbItem item = sbGhostShown;
      bool fluid = item.Kind == SbKind.Fluid;
      Mesh mesh = fluid ? SbBlockMesh.Get(SbBlockArt.Glass, SbBlockArt.Glass, SbBlockArt.Glass, SbBlockArt.Glass) : item.Mesh;
      if (sbGhostMf.sharedMesh != mesh) sbGhostMf.sharedMesh = mesh;
      Material[] mats = sbGhostMr.sharedMaterials;
      int subs = mesh != null ? mesh.subMeshCount : 1;
      if (mats == null || mats.Length != subs || mats[0] != sbGhostMat)
      {
        mats = new Material[subs];
        for (int i = 0; i < subs; i++) mats[i] = sbGhostMat;
        sbGhostMr.sharedMaterials = mats;
      }

      float pop = sbGhostWant ? SbPop(sbGhostVis) : sbGhostVis;
      float fade = Mathf.Clamp01(sbGhostVis * 1.6f);
      float t = Time.unscaledTime;
      Color c = sbGhostValid ? new Color(st.Accent.r, st.Accent.g, st.Accent.b, 0.30f + 0.05f * Mathf.Sin(t * 3f)) : new Color(1f, 0.28f, 0.22f, 0.32f);
      if (fluid && sbGhostValid) c = item.Fluid == SbFluid.Water ? new Color(0.25f, 0.5f, 1f, 0.42f) : new Color(1f, 0.45f, 0.1f, 0.46f);
      c.a *= fade;
      sbGhostMat.color = c;
      sbGhost.transform.SetPositionAndRotation(sbGhostPos, sbGhostRot);
      sbGhost.transform.localScale = Vector3.one * Mathf.Max(0.0001f, sbGhostScale * pop);

      sbGhostBox.enabled = sbGhostCell;
      if (sbGhostCell)
      {
        sbGhostBox.startWidth = sbGhostBox.endWidth = 0.0035f / Mathf.Max(0.05f, sbGhostScale);
        Color bc = sbGhostValid ? st.Accent : new Color(1f, 0.3f, 0.25f);
        bc.a = 0.9f * fade;
        sbGhostBox.startColor = sbGhostBox.endColor = bc;
      }

      bool ring = !sbGhostCell && sbGhostHit;
      if (sbGhostRing.activeSelf != ring) sbGhostRing.SetActive(ring);
      if (ring)
      {
        float radius = 0.15f;
        if (mesh != null) { Vector3 e = mesh.bounds.extents; radius = Mathf.Max(e.x, e.z); }
        radius = Mathf.Clamp(radius * sbGhostScale * 2.4f, 0.08f, 3f);
        // under the preview, on the surface it sits against
        Vector3 onSurface = sbGhostPos - sbGhostNormal * Vector3.Dot(sbGhostPos - sbGhostHitPoint, sbGhostNormal);
        sbGhostRing.transform.position = onSurface + sbGhostNormal * 0.004f;
        sbGhostRing.transform.rotation = Quaternion.LookRotation(sbGhostNormal) * Quaternion.Euler(0f, 0f, t * 40f);
        sbGhostRing.transform.localScale = Vector3.one * radius * pop;
        Color rc = sbGhostValid ? st.Accent : new Color(1f, 0.3f, 0.25f);
        rc.a = 0.55f * fade;
        sbGhostRingMat.color = rc;
      }
    }

    // ================================================================ presence
    // your tablet / pointer / preview for the other Unknowns players (SbPresence)
    private bool sbShowOthersTablet = true, sbShowOthersPointer = true, sbShowOthersPreview = true;
    private float sbPresNext;
    private int sbPresEmptyLeft;

    private void SbPresenceTick()
    {
      SbPresence.Tick(sbShowOthersTablet, sbShowOthersPointer, sbShowOthersPreview, this.GetTabletHead());
      if (!PhotonNetwork.InRoom) return;
      float now = Time.unscaledTime;
      if (now < sbPresNext) return;
      sbPresNext = now + 1f / 12f;
      bool tablet = sbVisible && sbRoot != null && sbRoot.activeSelf;
      bool ptr = sbPtrWant && sbPtrVis > 0f;
      bool ghost = sbGhostWant && sbGhostShown != null;
      // a few empty packets after the last thing closes, then nothing
      if (tablet || ptr || ghost) sbPresEmptyLeft = 3;
      else if (sbPresEmptyLeft > 0) sbPresEmptyLeft--;
      else return;
      // only lobbies with other SandBox users hear about it
      if (SbNet.UserCount == 0) return;

      SbStyle st = SbStyles.Get(sbStyleIdx);
      float pscale = PlayerScale;
      var o = new SbPresence.Out { accent = st.Accent, accent2 = st.Accent2 };
      if (tablet)
      {
        o.flags |= SbPresence.F.Tablet;
        o.root = sbRootPos; o.yaw = sbRootYaw;
        o.size = sbUiSize * pscale; o.dist = sbUiDist * pscale; o.height = sbUiHeight * pscale;
      }
      if (ptr)
      {
        o.flags |= SbPresence.F.Pointer;
        if (sbPtrUi) o.flags |= SbPresence.F.PtrUi;
        if (sbPtrDelete) o.flags |= SbPresence.F.PtrDelete;
        o.from = sbPtrFrom; o.to = sbPtrTo; o.press = sbPtrPress;
      }
      if (ghost)
      {
        o.flags |= SbPresence.F.Ghost;
        if (sbGhostValid) o.flags |= SbPresence.F.GhostValid;
        if (sbGhostCell) o.flags |= SbPresence.F.GhostCell;
        o.ghostKey = sbGhostShown.Key; o.ghostPos = sbGhostPos; o.ghostRot = sbGhostRot; o.ghostScale = sbGhostScale;
      }
      SbNet.SendPresence(SbPresence.Write(o));
    }
  }
}
