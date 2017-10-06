using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Camera))]
public class RayTracer : MonoBehaviour {

    private Texture2D screenTex;
    private int screenWidth;
    private int screenHeight;
    private byte[] colors;
    private Camera cameraToTrace;
    private Light[] lights;
    private int collisionMask = 1 << 31;

	// Use this for initialization
	void Start () {
        screenWidth = Screen.width;
        screenHeight = Screen.height;
        screenTex = new Texture2D(Screen.width, Screen.height, TextureFormat.RGB24, false);
        colors = screenTex.GetRawTextureData();
        cameraToTrace = GetComponent<Camera>();
        lights = FindObjectsOfType<Light>();

        GenerateColliders();
        RayTrace();
	}
	
	// Update is called once per frame
	void Update () {
        
    }

    void OnRenderImage(RenderTexture source, RenderTexture destination)
    {
        Graphics.Blit(screenTex, destination);
    }

    private void RayTrace()
    {
        int index = 0;
        for (int row = 0; row < screenHeight; ++row)
        {
            for (int col = 0; col < screenWidth; ++col)
            {

                Ray ray = cameraToTrace.ScreenPointToRay(new Vector3(col, row, 0));
                Color color = TraceRay(ray);

                colors[index] = (byte)(Mathf.Clamp01(color.r) * 255);
                colors[index + 1] = (byte)(Mathf.Clamp01(color.g) * 255);
                colors[index + 2] = (byte)(Mathf.Clamp01(color.b) * 255);
                index += 3;
            }
        }

        screenTex.LoadRawTextureData(colors);
        screenTex.Apply();
    }

    private Color TraceRay(Ray ray)
    {
        Color color = Color.black;

        RaycastHit hit;

        if (Physics.Raycast(ray, out hit, Mathf.Infinity, collisionMask))
        {
            MeshCollider collider = hit.collider as MeshCollider;
            Mesh mesh = collider.sharedMesh;
            int[] triangles = mesh.triangles;
            Vector3[] normals = mesh.normals;
            Vector3 barycentry = hit.barycentricCoordinate;
            Vector3 interpolateNormal = normals[triangles[hit.triangleIndex * 3 + 0]] * barycentry.x +
                                        normals[triangles[hit.triangleIndex * 3 + 1]] * barycentry.y +
                                        normals[triangles[hit.triangleIndex * 3 + 2]] * barycentry.z;
            interpolateNormal.Normalize();
            hit.transform.TransformDirection(interpolateNormal);
            

            Material mat = hit.transform.GetComponent<Renderer>().material;
            if (mat.mainTexture != null)
            {
                Vector2 texCoord = hit.textureCoord;
                color += (mat.mainTexture as Texture2D).GetPixelBilinear(texCoord.x, texCoord.y);
            }
            else
            {
                color += mat.color;
            }

            Color ambient = color * 0.5f;
            ambient.a = 1;

            return TraceLight(hit.point + interpolateNormal * 0.0001f, interpolateNormal, color) + ambient;
        }

        return color;
    }

    private Color TraceLight(Vector3 pos, Vector3 normal, Color diffuseColor)
    {
        Color color = Color.black;

        foreach(Light light in lights)
        {
            if(light.enabled)
            {
                Color colorToAdd = LightTrace(light, pos, normal, diffuseColor);
                colorToAdd.a = 1;
                
                color += colorToAdd;
                
            }
        }

        return color;
    }

    private Color LightTrace(Light light, Vector3 pos, Vector3 normal, Color diffuseColor)
    {
        if(light.type == LightType.Directional)
        {
            float diff = Vector3.Dot(-light.transform.forward, normal);

            if (diff > 0)
            {
                if (Physics.Raycast(pos, -light.transform.forward, Mathf.Infinity, collisionMask))
                {
                    return Color.black;
                }

                Vector3 viewVector = (cameraToTrace.transform.position - pos).normalized;
                Vector3 halfVector = (-light.transform.forward + viewVector).normalized;
                float dot = Vector3.Dot(halfVector, normal);
                float spec = 0;
                if(dot > 0)
                {
                    spec = Mathf.Pow(dot, 128);
                }


                return light.color * light.intensity * spec + diffuseColor * diff;
            }
        }

        return Color.black;
    }


    private void GenerateColliders()
    {
        MeshFilter[] meshFilters = FindObjectsOfType<MeshFilter>();
        foreach(MeshFilter meshFilter in meshFilters)
        {
            GameObject go = meshFilter.gameObject;
            if (go.GetComponent<MeshRenderer>())
            {
                Collider collider = go.GetComponent<Collider>();
                if (collider != null)
                {
                    DestroyImmediate(collider);
                }

                go.AddComponent<MeshCollider>().sharedMesh = meshFilter.mesh;
                go.layer = 31;
            }
        }
    }
}
