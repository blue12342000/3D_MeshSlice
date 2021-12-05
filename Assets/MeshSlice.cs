using System.Collections;
using System.Collections.Generic;
using UnityEngine;


public class MeshSlice : MonoBehaviour
{
    struct MeshVertex
    {
        Vector3 vertex;
        Vector3 normal;
        Vector2 uv;
    }
    
    Vector3[] m_nearPoint = new Vector3[] { new Vector3(), new Vector3() };
    Vector3[] m_farPoint = new Vector3[] { new Vector3(), new Vector3() };
    Vector3 m_normal;
    bool isDrawGizmo = false;

    // Slice Side Mesh Vertices, Indices, Normals, Uvs
    List<int>[] m_sideIndices = new List<int>[] { new List<int>(), new List<int>() };
    List<Vector3>[] m_sideVertices = new List<Vector3>[] { new List<Vector3>(), new List<Vector3>() };
    List<Vector3>[] m_sideNormals = new List<Vector3>[] { new List<Vector3>(), new List<Vector3>() };
    List<Vector2>[] m_sideUvs = new List<Vector2>[] { new List<Vector2>(), new List<Vector2>() };

    // New Vertices in Plane
    // Originl Vertices -> 2 Mesh vertices
    Dictionary<int, int>[] m_meshVertexIndex = new Dictionary<int, int>[] { new Dictionary<int, int>(), new Dictionary<int, int>() };

    // 임시 나중에 Collider나 Trigger로 해당되는 Mesh들을 검출
    public MeshFilter meshFilter;
    public Material cutMaterial;
    public Material meshMat;

    Vector3 MousePointToWorldPoint(Vector3 point, Camera cam, float z)
    {
        point.z = z;
        return cam.ScreenToWorldPoint(point);
    }

    void ResetSideData()
    {
        m_sideIndices[0].Clear();
        m_sideVertices[0].Clear();
        m_sideNormals[0].Clear();
        m_sideUvs[0].Clear();
        m_meshVertexIndex[0].Clear();

        m_sideIndices[1].Clear();
        m_sideVertices[1].Clear();
        m_sideNormals[1].Clear();
        m_sideUvs[1].Clear();
        m_meshVertexIndex[1].Clear();
    }

    Vector3 GetVertexInPlane(Plane plane, Vector3 startPoint, Vector3 endPoint)
    {
        plane.Raycast(new Ray(startPoint, endPoint), out float ratio);
        return Vector3.Lerp(startPoint, endPoint, ratio);
    }

    void AddNewMeshVertex(bool sideFlag, int index)
    {
        int side = sideFlag ? 0 : 1;

        if (m_meshVertexIndex[side].ContainsKey(index))
        {
            m_sideIndices[side].Add(m_meshVertexIndex[side][index]);
        }
        else
        {
            m_sideIndices[side].Add(m_sideVertices[side].Count);
            m_sideVertices[side].Add(meshFilter.mesh.vertices[index]);
            m_sideUvs[side].Add(meshFilter.mesh.uv[index]);
        }
    }

