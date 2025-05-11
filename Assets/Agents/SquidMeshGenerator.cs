// /Users/user/Dev/Unity/Squid/Assets/Agents/SquidMeshGenerator.cs
using UnityEngine;
using System.Collections.Generic;

public class SquidMeshGenerator : MonoBehaviour
{
    private Genome genome;
    private Transform agentTransform;

    public GameObject mantleObject { get; private set; }
    public List<GameObject> swimmingTentacleObjects { get; private set; } = new List<GameObject>();
    // public List<GameObject> graspingTentacleObjects { get; private set; } = new List<GameObject>(); // УДАЛЕНО
    public GameObject eyesRootObject { get; private set; }
    
    public GameObject leftEyeWhite { get; private set; }
    public GameObject rightEyeWhite { get; private set; }
    public GameObject leftPupil { get; private set; }
    public GameObject rightPupil { get; private set; }


    [Header("Materials (Assign in Prefab)")]
    public Material mantleMaterial;
    public Material tentacleMaterial;
    public Material eyeMaterial;    // Для белка глаза
    public Material pupilMaterial;  // Для зрачка

    private bool isInitialized = false;

    public void Initialize(Transform parentTransform)
    {
        this.agentTransform = parentTransform;
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
        ClearExistingMeshObjects();

        // 1. Мантия
        Color mantleBaseColor = genome.mantleColor;
        mantleBaseColor.a = 1f;
        mantleObject = CreateMeshHolder("Mantle", agentTransform, mantleMaterial, mantleBaseColor);
        Mesh mantleMesh = GenerateMantleProceduralMesh(genome.mantleLength, genome.mantleMaxDiameter);
        if (mantleObject.TryGetComponent<MeshFilter>(out var mantleMF)) mantleMF.mesh = mantleMesh;
        mantleObject.transform.localPosition = new Vector3(0,0, 0.1f);
        
        float headBaseY = -genome.mantleLength * 0.4f;

        // 4. Глаза (КВАДРАТНЫЕ, между мантией и щупальцами)
        eyesRootObject = new GameObject("EyesRoot_Square_Bottom");
        eyesRootObject.transform.SetParent(agentTransform);
        eyesRootObject.transform.localPosition = new Vector3(0, headBaseY + genome.mantleMaxDiameter * 0.1f, -0.05f);
        
        float eyeSpacing = genome.eyeSize * 1.1f;
        float currentEyeSideLength = Mathf.Max(genome.eyeSize, 0.03f);

        leftEyeWhite = CreateEyePart_Square("LeftEye_Square", eyesRootObject.transform, -eyeSpacing / 2f, currentEyeSideLength, eyeMaterial, Color.white);
        rightEyeWhite = CreateEyePart_Square("RightEye_Square", eyesRootObject.transform, eyeSpacing / 2f, currentEyeSideLength, eyeMaterial, Color.white);

        float pupilSideLength = currentEyeSideLength * 0.5f;
        pupilSideLength = Mathf.Max(pupilSideLength, 0.015f);

        leftPupil = CreateEyePart_Square("LeftPupil_Square", leftEyeWhite.transform, 0, pupilSideLength, pupilMaterial, Color.black);
        leftPupil.transform.localPosition = new Vector3(0,0,-0.01f);
        
        rightPupil = CreateEyePart_Square("RightPupil_Square", rightEyeWhite.transform, 0, pupilSideLength, pupilMaterial, Color.black);
        rightPupil.transform.localPosition = new Vector3(0,0,-0.01f);


        // 2. Плавательные щупальца (вокруг "шейной" области, ниже глаз)
        for (int i = 0; i < TentacleController.NUM_SWIMMING_TENTACLES; i++) // Используем константу из TentacleController
        {
            Color tentacleBaseColor = Color.Lerp(genome.mantleColor, Color.black, 0.2f);
            tentacleBaseColor.a = 1f;
            GameObject tentacleGO = CreateMeshHolder($"SwimmingTentacle_{i}", agentTransform, tentacleMaterial, tentacleBaseColor);
            
            float angle = i * (360f / TentacleController.NUM_SWIMMING_TENTACLES);
            Vector3 tentacleAttachmentPoint = new Vector3(0, headBaseY - genome.mantleMaxDiameter * 0.05f, 0.05f);
            
            tentacleGO.transform.localPosition = tentacleAttachmentPoint +
                                                 Quaternion.Euler(0,0,angle) * Vector3.up * (genome.mantleMaxDiameter * 0.30f);
            tentacleGO.transform.localRotation = Quaternion.Euler(0,0,angle);
            tentacleGO.transform.localScale = Vector3.one * Mathf.Clamp(genome.mantleLength * 0.7f, 0.3f, 1.0f);

            Mesh swimTentacleMesh = GenerateProceduralTentacleMesh(genome.baseSwimTentacleLength, genome.swimTentacleThickness, 6, 4, false);
            if (tentacleGO.TryGetComponent<MeshFilter>(out var swimMF)) swimMF.mesh = swimTentacleMesh;
            swimmingTentacleObjects.Add(tentacleGO);
        }

        // 3. Хватательные щупальца - БОЛЬШЕ НЕ СОЗДАЕМ ИХ ОБЪЕКТЫ
        // graspingTentacleObjects.Clear(); // Уже очищается в ClearExistingMeshObjects
    }

