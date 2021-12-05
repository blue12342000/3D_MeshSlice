using System.Collections;
using System.Collections.Generic;
using UnityEngine;


public class MeshSlice : MonoBehaviour
{
    struct MeshVertex
    {
        public int index;
        public Vector3 vertex;
        public Vector3 normal;
        public Vector2 uv;

        public MeshVertex(Vector3 _vertex, Vector3 _normal, Vector2 _uv) => (index, vertex, normal, uv) = (-1, _vertex, _normal, _uv);
        public MeshVertex(int _index, Vector3 _vertex, Vector3 _normal, Vector2 _uv) => (index, vertex, normal, uv) = (_index, _vertex, _normal, _uv);
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
    Dictionary<int, int>[] m_sideVertexIndex = new Dictionary<int, int>[] { new Dictionary<int, int>(), new Dictionary<int, int>() };

    // New Vertices in Plane
    // Originl Vertices -> 2 Mesh vertices
    Dictionary<int, Dictionary<int, MeshVertex>> m_newVertexInPlane = new Dictionary<int, Dictionary<int, MeshVertex>>();

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
        m_sideVertexIndex[0].Clear();

        m_sideIndices[1].Clear();
        m_sideVertices[1].Clear();
        m_sideNormals[1].Clear();
        m_sideUvs[1].Clear();
        m_sideVertexIndex[1].Clear();

        m_newVertexInPlane.Clear();
    }

    Vector3 GetVertexInPlane(Plane plane, Vector3 startPoint, Vector3 endPoint)
    {
        plane.Raycast(new Ray(startPoint, endPoint), out float ratio);
        return Vector3.Lerp(startPoint, endPoint, ratio);
    }

    MeshVertex GetMeshVertexInsidePlane(Plane plane, MeshVertex mv1, MeshVertex mv2)
    {
        MeshVertex newVertex;
        if (mv1.index < mv2.index)
        {
            if (m_newVertexInPlane.ContainsKey(mv1.index) && m_newVertexInPlane[mv1.index].ContainsKey(mv2.index))
            {
                return m_newVertexInPlane[mv1.index][mv2.index];
            }
            else
            {
                plane.Raycast(new Ray(mv1.vertex, mv2.vertex), out float ratio);
                newVertex = new MeshVertex(Vector3.Lerp(mv1.vertex, mv2.vertex, ratio)
                                            , Vector3.Lerp(mv1.normal, mv2.normal, ratio)
                                            , Vector2.Lerp(mv1.uv, mv2.uv, ratio));
                if (!m_newVertexInPlane.ContainsKey(mv1.index)) { m_newVertexInPlane.Add(mv1.index, new Dictionary<int, MeshVertex>()); }
                m_newVertexInPlane[mv1.index].Add(mv2.index, newVertex);
            }
        }
        else
        {
            if (m_newVertexInPlane.ContainsKey(mv2.index) && m_newVertexInPlane[mv2.index].ContainsKey(mv1.index))
            {
                return m_newVertexInPlane[mv2.index][mv1.index];
            }
            else
            {
                plane.Raycast(new Ray(mv1.vertex, mv2.vertex), out float ratio);
                newVertex = new MeshVertex(Vector3.Lerp(mv1.vertex, mv2.vertex, ratio)
                                            , Vector3.Lerp(mv1.normal, mv2.normal, ratio)
                                            , Vector2.Lerp(mv1.uv, mv2.uv, ratio));
                if (!m_newVertexInPlane.ContainsKey(mv2.index)) { m_newVertexInPlane.Add(mv2.index, new Dictionary<int, MeshVertex>()); }
                m_newVertexInPlane[mv2.index].Add(mv1.index, newVertex);
            }
        }    

        return newVertex;
    }

    MeshVertex[] MakeNewVertexInPlane(Plane plane, List<MeshVertex>[] sideVertices)
    {
        float ratio;
        MeshVertex[] newVertices = new MeshVertex[2];
        newVertices[0] = GetMeshVertexInsidePlane(plane, sideVertices[0][0], sideVertices[1][0]);

        if (sideVertices[0].Count < sideVertices[1].Count)
        {
            newVertices[1] = GetMeshVertexInsidePlane(plane, sideVertices[0][0], sideVertices[1][1]);
        }
        else
        {
            newVertices[1] = GetMeshVertexInsidePlane(plane, sideVertices[0][1], sideVertices[1][0]);
        }
        return newVertices;
    }

