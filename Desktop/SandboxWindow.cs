using System.Collections.Generic;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

namespace UnknownsSandbox
{
  // The desktop window: every SandBox setting and control, in three tabs. It uses the
  // tablet's colours (SbGui reads them from the tablet's style).
  public partial class SandboxPlugin
  {
    private enum WindowTab { Build, Servers, Settings }

    private const float WindowW = 380f, WindowH = 560f, HeaderH = 52f, TabsH = 34f;

    private bool windowOpen;
    private Rect windowRect;
    private WindowTab windowTab;
    private Vector2 windowScroll;
    private bool windowDragging;
    private Vector2 windowDragOffset;

    private string toastTitle, toastText;
    private float toastAt = -10f;

    private void ShowToast(string title, string text)
    {
      toastTitle = title;
      toastText = text;
      toastAt = Time.unscaledTime;
    }

    private static float GuiScale => Mathf.Clamp(Screen.height / 1080f, 0.8f, 2f);

    // true while one of the window's text fields has the keyboard
    private bool WindowTyping => windowOpen && GUIUtility.keyboardControl != 0;

    private void ToggleWindow()
    {
      windowOpen = !windowOpen;
      if (!windowOpen) GUIUtility.keyboardControl = 0;
      if (windowOpen && windowRect.width <= 0f)
      {
        float s = GuiScale;
        windowRect = new Rect(
          PlayerPrefs.GetFloat("UnknownsSandbox_WindowX", 40f),
          PlayerPrefs.GetFloat("UnknownsSandbox_WindowY", 80f),
          WindowW, WindowH);
        ClampWindow(s);
      }
    }

    private void ClampWindow(float s)
    {
      windowRect.x = Mathf.Clamp(windowRect.x, 0f, Mathf.Max(0f, Screen.width / s - windowRect.width));
      windowRect.y = Mathf.Clamp(windowRect.y, 0f, Mathf.Max(0f, Screen.height / s - windowRect.height));
    }

    private void OnGUI()
    {
      SbLoadPrefs();
      SbStyle st = SbStyles.Get(sbStyleIdx);
      SbGui.Begin(st);
      Matrix4x4 keep = GUI.matrix;
      float s = GuiScale;
      GUI.matrix = Matrix4x4.Scale(new Vector3(s, s, 1f));
      try
      {
        if (windowOpen) DrawWindow(st, s);
        DrawToast(st, s);
      }
      catch (System.Exception ex) { SbLog.Throttled("Desktop window", ex); }
      GUI.matrix = keep;

      try { DrawSandBoxPortalSwirl(); } catch (System.Exception ex) { SbLog.Throttled("Portal swirl", ex); }
      try { DrawSandBoxLobbyDesktop(); } catch (System.Exception ex) { SbLog.Throttled("Lobby card", ex); }
    }

