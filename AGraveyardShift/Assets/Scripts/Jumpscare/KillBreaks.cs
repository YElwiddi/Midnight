using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Extra joints for a killer, for the length of one kill. Each long bone (upper arm, forearm, thigh, shin) gets
/// a second joint part way along it: a copy of the mesh is cut there (a new ring of vertices), the far half is
/// skinned to a new "break" bone, and bending that bone swings the rest of the limb with it. Nothing is scaled,
/// so nothing skews: a limb is lengthened by moving its joints apart, and the skin between the rings stretches
/// after them. It can also give the lower half of a painted face a jaw bone, so a mouth can open.
/// The killer's mesh must have Read/Write enabled in its import settings, or a build cannot cut it.
/// </summary>
public class KillBreaks
{
    public class Joint
    {
        public Transform seg, brk, child;
        public Vector3 restBrk, restChild;
    }

    public static readonly HumanBodyBones[] AllLimbs =
    {
        HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm, HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm,
        HumanBodyBones.LeftUpperLeg, HumanBodyBones.LeftLowerLeg, HumanBodyBones.RightUpperLeg, HumanBodyBones.RightLowerLeg,
    };

    /// <summary>The jaw bone (null unless one was asked for) and where it rests, in the head's space.</summary>
    public Transform jaw;
    public Vector3 jawRest;
    /// <summary>What was built, for the log.</summary>
    public string summary = "";

    private readonly Dictionary<HumanBodyBones, Joint> joints = new Dictionary<HumanBodyBones, Joint>();
    private SkinnedMeshRenderer smr;
    private Mesh original, mesh;
    private Transform[] originalBones;
    private List<Transform> bones;
    private List<Matrix4x4> binds;
    private List<Vector3> V, N;
    private List<Vector4> T;
    private List<Vector2> UV;
    private List<BoneWeight> W;
    private List<int> tris;

    private static HumanBodyBones Next(HumanBodyBones b)
    {
        switch (b)
        {
            case HumanBodyBones.LeftUpperArm: return HumanBodyBones.LeftLowerArm;
            case HumanBodyBones.LeftLowerArm: return HumanBodyBones.LeftHand;
            case HumanBodyBones.RightUpperArm: return HumanBodyBones.RightLowerArm;
            case HumanBodyBones.RightLowerArm: return HumanBodyBones.RightHand;
            case HumanBodyBones.LeftUpperLeg: return HumanBodyBones.LeftLowerLeg;
            case HumanBodyBones.LeftLowerLeg: return HumanBodyBones.LeftFoot;
            case HumanBodyBones.RightUpperLeg: return HumanBodyBones.RightLowerLeg;
            default: return HumanBodyBones.RightFoot;
        }
    }

    /// <summary>
    /// Cuts each of <paramref name="segs"/> at <paramref name="at"/> (0..1 along the bone) and swaps the cut mesh onto
    /// the killer. Returns null, leaving the killer as it was, when its mesh cannot be read.
    /// </summary>
    public static KillBreaks Build(KillSequenceRig rig, float at, bool withJaw, params HumanBodyBones[] segs)
    {
        SkinnedMeshRenderer renderer = rig.killer.GetComponentInChildren<SkinnedMeshRenderer>(true);
        if (renderer == null || renderer.sharedMesh == null) return null;
        Mesh src = renderer.sharedMesh;
        Vector3[] vertices = src.vertices;
        if (vertices == null || vertices.Length == 0 || vertices.Length != src.vertexCount)
        {
            Debug.LogWarning("KillBreaks: '" + src.name + "' cannot be read (enable Read/Write on the model); the kill plays without extra joints.");
            return null;
        }
        var b = new KillBreaks();
        b.smr = renderer;
        b.original = src;
        b.originalBones = renderer.bones;
        b.bones = new List<Transform>(b.originalBones);
        b.binds = new List<Matrix4x4>(src.bindposes);
        b.V = new List<Vector3>(vertices);
        b.N = new List<Vector3>(src.normals);
        b.T = new List<Vector4>(src.tangents);
        b.UV = new List<Vector2>(src.uv);
        b.W = new List<BoneWeight>(src.boneWeights);
        b.tris = new List<int>(src.triangles);
        if (b.N.Count != b.V.Count || b.UV.Count != b.V.Count || b.W.Count != b.V.Count) return null;
        foreach (HumanBodyBones s in segs) b.Add(rig, s, at);
        if (withJaw) b.AddJaw(rig);
        b.mesh = new Mesh { name = src.name + " (extra joints)" };
        b.mesh.SetVertices(b.V);
        b.mesh.SetNormals(b.N);
        if (b.T.Count == b.V.Count) b.mesh.SetTangents(b.T);
        b.mesh.SetUVs(0, b.UV);
        b.mesh.boneWeights = b.W.ToArray();
        b.mesh.SetTriangles(b.tris, 0);
        b.mesh.bindposes = b.binds.ToArray();
        b.mesh.RecalculateBounds();
        Bounds keep = renderer.localBounds;
        renderer.sharedMesh = b.mesh;
        renderer.bones = b.bones.ToArray();
        renderer.localBounds = keep;
        b.summary = src.vertexCount + " -> " + b.V.Count + " verts, " + b.joints.Count + " extra joints" + (b.jaw != null ? ", a jaw" : "");
        return b;
    }