    void AddSliceLineTriangle(Vector3 faceNormal, List<MeshVertex>[] sideVertices, MeshVertex[] newMeshVertices)
    {
        for (int i = 0; i < 2; ++i)
        {
            for (int h = 0; h < sideVertices[i].Count; ++h)
            {
                if (Vector3.Dot(faceNormal, Vector3.Cross(newMeshVertices[1].vertex - newMeshVertices[0].vertex, sideVertices[i][h].vertex - newMeshVertices[1].vertex)) < 0)
                {
                    // Incorrect Direction
                    m_sideIndices[i].Add(m_sideVertices[i].Count);
                    m_sideVertices[i].Add(newMeshVertices[1].vertex);
                    m_sideNormals[i].Add(newMeshVertices[1].normal);
                    m_sideUvs[i].Add(newMeshVertices[1].uv);

                    m_sideIndices[i].Add(m_sideVertices[i].Count);
                    m_sideVertices[i].Add(newMeshVertices[0].vertex);
                    m_sideNormals[i].Add(newMeshVertices[0].normal);
                    m_sideUvs[i].Add(newMeshVertices[0].uv);

                    DiviedVertexFromMesh(i == 0, sideVertices[i][h].index);
                }
                else
                {
                    // Correct Direction
                    m_sideIndices[i].Add(m_sideVertices[i].Count);
                    m_sideVertices[i].Add(newMeshVertices[0].vertex);
                    m_sideNormals[i].Add(newMeshVertices[0].normal);
                    m_sideUvs[i].Add(newMeshVertices[0].uv);

                    m_sideIndices[i].Add(m_sideVertices[i].Count);
                    m_sideVertices[i].Add(newMeshVertices[1].vertex);
                    m_sideNormals[i].Add(newMeshVertices[1].normal);
                    m_sideUvs[i].Add(newMeshVertices[1].uv);

                    DiviedVertexFromMesh(i == 0, sideVertices[i][h].index);
                }    
            }
        }
    }

    // Divied vertex from original mesh
    void DiviedVertexFromMesh(bool sideFlag, int index)
    {
        int side = sideFlag ? 0 : 1;

        if (m_sideVertexIndex[side].ContainsKey(index))
        {
            m_sideIndices[side].Add(m_sideVertexIndex[side][index]);
        }
        else
        {
            m_sideVertexIndex[side].Add(index, m_sideVertices[side].Count);
            m_sideIndices[side].Add(m_sideVertices[side].Count);
            m_sideVertices[side].Add(meshFilter.mesh.vertices[index]);
            m_sideNormals[side].Add(meshFilter.mesh.normals[index]);
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

        List<MeshVertex>[] sideVertices = new List<MeshVertex>[] { new List<MeshVertex>(), new List<MeshVertex>() };
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

            if (sides[0]) { sideVertices[0].Add(new MeshVertex(indices[i], vertices[indices[i]], meshFilter.mesh.normals[indices[i]], meshFilter.mesh.uv[indices[i]])); }
            else { sideVertices[1].Add(new MeshVertex(indices[i], vertices[indices[i]], meshFilter.mesh.normals[indices[i]], meshFilter.mesh.uv[indices[i]])); }
            if (sides[1]) { sideVertices[0].Add(new MeshVertex(indices[i + 1], vertices[indices[i + 1]], meshFilter.mesh.normals[indices[i + 1]], meshFilter.mesh.uv[indices[i + 1]])); }
            else { sideVertices[1].Add(new MeshVertex(indices[i + 1], vertices[indices[i + 1]], meshFilter.mesh.normals[indices[i + 1]], meshFilter.mesh.uv[indices[i + 1]])); }
            if (sides[2]) { sideVertices[0].Add(new MeshVertex(indices[i + 2], vertices[indices[i + 2]], meshFilter.mesh.normals[indices[i + 2]], meshFilter.mesh.uv[indices[i + 2]])); }
            else { sideVertices[1].Add(new MeshVertex(indices[i + 2], vertices[indices[i + 2]], meshFilter.mesh.normals[indices[i + 2]], meshFilter.mesh.uv[indices[i + 2]])); }

            if (sides[0] == sides[1] && sides[0] == sides[2])
            {
                // Same Side
                DiviedVertexFromMesh(sides[0], indices[i]);
                DiviedVertexFromMesh(sides[0], indices[i+1]);
                DiviedVertexFromMesh(sides[0], indices[i+2]);
            }
            else
            {
                // Slice 2 Sides
                // a side 1 vertex, other side 2 vertcies
                // 1. Make Vertex Info
                // 2. Add Triangle
                AddSliceLineTriangle(Vector3.Cross(v1 - v0, v1 - v2), sideVertices, MakeNewVertexInPlane(slice, sideVertices));

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
