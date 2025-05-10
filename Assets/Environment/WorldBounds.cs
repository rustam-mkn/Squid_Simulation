// /Users/user/Dev/Unity/Squid/Assets/Environment/WorldBounds.cs
using UnityEngine;

public class WorldBounds : MonoBehaviour
{
    public float xMin = -30f, xMax = 30f;
    public float yMin = -30f, yMax = 30f;

    // Можно добавить BoxCollider2D по периметру и настроить физический материал
    // чтобы агенты отскакивали или останавливались.
    // Если нет, то агенты должны сами проверять границы.

    void Start() {
        // Создаем физические границы, если их нет
        CreateBoundaryColliders();
    }

    void CreateBoundaryColliders() {
        // Убедимся, что на этом объекте есть Rigidbody2D (static) и коллайдеры
        if (GetComponent<Rigidbody2D>() == null) {
            Rigidbody2D rb = gameObject.AddComponent<Rigidbody2D>();
            rb.bodyType = RigidbodyType2D.Static;
        }

        float thickness = 1f; // Толщина стен
        // Нижняя стена
        CreateBoundaryWall("BottomWall", new Vector2((xMin+xMax)/2, yMin - thickness/2), new Vector2(xMax-xMin + thickness*2, thickness));
        // Верхняя стена
        CreateBoundaryWall("TopWall", new Vector2((xMin+xMax)/2, yMax + thickness/2), new Vector2(xMax-xMin + thickness*2, thickness));
        // Левая стена
        CreateBoundaryWall("LeftWall", new Vector2(xMin - thickness/2, (yMin+yMax)/2), new Vector2(thickness, yMax-yMin));
        // Правая стена
        CreateBoundaryWall("RightWall", new Vector2(xMax + thickness/2, (yMin+yMax)/2), new Vector2(thickness, yMax-yMin));
    }

    void CreateBoundaryWall(string name, Vector2 position, Vector2 size) {
        GameObject wall = new GameObject(name);
        wall.transform.SetParent(transform);
        wall.transform.position = position;
        BoxCollider2D bc = wall.AddComponent<BoxCollider2D>();
        bc.size = size;
        wall.layer = LayerMask.NameToLayer("Obstacle"); // Убедитесь, что слой "Obstacle" существует
    }


    // Если не используем физические границы, а агент сам проверяет:
    public bool IsOutOfBounds(Vector3 position) {
        return position.x < xMin || position.x > xMax || position.y < yMin || position.y > yMax;
    }

    public Vector3 ClampPosition(Vector3 position) {
        return new Vector3(
            Mathf.Clamp(position.x, xMin, xMax),
            Mathf.Clamp(position.y, yMin, yMax),
            position.z
        );
    }

    void OnDrawGizmos()
    {
        Gizmos.color = Color.cyan;
        Vector3 p1 = new Vector3(xMin, yMin, 0);
        Vector3 p2 = new Vector3(xMax, yMin, 0);
        Vector3 p3 = new Vector3(xMax, yMax, 0);
        Vector3 p4 = new Vector3(xMin, yMax, 0);
        Gizmos.DrawLine(p1,p2); Gizmos.DrawLine(p2,p3);
        Gizmos.DrawLine(p3,p4); Gizmos.DrawLine(p4,p1);
    }
}