    private static void Acc(Dictionary<int, float> d, int bone, float w)
    {
        if (w <= 0f) return;
        float c;
        d.TryGetValue(bone, out c);
        d[bone] = c + w;
    }

    private static BoneWeight Pack(Dictionary<int, float> d)
    {
        var top = d.OrderByDescending(kv => kv.Value).Take(4).ToList();
        float sum = top.Sum(kv => kv.Value);
        var o = new BoneWeight();
        if (sum <= 0f) return o;
        if (top.Count > 0) { o.boneIndex0 = top[0].Key; o.weight0 = top[0].Value / sum; }
        if (top.Count > 1) { o.boneIndex1 = top[1].Key; o.weight1 = top[1].Value / sum; }
        if (top.Count > 2) { o.boneIndex2 = top[2].Key; o.weight2 = top[2].Value / sum; }
        if (top.Count > 3) { o.boneIndex3 = top[3].Key; o.weight3 = top[3].Value / sum; }
        return o;
    }

    private static BoneWeight Blend(BoneWeight a, BoneWeight b, float s)
    {
        var d = new Dictionary<int, float>();
        Acc(d, a.boneIndex0, a.weight0 * (1 - s)); Acc(d, a.boneIndex1, a.weight1 * (1 - s)); Acc(d, a.boneIndex2, a.weight2 * (1 - s)); Acc(d, a.boneIndex3, a.weight3 * (1 - s));
        Acc(d, b.boneIndex0, b.weight0 * s); Acc(d, b.boneIndex1, b.weight1 * s); Acc(d, b.boneIndex2, b.weight2 * s); Acc(d, b.boneIndex3, b.weight3 * s);
        return Pack(d);
    }

    // Moves a share of the weight on one bone to another.
    private static BoneWeight Move(BoneWeight w, int from, int to, float share)
    {
        var d = new Dictionary<int, float>();
        int[] bi = { w.boneIndex0, w.boneIndex1, w.boneIndex2, w.boneIndex3 };
        float[] bw = { w.weight0, w.weight1, w.weight2, w.weight3 };
        for (int i = 0; i < 4; i++)
        {
            if (bi[i] == from) { Acc(d, to, bw[i] * share); Acc(d, from, bw[i] * (1f - share)); }
            else Acc(d, bi[i], bw[i]);
        }
        return Pack(d);
    }

    private static float On(BoneWeight w, int bone)
    {
        return (w.boneIndex0 == bone ? w.weight0 : 0f) + (w.boneIndex1 == bone ? w.weight1 : 0f) + (w.boneIndex2 == bone ? w.weight2 : 0f) + (w.boneIndex3 == bone ? w.weight3 : 0f);
    }

    private static float OnAny(BoneWeight w, HashSet<int> set)
    {
        return (set.Contains(w.boneIndex0) ? w.weight0 : 0f) + (set.Contains(w.boneIndex1) ? w.weight1 : 0f) + (set.Contains(w.boneIndex2) ? w.weight2 : 0f) + (set.Contains(w.boneIndex3) ? w.weight3 : 0f);
    }

