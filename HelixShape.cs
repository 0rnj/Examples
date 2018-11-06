using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(MeshFilter))]
[RequireComponent(typeof(MeshRenderer))]
public class HelixShape : MonoBehaviour
{
    public Material HelixMaterial;

    /// <summary>
    /// Number of slices in cylinder. Kind of LOD
    /// </summary>
    [Range(20, 30)]
    public int SliceNum = 20;

    /// <summary>
    /// Amount of 'helix' being sliced out
    /// </summary>
    [Range(0f, 1f)]
    public float SlicedPart = 0.1f;

    public float Radius = 5f, InnerRadius = 1f;
    public float Height = 0.1f;

    Mesh mesh;
    public List<Vector3> Vertices = new List<Vector3>();
    public List<int> Triangles = new List<int>();
    int triangleCount = 0;


    private void Awake()
    {
        mesh = GetComponent<MeshFilter>().mesh;
    }

    public void GenerateMesh()
    {
        Vertices.Clear();
        Triangles.Clear();

        var slices = SliceNum;
        var sliceAngle = (360f * (1f - SlicedPart)) / (float)slices;
        float angle, nextAngle;
        Vector3 innerLeftTop, innerRightTop;
        Vector3 outerLeftTop, outerRightTop;
        Vector3 innerLeftBot, innerRightBot;
        Vector3 outerLeftBot, outerRightBot;

        for (int i = 0; i < slices; i++)
        {
            angle = sliceAngle * i * Mathf.Deg2Rad;
            nextAngle = sliceAngle * (i + 1) * Mathf.Deg2Rad;

            /// Creating vertices
            var h = Height / 2;
            innerLeftTop = innerLeftBot = new Vector3(InnerRadius * Mathf.Cos(angle), -h, InnerRadius * Mathf.Sin(angle));
            innerRightTop = innerRightBot = new Vector3(InnerRadius * Mathf.Cos(nextAngle), -h, InnerRadius * Mathf.Sin(nextAngle));
            outerLeftTop = outerLeftBot = new Vector3(Radius * Mathf.Cos(angle), -h, Radius * Mathf.Sin(angle));
            outerRightTop = outerRightBot = new Vector3(Radius * Mathf.Cos(nextAngle), -h, Radius * Mathf.Sin(nextAngle));

            innerLeftTop.y = innerRightTop.y = outerLeftTop.y = outerRightTop.y = h;


            /// Adding vertices
            var v = new Vector3[]
            {
                // Bottom
                innerLeftBot, outerLeftBot, innerRightBot,
                outerLeftBot, outerRightBot, innerRightBot,

                // Top
                innerLeftTop, innerRightTop, outerLeftTop,
                outerLeftTop, innerRightTop, outerRightTop,

                // Inner
                innerLeftBot, innerRightBot, innerLeftTop,
                innerLeftTop, innerRightBot, innerRightTop,

                // Outer
                outerRightBot, outerLeftBot, outerRightTop,
                outerRightTop, outerLeftBot, outerLeftTop
            };
            Vertices.AddRange(v);

            /// Adding triangles
            AddTriangles(v.Length);    // 24 is number of vertices in slice

            /// Side triangles
            if (i == 0)
            {
                /// Left side
                var sv = new Vector3[]
                {
                    outerLeftBot, innerLeftBot, outerLeftTop,
                    outerLeftTop, innerLeftBot, innerLeftTop
                };
                Vertices.AddRange(sv);
                AddTriangles(sv.Length);
            }
            else if (i == slices - 1)
            {
                /// Right side
                var sv = new Vector3[]
                {
                    innerRightBot, outerRightBot, innerRightTop,
                    innerRightTop, outerRightBot, outerRightTop
                };
                Vertices.AddRange(sv);
                AddTriangles(sv.Length);
            }
        }

        /// Applying values to mesh
        mesh.vertices = Vertices.ToArray();
        mesh.triangles = Triangles.ToArray();
        mesh.RecalculateNormals();
        
        triangleCount = 0;
        GetComponent<Renderer>().material = HelixMaterial;
    }

    void AddTriangles(int trianglesNum)
    {
        var tr = new int[trianglesNum];
        for (int j = 0; j < trianglesNum; j++)
        {
            tr[j] = triangleCount;
            triangleCount++;
        }
        Triangles.AddRange(tr);
    }
}