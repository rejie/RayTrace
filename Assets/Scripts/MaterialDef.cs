using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum MaterialType
{
    DIFFUSE_AND_GLOSSY, REFLECTION_AND_REFRACTION, REFLECTION
}

public class MaterialDef : MonoBehaviour {

    public float Kd = 1.0f;
    public float Ks = 1.0f;
    public MaterialType materialType = MaterialType.DIFFUSE_AND_GLOSSY;
    public float Ior = 1.5f;
}
