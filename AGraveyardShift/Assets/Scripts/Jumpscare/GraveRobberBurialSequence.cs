using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The Grave Robber's kill (ending II): "Face to Face", then "Six Feet Under". His hands shoot out and he
/// hauls you in by the head, right up to his face, lit from below by your own torch. He cocks his head and
/// looks at you; twice, for a split second, the photo from the gate camera is there instead. Then the
/// headbutt. You come to on your back in an open grave with him at the edge with a shovel: four shovelfuls
/// land on the lens, each darker and more muffled, and the last one is black.
/// </summary>
[CreateAssetMenu(menuName = "Graveyard/Kill Sequences/Grave Robber Burial", fileName = "GraveRobberBurial")]
public class GraveRobberBurialSequence : KillSequence
{
    [Header("Animation")]
    [Tooltip("Stumble: arms reaching")]
    public AnimationClip reach;
    [Tooltip("KillerAttack: the headbutt lunge is at about 1.3 s")]
    public AnimationClip attack;
    [Tooltip("KillerKill: both fists up (lifting the shovel)")]
    public AnimationClip armsUp;

    [Header("Face to Face")]
    [Tooltip("Where he is (m) when the kill starts. Keep it equal to the event's killStopDistance.")]
    public float startDistance = 1.6f;
    [Tooltip("He has you by the head.")]
    public float grab = 0.12f;
    [Tooltip("His face's distance from the camera while he holds you (m).")]
    public float holdDistance = 0.74f;
    [Tooltip("The gate camera's photo, flashed over his face.")]
    public Texture2D flashImage;
    [Tooltip("When the photo flashes (s). Every second one is a negative.")]
    public float[] flashTimes = { 0.40f, 0.71f };
    [Tooltip("How long each flash lasts (s).")]
    public float flashLength = 0.066f;
    [Tooltip("The headbutt starts.")]
    public float headbutt = 1.05f;
    [Tooltip("The headbutt lands: one white frame, then black.")]
    public float hit = 1.15f;

    [Header("Six Feet Under")]
    [Tooltip("You come to in the grave.")]
    public float wake = 1.94f;
    [Tooltip("When each shovelful starts (s).")]
    public float[] shovelfuls = { 2.44f, 3.29f, 4.14f, 4.99f };
    [Tooltip("How much of the lens is covered after each shovelful (0..1).")]
    public float[] cover = { 0.3f, 0.6f, 0.87f, 1f };
    [Tooltip("From a shovelful starting to its dirt hitting the lens (s).")]
    public float landAfter = 0.8f;
    [Tooltip("Black.")]
    public float end = 5.89f;
    [Tooltip("Black before the ending screen.")]
    public float blackHold = 0.3f;
    [Tooltip("An empty spot far off the map where the grave is built.")]
    public Vector3 gravePosition = new Vector3(900f, 40f, 500f);
    public Material graveDirt;
    [Tooltip("The dull red sky over the hole (the night sky straight overhead renders black).")]
    public Material graveSky;
    [Tooltip("Dirt landing on the lens: rgb = soil, alpha = the order it lands in.")]
    public Texture2D lensDirt;
    public Mesh shovelMesh;
    public Material shovelMaterial;
    public float shovelScale = 30f;

    private const float PitDepth = 1.9f, PitWidth = 1.25f, PitLength = 2.3f;

    private class Clod
    {
        public Transform transform;
        public Vector3 start, velocity;
        public float thrownAt;
    }

    private class State
    {
        public GameObject grave, shovel;
        public readonly List<Clod> clods = new List<Clod>();
        public int lastThrow = -1;
    }

    public override float Duration => end + blackHold;

    public override void Begin(KillSequenceRig rig)
    {
        rig.UseClips(eyeUp, eyeForward, ("reach", reach), ("attack", attack), ("armsUp", armsUp));
        rig.state = new State();
        rig.fx.dirt = lensDirt;
    }

    public override void Evaluate(KillSequenceRig rig, float t)
    {
        rig.BeginFrame();
        if (t < hit + 0.033f)
        {
            FaceToFace(rig, Mathf.Min(t, hit));
            if (t >= hit)
            {
                rig.fx.flashColor = Color.white;
                rig.fx.flash = 0.55f;
            }
        }
        else if (t < wake)
        {
            rig.fx.flashColor = Color.black;
            rig.fx.flash = 1f;
        }
        else
        {
            Grave(rig, (State)rig.state, t);
        }
    }