    GameObject CreateMeshHolder(string name, Transform parent, Material sourceMaterial, Color color)
    {
        GameObject obj = new GameObject(name);
        obj.transform.SetParent(parent);
        obj.transform.localPosition = Vector3.zero;
        obj.transform.localRotation = Quaternion.identity;
        obj.AddComponent<MeshFilter>();
        MeshRenderer mr = obj.AddComponent<MeshRenderer>();
        
        if (sourceMaterial != null) {
             mr.material = new Material(sourceMaterial);
             mr.material.color = color;
        } else {
            Debug.LogWarning($"Source material is null for {name} on {parent.name}. Assigning default material.");
            mr.material = new Material(Shader.Find("Standard")); // Fallback
            mr.material.color = color;
        }
        return obj;
    }
    
    GameObject CreateEyePart_Square(string name, Transform parent, float xOffset, float sideLength, Material sourceMaterial, Color color)
    {
        GameObject eyePartObj = CreateMeshHolder(name, parent, sourceMaterial, color);
        eyePartObj.transform.localPosition = new Vector3(xOffset, 0, 0);
        Mesh eyeMesh = Generate2DSquareMesh(sideLength);
        if (eyePartObj.TryGetComponent<MeshFilter>(out var eyeMF)) eyeMF.mesh = eyeMesh;
        return eyePartObj;
    }

    Mesh GenerateMantleProceduralMesh(float length, float diameter) {
        Mesh mesh = new Mesh { name = "ProceduralMantle" };
        int segmentsAround = 12;
        int segmentsAlong = 4;
        float radius = diameter / 2f;
        List<Vector3> vertices = new List<Vector3>(); List<int> triangles = new List<int>(); List<Vector2> uvs = new List<Vector2>();
        for (int y_idx = 0; y_idx <= segmentsAlong; y_idx++) {
            float t_along = (float)y_idx / segmentsAlong; float currentY = -length / 2f + length * t_along;
            float radiusFactor = Mathf.Sin(t_along * Mathf.PI);
            if (t_along < 0.05f) radiusFactor = Mathf.Lerp(0.3f, Mathf.Sin(0.05f * Mathf.PI), t_along / 0.05f);
            else if (t_along > 0.95f) radiusFactor = Mathf.Lerp(Mathf.Sin(0.95f * Mathf.PI), 0.05f, (t_along - 0.95f) / 0.05f);
            float currentRadius = radius * radiusFactor; currentRadius = Mathf.Max(currentRadius, diameter * 0.02f);
            for (int i_idx = 0; i_idx <= segmentsAround; i_idx++) {
                float t_around = (float)i_idx / segmentsAround; float angle = t_around * Mathf.PI * 2f;
                vertices.Add(new Vector3(Mathf.Cos(angle) * currentRadius, currentY, Mathf.Sin(angle) * currentRadius));
                uvs.Add(new Vector2(t_around, t_along));
            }
        }
        for (int y = 0; y < segmentsAlong; y++) {
            for (int i = 0; i < segmentsAround; i++) {
                int v00 = y * (segmentsAround + 1) + i; int v01 = y * (segmentsAround + 1) + (i + 1);
                int v10 = (y + 1) * (segmentsAround + 1) + i; int v11 = (y + 1) * (segmentsAround + 1) + (i + 1);
                triangles.AddRange(new int[] { v00, v10, v11 }); triangles.AddRange(new int[] { v00, v11, v01 });
            }
        }
        int headPoleCenterIdx = vertices.Count; vertices.Add(new Vector3(0, -length/2f, 0)); uvs.Add(new Vector2(0.5f,0f));
        for(int i=0; i < segmentsAround; ++i) { triangles.AddRange(new int[] { headPoleCenterIdx, i+1, i }); }
        int tailPoleCenterIdx = vertices.Count; vertices.Add(new Vector3(0, length/2f, 0)); uvs.Add(new Vector2(0.5f,1f));
        int lastRingStartIdx = segmentsAlong * (segmentsAround +1);
        for(int i=0; i < segmentsAround; ++i) { triangles.AddRange(new int[] { tailPoleCenterIdx, lastRingStartIdx + i, lastRingStartIdx + i + 1 }); }
        mesh.vertices = vertices.ToArray(); mesh.triangles = triangles.ToArray(); mesh.uv = uvs.ToArray();
        mesh.RecalculateNormals(); mesh.RecalculateBounds(); return mesh;
    }

