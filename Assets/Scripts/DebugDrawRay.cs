using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DebugDrawRay : MonoBehaviour {

	// Use this for initialization
	void Start () {
		
	}
	
	// Update is called once per frame
	void Update () {
        Vector2 pos = Input.mousePosition;
        Ray ray = Camera.main.ScreenPointToRay(pos);

        RaycastHit hit;
        if(Physics.Raycast(ray, out hit, Mathf.Infinity, 1 << 31))
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
            interpolateNormal = hit.transform.TransformDirection(interpolateNormal);

            Vector3 inDir = (hit.point - transform.position).normalized;
            Vector3 reflectionDir = Vector3.Reflect(inDir, interpolateNormal);
            Vector3 reflectionOrigin = (Vector3.Dot(reflectionDir, interpolateNormal) < 0) ? (hit.point + interpolateNormal * 0.0001f) : (hit.point - interpolateNormal * 0.0001f);
            Ray reflectionRay = new Ray(reflectionOrigin, reflectionDir);
            Debug.DrawLine(transform.position, hit.point, Color.red, 0.01f);
            Debug.DrawRay(reflectionRay.origin, reflectionRay.direction, Color.green, 0.01f);
        }
    }
}
