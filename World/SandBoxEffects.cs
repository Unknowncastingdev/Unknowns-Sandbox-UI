using System;
using System.Collections.Generic;
using GorillaLocomotion;
using TMPro;
using UnityEngine;

namespace UnknownsSandbox
{
  // blood and bullet holes, the potion status HUD, firework rockets.
  //  - A player hit by a shot or a blade bleeds (SandBox settings: BLOOD WHEN PLAYERS ARE
  //    HIT); everyone sees it, it comes from the shot / swing everyone sees.
  //  - Bullets leave holes in the walls and in objects (the hole moves with the object).
  //  - The effects of the potions you drank, with the seconds left, float at the bottom
  //    left of your view.
  //  - Firework rocket: while gliding with an elytra, trigger = a boost along where you
  //    look; otherwise it goes up and bursts in the sky.
  internal sealed partial class SandBoxWorld
  {
    internal static bool BloodOn = true;

    private static bool Bulletish(SbAmmo a) { return a == SbAmmo.Bullet || a == SbAmmo.Pellet || a == SbAmmo.Sniper || a == SbAmmo.Minigun; }

    internal static void Blood(Vector3 at, Vector3 dir)
    {
      if (!BloodOn) return;
      Vector3 spray = (dir.sqrMagnitude > 1e-4f ? dir.normalized : Vector3.up) + Vector3.up * 0.2f;
      SbFx.Burst(at, 26, new Color(0.62f, 0.02f, 0.02f), new Color(0.32f, 0f, 0f), 1.2f, 3.5f, 0.015f, 0.045f, 0.35f, 0.7f, 1.4f, false, 0.04f, spray, 35f, 0f, 0.6f);
      SbFx.Burst(at, 8, new Color(0.5f, 0.02f, 0.02f, 0.8f), new Color(0.3f, 0f, 0f, 0.6f), 0.2f, 0.6f, 0.06f, 0.12f, 0.4f, 0.7f, 0.3f, false, 0.05f, null, 0f, 0f, 1.4f, true);
      // drops on the ground under them
      RaycastHit hit;
      if (Physics.Raycast(at + Vector3.up * 0.1f, Vector3.down, out hit, 3f, ~0, QueryTriggerInteraction.Ignore))
        SbFx.PaintDecal(hit.point, hit.normal, new Color(0.45f, 0.02f, 0.02f, 0.9f), 0.25f);
    }

    // ---------------------------------------------------------------- bullet holes
    private static readonly Queue<GameObject> bulletHoles = new Queue<GameObject>();
    private static Material bulletHoleMat;

    internal static void BulletHole(Vector3 at, Vector3 normal, Transform onto)
    {
      try
      {
        while (bulletHoles.Count > 80) { GameObject old = bulletHoles.Dequeue(); if (old != null) Destroy(old); }
        if (bulletHoleMat == null)
        {
          bulletHoleMat = new Material(SbMats.Line) { hideFlags = HideFlags.DontSave, renderQueue = 3004 };
          if (bulletHoleMat.HasProperty("_MainTex")) bulletHoleMat.SetTexture("_MainTex", SbMats.SoftDot);
          bulletHoleMat.color = new Color(0.03f, 0.03f, 0.03f, 0.95f);
        }
        GameObject g = GameObject.CreatePrimitive(PrimitiveType.Quad);
        g.name = "SB bullet hole";
        Collider c = g.GetComponent<Collider>(); if (c != null) Destroy(c);
        g.transform.SetParent(onto != null ? onto : SbFx.Root, true);
        g.transform.SetPositionAndRotation(at + normal * 0.004f, Quaternion.LookRotation(-normal) * Quaternion.Euler(0f, 0f, UnityEngine.Random.value * 360f));
        Vector3 ls = Vector3.one * UnityEngine.Random.Range(0.035f, 0.05f);
        if (onto != null) { Vector3 p = onto.lossyScale; ls = new Vector3(ls.x / Mathf.Max(1e-4f, p.x), ls.y / Mathf.Max(1e-4f, p.y), ls.z / Mathf.Max(1e-4f, p.z)); }
        g.transform.localScale = ls;
        Renderer r = g.GetComponent<Renderer>();
        r.sharedMaterial = bulletHoleMat;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        Destroy(g, 60f);
        bulletHoles.Enqueue(g);
        SbFx.Burst(at, 5, new Color(0.55f, 0.52f, 0.48f, 0.8f), new Color(0.35f, 0.33f, 0.3f, 0.6f), 0.4f, 1.4f, 0.01f, 0.025f, 0.3f, 0.6f, 1f, false, 0.01f, normal, 40f);
      }
      catch { }
    }

    // ---------------------------------------------------------------- the potion HUD
    private GameObject potionHud;
    private TextMeshPro potionText;