    Mesh GenerateProceduralTentacleMesh(float length, float thickness, int segmentsAlong, int segmentsAround, bool bulbousTip) {
        Mesh mesh = new Mesh { name = "ProceduralTentacle" };
        List<Vector3> vertices = new List<Vector3>(); List<int> triangles = new List<int>(); List<Vector2> uvs = new List<Vector2>();
        float radius = thickness / 2f;
        for (int y_idx = 0; y_idx <= segmentsAlong; y_idx++) {
            float t_along = (float)y_idx / segmentsAlong; float y_pos = length * t_along;
            float currentRadius = radius;
            if (t_along < 0.1f) currentRadius = Mathf.Lerp(radius * 0.8f, radius, t_along / 0.1f);
            else if (bulbousTip && t_along > 0.85f) currentRadius = Mathf.Lerp(radius, radius * 1.8f, (t_along - 0.85f) / 0.15f);
            else currentRadius = Mathf.Lerp(radius, radius * 0.2f, t_along);
            for (int i_idx = 0; i_idx <= segmentsAround; i_idx++) {
                float t_around = (float)i_idx / segmentsAround; float angle = t_around * Mathf.PI * 2f;
                vertices.Add(new Vector3(Mathf.Cos(angle) * currentRadius, y_pos, Mathf.Sin(angle) * currentRadius));
                uvs.Add(new Vector2(t_around, t_along));
            }
        }
        for (int y = 0; y < segmentsAlong; y++) {
            for (int i = 0; i < segmentsAround; i++) {
                int v00 = y * (segmentsAround + 1) + i; int v01 = y * (segmentsAround + 1) + (i + 1);
                int v10 = (y + 1) * (segmentsAround + 1) + i; int v11 = (y + 1) * (segmentsAround + 1) + (i + 1);
                triangles.AddRange(new int[] { v00, v10, v11 }); triangles.AddRange(new int[] { v00, v11, v01 });
            }
        }
        mesh.vertices = vertices.ToArray(); mesh.triangles = triangles.ToArray(); mesh.uv = uvs.ToArray();
        mesh.RecalculateNormals(); mesh.RecalculateBounds(); return mesh;
    }

    Mesh Generate2DSquareMesh(float sideLength)
    {
        Mesh mesh = new Mesh { name = "2DSquare" };
        float halfSide = sideLength / 2f;
        Vector3[] vertices = new Vector3[4] {
            new Vector3(-halfSide, -halfSide, 0), new Vector3( halfSide, -halfSide, 0),
            new Vector3(-halfSide,  halfSide, 0), new Vector3( halfSide,  halfSide, 0)
        };
        int[] triangles = new int[6] { 0, 2, 1, 2, 3, 1 };
        Vector2[] uvs = new Vector2[4] {
            new Vector2(0,0), new Vector2(1,0), new Vector2(0,1), new Vector2(1,1)
        };
        mesh.vertices = vertices; mesh.triangles = triangles; mesh.uv = uvs;
        mesh.RecalculateNormals(); mesh.RecalculateBounds();
        return mesh;
    }

    void ClearExistingMeshObjects() {
        if (mantleObject != null) Destroy(mantleObject); if (eyesRootObject != null) Destroy(eyesRootObject);
        foreach (var go in swimmingTentacleObjects) if (go != null) Destroy(go);
        // foreach (var go in graspingTentacleObjects) if (go != null) Destroy(go); // УДАЛЕНО
        swimmingTentacleObjects.Clear();
        // graspingTentacleObjects.Clear(); // УДАЛЕНО
        leftEyeWhite = rightEyeWhite = leftPupil = rightPupil = null;
    }

