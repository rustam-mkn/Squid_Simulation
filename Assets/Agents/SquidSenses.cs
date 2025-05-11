// /Users/user/Dev/Unity/Squid/Assets/Agents/SquidSenses.cs
using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class SquidSenses : MonoBehaviour
{
    [Header("Base Sensory Parameters")]
    public float baseSightRadius = 12f;
    public float baseSightAngle = 130f;
    
    [Header("Layers")]
    public LayerMask foodLayer;
    public LayerMask agentLayer;
    public LayerMask obstacleLayer;

    private Genome genome;
    private Transform agentTransform;

    // --- ИЗМЕНЕНИЕ: Делаем currentSightRadius публичным для чтения ---
    public float currentSightRadius { get; private set; }
    public float currentSightAngle { get; private set; }
    // -----------------------------------------------------------------

    private List<Transform> visibleFoodDebug = new List<Transform>();
    private List<Transform> visibleAgentsDebug = new List<Transform>();
    private Dictionary<GameObject, float> targetPriorities = new Dictionary<GameObject, float>();

    private SquidAgent parentAgent;
    private UIManager uiManager;

    public void Initialize(Genome agentGenome, Transform ownerTransform)
    {
        this.genome = agentGenome;
        this.agentTransform = ownerTransform;
        this.parentAgent = GetComponent<SquidAgent>();
        this.uiManager = FindFirstObjectByType<UIManager>();

        if (genome == null || agentTransform == null) {
            Debug.LogError($"SquidSenses on {ownerTransform?.name} initialized with null genome or transform! Disabling.");
            enabled = false; return;
        }

        // Расчет текущих параметров зрения на основе генома
        // eyeSize влияет на то, насколько сильно базовые значения могут быть увеличены/уменьшены
        // Предположим, eyeSize из генома это множитель от 0.5 до 1.5 к базовому восприятию
        // или он абсолютный и мы его как-то нормализуем.
        // В Genome.cs: eyeSize = Random.Range(0.10f, 0.18f) * mantleMaxDiameter;
        // Это абсолютный размер. Давайте сделаем так, чтобы он влиял на радиус.
        // Например, если средний eyeSize = 0.1, а базовый радиус 10, то это будет 10.
        // Если eyeSize = 0.2, то радиус будет больше.
        // Нормализуем eyeSize относительно некоторого "среднего" ожидаемого размера глаза,
        // чтобы получить множитель для базовых параметров.
        float averageExpectedEyeSize = 0.1f * 0.5f; // Пример: 0.1 * (средний mantleMaxDiameter)
        float eyeSizeFactor = Mathf.Clamp(genome.eyeSize / Mathf.Max(averageExpectedEyeSize, 0.01f), 0.5f, 2.0f); // Множитель от 0.5x до 2x

        currentSightRadius = baseSightRadius * eyeSizeFactor;
        currentSightAngle = baseSightAngle * Mathf.Sqrt(eyeSizeFactor); // Угол меняется меньше, чем радиус
        currentSightAngle = Mathf.Clamp(currentSightAngle, 30f, 359f);
    }

    // ... (GatherSenses, AddTargetSensorInputs, CalculatePriority, AddObstacleSensorInputs, CastObstacleRay, OnDrawGizmos - без изменений) ...
    // Скопирую их для полноты, так как вы просите полные файлы
    public List<float> GatherSenses()
    {
        if (!enabled || genome == null || agentTransform == null) return new List<float>(new float[genome?.inputNodes ?? 17]);

        List<float> inputs = new List<float>();
        visibleFoodDebug.Clear();
        visibleAgentsDebug.Clear();
        targetPriorities.Clear();

        AddTargetSensorInputs(inputs, foodLayer, 2, ref visibleFoodDebug, "Food");
        AddTargetSensorInputs(inputs, agentLayer, 1, ref visibleAgentsDebug, "Agent");
        AddObstacleSensorInputs(inputs);

        if (TryGetComponent<SquidMetabolism>(out var metabolism)) {
            inputs.Add(Mathf.Clamp01(metabolism.CurrentEnergy / metabolism.maxEnergyGeno));
            inputs.Add(Mathf.Clamp01(metabolism.Age / genome.maxAge));
        } else {
            inputs.Add(0.5f); inputs.Add(0f);
        }
        
        int expectedInputs = (genome != null) ? genome.inputNodes : FindFirstObjectByType<SimulationManager>().numInputNodes;
        while (inputs.Count < expectedInputs) inputs.Add(0f);
        if (inputs.Count > expectedInputs) inputs = inputs.Take(expectedInputs).ToList();

        return inputs;
    }
    
    public Dictionary<GameObject, float> GetTargetInfo() { return targetPriorities; }

    void AddTargetSensorInputs(List<float> inputs, LayerMask layer, int count, ref List<Transform> visibleDebugList, string targetType)
    {
        Collider2D[] colliders = Physics2D.OverlapCircleAll(agentTransform.position, currentSightRadius, layer);
        List<(Transform item, float dist, Vector2 dirRel, float priority)> sortedTargets =
            new List<(Transform, float, Vector2, float)>();

        foreach (var col in colliders)
        {
            if (targetType == "Agent" && col.gameObject == this.gameObject) continue;

            Vector3 directionToTargetWorld = col.transform.position - agentTransform.position;
            float angleToTarget = Vector2.Angle(agentTransform.up, directionToTargetWorld.normalized);

            if (angleToTarget < currentSightAngle / 2f)
            {
                float distance = directionToTargetWorld.magnitude;
                if (distance < currentSightRadius && distance > 0.01f)
                {
                    Vector2 relativeDirection = agentTransform.InverseTransformDirection(directionToTargetWorld.normalized);
                    float priority = CalculatePriority(col.gameObject, distance, targetType);
                    sortedTargets.Add((col.transform, distance, relativeDirection, priority));
                    if (!targetPriorities.ContainsKey(col.gameObject) || priority > targetPriorities[col.gameObject]) {
                        targetPriorities[col.gameObject] = priority;
                    }
                }
            }
        }
        sortedTargets = sortedTargets.OrderByDescending(t => t.priority).ThenBy(t => t.dist).ToList();

        for (int i = 0; i < count; i++)
        {
            if (i < sortedTargets.Count)
            {
                inputs.Add(1.0f);
                inputs.Add(1.0f - Mathf.Clamp01(sortedTargets[i].dist / currentSightRadius));
                inputs.Add(sortedTargets[i].dirRel.x);
                inputs.Add(sortedTargets[i].dirRel.y);
                
                float typeOrSizeInput = 0f;
                if (targetType == "Food" && sortedTargets[i].item.TryGetComponent<Food>(out var food)) {
                     typeOrSizeInput = (food.type == FoodType.Meat ? 1.0f : 0.5f);
                } else if (targetType == "Agent" && sortedTargets[i].item.TryGetComponent<SquidAgent>(out var otherAgent)) {
                     typeOrSizeInput = Mathf.Clamp01(otherAgent.genome.mantleLength / 2f);
                }
                inputs.Add(typeOrSizeInput);
                if (visibleDebugList != null) visibleDebugList.Add(sortedTargets[i].item);
            }
            else
            {
                inputs.Add(0.0f); inputs.Add(0f); inputs.Add(0f); inputs.Add(0f); inputs.Add(0f);
            }
        }
    }
    
    float CalculatePriority(GameObject target, float distance, string type) {
        float basePriority = 1.0f / (distance * distance + 0.1f);
        if (type == "Food" && target.TryGetComponent<Food>(out var foodComp)) {
             float preferenceFactor = (genome.foodPreference - 0.5f) * 2f; // -1 для растений, +1 для мяса, если foodPreference 0..1
             if (foodComp.type == FoodType.Plant) basePriority *= (1f - preferenceFactor * 0.5f); // Увеличиваем, если предпочитаем растения
             else basePriority *= (1f + preferenceFactor * 0.5f); // Увеличиваем, если предпочитаем мясо
             basePriority *= (foodComp.energyValue / 5f);
        } else if (type == "Agent" && target.TryGetComponent<SquidAgent>(out var otherAgent)) {
            // basePriority *= genome.aggression;
        }
        return basePriority;
    }

    void AddObstacleSensorInputs(List<float> inputs) {
        float rayLength = currentSightRadius * 0.5f;
        inputs.Add(1.0f - CastObstacleRay(agentTransform.up, rayLength));
        inputs.Add(1.0f - CastObstacleRay(Quaternion.Euler(0,0,40) * agentTransform.up, rayLength));
        inputs.Add(1.0f - CastObstacleRay(Quaternion.Euler(0,0,-40) * agentTransform.up, rayLength));
    }
    float CastObstacleRay(Vector2 direction, float length) {
        Vector2 rayOrigin = (Vector2)agentTransform.position + direction * 0.2f;
        RaycastHit2D hit = Physics2D.Raycast(rayOrigin, direction, length, obstacleLayer);
        if (hit.collider != null) return Mathf.Clamp01(hit.distance / length);
        return 1f;
    }
    void OnDrawGizmos() {
        if (agentTransform == null || genome == null) return;
        bool shouldDraw = (uiManager != null && uiManager.showAllAgentGizmos);
        if (uiManager != null && uiManager.selectedAgentUI == parentAgent) shouldDraw = true;
        if (shouldDraw) {
            Gizmos.color = new Color(genome.mantleColor.r, genome.mantleColor.g, genome.mantleColor.b, 0.15f);
            Vector3 forward = agentTransform.up;
            float halfFOV = currentSightAngle / 2.0f;
            Quaternion leftRayRotation = Quaternion.AngleAxis(-halfFOV, Vector3.forward);
            Quaternion rightRayRotation = Quaternion.AngleAxis(halfFOV, Vector3.forward);
            Vector3 leftRayDirection = leftRayRotation * forward;
            Vector3 rightRayDirection = rightRayRotation * forward;
            int segments = 20;
            Vector3 prevPoint = agentTransform.position + leftRayDirection * currentSightRadius;
            for(int i=1; i <= segments; ++i) {
                float angle = Mathf.Lerp(-halfFOV, halfFOV, (float)i/segments);
                Vector3 currentRayDir = Quaternion.AngleAxis(angle, Vector3.forward) * forward;
                Vector3 currentPoint = agentTransform.position + currentRayDir * currentSightRadius;
                Gizmos.DrawLine(prevPoint, currentPoint);
                Gizmos.DrawLine(agentTransform.position, prevPoint);
                prevPoint = currentPoint;
            }
            Gizmos.DrawLine(agentTransform.position, agentTransform.position + leftRayDirection * currentSightRadius);
            Gizmos.DrawLine(agentTransform.position, agentTransform.position + rightRayDirection * currentSightRadius);
            if (uiManager != null && uiManager.selectedAgentUI == parentAgent) {
                Gizmos.color = Color.green;
                foreach(var food in visibleFoodDebug) if(food != null) Gizmos.DrawLine(agentTransform.position, food.position);
                Gizmos.color = Color.cyan;
                foreach(var agent in visibleAgentsDebug) if(agent != null) Gizmos.DrawLine(agentTransform.position, agent.position);
            }
        }
    }
}