    private void FaceToFace(KillSequenceRig rig, float t)
    {
        float yank = Out3(Seg(t, 0.02f, grab + 0.03f));
        Vector3 cam = rig.Eye() + rig.forward * (0.8f * yank) + Vector3.up * (0.22f * yank)
                      + rig.forward * Kick(t, grab + 0.03f, 0.06f, 4f, 9f);
        float butt = In3(Seg(t, headbutt, hit));
        rig.Visible(true);
        rig.Place(rig.Ground(rig.At(Mathf.Lerp(startDistance, 1.5f, yank) - 0.3f * butt)), -rig.forward);
        float reaching = Seg(t, 0f, grab);
        rig.Pose(("reach", 0.2f + 0.45f * reaching, 1f - butt), ("attack", 1.25f + 0.08f * butt, butt));
        rig.LeanTo(cam, 0.85f);
        rig.Stare(cam, 1f);
        rig.PlaceFace(cam, Mathf.Lerp(startDistance - 0.25f, holdDistance, yank) - 0.6f * butt);
        float cock = InOut(Seg(t, 0.15f, 0.7f));
        rig.HeadTurn(cam, -32f * cock + 3f * Noise(t * 9f, 3), 4f * Noise(t * 5f, 4), 0f, 0.5f);

        // held by the head: you shake
        float held = t > grab ? 1f : 0f;
        rig.SetCamera(cam, rig.Face, Mathf.Lerp(rig.baseFov, rig.baseFov - 4f, yank),
            (Noise(t * 7f, 1) * 2.4f + Jitter(t, 30f, 2) * 0.5f) * held,
            (Noise(t * 6f, 3) * 2.8f + Jitter(t, 30f, 4) * 0.5f) * held,
            Noise(t * 4f, 5) * 2f * held);
        rig.Torch(2.8f, cam + Vector3.down * 0.45f + rig.right * 0.1f, rig.Face);
        rig.SetVhs(0.25f, 0.04f, -1f, 0.03f);

        for (int i = 0; i < flashTimes.Length; i++)
        {
            if (flashImage == null || t < flashTimes[i] || t >= flashTimes[i] + flashLength) continue;
            bool negative = i % 2 == 1;
            rig.fx.CoverOverlay(flashImage, rig.camera.aspect, new Vector2(0.5f, 0.6f), negative ? 1.05f : 0.85f);
            rig.fx.overlayAlpha = 1f;
            rig.fx.overlayInvert = negative;
            rig.SetVhs(1f, 0.04f, 0.02f, 0.03f);
        }
    }

