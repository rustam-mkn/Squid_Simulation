// /Users/user/Dev/Unity/Squid/Assets/UI/GraphRenderer.cs
using UnityEngine;
using System.Collections.Generic;

[RequireComponent(typeof(LineRenderer))]
public class GraphRenderer : MonoBehaviour
{
    private LineRenderer lineRenderer;
    public RectTransform graphAreaRect; // Прямоугольник UI Canvas, в котором рисуется график

    [Header("Graph Style")]
    public Color graphColor = Color.green;
    public float lineWidth = 2f; // Толщина линии в пикселях UI
    public int maxPointsToDisplay = 100; // Макс. кол-во точек на графике для производительности

    void Awake()
    {
        lineRenderer = GetComponent<LineRenderer>();
        if (lineRenderer == null) {
            Debug.LogError("GraphRenderer requires a LineRenderer component!");
            enabled = false; return;
        }
        
        // Настройка LineRenderer
        lineRenderer.startWidth = lineWidth;
        lineRenderer.endWidth = lineWidth;
        lineRenderer.material = new Material(Shader.Find("Legacy Shaders/Particles/Alpha Blended Premultiply")); // Простой материал
        // Или используйте Unlit/Color для простого цвета без прозрачности
        // lineRenderer.material = new Material(Shader.Find("Unlit/Color"));
        lineRenderer.startColor = graphColor;
        lineRenderer.endColor = graphColor;
        lineRenderer.positionCount = 0;
        lineRenderer.useWorldSpace = false; // Важно для UI! Рисуем в локальных координатах RectTransform
        lineRenderer.sortingOrder = 10; // Чтобы был поверх других UI элементов, если нужно
    }

    public void DrawGraph(List<float> dataPoints, float historicalMaxValueOverride = -1f)
    {
        if (!enabled || dataPoints == null) {
            if(lineRenderer != null) lineRenderer.positionCount = 0;
            return;
        }

        // Ограничиваем количество точек для отображения
        List<float> pointsToDraw = dataPoints;
        if (dataPoints.Count > maxPointsToDisplay) {
            pointsToDraw = dataPoints.GetRange(dataPoints.Count - maxPointsToDisplay, maxPointsToDisplay);
        }

        if (pointsToDraw.Count < 2) {
            lineRenderer.positionCount = 0;
            return;
        }
        
        lineRenderer.positionCount = pointsToDraw.Count;
        
        float maxValue = 0;
        if (historicalMaxValueOverride > 0.001f) { // Используем переданное макс. значение
            maxValue = historicalMaxValueOverride;
        } else { // Иначе ищем максимум в текущих данных
            foreach (float point in pointsToDraw) if (point > maxValue) maxValue = point;
        }
        if (maxValue < 0.001f && maxValue > -0.001f) maxValue = 1; // Избегаем деления на ноль, если все значения ~0

        if (graphAreaRect == null) {
            Debug.LogWarning("GraphArea RectTransform not assigned to GraphRenderer: " + gameObject.name);
            // Пытаемся взять RectTransform родителя, если это UI элемент
            graphAreaRect = transform.parent as RectTransform;
            if (graphAreaRect == null) {
                lineRenderer.positionCount = 0;
                return;
            }
        }

        Rect rect = graphAreaRect.rect; // Локальные размеры RectTransform
        float graphWidth = rect.width;
        float graphHeight = rect.height;

        for (int i = 0; i < pointsToDraw.Count; i++)
        {
            // X распределяется по всей ширине графика
            float x = (float)i / (pointsToDraw.Count - 1) * graphWidth;
            // Y нормализуется относительно maxValue и масштабируется по высоте графика
            float y = (pointsToDraw[i] / maxValue) * graphHeight;
            y = Mathf.Clamp(y, 0, graphHeight); // Ограничиваем, чтобы не выходило за пределы
            
            // Позиции в локальных координатах RectTransform (центр = 0,0)
            // Смещаем, чтобы 0,0 графика был в левом нижнем углу graphAreaRect
            lineRenderer.SetPosition(i, new Vector3(x - graphWidth * 0.5f, y - graphHeight * 0.5f, 0));
        }
    }
}