    private void Add(KillSequenceRig rig, HumanBodyBones hs, float at)
    {
        Transform seg = rig.Bone(hs), child = rig.Bone(Next(hs));
        if (seg == null || child == null) return;
        int si = bones.IndexOf(seg);
        if (si < 0) return;
        var limb = new HashSet<int>();
        foreach (Transform tr in seg.GetComponentsInChildren<Transform>(true))
        {
            int bi = bones.IndexOf(tr);
            if (bi >= 0) limb.Add(bi);
        }
        Vector3 cl = child.localPosition;
        float L = cl.magnitude;
        if (L < 1e-5f) return;
        Vector3 a = cl / L;
        Matrix4x4 bp = binds[si];
        const float eps = 0.03f;
        // how far along the bone a vertex sits (0 at its joint, 1 at the next)
        Func<int, float> U = v => Vector3.Dot(bp.MultiplyPoint3x4(V[v]), a) / L;
        var edge = new Dictionary<long, int>();
        Func<int, int, int> mid = (i, j) =>
        {
            long key = i < j ? ((long)i * 1000003L + j) : ((long)j * 1000003L + i);
            int m;
            if (edge.TryGetValue(key, out m)) return m;
            float ui = U(i), uj = U(j);
            float s = (at - ui) / (uj - ui);
            V.Add(Vector3.Lerp(V[i], V[j], s));
            N.Add(Vector3.Lerp(N[i], N[j], s).normalized);
            if (T.Count > 0) T.Add(Vector4.Lerp(T[i], T[j], s));
            UV.Add(Vector2.Lerp(UV[i], UV[j], s));
            W.Add(Blend(W[i], W[j], s));
            m = V.Count - 1;
            edge[key] = m;
            return m;
        };
        int n = tris.Count;
        for (int k = 0; k < n; k += 3)
        {
            int[] ix = { tris[k], tris[k + 1], tris[k + 2] };
            if (OnAny(W[ix[0]], limb) < 0.5f || OnAny(W[ix[1]], limb) < 0.5f || OnAny(W[ix[2]], limb) < 0.5f) continue;
            int[] sg = new int[3];
            int pos = 0, neg = 0;
            for (int q = 0; q < 3; q++)
            {
                float du = U(ix[q]) - at;
                sg[q] = du > eps ? 1 : (du < -eps ? -1 : 0);
                if (sg[q] > 0) pos++; else if (sg[q] < 0) neg++;
            }
            if (pos == 0 || neg == 0) continue;
            if (pos + neg == 2)
            {
                // one corner lies on the cut: split the opposite edge
                int z = Array.IndexOf(sg, 0);
                int p = ix[(z + 1) % 3], q2 = ix[(z + 2) % 3];
                int m = mid(p, q2);
                tris[k] = ix[z]; tris[k + 1] = p; tris[k + 2] = m;
                tris.Add(ix[z]); tris.Add(m); tris.Add(q2);
            }
            else
            {
                // one corner on its own side: a triangle there and a quad on the other
                int lone = pos == 1 ? Array.IndexOf(sg, 1) : Array.IndexOf(sg, -1);
                int i0 = ix[lone], i1 = ix[(lone + 1) % 3], i2 = ix[(lone + 2) % 3];
                int m1 = mid(i0, i1), m2 = mid(i0, i2);
                tris[k] = i0; tris[k + 1] = m1; tris[k + 2] = m2;
                tris.Add(m1); tris.Add(i1); tris.Add(i2);
                tris.Add(m1); tris.Add(i2); tris.Add(m2);
            }
        }
        Transform B = new GameObject(seg.name + " (break)").transform;
        B.SetParent(seg, false);
        B.localPosition = a * L * at;
        B.localRotation = Quaternion.identity;
        B.localScale = Vector3.one;
        int biNew = bones.Count;
        bones.Add(B);
        binds.Add(Matrix4x4.Translate(-(a * L * at)) * bp);
        for (int v = 0; v < V.Count; v++)
        {
            if (On(W[v], si) <= 0f) continue;
            float du = U(v) - at;
            if (du > eps) W[v] = Move(W[v], si, biNew, 1f);
            else if (du >= -eps && OnAny(W[v], limb) >= 0.5f) W[v] = Move(W[v], si, biNew, 0.5f);
        }
        joints[hs] = new Joint { seg = seg, brk = B, child = child, restBrk = B.localPosition, restChild = child.localPosition };
    }