    private void DrawWindow(SbStyle st, float s)
    {
      Rect w = windowRect;
      Event e = Event.current;

      // dragging by the header
      Rect header = new Rect(w.x, w.y, w.width - 44f, HeaderH);
      if (e.type == EventType.MouseDown && e.button == 0 && header.Contains(e.mousePosition))
      {
        windowDragging = true;
        windowDragOffset = e.mousePosition - w.position;
        e.Use();
      }
      else if (windowDragging && e.type == EventType.MouseDrag)
      {
        windowRect.position = e.mousePosition - windowDragOffset;
        ClampWindow(s);
        w = windowRect;
        e.Use();
      }
      else if (windowDragging && (e.type == EventType.MouseUp || e.rawType == EventType.MouseUp))
      {
        windowDragging = false;
        PlayerPrefs.SetFloat("UnknownsSandbox_WindowX", windowRect.x);
        PlayerPrefs.SetFloat("UnknownsSandbox_WindowY", windowRect.y);
        PlayerPrefs.Save();
      }

      // the panel, drawn like a tablet display: glow, fill, gradient, border
      int rad = SbGui.Radius(18);
      if (st.Glow > 0.01f)
        for (int i = 3; i >= 1; i--)
        {
          float g = i * 5f;
          SbGui.RoundedRect(new Rect(w.x - g, w.y - g, w.width + g * 2f, w.height + g * 2f), rad + (int)g, SbGui.WithAlpha(st.Accent, st.Glow * 0.08f), Color.clear, 0);
        }
      SbGui.RoundedRect(w, rad, SbGui.WithAlpha(st.Bg, Mathf.Max(st.Bg.a, 0.97f)), Color.clear, 0);
      SbGui.RoundedRect(w, rad, st.Bg2, st.Border, st.Pixel ? 4 : 2);

      // header: logo ring, title, status, close
      GUI.DrawTexture(new Rect(w.x + 16f, w.y + 12f, 28f, 28f), SbStyles.Icon("ring"), ScaleMode.ScaleToFit, true, 0f, st.Accent, 0f, 0f);
      SbGui.Title(new Rect(w.x + 54f, w.y + 8f, 200f, 24f), "SANDBOX");
      SbGui.SubLabel(new Rect(w.x + 54f, w.y + 28f, w.width - 110f, 18f), StatusLine());
      if (SbGui.Button(new Rect(w.xMax - 42f, w.y + 12f, 28f, 28f), "X", false)) { windowOpen = false; GUIUtility.keyboardControl = 0; }
      SbGui.RoundedRect(new Rect(w.x + 16f, w.y + HeaderH, w.width - 32f, 2f), 0, SbGui.WithAlpha(st.Accent, 0.6f), Color.clear, 0);

      // tabs
      string[] names = { "BUILD", "SERVERS", "SETTINGS" };
      float tw = (w.width - 32f - 12f) / 3f;
      for (int i = 0; i < 3; i++)
      {
        Rect r = new Rect(w.x + 16f + i * (tw + 6f), w.y + HeaderH + 10f, tw, TabsH - 6f);
        if (SbGui.Button(r, names[i], windowTab == (WindowTab)i) && windowTab != (WindowTab)i)
        {
          windowTab = (WindowTab)i;
          windowScroll = Vector2.zero;
          GUIUtility.keyboardControl = 0;
        }
      }

      Rect body = new Rect(w.x + 12f, w.y + HeaderH + TabsH + 14f, w.width - 24f, w.height - HeaderH - TabsH - 26f);
      GUILayout.BeginArea(body);
      windowScroll = GUILayout.BeginScrollView(windowScroll, false, false, GUIStyle.none, GUIStyle.none, GUIStyle.none);
      GUILayout.BeginVertical(GUILayout.Width(body.width - 10f));
      switch (windowTab)
      {
        case WindowTab.Build: DrawBuildTab(); break;
        case WindowTab.Servers: DrawServersTab(); break;
        default: DrawSettingsTab(); break;
      }
      GUILayout.Space(10f);
      GUILayout.EndVertical();
      GUILayout.EndScrollView();
      GUILayout.EndArea();

      // the window keeps clicks and scrolling to itself
      if (w.Contains(e.mousePosition) && (e.type == EventType.MouseDown || e.type == EventType.ScrollWheel)) e.Use();
    }

    private string StatusLine()
    {
      SandBoxWorld world = SandBoxWorld.Instance;
      if (world == null) return "Starting...";
      string mode = world.SolidAllowed ? "SOLID" : "SEE-THROUGH";
      return mode + "  |  " + world.GateReason + "  |  " + world.MineCount + " PLACED";
    }

