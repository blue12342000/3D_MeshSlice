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

        public int prevIndex;
        public int nextIndex;

        public MeshVertex(int _index, Vector3 _vertex, Vector3 _normal, Vector2 _uv) => (index, vertex, normal, uv, prevIndex, nextIndex) = (_index, _vertex, _normal, _uv, 0, 0);
    }
    
    Vector3[] m_nearPoint = new Vector3[] { new Vector3(), new Vector3() };
    Vector3[] m_farPoint = new Vector3[] { new Vector3(), new Vector3() };
    Vector3 m_normal;
    bool isDrawGizmo = false;

    // Slice Side Mesh Vertices, Indices, Normals, Uvs
    List<int>[] m_sideIndices = new List<int>[] { new List<int>(), new List<int>(), new List<int>() };
    List<Vector3>[] m_sideVertices = new List<Vector3>[] { new List<Vector3>(), new List<Vector3>() };
    List<Vector3>[] m_sideNormals = new List<Vector3>[] { new List<Vector3>(), new List<Vector3>() };
    List<Vector2>[] m_sideUvs = new List<Vector2>[] { new List<Vector2>(), new List<Vector2>() };
    Dictionary<int, int>[] m_sideVertexIndex = new Dictionary<int, int>[] { new Dictionary<int, int>(), new Dictionary<int, int>() };

    // New Vertices in Plane
    // Originl Vertices -> 2 Mesh vertices
    List<int> m_newVertexIndices = new List<int>();
    Dictionary<int, Dictionary<int, MeshVertex>> m_newVertexInPlane = new Dictionary<int, Dictionary<int, MeshVertex>>();
    int m_newVertexCount = 0;

    // Cut Plane Vertex
    List<int> m_cutPlaneVertexIndices = new List<int>();
    // A -> B
    Dictionary<int, int> m_newVertexKeyMap = new Dictionary<int, int>();

    // 임시 나중에 Collider나 Trigger로 해당되는 Mesh들을 검출
    public GameObject targetObject;
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

        m_sideIndices[2].Clear();

        m_newVertexInPlane.Clear();
        m_newVertexCount = 0;
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
                ++m_newVertexCount;
                plane.Raycast(new Ray(mv1.vertex, (mv2.vertex - mv1.vertex).normalized), out float length);
                newVertex = new MeshVertex(-1 * m_newVertexCount
                                            , Vector3.Lerp(mv1.vertex, mv2.vertex, length / (mv2.vertex - mv1.vertex).magnitude)
                                            , Vector3.Lerp(mv1.normal, mv2.normal, length / (mv2.vertex - mv1.vertex).magnitude)
                                            , Vector2.Lerp(mv1.uv, mv2.uv, length / (mv2.vertex - mv1.vertex).magnitude));
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
                ++m_newVertexCount;
                plane.Raycast(new Ray(mv1.vertex, (mv2.vertex - mv1.vertex).normalized), out float length);
                newVertex = new MeshVertex(-1 * m_newVertexCount
                                            , Vector3.Lerp(mv1.vertex, mv2.vertex, length / (mv2.vertex - mv1.vertex).magnitude)
                                            , Vector3.Lerp(mv1.normal, mv2.normal, length / (mv2.vertex - mv1.vertex).magnitude)
                                            , Vector2.Lerp(mv1.uv, mv2.uv, length / (mv2.vertex - mv1.vertex).magnitude));
                if (!m_newVertexInPlane.ContainsKey(mv2.index)) { m_newVertexInPlane.Add(mv2.index, new Dictionary<int, MeshVertex>()); }
                m_newVertexInPlane[mv2.index].Add(mv1.index, newVertex);
            }
        }    

        return newVertex;
    }

    MeshVertex[] MakeNewVertexInPlane(Plane plane, List<MeshVertex>[] sideVertices)
    {
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
            if (Vector3.Dot(faceNormal, Vector3.Cross(newMeshVertices[1].vertex - newMeshVertices[0].vertex, sideVertices[i][0].vertex - newMeshVertices[1].vertex)) < 0)
            {
                // Incorrect Direction
                DiviedVertexFromMesh(i == 0, newMeshVertices[1]);
                DiviedVertexFromMesh(i == 0, newMeshVertices[0]);
                DiviedVertexFromMesh(i == 0, sideVertices[i][0].index);

                if (sideVertices[i].Count == 2)
                {
                    if (Vector3.Dot(newMeshVertices[0].vertex - newMeshVertices[1].vertex, sideVertices[i][1].vertex - sideVertices[i][0].vertex) < 0)
                    {
                        // Correct Direction
                        DiviedVertexFromMesh(i == 0, sideVertices[i][0].index);
                        DiviedVertexFromMesh(i == 0, sideVertices[i][1].index);
                        DiviedVertexFromMesh(i == 0, newMeshVertices[1]);
                    }
                    else
                    {
                        // Incorrect Direction
                        DiviedVertexFromMesh(i == 0, newMeshVertices[0]);
                        DiviedVertexFromMesh(i == 0, sideVertices[i][1].index);
                        DiviedVertexFromMesh(i == 0, sideVertices[i][0].index);
                    }
                }
            }
            else
            {
                // Correct Direction
                DiviedVertexFromMesh(i == 0, newMeshVertices[0]);
                DiviedVertexFromMesh(i == 0, newMeshVertices[1]);
                DiviedVertexFromMesh(i == 0, sideVertices[i][0].index);

                if (sideVertices[i].Count == 2)
                {
                    if (Vector3.Dot(newMeshVertices[1].vertex - newMeshVertices[0].vertex, sideVertices[i][1].vertex - sideVertices[i][0].vertex) < 0)
                    {
                        // Correct Direction
                        DiviedVertexFromMesh(i == 0, newMeshVertices[0]);
                        DiviedVertexFromMesh(i == 0, sideVertices[i][0].index);
                        DiviedVertexFromMesh(i == 0, sideVertices[i][1].index);
                    }
                    else
                    {
                        // Incorrect Direction
                        DiviedVertexFromMesh(i == 0, newMeshVertices[1]);
                        DiviedVertexFromMesh(i == 0, sideVertices[i][1].index);
                        DiviedVertexFromMesh(i == 0, sideVertices[i][0].index);
                    }
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

    void DiviedVertexFromMesh(bool sideFlag, MeshVertex meshVertex)
    {
        int side = sideFlag ? 0 : 1;

        if (m_sideVertexIndex[side].ContainsKey(meshVertex.index))
        {
            m_sideIndices[side].Add(m_sideVertexIndex[side][meshVertex.index]);
        }
        else
        {
            m_sideVertexIndex[side].Add(meshVertex.index, m_sideVertices[side].Count);
            m_sideIndices[side].Add(m_sideVertices[side].Count);
            m_sideVertices[side].Add(meshVertex.vertex);
            m_sideNormals[side].Add(meshVertex.normal);
            m_sideUvs[side].Add(meshVertex.uv);
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
                // 1. Make New Vertex
                // 2. Is New Vertex Data Exist?
                // 3. Yes: Get Data / No: Add Two Triangles
                AddSliceLineTriangle(Vector3.Cross(v1 - v0, v2 - v1), sideVertices, MakeNewVertexInPlane(slice, sideVertices));
            }
        }

        // Not Split Mesh
        if (m_sideVertices[0].Count == 0 || m_sideVertices[1].Count == 0) return;

        // Make Cut Plane Mesh
        // 기본 기능 : 속이 꽉 찬 Mesh를 절단 하는 것
        // 1. New Mesh Vertex를 순서대로 분류한다 List<int>()
        // 2. 0번 인덱스와 인접하지 않은 가장 가까운 점과 연결하고 그 점에서 인접한 점을 찾는다
        // 3. 3개의 점에대한 외적이 plane normal 값과 비교해서 옳바른 방향인지 확인한다.
        // 4. 옳바른 방향이라면 해당 방향으로 나아간다.

        // 심화 기능 고민해봐야 할 것 : 속이 빈 것을 절단하는 경우
        // X Y Z축에 Projectoin을 해서 new vertex그룹이 겹쳐있다면 외부 내부를 가려야함
        // 만약 x y z축중 한곳이라도 분리가 되어있다면 분리축이론에따라 두 그룹은 서로 다른 메시그룹임
        // 만약 내부 외부로 나뉜다면 반드시 Triangle은 두 그룹을 2:1, 1:2로 구성된 버텍스정보여야함.
        // 큰 그룹 내의 작은 그룹이 여러개 일 수 있음 (재귀 함수를 이용해서 검사해야 할 듯)
        // 1. New Mesh Vertex를 순서대로 분류한다 List<int>()
        // 2. List<List<int>> 로 각 절단면(List<int>)그룹을 겹친부분들에 대해서 데이터를 구성한다.
        // 3. struct CutPlane { List<List<int>> : 해당하는 현재 그룹의 vertex }
        Mesh[] newMeshes = new Mesh[] { new Mesh(), new Mesh() };
        newMeshes[0].name = "Slice Side Main Mesh";
        newMeshes[0].vertices = m_sideVertices[0].ToArray();
        newMeshes[0].SetIndices(m_sideIndices[0].ToArray(), MeshTopology.Triangles, 0);
        newMeshes[0].normals = m_sideNormals[0].ToArray();
        newMeshes[0].uv = m_sideUvs[0].ToArray();

        newMeshes[1].name = "Slice Side Other Mesh";
        newMeshes[1].vertices = m_sideVertices[1].ToArray();
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

            foreach (var mapvertex in m_newVertexInPlane)
            {
                foreach (var mv in mapvertex.Value)
                {
                    Gizmos.DrawSphere(mv.Value.vertex, 0.003f);
                }
            }
        }
    }
}
