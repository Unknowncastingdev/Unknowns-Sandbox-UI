using UnityEngine;

namespace UnknownsSandbox
{
  // Placed things pop in (grow from nothing with a small overshoot) and deleted
  // things pop out (a little swell, then shrink away) instead of blinking.
  internal sealed class SbPopIn : MonoBehaviour
  {
    private const float Time0 = 0.3f;
    private Vector3 target, last;
    private float t;

    internal static void Play(Transform tr)
    {
      if (tr == null) return;
      SbPopIn p = tr.GetComponent<SbPopIn>();
      if (p != null) Destroy(p);
      p = tr.gameObject.AddComponent<SbPopIn>();
      p.target = tr.localScale;
      p.last = tr.localScale * 0.001f;
      tr.localScale = p.last;
    }

    private static float Ease(float x)
    {
      x = Mathf.Clamp01(x);
      const float c1 = 1.5f, c3 = c1 + 1f;
      return 1f + c3 * Mathf.Pow(x - 1f, 3f) + c1 * Mathf.Pow(x - 1f, 2f);
    }

    private void Update()
    {
      // something else resized it (held, scaled): stop and leave it alone
      if (transform.localScale != last) { Destroy(this); return; }
      t += Time.deltaTime;
      if (t >= Time0) { transform.localScale = target; Destroy(this); return; }
      last = target * Mathf.Max(0.001f, Ease(t / Time0));
      transform.localScale = last;
    }
  }

  internal sealed class SbPopOut : MonoBehaviour
  {
    private const float Time0 = 0.18f;
    private Vector3 start;
    private float t;

    // takes over a removed object's GameObject: no colliders or logic, just the shrink
    internal static void Play(GameObject go)
    {
      if (go == null) return;
      foreach (Collider c in go.GetComponentsInChildren<Collider>(true)) Destroy(c);
      foreach (SbRelay r in go.GetComponentsInChildren<SbRelay>(true)) Destroy(r);
      SbPopIn pin = go.GetComponent<SbPopIn>();
      if (pin != null) Destroy(pin);
      Rigidbody rb = go.GetComponent<Rigidbody>();
      if (rb != null) { rb.isKinematic = true; rb.detectCollisions = false; }
      var p = go.AddComponent<SbPopOut>();
      p.start = go.transform.localScale;
    }

    private void Update()
    {
      t += Time.deltaTime;
      float x = Mathf.Clamp01(t / Time0);
      if (x >= 1f) { Destroy(gameObject); return; }
      // swell 8 % for the first third, then shrink to nothing
      float s = x < 0.3f ? 1f + 0.08f * Mathf.Sin(x / 0.3f * Mathf.PI * 0.5f) : 1.08f * (1f - Mathf.Pow((x - 0.3f) / 0.7f, 2f));
      transform.localScale = start * Mathf.Max(0.001f, s);
    }
  }
}
