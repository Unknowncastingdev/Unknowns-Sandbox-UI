using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace UnknownsSandbox
{
  // SandBox Tablet: three floating holo displays that warp out of your chest.
  // Left = tabs + tools, centre = scrolling item grid (or settings), right =
  // the selected item + its options and the settings cog. Point with the hand
  // ray and pull the trigger to place, secondary to delete (both rebindable).
  public partial class SandboxPlugin
  {
    private enum SbInputKind { Trigger = 0, Grip = 1, Primary = 2, Secondary = 3 }

    private sealed class SbCtl
    {
      public RectTransform rt;
      public int panel;
      public Image bg;
      public Image edge;
      public TMP_Text label;
      public TMP_Text stateText;
      public Action click;
      public Func<bool> on;
      public bool slider;
      public Func<float> get;
      public Action<float> set;
      public RectTransform fill, knob, track;
      public bool inScroll;
      public bool catBar;   // a chip of the category bar (rebuilt with the grid)
      public bool card;
      public float hover, onBlend, pulse;
      public Color baseCol, hoverCol, onCol;
      public float lastClick = -10f;
    }

    // settings
    private bool sbPrefsLoaded;
    private int sbStyleIdx;
    private float sbUiSize = 0.85f, sbUiDist = 0.85f, sbUiHeight = -0.10f;   // 70 -> 85 cm
    private int sbOpenBind = 1; // TabletBind.LeftPrimary
    private int sbPlaceInput = (int)SbInputKind.Trigger;
    private bool sbPointerRight = true, sbFollow = true, sbKeepBuilding = true, sbSnap, sbGhostOn = true, sbShowOthers = true;
    private float sbVolume = 0.8f;
    private float sbScale = 1f;
    private int sbBlockIdx = 5;
    private bool sbAnchored = true;

    // state
    private bool sbVisible;
    private float sbAnimTime = 10f;
    private float sbOpenT;
    private bool sbOpenWasDown;
    private GameObject sbRoot;
    private readonly GameObject[] sbPanels = new GameObject[4];   // [3] the keyboard (SandBoxKeyboard.cs)
    private readonly RectTransform[] sbPanelRts = new RectTransform[4];
    private readonly CanvasGroup[] sbGroups = new CanvasGroup[3];
    private readonly LineRenderer[] sbBeams = new LineRenderer[3];
    private GameObject sbPortal;
    private Material sbPortalMat;
    private Vector3 sbRootPos;
    private float sbRootYaw;
    private readonly List<SbCtl> sbCtls = new List<SbCtl>();
    private SbTab sbTab = SbTab.Minecraft;
    private bool sbSettingsOpen;
    private SbItem sbSelected;
    private float sbScroll, sbScrollTarget, sbContentHeight;
    private RectTransform sbViewport, sbContent, sbThumb, sbTrack;
    private TMP_Text sbHeaderText, sbCountText, sbStatusText, sbPlacedText, sbSelName, sbSelDesc, sbSizeText, sbHintText;
    private Image sbStatusDot;
    private RawImage sbSelIcon;
    private RectTransform sbSelRing;
    private SbCtl sbAnchorCtl, sbSizeCtl, sbEquipL, sbEquipR, sbRotateCtl, sbMinusCtl, sbPlusCtl;
    private readonly List<KeyValuePair<RawImage, SbItem>> sbIconQueue = new List<KeyValuePair<RawImage, SbItem>>();
    private SbCtl sbDragCtl;
    private SbCtl sbHoverCtl;
    private bool sbTriggerWas, sbPlaceWas;
    private float sbPlaceRepeat, sbDeleteRepeat;
    private readonly SbAim sbAim = new SbAim();
    private float sbYawOffset;
    private bool sbStickXWas, sbStickYWas;
    private SbCtl sbFingerL, sbFingerR;
    private float sbNextStatus;
    private bool sbHooksSet;
    private float sbScanT;
    private bool sbPrefsDirty;
    private float sbPrefsSaveAt;
    private float sbAccentApplyAt;
    private SbCtl sbLobbyCtl;
    private SbCtl sbHostBtn;
    private RawImage[] sbScanlines = new RawImage[8];

    private static readonly float[] SbPanelW = { 300f, 640f, 300f };
    private const float SbPanelH = 560f;

    // ================================================================ prefs
    private void SbLoadPrefs()
    {
      if (sbPrefsLoaded) return;
      sbPrefsLoaded = true;
      sbStyleIdx = PlayerPrefs.GetInt("UnknownsSandBox_Style", 0);
      sbStyleIdx = Mathf.Clamp(sbStyleIdx, 0, SbStyles.All.Count - 1);
      SbStyles.UseCustomAccent = PlayerPrefs.GetInt("UnknownsSandBox_CustomAccent", 0) == 1;
      SbStyles.CustomAccent = SbLoadColor("UnknownsSandBox_AccentColor", SbStyles.CustomAccent);
      sbSpawnFxOn = PlayerPrefs.GetInt("UnknownsSandBox_SpawnFx", 1) == 1;
      sbUiSize = PlayerPrefs.GetFloat("UnknownsSandBox_Size", 0.85f);
      sbUiDist = PlayerPrefs.GetFloat("UnknownsSandBox_Dist", 0.85f);
      sbUiHeight = PlayerPrefs.GetFloat("UnknownsSandBox_Height", -0.10f);
      sbOpenBind = Mathf.Clamp(PlayerPrefs.GetInt("UnknownsSandBox_OpenBind", (int)TabletBind.LeftPrimary), 0, 7);
      sbPlaceInput = Mathf.Clamp(PlayerPrefs.GetInt("UnknownsSandBox_PlaceInput", 0), 0, 3);
      if (sbPlaceInput == (int)SbInputKind.Grip) sbPlaceInput = (int)SbInputKind.Trigger;   // grip is for holding and deleting
      sbPointerRight = PlayerPrefs.GetInt("UnknownsSandBox_PointerRight", 1) == 1;
      sbFollow = PlayerPrefs.GetInt("UnknownsSandBox_Follow", 1) == 1;
      sbFollowSmooth = Mathf.Clamp01(PlayerPrefs.GetFloat("UnknownsSandBox_FollowSmooth", 0.45f));
      SandBoxWorld.NetherViewChunks = Mathf.Clamp(PlayerPrefs.GetInt("UnknownsSandBox_NetherView", 2), 1, 6);
      sbKeepBuilding = PlayerPrefs.GetInt("UnknownsSandBox_KeepBuilding", 1) == 1;
      sbSnap = PlayerPrefs.GetInt("UnknownsSandBox_Snap", 0) == 1;
      sbGhostOn = PlayerPrefs.GetInt("UnknownsSandBox_Ghost", 1) == 1;
      sbShowOthers = PlayerPrefs.GetInt("UnknownsSandBox_ShowOthers", 1) == 1;
      sbShowOthersTablet = PlayerPrefs.GetInt("UnknownsSandBox_ShowOthersTablet", 1) == 1;
      sbShowOthersPointer = PlayerPrefs.GetInt("UnknownsSandBox_ShowOthersPointer", 1) == 1;
      SandBoxWorld.BloodOn = PlayerPrefs.GetInt("UnknownsSandBox_Blood", 1) == 1;
      sbShowOthersPreview = PlayerPrefs.GetInt("UnknownsSandBox_ShowOthersPreview", 1) == 1;
      sbVolume = PlayerPrefs.GetFloat("UnknownsSandBox_Volume", 0.8f);
      sbScale = Mathf.Clamp(PlayerPrefs.GetFloat("UnknownsSandBox_Scale", 1f), 0.25f, 4f);
      sbBlockIdx = Mathf.Clamp(PlayerPrefs.GetInt("UnknownsSandBox_BlockSize", 5), 0, SandBoxWorld.BlockSizes.Length - 1);
      sbAnchored = PlayerPrefs.GetInt("UnknownsSandBox_Anchored", 1) == 1;
      SandBoxWorld.ShowOthers = sbShowOthers;
      SbFx.Volume = sbVolume;
    }

    private void SbSavePrefs()
    {
      PlayerPrefs.SetInt("UnknownsSandBox_Style", sbStyleIdx);
      PlayerPrefs.SetInt("UnknownsSandBox_CustomAccent", SbStyles.UseCustomAccent ? 1 : 0);
      PlayerPrefs.SetString("UnknownsSandBox_AccentColor", ColorUtility.ToHtmlStringRGB(SbStyles.CustomAccent));
      PlayerPrefs.SetInt("UnknownsSandBox_SpawnFx", sbSpawnFxOn ? 1 : 0);
      PlayerPrefs.SetFloat("UnknownsSandBox_Size", sbUiSize);
      PlayerPrefs.SetFloat("UnknownsSandBox_Dist", sbUiDist);
      PlayerPrefs.SetFloat("UnknownsSandBox_Height", sbUiHeight);
      PlayerPrefs.SetInt("UnknownsSandBox_OpenBind", sbOpenBind);
      PlayerPrefs.SetInt("UnknownsSandBox_PlaceInput", sbPlaceInput);
      PlayerPrefs.SetInt("UnknownsSandBox_PointerRight", sbPointerRight ? 1 : 0);
      PlayerPrefs.SetInt("UnknownsSandBox_Follow", sbFollow ? 1 : 0);
      PlayerPrefs.SetFloat("UnknownsSandBox_FollowSmooth", sbFollowSmooth);
      PlayerPrefs.SetInt("UnknownsSandBox_NetherView", SandBoxWorld.NetherViewChunks);
      PlayerPrefs.SetInt("UnknownsSandBox_KeepBuilding", sbKeepBuilding ? 1 : 0);
      PlayerPrefs.SetInt("UnknownsSandBox_Snap", sbSnap ? 1 : 0);
      PlayerPrefs.SetInt("UnknownsSandBox_Ghost", sbGhostOn ? 1 : 0);
      PlayerPrefs.SetInt("UnknownsSandBox_ShowOthers", sbShowOthers ? 1 : 0);
      PlayerPrefs.SetInt("UnknownsSandBox_ShowOthersTablet", sbShowOthersTablet ? 1 : 0);
      PlayerPrefs.SetInt("UnknownsSandBox_ShowOthersPointer", sbShowOthersPointer ? 1 : 0);
      PlayerPrefs.SetInt("UnknownsSandBox_Blood", SandBoxWorld.BloodOn ? 1 : 0);
      PlayerPrefs.SetInt("UnknownsSandBox_ShowOthersPreview", sbShowOthersPreview ? 1 : 0);
      PlayerPrefs.SetFloat("UnknownsSandBox_Volume", sbVolume);
      PlayerPrefs.SetFloat("UnknownsSandBox_Scale", sbScale);
      PlayerPrefs.SetInt("UnknownsSandBox_BlockSize", sbBlockIdx);
      PlayerPrefs.SetInt("UnknownsSandBox_Anchored", sbAnchored ? 1 : 0);
      PlayerPrefs.Save();
      SandBoxWorld.ShowOthers = sbShowOthers;
      SbFx.Volume = sbVolume;
    }

    // A custom accent colour over the style (picked in the desktop window). The panels
    // are rebuilt once it stops changing, not on every step of the slider.
    internal void SbSetAccent(bool custom, Color colour)
    {
      colour.a = 1f;
      if (SbStyles.UseCustomAccent == custom && SbStyles.CustomAccent == colour) return;
      SbStyles.UseCustomAccent = custom;
      SbStyles.CustomAccent = colour;
      sbAccentApplyAt = Time.unscaledTime + 0.35f;
    }

    private static Color SbLoadColor(string key, Color fallback)
    {
      Color c;
      return ColorUtility.TryParseHtmlString("#" + PlayerPrefs.GetString(key, string.Empty), out c) ? c : fallback;
    }

    private static string SbInputName(int k)
    {
      switch ((SbInputKind)k)
      {
        case SbInputKind.Grip: return "GRIP";
        case SbInputKind.Primary: return "PRIMARY (A/X)";
        case SbInputKind.Secondary: return "SECONDARY (B/Y)";
        default: return "TRIGGER";
      }
    }

    private bool SbInputDown(int kind, bool right)
    {
      switch ((SbInputKind)kind)
      {
        case SbInputKind.Grip: return this.GetTabletBindDown(right ? TabletBind.RightGrip : TabletBind.LeftGrip);
        case SbInputKind.Primary: return this.GetTabletBindDown(right ? TabletBind.RightPrimary : TabletBind.LeftPrimary);
        case SbInputKind.Secondary: return this.GetTabletBindDown(right ? TabletBind.RightSecondary : TabletBind.LeftSecondary);
        default: return this.GetTabletBindDown(right ? TabletBind.RightTrigger : TabletBind.LeftTrigger);
      }
    }

    private static Vector2 SbStick(bool right)
    {
      try
      {
        ControllerInputPoller p = ControllerInputPoller.instance;
        if (p == null) return Vector2.zero;
        return right ? p.rightControllerPrimary2DAxis : p.leftControllerPrimary2DAxis;
      }
      catch { return Vector2.zero; }
    }

    // ================================================================ main update
    private void UpdateSandBoxTablet()
    {
      SbLoadPrefs();
      SandBoxWorld world = SandBoxWorld.Ensure();
      // SandBox lobbies: joining, the loading card and the spawn-in effect (SandBoxLobby.cs)
      try { SbUpdateLobby(); } catch (Exception ex) { SbLog.Throttled("SandBox lobby", ex); }
      if (!sbHooksSet)
      {
        sbHooksSet = true;
        SandBoxWorld.HandProvider = right => this.GetTabletHand(right);
        SandBoxWorld.HeadProvider = () => this.GetTabletHead();
        SandBoxWorld.PointerDirProvider = right =>
        {
          Vector3 o, d;
          return this.TryGetStableTabletPointerPose(right, null, out o, out d) ? d : Vector3.zero;
        };
        SandBoxWorld.Haptic = (right, s, d) => this.PulseTabletHaptic(right, s, d);
        SandBoxWorld.Toast = (t, m) => this.ShowToast(t, m);
      }

      TabletBind bind = (TabletBind)Mathf.Clamp(sbOpenBind, 0, 7);
      bool down = this.GetTabletBindDown(bind);
      if ((bind == TabletBind.LeftGrip || bind == TabletBind.RightGrip) && world.IsHolding(bind == TabletBind.RightGrip))
        down = false;
      // switched off in the desktop window: close it and ignore its button and key
      if (!sbTabletEnabled)
      {
        if (sbVisible) SbToggle();
        down = false;
      }
      if (down && !sbOpenWasDown) SbToggle();
      sbOpenWasDown = down;
      if (sbTabletEnabled && TabletKeyPressed()) SbToggle();

      // the Gorilla Ragdoll's icon and ghost are baked from the real gorilla
      if (sbVisible && SbRagdoll.RefreshPreview(SbCatalog.Get("ob_ragdoll")) && sbContent != null)
      {
        if (sbTab == SbTab.Objects && !sbSettingsOpen) SbRebuildGrid();
        SbRefreshRight();
      }
      // a new accent colour rebuilds the panels once the wheel is let go
      if (sbAccentApplyAt > 0f && Time.unscaledTime >= sbAccentApplyAt && sbDragCtl == null && sbFingerL == null && sbFingerR == null)
      {
        sbAccentApplyAt = 0f;
        SbSavePrefs();
        float keepScroll = sbScrollTarget;
        if (sbRoot != null) SbRebuildStyle();
        sbScroll = sbScrollTarget = keepScroll;
      }
      SbAnimate();
      SbInteract(world);
      SbPointerTick();
      // what others see of your tablet, pointer and preview (SandBoxPresence.cs)
      try { SbPresenceTick(); } catch (Exception ex) { SbLog.Throttled("SandBox presence", ex); }
      SbProcessIconQueue();
      if (sbPrefsDirty && sbDragCtl == null && sbFingerL == null && sbFingerR == null && Time.unscaledTime >= sbPrefsSaveAt)
      {
        sbPrefsDirty = false;
        SbSavePrefs();
      }
      if (sbRoot != null && sbRoot.activeSelf && Time.unscaledTime >= sbNextStatus)
      {
        sbNextStatus = Time.unscaledTime + 0.25f;
        SbRefreshStatus(world);
      }
    }

    // from the desktop window
    private void ToggleSandBoxTablet()
    {
      SbLoadPrefs();
      if (!sbTabletEnabled && !sbVisible) return;
      SandBoxWorld.Ensure();
      SbToggle();
    }

    private void SbToggle()
    {
      sbVisible = !sbVisible;
      sbAnimTime = 0f;
      Transform head = this.GetTabletHead();
      if (sbVisible)
      {
        SbEnsureUi();
        if (head != null)
        {
          sbRootPos = head.position;
          Vector3 f = Vector3.ProjectOnPlane(head.forward, Vector3.up);
          sbRootYaw = f.sqrMagnitude > 1e-4f ? Mathf.Atan2(f.x, f.z) * Mathf.Rad2Deg : 0f;
        }
        sbRoot.SetActive(true);
        Vector3 chest = SbChest();
        SbFx.Play(SbSound.UiOpen, chest, 0.6f);
        SbStyle st = SbStyles.Get(sbStyleIdx);
        SbFx.Burst(chest, 30, st.Accent, st.Accent2, 0.4f, 1.4f, 0.01f, 0.025f, 0.3f, 0.6f, 0f, true, 0.05f, SbForward(), 35f, 2f);
        this.PulseTabletHaptic(true, 0.25f, 0.08f);
        this.PulseTabletHaptic(false, 0.25f, 0.08f);
      }
      else
      {
        sbDragCtl = null;
        SbFx.Play(SbSound.UiClose, SbChest(), 0.55f);
      }
    }

    private Vector3 SbForward()
    {
      return Quaternion.Euler(0f, sbRootYaw, 0f) * Vector3.forward;
    }

    private Vector3 SbChest()
    {
      Transform head = this.GetTabletHead();
      if (head == null) return sbRootPos;
      float scale = PlayerScale;
      Vector3 f = Vector3.ProjectOnPlane(head.forward, Vector3.up);
      if (f.sqrMagnitude < 1e-4f) f = SbForward();
      return head.position - Vector3.up * 0.33f * scale + f.normalized * 0.10f * scale;
    }

    // ================================================================ building the panels
    private void SbDestroyUi()
    {
      if (sbRoot != null) Destroy(sbRoot);
      sbRoot = null;
      sbCtls.Clear();
      sbIconQueue.Clear();
      sbHoverCtl = null;
      sbDragCtl = null;
      sbFingerL = sbFingerR = null;
      sbLobbyCtl = null;
      sbHostBtn = null;
      for (int i = 0; i < sbScanlines.Length; i++) sbScanlines[i] = null;
    }

    private void SbEnsureUi()
    {
      if (sbRoot != null) return;
      SbCatalog.Init();
      SbStyle st = SbStyles.Get(sbStyleIdx);
      sbRoot = new GameObject("Unknowns_SandBoxTablet");
      sbRoot.SetActive(false);
      for (int i = 0; i < 3; i++)
      {
        GameObject p = new GameObject(i == 0 ? "SB_Left" : i == 1 ? "SB_Centre" : "SB_Right", typeof(RectTransform));
        p.transform.SetParent(sbRoot.transform, false);
        Canvas c = p.AddComponent<Canvas>();
        c.renderMode = RenderMode.WorldSpace;
        c.sortingOrder = 31030;
        var scaler = p.AddComponent<CanvasScaler>();
        scaler.dynamicPixelsPerUnit = 3f;
        var gr = p.AddComponent<GraphicRaycaster>();
        gr.enabled = false;
        sbGroups[i] = p.AddComponent<CanvasGroup>();
        sbGroups[i].interactable = false;
        sbGroups[i].blocksRaycasts = false;
        RectTransform rt = p.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(SbPanelW[i], SbPanelH);
        sbPanels[i] = p;
        sbPanelRts[i] = rt;
        SbPanelBackground(p.transform, st, SbPanelW[i], SbPanelH, i);

        var beamGo = new GameObject("beam");
        beamGo.transform.SetParent(sbRoot.transform, false);
        var lr = beamGo.AddComponent<LineRenderer>();
        lr.useWorldSpace = true;
        lr.positionCount = 2;
        lr.sharedMaterial = SbMats.Line;
        lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        lr.numCapVertices = 4;
        lr.enabled = false;
        sbBeams[i] = lr;
      }

      sbPortal = GameObject.CreatePrimitive(PrimitiveType.Quad);
      sbPortal.name = "SB_Portal";
      var pc = sbPortal.GetComponent<Collider>(); if (pc != null) Destroy(pc);
      sbPortal.transform.SetParent(sbRoot.transform, false);
      sbPortalMat = new Material(SbMats.Line) { hideFlags = HideFlags.DontSave };
      if (sbPortalMat.HasProperty("_MainTex")) sbPortalMat.SetTexture("_MainTex", SbMats.Ring);
      var prr = sbPortal.GetComponent<Renderer>();
      prr.sharedMaterial = sbPortalMat;
      prr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

      SbBuildLeft(st);
      SbBuildCentre(st);
      SbBuildRight(st);
      SbBuildKeyboard(st);
      SbRebuildGrid();
      SbRefreshRight();
    }

    private void SbRebuildStyle()
    {
      bool wasVisible = sbVisible;
      SbDestroyUi();
      SbEnsureUi();
      if (wasVisible) { sbRoot.SetActive(true); sbAnimTime = 10f; }
    }

    private static GameObject SbRect(string name, Transform parent, Vector2 pos, Vector2 size)
    {
      GameObject go = new GameObject(name, typeof(RectTransform));
      go.transform.SetParent(parent, false);
      RectTransform rt = go.GetComponent<RectTransform>();
      rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
      rt.anchoredPosition = pos;
      rt.sizeDelta = size;
      return go;
    }

    private static Image SbImage(Transform parent, string name, Vector2 pos, Vector2 size, Sprite sprite, Color c)
    {
      GameObject go = SbRect(name, parent, pos, size);
      Image im = go.AddComponent<Image>();
      im.sprite = sprite;
      im.type = sprite != null ? Image.Type.Sliced : Image.Type.Simple;
      im.color = c;
      im.raycastTarget = false;
      return im;
    }

    private static TMP_Text SbText(Transform parent, string text, Vector2 pos, Vector2 size, float fontSize, Color c, TextAlignmentOptions align = TextAlignmentOptions.Center, FontStyles style = FontStyles.Bold, bool wrap = false)
    {
      GameObject go = SbRect("T_" + (text.Length > 12 ? text.Substring(0, 12) : text), parent, pos, size);
      TMP_Text t = go.AddComponent<TextMeshProUGUI>();
      t.text = text;
      t.fontSize = fontSize;
      t.fontStyle = style;
      t.alignment = align;
      t.color = c;
      t.raycastTarget = false;
      t.textWrappingMode = wrap ? TextWrappingModes.Normal : TextWrappingModes.NoWrap;
      t.enableAutoSizing = true;
      t.fontSizeMin = Mathf.Max(8f, fontSize * 0.55f);
      t.fontSizeMax = fontSize;
      t.overflowMode = wrap ? TextOverflowModes.Truncate : TextOverflowModes.Ellipsis;
      return t;
    }

    private void SbPanelBackground(Transform panel, SbStyle st, float w, float h, int index)
    {
      if (st.Glow > 0.01f)
      {
        Color g = st.Accent; g.a = st.Glow;
        SbImage(panel, "glow", Vector2.zero, new Vector2(w + 48f, h + 48f), SbStyles.Shape(st.Radius, st.Cut, st.Pixel, 2), g);
      }
      SbImage(panel, "fill", Vector2.zero, new Vector2(w, h), SbStyles.Shape(st.Radius, st.Cut, st.Pixel, 0), st.Bg);
      SbImage(panel, "grad", Vector2.zero, new Vector2(w, h), SbStyles.Shape(st.Radius, st.Cut, st.Pixel, 3), st.Bg2);
      if (st.Pattern > 0)
      {
        GameObject pg = SbRect("pattern", panel, Vector2.zero, new Vector2(w - st.Radius, h - st.Radius));
        RawImage ri = pg.AddComponent<RawImage>();
        ri.texture = SbStyles.Pattern(st.Pattern);
        ri.uvRect = new Rect(0f, 0f, (w - st.Radius) / 32f, (h - st.Radius) / 32f);
        Color pc = st.Pixel ? new Color(0f, 0f, 0f, st.PatternAlpha) : new Color(st.Accent.r, st.Accent.g, st.Accent.b, st.PatternAlpha);
        ri.color = pc;
        ri.raycastTarget = false;
        if (st.Pattern == 1) sbScanlines[index] = ri;
      }
      SbImage(panel, "border", Vector2.zero, new Vector2(w, h), SbStyles.Shape(st.Radius, st.Cut, st.Pixel, 1, st.Pixel ? 5 : 3), st.Border);
      if (st.Pixel)
      {
        // Minecraft-style bevel: light top/left, dark bottom/right
        SbImage(panel, "bevelT", new Vector2(0f, h * 0.5f - 9f), new Vector2(w - 16f, 4f), null, new Color(1f, 1f, 1f, 0.35f));
        SbImage(panel, "bevelB", new Vector2(0f, -h * 0.5f + 9f), new Vector2(w - 16f, 4f), null, new Color(0f, 0f, 0f, 0.45f));
      }
      if (st.Brackets)
      {
        float bx = w * 0.5f - 10f, by = h * 0.5f - 10f;
        Color bc = st.Accent;
        for (int sx = -1; sx <= 1; sx += 2)
          for (int sy = -1; sy <= 1; sy += 2)
          {
            SbImage(panel, "brk", new Vector2(sx * (bx - 12f), sy * by), new Vector2(26f, 3f), null, bc);
            SbImage(panel, "brk", new Vector2(sx * bx, sy * (by - 12f)), new Vector2(3f, 26f), null, bc);
          }
      }
    }

    private SbCtl SbButton(int panel, Transform parent, string text, Vector2 pos, Vector2 size, Func<bool> on, Action click, float fontSize = 18f, SbStyle st = null)
    {
      st = st ?? SbStyles.Get(sbStyleIdx);
      GameObject go = SbRect("B_" + text, parent, pos, size);
      var ctl = new SbCtl { rt = go.GetComponent<RectTransform>(), panel = panel, click = click, on = on };
      int rad = Mathf.Min(st.Radius, (int)(size.y * 0.5f) - 2);
      ctl.bg = go.AddComponent<Image>();
      ctl.bg.sprite = SbStyles.Shape(Mathf.Max(0, rad), st.Cut, st.Pixel, 0);
      ctl.bg.type = Image.Type.Sliced;
      ctl.bg.raycastTarget = false;
      ctl.baseCol = st.Card;
      ctl.hoverCol = st.CardHover;
      ctl.onCol = Color.Lerp(st.Card, st.Accent, 0.55f);
      ctl.bg.color = ctl.baseCol;
      ctl.edge = SbImage(go.transform, "edge", Vector2.zero, size, SbStyles.Shape(Mathf.Max(0, rad), st.Cut, st.Pixel, 1, 2), new Color(st.Accent.r, st.Accent.g, st.Accent.b, 0f));
      if (!string.IsNullOrEmpty(text))
        ctl.label = SbText(go.transform, text, Vector2.zero, size - new Vector2(14f, 6f), fontSize, st.Text);
      sbCtls.Add(ctl);
      return ctl;
    }

    private SbCtl SbIconButton(int panel, Transform parent, string icon, Vector2 pos, Vector2 size, Action click)
    {
      SbCtl c = SbButton(panel, parent, "", pos, size, null, click);
      GameObject ig = SbRect("icon", c.rt, Vector2.zero, size * 0.62f);
      RawImage ri = ig.AddComponent<RawImage>();
      ri.texture = SbStyles.Icon(icon);
      ri.color = SbStyles.Get(sbStyleIdx).Text;
      ri.raycastTarget = false;
      return c;
    }

    private SbCtl SbSlider(int panel, Transform parent, Vector2 pos, float width, Func<float> get, Action<float> set)
    {
      SbStyle st = SbStyles.Get(sbStyleIdx);
      GameObject go = SbRect("Slider", parent, pos, new Vector2(width, 34f));
      var ctl = new SbCtl { rt = go.GetComponent<RectTransform>(), panel = panel, slider = true, get = get, set = set };
      Image hit = go.AddComponent<Image>();
      hit.color = new Color(0f, 0f, 0f, 0f);
      hit.raycastTarget = false;
      ctl.bg = hit;
      ctl.baseCol = ctl.hoverCol = ctl.onCol = new Color(0f, 0f, 0f, 0f);
      Image track = SbImage(go.transform, "track", Vector2.zero, new Vector2(width, 8f), SbStyles.Shape(st.Pixel ? 0 : 4, false, st.Pixel, 0), new Color(st.Sub.r, st.Sub.g, st.Sub.b, 0.35f));
      ctl.track = track.rectTransform;
      Image fill = SbImage(go.transform, "fill", new Vector2(-width * 0.5f, 0f), new Vector2(0f, 8f), SbStyles.Shape(st.Pixel ? 0 : 4, false, st.Pixel, 0), st.Accent);
      fill.rectTransform.pivot = new Vector2(0f, 0.5f);
      ctl.fill = fill.rectTransform;
      Image knob = SbImage(go.transform, "knob", Vector2.zero, new Vector2(22f, 22f), SbStyles.Shape(st.Pixel ? 0 : 11, false, st.Pixel, 0), st.Text);
      ctl.knob = knob.rectTransform;
      ctl.edge = SbImage(knob.transform, "ring", Vector2.zero, new Vector2(28f, 28f), SbStyles.Shape(st.Pixel ? 0 : 14, false, st.Pixel, 1, 3), new Color(st.Accent.r, st.Accent.g, st.Accent.b, 0f));
      sbCtls.Add(ctl);
      SbUpdateSlider(ctl);
      return ctl;
    }

    private static void SbUpdateSlider(SbCtl c)
    {
      if (c == null || !c.slider || c.get == null) return;
      float v = Mathf.Clamp01(c.get());
      float w = c.rt.sizeDelta.x;
      if (c.fill != null) c.fill.sizeDelta = new Vector2(w * v, c.fill.sizeDelta.y);
      if (c.knob != null) c.knob.anchoredPosition = new Vector2(-w * 0.5f + w * v, 0f);
    }

    // ---------------------------------------------------------------- left
    private void SbBuildLeft(SbStyle st)
    {
      Transform p = sbPanels[0].transform;
      float w = SbPanelW[0];
      RawImage logo = SbRect("logo", p, new Vector2(-w * 0.5f + 40f, 238f), new Vector2(44f, 44f)).AddComponent<RawImage>();
      logo.texture = SbStyles.Icon("ring");
      logo.color = st.Accent;
      logo.raycastTarget = false;
      SbText(p, "SANDBOX", new Vector2(22f, 246f), new Vector2(200f, 40f), 34f, st.Text, TextAlignmentOptions.Left);
      SbText(p, "TABLET", new Vector2(22f, 218f), new Vector2(200f, 22f), 15f, st.Accent, TextAlignmentOptions.Left, FontStyles.Bold);
      SbImage(p, "line", new Vector2(0f, 196f), new Vector2(w - 40f, 2f), null, new Color(st.Accent.r, st.Accent.g, st.Accent.b, 0.6f));

      string[] icons = { "mc_grass", "ob_beachball", "gun_pistol" };
      for (int i = 0; i < 3; i++)
      {
        int idx = i;
        SbCtl tab = SbButton(0, p, "", new Vector2(0f, 160f - i * 56f), new Vector2(w - 40f, 50f), () => sbTab == (SbTab)idx && !sbSettingsOpen && !sbServerOpen, () =>
        {
          sbTab = (SbTab)idx;
          sbSettingsOpen = false;
          sbServerOpen = false;
          sbCatFilter = null;
          sbCatPage = 0;
          sbScroll = sbScrollTarget = 0f;
          SbRebuildGrid();
        });
        SbItem rep = SbCatalog.Get(icons[i]);
        RawImage ic = SbRect("icon", tab.rt, new Vector2(-(w - 40f) * 0.5f + 38f, 0f), new Vector2(40f, 40f)).AddComponent<RawImage>();
        ic.raycastTarget = false;
        if (rep != null) sbIconQueue.Add(new KeyValuePair<RawImage, SbItem>(ic, rep));
        SbText(tab.rt, SbCatalog.TabNames[i], new Vector2(34f, 0f), new Vector2(w - 130f, 40f), 21f, st.Text, TextAlignmentOptions.Left);
      }
      // SERVERS (SandBoxServers.cs)
      SbCtl srv = SbButton(0, p, "", new Vector2(0f, 160f - 3 * 56f), new Vector2(w - 40f, 50f), () => sbServerOpen && !sbSettingsOpen, () =>
      {
        sbServerOpen = true;
        sbSettingsOpen = false;
        sbScroll = sbScrollTarget = 0f;
        SbRebuildGrid();
      });
      RawImage srvIc = SbRect("icon", srv.rt, new Vector2(-(w - 40f) * 0.5f + 38f, 0f), new Vector2(36f, 36f)).AddComponent<RawImage>();
      srvIc.texture = SbStyles.Icon("ring");
      srvIc.color = st.Accent;
      srvIc.raycastTarget = false;
      SbText(srv.rt, "SERVERS", new Vector2(34f, 0f), new Vector2(w - 130f, 40f), 21f, st.Text, TextAlignmentOptions.Left);

      SbImage(p, "line2", new Vector2(0f, -38f), new Vector2(w - 40f, 2f), null, new Color(st.Accent.r, st.Accent.g, st.Accent.b, 0.35f));
      sbStatusDot = SbRect("dot", p, new Vector2(-w * 0.5f + 32f, -66f), new Vector2(14f, 14f)).AddComponent<Image>();
      sbStatusDot.sprite = SbStyles.Shape(7, false, st.Pixel, 0);
      sbStatusDot.raycastTarget = false;
      sbStatusText = SbText(p, "COLLISION", new Vector2(12f, -66f), new Vector2(w - 70f, 26f), 15f, st.Text, TextAlignmentOptions.Left);
      sbPlacedText = SbText(p, "PLACED 0", new Vector2(0f, -94f), new Vector2(w - 40f, 22f), 14f, st.Sub, TextAlignmentOptions.Left, FontStyles.Normal);

      float half = (w - 52f) * 0.5f;
      SbButton(0, p, "UNDO", new Vector2(-(half * 0.5f + 6f), -136f), new Vector2(half, 46f), null, () => SandBoxWorld.Instance?.Undo(), 17f);
      SbButton(0, p, "CLEAR MINE", new Vector2(half * 0.5f + 6f, -136f), new Vector2(half, 46f), null, () =>
      {
        SandBoxWorld.Instance?.ClearMine();
        this.ShowToast("SandBox", "Removed everything you placed");
      }, 16f);
      SbCtl det = SbButton(0, p, "DETONATE ALL", new Vector2(-(half * 0.5f + 6f), -188f), new Vector2(half, 46f), null, () => SandBoxWorld.Instance?.DetonateAll(), 16f);
      det.baseCol = Color.Lerp(st.Card, new Color(0.85f, 0.15f, 0.12f, st.Card.a), 0.55f);
      det.hoverCol = Color.Lerp(st.CardHover, new Color(1f, 0.25f, 0.2f, st.Card.a), 0.6f);
      SbButton(0, p, "STOP BUILDING", new Vector2(half * 0.5f + 6f, -188f), new Vector2(half, 46f), () => sbSelected == null, () =>
      {
        sbSelected = null;
        SbRefreshRight();
      }, 16f);
      // SandBox lobbies (SandBoxLobby.cs): _SANDBOX, then _SANDBOX1, 2 ... when full
      sbLobbyCtl = SbButton(0, p, SbLobbyButtonText(), new Vector2(0f, -240f), new Vector2(w - 40f, 46f), null, SbQuickCreateRoom, 16f);   // creates a public room
      sbLobbyCtl.baseCol = Color.Lerp(st.Card, st.Accent, 0.3f);
      sbLobbyCtl.hoverCol = Color.Lerp(st.CardHover, st.Accent, 0.45f);
    }

    // ---------------------------------------------------------------- centre
    private void SbBuildCentre(SbStyle st)
    {
      Transform p = sbPanels[1].transform;
      float w = SbPanelW[1];
      sbHeaderText = SbText(p, "MINECRAFT", new Vector2(-w * 0.5f + 160f, 244f), new Vector2(280f, 40f), 30f, st.Text, TextAlignmentOptions.Left);
      sbCountText = SbText(p, "", new Vector2(70f, 244f), new Vector2(170f, 30f), 15f, st.Sub, TextAlignmentOptions.Right, FontStyles.Normal);
      SbIconButton(1, p, "up", new Vector2(w * 0.5f - 112f, 244f), new Vector2(46f, 40f), () => sbScrollTarget -= 300f);
      SbIconButton(1, p, "down", new Vector2(w * 0.5f - 60f, 244f), new Vector2(46f, 40f), () => sbScrollTarget += 300f);
      SbBuildServerHeaderButton(p, w, st);
      SbImage(p, "line", new Vector2(0f, 216f), new Vector2(w - 40f, 2f), null, new Color(st.Accent.r, st.Accent.g, st.Accent.b, 0.6f));
      // the category bar (SbBuildCategoryBar)
      sbCatBar = SbRect("catbar", p, new Vector2(0f, 194f), new Vector2(w - 40f, 32f)).GetComponent<RectTransform>();

      GameObject vp = SbRect("viewport", p, new Vector2(-8f, SbViewY), new Vector2(w - 56f, SbViewH));
      vp.AddComponent<RectMask2D>();
      sbViewport = vp.GetComponent<RectTransform>();
      GameObject content = SbRect("content", vp.transform, Vector2.zero, new Vector2(w - 56f, 100f));
      sbContent = content.GetComponent<RectTransform>();
      sbContent.anchorMin = sbContent.anchorMax = new Vector2(0.5f, 1f);
      sbContent.pivot = new Vector2(0.5f, 1f);
      sbContent.anchoredPosition = Vector2.zero;

      sbTrack = SbImage(p, "track", new Vector2(w * 0.5f - 22f, SbViewY), new Vector2(6f, SbViewH), SbStyles.Shape(st.Pixel ? 0 : 3, false, st.Pixel, 0), new Color(st.Sub.r, st.Sub.g, st.Sub.b, 0.25f)).rectTransform;
      sbThumb = SbImage(p, "thumb", new Vector2(w * 0.5f - 22f, 0f), new Vector2(8f, 80f), SbStyles.Shape(st.Pixel ? 0 : 4, false, st.Pixel, 0), st.Accent).rectTransform;
      // the scroll bar is draggable: an invisible slider over it
      SbCtl drag = new SbCtl { rt = SbRect("scrollhit", p, new Vector2(w * 0.5f - 22f, SbViewY), new Vector2(34f, SbViewH)).GetComponent<RectTransform>(), panel = 1, slider = true };
      drag.get = () => sbContentHeight <= SbViewH ? 0f : sbScroll / (sbContentHeight - SbViewH);
      drag.set = v => { sbScrollTarget = (1f - v) * Mathf.Max(0f, sbContentHeight - SbViewH); sbScroll = sbScrollTarget; };
      drag.baseCol = drag.hoverCol = drag.onCol = Color.clear;
      sbCtls.Add(drag);
    }

    private void SbClearScrollContent()
    {
      for (int i = sbCtls.Count - 1; i >= 0; i--) if (sbCtls[i].inScroll) sbCtls.RemoveAt(i);
      sbIconQueue.RemoveAll(kv => kv.Key == null || kv.Key.transform.IsChildOf(sbContent));
      for (int i = sbContent.childCount - 1; i >= 0; i--) Destroy(sbContent.GetChild(i).gameObject);
      sbHoverCtl = null;
    }

    private SbCtl SbMark(SbCtl c) { c.inScroll = true; return c; }

    private void SbRebuildGrid()
    {
      if (sbContent == null) return;
      SbClearScrollContent();
      SbUpdateServerHeader();
      SbStyle st = SbStyles.Get(sbStyleIdx);
      SbClearCategoryBar();
      if (sbSettingsOpen) { SbBuildSettings(st); return; }
      if (sbServerOpen) { SbBuildServers(st); return; }
      sbHeaderText.text = SbCatalog.TabNames[(int)sbTab];
      List<SbItem> items = SbCatalog.ForTab(sbTab);
      SbBuildCategoryBar(st, items);
      // one category, or ALL
      if (sbCatFilter != null) items = items.FindAll(i => i.Section == sbCatFilter);
      sbCountText.text = items.Count + " ITEMS";
      float cw = 132f, gap = 12f;
      float y = -6f;
      string section = null;
      int col = 0;
      foreach (SbItem item in items)
      {
        if (item.Section != section)
        {
          if (col != 0) { y -= cw + gap; col = 0; }
          section = item.Section;
          TMP_Text sl = SbText(sbContent, section, new Vector2(-150f, y - 16f), new Vector2(270f, 28f), 17f, st.Accent, TextAlignmentOptions.Left);
          SbAnchorTop(sl.rectTransform);
          Image ln = SbImage(sbContent, "secline", new Vector2(130f, y - 16f), new Vector2(300f, 2f), null, new Color(st.Accent.r, st.Accent.g, st.Accent.b, 0.3f));
          SbAnchorTop(ln.rectTransform);
          y -= 36f;
        }
        float x = -(cw + gap) * 1.5f + col * (cw + gap);
        SbItem captured = item;
        SbCtl card = SbMark(SbButton(1, sbContent, "", new Vector2(x, y - cw * 0.5f), new Vector2(cw, cw), () => sbSelected == captured, () =>
        {
          sbSelected = sbSelected == captured ? null : captured;
          // the build gun places the last block picked
          if (captured.Kind == SbKind.Block) { SandBoxWorld.BuildGunBlock = captured.Id; SandBoxWorld.BuildGunSize = sbBlockIdx; }
          sbYawOffset = 0f;
          if (captured.Kind != SbKind.Block && captured.Kind != SbKind.Fluid) sbAnchored = captured.DefaultAnchored || captured.IsHeldType;
          SbRefreshRight();
        }));
        card.card = true;
        SbAnchorTop(card.rt);
        RawImage ic = SbRect("icon", card.rt, new Vector2(0f, 14f), new Vector2(88f, 88f)).AddComponent<RawImage>();
        ic.raycastTarget = false;
        ic.color = new Color(1f, 1f, 1f, 0f);
        sbIconQueue.Add(new KeyValuePair<RawImage, SbItem>(ic, item));
        SbText(card.rt, item.Name.ToUpperInvariant(), new Vector2(0f, -46f), new Vector2(cw - 12f, 30f), 13f, st.Text, TextAlignmentOptions.Center, FontStyles.Bold, true);
        col++;
        if (col == 4) { col = 0; y -= cw + gap; }
      }
      if (col != 0) y -= cw + gap;
      sbContentHeight = -y + 10f;
      sbContent.sizeDelta = new Vector2(sbContent.sizeDelta.x, sbContentHeight);
      sbScrollTarget = Mathf.Clamp(sbScrollTarget, 0f, Mathf.Max(0f, sbContentHeight - SbViewH));
    }

    private static void SbAnchorTop(RectTransform rt)
    {
      Vector2 pos = rt.anchoredPosition;
      rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
      rt.anchoredPosition = pos;
    }

    // ---------------------------------------------------------------- settings page (centre)
    private void SbBuildSettings(SbStyle st)
    {
      sbHeaderText.text = "SETTINGS";
      sbCountText.text = "";
      float y = -6f;
      float w = sbContent.sizeDelta.x;

      Action<string> section = s =>
      {
        TMP_Text sl = SbText(sbContent, s, new Vector2(-w * 0.5f + 150f, y - 16f), new Vector2(280f, 28f), 17f, st.Accent, TextAlignmentOptions.Left);
        SbAnchorTop(sl.rectTransform);
        y -= 40f;
      };

      section("UI STYLE");
      for (int i = 0; i < SbStyles.All.Count; i++)
      {
        int idx = i;
        SbStyle s = SbStyles.All[i];
        float x = -w * 0.5f + 10f + 92f + (i % 3) * 190f;
        float yy = y - 34f - (i / 3) * 80f;
        SbCtl c = SbMark(SbButton(1, sbContent, "", new Vector2(x, yy), new Vector2(178f, 68f), () => sbStyleIdx == idx, () =>
        {
          if (sbStyleIdx == idx) return;
          sbStyleIdx = idx;
          SbSavePrefs();
          SbRebuildStyle();
        }));
        SbAnchorTop(c.rt);
        SbImage(c.rt, "sw1", new Vector2(-62f, 8f), new Vector2(34f, 22f), SbStyles.Shape(Mathf.Min(s.Radius, 8), s.Cut, s.Pixel, 0), new Color(s.Bg.r, s.Bg.g, s.Bg.b, 1f));
        SbImage(c.rt, "sw2", new Vector2(-62f, -14f), new Vector2(34f, 10f), null, s.Accent);
        SbText(c.rt, s.Name, new Vector2(22f, 0f), new Vector2(120f, 30f), 17f, st.Text, TextAlignmentOptions.Left);
      }
      y -= 34f + 80f * Mathf.Ceil(SbStyles.All.Count / 3f) - 30f;

      section("ACCENT COLOUR");
      SbSettingToggle(st, ref y, "CUSTOM ACCENT COLOUR", () => SbStyles.UseCustomAccent, () => SbSetAccent(!SbStyles.UseCustomAccent, SbStyles.CustomAccent));
      Image swatch = SbImage(sbContent, "swatch", new Vector2(-w * 0.5f + 15f + 40f, y - 30f), new Vector2(80f, 44f), SbStyles.Shape(Mathf.Min(st.Radius, 12), st.Cut, st.Pixel, 0), SbStyles.CustomAccent);
      SbAnchorTop(swatch.rectTransform);
      TMP_Text accentInfo = SbText(sbContent, "Pick the colour in the desktop window. Off: the style keeps its own colours.",
        new Vector2(50f, y - 30f), new Vector2(w - 140f, 44f), 14f, st.Sub, TextAlignmentOptions.Left, FontStyles.Normal, true);
      SbAnchorTop(accentInfo.rectTransform);
      y -= 64f;

      // Minecraft's own textures by default, or a pack from the packs folder
      section("TEXTURES");
      SbSettingCycle(st, ref y, "TEXTURE PACK", () => SbMinecraft.PackLabel(SbMinecraft.Pack), SbNextTexturePack);
      TMP_Text packInfo = SbText(sbContent,
        "Packs: put Minecraft resource pack .zip files in Gorilla Tag / Unknowns Sandbox / minecraft / packs, then pick one here. " +
        "The template pack there is the default. What a pack doesn't have comes from the other packs.",
        new Vector2(0f, y - 22f), new Vector2(w - 30f, 40f), 13f, st.Sub, TextAlignmentOptions.TopLeft, FontStyles.Normal, true);
      SbAnchorTop(packInfo.rectTransform);
      y -= 48f;

      section("LAYOUT");
      SbSettingSlider(st, ref y, "UI SIZE", () => (sbUiSize - 0.6f) / 0.9f, v => { sbUiSize = Mathf.Lerp(0.6f, 1.5f, v); }, () => sbUiSize.ToString("0.00") + "x");
      SbSettingSlider(st, ref y, "UI DISTANCE", () => (sbUiDist - 0.4f) / 0.8f, v => { sbUiDist = Mathf.Lerp(0.4f, 1.2f, v); }, () => (sbUiDist * 100f).ToString("0") + " cm");
      SbSettingSlider(st, ref y, "UI HEIGHT", () => (sbUiHeight + 0.4f) / 0.6f, v => { sbUiHeight = Mathf.Lerp(-0.4f, 0.2f, v); }, () => (sbUiHeight * 100f).ToString("0") + " cm");
      SbSettingSlider(st, ref y, "FOLLOW SMOOTHNESS", () => sbFollowSmooth, v => { sbFollowSmooth = v; }, () => sbFollowSmooth < 0.2f ? "SNAPPY" : sbFollowSmooth > 0.75f ? "FLOATY" : Mathf.RoundToInt(sbFollowSmooth * 100f) + "%");

      section("CONTROLS");
      SbSettingCycle(st, ref y, "OPEN / CLOSE", () => TabletBindName((TabletBind)sbOpenBind).ToUpperInvariant(), () => { sbOpenBind = (sbOpenBind + 1) % 8; });
      SbSettingCycle(st, ref y, "PLACE", () => SbInputName(sbPlaceInput), () => { sbPlaceInput = (sbPlaceInput + 1) % 4; if (sbPlaceInput == (int)SbInputKind.Grip) sbPlaceInput = (sbPlaceInput + 1) % 4; });
      SbSettingCycle(st, ref y, "POINTER HAND", () => sbPointerRight ? "RIGHT" : "LEFT", () => { sbPointerRight = !sbPointerRight; });

      section("OPTIONS");
      SbSettingToggle(st, ref y, "UI FOLLOWS YOU", () => sbFollow, () => sbFollow = !sbFollow);
      SbSettingToggle(st, ref y, "KEEP BUILDING WHEN CLOSED", () => sbKeepBuilding, () => sbKeepBuilding = !sbKeepBuilding);
      SbSettingToggle(st, ref y, "SNAP OBJECTS TO 5 CM", () => sbSnap, () => sbSnap = !sbSnap);
      SbSettingToggle(st, ref y, "PLACEMENT PREVIEW", () => sbGhostOn, () => sbGhostOn = !sbGhostOn);
      SbSettingToggle(st, ref y, "SHOW OTHER PLAYERS' OBJECTS", () => sbShowOthers, () =>
      {
        sbShowOthers = !sbShowOthers;
        SandBoxWorld.ShowOthers = sbShowOthers;
        SandBoxWorld.Instance?.ApplyShowOthers();
      });
      SbSettingToggle(st, ref y, "SHOW OTHER PLAYERS' TABLETS", () => sbShowOthersTablet, () => sbShowOthersTablet = !sbShowOthersTablet);
      SbSettingToggle(st, ref y, "SHOW OTHER PLAYERS' POINTERS", () => sbShowOthersPointer, () => sbShowOthersPointer = !sbShowOthersPointer);
      SbSettingToggle(st, ref y, "BLOOD WHEN PLAYERS ARE HIT", () => SandBoxWorld.BloodOn, () => SandBoxWorld.BloodOn = !SandBoxWorld.BloodOn);
      SbSettingToggle(st, ref y, "SHOW OTHER PLAYERS' PREVIEWS", () => sbShowOthersPreview, () => sbShowOthersPreview = !sbShowOthersPreview);
      SbSettingToggle(st, ref y, "PLAYER JOIN / LEAVE EFFECT (EVERY LOBBY)", () => sbSpawnFxOn, () => sbSpawnFxOn = !sbSpawnFxOn);
      // how far the Nether is built around you (lower = smoother)
      SbSettingSlider(st, ref y, "NETHER RENDER DISTANCE", () => (SandBoxWorld.NetherViewChunks - 1) / 5f, v => { SandBoxWorld.NetherViewChunks = Mathf.Clamp(1 + Mathf.RoundToInt(v * 5f), 1, 6); }, () => SandBoxWorld.NetherViewChunks + " (" + (SandBoxWorld.NetherViewChunks * 16) + " M)");
      SbSettingSlider(st, ref y, "SOUND VOLUME", () => sbVolume, v => { sbVolume = v; SbFx.Volume = v; }, () => Mathf.RoundToInt(sbVolume * 100f) + "%");

      section("HOW IT WORKS");
      TMP_Text info = SbText(sbContent,
        "Other SandBox players see what you place. You can stand on and bump into objects only in Utilla modded lobbies or SandBox lobbies, " +
        "or when everyone in the lobby has the SandBox (or when you are offline). Anywhere else they are see-through.\n" +
        "While aiming, the OTHER hand's stick rotates (left/right) and resizes (up/down). Pointing at the middle display, your stick scrolls. " +
        "Grip near a gun, sword, bomb or object to hold it, trigger to use it. Anchored things stay where you let go; the rest gets thrown. " +
        "Hold grip (away from your things) and pull the trigger to delete what you point at.\n" +
        "JOIN SANDBOX LOBBY joins _SANDBOX (or _SANDBOX1, 2 ... when it is full). The in-game computer can't type the _, so only SandBox players get in.\n" +
        "Thrusters, wheels and balloons placed on one of your objects stick to it and move it (touch a thruster or wheel to switch it). " +
        "12 End Portal Frames with an Eye of Ender each (a 5 x 5 ring, no corners) open a portal to the End. Everyone shares one Nether and one End.",
        new Vector2(0f, y - 115f), new Vector2(w - 30f, 230f), 14f, st.Sub, TextAlignmentOptions.TopLeft, FontStyles.Normal, true);
      SbAnchorTop(info.rectTransform);
      y -= 240f;

      sbContentHeight = -y + 10f;
      sbContent.sizeDelta = new Vector2(sbContent.sizeDelta.x, sbContentHeight);
      sbScrollTarget = Mathf.Clamp(sbScrollTarget, 0f, Mathf.Max(0f, sbContentHeight - SbViewH));
    }

    // the built-in textures, then each pack in the packs folder, then the plain colours
    private void SbNextTexturePack()
    {
      var choices = new List<string> { "" };
      foreach (string p in SbMinecraft.AvailablePacks()) if (p != SbMinecraft.DefaultPack()) choices.Add(p);
      choices.Add(SbMinecraft.BuiltIn);
      int at = Mathf.Max(0, choices.IndexOf(SbMinecraft.Pack));
      SbMinecraft.UsePack(choices[(at + 1) % choices.Count]);
      ShowToast("Texture pack", SbMinecraft.PackLabel(SbMinecraft.Pack) + "  (" + SbMinecraft.TextureSource + ")");
    }

    private void SbSettingSlider(SbStyle st, ref float y, string label, Func<float> get, Action<float> set, Func<string> value)
    {
      float w = sbContent.sizeDelta.x;
      TMP_Text l = SbText(sbContent, label, new Vector2(-w * 0.5f + 120f, y - 22f), new Vector2(220f, 30f), 16f, st.Text, TextAlignmentOptions.Left);
      SbAnchorTop(l.rectTransform);
      TMP_Text v = SbText(sbContent, value(), new Vector2(w * 0.5f - 50f, y - 22f), new Vector2(90f, 30f), 15f, st.Sub, TextAlignmentOptions.Right, FontStyles.Normal);
      SbAnchorTop(v.rectTransform);
      SbCtl s = SbMark(SbSlider(1, sbContent, new Vector2(60f, y - 22f), 220f, get, val =>
      {
        set(val);
        v.text = value();
      }));
      SbAnchorTop(s.rt);
      y -= 46f;
    }

    private void SbSettingCycle(SbStyle st, ref float y, string label, Func<string> value, Action next)
    {
      float w = sbContent.sizeDelta.x;
      TMP_Text l = SbText(sbContent, label, new Vector2(-w * 0.5f + 120f, y - 24f), new Vector2(220f, 30f), 16f, st.Text, TextAlignmentOptions.Left);
      SbAnchorTop(l.rectTransform);
      SbCtl b = null;
      b = SbMark(SbButton(1, sbContent, value(), new Vector2(110f, y - 24f), new Vector2(300f, 42f), null, () =>
      {
        next();
        SbSavePrefs();
        if (b != null && b.label != null) b.label.text = value();
        SbRefreshRight();
      }, 16f));
      SbAnchorTop(b.rt);
      y -= 50f;
    }

    private void SbSettingToggle(SbStyle st, ref float y, string label, Func<bool> get, Action flip)
    {
      float w = sbContent.sizeDelta.x;
      SbCtl b = SbMark(SbButton(1, sbContent, "", new Vector2(0f, y - 24f), new Vector2(w - 30f, 42f), get, () => { flip(); SbSavePrefs(); }, 16f));
      SbAnchorTop(b.rt);
      TMP_Text l = SbText(b.rt, label, new Vector2(-40f, 0f), new Vector2(w - 130f, 30f), 16f, st.Text, TextAlignmentOptions.Left);
      TMP_Text state = SbText(b.rt, "", new Vector2((w - 30f) * 0.5f - 46f, 0f), new Vector2(70f, 30f), 15f, st.Accent, TextAlignmentOptions.Right);
      b.stateText = state;
      y -= 50f;
    }

    // ---------------------------------------------------------------- right
    private void SbBuildRight(SbStyle st)
    {
      Transform p = sbPanels[2].transform;
      float w = SbPanelW[2];
      SbText(p, "SELECTED", new Vector2(-72f, 244f), new Vector2(120f, 30f), 18f, st.Sub, TextAlignmentOptions.Left);
      // SERVER HOST, next to the settings icon, only while you host a server (SandBoxServers.cs)
      sbHostBtn = SbButton(2, p, "HOST", new Vector2(w * 0.5f - 104f, 244f), new Vector2(60f, 50f), () => sbSrvHosting && sbServerOpen, SbOpenHostPage, 14f);
      sbHostBtn.rt.gameObject.SetActive(false);
      SbIconButton(2, p, "gear", new Vector2(w * 0.5f - 40f, 244f), new Vector2(50f, 50f), () =>
      {
        sbSettingsOpen = !sbSettingsOpen;
        if (sbSettingsOpen) sbServerOpen = false;
        sbScroll = sbScrollTarget = 0f;
        SbRebuildGrid();
      }).on = () => sbSettingsOpen;
      SbImage(p, "line", new Vector2(0f, 214f), new Vector2(w - 40f, 2f), null, new Color(st.Accent.r, st.Accent.g, st.Accent.b, 0.6f));

      RawImage ringRi = SbRect("ring", p, new Vector2(0f, 120f), new Vector2(190f, 190f)).AddComponent<RawImage>();
      ringRi.texture = SbMats.Ring;
      ringRi.color = new Color(st.Accent.r, st.Accent.g, st.Accent.b, 0.45f);
      ringRi.raycastTarget = false;
      sbSelRing = ringRi.rectTransform;
      sbSelIcon = SbRect("sel", p, new Vector2(0f, 120f), new Vector2(160f, 160f)).AddComponent<RawImage>();
      sbSelIcon.raycastTarget = false;
      sbSelName = SbText(p, "NOTHING SELECTED", new Vector2(0f, 14f), new Vector2(w - 40f, 34f), 22f, st.Text);
      sbSelDesc = SbText(p, "Pick something from the middle display.", new Vector2(0f, -28f), new Vector2(w - 44f, 52f), 12.5f, st.Sub, TextAlignmentOptions.Top, FontStyles.Normal, true);

      sbAnchorCtl = SbButton(2, p, "ANCHORED", new Vector2(0f, -80f), new Vector2(w - 40f, 42f), () => sbAnchored, () => { sbAnchored = !sbAnchored; SbSavePrefs(); SbRefreshRight(); }, 17f);
      sbSizeText = SbText(p, "SIZE 1.00x", new Vector2(0f, -116f), new Vector2(w - 40f, 24f), 15f, st.Text);
      sbMinusCtl = SbButton(2, p, "-", new Vector2(-w * 0.5f + 40f, -148f), new Vector2(40f, 40f), null, () => SbStepSize(-1), 24f);
      sbPlusCtl = SbButton(2, p, "+", new Vector2(w * 0.5f - 40f, -148f), new Vector2(40f, 40f), null, () => SbStepSize(1), 24f);
      sbSizeCtl = SbSlider(2, p, new Vector2(0f, -148f), w - 140f, () =>
      {
        if (sbSelected != null && (sbSelected.Kind == SbKind.Block || sbSelected.Kind == SbKind.Fluid)) return sbBlockIdx / (float)(SandBoxWorld.BlockSizes.Length - 1);
        return Mathf.Log(sbScale / 0.25f, 16f);
      }, v =>
      {
        if (sbSelected != null && (sbSelected.Kind == SbKind.Block || sbSelected.Kind == SbKind.Fluid))
          sbBlockIdx = Mathf.Clamp(Mathf.RoundToInt(v * (SandBoxWorld.BlockSizes.Length - 1)), 0, SandBoxWorld.BlockSizes.Length - 1);
        else
          sbScale = Mathf.Clamp(0.25f * Mathf.Pow(16f, v), 0.25f, 4f);
        SbRefreshSizeText();
      });
      float half = (w - 52f) * 0.5f;
      sbEquipL = SbButton(2, p, "EQUIP LEFT", new Vector2(-(half * 0.5f + 6f), -200f), new Vector2(half, 42f), null, () => SbEquip(false), 15f);
      sbEquipR = SbButton(2, p, "EQUIP RIGHT", new Vector2(half * 0.5f + 6f, -200f), new Vector2(half, 42f), null, () => SbEquip(true), 15f);
      sbRotateCtl = SbButton(2, p, "ROTATE 45", new Vector2(0f, -200f), new Vector2(w - 40f, 42f), null, () => { sbYawOffset = (sbYawOffset + 45f) % 360f; }, 16f);
      sbHintText = SbText(p, "", new Vector2(0f, -248f), new Vector2(w - 30f, 40f), 12f, st.Sub, TextAlignmentOptions.Center, FontStyles.Normal, true);
    }

    private void SbEquip(bool right)
    {
      if (sbSelected == null || !sbSelected.IsHeldType) return;
      SandBoxWorld.Instance?.SpawnHeld(sbSelected, right, sbScale);
    }

    private void SbStepSize(int dir)
    {
      if (sbSelected != null && (sbSelected.Kind == SbKind.Block || sbSelected.Kind == SbKind.Fluid))
        sbBlockIdx = Mathf.Clamp(sbBlockIdx + dir, 0, SandBoxWorld.BlockSizes.Length - 1);
      else
        sbScale = Mathf.Clamp(sbScale * (dir > 0 ? 1.25f : 0.8f), 0.25f, 4f);
      SbSavePrefs();
      SbRefreshSizeText();
      SbUpdateSlider(sbSizeCtl);
    }

    private void SbRefreshSizeText()
    {
      if (sbSizeText == null) return;
      if (sbSelected != null && (sbSelected.Kind == SbKind.Block || sbSelected.Kind == SbKind.Fluid))
        sbSizeText.text = "BLOCK SIZE " + (SandBoxWorld.BlockSizes[sbBlockIdx] * 100f).ToString("0") + " CM";
      else sbSizeText.text = "SIZE " + sbScale.ToString("0.00") + "x";
    }

    private void SbRefreshRight()
    {
      if (sbSelName == null) return;
      SbStyle st = SbStyles.Get(sbStyleIdx);
      SbItem it = sbSelected;
      sbSelName.text = it == null ? "NOTHING SELECTED" : it.Name.ToUpperInvariant();
      sbSelDesc.text = it == null ? "Pick something from the middle display, then point and pull the trigger to place it." : it.Desc;
      sbSelIcon.texture = it != null ? SbCatalog.Icon(it) : SbStyles.Icon("ring");
      sbSelIcon.color = it != null ? Color.white : new Color(st.Sub.r, st.Sub.g, st.Sub.b, 0.4f);
      bool block = it != null && (it.Kind == SbKind.Block || it.Kind == SbKind.Fluid);
      bool held = it != null && it.IsHeldType;
      SbShow(sbAnchorCtl, it != null && !block);
      if (sbAnchorCtl != null && sbAnchorCtl.label != null) sbAnchorCtl.label.text = sbAnchored ? "ANCHORED: ON" : "ANCHORED: OFF (PHYSICS)";
      SbShow(sbSizeCtl, it != null);
      SbShow(sbMinusCtl, it != null);
      SbShow(sbPlusCtl, it != null);
      if (sbSizeText != null) sbSizeText.gameObject.SetActive(it != null);
      SbShow(sbEquipL, held);
      SbShow(sbEquipR, held);
      SbShow(sbRotateCtl, it != null && !block && !held);
      SbRefreshSizeText();
      SbUpdateSlider(sbSizeCtl);
      if (sbHintText != null)
        sbHintText.text = (sbPointerRight ? "RIGHT " : "LEFT ") + SbInputName(sbPlaceInput) + ": PLACE   |   GRIP + TRIGGER: DELETE\nOPEN/CLOSE: " + TabletBindName((TabletBind)sbOpenBind).ToUpperInvariant();
    }

    private static void SbShow(SbCtl c, bool show)
    {
      if (c != null && c.rt != null && c.rt.gameObject.activeSelf != show) c.rt.gameObject.SetActive(show);
    }

    private void SbRefreshStatus(SandBoxWorld world)
    {
      if (sbStatusText == null) return;
      bool solid = world.SolidAllowed;
      sbStatusDot.color = solid ? new Color(0.3f, 1f, 0.45f) : new Color(1f, 0.7f, 0.2f);
      sbStatusText.text = (solid ? "SOLID | " : "SEE-THROUGH | ") + world.GateReason;
      int others = SbNet.UserCount;
      sbPlacedText.text = "PLACED " + world.MineCount + " / " + SandBoxWorld.MaxMine + (others > 0 ? "   |   " + others + " SANDBOX USER" + (others == 1 ? "" : "S") : "");
      if (sbLobbyCtl != null && sbLobbyCtl.label != null) sbLobbyCtl.label.text = SbLobbyButtonText();
    }

    private void SbProcessIconQueue()
    {
      int budget = 3;
      while (budget-- > 0 && sbIconQueue.Count > 0)
      {
        var kv = sbIconQueue[0];
        sbIconQueue.RemoveAt(0);
        if (kv.Key == null) { budget++; continue; }
        kv.Key.texture = SbCatalog.Icon(kv.Value);
        kv.Key.color = Color.white;
      }
    }

    // ================================================================ animation / placement of the displays
    private static float SbEaseOutCubic(float t) { t = Mathf.Clamp01(t); return 1f - (1f - t) * (1f - t) * (1f - t); }
    private static float SbEaseOutBack(float t) { t = Mathf.Clamp01(t); const float c1 = 1.70158f, c3 = c1 + 1f; return 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f); }

    private static readonly float[] sbAnimProgress = new float[3];

    private void SbAnimate()
    {
      if (sbRoot == null) return;
      float dt = Mathf.Min(Time.unscaledDeltaTime, 0.05f);
      sbAnimTime += dt;
      float[] progress = sbAnimProgress;   // one array, not a new one every frame
      bool any = false;
      for (int i = 0; i < 3; i++)
      {
        float p;
        if (sbVisible)
        {
          float delay = i == 1 ? 0f : 0.09f;
          p = Mathf.Clamp01((sbAnimTime - delay) / 0.52f);
        }
        else
        {
          float delay = i == 1 ? 0.07f : 0f;
          p = 1f - Mathf.Clamp01((sbAnimTime - delay) / 0.36f);
        }
        progress[i] = p;
        if (p > 0.001f) any = true;
      }
      sbOpenT = Mathf.Min(progress[0], Mathf.Min(progress[1], progress[2]));
      if (!sbVisible && !any)
      {
        if (sbRoot.activeSelf) sbRoot.SetActive(false);
        SbHideWorldPointer();
        return;
      }
      if (!sbRoot.activeSelf) sbRoot.SetActive(true);

      Transform head = this.GetTabletHead();
      float pscale = PlayerScale;
      if (head != null && sbVisible && sbFollow)
      {
        // a soft, continuous follow (no dead zone that snaps): the smoothing slider
        // sets how long it takes to catch up, 0.12 s .. 1.3 s
        float smooth = Mathf.Lerp(0.12f, 1.3f, sbFollowSmooth);
        Vector3 to = head.position - sbRootPos;
        Vector3 target = to.magnitude > 0.08f * pscale ? head.position : sbRootPos;
        sbRootPos = Vector3.SmoothDamp(sbRootPos, target, ref sbFollowVel, smooth, Mathf.Infinity, dt);
        Vector3 f = Vector3.ProjectOnPlane(head.forward, Vector3.up);
        if (f.sqrMagnitude > 1e-4f)
        {
          float yaw = Mathf.Atan2(f.x, f.z) * Mathf.Rad2Deg;
          // turns with you once you look ~35 degrees away, and keeps easing until centred
          if (Mathf.Abs(Mathf.DeltaAngle(sbRootYaw, yaw)) > 35f) sbFollowYaw = true;
          if (sbFollowYaw)
          {
            sbRootYaw = Mathf.SmoothDampAngle(sbRootYaw, yaw, ref sbFollowYawVel, smooth * 1.2f, Mathf.Infinity, dt);
            if (Mathf.Abs(Mathf.DeltaAngle(sbRootYaw, yaw)) < 1.5f && Mathf.Abs(sbFollowYawVel) < 5f) sbFollowYaw = false;
          }
          else sbFollowYawVel = 0f;
        }
      }
      else { sbFollowVel = Vector3.zero; sbFollowYawVel = 0f; }

      float size = sbUiSize * pscale;
      float d = sbUiDist * pscale;
      Quaternion rootRot = Quaternion.Euler(0f, sbRootYaw, 0f);
      Vector3 chest = SbChest();
      float wc = SbPanelW[1] * 0.001f * size, ws = SbPanelW[0] * 0.001f * size;
      float theta = (wc * 0.5f + ws * 0.5f + 0.03f * size) / Mathf.Max(0.2f, d) * Mathf.Rad2Deg;
      SbStyle st = SbStyles.Get(sbStyleIdx);
      sbScanT += dt;

      for (int i = 0; i < 3; i++)
      {
        float ang = i == 0 ? -theta : i == 2 ? theta : 0f;
        Quaternion pr = rootRot * Quaternion.Euler(0f, ang, 0f);
        Vector3 finalPos = sbRootPos + Vector3.up * (sbUiHeight * pscale) + pr * Vector3.forward * d;
        float p = progress[i];
        float e = SbEaseOutCubic(p);
        Vector3 ctrl = Vector3.Lerp(chest, finalPos, 0.5f) + Vector3.up * 0.10f * size + (pr * Vector3.right) * (i == 0 ? -0.06f : i == 2 ? 0.06f : 0f);
        Vector3 a1 = Vector3.Lerp(chest, ctrl, e), a2 = Vector3.Lerp(ctrl, finalPos, e);
        Vector3 pos = Vector3.Lerp(a1, a2, e);
        float sy = Mathf.Lerp(0.03f, 1f, SbEaseOutBack(p / 0.6f));
        float sx = Mathf.Lerp(0.012f, 1f, SbEaseOutCubic((p - 0.28f) / 0.72f));
        Transform t = sbPanels[i].transform;
        t.position = pos;
        t.rotation = pr * Quaternion.Euler((1f - e) * -25f, (1f - e) * (i == 0 ? -30f : i == 2 ? 30f : 0f), 0f);
        t.localScale = new Vector3(sx, sy, 1f) * 0.001f * size;
        sbGroups[i].alpha = Mathf.Clamp01(p * 2.4f) * (0.92f + 0.08f * Mathf.Sin(sbScanT * 31f + i) * (1f - p));

        LineRenderer beam = sbBeams[i];
        float beamA = Mathf.Sin(Mathf.PI * p) * (p < 0.999f ? 1f : 0f);
        if (beamA > 0.02f)
        {
          beam.enabled = true;
          beam.SetPosition(0, chest);
          beam.SetPosition(1, pos);
          beam.startWidth = 0.004f * size;
          beam.endWidth = 0.03f * size * beamA;
          Color c0 = st.Accent; c0.a = 0.9f * beamA;
          Color c1 = st.Accent2; c1.a = 0.25f * beamA;
          beam.startColor = c0;
          beam.endColor = c1;
        }
        else beam.enabled = false;

        if (sbScanlines[i] != null)
        {
          Rect r = sbScanlines[i].uvRect;
          r.y = -sbScanT * 0.4f;
          sbScanlines[i].uvRect = r;
        }
      }

      SbPlaceKeyboard(size, dt);

      // the portal at the chest
      float portal = Mathf.Sin(Mathf.PI * Mathf.Clamp01(sbAnimTime / (sbVisible ? 0.62f : 0.45f)));
      if (portal > 0.01f && sbAnimTime < 0.7f)
      {
        sbPortal.SetActive(true);
        sbPortal.transform.position = chest;
        sbPortal.transform.rotation = Quaternion.LookRotation(rootRot * Vector3.forward) * Quaternion.Euler(0f, 0f, sbAnimTime * 360f);
        sbPortal.transform.localScale = Vector3.one * (0.06f + 0.22f * portal) * size;
        Color c = st.Accent; c.a = portal;
        sbPortalMat.color = c;
      }
      else sbPortal.SetActive(false);

      // selected preview ring spins
      if (sbSelRing != null) sbSelRing.localRotation = Quaternion.Euler(0f, 0f, -sbScanT * 40f);

      // card hover / state animation
      foreach (SbCtl c in sbCtls)
      {
        if (c.rt == null) continue;
        bool on = c.on != null && c.on();
        c.onBlend = Mathf.MoveTowards(c.onBlend, on ? 1f : 0f, dt * 7f);
        c.hover = Mathf.MoveTowards(c.hover, c == sbHoverCtl || c == sbFingerL || c == sbFingerR || c == sbDragCtl ? 1f : 0f, dt * 9f);
        c.pulse = Mathf.MoveTowards(c.pulse, 0f, dt * 4f);
        if (c.slider) { SbUpdateSlider(c); if (c.edge != null) { Color ec = c.edge.color; ec.a = c.hover; c.edge.color = ec; } continue; }
        if (c.bg != null)
        {
          Color baseC = Color.Lerp(c.baseCol, c.onCol, c.onBlend);
          c.bg.color = Color.Lerp(baseC, c.hoverCol, c.hover * (1f - c.onBlend * 0.5f));
        }
        if (c.edge != null) { Color ec = c.edge.color; ec.a = Mathf.Max(c.onBlend, c.hover * 0.7f); c.edge.color = ec; }
        float s = 1f + c.hover * (c.card ? 0.05f : 0.025f) - c.pulse * 0.06f;
        c.rt.localScale = Vector3.one * s;
        if (c.stateText != null) c.stateText.text = on ? "ON" : "OFF";
      }

      // smooth scrolling
      float maxScroll = Mathf.Max(0f, sbContentHeight - SbViewH);
      sbScrollTarget = Mathf.Clamp(sbScrollTarget, 0f, maxScroll);
      sbScroll = Mathf.Lerp(sbScroll, sbScrollTarget, 1f - Mathf.Exp(-14f * dt));
      if (sbContent != null) sbContent.anchoredPosition = new Vector2(0f, sbScroll);
      if (sbThumb != null)
      {
        float vis = maxScroll <= 0f ? 1f : SbViewH / sbContentHeight;
        float th = Mathf.Max(40f, SbViewH * vis);
        float k = maxScroll <= 0f ? 0f : sbScroll / maxScroll;
        sbThumb.sizeDelta = new Vector2(8f, th);
        sbThumb.anchoredPosition = new Vector2(sbThumb.anchoredPosition.x, SbViewY + (SbViewH - th) * 0.5f - (SbViewH - th) * k);
        sbThumb.gameObject.SetActive(maxScroll > 0f);
      }
    }

    private bool sbFollowYaw;
    private float sbFollowSmooth = 0.45f, sbFollowYawVel;
    private Vector3 sbFollowVel;

    // ================================================================ interaction
    private SbCtl SbCtlAt(int panel, Vector3 world)
    {
      bool inViewport = false;
      if (panel == 1 && sbViewport != null)
      {
        Vector3 vl = sbViewport.InverseTransformPoint(world);
        inViewport = sbViewport.rect.Contains(new Vector2(vl.x, vl.y));
      }
      for (int i = sbCtls.Count - 1; i >= 0; i--)
      {
        SbCtl c = sbCtls[i];
        if (c.panel != panel || c.rt == null || !c.rt.gameObject.activeInHierarchy) continue;
        if (c.inScroll && !inViewport) continue;
        Vector3 l = c.rt.InverseTransformPoint(world);
        Rect r = c.rt.rect;
        if (c.slider) r = new Rect(r.x - 14f, r.y - 8f, r.width + 28f, r.height + 16f);
        if (r.Contains(new Vector2(l.x, l.y))) return c;
      }
      return null;
    }

    private bool SbRayPanels(Vector3 origin, Vector3 dir, out int panel, out Vector3 point, out float dist)
    {
      panel = -1; point = origin; dist = float.MaxValue;
      if (sbRoot == null || !sbRoot.activeSelf || !sbVisible || sbOpenT < 0.9f) return false;
      var ray = new Ray(origin, dir);
      for (int i = 0; i < sbPanels.Length; i++)
      {
        if (sbPanels[i] == null || !sbPanels[i].activeInHierarchy) continue;
        Transform t = sbPanels[i].transform;
        Plane pl = new Plane(t.forward, t.position);
        float enter;
        if (!pl.Raycast(ray, out enter) || enter < 0f || enter > 3f || enter >= dist) continue;
        Vector3 hp = ray.GetPoint(enter);
        Vector3 l = sbPanelRts[i].InverseTransformPoint(hp);
        if (!sbPanelRts[i].rect.Contains(new Vector2(l.x, l.y))) continue;
        panel = i; point = hp; dist = enter;
      }
      return panel >= 0;
    }

    private void SbClick(SbCtl c, bool right)
    {
      if (c == null || c.click == null) return;
      if (Time.unscaledTime - c.lastClick < 0.25f) return;
      c.lastClick = Time.unscaledTime;
      c.pulse = 1f;
      SbFx.Play(SbSound.UiClick, c.rt.position, 0.55f);
      this.PulseTabletHaptic(right, 0.18f, 0.03f);
      try { c.click(); } catch (Exception ex) { SbLog.Throttled("SandBox click", ex); }
    }

    private void SbSliderFromPoint(SbCtl c, Vector3 world)
    {
      if (c == null || c.set == null) return;
      sbPrefsDirty = true;
      sbPrefsSaveAt = Time.unscaledTime + 0.5f;
      Vector3 l = c.rt.InverseTransformPoint(world);
      float w = c.rt.rect.height > c.rt.rect.width * 2f ? c.rt.rect.height : c.rt.rect.width;
      float v = c.rt.rect.height > c.rt.rect.width * 2f ? 1f - (l.y / w + 0.5f) : l.x / w + 0.5f;
      // vertical (scroll bar): top = 0 visually, the setter wants 1 at the top
      if (c.rt.rect.height > c.rt.rect.width * 2f) v = 1f - v;
      c.set(Mathf.Clamp01(v));
      SbUpdateSlider(c);
    }

    private void SbInteract(SandBoxWorld world)
    {
      // the branches below ask for the pointer / preview; SbPointerTick draws them after
      sbPtrWant = false;
      sbPtrDelete = false;
      sbGhostWant = false;
      // switched off in the desktop window: no pointer, no building
      if (!sbTabletEnabled) return;
      bool right = sbPointerRight;
      bool uiOpen = sbVisible && sbRoot != null && sbOpenT >= 0.9f;
      SbItem heldItem = world.HeldIn(right) != null ? world.HeldIn(right).Item : null;
      bool handBusy = heldItem != null;

      Vector3 origin, dir;
      bool hasPose = this.TryGetStableTabletPointerPose(right, sbRoot != null ? sbRoot.transform : null, out origin, out dir);
      bool trigger = this.GetTabletPointerPressed(right);
      bool triggerEdge = trigger && !sbTriggerWas;
      sbTriggerWas = trigger;
      bool place = SbInputDown(sbPlaceInput, right);
      bool placeEdge = place && !sbPlaceWas;
      sbPlaceWas = place;

      // fingertip touches (both hands) on the displays: only while the tablet is out (they ran every frame
      // with it closed, testing both hands against panels nobody could see)
      bool tabletShown = sbRoot != null && sbRoot.activeSelf;
      if (tabletShown)
      {
        SbFingerTouch(false);
        SbFingerTouch(true);
      }

      int panel = -1; Vector3 uiPoint = Vector3.zero; float uiDist;
      bool overUi = tabletShown && hasPose && !handBusy && SbRayPanels(origin, dir, out panel, out uiPoint, out uiDist);
      SbCtl prevHover = sbHoverCtl;
      sbHoverCtl = overUi ? SbCtlAt(panel, uiPoint) : null;
      if (sbHoverCtl != null && sbHoverCtl != prevHover && !sbHoverCtl.slider) SbFx.Play(SbSound.UiHover, uiPoint, 0.25f);

      if (sbDragCtl != null)
      {
        if (!trigger || !uiOpen) sbDragCtl = null;
        else if (hasPose)
        {
          Transform pt = sbPanels[sbDragCtl.panel].transform;
          Plane pl = new Plane(pt.forward, pt.position);
          float enter;
          if (pl.Raycast(new Ray(origin, dir), out enter)) SbSliderFromPoint(sbDragCtl, origin + dir * enter);
        }
      }

      bool buildActive = sbSelected != null && (sbVisible || sbKeepBuilding) && !handBusy;

      if (overUi)
      {
        SbHideGhost();
        if (triggerEdge && sbHoverCtl != null)
        {
          if (sbHoverCtl.slider) { sbDragCtl = sbHoverCtl; SbSliderFromPoint(sbHoverCtl, uiPoint); }
          else SbClick(sbHoverCtl, right);
        }
        // stick scrolls the middle display
        Vector2 stick = SbStick(right);
        if (panel == 1 && Mathf.Abs(stick.y) > 0.15f) sbScrollTarget -= stick.y * 900f * Time.unscaledDeltaTime;
        SbDrawPointer(origin, uiPoint, true);
        return;
      }

      // world
      // hold grip and pull the trigger = delete what you point at
      bool gripHeld = this.GetTabletBindDown(right ? TabletBind.RightGrip : TabletBind.LeftGrip) && sbPlaceInput != (int)SbInputKind.Grip;
      // with the tablet closed it only deletes while you are still building something
      if (gripHeld && !handBusy && hasPose && (sbVisible || (sbKeepBuilding && sbSelected != null)))
      {
        SbHideGhost();
        SbObject target = world.AimDelete(origin, dir);
        RaycastHit gwh;
        Vector3 gend = target != null ? target.Center : (world.RaycastWorld(new Ray(origin, dir), 8f, out gwh) ? gwh.point : origin + dir * 2.5f);
        SbDrawPointer(origin, gend, false);
        sbPtrDelete = true;
        if (target != null && trigger && (triggerEdge || Time.unscaledTime >= sbDeleteRepeat))
        {
          sbDeleteRepeat = Time.unscaledTime + (triggerEdge ? 0.35f : 0.18f);
          world.Delete(target);
          this.PulseTabletHaptic(right, 0.25f, 0.04f);
        }
        // nothing of yours there: in the Nether / End the ground itself breaks
        else if (target == null && trigger && (triggerEdge || Time.unscaledTime >= sbDeleteRepeat) && (world.InNether || world.InEnd))
        {
          sbDeleteRepeat = Time.unscaledTime + (triggerEdge ? 0.35f : 0.22f);
          if (world.BreakTerrain(origin, dir)) this.PulseTabletHaptic(right, 0.25f, 0.04f);
        }
        return;
      }
      bool showPointer = hasPose && !handBusy && (sbVisible || buildActive);
      if (buildActive && hasPose && sbSelected.Kind == SbKind.Tool)
      {
        // flint and steel: light whatever the ray hits
        SbHideGhost();
        RaycastHit twh;
        Vector3 tend = world.RaycastWorld(new Ray(origin, dir), 8f, out twh) ? twh.point : origin + dir * 2.5f;
        float tod; Vector3 ton;
        if (world.RaycastObjects(new Ray(origin, dir), 8f, out tod, out ton, false, false) != null && tod < Vector3.Distance(origin, tend)) tend = origin + dir * tod;
        SbDrawPointer(origin, tend, false);
        if (placeEdge) { world.UseTool(sbSelected, origin, dir); this.PulseTabletHaptic(right, 0.2f, 0.03f); }
      }
      else if (buildActive && hasPose)
      {
        SbStickAdjust(!right);
        world.ComputeAim(origin, dir, sbSelected, sbBlockIdx, sbScale, sbYawOffset, sbSnap, sbAim);
        // thrusters, wheels and balloons pointed at one of your objects stick to it
        world.AdjustAttachAim(sbAim, sbSelected, sbScale);
        SbShowGhost(sbAim, sbSelected);
        bool repeatable = sbSelected.Kind == SbKind.Block;
        if (place && sbAim.Valid)
        {
          if (placeEdge || (repeatable && Time.unscaledTime >= sbPlaceRepeat))
          {
            sbPlaceRepeat = Time.unscaledTime + (placeEdge ? 0.35f : 0.18f);
            if (world.Place(sbAim, sbSelected, sbAnchored) != null) this.PulseTabletHaptic(right, 0.2f, 0.03f);
          }
        }
        else if (placeEdge && !sbAim.Valid) SbFx.Play(SbSound.UiHover, sbAim.Pos, 0.4f, 0.5f);
        SbDrawPointer(origin, sbAim.Hit ? sbAim.HitPoint : origin + dir * 2.5f, false);
      }
      else
      {
        SbHideGhost();
        if (showPointer)
        {
          RaycastHit wh;
          Vector3 end = world.RaycastWorld(new Ray(origin, dir), 8f, out wh) ? wh.point : origin + dir * 2.5f;
          SbDrawPointer(origin, end, false);
        }
        else SbHideWorldPointer();
      }
    }

    private void SbStickAdjust(bool right)
    {
      Vector2 s = SbStick(right);
      bool xOut = Mathf.Abs(s.x) > 0.7f, yOut = Mathf.Abs(s.y) > 0.7f;
      if (xOut && !sbStickXWas)
      {
        sbYawOffset = (sbYawOffset + (s.x > 0f ? 45f : -45f) + 360f) % 360f;
        SbFx.Play(SbSound.UiHover, sbAim.Pos, 0.3f, 1.2f);
      }
      if (yOut && !sbStickYWas) SbStepSize(s.y > 0f ? 1 : -1);
      sbStickXWas = xOut;
      sbStickYWas = yOut;
    }

    private void SbFingerTouch(bool right)
    {
      SbCtl prev = right ? sbFingerR : sbFingerL;
      SbCtl now = null;
      Vector3 tip = Vector3.zero, fdir;
      bool has = sbVisible && sbRoot != null && sbOpenT >= 0.9f && TryGetTabletFingerPose(right, sbRoot.transform, out tip, out fdir);
      if (has)
      {
        for (int i = 0; i < sbPanels.Length && now == null; i++)
        {
          if (sbPanels[i] == null || !sbPanels[i].activeInHierarchy) continue;
          Transform t = sbPanels[i].transform;
          Vector3 l = sbPanelRts[i].InverseTransformPoint(tip);
          float depth = Mathf.Abs(l.z) * Mathf.Abs(t.lossyScale.z);
          if (depth > 0.022f || !sbPanelRts[i].rect.Contains(new Vector2(l.x, l.y))) continue;
          now = SbCtlAt(i, tip);
        }
      }
      if (now != null && now != prev)
      {
        if (now.slider) SbSliderFromPoint(now, tip);
        else SbClick(now, right);
      }
      else if (now != null && now.slider) SbSliderFromPoint(now, tip);
      if (right) sbFingerR = now; else sbFingerL = now;
    }
  }
}
