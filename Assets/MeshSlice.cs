using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MeshSlice : MonoBehaviour
{
    Vector3 m_startPoint;
    Vector3 m_endPoint;
    Vector3 m_normal;
    bool isDrawGizmo = false;

    public MeshFilter meshFilter;

    Vector3 MousePointToWorldPoint(Vector3 point, Camera cam)
    {
        point.z = cam.nearClipPlane;
        return cam.ScreenToWorldPoint(point);
    }

    void SliceMesh(Mesh mesh)
    {

    }

    void Update()
    {
        if (Input.GetMouseButtonDown(0))
        {
            isDrawGizmo = false;
            m_startPoint = MousePointToWorldPoint(Input.mousePosition, Camera.main);
        }
        else if (Input.GetMouseButtonUp(0))
        {
            m_endPoint = MousePointToWorldPoint(Input.mousePosition, Camera.main);
            m_normal = Vector3.Cross(m_endPoint - m_startPoint, Camera.main.transform.forward).normalized;
            isDrawGizmo = true;

            SliceMesh(meshFilter.mesh);

            Debug.Log($"{m_startPoint} -> {m_endPoint}");
            Debug.Log($"normal : {m_normal}");
        }
    }

    private void OnDrawGizmos()
    {
        if (isDrawGizmo)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawLine(m_startPoint, m_endPoint);
        }
    }
}
