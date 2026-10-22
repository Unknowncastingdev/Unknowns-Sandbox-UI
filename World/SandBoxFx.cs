using System;
using System.Collections.Generic;
using UnityEngine;

namespace UnknownsSandbox
{
  internal enum SbSound
  {
    Boom, Nuke, Shot, Shotgun, Sniper, Laser, Rocket, Pop, Splat, Squeak, Puff, Bubble, Confetti, Place, Break, Glass,
    Splash, Sizzle, Fuse, Beep, Siren, Swing, Hit, UiOpen, UiClose, UiHover, UiClick, Flame, Minigun, Thunk, Crackle, Suck
  }

  // Sounds are made in code at start (no audio files). Particles use the
  // shared soft-dot / smoke textures from SbMats.
  internal static class SbFx
  {
    private const int Rate = 44100;
    private static readonly Dictionary<SbSound, AudioClip> clips = new Dictionary<SbSound, AudioClip>();
    private static AudioSource[] pool;
    private static int poolNext;
    private static GameObject root;
    internal static float Volume = 1f;
    private static System.Random rng = new System.Random(1234);

    internal static Transform Root
    {
      get
      {
        if (root == null)
        {
          root = new GameObject("Unknowns_SandBoxFx");
          UnityEngine.Object.DontDestroyOnLoad(root);
        }
        return root.transform;
      }
    }

    // ---------------------------------------------------------------- audio
    private static float N() { return (float)(rng.NextDouble() * 2.0 - 1.0); }

    private static AudioClip Make(string name, float seconds, Func<float, float> f)
    {
      int n = Mathf.Max(1, (int)(seconds * Rate));
      float[] d = new float[n];
      float peak = 0.0001f;
      for (int i = 0; i < n; i++) { d[i] = f(i / (float)Rate); peak = Mathf.Max(peak, Mathf.Abs(d[i])); }
      float g = 0.92f / peak;
      // short fade so nothing clicks at the end
      int fade = Mathf.Min(n, 400);
      for (int i = 0; i < n; i++)
      {
        float e = i >= n - fade ? (n - i) / (float)fade : 1f;
        d[i] *= g * e;
      }
      // The PCM reader overload keeps ReadOnlySpan (SetData) out of the net48 build.
      // Unity reads the clip in chunks (~4096 samples), so the reader must
      // carry on from where it is; restarting at 0 every chunk is what made the
      // longer sounds (explosions, shotgun, sniper, nuke) stutter and loop.
      int pos = 0;
      AudioClip c = AudioClip.Create("Unknowns SandBox " + name, n, 1, Rate, false, delegate(float[] data)
      {
        int count = Math.Max(0, Math.Min(d.Length - pos, data.Length));
        if (count > 0) Array.Copy(d, pos, data, 0, count);
        if (count < data.Length) Array.Clear(data, count, data.Length - count);
        pos += count;
      }, delegate(int newPos) { pos = Mathf.Clamp(newPos, 0, d.Length); });
      return c;
    }

    private sealed class LP
    {
      private float y; private readonly float a;
      internal LP(float cutoff) { a = 1f - Mathf.Exp(-2f * Mathf.PI * cutoff / Rate); }
      internal float Step(float x) { y += a * (x - y); return y; }
    }