    private void Grave(KillSequenceRig rig, State s, float t)
    {
        if (s.grave == null) BuildGrave(rig, s);
        Vector3 floor = gravePosition;
        Vector3 cam = floor + new Vector3(0f, 0.3f, -0.8f);

        // He stands at the head of the grave, toes at the edge, and shovels from the pile on his right: scoop, lift it
        // over the grave, turn the blade and tip it in. The shovel moves and his hands follow it (left at the grip,
        // right low on the shaft), so it is always in his hands.
        rig.Visible(true);
        rig.Place(floor + new Vector3(0f, PitDepth, PitLength / 2f + 0.35f), Vector3.back);
        int k = 0;
        for (int i = 0; i < shovelfuls.Length; i++) if (t >= shovelfuls[i] - 0.25f) k = i;
        float phase = Seg(t, shovelfuls[k] - 0.25f, shovelfuls[k] + 0.55f);
        float lift = InOut(Seg(phase, 0.25f, 0.55f)) * (1f - InOut(Seg(phase, 0.88f, 1f)));
        float tip = InOut(Seg(phase, 0.55f, 0.85f)) * (1f - Seg(phase, 0.9f, 1f));
        rig.Pose(("reach", 0.45f + 0.2f * lift, 1f));
        Vector3 r = rig.killer.right, f = rig.killer.forward, u = Vector3.up, feet = rig.killer.position;
        Vector3 handle = feet + Vector3.Lerp(r * -0.25f + u * 1.95f + f * 0.3f, r * -0.45f + u * 1.85f - f * 0.05f, lift);
        Vector3 down = Vector3.Slerp((r - u * 1.3f + f * 0.3f).normalized, (r * 0.35f - u * 0.2f + f * 0.92f).normalized, lift);
        float bladeTip = shovelMesh != null ? -shovelMesh.bounds.min.y : 0.0328f;   // mesh units below its origin
        float length = (bladeTip + (shovelMesh != null ? shovelMesh.bounds.max.y : 0.04f)) * shovelScale;
        Vector3 blade = handle + down * length;
        Vector3 leftGrip = handle + down * 0.15f * length / 2.18f, rightGrip = handle + down * 1.05f * length / 2.18f;
        rig.LeanTo(rightGrip, 0.6f * (1f - lift));
        rig.Stare(cam, 0.8f);
        Vector3 leftShoulder = rig.animator.GetBoneTransform(HumanBodyBones.LeftUpperArm).position;
        Vector3 rightShoulder = rig.animator.GetBoneTransform(HumanBodyBones.RightUpperArm).position;
        rig.Reach(HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftHand, leftGrip, 1f, leftShoulder - r * 0.7f - u * 0.5f + f * 0.2f);
        rig.Reach(HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand, rightGrip, 1f, rightShoulder + r * 0.7f - u * 0.5f + f * 0.2f);
        // the blade's face up while it scoops, turned over to tip the dirt in
        Vector3 faceUp = (u - down * Vector3.Dot(u, down)).normalized;
        Vector3 face = Quaternion.AngleAxis(-115f * tip, down) * faceUp;
        if (s.shovel != null) s.shovel.transform.SetPositionAndRotation(blade - down * (bladeTip * shovelScale), Quaternion.LookRotation(face, -down));

        // The dirt leaves the blade as it tips and lands on the lens landAfter seconds after the shovelful starts.
        for (int j = s.lastThrow + 1; j < shovelfuls.Length; j++)
        {
            float release = shovelfuls[j] + landAfter - 0.36f;
            if (t < release) break;
            ThrowClods(rig, s, j, blade - down * 0.2f, cam, release);
            s.lastThrow = j;
        }
        foreach (Clod c in s.clods)
        {
            float flight = t - c.thrownAt;
            bool inAir = flight >= 0f && flight < 0.45f;
            c.transform.gameObject.SetActive(inAir);
            if (inAir) c.transform.position = c.start + c.velocity * flight + 0.5f * Physics.gravity * flight * flight;
        }

        float pitch = 0f;
        for (int i = 0; i < shovelfuls.Length; i++) pitch += Kick(t, shovelfuls[i] + landAfter, 1.5f, 6f, 12f);
        Vector3 drift = new Vector3(Noise(t * 0.4f, 1) * 0.02f, Noise(t * 0.5f, 2) * 0.015f, 0f);
        rig.SetCamera(cam + drift, Vector3.Lerp(cam + Vector3.up * 3f + Vector3.forward * 2.2f, rig.Face, 0.55f),
            rig.baseFov - 4f, pitch, 0f, 3f + Noise(t * 0.3f, 3) * 2f);
        rig.Torch(0f, cam, cam + Vector3.up);
        rig.SetVhs(0.18f, 0.04f, -1f, 0.05f);

        // Coming to: eyes open, blurred, then the dirt piles up on the lens.
        float awake = Seg(t, wake, wake + 0.6f);
        rig.fx.lids = 1f - InOut(awake);
        rig.fx.blur = Mathf.Lerp(0.007f, 0.001f, awake);
        rig.fx.darken = Mathf.Lerp(0.5f, 0.1f, awake);
        rig.fx.desaturate = 0.2f;
        rig.fx.dirtTint = new Color(0.55f, 0.5f, 0.45f);
        float covered = 0f;
        for (int i = 0; i < shovelfuls.Length && i < cover.Length; i++)
        {
            float land = shovelfuls[i] + landAfter;
            covered = Mathf.Max(covered, cover[i] * Out3(Seg(t, land - 0.02f, land + 0.05f)));
        }
        rig.fx.dirtAmount = covered;
        if (t >= end)
        {
            rig.fx.flashColor = Color.black;
            rig.fx.flash = 1f;
        }
    }

    private void ThrowClods(KillSequenceRig rig, State s, int shovelful, Vector3 blade, Vector3 cam, float release)
    {
        for (int i = 0; i < 14; i++)
        {
            GameObject g = rig.Keep(GameObject.CreatePrimitive(i % 3 == 0 ? PrimitiveType.Cube : PrimitiveType.Sphere));
            g.name = "GraveClod";
            RemoveCollider(g);
            if (graveDirt != null) g.GetComponent<Renderer>().sharedMaterial = graveDirt;
            g.transform.localScale = Vector3.one * (0.05f + Hash(i + shovelful * 31) * 0.12f);
            g.transform.rotation = Quaternion.Euler(Hash(i * 3) * 360f, Hash(i * 5) * 360f, 0f);
            Vector3 target = cam + new Vector3((Hash(i * 7 + shovelful) - 0.5f) * 0.9f, 0f, (Hash(i * 11 + shovelful) - 0.5f) * 0.7f);
            float flight = 0.36f + Hash(i * 13 + shovelful) * 0.06f;
            Vector3 start = blade + new Vector3((Hash(i * 17) - 0.5f) * 0.25f, 0f, (Hash(i * 19) - 0.5f) * 0.2f);
            Vector3 velocity = (target - start - 0.5f * Physics.gravity * flight * flight) / flight;
            g.SetActive(false);
            s.clods.Add(new Clod { transform = g.transform, start = start, velocity = velocity, thrownAt = release });
        }
    }