    // ---------------------------------------------------------------- BUILD
    private void DrawBuildTab()
    {
      SbGui.Section("Tablet");
      bool enabled = SbGui.Toggle("SandBox Tablet", sbTabletEnabled);
      if (enabled != sbTabletEnabled)
      {
        sbTabletEnabled = enabled;
        PlayerPrefs.SetInt("UnknownsTabletOn_SandBox", enabled ? 1 : 0);
        PlayerPrefs.Save();
      }
      if (sbTabletEnabled && SbGui.Button(sbVisible ? "CLOSE TABLET" : "OPEN TABLET", sbVisible)) ToggleSandBoxTablet();
      SbGui.Note("In VR: " + TabletBindName((TabletBind)sbOpenBind) + " opens and closes it" +
        (tabletKey.Value != UnityEngine.InputSystem.Key.None ? ", on the keyboard: " + tabletKey.Value : string.Empty) + ".");

      SbGui.Section("Your Things");
      SandBoxWorld world = SandBoxWorld.Instance;
      int others = SbNet.UserCount;
      SbGui.Note((world != null ? world.MineCount : 0) + " / " + SandBoxWorld.MaxMine + " placed" +
        (others > 0 ? "   |   " + others + " other SandBox user" + (others == 1 ? "" : "s") : string.Empty));
      int pick = SbGui.ButtonPair("UNDO", "CLEAR MINE");
      if (pick == 0) world?.Undo();
      if (pick == 1) { world?.ClearMine(); ShowToast("SandBox", "Removed everything you placed"); }
      GUILayout.Space(6f);
      pick = SbGui.ButtonPair("DETONATE ALL", "STOP BUILDING", false, sbSelected == null);
      if (pick == 0) world?.DetonateAll();
      if (pick == 1) { sbSelected = null; SbRefreshRight(); }
      if (sbSelected != null) SbGui.Note("Building with: " + sbSelected.Name);

      SbGui.Section("Building");
      sbKeepBuilding = SettingToggle("Keep building when closed", sbKeepBuilding);
      sbSnap = SettingToggle("Snap objects to 5 cm", sbSnap);
      sbGhostOn = SettingToggle("Placement preview", sbGhostOn);

      SbGui.Section("Other Players");
      bool showOthers = SettingToggle("Show their objects", sbShowOthers);
      if (showOthers != sbShowOthers)
      {
        sbShowOthers = showOthers;
        SandBoxWorld.ShowOthers = showOthers;
        SandBoxWorld.Instance?.ApplyShowOthers();
      }
      sbShowOthersTablet = SettingToggle("Show their tablets", sbShowOthersTablet);
      sbShowOthersPointer = SettingToggle("Show their pointers", sbShowOthersPointer);
      sbShowOthersPreview = SettingToggle("Show their previews", sbShowOthersPreview);
      SandBoxWorld.BloodOn = SettingToggle("Blood when players are hit", SandBoxWorld.BloodOn);
      sbSpawnFxOn = SettingToggle("Join / leave effect (every lobby)", sbSpawnFxOn);
    }

    // A toggle that saves the tablet's settings and refreshes its settings page when it changes.
    private bool SettingToggle(string text, bool value)
    {
      bool now = SbGui.Toggle(text, value);
      if (now != value) SettingChanged();
      return now;
    }

    private void SettingChanged()
    {
      sbPrefsDirty = true;
      sbPrefsSaveAt = Time.unscaledTime + 0.5f;
      if (sbSettingsOpen && sbContent != null) sbRefreshSettingsPage = true;
      SbRefreshRight();
    }

    private bool sbRefreshSettingsPage;

    // ---------------------------------------------------------------- SERVERS
    private void DrawServersTab()
    {
      Room room = null;
      try { room = PhotonNetwork.InRoom ? PhotonNetwork.CurrentRoom : null; } catch { }
      SbGui.Section("Now");
      SbGui.Note(SbServerStatusText(room));
      int now = SbGui.ButtonPair("JOIN DEFAULT SERVER", SbLobbyButtonText());
      if (now == 0) SbJoinSandboxLobby();
      if (now == 1) SbQuickCreateRoom();

      bool hosting = room != null && SbIsServerRoom(room.Name) && PhotonNetwork.IsMasterClient;
      SbGui.Section(hosting ? "Make A Server / Your Server" : "Make A Server");
      SbGui.FieldLabel("Name");
      sbSrvName = SbGui.TextField("sb_name", sbSrvName, 20);
      GUILayout.Space(4f);
      sbSrvPublic = SbGui.Toggle("Public (no password)", sbSrvPublic);
      if (!sbSrvPublic)
      {
        SbGui.FieldLabel("Password");
        sbSrvPassword = SbGui.TextField("sb_pw", sbSrvPassword, 16, true);
        GUILayout.Space(4f);
      }
      sbSrvListed = SbGui.Toggle("Show in the server list", sbSrvListed);
      sbSrvMax = Mathf.RoundToInt(SbGui.Slider("Max players", sbSrvMax, 1f, 20f, sbSrvMax.ToString()));
      if (hosting)
      {
        int pick = SbGui.ButtonPair("CREATE SERVER", "APPLY TO MY SERVER", true, false);
        if (pick == 0) SbCreateServer();
        if (pick == 1) SbApplyHostSettings();
      }
      else if (SbGui.Button("CREATE SERVER", true)) SbCreateServer();

      SbGui.Section("Join A Server");
      SbGui.FieldLabel("Code");
      sbSrvCode = (SbGui.TextField("sb_code", sbSrvCode, 9) ?? string.Empty).ToUpperInvariant();
      SbGui.FieldLabel("Password (if it has one)");
      sbSrvJoinPw = SbGui.TextField("sb_joinpw", sbSrvJoinPw, 16, true);
      GUILayout.Space(4f);
      int join = SbGui.ButtonPair("JOIN", "QUICK JOIN PUBLIC");
      if (join == 0) SbJoinServerByCode();
      if (join == 1) SbQuickJoinPublic();

      SbGui.Section("Server List");
      SbServerList.WantList();
      SbGui.Note(SbServerList.BrowseStatus);
      List<SbServerList.Entry> servers = SbServerList.Servers();
      for (int i = 0; i < servers.Count && i < 16; i++)
      {
        SbServerList.Entry e = servers[i];
        string text = (e.Locked ? "[LOCKED]  " : string.Empty) + e.Name + "   " + e.Count + " / " + e.Max;
        if (SbGui.Button(text, false, 28f))
        {
          sbSrvCode = e.Code;
          if (!e.Locked) SbServerJoin(new SbServerPatches.Request { Code = e.Code }, e.Code);
          else if (string.IsNullOrEmpty(sbSrvJoinPw)) ShowToast("Servers", "Type its password under Join A Server, then press JOIN");
          else SbJoinServerByCode();
        }
        GUILayout.Space(4f);
      }
    }

