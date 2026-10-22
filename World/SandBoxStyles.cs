using System.Collections.Generic;
using UnityEngine;

namespace UnknownsSandbox
{
  // SandBox Tablet looks. Each style is a full set of colours, corner shape,
  // background pattern and glow; the panels are rebuilt when it changes.
  internal sealed class SbStyle
  {
    internal string Name;
    internal Color Bg, Bg2, Border, Accent, Accent2, Text, Sub, Card, CardHover;
    internal int Radius = 18;
    internal bool Cut;
    internal int Pattern;        // 0 none, 1 scanlines, 2 grid, 3 pixel, 4 dots, 5 diagonal
    internal float PatternAlpha = 0.12f;
    internal float Glow = 0.5f;
    internal bool Pixel;
    internal bool Brackets = true;

    internal SbStyle Clone() { return (SbStyle)MemberwiseClone(); }
  }

  internal static class SbStyles
  {
    internal static readonly List<SbStyle> All = new List<SbStyle>
    {
      // purple is the default look
      new SbStyle
      {
        Name = "PURPLE", Bg = new Color(0.07f, 0.03f, 0.12f, 0.88f), Bg2 = new Color(0.32f, 0.12f, 0.52f, 0.55f),
        Border = new Color(0.72f, 0.45f, 1f, 0.95f), Accent = new Color(0.74f, 0.47f, 1f), Accent2 = new Color(0.95f, 0.55f, 1f),
        Text = new Color(0.97f, 0.93f, 1f), Sub = new Color(0.74f, 0.62f, 0.9f), Card = new Color(0.16f, 0.07f, 0.27f, 0.88f),
        CardHover = new Color(0.3f, 0.13f, 0.48f, 0.94f), Radius = 18, Pattern = 1, PatternAlpha = 0.09f, Glow = 0.6f
      },
      new SbStyle
      {
        Name = "HOLO", Bg = new Color(0.02f, 0.07f, 0.11f, 0.84f), Bg2 = new Color(0.03f, 0.25f, 0.33f, 0.55f),
        Border = new Color(0.3f, 0.95f, 1f, 0.95f), Accent = new Color(0.3f, 0.95f, 1f), Accent2 = new Color(0.62f, 0.45f, 1f),
        Text = new Color(0.88f, 0.98f, 1f), Sub = new Color(0.5f, 0.76f, 0.84f), Card = new Color(0.05f, 0.17f, 0.24f, 0.86f),
        CardHover = new Color(0.08f, 0.3f, 0.4f, 0.92f), Radius = 18, Pattern = 1, PatternAlpha = 0.10f, Glow = 0.6f
      },
      new SbStyle
      {
        Name = "SYNTHWAVE", Bg = new Color(0.07f, 0.02f, 0.13f, 0.9f), Bg2 = new Color(0.4f, 0.05f, 0.38f, 0.55f),
        Border = new Color(1f, 0.32f, 0.86f, 0.95f), Accent = new Color(1f, 0.38f, 0.88f), Accent2 = new Color(1f, 0.62f, 0.18f),
        Text = new Color(1f, 0.93f, 1f), Sub = new Color(0.86f, 0.6f, 0.92f), Card = new Color(0.17f, 0.04f, 0.25f, 0.9f),
        CardHover = new Color(0.32f, 0.07f, 0.4f, 0.95f), Radius = 10, Pattern = 2, PatternAlpha = 0.16f, Glow = 0.65f
      },
      new SbStyle
      {
        Name = "STEALTH", Bg = new Color(0.035f, 0.035f, 0.04f, 0.96f), Bg2 = new Color(0.14f, 0.14f, 0.15f, 0.55f),
        Border = new Color(0.92f, 0.13f, 0.16f, 0.95f), Accent = new Color(1f, 0.2f, 0.22f), Accent2 = new Color(1f, 1f, 1f),
        Text = new Color(0.96f, 0.96f, 0.96f), Sub = new Color(0.56f, 0.56f, 0.6f), Card = new Color(0.1f, 0.1f, 0.11f, 0.96f),
        CardHover = new Color(0.2f, 0.12f, 0.13f, 0.98f), Radius = 16, Cut = true, Pattern = 5, PatternAlpha = 0.07f, Glow = 0.28f
      },
      new SbStyle
      {
        Name = "AURORA", Bg = new Color(0.02f, 0.1f, 0.1f, 0.86f), Bg2 = new Color(0.12f, 0.38f, 0.33f, 0.5f),
        Border = new Color(0.42f, 1f, 0.78f, 0.9f), Accent = new Color(0.38f, 1f, 0.72f), Accent2 = new Color(0.42f, 0.6f, 1f),
        Text = new Color(0.9f, 1f, 0.96f), Sub = new Color(0.56f, 0.86f, 0.76f), Card = new Color(0.05f, 0.21f, 0.19f, 0.88f),
        CardHover = new Color(0.08f, 0.34f, 0.3f, 0.93f), Radius = 28, Pattern = 4, PatternAlpha = 0.10f, Glow = 0.55f
      },
      new SbStyle
      {
        Name = "CRAFT", Bg = new Color(0.25f, 0.25f, 0.25f, 0.98f), Bg2 = new Color(0.1f, 0.1f, 0.1f, 0.35f),
        Border = new Color(0.04f, 0.04f, 0.04f, 1f), Accent = new Color(0.45f, 0.86f, 0.3f), Accent2 = new Color(1f, 0.86f, 0.2f),
        Text = new Color(1f, 1f, 1f), Sub = new Color(0.78f, 0.78f, 0.78f), Card = new Color(0.45f, 0.45f, 0.45f, 1f),
        CardHover = new Color(0.55f, 0.6f, 0.75f, 1f), Radius = 0, Pattern = 3, PatternAlpha = 0.22f, Glow = 0f, Pixel = true, Brackets = false
      },
      new SbStyle
      {
        Name = "FROST", Bg = new Color(0.93f, 0.95f, 0.99f, 0.92f), Bg2 = new Color(0.78f, 0.87f, 1f, 0.55f),
        Border = new Color(0.36f, 0.56f, 1f, 0.9f), Accent = new Color(0.22f, 0.46f, 1f), Accent2 = new Color(1f, 0.46f, 0.32f),
        Text = new Color(0.08f, 0.1f, 0.17f), Sub = new Color(0.36f, 0.41f, 0.52f), Card = new Color(1f, 1f, 1f, 0.96f),
        CardHover = new Color(0.86f, 0.92f, 1f, 1f), Radius = 22, Pattern = 0, Glow = 0.35f
      },
    };