    private void UpdatePotionHud()
    {
      float now = Time.time;
      var lines = new List<string>();
      void Add(string name, float until, string col) { if (now < until) lines.Add("<color=" + col + ">■</color> " + name + "  " + Mathf.CeilToInt(until - now) / 60 + ":" + (Mathf.CeilToInt(until - now) % 60).ToString("00")); }
      Add("Swiftness", speedUntil, "#7cafc6");
      Add("Leaping", jumpUntil, "#22ff4c");
      Add("Night Vision", nightUntil, "#3a3aff");
      Add("Slow Falling", slowFallUntil, "#f7f8e0");
      Transform head = HeadProvider != null ? HeadProvider() : null;
      if (lines.Count == 0 || head == null) { if (potionHud != null && potionHud.activeSelf) potionHud.SetActive(false); return; }
      if (potionHud == null)
      {
        potionHud = new GameObject("SB potion hud");
        potionHud.transform.SetParent(SbFx.Root, false);
        potionText = potionHud.AddComponent<TextMeshPro>();
        potionText.alignment = TextAlignmentOptions.BottomLeft;
        potionText.fontSize = 0.32f;
        potionText.textWrappingMode = TextWrappingModes.NoWrap;
        potionText.richText = true;
        potionText.rectTransform.sizeDelta = new Vector2(0.8f, 0.4f);
        potionText.color = Color.white;
        potionText.outlineWidth = 0.2f;
        potionText.outlineColor = new Color32(0, 0, 0, 200);
      }
      if (!potionHud.activeSelf) potionHud.SetActive(true);
      potionHud.transform.SetPositionAndRotation(head.position + head.forward * 1.2f - head.up * 0.36f - head.right * 0.3f, Quaternion.LookRotation(head.forward, head.up));
      string text = string.Join("\n", lines.ToArray());
      if (potionText.text != text) potionText.text = text;
    }

    // ---------------------------------------------------------------- fireworks
    private float fireworkBoostUntil;

    private bool UseFirework(SbObject rocket, bool right)
    {
      Vector3 at = rocket.T.position;
      bool gliding = false;
      try { GTPlayer p = GTPlayer.Instance; gliding = elytraOn && p != null && p.playerRigidBody != null && p.playerRigidBody.linearVelocity.magnitude > 2f; } catch { }
      SbFx.PlayMc("firework_launch", SbSound.Rocket, at, 0.9f, 1.1f, 30f);
      if (gliding && PearlAllowed)
      {
        fireworkBoostUntil = Time.time + 1.4f;
        Haptic?.Invoke(right, 0.6f, 0.3f);
        return true;
      }
      // up into the sky, then a burst
      Vector3 sky = at + Vector3.up * 16f;
      SbFx.Burst(at, 30, new Color(1f, 0.8f, 0.4f), new Color(1f, 0.4f, 0.1f), 2f, 6f, 0.02f, 0.05f, 0.5f, 1f, -0.5f, true, 0.05f, Vector3.up, 8f);
      Color c = Color.HSVToRGB(UnityEngine.Random.value, 0.8f, 1f);
      SbFx.Burst(sky, 120, c, Color.white, 4f, 9f, 0.04f, 0.09f, 1f, 1.8f, 0.4f, true, 0.2f);
      SbFx.Play(SbSound.Pop, sky, 1f, 0.6f, 120f);
      return true;
    }

    // FixedUpdate: the boost
    private void ApplyFireworkBoost()
    {
      if (Time.time >= fireworkBoostUntil || !PearlAllowed) return;
      try
      {
        GTPlayer p = GTPlayer.Instance;
        Rigidbody rb = p != null ? p.playerRigidBody : null;
        Transform head = HeadProvider != null ? HeadProvider() : null;
        if (rb == null || head == null) return;
        rb.linearVelocity = Vector3.Lerp(rb.linearVelocity, head.forward * 26f, 3f * Time.fixedDeltaTime);
        if (Time.frameCount % 2 == 0)
        {
          Transform hand = HandProvider != null ? HandProvider(true) : null;
          if (hand != null) SbFx.Burst(hand.position, 3, new Color(1f, 0.85f, 0.5f), new Color(1f, 0.5f, 0.2f), 0.2f, 0.8f, 0.02f, 0.04f, 0.3f, 0.5f, 0f, true, 0.02f);
        }
      }
      catch { }
    }
  }

  internal static class SbEffectItems
  {
    internal static void AddToCatalog(Action<SbItem> add)
    {
      add(new SbItem
      {
        Id = "mc_firework_rocket", Name = "Firework Rocket", Desc = "Trigger: with an elytra while gliding, a boost where you look. Otherwise it flies up and bursts.", Tab = SbTab.Minecraft, Section = "ITEMS",
        Kind = SbKind.Tool, Holdable = true, DefaultAnchored = true, Sound = "wool",
        Build = k =>
        {
          k.Cyl(new Vector3(0f, 0.04f, 0f), 0.025f, 0.12f, Quaternion.identity, SbCatalog.C("#c03a2a"), 12);
          k.Cone(new Vector3(0f, 0.12f, 0f), 0.026f, 0.04f, Quaternion.identity, SbCatalog.C("#e8e8e8"), 12);
          k.Cyl(new Vector3(0f, -0.04f, 0f), 0.004f, 0.06f, Quaternion.identity, SbCatalog.C("#7a5a32"), 6);
          k.GripAt(Vector3.zero).MuzzleAt(new Vector3(0f, 0.15f, 0f));
        }
      });
    }
  }
}