    // ---------------------------------------------------------------- SETTINGS
    private void DrawSettingsTab()
    {
      SbGui.Section("Style");
      List<SbStyle> styles = SbStyles.All;
      for (int i = 0; i < styles.Count; i += 2)
      {
        GUILayout.BeginHorizontal();
        for (int j = i; j < i + 2; j++)
        {
          if (j >= styles.Count) { GUILayout.FlexibleSpace(); continue; }
          Rect r = GUILayoutUtility.GetRect(GUIContent.none, GUIStyle.none, GUILayout.Height(34f), GUILayout.ExpandWidth(true));
          if (SbGui.Button(r, "     " + styles[j].Name, sbStyleIdx == j) && sbStyleIdx != j)
          {
            sbStyleIdx = j;
            SbSavePrefs();
            if (sbRoot != null) SbRebuildStyle();
          }
          SbGui.RoundedRect(new Rect(r.x + 10f, r.y + 9f, 22f, 16f), 4, SbGui.WithAlpha(styles[j].Bg, 1f), styles[j].Accent, 2);
          if (j == i) GUILayout.Space(6f);
        }
        GUILayout.EndHorizontal();
        GUILayout.Space(6f);
      }

      SbGui.Section("Accent Colour");
      bool custom = SbGui.Toggle("Custom accent colour", SbStyles.UseCustomAccent);
      Color.RGBToHSV(SbStyles.CustomAccent, out float h, out float sat, out float val);
      float hue = h;
      if (custom)
      {
        hue = SbGui.Slider("Hue", h, 0f, 1f, "#" + ColorUtility.ToHtmlStringRGB(SbStyles.CustomAccent));
        Rect sw = GUILayoutUtility.GetRect(GUIContent.none, GUIStyle.none, GUILayout.Height(10f), GUILayout.ExpandWidth(true));
        SbGui.RoundedRect(new Rect(sw.x + 8f, sw.y + 2f, sw.width - 16f, 6f), 3, SbStyles.CustomAccent, Color.clear, 0);
      }
      else SbGui.Note("Off: the style keeps its own colours.");
      if (custom != SbStyles.UseCustomAccent || !Mathf.Approximately(hue, h))
      {
        SbSetAccent(custom, Color.HSVToRGB(hue, Mathf.Max(sat, 0.45f), Mathf.Max(val, 0.85f)));
        SettingChanged();
      }

      SbGui.Section("Tablet Layout");
      sbUiSize = LayoutSlider("UI size", sbUiSize, 0.6f, 1.5f, sbUiSize.ToString("0.00") + "x");
      sbUiDist = LayoutSlider("UI distance", sbUiDist, 0.4f, 1.2f, (sbUiDist * 100f).ToString("0") + " cm");
      sbUiHeight = LayoutSlider("UI height", sbUiHeight, -0.4f, 0.2f, (sbUiHeight * 100f).ToString("0") + " cm");
      sbFollowSmooth = LayoutSlider("Follow smoothness", sbFollowSmooth, 0f, 1f,
        sbFollowSmooth < 0.2f ? "SNAPPY" : sbFollowSmooth > 0.75f ? "FLOATY" : Mathf.RoundToInt(sbFollowSmooth * 100f) + "%");
      sbFollow = SettingToggle("UI follows you", sbFollow);

      SbGui.Section("Controls");
      if (SbGui.Cycle("Tablet button (VR)", capturing == Capture.TabletButton ? "PRESS A BUTTON..." : TabletBindName((TabletBind)sbOpenBind).ToUpperInvariant()))
        StartCapture(Capture.TabletButton);
      if (SbGui.Cycle("Tablet key", capturing == Capture.TabletKey ? "PRESS A KEY..." : KeyName(tabletKey.Value)))
        StartCapture(Capture.TabletKey);
      if (SbGui.Cycle("Window key", capturing == Capture.WindowKey ? "PRESS A KEY..." : KeyName(windowKey.Value)))
        StartCapture(Capture.WindowKey);
      SbGui.Note("Click one, then press the button or key you want. Esc cancels, Delete clears the tablet key.");
      if (SbGui.Cycle("Place", SbInputName(sbPlaceInput)))
      {
        sbPlaceInput = (sbPlaceInput + 1) % 4;
        if (sbPlaceInput == (int)SbInputKind.Grip) sbPlaceInput = (sbPlaceInput + 1) % 4;
        SettingChanged();
      }
      if (SbGui.Cycle("Pointer hand", sbPointerRight ? "RIGHT" : "LEFT"))
      {
        sbPointerRight = !sbPointerRight;
        SettingChanged();
      }

      SbGui.Section("World");
      if (SbGui.Cycle("Texture pack", SbMinecraft.PackLabel(SbMinecraft.Pack))) SbNextTexturePack();
      SbGui.Note("Put Minecraft resource pack .zip files in Gorilla Tag\\Unknowns Sandbox\\minecraft\\packs, then pick one here.");
      int nether = Mathf.RoundToInt(SbGui.Slider("Nether render distance", SandBoxWorld.NetherViewChunks, 1f, 6f,
        SandBoxWorld.NetherViewChunks + " (" + SandBoxWorld.NetherViewChunks * 16 + " m)"));
      if (nether != SandBoxWorld.NetherViewChunks) { SandBoxWorld.NetherViewChunks = nether; SettingChanged(); }
      float volume = SbGui.Slider("Sound volume", sbVolume, 0f, 1f, Mathf.RoundToInt(sbVolume * 100f) + "%");
      if (!Mathf.Approximately(volume, sbVolume)) { sbVolume = volume; SbFx.Volume = volume; SettingChanged(); }
    }