    private static void BuildClips()
    {
      if (clips.Count > 0) return;
      {
        var lp = new LP(900f); var lp2 = new LP(120f);
        clips[SbSound.Boom] = Make("boom", 1.8f, t =>
        {
          float n = N();
          float body = lp.Step(n) * Mathf.Exp(-t * 2.6f) * 2.2f;
          float sub = lp2.Step(n) * Mathf.Exp(-t * 1.6f) * 6f;
          float thump = Mathf.Sin(2f * Mathf.PI * (55f - 25f * t) * t) * Mathf.Exp(-t * 5f) * 0.9f;
          float crack = t < 0.03f ? n * (1f - t / 0.03f) : 0f;
          return body + sub + thump + crack;
        });
      }
      {
        var lp = new LP(160f); var lp2 = new LP(500f);
        clips[SbSound.Nuke] = Make("nuke", 7.5f, t =>
        {
          float n = N();
          float att = Mathf.Clamp01(t / 0.08f);
          float rumble = lp.Step(n) * 5f * Mathf.Exp(-t * 0.45f);
          float roar = lp2.Step(n) * 1.4f * Mathf.Exp(-t * 0.9f);
          float thump = Mathf.Sin(2f * Mathf.PI * (38f - 8f * t) * t) * Mathf.Exp(-t * 1.4f) * 0.8f;
          float crackle = (rng.NextDouble() < 0.004 * Mathf.Exp(-t * 0.5f)) ? N() * 0.9f : 0f;
          return att * (rumble + roar + thump) + crackle;
        });
      }
      {
        var lp = new LP(3500f);
        clips[SbSound.Shot] = Make("shot", 0.32f, t =>
          lp.Step(N()) * Mathf.Exp(-t * 26f) * 1.3f + Mathf.Sin(2f * Mathf.PI * 140f * t) * Mathf.Exp(-t * 45f) * 0.8f);
      }
      {
        var lp = new LP(2200f);
        clips[SbSound.Shotgun] = Make("shotgun", 0.6f, t =>
          lp.Step(N()) * Mathf.Exp(-t * 11f) * 1.5f + Mathf.Sin(2f * Mathf.PI * 90f * t) * Mathf.Exp(-t * 22f));
      }
      {
        var lp = new LP(4500f);
        clips[SbSound.Sniper] = Make("sniper", 1.1f, t =>
          lp.Step(N()) * Mathf.Exp(-t * 7f) * 1.4f + Mathf.Sin(2f * Mathf.PI * 70f * t) * Mathf.Exp(-t * 14f) +
          (t > 0.25f ? N() * 0.15f * Mathf.Exp(-(t - 0.25f) * 6f) : 0f));
      }
      {
        float ph = 0f;
        clips[SbSound.Laser] = Make("laser", 0.3f, t =>
        {
          float f = Mathf.Lerp(2000f, 260f, t / 0.3f) * (1f + 0.05f * Mathf.Sin(t * 180f));
          ph += 2f * Mathf.PI * f / Rate;
          return (Mathf.Sin(ph) + 0.3f * Mathf.Sign(Mathf.Sin(ph))) * Mathf.Exp(-t * 7f);
        });
      }
      {
        var lp = new LP(700f);
        clips[SbSound.Rocket] = Make("rocket", 0.9f, t =>
          lp.Step(N()) * Mathf.Clamp01(t / 0.05f) * Mathf.Exp(-t * 3.2f) * 2f + Mathf.Sin(2f * Mathf.PI * 100f * t) * Mathf.Exp(-t * 30f));
      }
      {
        float ph = 0f;
        clips[SbSound.Pop] = Make("pop", 0.16f, t =>
        {
          ph += 2f * Mathf.PI * Mathf.Lerp(380f, 110f, t / 0.16f) / Rate;
          return Mathf.Sin(ph) * Mathf.Exp(-t * 22f) + (t < 0.006f ? N() : 0f);
        });
      }
      {
        var lp = new LP(1100f);
        clips[SbSound.Splat] = Make("splat", 0.45f, t =>
          lp.Step(N()) * Mathf.Exp(-t * 9f) * (0.7f + 0.3f * Mathf.Sin(t * 190f)) * 1.6f + Mathf.Sin(2f * Mathf.PI * 95f * t) * Mathf.Exp(-t * 25f) * 0.6f);
      }
      {
        float ph = 0f;
        clips[SbSound.Squeak] = Make("squeak", 0.28f, t =>
        {
          float f = t < 0.1f ? Mathf.Lerp(1100f, 1700f, t / 0.1f) : Mathf.Lerp(1700f, 1000f, (t - 0.1f) / 0.18f);
          ph += 2f * Mathf.PI * f / Rate;
          return (Mathf.Sin(ph) + 0.35f * Mathf.Sin(ph * 2f)) * Mathf.Sin(Mathf.PI * Mathf.Clamp01(t / 0.28f));
        });
      }
      {
        var lp = new LP(1500f);
        clips[SbSound.Puff] = Make("puff", 0.3f, t => lp.Step(N()) * Mathf.Exp(-t * 14f));
      }
      {
        float ph = 0f;
        clips[SbSound.Bubble] = Make("bubble", 0.06f, t =>
        {
          ph += 2f * Mathf.PI * Mathf.Lerp(500f, 1500f, t / 0.06f) / Rate;
          return Mathf.Sin(ph) * Mathf.Exp(-t * 40f);
        });
      }
      clips[SbSound.Confetti] = Make("confetti", 0.9f, t =>
      {
        float pop = t < 0.05f ? N() * (1f - t / 0.05f) * 1.5f : 0f;
        float crackle = rng.NextDouble() < 0.012 * Mathf.Exp(-t * 3f) ? N() : 0f;
        return pop + crackle;
      });
      {
        var lp = new LP(600f);
        clips[SbSound.Place] = Make("place", 0.12f, t => lp.Step(N()) * Mathf.Exp(-t * 40f) * 2f + Mathf.Sin(2f * Mathf.PI * 170f * t) * Mathf.Exp(-t * 50f));
      }
      {
        var lp = new LP(2400f);
        clips[SbSound.Break] = Make("break", 0.25f, t => lp.Step(N()) * Mathf.Exp(-t * 16f) * (rng.NextDouble() < 0.4 ? 1.4f : 0.6f));
      }
      clips[SbSound.Glass] = Make("glass", 0.6f, t =>
      {
        float s = 0f;
        float[] fr = { 2900f, 3700f, 4400f, 5300f, 6100f };
        for (int i = 0; i < fr.Length; i++) s += Mathf.Sin(2f * Mathf.PI * fr[i] * t + i) * Mathf.Exp(-t * (8f + i * 3f)) * (t > i * 0.03f ? 1f : 0f);
        return s + (t < 0.02f ? N() : 0f);
      });
      {
        var lp = new LP(1800f); var lp2 = new LP(300f);
        clips[SbSound.Splash] = Make("splash", 0.8f, t => { float n = N(); return (lp.Step(n) - lp2.Step(n)) * Mathf.Exp(-t * 5f) * 3f; });
      }
      {
        var lp = new LP(5000f); var lp2 = new LP(2000f);
        clips[SbSound.Sizzle] = Make("sizzle", 0.7f, t => { float n = N(); return (lp.Step(n) - lp2.Step(n)) * Mathf.Sin(Mathf.PI * t / 0.7f) * (rng.NextDouble() < 0.3 ? 1.5f : 0.6f); });
      }
      {
        var lp = new LP(6000f); var lp2 = new LP(2500f);
        clips[SbSound.Fuse] = Make("fuse", 1.0f, t => { float n = N(); return (lp.Step(n) - lp2.Step(n)) * (0.8f + 0.2f * Mathf.Sin(t * 60f)) * (rng.NextDouble() < 0.02 ? 2f : 1f); });
      }
      clips[SbSound.Beep] = Make("beep", 0.12f, t => Mathf.Sin(2f * Mathf.PI * 1250f * t) * (t < 0.1f ? 1f : (0.12f - t) / 0.02f));
      clips[SbSound.Siren] = Make("siren", 2.0f, t =>
      {
        float f = ((int)(t * 2f) % 2 == 0) ? 650f : 900f;
        return Mathf.Sin(2f * Mathf.PI * f * t) * 0.6f + Mathf.Sign(Mathf.Sin(2f * Mathf.PI * f * t)) * 0.2f;
      });
      {
        var lp = new LP(900f); var lp2 = new LP(250f);
        clips[SbSound.Swing] = Make("swing", 0.3f, t => { float n = N(); return (lp.Step(n) - lp2.Step(n)) * Mathf.Sin(Mathf.PI * t / 0.3f) * 3f; });
      }
      {
        var lp = new LP(1300f);
        clips[SbSound.Hit] = Make("hit", 0.18f, t => lp.Step(N()) * Mathf.Exp(-t * 30f) * 1.5f + Mathf.Sin(2f * Mathf.PI * 160f * t) * Mathf.Exp(-t * 30f));
      }
      {
        float ph = 0f, ph2 = 0f;
        clips[SbSound.UiOpen] = Make("ui open", 0.55f, t =>
        {
          float k = t / 0.55f;
          ph += 2f * Mathf.PI * Mathf.Lerp(260f, 1300f, k * k) / Rate;
          ph2 += 2f * Mathf.PI * Mathf.Lerp(520f, 2600f, k) / Rate;
          return (Mathf.Sin(ph) * 0.7f + Mathf.Sin(ph2) * 0.25f * k) * Mathf.Sin(Mathf.PI * k) + N() * 0.06f * (1f - k);
        });
      }
      {
        float ph = 0f;
        clips[SbSound.UiClose] = Make("ui close", 0.4f, t =>
        {
          float k = t / 0.4f;
          ph += 2f * Mathf.PI * Mathf.Lerp(1200f, 220f, k) / Rate;
          return Mathf.Sin(ph) * Mathf.Sin(Mathf.PI * k) * 0.8f;
        });
      }
      clips[SbSound.UiHover] = Make("ui hover", 0.03f, t => Mathf.Sin(2f * Mathf.PI * 2400f * t) * Mathf.Exp(-t * 120f));
      clips[SbSound.UiClick] = Make("ui click", 0.08f, t => Mathf.Sin(2f * Mathf.PI * Mathf.Lerp(1500f, 900f, t / 0.08f) * t) * Mathf.Exp(-t * 45f));
      {
        var lp = new LP(900f);
        clips[SbSound.Flame] = Make("flame", 0.25f, t => lp.Step(N()) * Mathf.Sin(Mathf.PI * t / 0.25f) * 2f);
      }
      {
        var lp = new LP(4000f);
        clips[SbSound.Minigun] = Make("minigun", 0.09f, t => lp.Step(N()) * Mathf.Exp(-t * 50f) + Mathf.Sin(2f * Mathf.PI * 180f * t) * Mathf.Exp(-t * 70f) * 0.6f);
      }
      {
        float ph = 0f;
        clips[SbSound.Thunk] = Make("thunk", 0.2f, t =>
        {
          ph += 2f * Mathf.PI * Mathf.Lerp(160f, 60f, t / 0.2f) / Rate;
          return Mathf.Sin(ph) * Mathf.Exp(-t * 18f) + (t < 0.01f ? N() * 0.6f : 0f);
        });
      }
      clips[SbSound.Crackle] = Make("crackle", 1.4f, t => rng.NextDouble() < 0.02 * Mathf.Exp(-t * 1.5f) ? N() : N() * 0.02f);
      {
        var lp = new LP(400f);
        clips[SbSound.Suck] = Make("suck", 2.0f, t => lp.Step(N()) * Mathf.Pow(t / 2f, 2f) * 4f);
      }
    }

