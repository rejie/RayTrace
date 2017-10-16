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
    private Color RGBAZero;

	// Use this for initialization
	void Start () {
        screenWidth = Screen.width;
        screenHeight = Screen.height;
        screenTex = new Texture2D(Screen.width, Screen.height, TextureFormat.RGB24, false);
        colors = screenTex.GetRawTextureData();
        cameraToTrace = GetComponent<Camera>();
        lights = FindObjectsOfType<Light>();
        RGBAZero = new Color(0, 0, 0, 0);

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
                Color color = TraceRay(ray, 0);

                colors[index] = (byte)(Mathf.Clamp01(color.r) * 255);
                colors[index + 1] = (byte)(Mathf.Clamp01(color.g) * 255);
                colors[index + 2] = (byte)(Mathf.Clamp01(color.b) * 255);
                index += 3;
            }
        }

        screenTex.LoadRawTextureData(colors);
        screenTex.Apply();
    }

    private Color TraceRay(Ray ray, int depth)
    {
        Color color = RGBAZero;

        RaycastHit hit;

        if (depth < 5 && Physics.Raycast(ray, out hit, Mathf.Infinity, collisionMask))
        {
            MaterialDef materialDef = hit.transform.GetComponent<MaterialDef>();
            MeshCollider collider = hit.collider as MeshCollider;
            Mesh mesh = collider.sharedMesh;
            int[] triangles = mesh.triangles;
            Vector3[] normals = mesh.normals;
            Vector3 barycentry = hit.barycentricCoordinate;
            Vector3 interpolateNormal = normals[triangles[hit.triangleIndex * 3 + 0]] * barycentry.x +
                                        normals[triangles[hit.triangleIndex * 3 + 1]] * barycentry.y +
                                        normals[triangles[hit.triangleIndex * 3 + 2]] * barycentry.z;
            interpolateNormal.Normalize();
            interpolateNormal = hit.transform.TransformDirection(interpolateNormal);

            switch (materialDef.materialType)
            {
                case MaterialType.REFLECTION_AND_REFRACTION:
                    {
                        Vector3 inDir = ray.direction;
                        Vector3 refractionDir = refract(inDir, interpolateNormal, materialDef.Ior);
                        Vector3 reflectionDir = Vector3.Reflect(inDir, interpolateNormal);
                        Vector3 reflectionOrigin = (Vector3.Dot(reflectionDir, interpolateNormal) < 0) ? (hit.point - interpolateNormal * 0.00001f) : (hit.point + interpolateNormal * 0.00001f);
                        Vector3 refractionOrigin = (Vector3.Dot(refractionDir, interpolateNormal) < 0) ? (hit.point - interpolateNormal * 0.00001f) : (hit.point + interpolateNormal * 0.00001f);

                        Color refractionColor = TraceRay(new Ray(refractionOrigin, refractionDir), depth + 1);
                        Color reflectionColor = TraceRay(new Ray(reflectionOrigin, reflectionDir), depth + 1);
                        float kr;
                        fresnel(inDir, interpolateNormal, materialDef.Ior, out kr);
                        color = reflectionColor * kr + refractionColor * (1 - kr);

                    }
                    break;
                case MaterialType.REFLECTION:
                    {
                        Color diffuseColor = RGBAZero;
                        Material mat = hit.transform.GetComponent<Renderer>().material;
                        if (mat.mainTexture != null)
                        {
                            Vector2 texCoord = hit.textureCoord;
                            diffuseColor += (mat.mainTexture as Texture2D).GetPixelBilinear(texCoord.x, texCoord.y);
                        }
                        else
                        {
                            diffuseColor += mat.color;
                        }

                        Vector3 inDir = ray.direction;
                        float kr;
                        fresnel(inDir, interpolateNormal, materialDef.Ior, out kr);
                        Vector3 reflectionDir = Vector3.Reflect(inDir, interpolateNormal);
                        Vector3 reflectionOrigin = (Vector3.Dot(reflectionDir, interpolateNormal) < 0) ? (hit.point + interpolateNormal * 0.00001f) : (hit.point - interpolateNormal * 0.00001f);
                        Ray reflectionRay = new Ray(reflectionOrigin, reflectionDir);
                        color = TraceRay(reflectionRay, depth + 1) * kr + TraceLight(hit.point + interpolateNormal * 0.00001f, interpolateNormal, diffuseColor, materialDef) * (1 - kr);
                    }
                    break;
                case MaterialType.DIFFUSE_AND_GLOSSY:
                    {
                        Color diffuseColor = RGBAZero;
                        Material mat = hit.transform.GetComponent<Renderer>().material;
                        if (mat.mainTexture != null)
                        {
                            Vector2 texCoord = hit.textureCoord;
                            diffuseColor += (mat.mainTexture as Texture2D).GetPixelBilinear(texCoord.x, texCoord.y);
                        }
                        else
                        {
                            diffuseColor += mat.color;
                        }

                        color = TraceLight(hit.point + interpolateNormal * 0.00001f, interpolateNormal, diffuseColor, materialDef);
                    }
                    break;
                default:
                    break;
            }
        }

        return color;
    }

    private Color TraceLight(Vector3 pos, Vector3 normal, Color diffuseColor, MaterialDef materialDef)
    {
        Color color = RGBAZero;

        foreach (Light light in lights)
        {
            if(light.enabled)
            {
                Color colorToAdd = LightTrace(light, pos, normal, diffuseColor, materialDef);
                colorToAdd.a = 1;
                
                color += colorToAdd;
                
            }
        }

        return color;
    }

    private Color LightTrace(Light light, Vector3 pos, Vector3 normal, Color diffuseColor, MaterialDef materialDef)
    {
        float diffuse;

        if (light.type == LightType.Directional)
        {
            diffuse = Vector3.Dot(-light.transform.forward, normal);
            if (diffuse > 0)
            {
                if (Physics.Raycast(pos, -light.transform.forward, Mathf.Infinity, collisionMask))
                {
                    return RGBAZero;
                }

                Vector3 viewVector = (cameraToTrace.transform.position - pos).normalized;
                Vector3 halfVector = (-light.transform.forward + viewVector).normalized;
                float dot = Vector3.Dot(halfVector, normal);
                float spec = 0;
                if(dot > 0)
                {
                    spec = Mathf.Pow(dot, 128);
                }


                return light.color * light.intensity * spec * materialDef.Ks + diffuseColor * diffuse * materialDef.Kd;
            }
        }
        else
        {
            float distance = Vector3.Distance(pos, light.transform.position);
            Vector3 direction = (light.transform.position - pos).normalized;
            diffuse = Vector3.Dot(normal, direction);

            if(distance < light.range && diffuse > 0)
            {
                if(light.type == LightType.Point)
                {
                    if(Physics.Raycast(pos, direction, distance, collisionMask))
                    {
                        return RGBAZero;
                    }

                    Vector3 viewVector = (cameraToTrace.transform.position - pos).normalized;
                    Vector3 halfVector = (direction + viewVector).normalized;
                    float dot = Vector3.Dot(halfVector, normal);
                    float spec = 0;
                    if (dot > 0)
                    {
                        spec = Mathf.Pow(dot, 128);
                    }

                    float r = distance / light.range;
                    float Attenuation = 1.0f + 25.0f * r * r;
                    return (light.color * spec * materialDef.Ks + diffuseColor * diffuse * materialDef.Kd) * light.intensity / Attenuation;
                }
                else if(light.type == LightType.Spot)
                {
                    float spotFactor = Vector3.Dot(-light.transform.forward, direction);
                    float spotCulloff = Mathf.Cos((light.spotAngle / 2.0f) * Mathf.Deg2Rad);
                    if (spotFactor > spotCulloff)
                    {
                        if (Physics.Raycast(pos, direction, distance, collisionMask))
                        {
                            return RGBAZero;
                        }

                        Vector3 viewVector = (cameraToTrace.transform.position - pos).normalized;
                        Vector3 halfVector = (direction + viewVector).normalized;
                        float dot = Vector3.Dot(halfVector, normal);
                        float spec = 0;
                        if (dot > 0)
                        {
                            spec = Mathf.Pow(dot, 128);
                        }


                        return (light.color * spec * materialDef.Ks + diffuseColor * diffuse * materialDef.Kd) * light.intensity * (1.0f - distance / light.range) * (1.0f - (1.0f - spotFactor) / (1.0f - spotCulloff));
                    }
                }
                else if(light.type == LightType.Area)
                {
                    Color color = RGBAZero;

                    Vector3 lightPos = light.transform.position;
                    Vector3 right = light.transform.right;
                    Vector3 pNormal = light.transform.forward;
                    Vector3 up = light.transform.up;

                    Vector2 lightArea = light.areaSize;

                    Vector3 projection = projectionOnPlane(pos, lightPos, pNormal);
                    Vector3 dir = projection - lightPos;

                    Vector2 diagonal = new Vector2(Vector3.Dot(dir, right), Vector3.Dot(dir, up));
                    Vector2 nearest2D = new Vector2(Mathf.Clamp(diagonal.x, -lightArea.x, lightArea.x), Mathf.Clamp(diagonal.y, -lightArea.y, lightArea.y));
                    Vector3 nearestPointInside = lightPos + right * nearest2D.x + up * nearest2D.y;

                    float dist = Vector3.Distance(pos, nearestPointInside);
                    Vector3 L = (nearestPointInside - pos).normalized;
                    float r = Vector3.Distance(pos, nearestPointInside) / light.range;
                    float Attenuation = 1.0f + 25.0f * r * r;

                    float nDotL = Vector3.Dot(pNormal, -L);

                    if(nDotL > 0 && sideOfPlane(pos, lightPos, pNormal) == 1)
                    {
                        Vector3 viewVector = (cameraToTrace.transform.position - pos).normalized;
                        Vector3 R = Vector3.Reflect(-viewVector, normal);
                        Vector3 E = linePlaneIntersect(pos, R, lightPos, pNormal);

                        float specAngle = Vector3.Dot(-R, pNormal);
                        if(specAngle > 0.0f)
                        {
                            Vector3 dirSpec = E - lightPos;
                            Vector2 dirSpec2D = new Vector2(Vector3.Dot(dirSpec, right), Vector3.Dot(dirSpec, up));
                            Vector2 nearestSpec2D = new Vector2(Mathf.Clamp(dirSpec2D.x, -lightArea.x, lightArea.x), Mathf.Clamp(dirSpec2D.y, -lightArea.y, lightArea.y));
                            float specFactor = 1 - Mathf.Clamp(Vector2.Distance(nearestSpec2D, dirSpec2D) * 128, 0, 1);

                            color += light.color * light.intensity * specFactor * specAngle / Attenuation;
                        }

                        color += light.intensity * diffuseColor * nDotL / Attenuation;
                    }

                    return color;
                }
            }
        }

        return RGBAZero;
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

    /*
     ================================================================================
     utility functions
     ================================================================================
     */

    private void fresnel(Vector3 inDir, Vector3 normal, float ior, out float kr)
    {
        float cosi = Mathf.Clamp(Vector3.Dot(inDir, normal), -1, 1);
        float etai = 1, etat = ior;
        if(cosi > 0)
        {
            float temp = etai;
            etai = etat;
            etat = temp;
        }
        float sint = etai / etat * Mathf.Sqrt(Mathf.Max(0.0f, 1 - cosi * cosi));

        if(sint >= 1)
        {
            kr = 1;
        }
        else
        {
            float cost = Mathf.Sqrt(Mathf.Max(0.0f, 1 - sint * sint));
            cosi = Mathf.Abs(cosi);
            float Rs = ((etat * cosi) - (etai * cost)) / ((etat * cosi) + (etai * cost));
            float Rp = ((etai * cosi) - (etat * cost)) / ((etai * cosi) + (etat * cost));
            kr = (Rs * Rs + Rp * Rp) / 2;
        }

        //kr = 1 - kr;
    }

    private Vector3 refract(Vector3 inDir, Vector3 normal, float ior)
    {
        float cosi = Mathf.Clamp(Vector3.Dot(inDir, normal), -1, 1);
        float etai = 1, etat = ior;
        Vector3 N = normal;
        if (cosi < 0)
        {
            cosi = -cosi;
        }
        else
        {
            float temp = etai;
            etai = etat;
            etat = temp;
            N = -normal;
        }

        float eta = etai / etat;
        float k = 1 - eta * eta * (1 - cosi * cosi);
        
        if(k < 0)
        {
            return Vector3.zero;
        }
        else
        {
            return (eta * inDir + (eta * cosi - Mathf.Sqrt(k)) * N).normalized;
        }

    }

    private Vector3 projectionOnPlane(Vector3 p, Vector3 planeCenter, Vector3 planeNormal)
    {
        float distance = Vector3.Dot(planeNormal, p - planeCenter);
        return p - distance * planeNormal;
    }

    private int sideOfPlane(Vector3 p, Vector3 planeCenter, Vector3 planeNormal)
    {
        if(Vector3.Dot(p - planeCenter, planeNormal) >= 0.0f)
        {
            return 1;
        }
        else
        {
            return 0;
        }
    }

    private Vector3 linePlaneIntersect(Vector3 p, Vector3 dir, Vector3 planeCenter, Vector3 planeNormal)
    {
        return p + dir * (Vector3.Dot(planeNormal, planeCenter - p) / Vector3.Dot(planeNormal, dir));
    }
}
