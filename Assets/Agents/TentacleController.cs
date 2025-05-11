// /Users/user/Dev/Unity/Squid/Assets/Agents/TentacleController.cs
using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class TentacleController : MonoBehaviour
{
    public const int NUM_SWIMMING_TENTACLES = 8;
    // public const int NUM_GRASPING_TENTACLES = 2; // УДАЛЕНО

    public List<SwimmingTentacle> swimmingTentacles { get; private set; } = new List<SwimmingTentacle>();
    // public List<GraspingTentacle> graspingTentacles { get; private set; } = new List<GraspingTentacle>(); // УДАЛЕНО

    private Genome genome;
    private Transform agentTransform;
    private SquidMeshGenerator meshGenerator;

    // private Dictionary<GameObject, float> currentSenseTargets; // Больше не нужен здесь, если нет хватательных

    public void Initialize(Genome agentGenome, Transform parentTransform, SquidMeshGenerator meshGen)
    {
        this.genome = agentGenome;
        this.agentTransform = parentTransform;
        this.meshGenerator = meshGen;

        if (genome == null || agentTransform == null || meshGenerator == null) {
            Debug.LogError($"TentacleController ({gameObject.name}) initialized with null dependencies for agent {parentTransform?.name}!");
            enabled = false; return;
        }

        // Очищаем список перед заполнением (GameObjects щупалец управляются SquidMeshGenerator)
        // Старые объекты щупалец должны быть уничтожены в SquidMeshGenerator.ClearExistingMeshObjects()
        swimmingTentacles.Clear();
        // graspingTentacles.Clear(); // УДАЛЕНО

        for (int i = 0; i < NUM_SWIMMING_TENTACLES; i++)
        {
            if (i < meshGenerator.swimmingTentacleObjects.Count && meshGenerator.swimmingTentacleObjects[i] != null)
            {
                GameObject tentacleGO = meshGenerator.swimmingTentacleObjects[i];
                SwimmingTentacle st = tentacleGO.GetComponent<SwimmingTentacle>();
                if (st == null) st = tentacleGO.AddComponent<SwimmingTentacle>();
                
                st.Initialize(genome, i, tentacleGO.GetComponent<MeshFilter>(), agentTransform);
                swimmingTentacles.Add(st);
            } else {
                Debug.LogWarning($"Swimming tentacle GO missing or null for index {i} on agent {agentTransform.name}");
            }
        }
        // Блок инициализации хватательных щупалец УДАЛЕН
    }

    public void UpdateAllTentacles(SquidBrain.BrainOutput brainOutput, Dictionary<GameObject, float> sensedTargets_IGNORED) // sensedTargets больше не нужен здесь
    {
        if (!enabled) return;
        // this.currentSenseTargets = sensedTargets; // Больше не нужен

        float moveIntensity = Mathf.Abs(brainOutput.moveForward) + Mathf.Abs(brainOutput.turn) * 0.5f;
        foreach (var st in swimmingTentacles)
        {
            if (st != null) st.UpdateMovement(Mathf.Clamp01(moveIntensity));
        }
        // Блок обновления хватательных щупалец УДАЛЕН
    }

    // Методы IsHoldingFood и ConsumeHeldFood больше не нужны в TentacleController,
    // так как поедание будет обрабатываться через коллайдер "рта" в SquidAgent.
    // public bool IsHoldingFood() { return false; }
    // public Food ConsumeHeldFood() { return null; }

} // END OF CLASS TentacleController


// ====================================================================================
// Вспомогательный класс для плавательного щупальца
// (Код взят из вашей предыдущей полной версии этого файла)
// ====================================================================================
public class SwimmingTentacle : MonoBehaviour
{
    private Genome genome;
    private MeshFilter meshFilter;
    private Transform rootTransform; // Его собственный transform (точка крепления)
    // private Transform agentBodyTransform; // Не используется напрямую в этой версии

    private float wavePhaseOffset;
    private float waveFrequencyBase = 4f;
    private float waveAmplitudeFactor = 0.08f;

    private Vector3[] originalVertices;
    private bool initializedCorrectly = false;

    public void Initialize(Genome agentGenome, int index, MeshFilter mf, Transform agentTF_IGNORED) // agentTF не используется
    {
        this.genome = agentGenome;
        this.meshFilter = mf;
        this.rootTransform = transform; // Точка крепления щупальца

        if (genome == null || rootTransform == null) {
            Debug.LogWarning($"SwimmingTentacle on {gameObject.name} failed to initialize properly (genome or rootTransform is null).");
            enabled = false; return;
        }

        this.wavePhaseOffset = Random.value * Mathf.PI * 2f + index * 0.5f; // Разное смещение для каждого щупальца
        
        if (meshFilter != null && meshFilter.sharedMesh != null) {
            originalVertices = meshFilter.sharedMesh.vertices;
            // Создаем уникальный экземпляр меша для анимации, если еще не сделано
            if (meshFilter.mesh == meshFilter.sharedMesh || meshFilter.mesh == null) { // Проверяем и на null
                 meshFilter.mesh = Instantiate<Mesh>(meshFilter.sharedMesh);
            }
            initializedCorrectly = originalVertices != null && originalVertices.Length > 0;
        } else {
            // Debug.LogWarning($"SwimmingTentacle {name} has no mesh filter or shared mesh for animation.");
             initializedCorrectly = false;
             enabled = false;
        }
    }

    public void UpdateMovement(float moveIntensity)
    {
        if (!initializedCorrectly || genome == null) return; // Добавил проверку на genome
        
        float effectiveWaveFrequency = waveFrequencyBase * (0.5f + moveIntensity * 1.5f);
        float time = Time.time * effectiveWaveFrequency;
        // Убедимся, что baseSwimTentacleLength не нулевой, чтобы избежать NaN или Infinity
        float currentBaseLength = (genome.baseSwimTentacleLength > 0.01f) ? genome.baseSwimTentacleLength : 0.1f;
        float actualAmplitude = currentBaseLength * waveAmplitudeFactor;

        Vector3[] currentVertices = meshFilter.mesh.vertices;
        if (currentVertices.Length != originalVertices.Length) {
            Debug.LogWarning($"Vertex count mismatch in SwimmingTentacle {name}. Reverting to original.");
            currentVertices = (Vector3[])originalVertices.Clone();
            if(currentVertices.Length != originalVertices.Length) { // Если и клон не помог
                Debug.LogError($"Catastrophic vertex mismatch in SwimmingTentacle {name}. Disabling animation.");
                enabled = false; return;
            }
        }

        for(int i=0; i < originalVertices.Length; ++i) {
            Vector3 originalVert = originalVertices[i];
            float normalizedYPos = (currentBaseLength > 0.01f) ?
                                   Mathf.Clamp01(originalVert.y / currentBaseLength) : 0f;
            
            float phase = normalizedYPos * 6f + time + wavePhaseOffset;
            currentVertices[i].x = originalVert.x + Mathf.Sin(phase) * actualAmplitude * (1f - normalizedYPos * normalizedYPos);
        }
        meshFilter.mesh.vertices = currentVertices;
        if (meshFilter.mesh.vertexCount > 0) meshFilter.mesh.RecalculateNormals();
    }
} // END OF CLASS SwimmingTentacle

// Класс GraspingTentacle БОЛЬШЕ НЕ НУЖЕН И ДОЛЖЕН БЫТЬ УДАЛЕН ИЗ ПРОЕКТА.