    // A Minecraft sound when the player's own Minecraft install has it, else ours.
    internal static void PlayMc(string group, SbSound fallback, Vector3 pos, float volume = 1f, float pitch = 1f, float maxDistance = 40f)
    {
      AudioClip mc = SbMinecraft.Sound(group);
      if (mc != null) PlayClip(mc, pos, volume, pitch, maxDistance);
      else Play(fallback, pos, volume, pitch, maxDistance);
    }

    // the clip itself (for looping sources, e.g. a running thruster)
    internal static AudioClip Clip(SbSound s)
    {
      BuildClips();
      AudioClip clip;
      return clips.TryGetValue(s, out clip) ? clip : null;
    }

    internal static void Play(SbSound s, Vector3 pos, float volume = 1f, float pitch = 1f, float maxDistance = 60f)
    {
      BuildClips();
      AudioClip clip;
      if (!clips.TryGetValue(s, out clip) || clip == null) return;
      PlayClip(clip, pos, volume, pitch, maxDistance);
    }

    internal static void PlayClip(AudioClip clip, Vector3 pos, float volume, float pitch, float maxDistance)
    {
      try
      {
        if (clip == null) return;
        if (pool == null)
        {
          pool = new AudioSource[28];
          for (int i = 0; i < pool.Length; i++)
          {
            var go = new GameObject("SB Audio " + i);
            go.transform.SetParent(Root, false);
            var a = go.AddComponent<AudioSource>();
            a.playOnAwake = false;
            a.spatialBlend = 1f;
            a.rolloffMode = AudioRolloffMode.Linear;
            a.minDistance = 1.5f;
            a.dopplerLevel = 0f;
            pool[i] = a;
          }
        }
        // pick a free source, else the oldest one
        AudioSource src = null;
        for (int i = 0; i < pool.Length; i++)
        {
          AudioSource p = pool[(poolNext + i) % pool.Length];
          if (p != null && !p.isPlaying) { src = p; poolNext = (poolNext + i + 1) % pool.Length; break; }
        }
        if (src == null) { src = pool[poolNext]; poolNext = (poolNext + 1) % pool.Length; }
        if (src == null) return;
        src.transform.position = pos;
        src.maxDistance = maxDistance;
        src.minDistance = maxDistance > 100f ? 20f : 1.5f;
        src.pitch = pitch * (1f + (float)(rng.NextDouble() - 0.5) * 0.06f);
        src.volume = Mathf.Clamp01(volume * Volume);
        src.clip = clip;
        src.Play();
      }
      catch { }
    }