    public void UpdateDynamicMeshes(SquidBrain.BrainOutput brainOutput)  {
        if (!isInitialized || genome == null) return;
        if (mantleObject != null) {
            if (mantleObject.TryGetComponent<MeshRenderer>(out var mr)) {
                Color targetColor = genome.mantleColor; targetColor.a = 1f;
                if (mr.material.color != targetColor) mr.material.color = targetColor;
            }
        }
        UpdateEyeLook(brainOutput);
    }

    void UpdateEyeLook(SquidBrain.BrainOutput brainOutput) {
        if (leftPupil == null || rightPupil == null || genome == null || agentTransform == null) return;
        Vector2 lookDirAgentLocal = Vector2.up;
        bool hasSpecificTarget = false;

        // Логика определения lookDirAgentLocal (оставлена как в вашем эталонном коде)
        // ... (если хватательные щупальца активны) ...
        // ... (если еда видна) ...
        // ... (если нет цели - по направлению движения + блуждание) ...
        // Для примера, если хватательных щупалец нет, упростим:
        SquidSenses senses = GetComponent<SquidSenses>();
        if (senses != null) {
            var targetsInfo = senses.GetTargetInfo();
            GameObject bestTarget = null; float highestPriority = -1f;
            if (targetsInfo != null) {
                foreach (var entry in targetsInfo) {
                    if (entry.Key != null && entry.Key.GetComponent<Food>() != null) {
                        if (entry.Value > highestPriority) { highestPriority = entry.Value; bestTarget = entry.Key; }
                    }
                }
            }
            if (bestTarget != null) {
                Vector3 directionToTargetWorld = (bestTarget.transform.position - agentTransform.position);
                lookDirAgentLocal = agentTransform.InverseTransformDirection(directionToTargetWorld.normalized).normalized;
                hasSpecificTarget = true;
            }
        }
        if (!hasSpecificTarget) {
            if (TryGetComponent<Rigidbody2D>(out var rb) && rb.linearVelocity.sqrMagnitude > 0.01f) {
                lookDirAgentLocal = agentTransform.InverseTransformDirection(rb.linearVelocity.normalized).normalized;
            } else {
                lookDirAgentLocal = Vector2.up;
                float wanderAngle = (Mathf.PerlinNoise(Time.time * 0.5f + agentTransform.GetInstanceID() * 0.1f, Time.time * 0.3f) - 0.5f) * 2f * 45f;
                lookDirAgentLocal = Quaternion.Euler(0,0,wanderAngle) * lookDirAgentLocal;
            }
        }
        // Конец упрощенной логики lookDirAgentLocal

        float eyeWhiteSide = Mathf.Max(genome.eyeSize, 0.03f);
        float pupilSide = eyeWhiteSide * 0.5f;
        float maxPupilOffset = (eyeWhiteSide - pupilSide) / 2f * 0.95f;

        Vector3 localPupilTargetPosition = new Vector3(
            Mathf.Clamp(lookDirAgentLocal.x * maxPupilOffset * 2.0f, -maxPupilOffset, maxPupilOffset),
            Mathf.Clamp(lookDirAgentLocal.y * maxPupilOffset * 2.0f, -maxPupilOffset, maxPupilOffset),
            leftPupil.transform.localPosition.z
        );
        
        float pupilSmoothSpeed = 7f;
        leftPupil.transform.localPosition = Vector3.Lerp(leftPupil.transform.localPosition, localPupilTargetPosition, Time.deltaTime * pupilSmoothSpeed);
        rightPupil.transform.localPosition = Vector3.Lerp(rightPupil.transform.localPosition, localPupilTargetPosition, Time.deltaTime * pupilSmoothSpeed);
        
        float blinkSpeed = 1.8f + genome.aggression * 1.5f;
        float blinkPhase = Time.time * blinkSpeed + agentTransform.GetInstanceID() * 0.37f;
        float blinkValue = (Mathf.Sin(blinkPhase * Mathf.PI * 2.0f) + 1.0f) / 2.0f;
        float eyeScaleY = 1.0f;
        if (blinkValue < 0.05f) { eyeScaleY = 0.05f; }
        else if (blinkValue < 0.2f) { eyeScaleY = Mathf.Lerp(0.05f, 1.0f, blinkValue / 0.2f ); }
        
        float baseSideLength = Mathf.Max(genome.eyeSize, 0.03f);
        if (leftEyeWhite != null) leftEyeWhite.transform.localScale = new Vector3(baseSideLength, baseSideLength * eyeScaleY, 1f);
        if (rightEyeWhite != null) rightEyeWhite.transform.localScale = new Vector3(baseSideLength, baseSideLength * eyeScaleY, 1f);
    }
}
