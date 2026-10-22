using System;
using System.Collections.Generic;
using GorillaNetworking;
using Photon.Pun;
using Photon.Realtime;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace UnknownsSandbox
{
  // SandBox lobbies: JOIN SANDBOX LOBBY on the tablet joins _SANDBOX, or
  // _SANDBOX1, _SANDBOX2 ... when the one before is full. The underscore can't be
  // typed on the in-game computer, so only SandBox players get in.
  // While joining, a loading card shows which lobby and how many players are in it
  // (in the headset and on the desktop), with a quiet loading sound. In every
  // lobby, players who join or leave get an accent wireframe (SbSpawnFx).
  public partial class SandboxPlugin
  {
    private enum SbLobbyPhase { Idle, Joining, Joined, Failed }

    private const string SbLobbyBase = "_SANDBOX";
    private const int SbLobbyMax = 99;
    private const float SbLobbyHold = 2.6f;      // the card stays up this long after joining / failing
    private const float SbLobbyTimeout = 25f;

    private SbLobbyPhase sbLobbyPhase;
    private int sbLobbyIdx;
    private string sbLobbyTarget = string.Empty;
    private string sbLobbyNote = string.Empty;
    private float sbLobbyPhaseAt, sbLobbyAttemptAt, sbLobbyNextAttempt = -1f;
    private int sbLobbyRetries;
    private int sbLobbyResult = -1;            // NetJoinResult from the game's callback, -1 = none yet
    private int sbLobbyAttemptId;
    private float sbLobbyAlpha;

    private bool sbSpawnFxOn = true;

    // ================================================================ lobby names
    private static string SbLobbyRoomName(int idx) { return idx <= 0 ? SbLobbyBase : SbLobbyBase + idx; }

    private static int SbLobbyIndexOf(string room)
    {
      if (string.IsNullOrEmpty(room) || !room.StartsWith(SbLobbyBase, StringComparison.OrdinalIgnoreCase)) return -1;
      if (room.Length == SbLobbyBase.Length) return 0;
      int n;
      return int.TryParse(room.Substring(SbLobbyBase.Length), out n) && n > 0 ? n : -1;
    }

    // the lobby you are in (0 = _SANDBOX), -1 when you are not in one
    private static int SbCurrentLobby
    {
      get
      {
        try { return PhotonNetwork.InRoom && PhotonNetwork.CurrentRoom != null ? SbLobbyIndexOf(PhotonNetwork.CurrentRoom.Name) : -1; }
        catch { return -1; }
      }
    }

    private static string SbRoomPlayers()
    {
      try
      {
        Room r = PhotonNetwork.CurrentRoom;
        if (r == null) return string.Empty;
        return r.PlayerCount + " / " + r.MaxPlayers;
      }
      catch { return string.Empty; }
    }

    // the button at the bottom left makes a room (public, no password)
    private string SbLobbyButtonText()
    {
      if (SbServerBusy()) return "CREATING...";
      return "CREATE ROOM (PUBLIC)";
    }

    // ================================================================ joining
    private void SbJoinSandboxLobby()
    {
      if (sbLobbyPhase == SbLobbyPhase.Joining) return;
      int cur = SbCurrentLobby;
      if (cur >= 0)
      {
        // already in one: show where you are
        sbLobbyIdx = cur;
        sbLobbyTarget = SbLobbyRoomName(cur);
        sbLobbyNote = string.Empty;
        SbLobbySetPhase(SbLobbyPhase.Joined, false);
        return;
      }
      sbLobbyIdx = 0;
      sbLobbyRetries = 0;
      sbLobbyNote = string.Empty;
      SbLobbySetPhase(SbLobbyPhase.Joining, true);
      SbLobbyAttempt();
    }

    private void SbLobbyAttempt()
    {
      sbLobbyNextAttempt = -1f;
      sbLobbyTarget = SbLobbyRoomName(sbLobbyIdx);
      sbLobbyAttemptAt = Time.unscaledTime;
      sbLobbyResult = -1;
      int id = ++sbLobbyAttemptId;
      try
      {
        if ((Object)PhotonNetworkController.Instance != (Object)null)
        {
          // the callback says when the lobby is full, so the next number can be tried
          PhotonNetworkController.Instance.AttemptToJoinSpecificRoomWithCallback(sbLobbyTarget, (JoinType)0, r =>
          {
            if (id == sbLobbyAttemptId) sbLobbyResult = (int)r;
          });
        }
        else
        {
          PhotonNetwork.JoinOrCreateRoom(sbLobbyTarget, new RoomOptions { MaxPlayers = 10, IsOpen = true, IsVisible = false }, TypedLobby.Default, null);
        }
      }
      catch (Exception ex)
      {
        SbLobbyFail("JOIN FAILED: " + ex.Message.ToUpperInvariant());
      }
    }

    private void SbLobbySetPhase(SbLobbyPhase phase, bool sound)
    {
      sbLobbyPhase = phase;
      sbLobbyPhaseAt = Time.unscaledTime;
      if (!sound) return;
      if (phase == SbLobbyPhase.Joined) SbLobbyPlayOneShot(SbLobbyClip(1), 0.16f);
      else if (phase == SbLobbyPhase.Failed) SbLobbyPlayOneShot(SbLobbyClip(2), 0.12f);
    }

    private void SbLobbyFail(string why)
    {
      sbLobbyNote = why;
      SbLobbySetPhase(SbLobbyPhase.Failed, true);
      this.ShowToast("SandBox", "Couldn't join a SandBox lobby");
    }

    private void SbUpdateLobby()
    {
      float now = Time.unscaledTime;
      if (sbLobbyPhase == SbLobbyPhase.Joining)
      {
        bool inTarget = false;
        try { inTarget = PhotonNetwork.InRoom && PhotonNetwork.CurrentRoom != null && PhotonNetwork.CurrentRoom.Name == sbLobbyTarget; } catch { }
        if (inTarget)
        {
          sbLobbyNote = string.Empty;
          SbLobbySetPhase(SbLobbyPhase.Joined, true);
          this.ShowToast("SandBox", "Joined SandBox Lobby " + (sbLobbyIdx + 1) + "  (" + SbRoomPlayers() + ")");
        }
        else if (sbLobbyResult >= 0)
        {
          NetJoinResult r = (NetJoinResult)sbLobbyResult;
          sbLobbyResult = -1;
          if (r == NetJoinResult.Failed_Full)
          {
            sbLobbyNote = "LOBBY " + (sbLobbyIdx + 1) + " IS FULL";
            sbLobbyIdx++;
            sbLobbyRetries = 0;
            if (sbLobbyIdx > SbLobbyMax) SbLobbyFail("EVERY LOBBY IS FULL");
            else sbLobbyNextAttempt = now + 0.35f;
          }
          else if (r == NetJoinResult.Failed_Other)
          {
            // usually still leaving the last room: give it a moment
            sbLobbyRetries++;
            if (sbLobbyRetries > 4) SbLobbyFail("COULDN'T CONNECT");
            else { sbLobbyNote = "RETRYING..."; sbLobbyNextAttempt = now + 1.5f; }
          }
          // Success / FallbackCreated / AlreadyInRoom: in the room any moment now
          else sbLobbyAttemptAt = now;
        }
        else if (sbLobbyNextAttempt > 0f && now >= sbLobbyNextAttempt) SbLobbyAttempt();
        else if (sbLobbyNextAttempt < 0f && now - sbLobbyAttemptAt > SbLobbyTimeout) SbLobbyFail("TIMED OUT");
      }
      else if (sbLobbyPhase != SbLobbyPhase.Idle && now - sbLobbyPhaseAt > SbLobbyHold)
        sbLobbyPhase = SbLobbyPhase.Idle;

      float target = sbLobbyPhase == SbLobbyPhase.Idle ? 0f : 1f;
      sbLobbyAlpha = Mathf.MoveTowards(sbLobbyAlpha, target, Time.unscaledDeltaTime * (target > sbLobbyAlpha ? 5f : 3f));
      SbLobbyUpdateAudio();
      SbLobbyUpdateVr();
      SbUpdateSpawnFx();
      try { SbServerTick(); } catch (Exception ex) { SbLog.Throttled("SandBox servers", ex); }
    }

    // ================================================================ audio
    // Made in code like the rest of the SandBox's sounds, and kept quiet: a soft
    // pad while joining, a two-note chime when you're in, a low tone on failure.
    private static AudioClip[] sbLobbyClips;
    private GameObject sbLobbyAudioGo;
    private AudioSource sbLobbyLoop, sbLobbyOneShot;
    private float sbLobbyLoopVol;

    private static AudioClip SbLobbyClip(int i)
    {
      if (sbLobbyClips == null)
      {
        sbLobbyClips = new AudioClip[3];
        // every frequency fits a whole number of cycles in 4 s, so the loop is seamless
        sbLobbyClips[0] = SbLobbySynth("loading", 4f, t =>
        {
          float pad = Mathf.Sin(2f * Mathf.PI * 220f * t) * (0.55f + 0.45f * Mathf.Sin(2f * Mathf.PI * 0.25f * t))
                    + 0.7f * Mathf.Sin(2f * Mathf.PI * 329.75f * t) * (0.55f + 0.45f * Mathf.Sin(2f * Mathf.PI * 0.5f * t + 1.3f))
                    + 0.45f * Mathf.Sin(2f * Mathf.PI * 440f * t) * (0.5f + 0.5f * Mathf.Sin(2f * Mathf.PI * 0.25f * t + 2.6f));
          float beat = t % 1f;   // a soft tick each second
          float tick = Mathf.Sin(2f * Mathf.PI * 1760f * t) * Mathf.Exp(-beat * 38f) * 0.55f;
          return pad * 0.4f + tick;
        });
        sbLobbyClips[1] = SbLobbySynth("joined", 1.3f, t =>
        {
          float a = Mathf.Sin(2f * Mathf.PI * 880f * t) * Mathf.Exp(-t * 5f);
          float t2 = Mathf.Max(0f, t - 0.13f);
          float b = t >= 0.13f ? (Mathf.Sin(2f * Mathf.PI * 1318.5f * t2) + 0.3f * Mathf.Sin(2f * Mathf.PI * 2637f * t2)) * Mathf.Exp(-t2 * 3.5f) : 0f;
          return (a * 0.8f + b) * Mathf.Clamp01(t * 200f);
        });
        sbLobbyClips[2] = SbLobbySynth("failed", 0.7f, t =>
        {
          float f = Mathf.Lerp(330f, 247f, Mathf.Clamp01(t / 0.35f));
          return Mathf.Sin(2f * Mathf.PI * f * t) * Mathf.Exp(-t * 4.5f) * Mathf.Clamp01(t * 150f);
        });
      }
      return sbLobbyClips[i];
    }

    private static AudioClip SbLobbySynth(string name, float seconds, Func<float, float> f)
    {
      const int rate = 44100;
      int n = Mathf.Max(1, (int)(seconds * rate));
      float[] d = new float[n];
      float peak = 0.0001f;
      for (int i = 0; i < n; i++) { d[i] = f(i / (float)rate); peak = Mathf.Max(peak, Mathf.Abs(d[i])); }
      float g = 0.9f / peak;
      for (int i = 0; i < n; i++) d[i] *= g;
      // the PCM reader keeps ReadOnlySpan (SetData) out of the net48 build, see SbFx
      int pos = 0;
      return AudioClip.Create("Unknowns SandBox Lobby " + name, n, 1, rate, false, data =>
      {
        int count = Math.Max(0, Math.Min(d.Length - pos, data.Length));
        if (count > 0) Array.Copy(d, pos, data, 0, count);
        if (count < data.Length) Array.Clear(data, count, data.Length - count);
        pos += count;
      }, p => { pos = Mathf.Clamp(p, 0, d.Length); });
    }

    private void SbLobbyEnsureAudio()
    {
      if (sbLobbyAudioGo != null) return;
      sbLobbyAudioGo = new GameObject("Unknowns_SandBoxLobbyAudio");
      Object.DontDestroyOnLoad(sbLobbyAudioGo);
      sbLobbyLoop = sbLobbyAudioGo.AddComponent<AudioSource>();
      sbLobbyLoop.playOnAwake = false;
      sbLobbyLoop.loop = true;
      sbLobbyLoop.spatialBlend = 0f;
      sbLobbyLoop.volume = 0f;
      sbLobbyOneShot = sbLobbyAudioGo.AddComponent<AudioSource>();
      sbLobbyOneShot.playOnAwake = false;
      sbLobbyOneShot.spatialBlend = 0f;
    }

    private void SbLobbyPlayOneShot(AudioClip clip, float volume)
    {
      try
      {
        SbLobbyEnsureAudio();
        if (clip != null) sbLobbyOneShot.PlayOneShot(clip, volume * Mathf.Clamp01(sbVolume));
      }
      catch { }
    }

    private void SbLobbyUpdateAudio()
    {
      float want = sbLobbyPhase == SbLobbyPhase.Joining ? 0.09f * Mathf.Clamp01(sbVolume) : 0f;
      if (want <= 0f && sbLobbyLoopVol <= 0f) return;
      try
      {
        SbLobbyEnsureAudio();
        sbLobbyLoopVol = Mathf.MoveTowards(sbLobbyLoopVol, want, Time.unscaledDeltaTime * 0.12f);
        if (sbLobbyLoopVol > 0f && !sbLobbyLoop.isPlaying)
        {
          sbLobbyLoop.clip = SbLobbyClip(0);
          sbLobbyLoop.Play();
        }
        sbLobbyLoop.volume = sbLobbyLoopVol;
        if (sbLobbyLoopVol <= 0f && sbLobbyLoop.isPlaying) sbLobbyLoop.Stop();
      }
      catch { }
    }

    // ================================================================ loading card text
    private string SbLobbyTitle()
    {
      switch (sbLobbyPhase)
      {
        case SbLobbyPhase.Joined: return "WELCOME TO";
        case SbLobbyPhase.Failed: return "COULDN'T JOIN";
        default: return "JOINING SANDBOX";
      }
    }

    private string SbLobbyStatus()
    {
      switch (sbLobbyPhase)
      {
        case SbLobbyPhase.Joined:
          string p = SbRoomPlayers();
          return string.IsNullOrEmpty(p) ? "CONNECTED" : p + " PLAYERS IN THIS LOBBY";
        case SbLobbyPhase.Failed: return sbLobbyNote;
        default:
          string dots = new string('.', 1 + (int)(Time.unscaledTime * 2.5f) % 3);
          return (string.IsNullOrEmpty(sbLobbyNote) ? "CONNECTING" : sbLobbyNote + "  -  TRYING LOBBY " + (sbLobbyIdx + 1)) + dots;
      }
    }

    // ================================================================ VR card
    private GameObject sbLobbyVr;
    private CanvasGroup sbLobbyVrGroup;
    private TMP_Text sbLobbyVrTitle, sbLobbyVrLobby, sbLobbyVrCode, sbLobbyVrStatus;
    private RectTransform sbLobbyVrSpin, sbLobbyVrSpinDot, sbLobbyVrBarSeg, sbLobbyVrCard;
    private Image sbLobbyVrBarFill;
    private Color sbLobbyVrBuiltFor;
    private bool sbLobbyVrSnap = true;
    private const float SbLobbyCardW = 620f, SbLobbyCardH = 300f, SbLobbyBarW = 520f;

    private void SbLobbyBuildVr(SbStyle st)
    {
      if (sbLobbyVr != null) Destroy(sbLobbyVr);
      sbLobbyVr = new GameObject("Unknowns_SandBoxLobbyLoading", typeof(RectTransform));
      Object.DontDestroyOnLoad(sbLobbyVr);
      Canvas c = sbLobbyVr.AddComponent<Canvas>();
      c.renderMode = RenderMode.WorldSpace;
      c.sortingOrder = 31060;
      sbLobbyVr.AddComponent<CanvasScaler>().dynamicPixelsPerUnit = 3f;
      sbLobbyVrGroup = sbLobbyVr.AddComponent<CanvasGroup>();
      sbLobbyVrGroup.interactable = false;
      sbLobbyVrGroup.blocksRaycasts = false;
      RectTransform rt = sbLobbyVr.GetComponent<RectTransform>();
      rt.sizeDelta = new Vector2(SbLobbyCardW, SbLobbyCardH);

      Transform p = SbRect("card", sbLobbyVr.transform, Vector2.zero, new Vector2(SbLobbyCardW, SbLobbyCardH)).transform;
      sbLobbyVrCard = (RectTransform)p;
      float w = SbLobbyCardW, h = SbLobbyCardH;
      Color g = st.Accent; g.a = Mathf.Max(0.25f, st.Glow);
      SbImage(p, "glow", Vector2.zero, new Vector2(w + 60f, h + 60f), SbStyles.Shape(st.Radius, st.Cut, st.Pixel, 2), g);
      SbImage(p, "fill", Vector2.zero, new Vector2(w, h), SbStyles.Shape(st.Radius, st.Cut, st.Pixel, 0), new Color(st.Bg.r, st.Bg.g, st.Bg.b, Mathf.Max(0.92f, st.Bg.a)));
      SbImage(p, "grad", Vector2.zero, new Vector2(w, h), SbStyles.Shape(st.Radius, st.Cut, st.Pixel, 3), st.Bg2);
      SbImage(p, "border", Vector2.zero, new Vector2(w, h), SbStyles.Shape(st.Radius, st.Cut, st.Pixel, 1, st.Pixel ? 5 : 3), st.Border);

      RawImage ring = SbRect("spin", p, new Vector2(-w * 0.5f + 92f, 22f), new Vector2(118f, 118f)).AddComponent<RawImage>();
      ring.texture = SbMats.Ring;
      ring.color = new Color(st.Accent.r, st.Accent.g, st.Accent.b, 0.5f);
      ring.raycastTarget = false;
      sbLobbyVrSpin = ring.rectTransform;
      Image dot = SbImage(sbLobbyVrSpin, "dot", new Vector2(0f, 48f), new Vector2(16f, 16f), SbStyles.Shape(st.Pixel ? 0 : 8, false, st.Pixel, 0), st.Accent2);
      sbLobbyVrSpinDot = dot.rectTransform;
      RawImage logo = SbRect("logo", p, new Vector2(-w * 0.5f + 92f, 22f), new Vector2(54f, 54f)).AddComponent<RawImage>();
      logo.texture = SbStyles.Icon("ring");
      logo.color = st.Accent;
      logo.raycastTarget = false;

      float tx = 70f;
      sbLobbyVrTitle = SbText(p, "JOINING SANDBOX", new Vector2(tx, 92f), new Vector2(400f, 30f), 20f, st.Sub, TextAlignmentOptions.Left);
      sbLobbyVrLobby = SbText(p, "LOBBY 1", new Vector2(tx, 40f), new Vector2(400f, 72f), 62f, st.Text, TextAlignmentOptions.Left);
      sbLobbyVrCode = SbText(p, SbLobbyBase, new Vector2(tx, -10f), new Vector2(400f, 26f), 18f, st.Accent, TextAlignmentOptions.Left);
      sbLobbyVrStatus = SbText(p, "", new Vector2(0f, -70f), new Vector2(w - 60f, 30f), 19f, st.Text, TextAlignmentOptions.Center, FontStyles.Bold);

      SbImage(p, "bartrack", new Vector2(0f, -112f), new Vector2(SbLobbyBarW, 8f), SbStyles.Shape(st.Pixel ? 0 : 4, false, st.Pixel, 0), new Color(st.Sub.r, st.Sub.g, st.Sub.b, 0.25f));
      GameObject mask = SbRect("barmask", p, new Vector2(0f, -112f), new Vector2(SbLobbyBarW, 8f));
      mask.AddComponent<RectMask2D>();
      sbLobbyVrBarSeg = SbImage(mask.transform, "seg", Vector2.zero, new Vector2(140f, 8f), SbStyles.Shape(st.Pixel ? 0 : 4, false, st.Pixel, 0), st.Accent).rectTransform;
      sbLobbyVrBarFill = SbImage(mask.transform, "fill", new Vector2(-SbLobbyBarW * 0.5f, 0f), new Vector2(0f, 8f), SbStyles.Shape(st.Pixel ? 0 : 4, false, st.Pixel, 0), st.Accent);
      sbLobbyVrBarFill.rectTransform.pivot = new Vector2(0f, 0.5f);

      sbLobbyVrBuiltFor = st.Accent;
      sbLobbyVrSnap = true;
    }

    private void SbLobbyUpdateVr()
    {
      if (sbLobbyAlpha <= 0.001f || !IsHeadsetActive())
      {
        if (sbLobbyVr != null && sbLobbyVr.activeSelf) sbLobbyVr.SetActive(false);
        sbLobbyVrSnap = true;
        return;
      }
      SbStyle st = SbStyles.Get(sbStyleIdx);
      if (sbLobbyVr == null || sbLobbyVrBuiltFor != st.Accent) SbLobbyBuildVr(st);
      if (!sbLobbyVr.activeSelf) sbLobbyVr.SetActive(true);

      Transform head = this.GetTabletHead();
      if (head != null)
      {
        float scale = PlayerScale;
        Vector3 f = Vector3.ProjectOnPlane(head.forward, Vector3.up);
        if (f.sqrMagnitude < 1e-4f) f = head.forward;
        f.Normalize();
        Vector3 pos = head.position + f * 1.1f * scale - Vector3.up * 0.06f * scale;
        Quaternion rot = Quaternion.LookRotation(pos - head.position, Vector3.up);
        Transform t = sbLobbyVr.transform;
        if (sbLobbyVrSnap) { t.SetPositionAndRotation(pos, rot); sbLobbyVrSnap = false; }
        else
        {
          float k = 1f - Mathf.Exp(-Time.unscaledDeltaTime * 4f);
          t.SetPositionAndRotation(Vector3.Lerp(t.position, pos, k), Quaternion.Slerp(t.rotation, rot, k));
        }
        t.localScale = Vector3.one * 0.0012f * scale;
      }

      float now = Time.unscaledTime;
      sbLobbyVrGroup.alpha = sbLobbyAlpha;
      float pop = 0.92f + 0.08f * (1f - Mathf.Pow(1f - sbLobbyAlpha, 3f));
      if (sbLobbyPhase == SbLobbyPhase.Joined) pop += 0.03f * Mathf.Exp(-(now - sbLobbyPhaseAt) * 6f);
      sbLobbyVrCard.localScale = Vector3.one * pop;
      sbLobbyVrTitle.text = SbLobbyTitle();
      sbLobbyVrLobby.text = "LOBBY " + (sbLobbyIdx + 1);
      sbLobbyVrCode.text = sbLobbyTarget;
      sbLobbyVrStatus.text = SbLobbyStatus();
      sbLobbyVrStatus.color = sbLobbyPhase == SbLobbyPhase.Failed ? new Color(1f, 0.45f, 0.4f) : st.Text;

      bool joining = sbLobbyPhase == SbLobbyPhase.Joining;
      sbLobbyVrSpin.localRotation = Quaternion.Euler(0f, 0f, -now * (joining ? 240f : 40f));
      sbLobbyVrSpinDot.localScale = Vector3.one * (joining ? 1f + 0.25f * Mathf.Sin(now * 8f) : 1.2f);
      sbLobbyVrBarSeg.gameObject.SetActive(joining);
      sbLobbyVrBarFill.gameObject.SetActive(!joining);
      if (joining)
      {
        float u = (now * 0.75f) % 1f;
        sbLobbyVrBarSeg.anchoredPosition = new Vector2(Mathf.Lerp(-SbLobbyBarW * 0.5f - 80f, SbLobbyBarW * 0.5f + 80f, Mathf.SmoothStep(0f, 1f, u)), 0f);
      }
      else
      {
        float fill = sbLobbyPhase == SbLobbyPhase.Joined ? Mathf.Clamp01((now - sbLobbyPhaseAt) / 0.4f) : 1f;
        sbLobbyVrBarFill.rectTransform.sizeDelta = new Vector2(SbLobbyBarW * fill, 8f);
        sbLobbyVrBarFill.color = sbLobbyPhase == SbLobbyPhase.Failed ? new Color(1f, 0.35f, 0.3f) : st.Accent;
      }
    }

    // ================================================================ desktop card (OnGUI)
    private GUIStyle sbLobbyGuiTitle, sbLobbyGuiBig, sbLobbyGuiCode, sbLobbyGuiStatus;
    private float sbLobbyGuiScale = -1f;

    private void DrawSandBoxLobbyDesktop()
    {
      if (sbLobbyAlpha <= 0.001f) return;
      Event e = Event.current;
      if (e == null || e.type != EventType.Repaint) return;
      SbStyle st = SbStyles.Get(sbStyleIdx);
      float s = Mathf.Clamp(Screen.height / 1080f, 0.6f, 2.5f);
      if (sbLobbyGuiTitle == null || !Mathf.Approximately(sbLobbyGuiScale, s))
      {
        sbLobbyGuiScale = s;
        Func<int, FontStyle, TextAnchor, GUIStyle> make = (size, fs, anchor) => new GUIStyle(GUI.skin.label)
        {
          fontSize = Mathf.RoundToInt(size * s), fontStyle = fs, alignment = anchor, wordWrap = false, clipping = TextClipping.Clip, richText = false
        };
        sbLobbyGuiTitle = make(15, FontStyle.Bold, TextAnchor.MiddleLeft);
        sbLobbyGuiBig = make(46, FontStyle.Bold, TextAnchor.MiddleLeft);
        sbLobbyGuiCode = make(14, FontStyle.Bold, TextAnchor.MiddleLeft);
        sbLobbyGuiStatus = make(15, FontStyle.Bold, TextAnchor.MiddleCenter);
      }

      float a = sbLobbyAlpha;
      float now = Time.unscaledTime;
      Color keep = GUI.color;
      GUI.color = Color.white;
      Matrix4x4 keepM = GUI.matrix;
      GUI.matrix = Matrix4x4.identity;

      // dim everything behind it a little
      SbGui.RoundedRect(new Rect(0f, 0f, Screen.width, Screen.height), 0, new Color(0f, 0f, 0f, 0.5f * a), Color.clear, 0);

      float w = 480f * s, h = 230f * s;
      float pop = 0.94f + 0.06f * (1f - Mathf.Pow(1f - a, 3f));
      if (sbLobbyPhase == SbLobbyPhase.Joined) pop += 0.025f * Mathf.Exp(-(now - sbLobbyPhaseAt) * 6f);
      w *= pop; h *= pop;
      Rect card = new Rect((Screen.width - w) * 0.5f, (Screen.height - h) * 0.5f, w, h);
      Color acc = st.Accent;
      int rad = Mathf.RoundToInt(Mathf.Min(st.Radius, 18) * s);
      for (int i = 3; i >= 1; i--)
      {
        float grow = i * 7f * s;
        SbGui.RoundedRect(new Rect(card.x - grow, card.y - grow, card.width + grow * 2f, card.height + grow * 2f), rad + (int)grow, new Color(acc.r, acc.g, acc.b, 0.06f * a), Color.clear, 0);
      }
      Color bg = st.Bg; bg.a = 0.96f * a;
      Color tint = st.Bg2; tint.a *= a;
      Color border = st.Border; border.a *= a;
      SbGui.RoundedRect(card, rad, bg, Color.clear, 0);
      SbGui.RoundedRect(card, rad, tint, border, Mathf.Max(1, Mathf.RoundToInt(2f * s)));

      // spinner: twelve dots chasing round
      bool joining = sbLobbyPhase == SbLobbyPhase.Joining;
      Vector2 sc = new Vector2(card.x + 74f * s * pop, card.y + h * 0.40f);
      float sr = 30f * s;
      for (int i = 0; i < 12; i++)
      {
        float ang = i / 12f * Mathf.PI * 2f;
        float lit = joining ? Mathf.Repeat(now * 1.6f - i / 12f, 1f) : 1f;
        float d = (joining ? Mathf.Lerp(3f, 6.5f, Mathf.Pow(1f - lit, 3f)) : 4.5f) * s;
        Color dc = sbLobbyPhase == SbLobbyPhase.Failed ? new Color(1f, 0.4f, 0.35f) : Color.Lerp(acc, st.Accent2, i / 12f);
        dc.a = a * (joining ? Mathf.Lerp(0.2f, 1f, Mathf.Pow(1f - lit, 2f)) : 0.9f);
        Vector2 dp = sc + new Vector2(Mathf.Sin(ang), -Mathf.Cos(ang)) * sr;
        SbGui.RoundedRect(new Rect(dp.x - d, dp.y - d, d * 2f, d * 2f), (int)d, dc, Color.clear, 0);
      }

      float tx = card.x + 132f * s * pop, tw = card.xMax - tx - 20f * s;
      Color sub = st.Sub; sub.a *= a;
      Color text = st.Text; text.a *= a;
      Color accText = acc; accText.a = a;
      SbGuiLabel(new Rect(tx, card.y + 22f * s, tw, 24f * s), SbLobbyTitle(), sbLobbyGuiTitle, sub);
      SbGuiLabel(new Rect(tx, card.y + 44f * s, tw, 60f * s), "LOBBY " + (sbLobbyIdx + 1), sbLobbyGuiBig, text);
      SbGuiLabel(new Rect(tx, card.y + 104f * s, tw, 22f * s), sbLobbyTarget, sbLobbyGuiCode, accText);
      Color statusC = sbLobbyPhase == SbLobbyPhase.Failed ? new Color(1f, 0.5f, 0.45f, a) : text;
      SbGuiLabel(new Rect(card.x + 20f * s, card.y + h - 76f * s, w - 40f * s, 26f * s), SbLobbyStatus(), sbLobbyGuiStatus, statusC);

      // progress bar: a sweeping segment while joining, fills when done
      Rect bar = new Rect(card.x + 30f * s, card.y + h - 36f * s, w - 60f * s, 6f * s);
      SbGui.RoundedRect(bar, (int)(3f * s), new Color(st.Sub.r, st.Sub.g, st.Sub.b, 0.25f * a), Color.clear, 0);
      if (joining)
      {
        float u = Mathf.SmoothStep(0f, 1f, (now * 0.75f) % 1f);
        float segW = bar.width * 0.26f;
        float x0 = Mathf.Lerp(bar.x - segW, bar.xMax, u);
        float l = Mathf.Max(bar.x, x0), r = Mathf.Min(bar.xMax, x0 + segW);
        if (r > l) SbGui.RoundedRect(new Rect(l, bar.y, r - l, bar.height), (int)(3f * s), accText, Color.clear, 0);
      }
      else
      {
        float fill = sbLobbyPhase == SbLobbyPhase.Joined ? Mathf.Clamp01((now - sbLobbyPhaseAt) / 0.4f) : 1f;
        Color fc = sbLobbyPhase == SbLobbyPhase.Failed ? new Color(1f, 0.35f, 0.3f, a) : accText;
        SbGui.RoundedRect(new Rect(bar.x, bar.y, bar.width * fill, bar.height), (int)(3f * s), fc, Color.clear, 0);
      }

      GUI.matrix = keepM;
      GUI.color = keep;
    }

    // the Nether portal swirl on the desktop too, while you stand in a portal
    private void DrawSandBoxPortalSwirl()
    {
      float a = SandBoxWorld.PortalSwirl;
      if (a <= 0.001f) return;
      Event e = Event.current;
      if (e == null || e.type != EventType.Repaint) return;
      Texture2D tex = SbPortalOverlay.Texture;
      if (tex == null) return;
      Color keep = GUI.color;
      Matrix4x4 keepM = GUI.matrix;
      GUI.matrix = Matrix4x4.identity;
      float t = Time.unscaledTime;
      float tiles = 6f;
      Rect uv = new Rect(Mathf.Sin(t * 0.7f) * 0.3f, -t * 0.35f, tiles, tiles * Screen.height / Mathf.Max(1f, Screen.width));
      GUI.color = new Color(0.8f, 0.4f, 1f, Mathf.Clamp01(a) * 0.8f);
      GUI.DrawTextureWithTexCoords(new Rect(0f, 0f, Screen.width, Screen.height), tex, uv, true);
      GUI.matrix = keepM;
      GUI.color = keep;
    }

    private static void SbGuiLabel(Rect r, string text, GUIStyle style, Color c)
    {
      style.normal.textColor = c;
      GUI.Label(r, text, style);
    }

    // ================================================================ join / leave effect
    // In every lobby. Rigs are pooled: a rig whose Creator changes to someone new
    // is a player who just joined. Everyone already there when you arrive is left
    // alone. Leaving is caught just before the game takes the rig away.
    private readonly Dictionary<VRRig, NetPlayer> sbSpawnSeen = new Dictionary<VRRig, NetPlayer>();
    private readonly Dictionary<VRRig, float> sbSpawnPending = new Dictionary<VRRig, float>();
    private readonly List<VRRig> sbSpawnScratch = new List<VRRig>();
    private string sbSpawnRoom;
    private float sbSpawnRoomAt, sbSpawnScanAt;

    private void SbUpdateSpawnFx()
    {
      float now = Time.unscaledTime;
      if (now >= sbSpawnScanAt)
      {
        sbSpawnScanAt = now + 0.4f;
        string room = null;
        if (sbSpawnFxOn)
        {
          try { room = PhotonNetwork.InRoom && PhotonNetwork.CurrentRoom != null ? PhotonNetwork.CurrentRoom.Name : null; } catch { }
        }
        if (room != sbSpawnRoom)
        {
          sbSpawnRoom = room;
          sbSpawnRoomAt = now;
          sbSpawnSeen.Clear();
          sbSpawnPending.Clear();
        }
        if (room == null) return;
        bool settled = now - sbSpawnRoomAt > 3f;
        List<VRRig> rigs = SbRigs.All();
        for (int ri = 0; ri < rigs.Count; ri++)
        {
          VRRig rig = rigs[ri];
          if (rig == null || !rig.gameObject.activeInHierarchy || SbRigs.IsMine(rig)) continue;
          NetPlayer who;
          try { who = rig.Creator; } catch { continue; }
          if (who == null) continue;
          NetPlayer before;
          if (sbSpawnSeen.TryGetValue(rig, out before) && SamePlayer(before, who)) { sbSpawnSeen[rig] = who; continue; }
          sbSpawnSeen[rig] = who;
          // only players who joined after you (Photon numbers players in
          // the order they join): everyone already in the lobby you join, however
          // late their rig shows up, is left alone
          bool joinedAfterMe = false;
          try { joinedAfterMe = PhotonNetwork.LocalPlayer != null && who.ActorNumber > PhotonNetwork.LocalPlayer.ActorNumber; } catch { }
          // give the rig a moment to get its first position from the network
          if (settled && joinedAfterMe) sbSpawnPending[rig] = now + 0.35f;
        }
      }

      if (sbSpawnPending.Count == 0) return;
      sbSpawnScratch.Clear();
      foreach (KeyValuePair<VRRig, float> kv in sbSpawnPending) if (now >= kv.Value) sbSpawnScratch.Add(kv.Key);
      if (sbSpawnScratch.Count == 0) return;
      SbStyle st = SbStyles.Get(sbStyleIdx);
      foreach (VRRig rig in sbSpawnScratch)
      {
        sbSpawnPending.Remove(rig);
        if (rig != null && rig.gameObject.activeInHierarchy) SbSpawnFx.Play(rig, st.Accent, st.Accent2);
      }
    }

    // the same player even when the game hands over another NetPlayer object for them
    private static bool SamePlayer(NetPlayer a, NetPlayer b)
    {
      if (a == null || b == null) return false;
      if (ReferenceEquals(a, b)) return true;
      try
      {
        if (a.ActorNumber > 0 && a.ActorNumber == b.ActorNumber) return true;
        return !string.IsNullOrEmpty(a.UserId) && a.UserId == b.UserId;
      }
      catch { return false; }
    }

    // Called from a NetworkSystem.PlayerLeft prefix, while their rig is still there.
    internal void SbOnPlayerLeaving(NetPlayer player)
    {
      if (player == null || player.IsLocal || !sbSpawnFxOn) return;
      try
      {
        VRRig found = null;
        foreach (KeyValuePair<VRRig, NetPlayer> kv in sbSpawnSeen)
          if (kv.Key != null && SamePlayer(kv.Value, player)) { found = kv.Key; break; }
        if (found == null)
          foreach (VRRig rig in SbRigs.All())
            if (rig != null && SamePlayer(rig.Creator, player) && !SbRigs.IsMine(rig)) { found = rig; break; }
        if (found == null || !SamePlayer(found.Creator, player)) return;
        sbSpawnSeen.Remove(found);
        sbSpawnPending.Remove(found);
        SbStyle st = SbStyles.Get(sbStyleIdx);
        SbSpawnFx.PlayLeave(found, st.Accent, st.Accent2);
      }
      catch (Exception ex) { SbLog.Throttled("Player leave effect", ex); }
    }
  }
}