    // ---------------------------------------------------------------- particles
    internal static ParticleSystem Burst(Vector3 pos, int count, Color c0, Color c1, float spMin, float spMax, float szMin, float szMax,
      float lifeMin, float lifeMax, float gravity, bool additive, float radius = 0.05f, Vector3? dir = null, float cone = 25f,
      float drag = 0f, float sizeEnd = 0.2f, bool smoke = false)
    {
      try
      {
        var go = new GameObject("SB Fx");
        go.transform.SetParent(Root, false);
        go.transform.position = pos;
        if (dir.HasValue && dir.Value.sqrMagnitude > 1e-6f) go.transform.rotation = Quaternion.LookRotation(dir.Value);
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ps.main;
        main.duration = 0.2f;
        main.loop = false;
        main.playOnAwake = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(lifeMin, lifeMax);
        main.startSpeed = new ParticleSystem.MinMaxCurve(spMin, spMax);
        main.startSize = new ParticleSystem.MinMaxCurve(szMin, szMax);
        main.startColor = new ParticleSystem.MinMaxGradient(c0, c1);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.gravityModifier = gravity;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = count + 8;
        main.stopAction = ParticleSystemStopAction.Destroy;
        var em = ps.emission;
        em.enabled = true;
        em.rateOverTime = 0f;
        em.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)Mathf.Clamp(count, 1, 3000)) });
        var sh = ps.shape;
        sh.enabled = true;
        if (dir.HasValue) { sh.shapeType = ParticleSystemShapeType.Cone; sh.angle = cone; sh.radius = Mathf.Max(0.001f, radius); }
        else { sh.shapeType = ParticleSystemShapeType.Sphere; sh.radius = Mathf.Max(0.001f, radius); }
        var col = ps.colorOverLifetime;
        col.enabled = true;
        var grad = new Gradient();
        grad.colorKeys = new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) };
        grad.alphaKeys = new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(smoke ? 0.7f : 0.9f, 0.4f), new GradientAlphaKey(0f, 1f) };
        col.color = new ParticleSystem.MinMaxGradient(grad);
        var sol = ps.sizeOverLifetime;
        sol.enabled = true;
        sol.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 1f, 1f, sizeEnd));
        if (drag > 0f)
        {
          var lim = ps.limitVelocityOverLifetime;
          lim.enabled = true;
          lim.drag = drag;
          lim.multiplyDragByParticleSize = false;
          lim.multiplyDragByParticleVelocity = true;
        }
        var r = go.GetComponent<ParticleSystemRenderer>();
        r.sharedMaterial = additive ? SbMats.ParticleAdd : SbMats.ParticleAlpha;
        r.renderMode = ParticleSystemRenderMode.Billboard;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;
        ps.Play();
        return ps;
      }
      catch { return null; }
    }

    internal static void Flash(Vector3 pos, Color c, float intensity, float range, float seconds)
    {
      try
      {
        var go = new GameObject("SB Flash");
        go.transform.SetParent(Root, false);
        go.transform.position = pos;
        var l = go.AddComponent<Light>();
        l.type = LightType.Point;
        l.color = c;
        l.intensity = intensity;
        l.range = range;
        l.shadows = LightShadows.None;
        var f = go.AddComponent<SbFade>();
        f.light = l; f.life = seconds; f.startIntensity = intensity;
      }
      catch { }
    }

    // A flat ring that grows (shockwave) or a sphere shell that grows and fades.
    internal static void Shell(Vector3 pos, Color c, float fromSize, float toSize, float seconds, bool ring, Quaternion? rot = null)
    {
      try
      {
        GameObject go;
        if (ring)
        {
          go = GameObject.CreatePrimitive(PrimitiveType.Quad);
          go.transform.rotation = rot ?? Quaternion.Euler(90f, 0f, 0f);
        }
        else go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        go.name = "SB Shell";
        Collider cc = go.GetComponent<Collider>();
        if (cc != null) UnityEngine.Object.Destroy(cc);
        go.transform.SetParent(Root, true);
        go.transform.position = pos;
        var r = go.GetComponent<Renderer>();
        var m = new Material(SbMats.Line) { hideFlags = HideFlags.DontSave };
        if (ring && m.HasProperty("_MainTex")) m.SetTexture("_MainTex", SbMats.Ring);
        m.color = c;
        r.sharedMaterial = m;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        var f = go.AddComponent<SbFade>();
        f.rend = r; f.mat = m; f.life = seconds; f.fromSize = fromSize; f.toSize = toSize; f.baseColor = c;
        go.transform.localScale = Vector3.one * fromSize;
      }
      catch { }
    }

    // ---------------------------------------------------------------- bigger effects
    internal static void Explosion(Vector3 pos, float radius, SbBlast kind)
    {
      float r = Mathf.Max(0.5f, radius);
      switch (kind)
      {
        case SbBlast.Nuke:
        case SbBlast.MiniNuke:
          Nuke(pos, r, kind == SbBlast.MiniNuke);
          return;
        case SbBlast.BlackHole:
          Burst(pos, 160, new Color(0.7f, 0.3f, 1f), new Color(0.2f, 0.9f, 1f), r * 1.5f, r * 3f, 0.05f, 0.18f, 0.4f, 1.0f, 0f, true, r * 0.2f, null, 0f, 2f);
          Shell(pos, new Color(0.6f, 0.2f, 1f, 0.8f), r * 0.2f, r * 2.4f, 0.7f, false);
          Flash(pos, new Color(0.6f, 0.3f, 1f), 8f, r * 4f, 0.6f);
          Play(SbSound.Boom, pos, 1f, 1.4f, 80f);
          return;
        case SbBlast.Firework:
          {
            Color a = Color.HSVToRGB((float)rng.NextDouble(), 0.8f, 1f), b = Color.HSVToRGB((float)rng.NextDouble(), 0.7f, 1f);
            Burst(pos, 260, a, b, 6f, 9f, 0.08f, 0.16f, 1.2f, 2.0f, 0.35f, true, 0.1f, null, 0f, 1.2f, 0.4f);
            Burst(pos, 80, Color.white, new Color(1f, 0.95f, 0.7f), 2f, 5f, 0.04f, 0.07f, 1.2f, 2.4f, 0.5f, true, 0.1f, null, 0f, 0.6f, 0.1f);
            Flash(pos, a, 6f, 30f, 0.8f);
            Play(SbSound.Boom, pos, 0.8f, 1.6f, 160f);
            Play(SbSound.Crackle, pos, 0.9f, 1f, 160f);
            return;
          }
      }
      // TNT, creepers and fireballs blow up the Minecraft way when the pack has it
      if ((kind == SbBlast.Tnt || kind == SbBlast.Mc) && SbMcFx.Explosion(pos, r))
      {
        Burst(pos, (int)(20 + r * 6), new Color(0.9f, 0.9f, 0.9f, 0.9f), new Color(0.6f, 0.6f, 0.6f, 0.8f), r * 0.4f, r * 1.2f, 0.06f, 0.14f, 0.6f, 1.4f, -0.05f, false, r * 0.3f, null, 0f, 0.4f);
        Flash(pos, new Color(1f, 0.85f, 0.6f), 4f + r, r * 4f, 0.3f);
        if (SbMinecraft.HasSound("explode")) PlayClip(SbMinecraft.Sound("explode"), pos, 1f, UnityEngine.Random.Range(0.85f, 1.05f), 80f);
        else Play(SbSound.Boom, pos, Mathf.Clamp01(0.6f + r * 0.1f), 1f, 80f);
        return;
      }
      // normal blast
      float s = Mathf.Clamp(r / 3f, 0.4f, 3f);
      Burst(pos, (int)(60 * s + 30), new Color(1f, 0.85f, 0.4f), new Color(1f, 0.4f, 0.1f), r * 0.6f, r * 1.6f, 0.4f * s, 1.0f * s, 0.25f, 0.6f, -0.1f, true, r * 0.15f, null, 0f, 3f, 0.4f);
      Burst(pos, (int)(40 * s + 20), new Color(0.35f, 0.33f, 0.31f, 0.85f), new Color(0.15f, 0.14f, 0.13f, 0.9f), r * 0.3f, r * 0.9f, 0.8f * s, 1.6f * s, 1.4f, 2.8f, -0.06f, false, r * 0.2f, null, 0f, 1.8f, 1.6f, true);
      Burst(pos, (int)(50 * s + 20), new Color(1f, 0.9f, 0.5f), new Color(1f, 0.6f, 0.2f), r * 2f, r * 4f, 0.03f, 0.07f, 0.4f, 1.2f, 1f, true, r * 0.1f, null, 0f, 0.5f, 0.2f);
      Shell(pos, new Color(1f, 0.9f, 0.7f, 0.55f), r * 0.3f, r * 2.2f, 0.35f, false);
      Shell(pos + Vector3.up * 0.05f, new Color(1f, 0.95f, 0.85f, 0.7f), r * 0.4f, r * 3.5f, 0.6f, true);
      Flash(pos, new Color(1f, 0.7f, 0.35f), 6f + r, r * 5f, 0.5f);
      if ((kind == SbBlast.Tnt || kind == SbBlast.Mc) && SbMinecraft.HasSound("explode")) PlayClip(SbMinecraft.Sound("explode"), pos, 1f, 1f, 80f);
      else Play(SbSound.Boom, pos, Mathf.Clamp01(0.6f + r * 0.1f), Mathf.Clamp(1.4f - r * 0.06f, 0.75f, 1.3f), 70f + r * 10f);
    }

    private static void Nuke(Vector3 pos, float r, bool mini)
    {
      var go = new GameObject(mini ? "SB Mini Nuke" : "SB Nuke");
      go.transform.SetParent(Root, false);
      go.transform.position = pos;
      var n = go.AddComponent<SbNukeFx>();
      n.radius = r;
      n.mini = mini;
    }

    internal static void Sparks(Vector3 pos, Vector3 normal, Color c, int count = 12)
    {
      Burst(pos, count, c, Color.white, 1.5f, 4f, 0.015f, 0.035f, 0.15f, 0.4f, 1f, true, 0.01f, normal, 50f);
    }

    internal static void Splat(Vector3 pos, Vector3 normal, Color a, Color b, float size)
    {
      Burst(pos, 40, a, b, 1.5f, 4.5f, 0.04f * size, 0.1f * size, 0.4f, 0.9f, 1.4f, false, 0.05f, normal, 70f, 0f, 0.6f);
    }

    internal static void Debris(Vector3 pos, Color c, float size)
    {
      Color d = new Color(c.r * 0.7f, c.g * 0.7f, c.b * 0.7f, 1f);
      Burst(pos, 14, c, d, 0.8f, 2.4f, 0.05f * size, 0.12f * size, 0.5f, 0.9f, 1.4f, false, size * 0.35f, null, 0f, 0f, 0.5f);
    }

    // a block breaking the Minecraft way: a 4x4x4 cloud of little flat squares,
    // each a random quarter of the block's texture, that hop out, fall and blink out
    // (no fading, no spinning)
    internal static void McBreak(Vector3 pos, int tile, float size, Color tint)
    {
      try
      {
        var go = new GameObject("SB Mc Bits");
        go.transform.SetParent(Root, false);
        go.transform.position = pos;
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ps.main;
        main.duration = 0.1f;
        main.loop = false;
        main.playOnAwake = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.25f, 1.0f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.4f * size, 1.6f * size);
        main.startSize = new ParticleSystem.MinMaxCurve(size * 0.08f, size * 0.15f);
        main.startColor = new ParticleSystem.MinMaxGradient(tint, tint * 0.85f);
        main.startRotation = 0f;
        main.gravityModifier = 0.9f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 80;
        main.stopAction = ParticleSystemStopAction.Destroy;
        var em = ps.emission;
        em.enabled = true;
        em.rateOverTime = 0f;
        em.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)64) });
        var sh = ps.shape;
        sh.enabled = true;
        sh.shapeType = ParticleSystemShapeType.Box;
        sh.scale = Vector3.one * size * 0.8f;
        sh.randomDirectionAmount = 1f;
        // every bit is one of 16 crops of the tile
        var tsa = ps.textureSheetAnimation;
        tsa.enabled = true;
        tsa.mode = ParticleSystemAnimationMode.Grid;
        tsa.numTilesX = 4;
        tsa.numTilesY = 4;
        tsa.animation = ParticleSystemAnimationType.WholeSheet;
        tsa.frameOverTime = new ParticleSystem.MinMaxCurve(0f, 0.999f);   // a random crop each, kept for its life
        tsa.cycleCount = 1;
        var r = go.GetComponent<ParticleSystemRenderer>();
        r.sharedMaterial = SbMats.TileParticle(tile);
        r.renderMode = ParticleSystemRenderMode.Billboard;
        r.alignment = ParticleSystemRenderSpace.View;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;
        ps.Play();
      }
      catch { }
    }

    // ---------------------------------------------------------------- decals
    private static readonly Queue<GameObject> decals = new Queue<GameObject>();
    internal static void PaintDecal(Vector3 pos, Vector3 normal, Color c, float size)
    {
      try
      {
        while (decals.Count > 160) { var old = decals.Dequeue(); if (old != null) UnityEngine.Object.Destroy(old); }
        var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
        go.name = "SB Paint";
        var cc = go.GetComponent<Collider>(); if (cc != null) UnityEngine.Object.Destroy(cc);
        go.transform.SetParent(Root, true);
        go.transform.position = pos + normal * 0.006f;
        go.transform.rotation = Quaternion.LookRotation(-normal) * Quaternion.Euler(0f, 0f, (float)rng.NextDouble() * 360f);
        go.transform.localScale = Vector3.one * size * (0.8f + (float)rng.NextDouble() * 0.5f);
        var m = new Material(SbMats.Line) { hideFlags = HideFlags.DontSave };
        if (m.HasProperty("_MainTex")) m.SetTexture("_MainTex", SplatTexture);
        m.color = c;
        m.renderQueue = 3005;
        var r = go.GetComponent<Renderer>();
        r.sharedMaterial = m;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        var f = go.AddComponent<SbFade>();
        f.rend = r; f.mat = m; f.life = 40f; f.fadeOnlyAtEnd = true; f.baseColor = c; f.fromSize = f.toSize = -1f;
        decals.Enqueue(go);
      }
      catch { }
    }

    private static Texture2D splatTex;
    private static Texture2D SplatTexture
    {
      get
      {
        if (splatTex != null) return splatTex;
        int s = 64;
        splatTex = new Texture2D(s, s, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.DontSave, name = "Unknowns SandBox Splat" };
        var p = new Color32[s * s];
        var blobs = new List<Vector3>();
        blobs.Add(new Vector3(0.5f, 0.5f, 0.28f));
        var r = new System.Random(77);
        for (int i = 0; i < 10; i++)
        {
          float a = (float)(r.NextDouble() * Math.PI * 2), d = 0.22f + (float)r.NextDouble() * 0.2f;
          blobs.Add(new Vector3(0.5f + Mathf.Cos(a) * d, 0.5f + Mathf.Sin(a) * d, 0.04f + (float)r.NextDouble() * 0.07f));
        }
        for (int y = 0; y < s; y++)
          for (int x = 0; x < s; x++)
          {
            float u = (x + 0.5f) / s, v = (y + 0.5f) / s, a = 0f;
            foreach (Vector3 b in blobs)
            {
              float d = Vector2.Distance(new Vector2(u, v), new Vector2(b.x, b.y));
              a = Mathf.Max(a, Mathf.Clamp01((b.z - d) / 0.015f));
            }
            p[y * s + x] = new Color(1f, 1f, 1f, a * 0.92f);
          }
        splatTex.SetPixels32(p);
        splatTex.Apply(false, false);
        return splatTex;
      }
    }
  }

  // Fades lights / shells / decals and removes them.
  internal sealed class SbFade : MonoBehaviour
  {
    internal Light light;
    internal Renderer rend;
    internal Material mat;
    internal float life = 1f;
    internal float startIntensity;
    internal float fromSize = -1f, toSize = -1f;
    internal Color baseColor = Color.white;
    internal bool fadeOnlyAtEnd;
    private float t;

    private void Update()
    {
      t += Time.deltaTime;
      float k = Mathf.Clamp01(t / Mathf.Max(0.01f, life));
      if (light != null) light.intensity = startIntensity * (1f - k) * (1f - k);
      if (fromSize >= 0f && toSize >= 0f)
      {
        float e = 1f - (1f - k) * (1f - k) * (1f - k);
        transform.localScale = Vector3.one * Mathf.Lerp(fromSize, toSize, e);
      }
      if (mat != null)
      {
        float a = fadeOnlyAtEnd ? Mathf.Clamp01((1f - k) / 0.15f) : (1f - k);
        Color c = baseColor; c.a = baseColor.a * a;
        mat.color = c;
      }
      if (t >= life)
      {
        if (mat != null) Destroy(mat);
        Destroy(gameObject);
      }
    }
  }

  // The nuke: a blinding flash (it whites out your view if you are
  // close), a white-hot fireball that swells and rises, a condensation dome,
  // two ground shockwaves, a base surge of dust rolling outward, a stem, and a
  // rolling mushroom cap (a vortex ring: smoke rolls up the outside and down the
  // middle) lit orange from inside and cooling to grey. Embers rain down, ash
  // falls at the end. The bang reaches you at the speed of sound.
  internal sealed class SbNukeFx : MonoBehaviour
  {
    internal float radius = 28f;
    internal bool mini;
    private float t;
    private Vector3 basePos;
    private ParticleSystem fire, cap, stem, skirt, ash;
    private Light light;
    private GameObject core;
    private Material coreMat;
    private GameObject flashQuad;
    private Material flashMat;
    private float flashStrength;
    private float boomAt = -1f;
    private float total;

    private void Start()
    {
      basePos = transform.position;
      float r = radius;
      total = mini ? 13f : 24f;
      Camera cam = Camera.main;
      float dist = cam != null ? Vector3.Distance(cam.transform.position, basePos) : 0f;
      boomAt = Mathf.Min(dist / 343f, 2.5f);
      SbFx.Play(SbSound.Boom, basePos, 1f, mini ? 0.75f : 0.55f, mini ? 300f : 1500f);
      flashStrength = Mathf.Clamp01(1.15f - dist / (r * 14f));

      // white-out over the headset view
      if (cam != null && flashStrength > 0.02f)
      {
        flashQuad = GameObject.CreatePrimitive(PrimitiveType.Quad);
        flashQuad.name = "SB Nuke Flash";
        var c = flashQuad.GetComponent<Collider>(); if (c != null) Destroy(c);
        flashQuad.transform.SetParent(cam.transform, false);
        flashQuad.transform.localPosition = new Vector3(0f, 0f, Mathf.Max(cam.nearClipPlane * 1.5f, 0.06f));
        flashQuad.transform.localScale = Vector3.one * 0.8f;
        flashMat = new Material(SbMats.Line) { hideFlags = HideFlags.DontSave, renderQueue = 4001 };
        flashMat.color = new Color(1f, 1f, 0.95f, flashStrength);
        var fr = flashQuad.GetComponent<Renderer>();
        fr.sharedMaterial = flashMat;
        fr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
      }

      // fireball core
      core = GameObject.CreatePrimitive(PrimitiveType.Sphere);
      core.name = "SB Nuke Core";
      var cc = core.GetComponent<Collider>(); if (cc != null) Destroy(cc);
      core.transform.SetParent(transform, false);
      coreMat = new Material(SbMats.Line) { hideFlags = HideFlags.DontSave, renderQueue = 3020 };
      var cr = core.GetComponent<Renderer>();
      cr.sharedMaterial = coreMat;
      cr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

      // condensation dome + shockwaves + first blast of embers
      SbFx.Shell(basePos, new Color(1f, 1f, 1f, 0.55f), r * 0.4f, r * 3.4f, mini ? 0.7f : 1.1f, false);
      SbFx.Shell(basePos + Vector3.up * 0.15f, new Color(1f, 0.95f, 0.85f, 0.9f), r * 0.3f, r * (mini ? 5f : 8f), mini ? 2f : 3.4f, true);
      SbFx.Shell(basePos + Vector3.up * 0.4f, new Color(0.85f, 0.7f, 0.55f, 0.6f), r * 0.2f, r * (mini ? 3.5f : 5.5f), mini ? 3f : 5f, true);
      SbFx.Burst(basePos, mini ? 200 : 450, new Color(1f, 0.95f, 0.6f), new Color(1f, 0.45f, 0.08f), r * 1.2f, r * 3.2f, 0.06f, 0.2f, 1.5f, 3.5f, 0.9f, true, r * 0.2f, null, 0f, 0.6f, 0.3f);

      var lg = new GameObject("SB Nuke Light");
      lg.transform.SetParent(transform, false);
      light = lg.AddComponent<Light>();
      light.type = LightType.Point;
      light.color = Color.white;
      light.range = r * 16f;
      light.intensity = 40f;
      light.shadows = LightShadows.None;

      fire = MakeSystem(true, 900, 0.8f, 1.8f);
      cap = MakeSystem(false, 2400, 5f, 9f);
      stem = MakeSystem(false, 1400, 3.5f, 6f);
      skirt = MakeSystem(false, 1400, 3f, 5.5f);
      ash = MakeSystem(false, 900, 4f, 7f);
      var ashMain = ash.main; ashMain.gravityModifier = 0.03f;
      var shake = SandBoxWorld.Haptic;
      if (shake != null && flashStrength > 0.1f) { shake(true, flashStrength, 0.6f); shake(false, flashStrength, 0.6f); }
    }

    private ParticleSystem MakeSystem(bool additive, int max, float lifeMin, float lifeMax)
    {
      var go = new GameObject("SB Nuke Part");
      go.transform.SetParent(transform, false);
      var ps = go.AddComponent<ParticleSystem>();
      ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
      var main = ps.main;
      main.duration = 1f;
      main.loop = true;
      main.startLifetime = new ParticleSystem.MinMaxCurve(lifeMin, lifeMax);
      main.startSpeed = 0f;
      main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
      main.simulationSpace = ParticleSystemSimulationSpace.World;
      main.maxParticles = max;
      var em = ps.emission; em.rateOverTime = 0f;
      var col = ps.colorOverLifetime; col.enabled = true;
      var grad = new Gradient();
      grad.colorKeys = new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(new Color(0.75f, 0.72f, 0.7f), 1f) };
      grad.alphaKeys = new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.08f), new GradientAlphaKey(0.85f, 0.65f), new GradientAlphaKey(0f, 1f) };
      col.color = new ParticleSystem.MinMaxGradient(grad);
      var sol = ps.sizeOverLifetime; sol.enabled = true; sol.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.6f, 1f, 1.6f));
      var rot = ps.rotationOverLifetime; rot.enabled = true; rot.z = new ParticleSystem.MinMaxCurve(-0.4f, 0.4f);
      var r = go.GetComponent<ParticleSystemRenderer>();
      r.sharedMaterial = additive ? SbMats.ParticleAdd : SbMats.ParticleAlpha;
      r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
      r.maxParticleSize = 3f;
      ps.Play();
      return ps;
    }

    private static void Emit(ParticleSystem ps, Vector3 pos, Vector3 vel, float size, Color c, float life = -1f)
    {
      if (ps == null) return;
      var ep = new ParticleSystem.EmitParams();
      ep.position = pos;
      ep.velocity = vel;
      ep.startSize = size;
      ep.startColor = c;
      if (life > 0f) ep.startLifetime = life;
      ps.Emit(ep, 1);
    }

    // hot -> cooling smoke colour for the cap/stem, by time and by how high up
    private Color SmokeColor(float heat)
    {
      Color hot = new Color(1f, 0.62f, 0.25f, 1f), warm = new Color(0.72f, 0.4f, 0.26f, 1f), cold = new Color(0.46f, 0.42f, 0.4f, 1f);
      return heat > 0.5f ? Color.Lerp(warm, hot, (heat - 0.5f) * 2f) : Color.Lerp(cold, warm, heat * 2f);
    }

    private void Update()
    {
      float dt = Time.deltaTime;
      t += dt;
      float r = radius;
      float k = Time.deltaTime * (mini ? 0.6f : 1f);
      float rise = 1f - Mathf.Exp(-t / (mini ? 1.8f : 2.8f));
      float height = r * (mini ? 1.8f : 2.7f) * rise;
      Vector3 top = basePos + Vector3.up * height;
      float heat = Mathf.Clamp01(1f - t / (mini ? 6f : 10f));

      if (boomAt >= 0f && t >= boomAt)
      {
        boomAt = -1f;
        SbFx.Play(SbSound.Nuke, basePos, 1f, mini ? 1.3f : 1f, mini ? 300f : 1500f);
      }

      // flash fades out
      if (flashMat != null)
      {
        float a = flashStrength * Mathf.Clamp01(1f - t / 1.6f);
        flashMat.color = new Color(1f, Mathf.Lerp(0.6f, 1f, a), Mathf.Lerp(0.3f, 0.95f, a), a);
        if (a <= 0f) { Destroy(flashQuad); Destroy(flashMat); flashQuad = null; flashMat = null; }
      }

      // fireball: white-hot ball that swells, rises and goes orange then dark
      if (core != null)
      {
        float grow = 1f - Mathf.Exp(-t * 5f);
        float size = r * (0.25f + 0.65f * grow) * (1f + 0.04f * Mathf.Sin(t * 23f));
        core.transform.position = Vector3.Lerp(basePos + Vector3.up * r * 0.3f, top, Mathf.Clamp01(t / 2f));
        core.transform.localScale = Vector3.one * size * Mathf.Lerp(1f, 0.55f, Mathf.Clamp01(t / 6f));
        Color c = t < 0.4f ? Color.Lerp(Color.white, new Color(1f, 0.95f, 0.6f), t / 0.4f)
                : Color.Lerp(new Color(1f, 0.7f, 0.25f), new Color(0.6f, 0.15f, 0.05f), Mathf.Clamp01((t - 0.4f) / 5f));
        c.a = Mathf.Clamp01(1.3f - t / (mini ? 5f : 8f));
        coreMat.color = c;
        if (c.a <= 0f) { Destroy(core); Destroy(coreMat); core = null; }
      }

      if (light != null)
      {
        light.transform.position = top;
        light.intensity = 40f * Mathf.Exp(-t * 1.6f) + 5f * heat;
        light.color = Color.Lerp(new Color(1f, 0.55f, 0.25f), Color.white, Mathf.Exp(-t * 2f));
      }

      bool emitting = t < total - 6f;
      if (emitting)
      {
        // fireball churn
        if (t < (mini ? 5f : 8f))
          for (int i = 0, n = Rate(110f * heat + 10f); i < n; i++)
          {
            Vector3 d = UnityEngine.Random.onUnitSphere;
            float rr = r * 0.45f * Mathf.Clamp01(t * 3f);
            Emit(fire, top + d * rr, d * r * 0.08f + Vector3.up * r * 0.05f, r * UnityEngine.Random.Range(0.18f, 0.32f),
              Color.Lerp(new Color(1f, 0.45f, 0.1f), new Color(1f, 0.92f, 0.6f), UnityEngine.Random.value * heat));
          }

        // the mushroom cap: a rolling vortex ring around the top
        float ringR = r * (0.35f + 0.45f * rise);
        float tube = r * (0.22f + 0.18f * rise);
        for (int i = 0, n = Rate(170f); i < n; i++)
        {
          float th = UnityEngine.Random.value * Mathf.PI * 2f, ph = UnityEngine.Random.value * Mathf.PI * 2f;
          Vector3 radial = new Vector3(Mathf.Cos(th), 0f, Mathf.Sin(th));
          Vector3 pos = top + radial * (ringR + Mathf.Cos(ph) * tube) + Vector3.up * (Mathf.Sin(ph) * tube * 0.8f);
          Vector3 roll = (-Mathf.Sin(ph) * radial + Mathf.Cos(ph) * Vector3.up) * r * 0.12f;
          Vector3 vel = roll + Vector3.up * r * 0.05f * (1f - rise) + radial * r * 0.015f;
          float lit = Mathf.Clamp01(heat * (0.6f + 0.4f * Mathf.Sin(ph)));
          Color c = SmokeColor(lit);
          c.a = 0.9f;
          Emit(cap, pos, vel, r * UnityEngine.Random.Range(0.28f, 0.48f), c);
        }

        // the stem
        for (int i = 0, n = Rate(80f); i < n; i++)
        {
          float h = UnityEngine.Random.value;
          Vector3 off = UnityEngine.Random.insideUnitCircle * r * (0.12f + 0.1f * (1f - h));
          Vector3 pos = Vector3.Lerp(basePos, top - Vector3.up * tube * 0.6f, h) + new Vector3(off.x, 0f, off.y);
          Color c = SmokeColor(heat * h * 0.8f);
          c.a = 0.85f;
          Emit(stem, pos, Vector3.up * r * 0.12f - new Vector3(off.x, 0f, off.y) * 0.2f, r * UnityEngine.Random.Range(0.18f, 0.3f), c);
        }

        // base surge rolling outward along the ground
        if (t < (mini ? 5f : 9f))
          for (int i = 0, n = Rate(120f * Mathf.Clamp01(1.3f - t / 8f)); i < n; i++)
          {
            float th = UnityEngine.Random.value * Mathf.PI * 2f;
            Vector3 radial = new Vector3(Mathf.Cos(th), 0f, Mathf.Sin(th));
            float rr = r * (0.3f + 2.2f * (1f - Mathf.Exp(-t / 1.5f)));
            Color c = Color.Lerp(new Color(0.55f, 0.48f, 0.4f, 0.85f), new Color(0.9f, 0.6f, 0.35f, 0.9f), heat * 0.5f);
            Emit(skirt, basePos + radial * rr + Vector3.up * r * 0.08f, radial * r * 0.3f + Vector3.up * r * 0.02f, r * UnityEngine.Random.Range(0.25f, 0.4f), c);
          }
      }

      // embers raining down and ash at the end
      if (t > 1.5f && t < total - 3f && UnityEngine.Random.value < Time.deltaTime * 8f)
        SbFx.Burst(top + UnityEngine.Random.insideUnitSphere * r * 0.6f, 12, new Color(1f, 0.7f, 0.3f), new Color(1f, 0.35f, 0.05f), 1f, r * 0.2f, 0.05f, 0.12f, 2f, 3.5f, 0.6f, true, r * 0.2f);
      if (t > (mini ? 4f : 7f))
        for (int i = 0, n = Rate(60f); i < n; i++)
        {
          Vector2 o = UnityEngine.Random.insideUnitCircle * r * 3f;
          Emit(ash, basePos + new Vector3(o.x, height * 0.9f, o.y), new Vector3(UnityEngine.Random.Range(-0.3f, 0.3f), -0.6f, UnityEngine.Random.Range(-0.3f, 0.3f)), UnityEngine.Random.Range(0.05f, 0.12f), new Color(0.3f, 0.3f, 0.3f, 0.8f), 6f);
        }

      if (t > total)
      {
        if (coreMat != null) Destroy(coreMat);
        if (flashMat != null) Destroy(flashMat);
        if (flashQuad != null) Destroy(flashQuad);
        Destroy(gameObject);
      }
    }

    private float carry;
    private int Rate(float perSecond)
    {
      carry += perSecond * Time.deltaTime * (mini ? 0.6f : 1f);
      int n = (int)carry;
      carry -= n;
      return Mathf.Min(n, 40);
    }
  }
}
