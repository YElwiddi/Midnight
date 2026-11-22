using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A mouth full of needle teeth for a killer whose mouth is only paint on a flat mask. The lower face gets a jaw
/// bone (KillBreaks), and this PAINTS the maw between the lips into a small texture every frame it is open: at
/// the face texture's own texel density, filtered the way the face is, and drawn with a lit shader on her own
/// layer so it takes the light her face takes. Crisp polygons on a sixty-texel face look pasted on; this is as
/// soft and as coarse as her eye sockets are.
/// Everything is in the head bone's space, sized for the Spirit's head.
/// </summary>
public class PaintedMaw
{
    private const int N = 17, SS = 3, Rows = 8;
    // the window of head space the paint covers (it has to hold the mouth at its largest)
    private const float X0 = -0.52f, X1 = 0.52f, Y0 = -0.64f, Y1 = 0.58f;
    private static readonly Color Lip = new Color(0.25f, 0.15f, 0.16f), VoidEdge = new Color(0.04f, 0.008f, 0.01f), VoidDeep = new Color(0.004f, 0f, 0f);
    private static readonly Color GumOut = new Color(0.3f, 0.11f, 0.12f), GumIn = new Color(0.1f, 0.025f, 0.03f);
    private static readonly Color Root = new Color(0.44f, 0.37f, 0.3f), Tip = new Color(0.93f, 0.9f, 0.82f);

    private readonly Transform head, jaw;
    private readonly Vector3 jawRest;
    private readonly GameObject go;
    private readonly Mesh mesh;
    private readonly Material mat;
    private readonly Texture2D tex;
    private readonly int W, H, SW, SH, count;
    private readonly float dens;
    private readonly float[] sr, sg, sb, sa, grain;
    private readonly Color32[] px;
    private readonly float[] rnd = new float[160];
    private readonly Vector3[] up = new Vector3[N], lo = new Vector3[N];
    private readonly Vector2[] upR = new Vector2[N], loR = new Vector2[N];
    private readonly List<Vector3> mv = new List<Vector3>(), mn = new List<Vector3>();
    private readonly List<Vector2> muv = new List<Vector2>();
    private readonly List<int> mt = new List<int>();
    private Vector3 centreLocal;
    private static readonly int EmissionId = Shader.PropertyToID("_Emission");

    /// <summary>The middle of the opening, world space, wherever she is now (after <see cref="Set"/>).</summary>
    public Vector3 Centre { get { return head.TransformPoint(centreLocal); } }

    /// <param name="shader">Graveyard/Painted Maw (lit, alpha blended).</param>
    /// <param name="density">Texels per head-local unit. The Spirit's painted face measures 115 to 125.</param>
    /// <param name="teeth">Needles in the upper front row.</param>
    public PaintedMaw(KillSequenceRig rig, KillBreaks breaks, Shader shader, FilterMode filter, float density, int teeth, int layer)
    {
        head = rig.Bone(HumanBodyBones.Head);
        jaw = breaks.jaw;
        jawRest = breaks.jawRest;
        dens = Mathf.Clamp(density, 60f, 400f);
        count = Mathf.Clamp(teeth, 4, 14);
        W = Mathf.CeilToInt((X1 - X0) * dens);
        H = Mathf.CeilToInt((Y1 - Y0) * dens);
        SW = W * SS;
        SH = H * SS;
        int n = SW * SH;
        sr = new float[n]; sg = new float[n]; sb = new float[n]; sa = new float[n];
        px = new Color32[W * H];
        grain = new float[W * H];
        for (int i = 0; i < grain.Length; i++) grain[i] = 1f + 0.16f * (Hash(i * 1.37f + 0.11f) - 0.5f);
        for (int i = 0; i < rnd.Length; i++) rnd[i] = Hash(i * 3.17f + 0.4f);
        tex = rig.Keep(new Texture2D(W, H, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = filter, name = "MawPaint" });
        go = rig.Keep(new GameObject("Maw", typeof(MeshFilter), typeof(MeshRenderer)));
        go.transform.SetParent(head, false);
        go.layer = layer;                                    // her own light must find it
        mesh = rig.Keep(new Mesh { name = "Maw" });
        mesh.MarkDynamic();
        go.GetComponent<MeshFilter>().sharedMesh = mesh;
        mat = rig.Keep(new Material(shader) { mainTexture = tex });
        MeshRenderer mr = go.GetComponent<MeshRenderer>();
        mr.sharedMaterial = mat;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = false;
        go.SetActive(false);
    }