    private float LayoutSlider(string text, float value, float min, float max, string shown)
    {
      float now = SbGui.Slider(text, value, min, max, shown);
      if (!Mathf.Approximately(now, value)) SettingChanged();
      return now;
    }

    // ---------------------------------------------------------------- toast
    private void DrawToast(SbStyle st, float s)
    {
      float age = Time.unscaledTime - toastAt;
      if (age > 3.2f || string.IsNullOrEmpty(toastText)) return;
      float a = Mathf.Clamp01(age / 0.15f) * Mathf.Clamp01((3.2f - age) / 0.4f);
      float w = 320f, h = 58f;
      Rect r = new Rect(Screen.width / s - w - 20f, 20f + (1f - a) * -10f, w, h);
      Color keep = GUI.color;
      GUI.color = new Color(1f, 1f, 1f, a);
      SbGui.RoundedRect(r, SbGui.Radius(14), SbGui.WithAlpha(st.Bg, 0.97f), st.Border, 2);
      SbGui.RoundedRect(new Rect(r.x + 10f, r.y + 10f, 3f, h - 20f), 1, st.Accent, Color.clear, 0);
      SbGui.Label(new Rect(r.x + 22f, r.y + 8f, w - 32f, 20f), (toastTitle ?? string.Empty).ToUpperInvariant());
      SbGui.SubLabel(new Rect(r.x + 22f, r.y + 28f, w - 32f, 24f), toastText);
      GUI.color = keep;
    }
  }
}
