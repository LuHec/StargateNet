using System;
using UnityEngine;
using System.Collections.Generic;

public class GizmoTimerDrawer : MonoBehaviour
{
    // 单例实例
    private static GizmoTimerDrawer _instance;

    // 获取单例实例
    public static GizmoTimerDrawer Instance
    {
        get
        {
            if (_instance == null)
            {
                // 查找场景中的 GizmoTimerDrawer 对象
                _instance = FindObjectOfType<GizmoTimerDrawer>();

                // 如果场景中没有实例，则创建一个新的
                if (_instance == null)
                {
                    GameObject obj = new GameObject("GizmoTimerDrawer");
                    _instance = obj.AddComponent<GizmoTimerDrawer>();
                }
            }
            return _instance;
        }
    }

    // 存储绘制的Gizmo对象信息
    private struct GizmoDraw : IComparable<GizmoDraw>
    {
        public float endTime;       // 绘制结束时间 (startTime + duration)
        public GizmoType gizmoType; // 绘制类型
        public object[] parameters; // 绘制参数
        public Color color;        // 绘制颜色

        // 实现 IComparable 接口，按照 endTime 排序
        public int CompareTo(GizmoDraw other)
        {
            return endTime.CompareTo(other.endTime); // 按照结束时间排序
        }
    }

    private enum GizmoType
    {
        DrawLine,
        DrawWireSphere,
        DrawRay,
        DrawCube
    }

    private PriorityQueue<GizmoDraw> gizmoQueue = new PriorityQueue<GizmoDraw>();  // 使用优先队列存储Gizmo绘制
    private List<GizmoDraw> activeGizmos = new List<GizmoDraw>();  // 存储当前仍然有效的Gizmos

    // 绘制方法：绘制一条持续时间为duration的线，支持颜色
    public void DrawLineWithTimer(Vector3 start, Vector3 end, float duration, Color color)
    {
        GizmoDraw newGizmo = new GizmoDraw
        {
            endTime = Time.time + duration,
            gizmoType = GizmoType.DrawLine,
            parameters = new object[] { start, end },
            color = color
        };
        gizmoQueue.Enqueue(newGizmo);
        activeGizmos.Add(newGizmo);
    }

    // 绘制方法：绘制一个持续时间为duration的WireSphere，支持颜色
    public void DrawWireSphereWithTimer(Vector3 center, float radius, float duration, Color color)
    {
        GizmoDraw newGizmo = new GizmoDraw
        {
            endTime = Time.time + duration,
            gizmoType = GizmoType.DrawWireSphere,
            parameters = new object[] { center, radius },
            color = color
        };
        gizmoQueue.Enqueue(newGizmo);
        activeGizmos.Add(newGizmo);
    }

    // 绘制方法：绘制一个持续时间为duration的射线，支持颜色
    public void DrawRayWithTimer(Vector3 start, Vector3 direction, float duration, Color color)
    {
        GizmoDraw newGizmo = new GizmoDraw
        {
            endTime = Time.time + duration,
            gizmoType = GizmoType.DrawRay,
            parameters = new object[] { start, direction },
            color = color
        };
        gizmoQueue.Enqueue(newGizmo);
        activeGizmos.Add(newGizmo);
    }

    // 绘制方法：绘制一个持续时间为duration的Cube，支持颜色
    public void DrawCubeWithTimer(Vector3 center, Vector3 size, float duration, Color color)
    {
        GizmoDraw newGizmo = new GizmoDraw
        {
            endTime = Time.time + duration,
            gizmoType = GizmoType.DrawCube,
            parameters = new object[] { center, size },
            color = color
        };
        gizmoQueue.Enqueue(newGizmo);
        activeGizmos.Add(newGizmo);
    }

    // 每帧检查并绘制有效的Gizmos
    private void RemoveExpiredGizmos()
    {
        // 检查并移除已过期的Gizmo
        for (int i = activeGizmos.Count - 1; i >= 0; i--)
        {
            GizmoDraw gizmo = activeGizmos[i];
            if (gizmo.endTime <= Time.time)  // 如果到期了
            {
                activeGizmos.RemoveAt(i);  // 从有效队列中移除
            }
        }
    }

    // 每帧绘制有效的Gizmos
    private void DrawActiveGizmos()
    {
        foreach (var gizmo in activeGizmos)
        {
            DrawGizmo(gizmo);  // 持续绘制有效的Gizmo
        }
    }

    // 绘制Gizmo
    private void DrawGizmo(GizmoDraw gizmo)
    {
        // 设置颜色
        Gizmos.color = gizmo.color;

        switch (gizmo.gizmoType)
        {
            case GizmoType.DrawLine:
                // 编辑模式下使用 Gizmos
                if (Application.isEditor)
                {
                    Gizmos.DrawLine((Vector3)gizmo.parameters[0], (Vector3)gizmo.parameters[1]);
                }
                else
                {
                    // 打包后使用 Debug.DrawLine
                    Debug.DrawLine((Vector3)gizmo.parameters[0], (Vector3)gizmo.parameters[1], gizmo.color);
                }
                break;
            case GizmoType.DrawWireSphere:
                if (Application.isEditor)
                {
                    Gizmos.DrawWireSphere((Vector3)gizmo.parameters[0], (float)gizmo.parameters[1]);
                }
                else
                {
                    // 打包后无法直接显示，采用 Debug 绘制
                    Debug.DrawRay((Vector3)gizmo.parameters[0], Vector3.up * (float)gizmo.parameters[1], gizmo.color, gizmo.endTime - Time.time);
                }
                break;
            case GizmoType.DrawRay:
                if (Application.isEditor)
                {
                    Gizmos.DrawRay((Vector3)gizmo.parameters[0], (Vector3)gizmo.parameters[1]);
                }
                else
                {
                    Debug.DrawRay((Vector3)gizmo.parameters[0], (Vector3)gizmo.parameters[1], gizmo.color, gizmo.endTime - Time.time);
                }
                break;
            case GizmoType.DrawCube:
                if (Application.isEditor)
                {
                    Gizmos.DrawWireCube((Vector3)gizmo.parameters[0], (Vector3)gizmo.parameters[1]);
                }
                else
                {
                    // 使用 Debug 绘制一个立方体，暂时用线段绘制包围盒（这个需要根据实际情况调整）
                    Debug.DrawLine((Vector3)gizmo.parameters[0], (Vector3)gizmo.parameters[1], gizmo.color, gizmo.endTime - Time.time);
                }
                break;
        }
    }

    // 在编辑模式下绘制所有仍在持续时间内的Gizmos
    void OnDrawGizmos()
    {
        RemoveExpiredGizmos();  // 移除已过期的Gizmos
        DrawActiveGizmos();     // 绘制有效的Gizmos
    }
}