    // a custom accent colour laid over any style (set in the desktop window)
    internal static bool UseCustomAccent;
    internal static Color CustomAccent = new Color(0.74f, 0.47f, 1f);
    private static SbStyle customFor, customStyle;
    private static Color customBuiltWith;

    internal static SbStyle Get(int i)
    {
      SbStyle s = All[Mathf.Clamp(i, 0, All.Count - 1)];
      if (!UseCustomAccent) return s;
      if (customStyle == null || customFor != s || customBuiltWith != CustomAccent)
      {
        customStyle = s.Clone();
        Color a = CustomAccent; a.a = 1f;
        float h, sat, v;
        Color.RGBToHSV(a, out h, out sat, out v);
        customStyle.Accent = a;
        customStyle.Accent2 = Color.HSVToRGB((h + 0.08f) % 1f, Mathf.Clamp01(sat * 0.85f), Mathf.Clamp01(v + 0.1f));
        customStyle.Border = new Color(a.r, a.g, a.b, s.Border.a);
        // the tinted background follows the accent too
        customStyle.Bg2 = new Color(a.r * 0.45f, a.g * 0.45f, a.b * 0.45f, s.Bg2.a);
        customFor = s;
        customBuiltWith = CustomAccent;
      }
      return customStyle;
    }

    private static readonly Dictionary<string, Sprite> sprites = new Dictionary<string, Sprite>();
    private static readonly Dictionary<string, Texture2D> textures = new Dictionary<string, Texture2D>();

