using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;

namespace UnknownsSandbox
{
  // Gun models: Kenney "Blaster Kit" 2.1 (www.kenney.nl, CC0 public domain).
  // The OBJ files were converted to triangles with their colours baked in
  // (SandBox\Models\kenney_blaster_kit.txt, embedded in the DLL), so they use
  // the same palette material as every other SandBox model.
  internal static class SbKenney
  {
    private sealed class Model
    {
      public readonly List<Vector3> P = new List<Vector3>();
      public readonly List<int> C = new List<int>();
      public readonly List<Color> Colors = new List<Color>();
    }

    private static Dictionary<string, Model> models;

    private static void Load()
    {
      if (models != null) return;
      models = new Dictionary<string, Model>(StringComparer.Ordinal);
      LoadFile("kenney_blaster_kit.txt");
      LoadFile("kenney_objects.txt");   // furniture / nature / food / holiday / racing kits
      LoadFile("realistic_models.txt.gz");   // Poly Haven props and Quaternius guns (polyhaven/..., quaternius/...)
    }

    private static void LoadFile(string file)
    {
      try
      {
        Stream s = typeof(SbKenney).Assembly.GetManifestResourceStream("UnknownsSandbox." + file);
        if (s == null) return;
        if (file.EndsWith(".gz", StringComparison.Ordinal)) s = new System.IO.Compression.GZipStream(s, System.IO.Compression.CompressionMode.Decompress);
        using (var r = new StreamReader(s))
        {
          Model cur = null;
          string line;
          var inv = CultureInfo.InvariantCulture;
          while ((line = r.ReadLine()) != null)
          {
            if (line.Length < 2) continue;
            char k = line[0];
            if (k == 'm') { cur = new Model(); models[line.Substring(2).Trim()] = cur; }
            else if (k == 'c' && cur != null)
            {
              foreach (string hex in line.Substring(2).Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries))
              {
                Color c; ColorUtility.TryParseHtmlString("#" + hex, out c);
                cur.Colors.Add(c);
              }
            }
            else if (k == 't' && cur != null)
            {
              string[] p = line.Split(' ');
              if (p.Length < 11) continue;
              int ci = int.Parse(p[1], inv);
              for (int i = 0; i < 3; i++)
                cur.P.Add(new Vector3(float.Parse(p[2 + i * 3], inv), float.Parse(p[3 + i * 3], inv), float.Parse(p[4 + i * 3], inv)));
              cur.C.Add(ci);
            }
          }
        }
      }
      catch (Exception ex) { SbLog.Warn("Kenney models: " + ex.Message); }
    }

    internal static bool Has(string name) { Load(); return models.ContainsKey(name); }

    // the model centred on the origin, scaled to `height` metres tall
    internal static void AddCentered(SbMeshKit k, string name, float height)
    {
      Fit(k, name, height, true, 0f);
    }

    // the model centred on the origin; size = its height (byHeight) or its largest side, in metres;
    // turned yawDeg about Y so its front faces +Z (the SandBox puts +Z towards the player)
    internal static void Fit(SbMeshKit k, string name, float size, bool byHeight, float yawDeg)
    {
      Load();
      Model m;
      if (!models.TryGetValue(name, out m) || m.P.Count == 0) return;
      Bounds b = new Bounds(m.P[0], Vector3.zero);
      foreach (Vector3 p in m.P) b.Encapsulate(p);
      float ext = byHeight ? b.size.y : Mathf.Max(b.size.x, Mathf.Max(b.size.y, b.size.z));
      float s = size / Mathf.Max(0.001f, ext);
      Quaternion r = Quaternion.Euler(0f, yawDeg, 0f);
      for (int i = 0; i + 2 < m.P.Count; i += 3)
        k.RawTri(r * ((m.P[i] - b.center) * s), r * ((m.P[i + 1] - b.center) * s), r * ((m.P[i + 2] - b.center) * s), m.Colors[Mathf.Clamp(m.C[i / 3], 0, m.Colors.Count - 1)]);
    }

