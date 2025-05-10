// /Users/user/Dev/Unity/Squid/Assets/Agents/SquidSenses.cs
using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class SquidSenses : MonoBehaviour
{
    [Header("Base Sensory Parameters")]
    public float baseSightRadius = 10f;
    public float baseSightAngle = 120f; // Угол обзора в градусах
    
    [Header("Layers")]
    public LayerMask foodLayer;
    public LayerMask agentLayer;
    public LayerMask obstacleLayer;

    private Genome genome;
    private Transform agentTransform; // Кэшируем для производительности

    // Динамические параметры сенсоров, зависящие от генома
    private float currentSightRadius;
    private float currentSightAngle;

    // Для отладки и передачи информации о целях
    private List<Transform> visibleFoodDebug = new List<Transform>();
    private List<Transform> visibleAgentsDebug = new List<Transform>();
    private Dictionary<GameObject, float> targetPriorities = new Dictionary<GameObject, float>();


    public void Initialize(Genome agentGenome, Transform ownerTransform)
    {
        this.genome = agentGenome;
        this.agentTransform = ownerTransform;

        if (genome == null || agentTransform == null) {
            Debug.LogError("SquidSenses initialized with null genome or transform!");
            enabled = false; return;
        }

        // Настройка сенсоров на основе генома (например, размер глаз влияет на радиус/угол)
        currentSightRadius = baseSightRadius * (1f + genome.eyeSize * 2f); // Глаза влияют на радиус
        currentSightAngle = baseSightAngle * (1f + genome.eyeSize * 0.5f); // И немного на угол
    }

    public List<float> GatherSenses()
    {
        if (!enabled) return new List<float>(); // Если не инициализирован

        List<float> inputs = new List<float>();
        visibleFoodDebug.Clear();
        visibleAgentsDebug.Clear();
        targetPriorities.Clear();

        // 1. Сенсоры еды (N ближайших в поле зрения)
        // Параметр count - сколько ближайших объектов каждого типа мы хотим передать в НС
        AddTargetSensorInputs(inputs, foodLayer, 2, ref visibleFoodDebug, "Food");

        // 2. Сенсоры других агентов (N ближайших в поле зрения)
        AddTargetSensorInputs(inputs, agentLayer, 1, ref visibleAgentsDebug, "Agent");

        // 3. Сенсоры стен/препятствий (например, 3 луча: вперед, вперед-влево, вперед-вправо)
        AddObstacleSensorInputs(inputs);

        // 4. Собственные параметры
        if (TryGetComponent<SquidMetabolism>(out var metabolism))
        {
            inputs.Add(Mathf.Clamp01(metabolism.CurrentEnergy / metabolism.maxEnergyGeno));
            inputs.Add(Mathf.Clamp01(metabolism.Age / genome.maxAge));
        } else {
            inputs.Add(0.5f); inputs.Add(0f); // Default if no metabolism
        }
        
        // 5. Состояние щупалец (например, занято ли щупальце, держит ли еду)
        // TODO: Это потребует методов в TentacleController
        // TentacleController tc = GetComponent<TentacleController>();
        // if (tc != null && tc.graspingTentacles.Count > 0) {
        //    inputs.Add(tc.graspingTentacles[0].IsBusy() ? 1f : 0f);
        //    inputs.Add(tc.graspingTentacles[0].IsHoldingFoodItem() ? 1f : 0f);
        // } else { inputs.Add(0f); inputs.Add(0f); }
        // if (tc != null && tc.graspingTentacles.Count > 1) {
        //    inputs.Add(tc.graspingTentacles[1].IsBusy() ? 1f : 0f);
        //    inputs.Add(tc.graspingTentacles[1].IsHoldingFoodItem() ? 1f : 0f);
        // } else { inputs.Add(0f); inputs.Add(0f); }


        // Убедимся, что количество входов соответствует ожиданиям НС (из генома)
        int expectedInputs = genome.inputNodes;
        while (inputs.Count < expectedInputs)
        {
            inputs.Add(0f); // Дополняем нулями (означает "ничего не обнаружено" для этих сенсоров)
        }
        if (inputs.Count > expectedInputs)
        {
            Debug.LogWarning($"Senses generated {inputs.Count} inputs, but NN expects {expectedInputs}. Truncating.");
            inputs = inputs.Take(expectedInputs).ToList();
        }

        return inputs;
    }
    
    // Для TentacleController, чтобы он знал о целях
    public Dictionary<GameObject, float> GetTargetInfo()
    {
        return targetPriorities;
    }

    void AddTargetSensorInputs(List<float> inputs, LayerMask layer, int count, ref List<Transform> visibleDebugList, string targetType)
    {
        Collider2D[] colliders = Physics2D.OverlapCircleAll(agentTransform.position, currentSightRadius, layer);
        List<(Transform item, float dist, Vector2 dirRel, float priority)> sortedTargets =
            new List<(Transform, float, Vector2, float)>();

        foreach (var col in colliders)
        {
            if (targetType == "Agent" && col.gameObject == this.gameObject) continue; // Агент не видит себя

            Vector3 directionToTargetWorld = col.transform.position - agentTransform.position;
            float angleToTarget = Vector2.Angle(agentTransform.up, directionToTargetWorld.normalized);

            if (angleToTarget < currentSightAngle / 2f) // В поле зрения
            {
                float distance = directionToTargetWorld.magnitude;
                if (distance < currentSightRadius && distance > 0.01f) // Проверка дистанции и не нулевой дистанции
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
        // Сортируем по приоритету (убывающий), затем по дистанции (возрастающей)
        sortedTargets = sortedTargets.OrderByDescending(t => t.priority).ThenBy(t => t.dist).ToList();

        for (int i = 0; i < count; i++)
        {
            if (i < sortedTargets.Count)
            {
                inputs.Add(Mathf.Clamp01(sortedTargets[i].dist / currentSightRadius)); // Норм. расстояние
                inputs.Add(sortedTargets[i].dirRel.x); // -1 (слева) to 1 (справа)
                inputs.Add(sortedTargets[i].dirRel.y); // -1 (сзади) to 1 (впереди)
                // Дополнительный вход: тип цели или ее "ценность" (1 для еды, 0.5 для другого агента и т.д.)
                inputs.Add(targetType == "Food" ? 1.0f : (targetType == "Agent" ? 0.5f : 0f) );
                if (visibleDebugList != null) visibleDebugList.Add(sortedTargets[i].item);
            }
            else // Если целей меньше, чем count
            {
                inputs.Add(1f); // Макс. расстояние
                inputs.Add(0f); inputs.Add(0f); // Нет направления
                inputs.Add(0f); // Нет типа цели
            }
        }
    }
    
    float CalculatePriority(GameObject target, float distance, string type)
    {
        float basePriority = 1.0f / (distance + 0.1f); // Ближе - выше приоритет
        if (type == "Food") {
            Food foodComp = target.GetComponent<Food>();
            if (foodComp != null) {
                 // Предпочтение типа пищи из генома: 0 (растения) - 1 (мясо)
                 float preferenceFactor = (foodComp.type == FoodType.Plant) ? (1f - genome.foodPreference) : genome.foodPreference;
                 basePriority *= (1f + preferenceFactor); // Увеличиваем приоритет для предпочитаемой еды
                 basePriority *= (foodComp.energyValue / 10f); // Более питательная еда приоритетнее
            }
        } else if (type == "Agent") {
            // TODO: Приоритет для других агентов (атака/избегание)
            // Можно учитывать их размер, энергию, агрессию (если видна)
            // basePriority *= (1f - genome.cautionFactor); // Менее осторожные более склонны рассматривать других агентов
        }
        return basePriority;
    }


    void AddObstacleSensorInputs(List<float> inputs)
    {
        float rayLength = currentSightRadius * 0.3f; // Лучи короче основного зрения
        // Вперед
        inputs.Add(CastObstacleRay(agentTransform.up, rayLength));
        // Вперед-Влево (например, 30 градусов)
        inputs.Add(CastObstacleRay(Quaternion.Euler(0,0,30) * agentTransform.up, rayLength));
        // Вперед-Вправо (например, 30 градусов)
        inputs.Add(CastObstacleRay(Quaternion.Euler(0,0,-30) * agentTransform.up, rayLength));
    }

    float CastObstacleRay(Vector2 direction, float length)
    {
        // Начинаем луч чуть впереди от центра, чтобы не задевать свой коллайдер
        Vector2 rayOrigin = (Vector2)agentTransform.position + direction * 0.1f;
        RaycastHit2D hit = Physics2D.Raycast(rayOrigin, direction, length, obstacleLayer);
        if (hit.collider != null)
        {
            return Mathf.Clamp01(hit.distance / length); // 0 (близко) to 1 (далеко)
        }
        return 1f; // Нет препятствия (макс. расстояние)
    }

    void OnDrawGizmosSelected()
    {
        if (agentTransform == null) agentTransform = transform; // Для работы в редакторе до инициализации

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(agentTransform.position, currentSightRadius);

        Vector3 forward = agentTransform.up;
        Vector3 leftFOVLine = Quaternion.AngleAxis(-currentSightAngle / 2, Vector3.forward) * forward;
        Vector3 rightFOVLine = Quaternion.AngleAxis(currentSightAngle / 2, Vector3.forward) * forward;
        Gizmos.DrawRay(agentTransform.position, leftFOVLine * currentSightRadius);
        Gizmos.DrawRay(agentTransform.position, rightFOVLine * currentSightRadius);

        Gizmos.color = Color.red;
        float rayLength = currentSightRadius * 0.3f;
        Gizmos.DrawRay(agentTransform.position, agentTransform.up * rayLength);
        Gizmos.DrawRay(agentTransform.position, Quaternion.Euler(0,0,30) * agentTransform.up * rayLength);
        Gizmos.DrawRay(agentTransform.position, Quaternion.Euler(0,0,-30) * agentTransform.up * rayLength);

        if (Application.isPlaying) { // Только во время игры
            Gizmos.color = Color.green;
            foreach(var food in visibleFoodDebug) if(food != null) Gizmos.DrawLine(agentTransform.position, food.position);
            Gizmos.color = Color.blue;
            foreach(var agent in visibleAgentsDebug) if(agent != null) Gizmos.DrawLine(agentTransform.position, agent.position);
        }
    }
}
