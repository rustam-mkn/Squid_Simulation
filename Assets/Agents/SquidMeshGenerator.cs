// /Users/user/Dev/Unity/Squid/Assets/Agents/SquidMeshGenerator.cs
using UnityEngine;
using System.Collections.Generic;

public class SquidMeshGenerator : MonoBehaviour
{
    private Genome genome;
    private Transform agentTransform;

    // --- Ссылки на созданные GameObjects с мешами ---
    public GameObject mantleObject { get; private set; }
    public List<GameObject> swimmingTentacleObjects { get; private set; } = new List<GameObject>();
    public List<GameObject> graspingTentacleObjects { get; private set; } = new List<GameObject>();
    public GameObject eyesRootObject { get; private set; } // Родительский для глаз

    // --- Материалы (назначаются в инспекторе префаба SquidAgent) ---
    [Header("Materials (Assign in Prefab)")]
    public Material mantleMaterial;
    public Material tentacleMaterial; // Общий для всех _щупалец
    public Material eyeMaterial;    // Для "белка" глаза
    public Material pupilMaterial;  // Для зрачка

    private bool isInitialized = false;

    public void Initialize(Transform parentTransform)
    {
        this.agentTransform = parentTransform;
        // Проверка материалов (должны быть назначены на префабе агента)
        if (mantleMaterial == null || tentacleMaterial == null || eyeMaterial == null || pupilMaterial == null) {
            Debug.LogError($"SquidMeshGenerator on {agentTransform.name} is missing one or more materials. Please assign them on the Agent prefab.");
            enabled = false; return;
        }
        isInitialized = true;
    }

    public void GenerateInitialMeshes(Genome agentGenome)
    {
        if (!isInitialized || agentGenome == null) {
             Debug.LogError($"SquidMeshGenerator cannot generate meshes: Not initialized or genome is null. Agent: {agentTransform.name}");
             return;
        }
        this.genome = agentGenome;

        // Очистка старых объектов, если они были (на случай регенерации)
        ClearExistingMeshObjects();

        // 1. Создание меша мантии
        mantleObject = CreateMeshHolder("Mantle", agentTransform, mantleMaterial, genome.mantleColor);
        Mesh mantleMesh = GenerateMantleProceduralMesh(genome.mantleLength, genome.mantleMaxDiameter);
        if (mantleObject.TryGetComponent<MeshFilter>(out var mantleMF)) mantleMF.mesh = mantleMesh;
        
        // 2. Создание объектов для плавательных щупалец (8 штук)
        for (int i = 0; i < TentacleController.NUM_SWIMMING_TENTACLES; i++)
        {
            GameObject tentacleGO = CreateMeshHolder($"SwimmingTentacle_{i}", agentTransform, tentacleMaterial, genome.mantleColor * 0.85f);
            float angle = i * (360f / TentacleController.NUM_SWIMMING_TENTACLES) + 180f; // Начинаем сзади и по кругу
            // Располагаем у "основания" мантии (ближе к хвосту)
            Vector3 baseOffset = new Vector3(0, -genome.mantleLength * 0.4f, 0);
            tentacleGO.transform.localPosition = baseOffset + Quaternion.Euler(0,0,angle) * Vector3.up * (genome.mantleMaxDiameter * 0.45f);
            tentacleGO.transform.localRotation = Quaternion.Euler(0,0,angle);

            // Генерируем меш для плавательного щупальца
            // Для MVP можно использовать простой цилиндр или даже не генерировать меш, а просто объект-заглушку
            Mesh swimTentacleMesh = GenerateProceduralTentacleMesh(genome.baseSwimTentacleLength, genome.swimTentacleThickness, 10, 6, false);
            if (tentacleGO.TryGetComponent<MeshFilter>(out var swimMF)) swimMF.mesh = swimTentacleMesh;
            swimmingTentacleObjects.Add(tentacleGO);
        }

        // 3. Создание объектов для хватательных щупалец (2 штуки)
        for (int i = 0; i < TentacleController.NUM_GRASPING_TENTACLES; i++)
        {
            // Хватательные щупальца будут использовать LineRenderer, управляемый из GraspingTentacle.cs
            // Поэтому здесь создаем только пустой GameObject как точку крепления и для скрипта.
            GameObject tentacleGO = new GameObject($"GraspingTentacle_{i}");
            tentacleGO.transform.SetParent(agentTransform);
            // Располагаем их чуть более вентрально и ближе к "голове"
            float sideOffset = (i == 0 ? -1f : 1f) * genome.mantleMaxDiameter * 0.2f;
            tentacleGO.transform.localPosition = new Vector3(sideOffset, -genome.mantleLength * 0.25f, -0.01f); // Чуть впереди по Z для видимости
            tentacleGO.transform.localRotation = Quaternion.Euler(0,0, (i == 0 ? 15f : -15f)); // Небольшой начальный разворот
            graspingTentacleObjects.Add(tentacleGO);
            // MeshFilter и MeshRenderer не нужны, если LineRenderer будет в GraspingTentacle.cs
        }

        // 4. Создание "глаз"
        eyesRootObject = new GameObject("EyesRoot");
        eyesRootObject.transform.SetParent(agentTransform);
        // Располагаем на "головной" части мантии
        eyesRootObject.transform.localPosition = new Vector3(0, genome.mantleLength * 0.3f, -0.02f); // Z, чтобы были поверх мантии
        
        CreateEye("LeftEye", eyesRootObject.transform, -genome.mantleMaxDiameter * 0.25f, genome.eyeSize);
        CreateEye("RightEye", eyesRootObject.transform, genome.mantleMaxDiameter * 0.25f, genome.eyeSize);
    }