    // Adds the model with its barrel along +Z, scaled to `length` metres, the
    // hand (grip) at the origin. Returns the model's bounds in that space.
    internal static Bounds Add(SbMeshKit k, string name, float length, out Vector3 muzzle)
    {
      Load();
      muzzle = new Vector3(0f, 0f, length * 0.6f);
      Model m;
      if (!models.TryGetValue(name, out m) || m.P.Count == 0) return new Bounds(Vector3.zero, Vector3.one * 0.1f);
      Bounds b = new Bounds(m.P[0], Vector3.zero);
      foreach (Vector3 p in m.P) b.Encapsulate(p);
      // barrel axis = the longest horizontal axis
      bool alongX = b.size.x > b.size.z;
      Func<Vector3, Vector3> axis = p => alongX ? new Vector3(-p.z, p.y, p.x) : p;
      Bounds a = new Bounds(axis(m.P[0]), Vector3.zero);
      foreach (Vector3 p in m.P) a.Encapsulate(axis(p));
      // grip = the lowest part (the handle)
      float lowY = a.min.y + a.size.y * 0.3f;
      Vector3 gsum = Vector3.zero; int gn = 0;
      foreach (Vector3 p0 in m.P) { Vector3 p = axis(p0); if (p.y <= lowY) { gsum += p; gn++; } }
      Vector3 grip = gn > 0 ? gsum / gn : a.center;
      // the barrel is the end farther from the handle
      bool frontIsMinus = Mathf.Abs(grip.z - a.min.z) > Mathf.Abs(grip.z - a.max.z);
      Quaternion flip = frontIsMinus ? Quaternion.Euler(0f, 180f, 0f) : Quaternion.identity;
      float scale = length / Mathf.Max(0.001f, a.size.z);
      Vector3 gripPoint = new Vector3(grip.x, a.min.y + a.size.y * 0.22f, grip.z);
      Func<Vector3, Vector3> tf = p => flip * ((axis(p) - gripPoint) * scale);

      Bounds outB = new Bounds(tf(m.P[0]), Vector3.zero);
      for (int i = 0; i + 2 < m.P.Count; i += 3)
      {
        Vector3 pa = tf(m.P[i]), pb = tf(m.P[i + 1]), pc = tf(m.P[i + 2]);
        // the axis swap / flip keeps winding (rotations only)
        k.RawTri(pa, pb, pc, m.Colors[Mathf.Clamp(m.C[i / 3], 0, m.Colors.Count - 1)]);
        outB.Encapsulate(pa); outB.Encapsulate(pb); outB.Encapsulate(pc);
      }
      // muzzle: centre of the front-most vertices
      float front = outB.max.z;
      Vector3 msum = Vector3.zero; int mn = 0;
      for (int i = 0; i < m.P.Count; i++)
      {
        Vector3 p = tf(m.P[i]);
        if (p.z >= front - outB.size.z * 0.06f) { msum += p; mn++; }
      }
      muzzle = mn > 0 ? msum / mn + Vector3.forward * 0.01f : new Vector3(0f, outB.center.y, front);
      k.GripAt(Vector3.zero).MuzzleAt(muzzle);
      return outB;
    }
  }

  // A Gorilla Tag gorilla made from a real rig: the body skin, face and bones are
  // copied (no game scripts), then the body, head, arms and hands get
  // rigidbodies and joints so it flops.
  internal static class SbRagdoll
  {
    internal sealed class Result
    {
      public GameObject Root;     // the body bone (gets the main Rigidbody)
      public readonly List<Collider> Cols = new List<Collider>();
      public readonly List<Rigidbody> Limbs = new List<Rigidbody>();
      public Bounds LocalBounds;
    }

    internal static string LastError = "";

    internal static Result Build(VRRig src, Transform parent, Color? fur = null)
    {
      LastError = "";
      if (src == null) { LastError = "no rig"; return null; }
      GorillaIK ik = src.GetComponent<GorillaIK>();
      if (ik == null) ik = src.GetComponentInChildren<GorillaIK>(true);
      if (ik == null) ik = src.GetComponentInParent<GorillaIK>();
      SkinnedMeshRenderer skin = null;
      try { if (src.bodyRenderer != null) skin = src.bodyRenderer.GetBody(GorillaBodyType.Default); } catch { }
      if (skin == null || skin.sharedMesh == null) skin = src.mainSkin;
      if (skin == null || skin.sharedMesh == null)
      {
        // any skinned body under the rig
        foreach (SkinnedMeshRenderer r in src.GetComponentsInChildren<SkinnedMeshRenderer>(true))
          if (r != null && r.sharedMesh != null && r.bones != null && r.bones.Length > 5) { skin = r; break; }
      }
      if (skin == null || skin.sharedMesh == null) { LastError = "no body skin"; return null; }

      // the body bone: the IK's, else the skin's root bone
      Transform body = ik != null && ik.bodyBone != null ? ik.bodyBone : skin.rootBone;
      if (body == null) { LastError = "no body bone"; return null; }
      if (ik == null) LastError = "no IK (body only)";
      var map = new Dictionary<Transform, Transform>();
      var res = new Result();
      // the root is upright and faces where the rig faces (the body bone's own axes
      // are the model's, not the world's); the copied body bone sits under it with its real offset.
      GameObject rootGo = new GameObject("SB Gorilla Ragdoll");
      rootGo.transform.SetParent(parent, false);
      rootGo.transform.SetPositionAndRotation(body.position, RigFrame(src));
      rootGo.transform.localScale = Vector3.one * RigScale(src);
      var bodyGo = new GameObject(body.name);
      bodyGo.transform.SetParent(rootGo.transform, false);
      bodyGo.transform.rotation = body.rotation;
      bodyGo.transform.localPosition = Vector3.zero;
      Vector3 rs = rootGo.transform.lossyScale;
      bodyGo.transform.localScale = new Vector3(body.lossyScale.x / Mathf.Max(1e-4f, rs.x), body.lossyScale.y / Mathf.Max(1e-4f, rs.y), body.lossyScale.z / Mathf.Max(1e-4f, rs.z));
      Transform bodyCopy = bodyGo.transform;
      map[body] = bodyCopy;

      Func<Transform, Transform> clone = null;
      clone = t =>
      {
        if (t == null) return bodyCopy;
        Transform got;
        if (map.TryGetValue(t, out got)) return got;
        if (!t.IsChildOf(body)) return bodyCopy; // above the body: fold into the body
        Transform p = clone(t.parent);
        var go = new GameObject(t.name);
        go.transform.SetParent(p, false);
        go.transform.localPosition = t.localPosition;
        go.transform.localRotation = t.localRotation;
        go.transform.localScale = t.localScale;
        map[t] = go.transform;
        return go.transform;
      };

      Transform[] bones = skin.bones;
      var newBones = new Transform[bones.Length];
      for (int i = 0; i < bones.Length; i++) newBones[i] = bones[i] == null ? rootGo.transform : clone(bones[i]);

      var skinGo = new GameObject("skin");
      skinGo.transform.SetParent(rootGo.transform, false);
      var smr = skinGo.AddComponent<SkinnedMeshRenderer>();
      smr.sharedMesh = skin.sharedMesh;
      Material[] mats = skin.sharedMaterials;
      var copy = new Material[mats.Length];
      for (int i = 0; i < mats.Length; i++) copy[i] = mats[i] != null ? new Material(mats[i]) { hideFlags = HideFlags.DontSave } : null;
      // the game's normal fur (VRRig.materialsToChangeTo[0]), never the skin the spawner is
      // wearing right now: tagged, that is the lava skin (and a rock monkey is another one)
      try
      {
        Material[] skins = src.materialsToChangeTo;
        if (skins != null && skins.Length > 0 && skins[0] != null && copy.Length > 0) copy[0] = new Material(skins[0]) { name = "Unknowns ragdoll fur", hideFlags = HideFlags.DontSave };
      }
      catch { }
      smr.sharedMaterials = copy;
      // the fur colour / material index can live in a property block; with a colour of its
      // own the spawner's block (their colour or tag state) is left out
      if (!fur.HasValue) { try { var mpb = new MaterialPropertyBlock(); skin.GetPropertyBlock(mpb); if (!mpb.isEmpty) smr.SetPropertyBlock(mpb); } catch { } }
      if (fur.HasValue)
      {
        // its own fur colour (the shape and face stay the spawner's)
        Color c = fur.Value; c.a = 1f;
        foreach (Material m in copy)
        {
          if (m == null) continue;
          if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
          if (m.HasProperty("_Color")) m.SetColor("_Color", c);
        }
        try
        {
          var mpb = new MaterialPropertyBlock();
          smr.GetPropertyBlock(mpb);
          mpb.SetColor("_BaseColor", c);
          mpb.SetColor("_Color", c);
          smr.SetPropertyBlock(mpb);
        }
        catch { }
      }
      smr.bones = newBones;
      smr.rootBone = skin.rootBone != null ? clone(skin.rootBone) : rootGo.transform;
      smr.updateWhenOffscreen = true;
      // bind the skin to the copied bones exactly like the original
      try { smr.localBounds = skin.localBounds; } catch { }

      // face
      MeshRenderer face = null;
      try { face = src.bodyRenderer != null && src.bodyRenderer.faceRenderer != null ? src.bodyRenderer.faceRenderer : src.faceSkin; } catch { }
      if (face != null)
      {
        MeshFilter mf = face.GetComponent<MeshFilter>();
        if (mf != null && mf.sharedMesh != null)
        {
          var fgo = new GameObject("face");
          Transform fp = face.transform.IsChildOf(body) ? clone(face.transform.parent) : (ik != null && ik.headBone != null && ik.headBone.IsChildOf(body) ? clone(ik.headBone) : rootGo.transform);
          fgo.transform.SetParent(fp, false);
          fgo.transform.position = face.transform.position;
          fgo.transform.rotation = face.transform.rotation;
          fgo.transform.localScale = Vector3.Scale(face.transform.lossyScale, new Vector3(1f / Mathf.Max(1e-4f, fp.lossyScale.x), 1f / Mathf.Max(1e-4f, fp.lossyScale.y), 1f / Mathf.Max(1e-4f, fp.lossyScale.z)));
          fgo.AddComponent<MeshFilter>().sharedMesh = mf.sharedMesh;
          var fr = fgo.AddComponent<MeshRenderer>();
          Material[] fm = face.sharedMaterials;
          var fcopy = new Material[fm.Length];
          for (int i = 0; i < fm.Length; i++) fcopy[i] = fm[i] != null ? new Material(fm[i]) { hideFlags = HideFlags.DontSave } : null;
          fr.sharedMaterials = fcopy;
        }
      }

      // physics: body capsule on a child (layer-switchable), limbs on their bones
      float s = body.lossyScale.y > 1e-4f ? 1f : 1f;
      Func<Transform, Transform> bone = t => t != null && t.IsChildOf(body) && t != body ? clone(t) : null;
      Transform head = ik != null ? bone(ik.headBone) : null;
      Transform lu = ik != null ? bone(ik.leftUpperArm) : null, ll = ik != null ? bone(ik.leftLowerArm) : null, lh = ik != null ? bone(ik.leftHand) : null;
      Transform ru = ik != null ? bone(ik.rightUpperArm) : null, rl = ik != null ? bone(ik.rightLowerArm) : null, rh = ik != null ? bone(ik.rightHand) : null;

      var colGo = new GameObject("col");
      colGo.transform.SetParent(rootGo.transform, false);
      var bodyCol = colGo.AddComponent<CapsuleCollider>();
      float bodyR = 0.16f / Mathf.Max(1e-4f, rootGo.transform.lossyScale.x);
      bodyCol.radius = bodyR;
      bodyCol.height = bodyR * 3.2f;
      bodyCol.direction = 1;
      bodyCol.center = Vector3.up * bodyR * 0.3f;
      res.Cols.Add(bodyCol);

      res.Root = rootGo;
      Rigidbody headRb = Limb(res, head, rootGo.transform, 0.11f, true);
      Rigidbody luRb = Limb(res, lu, rootGo.transform, 0.05f, false, ll);
      Rigidbody llRb = Limb(res, ll, lu, 0.045f, false, lh);
      Limb(res, lh, ll, 0.05f, true);
      Rigidbody ruRb = Limb(res, ru, rootGo.transform, 0.05f, false, rl);
      Rigidbody rlRb = Limb(res, rl, ru, 0.045f, false, rh);
      Limb(res, rh, rl, 0.05f, true);

      // bounds for grabbing / aiming, in root space
      Bounds lb = new Bounds(Vector3.zero, Vector3.one * bodyR * 2f);
      foreach (Transform t in map.Values) lb.Encapsulate(rootGo.transform.InverseTransformPoint(t.position));
      lb.Expand(bodyR);
      res.LocalBounds = lb;
      return res;
    }

    // The rig's upright facing (yaw only) and its size: the ragdoll's root frame.
    private static Quaternion RigFrame(VRRig src)
    {
      Vector3 f = src.transform.forward;
      f.y = 0f;
      return f.sqrMagnitude > 1e-6f ? Quaternion.LookRotation(f.normalized, Vector3.up) : Quaternion.identity;
    }

    private static float RigScale(VRRig src)
    {
      float s = src.transform.lossyScale.y;
      return s > 0.01f && s < 100f ? s : 1f;
    }

    internal static VRRig LocalRig()
    {
      try { return GorillaTagger.Instance != null ? GorillaTagger.Instance.offlineVRRig : null; } catch { return null; }
    }

    // the SandBox icon and the placement ghost show the real gorilla too.
    // The local player's body (and face) is baked into a palette mesh in the same
    // frame the ragdoll's root uses; it is redone when the fur colour changes.
    private static float nextPreviewCheck;
    private static Color previewColor = new Color(-1f, 0f, 0f, 0f);

    // true when the item's mesh and icon changed (the tablet then redraws them)
    internal static bool RefreshPreview(SbItem item)
    {
      if (item == null || Time.unscaledTime < nextPreviewCheck) return false;
      nextPreviewCheck = Time.unscaledTime + 2f;
      VRRig src = LocalRig();
      if (src == null) return false;
      Color fur = src.playerColor;
      fur.a = 1f;
      if (fur == previewColor) return false;   // same colour (or it failed with this colour)
      previewColor = fur;
      Mesh m = null;
      try { m = BakePreview(src, fur); } catch (Exception ex) { SbLog.Warn("ragdoll preview: " + ex.Message); }
      if (m == null) return false;
      Mesh old = item.Mesh;
      item.Mesh = m;
      if (item.icon != null && item.icon != Texture2D.whiteTexture) UnityEngine.Object.Destroy(item.icon);
      item.icon = null;
      if (old != null && old.name == "Unknowns SandBox Gorilla") UnityEngine.Object.Destroy(old);
      return true;
    }

    private static Mesh BakePreview(VRRig src, Color fur)
    {
      SkinnedMeshRenderer skin = null;
      try { if (src.bodyRenderer != null) skin = src.bodyRenderer.GetBody(GorillaBodyType.Default); } catch { }
      if (skin == null || skin.sharedMesh == null) skin = src.mainSkin;
      if (skin == null || skin.sharedMesh == null || !skin.sharedMesh.isReadable) return null;
      GorillaIK ik = src.GetComponent<GorillaIK>();
      if (ik == null) ik = src.GetComponentInChildren<GorillaIK>(true);
      Transform body = ik != null && ik.bodyBone != null ? ik.bodyBone : skin.rootBone;
      if (body == null) return null;
      Quaternion inv = Quaternion.Inverse(RigFrame(src));
      float invS = 1f / RigScale(src);
      Vector3 origin = body.position;
      Func<Vector3, Vector3> toLocal = w => inv * (w - origin) * invS;

      var kit = new SbMeshKit();
      var baked = new Mesh();
      skin.BakeMesh(baked, true);
      Vector3[] v = baked.vertices;
      Transform st = skin.transform;
      for (int i = 0; i < v.Length; i++) v[i] = toLocal(st.position + st.rotation * v[i]);
      Color chest = Color.Lerp(fur, new Color(0.85f, 0.75f, 0.62f, 1f), 0.55f);
      for (int sub = 0; sub < baked.subMeshCount; sub++)
      {
        int[] tri = baked.GetTriangles(sub);
        Color col = sub == 0 ? fur : chest;
        for (int i = 0; i + 2 < tri.Length; i += 3) kit.RawTri(v[tri[i]], v[tri[i + 1]], v[tri[i + 2]], col);
      }
      UnityEngine.Object.Destroy(baked);

      MeshRenderer face = null;
      try { face = src.bodyRenderer != null && src.bodyRenderer.faceRenderer != null ? src.bodyRenderer.faceRenderer : src.faceSkin; } catch { }
      MeshFilter mf = face != null ? face.GetComponent<MeshFilter>() : null;
      if (mf != null && mf.sharedMesh != null && mf.sharedMesh.isReadable)
      {
        Mesh fm = mf.sharedMesh;
        Vector3[] fv = fm.vertices;
        for (int i = 0; i < fv.Length; i++) fv[i] = toLocal(face.transform.TransformPoint(fv[i]));
        int[] ft = fm.triangles;
        Color faceCol = new Color(0.16f, 0.13f, 0.12f, 1f);
        for (int i = 0; i + 2 < ft.Length; i += 3) kit.RawTri(fv[ft[i]], fv[ft[i + 1]], fv[ft[i + 2]], faceCol);
      }
      return kit.ToMesh("Unknowns SandBox Gorilla");
    }

    // Rigidbody + collider on a bone, jointed to its parent bone's rigidbody
    // (the root's Rigidbody is added later by the SandBox world).
    private static Rigidbody Limb(Result res, Transform bone, Transform parentBone, float radiusWorld, bool sphere, Transform child = null)
    {
      if (bone == null) return null;
      float ls = Mathf.Max(1e-4f, bone.lossyScale.x);
      Collider c;
      if (sphere || child == null)
      {
        var sc = bone.gameObject.AddComponent<SphereCollider>();
        sc.radius = radiusWorld / ls;
        c = sc;
      }
      else
      {
        var cc = bone.gameObject.AddComponent<CapsuleCollider>();
        Vector3 to = bone.InverseTransformPoint(child.position);
        float len = to.magnitude;
        cc.radius = radiusWorld / ls;
        cc.height = len + cc.radius * 2f;
        cc.center = to * 0.5f;
        Vector3 ad = new Vector3(Mathf.Abs(to.x), Mathf.Abs(to.y), Mathf.Abs(to.z));
        cc.direction = ad.x >= ad.y && ad.x >= ad.z ? 0 : ad.y >= ad.z ? 1 : 2;
        c = cc;
      }
      res.Cols.Add(c);
      var rb = bone.gameObject.AddComponent<Rigidbody>();
      rb.mass = 0.4f;
      rb.linearDamping = 0.4f;
      rb.angularDamping = 1.5f;
      rb.interpolation = RigidbodyInterpolation.Interpolate;
      res.Limbs.Add(rb);
      bone.gameObject.AddComponent<SbJointLater>().parent = parentBone;
      return rb;
    }

    // Joints are connected once the root has its Rigidbody.
    internal static void ConnectJoints(GameObject root)
    {
      foreach (SbJointLater j in root.GetComponentsInChildren<SbJointLater>(true))
      {
        Rigidbody parentRb = j.parent != null ? j.parent.GetComponent<Rigidbody>() : null;
        if (parentRb == null) parentRb = root.GetComponent<Rigidbody>();
        if (parentRb == null) continue;
        var cj = j.gameObject.AddComponent<CharacterJoint>();
        cj.connectedBody = parentRb;
        cj.enableProjection = true;
        var low = cj.lowTwistLimit; low.limit = -60f; cj.lowTwistLimit = low;
        var high = cj.highTwistLimit; high.limit = 60f; cj.highTwistLimit = high;
        var s1 = cj.swing1Limit; s1.limit = 70f; cj.swing1Limit = s1;
        var s2 = cj.swing2Limit; s2.limit = 70f; cj.swing2Limit = s2;
        UnityEngine.Object.Destroy(j);
      }
    }
  }

  // realistic models in place of the low-poly ones: Poly Haven's scanned props
  // (furniture, street things, food, statues) and real guns (Poly Haven's pistol, rifle
  // and stick grenade, Quaternius' assault rifles, shotgun, SMG, revolver, sawed-off and
  // bullpup), all CC0, baked to colours (SandBox\Models\realistic_models.txt.gz).
  // Same item ids as before, so placed and networked objects keep working.
  internal static class SbRealModels
  {
    internal static void Apply(Func<string, SbItem> get, Action<SbItem> add)
    {
      Action<string, string, float, bool, string> swap = (id, model, size, byHeight, rename) =>
      {
        SbItem it = get(id);
        if (it == null || !SbKenney.Has(model)) return;
        it.Build = k => SbKenney.Fit(k, model, size, byHeight, 0f);
        if (rename != null) it.Name = rename;
      };
      swap("ob_chair", "polyhaven/WoodenChair_01", 1.15f, true, null);
      swap("ob_table", "polyhaven/WoodenTable_01", 0.78f, true, null);
      swap("ob_couch", "polyhaven/Sofa_01", 0.9f, true, null);
      swap("ob_tv", "polyhaven/Television_01", 0.55f, true, null);
      swap("ob_crate", "polyhaven/wooden_crate_01", 0.55f, false, "Wooden Crate");
      swap("ob_lamp", "polyhaven/street_lamp_01", 3.2f, true, "Street Lamp");
      swap("ob_campfire", "polyhaven/stone_fire_pit", 1.1f, false, null);
      swap("ob_rock", "polyhaven/boulder_01", 1.2f, false, null);
      swap("ob_banana", "polyhaven/bananas", 0.24f, false, "Bananas");
      swap("ob_barrel", "polyhaven/Barrel_01", 0.9f, true, "Oil Drum");
      swap("ob_bed", "polyhaven/GothicBed_01", 2.1f, false, null);
      swap("ob_bookcase", "polyhaven/wooden_bookshelf_worn", 1.9f, true, null);
      swap("ob_laptop", "polyhaven/classic_laptop", 0.32f, false, null);
      swap("ob_speaker", "polyhaven/boombox", 0.5f, false, "Boombox");
      swap("ob_apple", "polyhaven/food_apple_01", 0.09f, false, null);
      swap("ob_barrier", "polyhaven/concrete_road_barrier", 2.0f, false, "Concrete Barrier");
      swap("ob_duck", "polyhaven/rubber_duck_toy", 0.3f, false, null);
      swap("ob_tire", "polyhaven/old_tyre", 0.7f, false, null);
      swap("ob_stonehead", "polyhaven/marble_bust_01", 0.8f, true, "Marble Bust");
      swap("ob_sign", "polyhaven/WetFloorSign_01", 0.6f, true, "Wet Floor Sign");
      swap("ob_lantern", "polyhaven/Lantern_01", 0.4f, true, null);
      swap("ob_plant", "polyhaven/potted_plant_01", 0.9f, true, null);

      Action<string, string, string, string, string, float, bool, bool, SbCol, float> obj = (id, name, desc, section, model, size, byHeight, anchored, col, mass) =>
      {
        if (get(id) != null || !SbKenney.Has(model)) return;
        add(new SbItem
        {
          Id = id, Name = name, Desc = desc, Tab = SbTab.Objects, Section = section, Kind = SbKind.Prop,
          Build = k => SbKenney.Fit(k, model, size, byHeight, 0f), Col = col, DefaultAnchored = anchored, Mass = mass
        });
      };
      obj("ob_armchair", "Armchair", "A comfy old armchair.", "HOME", "polyhaven/ArmChair_01", 1.0f, true, true, SbCol.Box, 25f);
      obj("ob_rockingchair", "Rocking Chair", "Rocks back and forth.", "HOME", "polyhaven/Rockingchair_01", 1.1f, true, false, SbCol.Box, 12f);
      obj("ob_wheelchair", "Wheelchair", "Rolls.", "HOME", "polyhaven/wheelchair_01", 0.95f, true, false, SbCol.Box, 15f);
      obj("ob_console", "Game Console", "Retro games.", "HOME", "polyhaven/gaming_console", 0.3f, false, false, SbCol.Box, 1.5f);
      obj("ob_microwave", "Microwave", "Ding!", "HOME", "polyhaven/vintage_microwave", 0.5f, false, true, SbCol.Box, 12f);
      obj("ob_bench", "Wooden Bench", "Sit down.", "HOME", "polyhaven/painted_wooden_bench", 1.4f, false, true, SbCol.Box, 20f);
      obj("ob_suitcase", "Suitcase", "Packed and ready.", "HOME", "polyhaven/vintage_suitcase", 0.6f, false, false, SbCol.Box, 5f);
      obj("ob_toolbox", "Toolbox", "Full of tools.", "HOME", "polyhaven/metal_toolbox", 0.45f, false, false, SbCol.Box, 6f);
      obj("ob_horse", "Horse Statue", "Rearing up.", "HOME", "polyhaven/horse_statue_01", 1.0f, true, true, SbCol.Box, 30f);
      obj("ob_hydrant", "Fire Hydrant", "For the fire trucks.", "STREET", "polyhaven/fire_hydrant", 0.8f, true, true, SbCol.Box, 60f);
      obj("ob_trashcan", "Trash Can", "Metal bin.", "STREET", "polyhaven/metal_trash_can", 0.9f, true, false, SbCol.Box, 8f);
      obj("ob_propane", "Propane Tank", "Gas bottle.", "STREET", "polyhaven/propane_tank", 0.6f, true, false, SbCol.Box, 15f);
      obj("ob_jerrycan", "Jerry Can", "Fuel.", "STREET", "polyhaven/metal_jerrycan", 0.45f, true, false, SbCol.Box, 5f);
      obj("ob_generator", "Generator", "Portable power.", "MACHINES", "polyhaven/portable_generator", 0.6f, true, true, SbCol.Box, 50f);
      obj("ob_ladder", "Ladder", "Wooden ladder.", "BUILDING", "polyhaven/wooden_ladder", 2.4f, true, true, SbCol.Box, 8f);
      obj("ob_chest", "Treasure Chest", "Pirate gold?", "FUN", "polyhaven/treasure_chest", 0.8f, false, true, SbCol.Box, 25f);
      obj("ob_football", "Football", "Kick it.", "FUN", "polyhaven/football", 0.28f, false, false, SbCol.Sphere, 0.4f);
      obj("ob_gnome", "Garden Gnome", "He is watching.", "NATURE", "polyhaven/garden_gnome", 0.5f, true, false, SbCol.Box, 3f);
      obj("ob_picnic", "Picnic Table", "Lunch outside.", "NATURE", "polyhaven/wooden_picnic_table", 1.8f, false, true, SbCol.Box, 40f);
      obj("ob_winebarrel", "Wine Barrel", "Oak barrel.", "FOOD", "polyhaven/wine_barrel_01", 0.95f, true, false, SbCol.Box, 40f);

      // guns: the real model, scaled to its length, the grip found from the handle
      Action<string, string, float> gun = (id, model, len) =>
      {
        SbItem it = get(id);
        if (it == null || !SbKenney.Has(model)) return;
        it.Build = k => { Vector3 mz; SbKenney.Add(k, model, len, out mz); k.GripAt(Vector3.zero).MuzzleAt(mz); };
      };
      gun("gun_pistol", "polyhaven/service_pistol", 0.21f);
      gun("gun_rifle", "quaternius/assault_rifle_c", 0.86f);
      gun("gun_shotgun", "quaternius/shotgun_a", 0.98f);
      gun("gun_sniper", "polyhaven/bolt_action_rifle_7_62", 1.12f);
      // the gadgets: real CC0 models (the Portal, Rick and Gravity guns stay the detailed recreations in SandBoxGadgets.cs)
      gun("gun_teleport", "gadgets/scifi_pistol", 0.3f);
      gun("gun_build", "gadgets/scifi_gun", 0.34f);
      Action<string, string, string, string, SbAmmo, float, bool, int, float, float, float> newGun = (id, name, desc, model, ammo, rate, auto, pellets, spread, speed, len) =>
      {
        if (get(id) != null || !SbKenney.Has(model)) return;
        add(new SbItem
        {
          Id = id, Name = name, Desc = desc, Tab = SbTab.Weapons, Section = "GUNS", Kind = SbKind.Gun, Ammo = ammo, FireRate = rate, Auto = auto,
          Pellets = pellets, Spread = spread, Speed = speed, Holdable = true, DefaultAnchored = true, Mass = 1.5f,
          Build = k => { Vector3 mz; SbKenney.Add(k, model, len, out mz); k.GripAt(Vector3.zero).MuzzleAt(mz); }
        });
      };
      newGun("gun_ak", "AK-47", "Full auto, a bit wilder than the assault rifle.", "quaternius/assault_rifle_a", SbAmmo.Bullet, 10f, true, 1, 1.6f, 80f, 0.88f);
      newGun("gun_smg", "SMG", "Very fast full auto, short range.", "quaternius/smg_a", SbAmmo.Bullet, 16f, true, 1, 2.2f, 65f, 0.5f);
      newGun("gun_revolver", "Revolver", "Slow, hard hitting.", "quaternius/revolver_a", SbAmmo.Sniper, 2f, false, 1, 0.3f, 75f, 0.32f);
      newGun("gun_sawedoff", "Sawed-Off", "Ten pellets, wide spread.", "quaternius/sawed_off", SbAmmo.Pellet, 1.2f, false, 10, 8f, 50f, 0.6f);
      newGun("gun_bullpup", "Bullpup Rifle", "Compact full auto.", "quaternius/bullpup", SbAmmo.Bullet, 12f, true, 1, 1.0f, 85f, 0.72f);

      SbItem nade = get("bomb_grenade");
      if (nade != null && SbKenney.Has("polyhaven/stick_grenade")) { nade.Build = k => SbKenney.Fit(k, "polyhaven/stick_grenade", 0.32f, true, 0f); nade.Col = SbCol.Box; }
    }
  }

  internal sealed class SbJointLater : MonoBehaviour { internal Transform parent; }
}