    // mode: 0 fill, 1 border ring, 2 soft glow, 3 vertical gradient fill (alpha 0 top -> 1 bottom)
    internal static Sprite Shape(int radius, bool cut, bool pixel, int mode, int borderPx = 3)
    {
      string key = radius + "|" + cut + "|" + pixel + "|" + mode + "|" + borderPx;
      Sprite sp;
      if (sprites.TryGetValue(key, out sp) && sp != null) return sp;
      int r = Mathf.Max(radius, 4);
      int pad = mode == 2 ? 24 : 0;
      int size = (r + pad) * 2 + 8;
      var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = pixel ? FilterMode.Point : FilterMode.Bilinear, hideFlags = HideFlags.DontSave, name = "Unknowns SandBox UI " + key };
      var px = new Color32[size * size];
      float inner = pad;
      for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
          float fx = x + 0.5f, fy = y + 0.5f;
          float d = SignedDistance(fx, fy, size, inner, radius, cut, pixel);  // <0 inside
          float a;
          if (mode == 0 || mode == 3) a = Mathf.Clamp01(0.5f - d);
          else if (mode == 1) a = Mathf.Clamp01(0.5f - d) * Mathf.Clamp01(d + borderPx + 0.5f);
          else a = d < 0f ? 0f : Mathf.Pow(Mathf.Clamp01(1f - d / pad), 2.2f);
          if (mode == 3) a *= Mathf.Clamp01(1f - fy / size);
          px[y * size + x] = new Color(1f, 1f, 1f, a);
        }
      tex.SetPixels32(px);
      tex.Apply(false, false);
      float b = r + pad + 2;
      sp = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(b, b, b, b));
      sprites[key] = sp;
      return sp;
    }

    private static float SignedDistance(float x, float y, int size, float pad, int radius, bool cut, bool pixel)
    {
      float minX = pad, minY = pad, maxX = size - pad, maxY = size - pad;
      float r = radius;
      if (pixel)
      {
        // chunky stepped corners (Minecraft-ish)
        float dx0 = Mathf.Max(minX - x, x - maxX), dy0 = Mathf.Max(minY - y, y - maxY);
        float d = Mathf.Max(dx0, dy0);
        float cx = Mathf.Min(x - minX, maxX - x), cy = Mathf.Min(y - minY, maxY - y);
        if (cx < 4f && cy < 4f) return 1f;
        return d;
      }
      float h = size * 0.5f - pad;
      float ax = Mathf.Abs(x - size * 0.5f), ay = Mathf.Abs(y - size * 0.5f);
      float ox = ax - h, oy = ay - h;
      if (cut)
      {
        float box = Mathf.Max(ox, oy);
        float corner = (ox + oy + r) / 1.41421f;
        return Mathf.Max(box, corner);
      }
      if (r <= 0.5f) return Mathf.Max(ox, oy);
      float qx = ax - (h - r), qy = ay - (h - r);
      float mx = Mathf.Max(qx, 0f), my = Mathf.Max(qy, 0f);
      return Mathf.Sqrt(mx * mx + my * my) + Mathf.Min(Mathf.Max(qx, qy), 0f) - r;
    }

    internal static Texture2D Pattern(int kind)
    {
      string key = "pat" + kind;
      Texture2D t;
      if (textures.TryGetValue(key, out t) && t != null) return t;
      int s = 32;
      t = new Texture2D(s, s, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Repeat, filterMode = kind == 3 ? FilterMode.Point : FilterMode.Bilinear, hideFlags = HideFlags.DontSave, name = "Unknowns SandBox Pattern" };
      var px = new Color32[s * s];
      for (int y = 0; y < s; y++)
        for (int x = 0; x < s; x++)
        {
          float a = 0f;
          switch (kind)
          {
            case 1: a = (y % 4 < 2) ? 1f : 0f; break;
            case 2: a = (x % 16 == 0 || y % 16 == 0) ? 1f : 0f; break;
            case 3:
              {
                uint h = (uint)((x / 2) * 73856093 ^ (y / 2) * 19349663);
                h = (h ^ (h >> 13)) * 1274126177u;
                a = (h & 255) / 255f;
                break;
              }
            case 4: { float dx = x % 8 - 3.5f, dy = y % 8 - 3.5f; a = dx * dx + dy * dy < 2.2f ? 1f : 0f; break; }
            case 5: a = ((x + y) % 12 < 2) ? 1f : 0f; break;
          }
          px[y * s + x] = new Color(1f, 1f, 1f, a);
        }
      t.SetPixels32(px);
      t.Apply(false, false);
      textures[key] = t;
      return t;
    }

    // small UI icons drawn in code
    internal static Texture2D Icon(string name)
    {
      Texture2D t;
      if (textures.TryGetValue("icon" + name, out t) && t != null) return t;
      int s = 64;
      t = new Texture2D(s, s, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.DontSave, name = "Unknowns SandBox Icon " + name };
      var px = new Color32[s * s];
      for (int y = 0; y < s; y++)
        for (int x = 0; x < s; x++)
        {
          float u = (x + 0.5f) / s * 2f - 1f, v = (y + 0.5f) / s * 2f - 1f;
          float a = 0f;
          float r = Mathf.Sqrt(u * u + v * v);
          float ang = Mathf.Atan2(v, u);
          switch (name)
          {
            case "gear":
              {
                float teeth = 0.72f + 0.14f * Mathf.Clamp(Mathf.Sin(ang * 8f) * 3f, -1f, 1f);
                a = Mathf.Clamp01((teeth - r) * 40f) * Mathf.Clamp01((r - 0.3f) * 40f);
                break;
              }
            case "back":
              {
                // left arrow
                float body = (Mathf.Abs(v) < 0.12f && u > -0.35f && u < 0.65f) ? 1f : 0f;
                float head = (u < -0.1f && u > -0.7f && Mathf.Abs(v) < (u + 0.7f) * 0.9f) ? 1f : 0f;
                a = Mathf.Max(body, head);
                break;
              }
            case "up": a = (v < 0.45f && v > -0.4f && Mathf.Abs(u) < (0.45f - v) * 0.8f) ? 1f : 0f; break;
            case "down": a = (v > -0.45f && v < 0.4f && Mathf.Abs(u) < (v + 0.45f) * 0.8f) ? 1f : 0f; break;
            case "close":
              {
                float d1 = Mathf.Abs(u - v) / 1.414f, d2 = Mathf.Abs(u + v) / 1.414f;
                a = (Mathf.Min(d1, d2) < 0.1f && r < 0.75f) ? 1f : 0f;
                break;
              }
            case "dot": a = Mathf.Clamp01((0.8f - r) * 20f); break;
            case "ring": a = Mathf.Clamp01((0.9f - r) * 20f) * Mathf.Clamp01((r - 0.7f) * 20f); break;
          }
          px[y * s + x] = new Color(1f, 1f, 1f, a);
        }
      t.SetPixels32(px);
      t.Apply(false, false);
      textures["icon" + name] = t;
      return t;
    }
  }
}