    private static float Hash(float x)
    {
        float s = Mathf.Sin(x * 12.9898f + 78.233f) * 43758.5453f;
        return s - Mathf.Floor(s);
    }

    /// <summary>Her mouth shut and as it is painted on her mask.</summary>
    public void Hide()
    {
        if (jaw != null)
        {
            jaw.localPosition = jawRest;
            jaw.localRotation = Quaternion.identity;
        }
        if (go != null) go.SetActive(false);
    }

    // ------------------------------------------------------------------ the brush

    private void Tri(Vector2 a, Vector2 b, Vector2 c, Color ca, Color cb, Color cc)
    {
        float k = dens * SS;
        float ax = (a.x - X0) * k, ay = (a.y - Y0) * k, bx = (b.x - X0) * k, by = (b.y - Y0) * k, cx = (c.x - X0) * k, cy = (c.y - Y0) * k;
        float area = (bx - ax) * (cy - ay) - (by - ay) * (cx - ax);
        if (Mathf.Abs(area) < 1e-5f) return;
        int x0 = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(ax, Mathf.Min(bx, cx)))), x1 = Mathf.Min(SW - 1, Mathf.CeilToInt(Mathf.Max(ax, Mathf.Max(bx, cx))));
        int y0 = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(ay, Mathf.Min(by, cy)))), y1 = Mathf.Min(SH - 1, Mathf.CeilToInt(Mathf.Max(ay, Mathf.Max(by, cy))));
        float inv = 1f / area;
        for (int y = y0; y <= y1; y++)
        {
            float py = y + 0.5f;
            int row = y * SW;
            for (int x = x0; x <= x1; x++)
            {
                float qx = x + 0.5f;
                float w0 = ((bx - qx) * (cy - py) - (by - py) * (cx - qx)) * inv;
                if (w0 < 0f) continue;
                float w1 = ((cx - qx) * (ay - py) - (cy - py) * (ax - qx)) * inv;
                if (w1 < 0f) continue;
                float w2 = 1f - w0 - w1;
                if (w2 < 0f) continue;
                int i = row + x;
                sr[i] = ca.r * w0 + cb.r * w1 + cc.r * w2;
                sg[i] = ca.g * w0 + cb.g * w1 + cc.g * w2;
                sb[i] = ca.b * w0 + cb.b * w1 + cc.b * w2;
                sa[i] = 1f;
            }
        }
    }

    private void Quad(Vector2 a, Vector2 b, Vector2 c, Vector2 d, Color ca, Color cb, Color cc, Color cd)
    {
        Tri(a, b, c, ca, cb, cc);
        Tri(a, c, d, ca, cc, cd);
    }

    private static Vector2 At(Vector3[] curve, float s)
    {
        float fl = Mathf.Clamp01(s) * (N - 1);
        int i = Mathf.Min(N - 2, Mathf.FloorToInt(fl));
        Vector3 p = Vector3.Lerp(curve[i], curve[i + 1], fl - i);
        return new Vector2(p.x, p.y);
    }

    // One needle: two facets meeting on a ridge a little off its middle, dark at the root and pale at the point.
    private void Needle(Vector2 root, Vector2 across, Vector2 along, float w, float len, float lean, float bright, bool back)
    {
        if (len < 0.004f) return;
        root -= along * 0.006f;
        Vector2 tip = root + (along + across * lean).normalized * (len + 0.006f);
        Vector2 l = root - across * w, r = root + across * w, ridge = root + across * w * 0.2f;
        Color cr = Root * bright, ct = Tip * bright;
        if (back) { Tri(l, r, tip, cr * 0.5f, cr * 0.5f, ct * 0.42f); return; }
        Tri(l, ridge, tip, cr, cr, ct);
        Tri(ridge, r, tip, cr * 0.6f, cr * 0.6f, ct * 0.66f);
    }

    private static byte B(float v)
    {
        return (byte)Mathf.Clamp(Mathf.RoundToInt(v * 255f), 0, 255);
    }

    private void Resolve()
    {
        byte lr = B(Lip.r), lg = B(Lip.g), lb = B(Lip.b);
        float full = SS * SS;
        for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
            {
                float r = 0f, g = 0f, b = 0f, a = 0f;
                for (int j = 0; j < SS; j++)
                {
                    int row = (y * SS + j) * SW + x * SS;
                    for (int i = 0; i < SS; i++)
                    {
                        float al = sa[row + i];
                        if (al <= 0f) continue;
                        a += al; r += sr[row + i]; g += sg[row + i]; b += sb[row + i];
                    }
                }
                int idx = y * W + x;
                // texels the paint does not reach still carry her lip colour, so the filtered edge does not go black
                if (a <= 0f) { px[idx] = new Color32(lr, lg, lb, 0); continue; }
                float k = grain[idx] / a;
                px[idx] = new Color32(B(r * k), B(g * k), B(b * k), B(a / full));
            }
        tex.SetPixels32(px);
        tex.Apply(false);
    }

    /// <summary>
    /// Shows her mouth. open 0..1: her jaw drops far past where a jaw stops. grin 0..1: the corners of her mouth are
    /// dragged out and up to her cheekbones. clench 0..1: the two rows locked together (a grin) rather than apart
    /// (a maw). scale: the whole mouth outgrowing her face. emit: how far the teeth light themselves.
    /// </summary>
    public void Set(float open, float grin = 0f, float clench = 0f, float scale = 1f, float emit = 0f)
    {
        open = Mathf.Clamp01(open);
        grin = Mathf.Clamp01(grin);
        jaw.localPosition = jawRest + new Vector3(0f, -0.25f * open, -0.03f * open);
        jaw.localRotation = Quaternion.Euler(12f * open, 0f, 0f);
        bool on = open > 0.02f || grin > 0.02f;
        go.SetActive(on);
        if (!on) return;
        Matrix4x4 J = Matrix4x4.TRS(jaw.localPosition, jaw.localRotation, Vector3.one) * Matrix4x4.Translate(-jawRest);
        float hw = Mathf.Lerp(0.10f, 0.172f, Mathf.Max(open, grin)) + 0.012f * grin;       // the mouth splits toward her ears
        for (int i = 0; i < N; i++)
        {
            float u = i / (float)(N - 1), s = 2f * u - 1f, bulge = 1f - s * s, edge = (1f - bulge) * (1f - bulge);
            float x = s * hw, z = 0.338f + 0.05f * bulge;
            Vector3 top = new Vector3(x, 0.07f + 0.012f * bulge * open + (0.062f * open + 0.115f * grin) * edge, z);
            // the lower lip line dips in the middle, far enough to cover the lower half of the mouth that is painted on her mask
            Vector3 dropped = J.MultiplyPoint3x4(new Vector3(x, 0.03f - 0.045f * bulge * bulge, 0.316f + 0.05f * bulge));
            dropped.z += 0.022f;
            float pin = Mathf.Pow(bulge, 0.45f);             // the corners stay pinned to the upper lip
            up[i] = top;
            lo[i] = Vector3.Lerp(top + new Vector3(0f, -0.004f, 0f), dropped, pin);
        }
        Vector3 C = (up[N / 2] + lo[N / 2]) * 0.5f;
        if (Mathf.Abs(scale - 1f) > 0.001f)
        {
            Vector3 grow = new Vector3(scale, scale, 1f), lift = new Vector3(0f, 0f, 0.07f * (scale - 1f));
            for (int i = 0; i < N; i++)
            {
                up[i] = C + Vector3.Scale(up[i] - C, grow) + lift;
                lo[i] = C + Vector3.Scale(lo[i] - C, grow) + lift;
            }
        }
        centreLocal = C;
        mat.SetFloat(EmissionId, Mathf.Max(0f, emit));

        // ---- paint
        Array.Clear(sa, 0, sa.Length);
        const float rim = 0.009f;
        for (int i = 0; i < N; i++)
        {
            int a = Mathf.Max(0, i - 1), b = Mathf.Min(N - 1, i + 1);
            Vector2 tu = ((Vector2)(up[b] - up[a])).normalized, tl = ((Vector2)(lo[b] - lo[a])).normalized;
            float side = i == 0 ? -1f : (i == N - 1 ? 1f : 0f);
            upR[i] = (Vector2)up[i] + new Vector2(-tu.y, tu.x) * rim + new Vector2(side * rim, 0f);
            loR[i] = (Vector2)lo[i] + new Vector2(tl.y, -tl.x) * rim + new Vector2(side * rim, 0f);
        }
        for (int i = 0; i < N - 1; i++) Quad(upR[i], upR[i + 1], loR[i + 1], loR[i], Lip, Lip, Lip, Lip);            // her lips, where the paint meets her face
        for (int i = 0; i < N - 1; i++)                                                                             // the dark
        {
            Vector2 m0 = ((Vector2)up[i] + (Vector2)lo[i]) * 0.5f, m1 = ((Vector2)up[i + 1] + (Vector2)lo[i + 1]) * 0.5f;
            Quad(up[i], up[i + 1], m1, m0, VoidEdge, VoidEdge, VoidDeep, VoidDeep);
            Quad(m0, m1, lo[i + 1], lo[i], VoidDeep, VoidDeep, VoidEdge, VoidEdge);
        }
        for (int i = 0; i < N - 1; i++)                                                                             // gums: a rind inside each lip
        {
            Vector2 d0 = (Vector2)(lo[i] - up[i]), d1 = (Vector2)(lo[i + 1] - up[i + 1]);
            float g0 = Mathf.Min(0.022f * scale, d0.magnitude * 0.3f), g1 = Mathf.Min(0.022f * scale, d1.magnitude * 0.3f);
            Vector2 n0 = d0.sqrMagnitude > 1e-8f ? d0.normalized : Vector2.down, n1 = d1.sqrMagnitude > 1e-8f ? d1.normalized : Vector2.down;
            Quad(up[i], up[i + 1], (Vector2)up[i + 1] + n1 * g1, (Vector2)up[i] + n0 * g0, GumOut, GumOut, GumIn, GumIn);
            Quad(lo[i], lo[i + 1], (Vector2)lo[i + 1] - n1 * g1, (Vector2)lo[i] - n0 * g0, GumOut, GumOut, GumIn, GumIn);
        }
        float span = 2f * hw * scale * 0.88f / count;
        const float ds = 0.02f;
        if (clench < 0.5f)                                                                                          // a second row behind the first
            for (int k = 0; k <= count; k++)
            {
                float s = 0.06f + 0.88f * (k + 0.1f + 0.3f * rnd[k + 100]) / (count + 0.2f);
                Vector2 a = At(up, s), b = At(lo, s);
                float gap = (b - a).magnitude;
                if (gap < 0.02f) continue;
                Vector2 down = (b - a) / gap, across = (At(up, s + ds) - At(up, s - ds)).normalized;
                Needle(a, across, down, span * 0.26f, gap * (0.14f + 0.12f * rnd[k + 110]), (rnd[k + 120] - 0.5f) * 0.4f, 1f, true);
                Needle(b, (At(lo, s + ds) - At(lo, s - ds)).normalized, -down, span * 0.24f, gap * (0.12f + 0.12f * rnd[k + 130]), (rnd[k + 140] - 0.5f) * 0.4f, 1f, true);
            }
        for (int k = 0; k < count; k++)                                                                             // upper needles hang from the lip
        {
            float s = 0.06f + 0.88f * (k + 0.5f + 0.3f * (rnd[k] - 0.5f)) / count;
            Vector2 a = At(up, s), b = At(lo, s);
            float gap = (b - a).magnitude;
            if (gap < 0.012f) continue;
            Vector2 down = (b - a) / gap, across = (At(up, s + ds) - At(up, s - ds)).normalized;
            bool fang = k == 1 || k == count - 2 || k == count / 2;
            float len = gap * Mathf.Lerp(0.22f + 0.24f * rnd[k + 10] + (fang ? 0.2f : 0f), 0.8f + 0.14f * rnd[k + 10], clench);
            Needle(a, across, down, span * (0.33f + 0.12f * rnd[k + 20] + (fang ? 0.05f : 0f)), len, (rnd[k + 30] - 0.5f) * 0.36f, 0.92f + 0.08f * rnd[k + 40], false);
        }
        for (int k = 0; k < count - 1; k++)                                                                         // lower ones stand up between them
        {
            float s = 0.06f + 0.88f * (k + 1f + 0.3f * (rnd[k + 50] - 0.5f)) / count;
            Vector2 a = At(lo, s), b = At(up, s);
            float gap = (b - a).magnitude;
            if (gap < 0.012f) continue;
            Vector2 upw = (b - a) / gap, across = (At(lo, s + ds) - At(lo, s - ds)).normalized;
            bool fang = k == 2 || k == count - 4;
            float len = gap * Mathf.Lerp(0.2f + 0.22f * rnd[k + 60] + (fang ? 0.18f : 0f), 0.78f + 0.14f * rnd[k + 60], clench);
            // wider than the upper row: thinner ones blur into the dark at this texel size
            Needle(a, across, upw, span * (0.4f + 0.12f * rnd[k + 70] + (fang ? 0.05f : 0f)), len, (rnd[k + 80] - 0.5f) * 0.3f, 0.95f + 0.05f * rnd[k + 90], false);
        }
        Resolve();

        // ---- the sheet the paint lies on: a grid over the opening, a little in front of her lips and her dropped jaw
        const float margin = 0.04f;
        float yTop = float.NegativeInfinity, yBot = float.PositiveInfinity;
        for (int i = 0; i < N; i++) { yTop = Mathf.Max(yTop, up[i].y); yBot = Mathf.Min(yBot, lo[i].y); }
        yTop = Mathf.Min(Y1, yTop + margin);
        yBot = Mathf.Max(Y0, yBot - margin);
        mv.Clear(); muv.Clear(); mn.Clear(); mt.Clear();
        int cols = N + 2;
        for (int ci = 0; ci < cols; ci++)
        {
            int i = Mathf.Clamp(ci - 1, 0, N - 1);
            float x = up[i].x + (ci == 0 ? -margin : (ci == cols - 1 ? margin : 0f));
            x = Mathf.Clamp(x, X0, X1);
            for (int ri = 0; ri <= Rows; ri++)
            {
                float y = Mathf.Lerp(yTop, yBot, ri / (float)Rows);
                float z = y >= up[i].y ? up[i].z : (y <= lo[i].y ? lo[i].z : Mathf.Lerp(up[i].z, lo[i].z, Mathf.InverseLerp(up[i].y, lo[i].y, y)));
                mv.Add(new Vector3(x, y, z));
                muv.Add(new Vector2((x - X0) / (X1 - X0), (y - Y0) / (Y1 - Y0)));
                mn.Add(Vector3.forward);
            }
        }
        for (int ci = 0; ci < cols - 1; ci++)
            for (int ri = 0; ri < Rows; ri++)
            {
                int a = ci * (Rows + 1) + ri, b = a + Rows + 1;
                mt.Add(a); mt.Add(b); mt.Add(b + 1);
                mt.Add(a); mt.Add(b + 1); mt.Add(a + 1);
            }
        mesh.Clear();
        mesh.SetVertices(mv);
        mesh.SetUVs(0, muv);
        mesh.SetNormals(mn);
        mesh.SetTriangles(mt, 0);
        mesh.RecalculateBounds();
    }
}