    void SliceMesh()
    {
        ResetSideData();

        int[] indices = meshFilter.mesh.triangles;
        Vector3[] vertices = meshFilter.mesh.vertices;

        Vector3 v0, v1, v2;
        Plane slice = new Plane(meshFilter.transform.InverseTransformDirection(m_normal), meshFilter.transform.InverseTransformPoint(m_nearPoint[0]));
        bool[] sides = new bool[3];

        List<Vector3>[] sideVertices = new List<Vector3>[] { new List<Vector3>(), new List<Vector3>() };
        Dictionary<int, Dictionary<int, MeshVertex>> newVertex = new Dictionary<int, Dictionary<int, MeshVertex>>();

        for (int i = 0; i < indices.Length; i += 3)
        {
            sideVertices[0].Clear();
            sideVertices[1].Clear();

            v0 = vertices[indices[i]];
            v1 = vertices[indices[i + 1]];
            v2 = vertices[indices[i + 2]];

            sides[0] = slice.GetSide(v0);
            sides[1] = slice.GetSide(v1);
            sides[2] = slice.GetSide(v2);

            if (sides[0]) { sideVertices[0].Add(v0); }
            else { sideVertices[1].Add(v0); }
            if (sides[1]) { sideVertices[0].Add(v1); }
            else { sideVertices[1].Add(v1); }
            if (sides[2]) { sideVertices[0].Add(v2); }
            else { sideVertices[1].Add(v2); }

            if (sides[0] == sides[1] && sides[0] == sides[2])
            {
                // Same Side
                AddNewMeshVertex(sides[0], indices[i]);
                AddNewMeshVertex(sides[0], indices[i+1]);
                AddNewMeshVertex(sides[0], indices[i+2]);
            }
            else
            {
                // Slice 2 Sides
                // a side 1 vertex, other side 2 vertcies
                // 1. Make Vertex Info
                // 2. 
                

                //GetVertexInPlane(slice, sideVertices[0][0], sideVertices[1][0]);
                //GetVertexInPlane(slice, sideVertices[0][sideVertices[0].Count - 1], sideVertices[1][sideVertices[1].Count - 1]);
            }
        }

        Mesh[] newMeshes = new Mesh[] { new Mesh(), new Mesh() };
        newMeshes[0].name = "Slice Side Main Mesh";
        newMeshes[0].vertices = m_sideVertices[0].ToArray();
        newMeshes[0].SetIndices(m_sideIndices[0].ToArray(), MeshTopology.Triangles, 0);
        newMeshes[0].normals = m_sideNormals[0].ToArray();
        newMeshes[0].uv = m_sideUvs[0].ToArray();

        newMeshes[1].vertices = m_sideVertices[1].ToArray();
        newMeshes[1].name = "Slice Side Other Mesh";
        newMeshes[1].SetIndices(m_sideIndices[1].ToArray(), MeshTopology.Triangles, 0);
        newMeshes[1].normals = m_sideNormals[1].ToArray();
        newMeshes[1].uv = m_sideUvs[1].ToArray();

        meshFilter.mesh = newMeshes[0];

        GameObject otherObject = new GameObject("OtherSide", typeof(MeshFilter), typeof(MeshRenderer));
        otherObject.transform.position = meshFilter.transform.position;
        otherObject.transform.rotation = meshFilter.transform.rotation;
        otherObject.GetComponent<MeshFilter>().mesh = newMeshes[1];
        otherObject.GetComponent<MeshRenderer>().material = meshMat;
    }

    void Update()
    {
        if (Input.GetMouseButtonDown(0))
        {
            isDrawGizmo = false;
            m_nearPoint[0] = MousePointToWorldPoint(Input.mousePosition, Camera.main, Camera.main.nearClipPlane);
            m_farPoint[0] = MousePointToWorldPoint(Input.mousePosition, Camera.main, Camera.main.farClipPlane);
        }
        else if (Input.GetMouseButtonUp(0))
        {
            m_nearPoint[1] = MousePointToWorldPoint(Input.mousePosition, Camera.main, Camera.main.nearClipPlane);
            m_farPoint[1] = MousePointToWorldPoint(Input.mousePosition, Camera.main, Camera.main.farClipPlane);
            m_normal = Vector3.Cross(m_nearPoint[1] - m_nearPoint[0], m_farPoint[0] - m_nearPoint[0]).normalized;
            isDrawGizmo = true;

            SliceMesh();

            Debug.Log($"{m_nearPoint} -> {m_farPoint}");
            Debug.Log($"normal : {m_normal}");
        }
    }

    private void OnDrawGizmos()
    {
        if (isDrawGizmo)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawLine(m_nearPoint[0], m_nearPoint[1]);
            Gizmos.DrawLine(m_farPoint[0], m_farPoint[1]);
            Gizmos.DrawLine(m_nearPoint[0], m_farPoint[0]);
            Gizmos.DrawLine(m_nearPoint[1], m_farPoint[1]);
            Gizmos.DrawLine(Vector3.zero, m_normal * 3);
        }
    }
}
