using System.Collections.Generic;
using UnityEngine;

namespace UnknownsSandbox
{
  // IMGUI controls for the desktop window, coloured from the tablet's current style
  // (SbStyle) so the window and the tablet always match.
  internal static class SbGui
  {
    internal const float RowH = 30f;

    private static SbStyle st;
    private static GUIStyle label, sub, subRight, header, title, button, field;
    private static readonly Dictionary<string, float> knobs = new Dictionary<string, float>();

    internal static SbStyle Style => st;
    internal static int Radius(int max) => st.Pixel ? 0 : Mathf.Min(st.Radius, max);

    // Called at the start of every OnGUI with the tablet's style.
    internal static void Begin(SbStyle style)
    {
      st = style;
      if (label == null)
      {
        label = new GUIStyle(GUI.skin.label) { fontSize = 13, alignment = TextAnchor.MiddleLeft, wordWrap = false, clipping = TextClipping.Clip };
        sub = new GUIStyle(label) { fontSize = 12, wordWrap = true };
        subRight = new GUIStyle(label) { fontSize = 12, alignment = TextAnchor.MiddleRight };
        header = new GUIStyle(label) { fontSize = 13, fontStyle = FontStyle.Bold };
        title = new GUIStyle(label) { fontSize = 18, fontStyle = FontStyle.Bold };
        button = new GUIStyle(label) { fontSize = 12, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
        field = new GUIStyle(GUI.skin.textField) { fontSize = 13, alignment = TextAnchor.MiddleLeft, padding = new RectOffset(10, 10, 0, 0) };
        foreach (GUIStyleState s in new[] { field.normal, field.hover, field.active, field.focused, field.onNormal, field.onFocused })
          s.background = null;
      }
      label.normal.textColor = st.Text;
      sub.normal.textColor = subRight.normal.textColor = st.Sub;
      header.normal.textColor = st.Accent;
      title.normal.textColor = st.Text;
      button.normal.textColor = st.Text;
      field.normal.textColor = field.focused.textColor = field.hover.textColor = st.Text;
      GUI.skin.settings.cursorColor = st.Accent;
      GUI.skin.settings.selectionColor = new Color(st.Accent.r, st.Accent.g, st.Accent.b, 0.35f);
    }

    internal static void RoundedRect(Rect r, int radius, Color fill, Color border, int borderThickness)
    {
      Event e = Event.current;
      if (e == null || e.type != EventType.Repaint || r.width <= 0.5f || r.height <= 0.5f) return;
      float rad = Mathf.Max(0f, Mathf.Min(radius, Mathf.Min(r.width, r.height) * 0.5f));
      if (fill.a > 0.001f)
        GUI.DrawTexture(r, Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0f, fill * GUI.color, 0f, rad);
      if (borderThickness > 0 && border.a > 0.001f)
        GUI.DrawTexture(r, Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0f, border * GUI.color, borderThickness, rad);
    }

    internal static Color WithAlpha(Color c, float a) { c.a = a; return c; }

    // ---------------------------------------------------------------- text
    internal static void Title(Rect r, string text) => GUI.Label(r, text, title);
    internal static void Label(Rect r, string text) => GUI.Label(r, text, label);
    internal static void SubLabel(Rect r, string text) => GUI.Label(r, text, sub);

    internal static void Section(string text)
    {
      GUILayout.Space(8f);
      Rect r = GUILayoutUtility.GetRect(GUIContent.none, header, GUILayout.Height(22f), GUILayout.ExpandWidth(true));
      GUI.Label(r, text.ToUpperInvariant(), header);
      RoundedRect(new Rect(r.x, r.yMax - 2f, r.width, 1f), 0, WithAlpha(st.Accent, 0.35f), Color.clear, 0);
      GUILayout.Space(4f);
    }

    internal static void Note(string text)
    {
      GUILayout.Label(text, sub, GUILayout.ExpandWidth(true));
    }

    // ---------------------------------------------------------------- controls
    internal static bool Button(string text, bool active = false, float height = RowH, params GUILayoutOption[] extra)
    {
      var opts = new List<GUILayoutOption>(extra) { GUILayout.Height(height) };
      if (extra.Length == 0) opts.Add(GUILayout.ExpandWidth(true));
      Rect r = GUILayoutUtility.GetRect(GUIContent.none, button, opts.ToArray());
      return Button(r, text, active);
    }

    // The same colours as a tablet button: card, hover card, and a blend towards the accent when on.
    internal static bool Button(Rect r, string text, bool active)
    {
      bool hover = r.Contains(Event.current.mousePosition);
      Color fill = active ? Color.Lerp(st.Card, st.Accent, 0.55f) : hover ? st.CardHover : st.Card;
      if (active && hover) fill = Color.Lerp(fill, st.CardHover, 0.3f);
      Color edge = WithAlpha(st.Accent, active ? 1f : hover ? 0.7f : 0f);
      RoundedRect(r, Radius(10), fill, edge, 2);
      GUI.Label(r, text, button);
      return GUI.Button(r, GUIContent.none, GUIStyle.none);
    }

    internal static bool Toggle(string text, bool value)
    {
      Rect row = GUILayoutUtility.GetRect(GUIContent.none, GUIStyle.none, GUILayout.Height(RowH), GUILayout.ExpandWidth(true));
      bool hover = row.Contains(Event.current.mousePosition);
      RoundedRect(row, Radius(10), hover ? st.CardHover : st.Card, Color.clear, 0);
      GUI.Label(new Rect(row.x + 12f, row.y, row.width - 70f, row.height), text, label);

      Rect track = new Rect(row.xMax - 50f, row.y + (row.height - 18f) * 0.5f, 36f, 18f);
      float k;
      if (!knobs.TryGetValue(text, out k)) k = value ? 1f : 0f;
      if (Event.current.type == EventType.Repaint)
      {
        k = Mathf.MoveTowards(k, value ? 1f : 0f, Time.unscaledDeltaTime * 8f);
        knobs[text] = k;
      }
      RoundedRect(track, Radius(9), Color.Lerp(WithAlpha(st.Sub, 0.35f), st.Accent, k), Color.clear, 0);
      float knob = 14f;
      RoundedRect(new Rect(track.x + 2f + (track.width - knob - 4f) * k, track.y + 2f, knob, knob), Radius(7), st.Text, Color.clear, 0);

      if (GUI.Button(row, GUIContent.none, GUIStyle.none)) return !value;
      return value;
    }

    // A label, the value on the right, and a slider like the tablet's (track, accent fill, knob).
    internal static float Slider(string text, float value, float min, float max, string shown)
    {
      Rect row = GUILayoutUtility.GetRect(GUIContent.none, GUIStyle.none, GUILayout.Height(40f), GUILayout.ExpandWidth(true));
      GUI.Label(new Rect(row.x + 2f, row.y, row.width * 0.6f, 18f), text, label);
      GUI.Label(new Rect(row.x + row.width * 0.5f, row.y, row.width * 0.5f - 2f, 18f), shown, subRight);

      Rect track = new Rect(row.x + 8f, row.y + 26f, row.width - 16f, 6f);
      float t = Mathf.InverseLerp(min, max, value);
      RoundedRect(track, Radius(3), WithAlpha(st.Sub, 0.35f), Color.clear, 0);
      RoundedRect(new Rect(track.x, track.y, track.width * t, track.height), Radius(3), st.Accent, Color.clear, 0);
      Rect knob = new Rect(track.x + track.width * t - 8f, track.center.y - 8f, 16f, 16f);
      RoundedRect(knob, Radius(8), st.Text, Color.clear, 0);

      Rect hit = new Rect(row.x, row.y + 18f, row.width, 22f);
      int id = GUIUtility.GetControlID(FocusType.Passive, hit);
      Event e = Event.current;
      switch (e.GetTypeForControl(id))
      {
        case EventType.MouseDown:
          if (hit.Contains(e.mousePosition) && e.button == 0) { GUIUtility.hotControl = id; e.Use(); goto case EventType.MouseDrag; }
          break;
        case EventType.MouseDrag:
          if (GUIUtility.hotControl == id)
          {
            value = Mathf.Lerp(min, max, Mathf.InverseLerp(track.x, track.xMax, e.mousePosition.x));
            GUI.changed = true;
            e.Use();
          }
          break;
        case EventType.MouseUp:
          if (GUIUtility.hotControl == id) { GUIUtility.hotControl = 0; e.Use(); }
          break;
      }
      return value;
    }

    internal static bool SliderHeld => GUIUtility.hotControl != 0;

    internal static string TextField(string name, string value, int max, bool password = false)
    {
      Rect r = GUILayoutUtility.GetRect(GUIContent.none, field, GUILayout.Height(RowH), GUILayout.ExpandWidth(true));
      GUI.SetNextControlName(name);
      bool focused = GUI.GetNameOfFocusedControl() == name;
      // clicking anywhere else lets go of the keyboard, so the window's hotkey works again
      if (focused && Event.current.type == EventType.MouseDown && !r.Contains(Event.current.mousePosition))
      {
        GUIUtility.keyboardControl = 0;
        focused = false;
      }
      RoundedRect(r, Radius(10), st.Card, WithAlpha(st.Accent, focused ? 1f : 0.25f), focused ? 2 : 1);
      return password ? GUI.PasswordField(r, value ?? string.Empty, '*', max, field) : GUI.TextField(r, value ?? string.Empty, max, field);
    }

    internal static void FieldLabel(string text)
    {
      GUILayout.Label(text, sub, GUILayout.Height(18f));
    }

    // Two buttons on one row.
    internal static int ButtonPair(string a, string b, bool aActive = false, bool bActive = false)
    {
      GUILayout.BeginHorizontal();
      bool pa = Button(a, aActive);
      GUILayout.Space(6f);
      bool pb = Button(b, bActive);
      GUILayout.EndHorizontal();
      return pa ? 0 : pb ? 1 : -1;
    }

    // A setting that steps through choices when clicked: "LABEL" on the left, the value on a button.
    internal static bool Cycle(string text, string shown)
    {
      GUILayout.BeginHorizontal();
      Rect l = GUILayoutUtility.GetRect(GUIContent.none, label, GUILayout.Height(RowH), GUILayout.Width(150f));
      GUI.Label(new Rect(l.x + 2f, l.y, l.width, l.height), text, label);
      bool pressed = Button(shown);
      GUILayout.EndHorizontal();
      return pressed;
    }
  }
}