    private void BuildGrave(KillSequenceRig rig, State s)
    {
        s.grave = rig.Keep(new GameObject("KillSequenceGrave"));
        Transform root = s.grave.transform;
        const float wall = 3f, yard = 9f;
        Box(root, new Vector3(0f, -0.25f, 0f), new Vector3(PitWidth + 2f * wall, 0.5f, PitLength + 2f * wall));
        Box(root, new Vector3(-(PitWidth / 2f + wall / 2f), PitDepth / 2f, 0f), new Vector3(wall, PitDepth, PitLength + 2f * wall));
        Box(root, new Vector3(PitWidth / 2f + wall / 2f, PitDepth / 2f, 0f), new Vector3(wall, PitDepth, PitLength + 2f * wall));
        Box(root, new Vector3(0f, PitDepth / 2f, -(PitLength / 2f + wall / 2f)), new Vector3(PitWidth, PitDepth, wall));
        Box(root, new Vector3(0f, PitDepth / 2f, PitLength / 2f + wall / 2f), new Vector3(PitWidth, PitDepth, wall));
        Box(root, new Vector3(0f, PitDepth - 0.05f, PitLength / 2f + yard / 2f + 0.01f), new Vector3(yard, 0.1f, yard));

        // a mound of spoil along one side, against the sky
        GameObject mound = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        RemoveCollider(mound);
        mound.transform.SetParent(root, false);
        mound.transform.position = gravePosition + new Vector3(-1.6f, PitDepth, 0.2f);
        mound.transform.localScale = new Vector3(1.6f, 1.1f, 2.8f);
        if (graveDirt != null) mound.GetComponent<Renderer>().sharedMaterial = graveDirt;

        if (graveSky != null)
        {
            GameObject sky = GameObject.CreatePrimitive(PrimitiveType.Quad);
            RemoveCollider(sky);
            sky.transform.SetParent(root, false);
            sky.transform.position = gravePosition + new Vector3(0f, 18f, 3f);
            sky.transform.rotation = Quaternion.Euler(-90f, 0f, 0f);
            sky.transform.localScale = new Vector3(60f, 60f, 1f);
            sky.GetComponent<Renderer>().sharedMaterial = graveSky;
        }

        // cold moonlight from above, a red lantern glow behind him
        var moon = new GameObject("GraveMoon", typeof(Light));
        moon.transform.SetParent(root, false);
        moon.transform.rotation = Quaternion.Euler(68f, 200f, 0f);
        Light moonLight = moon.GetComponent<Light>();
        moonLight.type = LightType.Directional;
        moonLight.intensity = 0.8f;
        moonLight.color = new Color(0.55f, 0.62f, 0.85f);
        moonLight.shadows = LightShadows.Soft;
        var glow = new GameObject("GraveGlow", typeof(Light));
        glow.transform.SetParent(root, false);
        glow.transform.position = gravePosition + new Vector3(1.2f, PitDepth + 1.4f, PitLength / 2f + 2.2f);
        Light glowLight = glow.GetComponent<Light>();
        glowLight.type = LightType.Point;
        glowLight.range = 9f;
        glowLight.intensity = 4f;
        glowLight.color = new Color(0.9f, 0.25f, 0.18f);

        if (shovelMesh != null)
        {
            s.shovel = rig.Keep(new GameObject("GraveShovel", typeof(MeshFilter), typeof(MeshRenderer)));
            s.shovel.GetComponent<MeshFilter>().sharedMesh = shovelMesh;
            s.shovel.GetComponent<MeshRenderer>().sharedMaterial = shovelMaterial;
            s.shovel.transform.localScale = Vector3.one * shovelScale;
        }
    }

    private void Box(Transform root, Vector3 center, Vector3 size)
    {
        GameObject g = GameObject.CreatePrimitive(PrimitiveType.Cube);
        RemoveCollider(g);
        g.transform.SetParent(root, false);
        g.transform.position = gravePosition + center;
        g.transform.localScale = size;
        if (graveDirt != null) g.GetComponent<Renderer>().sharedMaterial = graveDirt;
    }

    private static void RemoveCollider(GameObject g)
    {
        Collider c = g.GetComponent<Collider>();
        if (c == null) return;
        if (Application.isPlaying) Destroy(c);
        else DestroyImmediate(c);
    }
}