    GameObject CreateMeshHolder(string name, Transform parent, Material material, Color color)
    {
        GameObject obj = new GameObject(name);
        obj.transform.SetParent(parent);
        obj.transform.localPosition = Vector3.zero;
        obj.transform.localRotation = Quaternion.identity;
        obj.AddComponent<MeshFilter>();
        MeshRenderer mr = obj.AddComponent<MeshRenderer>();
        mr.material = material;
        mr.material.color = color; // Устанавливаем цвет копии материала
        return obj;
    }
    
    void CreateEye(string name, Transform parent, float xOffset, float eyeRadius)
    {
        // Белок глаза
        GameObject eyeWhiteObj = CreateMeshHolder(name, parent, eyeMaterial, Color.white);
        eyeWhiteObj.transform.localPosition = new Vector3(xOffset, 0, 0);
        Mesh eyeWhiteMesh = Generate2DCircleMesh(eyeRadius, 16);
        if (eyeWhiteObj.TryGetComponent<MeshFilter>(out var eyeMF)) eyeMF.mesh = eyeWhiteMesh;

        // Зрачок
        GameObject pupilObj = CreateMeshHolder("Pupil", eyeWhiteObj.transform, pupilMaterial, Color.black);
        pupilObj.transform.localPosition = new Vector3(0, 0, -0.01f); // Чуть впереди белка
        float pupilRadius = eyeRadius * 0.5f;
        Mesh pupilMesh = Generate2DCircleMesh(pupilRadius, 12);
        if (pupilObj.TryGetComponent<MeshFilter>(out var pupilMF)) pupilMF.mesh = pupilMesh;
        // TODO: Анимация зрачка (размер, положение)
    }