    // The lower half of her face (lip, chin, the corners of the jaw) onto a bone of its own, so her mouth can open.
    private void AddJaw(KillSequenceRig rig)
    {
        Transform head = rig.Bone(HumanBodyBones.Head);
        int hi = head != null ? bones.IndexOf(head) : -1;
        if (hi < 0) return;
        Matrix4x4 bp = binds[hi];
        Transform J = new GameObject("Jaw (added)").transform;
        J.SetParent(head, false);
        J.localPosition = new Vector3(0f, 0.05f, 0.1f);
        J.localRotation = Quaternion.identity;
        J.localScale = Vector3.one;
        int ji = bones.Count;
        bones.Add(J);
        binds.Add(Matrix4x4.Translate(-J.localPosition) * bp);
        for (int v = 0; v < V.Count; v++)
        {
            if (On(W[v], hi) < 0.5f) continue;
            Vector3 p = bp.MultiplyPoint3x4(V[v]);
            if (p.y < 0.045f && p.z > 0.1f && Mathf.Abs(p.x) < 0.19f) W[v] = Move(W[v], hi, ji, 1f);
        }
        jaw = J;
        jawRest = J.localPosition;
    }

    public Joint Get(HumanBodyBones seg)
    {
        Joint j;
        return joints.TryGetValue(seg, out j) ? j : null;
    }

    /// <summary>Every break straight again and every segment its own length. Call before posing each frame.</summary>
    public void Reset()
    {
        foreach (Joint j in joints.Values)
        {
            j.brk.localRotation = Quaternion.identity;
            j.brk.localPosition = j.restBrk;
            j.child.localPosition = j.restChild;
        }
        if (jaw != null)
        {
            jaw.localPosition = jawRest;
            jaw.localRotation = Quaternion.identity;
        }
    }

    /// <summary>Lengthens the near and far halves of a segment (1 = as built). Call before aiming or bending it.</summary>
    public void Lengthen(HumanBodyBones seg, float near, float far)
    {
        Joint j = Get(seg);
        if (j == null) return;
        j.brk.localPosition = j.restBrk * near;
        j.child.localPosition = j.restBrk * near + (j.restChild - j.restBrk) * far;
    }

    private static void Turn(Joint j, Quaternion q)
    {
        Vector3 p = j.brk.position;
        j.brk.rotation = q * j.brk.rotation;
        j.child.position = p + q * (j.child.position - p);
        j.child.rotation = q * j.child.rotation;
    }

    /// <summary>Bends a segment at its break so its far half, and everything beyond it, points along a world direction.</summary>
    public void BendTo(HumanBodyBones seg, Vector3 dir, float amount = 1f)
    {
        Joint j = Get(seg);
        if (j == null || amount <= 0f) return;
        Quaternion q = Quaternion.FromToRotation(j.child.position - j.brk.position, dir);
        Turn(j, Quaternion.Slerp(Quaternion.identity, q, amount));
    }

    /// <summary>Puts the killer's own mesh back and removes the extra bones.</summary>
    public void Dispose()
    {
        foreach (Joint j in joints.Values)
        {
            if (j.child != null) j.child.localPosition = j.restChild;   // limbs their own length again
        }
        if (smr != null)
        {
            Bounds keep = smr.localBounds;
            smr.sharedMesh = original;
            smr.bones = originalBones;
            smr.localBounds = keep;
        }
        foreach (Joint j in joints.Values) if (j.brk != null) Remove(j.brk.gameObject);
        joints.Clear();
        if (jaw != null) Remove(jaw.gameObject);
        jaw = null;
        if (mesh != null) Remove(mesh);
        mesh = null;
    }

    private static void Remove(UnityEngine.Object o)
    {
        if (Application.isPlaying) UnityEngine.Object.Destroy(o);
        else UnityEngine.Object.DestroyImmediate(o);
    }
}
