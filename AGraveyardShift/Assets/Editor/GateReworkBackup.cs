using System.Collections.Generic;
using UnityEngine;

/// <summary>What the gates looked like before Tools > Graveyard > Gate Rework > Apply Epitaph, so Remove Epitaph can put them back.</summary>
public class GateReworkBackup : ScriptableObject
{
    [System.Serializable]
    public class Leaf
    {
        public string name;
        public Vector3 position;
        public Quaternion localRotation;
        public Vector3 localScale;
        public Mesh mesh;
        public Material[] materials;
        public Mesh colliderMesh;
        public Vector3 boxCenter, boxSize;
        public Vector3 obstacleCenter, obstacleSize;
    }

    public List<Leaf> leaves = new List<Leaf>();
    public Vector3 frontPropPosition;
    public Quaternion frontPropRotation;
    public bool hasFeed;
    public Vector3 frontFeedPosition;
    public List<string> lampNames = new List<string>();
    public List<float> lampShadowNear = new List<float>();
}