    Mesh GenerateMantleProceduralMesh(float length, float diameter)
    {
        Mesh mesh = new Mesh { name = "ProceduralMantle" };
        // Упрощенный вариант: вытянутая сфера (капсула без полусфер на концах, а скорее эллипсоид вращения)
        // Для MVP: можно взять примитив Unity "Capsule" и отмасштабировать, но это не генерация.
        // Давайте сделаем простой цилиндр, сужающийся к одному концу.
        int segmentsAround = 12; // Количество сегментов по окружности
        int segmentsAlong = 2;  // Всего 3 кольца вершин (0, 1, 2)
        float radius = diameter / 2f;

        List<Vector3> vertices = new List<Vector3>();
        List<int> triangles = new List<int>();
        List<Vector2> uvs = new List<Vector2>();


        for (int y = 0; y <= segmentsAlong; y++)
        {
            float currentY = -length / 2f + (length / segmentsAlong) * y;
            float currentRadius = radius;
            if (y == segmentsAlong) currentRadius *= 0.1f; // Заостренный "хвост"
            else if (y == 0) currentRadius *= 0.7f; // "Голова" чуть уже основания

            for (int i = 0; i <= segmentsAround; i++) // <= для замыкания UV
            {
                float angle = (float)i / segmentsAround * Mathf.PI * 2f;
                vertices.Add(new Vector3(Mathf.Cos(angle) * currentRadius, currentY, Mathf.Sin(angle) * currentRadius));
                uvs.Add(new Vector2((float)i/segmentsAround, (float)y/segmentsAlong));
            }
        }
        
        // Вершина для "хвостового" полюса
        int tipPoleIndex = vertices.Count;
        vertices.Add(new Vector3(0, length / 2f + length * 0.05f, 0)); // Чуть дальше для остроты
        uvs.Add(new Vector2(0.5f, 1f));
        // Вершина для "головного" полюса
        int headPoleIndex = vertices.Count;
        vertices.Add(new Vector3(0, -length / 2f - length * 0.05f, 0));
        uvs.Add(new Vector2(0.5f, 0f));


        for (int y = 0; y < segmentsAlong; y++)
        {
            for (int i = 0; i < segmentsAround; i++)
            {
                int v0 = y * (segmentsAround + 1) + i;
                int v1 = y * (segmentsAround + 1) + (i + 1);
                int v2 = (y + 1) * (segmentsAround + 1) + i;
                int v3 = (y + 1) * (segmentsAround + 1) + (i + 1);
                triangles.AddRange(new int[] { v0, v2, v1 });
                triangles.AddRange(new int[] { v1, v2, v3 });
            }
        }
        
        // "Закрываем" хвост
        int lastRingStartIdx = segmentsAlong * (segmentsAround + 1);
        for (int i = 0; i < segmentsAround; i++) {
            triangles.AddRange(new int[] { tipPoleIndex, lastRingStartIdx + i, lastRingStartIdx + i + 1 });
        }
        // "Закрываем" голову
        int firstRingStartIdx = 0;
         for (int i = 0; i < segmentsAround; i++) {
            triangles.AddRange(new int[] { headPoleIndex, firstRingStartIdx + i + 1, firstRingStartIdx + i });
        }


        mesh.vertices = vertices.ToArray();
        mesh.triangles = triangles.ToArray();
        mesh.uv = uvs.ToArray();
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    Mesh GenerateProceduralTentacleMesh(float length, float thickness, int segmentsAlong, int segmentsAround, bool bulbousTip)
    {
        Mesh mesh = new Mesh { name = "ProceduralTentacle" };
        List<Vector3> vertices = new List<Vector3>();
        List<int> triangles = new List<int>();
        List<Vector2> uvs = new List<Vector2>();
        float radius = thickness / 2f;

        for (int y_idx = 0; y_idx <= segmentsAlong; y_idx++) {
            float y_pos = (length / segmentsAlong) * y_idx;
            float currentRadius = radius;
            if (y_idx == 0) currentRadius *= 0.8f; // У основания чуть тоньше
            if (bulbousTip && y_idx == segmentsAlong) currentRadius *= 1.8f; // Утолщение на конце
            else if (y_idx > segmentsAlong / 2) currentRadius = Mathf.Lerp(radius, radius * (bulbousTip ? 0.7f : 0.3f) , (float)(y_idx - segmentsAlong / 2) / (segmentsAlong / 2));

            for (int i_idx = 0; i_idx <= segmentsAround; i_idx++) { // <= для UV
                float angle = (float)i_idx / segmentsAround * Mathf.PI * 2f;
                // Ось Y - вдоль щупальца
                vertices.Add(new Vector3(Mathf.Cos(angle) * currentRadius, y_pos, Mathf.Sin(angle) * currentRadius));
                uvs.Add(new Vector2((float)i_idx/segmentsAround, (float)y_idx/segmentsAlong));
            }
        }
        // Полюс на кончике
        int tipPole = vertices.Count;
        vertices.Add(new Vector3(0, length + length * (bulbousTip ? 0.05f : 0.02f), 0));
        uvs.Add(new Vector2(0.5f,1f));


        for (int y = 0; y < segmentsAlong; y++) {
            for (int i = 0; i < segmentsAround; i++) {
                int v0 = y * (segmentsAround + 1) + i;
                int v1 = y * (segmentsAround + 1) + (i + 1);
                int v2 = (y + 1) * (segmentsAround + 1) + i;
                int v3 = (y + 1) * (segmentsAround + 1) + (i + 1);
                triangles.AddRange(new int[] { v0, v2, v1 });
                triangles.AddRange(new int[] { v1, v2, v3 });
            }
        }
        // Закрываем кончик
        int lastRingStart = segmentsAlong * (segmentsAround + 1);
        for (int i = 0; i < segmentsAround; i++) {
            triangles.AddRange(new int[] { tipPole, lastRingStart + i, lastRingStart + i + 1 });
        }
        // TODO: Закрыть основание (не так критично, т.к. оно "в теле")

        mesh.vertices = vertices.ToArray();
        mesh.triangles = triangles.ToArray();
        mesh.uv = uvs.ToArray();
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    Mesh Generate2DCircleMesh(float radius, int segments)
    {
        Mesh mesh = new Mesh { name = "2DCircle" };
        List<Vector3> vertices = new List<Vector3> { Vector3.zero }; // Центр
        List<int> triangles = new List<int>();
        List<Vector2> uvs = new List<Vector2> { new Vector2(0.5f, 0.5f) };

        for (int i = 0; i <= segments; i++) {
            float angle = (float)i / segments * Mathf.PI * 2f;
            vertices.Add(new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, 0));
            uvs.Add(new Vector2(Mathf.Cos(angle) * 0.5f + 0.5f, Mathf.Sin(angle) * 0.5f + 0.5f));
            if (i > 0) { // Формируем треугольники
                triangles.AddRange(new int[] { 0, i, i + 1 });
            }
        }
        mesh.vertices = vertices.ToArray();
        mesh.triangles = triangles.ToArray();
        mesh.uv = uvs.ToArray();
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    void ClearExistingMeshObjects()
    {
        if (mantleObject != null) Destroy(mantleObject);
        if (eyesRootObject != null) Destroy(eyesRootObject); // Уничтожит и дочерние глаза
        foreach (var go in swimmingTentacleObjects) if (go != null) Destroy(go);
        foreach (var go in graspingTentacleObjects) if (go != null) Destroy(go);
        swimmingTentacleObjects.Clear();
        graspingTentacleObjects.Clear();
    }

    public void UpdateDynamicMeshes(SquidBrain.BrainOutput brainOutput) // Вызывать из SquidAgent, если нужна анимация мешей
    {
        if (!isInitialized || genome == null) return;

        // Пример: Пульсация мантии через изменение вершин (сложно) или scale (проще)
        if (mantleObject != null)
        {
            float pulseFactor = 1f + Mathf.Sin(Time.time * 5f * (0.1f + brainOutput.moveForward * 0.9f)) * 0.05f * genome.mantleLength;
            mantleObject.transform.localScale = new Vector3(pulseFactor, genome.mantleLength, pulseFactor); // Грубая пульсация
            // Обновление цвета, если он меняется не только по геному
            if (mantleObject.TryGetComponent<MeshRenderer>(out var mr)) mr.material.color = genome.mantleColor;
        }
        
        // Анимация плавательных щупалец (если они мешевые и анимируются здесь)
        // уже делается в SwimmingTentacle.UpdateMovement
    }
}
