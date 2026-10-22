using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace UnknownsSandbox
{
  // the SandBox tablet's keyboard. It comes up under the tablet, tilted towards
  // you like a laptop's, while a text box on the SERVERS pages is picked (server name,
  // password, code) and goes away with DONE. It is a fourth panel of the tablet, so the
  // pointer and finger presses work on it like on the others. Also the CREATE SERVER
  // button at the top right of the SERVERS page (it opens the page where you make one).
  public partial class SandboxPlugin
  {
    private const int SbKbPanel = 3;
    private const float SbKbW = 640f, SbKbH = 236f;
    private CanvasGroup sbKbGroup;
    private float sbKbShow;
    private SbCtl sbSrvHeaderBtn;

    private void SbBuildKeyboard(SbStyle st)
    {
      GameObject p = new GameObject("SB_Keyboard", typeof(RectTransform));
      p.transform.SetParent(sbRoot.transform, false);
      Canvas c = p.AddComponent<Canvas>();
      c.renderMode = RenderMode.WorldSpace;
      c.sortingOrder = 31031;
      var scaler = p.AddComponent<CanvasScaler>();
      scaler.dynamicPixelsPerUnit = 3f;
      var gr = p.AddComponent<GraphicRaycaster>();
      gr.enabled = false;
      sbKbGroup = p.AddComponent<CanvasGroup>();
      sbKbGroup.interactable = false;
      sbKbGroup.blocksRaycasts = false;
      RectTransform rt = p.GetComponent<RectTransform>();
      rt.sizeDelta = new Vector2(SbKbW, SbKbH);
      sbPanels[SbKbPanel] = p;
      sbPanelRts[SbKbPanel] = rt;
      SbPanelBackground(p.transform, st, SbKbW, SbKbH, SbKbPanel);

      string[] rows = { "1234567890", "QWERTYUIOP", "ASDFGHJKL", "ZXCVBNM" };
      float kw = 54f, kh = 38f, gap = 5f;
      float y = SbKbH * 0.5f - 18f - kh * 0.5f;
      foreach (string row in rows)
      {
        float start = -(row.Length * (kw + gap) - gap) * 0.5f + kw * 0.5f;
        for (int i = 0; i < row.Length; i++)
        {
          string k = row[i].ToString();
          SbButton(SbKbPanel, p.transform, k, new Vector2(start + i * (kw + gap), y), new Vector2(kw, kh), null,
            () => SbServerKey(sbSrvField == 3 ? k : (sbShiftLower ? k.ToLowerInvariant() : k)), 18f, st);
        }
        y -= kh + gap;
      }
      string[] specials = { "_", "abc", "SPACE", "BACK", "CLEAR", "DONE" };
      float[] widths = { 60f, 70f, 200f, 90f, 90f, 90f };
      float total = 0f;
      foreach (float wd in widths) total += wd + gap;
      float x = -(total - gap) * 0.5f;
      for (int i = 0; i < specials.Length; i++)
      {
        string k = specials[i];
        Func<bool> on = k == "abc" ? (Func<bool>)(() => sbShiftLower) : null;
        Action click = k == "abc" ? (Action)(() => { sbShiftLower = !sbShiftLower; }) : () => SbServerKey(k);
        SbCtl key = SbButton(SbKbPanel, p.transform, k, new Vector2(x + widths[i] * 0.5f, y), new Vector2(widths[i], kh), on, click, 16f, st);
        if (k == "DONE")
        {
          key.baseCol = Color.Lerp(st.Card, st.Accent, 0.35f);
          key.hoverCol = Color.Lerp(st.CardHover, st.Accent, 0.5f);
        }
        x += widths[i] + gap;
      }
      p.SetActive(false);
    }

    // under the middle panel, tilted back towards you; only while a box is picked
    private void SbPlaceKeyboard(float size, float dt)
    {
      GameObject kb = sbPanels[SbKbPanel];
      if (kb == null || sbPanels[1] == null) return;
      bool want = sbVisible && sbOpenT >= 0.9f && sbServerOpen && !sbSettingsOpen && sbSrvField != 0;
      sbKbShow = Mathf.MoveTowards(sbKbShow, want ? 1f : 0f, dt * (want ? 6f : 8f));
      if (sbKbShow <= 0f) { if (kb.activeSelf) kb.SetActive(false); return; }
      if (!kb.activeSelf) kb.SetActive(true);
      Transform c = sbPanels[1].transform;
      Quaternion flat = Quaternion.Euler(0f, c.rotation.eulerAngles.y, 0f);
      float s = 0.001f * size;
      float e = 1f - (1f - sbKbShow) * (1f - sbKbShow);
      // below the panel's bottom edge, a bit closer to you, tipped back 32 degrees
      Vector3 below = c.position + flat * Vector3.down * ((SbPanelH * 0.5f + SbKbH * 0.42f + 26f) * s) + flat * Vector3.back * (0.07f * size);
      kb.transform.position = below + flat * Vector3.down * (0.04f * size * (1f - e));
      kb.transform.rotation = flat * Quaternion.Euler(Mathf.Lerp(60f, 32f, e), 0f, 0f);
      kb.transform.localScale = new Vector3(1f, Mathf.Lerp(0.2f, 1f, e), 1f) * s;
      if (sbKbGroup != null) sbKbGroup.alpha = e;
    }

    // ---------------------------------------------------------------- CREATE SERVER (top right)
    private void SbBuildServerHeaderButton(Transform p, float w, SbStyle st)
    {
      sbSrvHeaderBtn = SbButton(1, p, "CREATE SERVER", new Vector2(w * 0.5f - 250f, 244f), new Vector2(170f, 40f), null, () =>
      {
        // BACK from the create / host page, else a fresh CREATE SERVER page
        if (sbSrvCreating || sbSrvHosting) { sbSrvCreating = false; sbSrvHosting = false; sbSrvField = 0; sbScroll = sbScrollTarget = 0f; SbRebuildGrid(); }
        else SbOpenCreatePage();
      }, 15f, st);
      sbSrvHeaderBtn.baseCol = Color.Lerp(st.Card, st.Accent, 0.35f);
      sbSrvHeaderBtn.hoverCol = Color.Lerp(st.CardHover, st.Accent, 0.5f);
      sbSrvHeaderBtn.rt.gameObject.SetActive(false);
    }

    private void SbUpdateServerHeader()
    {
      if (sbSrvHeaderBtn == null || sbSrvHeaderBtn.rt == null) return;
      bool show = sbServerOpen && !sbSettingsOpen;
      sbSrvHeaderBtn.rt.gameObject.SetActive(show);
      if (!show) { sbSrvCreating = false; sbSrvHosting = false; }
      if (sbSrvHeaderBtn.label != null) sbSrvHeaderBtn.label.text = (sbSrvCreating || sbSrvHosting) ? "BACK" : "CREATE SERVER";
    }
  }
}
