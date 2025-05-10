// /Users/user/Dev/Unity/Squid/Assets/Agents/TentacleController.cs
using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class TentacleController : MonoBehaviour
{
    public const int NUM_SWIMMING_TENTACLES = 8;
    public const int NUM_GRASPING_TENTACLES = 2;

    public List<SwimmingTentacle> swimmingTentacles { get; private set; } = new List<SwimmingTentacle>();
    public List<GraspingTentacle> graspingTentacles { get; private set; } = new List<GraspingTentacle>();

    private Genome genome;
    private Transform agentTransform;
    private SquidMeshGenerator meshGenerator;

    private Dictionary<GameObject, float> currentSenseTargets; // Цели от SquidSenses

    public void Initialize(Genome agentGenome, Transform parentTransform, SquidMeshGenerator meshGen)
    {
        this.genome = agentGenome;
        this.agentTransform = parentTransform;
        this.meshGenerator = meshGen;

        if (genome == null || agentTransform == null || meshGenerator == null) {
            Debug.LogError("TentacleController initialized with null dependencies!");
            enabled = false; return;
        }

        // Очищаем списки на случай реинициализации (хотя обычно это не происходит)
        swimmingTentacles.Clear();
        graspingTentacles.Clear();

        // Создание и инициализация плавательных щупалец
        for (int i = 0; i < NUM_SWIMMING_TENTACLES; i++)
        {
            if (i < meshGenerator.swimmingTentacleObjects.Count && meshGenerator.swimmingTentacleObjects[i] != null)
            {
                GameObject tentacleGO = meshGenerator.swimmingTentacleObjects[i];
                SwimmingTentacle st = tentacleGO.GetComponent<SwimmingTentacle>();
                if (st == null) st = tentacleGO.AddComponent<SwimmingTentacle>(); // Добавляем, если нет
                
                st.Initialize(genome, i, tentacleGO.GetComponent<MeshFilter>(), agentTransform);
                swimmingTentacles.Add(st);
            } else {
                Debug.LogWarning($"Swimming tentacle GO missing or null for index {i}");
            }
        }

        // Создание и инициализация хватательных щупалец
        for (int i = 0; i < NUM_GRASPING_TENTACLES; i++)
        {
             if (i < meshGenerator.graspingTentacleObjects.Count && meshGenerator.graspingTentacleObjects[i] != null)
            {
                GameObject tentacleGO = meshGenerator.graspingTentacleObjects[i];
                GraspingTentacle gt = tentacleGO.GetComponent<GraspingTentacle>();
                if (gt == null) gt = tentacleGO.AddComponent<GraspingTentacle>();

                gt.Initialize(genome, i, agentTransform); // MeshFilter для хватательных может быть не нужен, если они LineRenderer
                graspingTentacles.Add(gt);
            } else {
                 Debug.LogWarning($"Grasping tentacle GO missing or null for index {i}");
            }
        }
    }

    public void UpdateAllTentacles(SquidBrain.BrainOutput brainOutput, Dictionary<GameObject, float> sensedTargets)
    {
        if (!enabled) return;
        this.currentSenseTargets = sensedTargets;

        // Обновление плавательных щупалец (для анимации волны и т.д.)
        float moveIntensity = Mathf.Abs(brainOutput.moveForward) + Mathf.Abs(brainOutput.turn) * 0.5f;
        foreach (var st in swimmingTentacles)
        {
            st.UpdateMovement(Mathf.Clamp01(moveIntensity));
        }

        // Обновление хватательных щупалец
        if (graspingTentacles.Count > 0)
        {
            GameObject target0 = SelectTargetForGraspingTentacle(0);
            graspingTentacles[0].UpdateLogic(
                target0,
                brainOutput.graspTentacleTargetDir0,
                brainOutput.graspTentacleExtend0,
                brainOutput.graspTentacleTryGrasp0,
                brainOutput.graspTentacleTryAttack0 // TODO: attack
            );
        }
        if (graspingTentacles.Count > 1)
        {
            GameObject target1 = SelectTargetForGraspingTentacle(1);
             graspingTentacles[1].UpdateLogic(
                target1,
                brainOutput.graspTentacleTargetDir1,
                brainOutput.graspTentacleExtend1,
                brainOutput.graspTentacleTryGrasp1,
                brainOutput.graspTentacleTryAttack1 // TODO: attack
            );
        }
    }

    GameObject SelectTargetForGraspingTentacle(int tentacleIndex) {
        if (currentSenseTargets == null || currentSenseTargets.Count == 0) return null;

        // Простая логика: каждое щупальце пытается взять самую приоритетную цель,
        // если она еще не захвачена другим щупальцем этого же агента.
        var sortedTargets = currentSenseTargets
            .Where(pair => pair.Key != null) // Убираем уничтоженные цели
            .OrderByDescending(pair => pair.Value)
            .ToList();

        int assignedTargetCount = 0;
        foreach(var targetEntry in sortedTargets)
        {
            bool alreadyTargetedByOther = false;
            for(int i=0; i < graspingTentacles.Count; ++i) {
                if (i != tentacleIndex && graspingTentacles[i].currentTargetGO == targetEntry.Key) {
                    alreadyTargetedByOther = true;
                    break;
                }
            }
            if (!alreadyTargetedByOther) {
                if (assignedTargetCount == tentacleIndex) { // Если это N-ая доступная цель для N-го щупальца
                    return targetEntry.Key;
                }
                assignedTargetCount++;
            }
        }
        return null; // Нет подходящей уникальной цели
    }

    public bool IsHoldingFood()
    {
        foreach (var gt in graspingTentacles)
        {
            if (gt.IsHoldingFoodItem()) return true;
        }
        return false;
    }

    public Food ConsumeHeldFood() // Потребляет еду из ПЕРВОГО щупальца, которое держит еду
    {
        foreach (var gt in graspingTentacles)
        {
            if (gt.IsHoldingFoodItem()) {
                return gt.RetrieveAndReleaseHeldFood(); // Возвращаем Food компонент и освобождаем щупальце
            }
        }
        return null;
    }
}


public class SwimmingTentacle : MonoBehaviour // Должен быть на объекте щупальца
{
    private Genome genome;
    private MeshFilter meshFilter; // Если анимируем меш
    private Transform rootTransform; // Корень щупальца (его собственный transform)
    private Transform agentBodyTransform; // Transform тела кальмара

    // Параметры для анимации волны
    private float wavePhaseOffset;
    private float waveFrequency = 5f;
    private float waveAmplitudeFactor = 0.1f; // % от длины щупальца

    private Vector3[] originalVertices; // Для сброса/расчета анимации

    public void Initialize(Genome agentGenome, int index, MeshFilter mf, Transform agentTF)
    {
        this.genome = agentGenome;
        this.meshFilter = mf;
        this.rootTransform = transform;
        this.agentBodyTransform = agentTF;

        this.wavePhaseOffset = Random.value * Mathf.PI * 2f;
        
        if (meshFilter != null && meshFilter.sharedMesh != null) { // Используем sharedMesh для получения оригинальных вершин
            originalVertices = meshFilter.sharedMesh.vertices;
            // Создаем уникальный экземпляр меша для анимации, если еще не сделано
            if (meshFilter.mesh == meshFilter.sharedMesh) {
                 meshFilter.mesh = Instantiate<Mesh>(meshFilter.sharedMesh);
            }
        } else {
            // Debug.LogWarning($"SwimmingTentacle {name} has no mesh filter or mesh for animation.");
        }
    }

    public void UpdateMovement(float moveIntensity)
    {
        if (meshFilter == null || meshFilter.mesh == null || originalVertices == null || originalVertices.Length == 0) return;
        
        // Процедурная анимация волнообразного движения
        // Смещаем вершины меша щупальца
        float time = Time.time * waveFrequency * (0.2f + moveIntensity * 0.8f); // Скорость волны зависит от интенсивности движения
        float actualAmplitude = genome.baseSwimTentacleLength * waveAmplitudeFactor;

        Vector3[] currentVertices = meshFilter.mesh.vertices; // Получаем текущие или работаем с копией originalVertices
        if (currentVertices.Length != originalVertices.Length) currentVertices = (Vector3[])originalVertices.Clone();


        for(int i=0; i < originalVertices.Length; ++i) {
            Vector3 originalVert = originalVertices[i];
            // Предполагаем, что Y - это ось вдоль Pщупальца от основания (0) к кончику (length)
            // И X - ось для волнообразного смещения
            float normalizedYPos = originalVert.y / genome.baseSwimTentacleLength; // От 0 до 1
            float phase = normalizedYPos * 5f + time + wavePhaseOffset; // 5f - количество "волн" на щупальце
            
            // Смещение только по X (или другой поперечной оси)
            // Направление смещения должно быть перпендикулярно оси щупальца и оси вращения кальмара
            // Для простоты, если щупальце выровнено по Y, смещаем по X
            currentVertices[i].x = originalVert.x + Mathf.Sin(phase) * actualAmplitude * (1f - normalizedYPos); // Амплитуда затухает к кончику
        }
        meshFilter.mesh.vertices = currentVertices;
        meshFilter.mesh.RecalculateNormals();
    }
}


public class GraspingTentacle : MonoBehaviour // Должен быть на объекте щупальца
{
    private Genome genome;
    private int tentacleId; // 0 или 1
    private Transform rootTransform; // Его собственный transform (точка крепления)
    private Transform agentBodyTransform;

    private LineRenderer lineRenderer;

    private enum State { Idle, Targeting, Extending, GraspingFood, Retracting }
    private State currentState = State.Idle;

    public GameObject currentTargetGO { get; private set; }
    private Food heldFoodItemComponent; // Компонент еды, которую держим
    private float currentExtensionRatio = 0f; // 0 (втянуто) to 1 (макс вытянуто)
    
    private float extensionRetractionSpeed = 2.5f; // Единиц (отношение к макс длине) в секунду

    public void Initialize(Genome agentGenome, int id, Transform agentTF)
    {
        this.genome = agentGenome;
        this.tentacleId = id;
        this.rootTransform = transform;
        this.agentBodyTransform = agentTF;

        lineRenderer = GetComponent<LineRenderer>();
        if (lineRenderer == null) lineRenderer = gameObject.AddComponent<LineRenderer>();
        
        lineRenderer.positionCount = 20; // Больше точек для плавного изгиба (если будем делать изгиб)
                                         // Для прямой линии достаточно 2
        lineRenderer.startWidth = genome.graspTentacleThickness * 0.8f;
        lineRenderer.endWidth = genome.graspTentacleThickness * 0.4f;
        // Настроить материал для LineRenderer в инспекторе или здесь:
        if (lineRenderer.sharedMaterial == null) {
             // lineRenderer.material = new Material(Shader.Find("Legacy Shaders/Particles/Alpha Blended Premultiply"));
             // Лучше создать материал в проекте и назначить
             var renderer = GetComponentInParent<SquidMeshGenerator>()?.tentacleMaterial;
             if (renderer != null) lineRenderer.material = renderer;
             else lineRenderer.material = new Material(Shader.Find("Unlit/Color")); // Простой цветной
        }
        lineRenderer.startColor = genome.mantleColor * 0.9f;
        lineRenderer.endColor = genome.mantleColor * 0.7f;
        lineRenderer.enabled = false;
    }

    public void UpdateLogic(GameObject target, Vector2 targetDirSignal, float extendSignal, bool tryGraspSignal, bool tryAttackSignal)
    {
        HandleStateTransitions(target, targetDirSignal, extendSignal, tryGraspSignal, tryAttackSignal);
        PerformStateActions(targetDirSignal); // Передаем направление от НС для наведения
        UpdateVisuals();
    }

    void HandleStateTransitions(GameObject target, Vector2 targetDir, float extend, bool grasp, bool attack) {
        switch (currentState)
        {
            case State.Idle:
                if (target != null && extend > 0.1f && (grasp || attack)) {
                    currentTargetGO = target;
                    currentState = State.Targeting;
                } else if (extend > 0.1f) { // Просто вытягивание без цели
                    currentTargetGO = null; // Цели нет, но есть команда вытянуться
                    currentState = State.Extending;
                }
                break;
            case State.Targeting:
                if (currentTargetGO == null || !currentTargetGO.activeInHierarchy) { // Цель исчезla
                    currentState = State.Retracting; break;
                }
                if (extend < 0.1f) { currentState = State.Retracting; break; } // Команда втянуть
                // Если цель близко и есть команда на захват/атаку - переходим к вытягиванию
                currentState = State.Extending;
                break;
            case State.Extending:
                if (currentTargetGO == null && extend < 0.1f) { currentState = State.Retracting; break; }
                if (currentTargetGO != null && (!currentTargetGO.activeInHierarchy || extend < 0.1f)) {
                     currentState = State.Retracting; break;
                }
                // Проверка на достижение цели
                if (currentTargetGO != null && CheckTargetReached()) {
                    if (grasp) TryGraspTarget(); else if (attack) { /* TODO: TryAttackTarget(); */ }
                    // Если не удалось схватить/атаковать, но цель достигнута, можно остаться в Extending или Retracting
                    if (!IsHoldingFoodItem()) currentState = State.Retracting; // Не схватил, втягиваем
                }
                break;
            case State.GraspingFood:
                // Если дана команда втянуть или еда исчезла (не должно быть, т.к. мы ее "уничтожили")
                if (extend < 0.5f || heldFoodItemComponent == null) { // Втягиваем с едой или если еда пропала
                    currentState = State.Retracting;
                }
                break;
            case State.Retracting:
                if (currentExtensionRatio < 0.01f) {
                    currentState = State.Idle;
                    if (heldFoodItemComponent != null) {
                        // Еда "доставлена", но не съедена. SquidAgent решает, когда есть.
                        // Здесь можно просто сбросить heldFoodItemComponent, если агент не успел забрать.
                        // Debug.LogWarning($"Tentacle retracted with food, but agent didn't consume it yet.");
                    }
                    currentTargetGO = null; // Сбрасываем цель
                }
                break;
        }
    }

    void PerformStateActions(Vector2 targetDirSignal) {
        float targetExtension = 0f;
        Vector3 tipTargetWorldPos = rootTransform.position; // Куда стремится кончик щупальца

        if (currentState == State.Targeting || currentState == State.Extending) {
            targetExtension = 1f; // Пытаемся полностью вытянуться к цели или в указанном направлении
            if (currentTargetGO != null) {
                tipTargetWorldPos = currentTargetGO.transform.position;
            } else { // Если нет конкретной цели, используем направление от НС
                Vector3 worldTargetDir = agentBodyTransform.TransformDirection(targetDirSignal);
                tipTargetWorldPos = rootTransform.position + worldTargetDir * GetMaxPossibleLength();
            }
        } else if (currentState == State.GraspingFood) {
            targetExtension = currentExtensionRatio; // Остаемся на текущей длине или чуть втягиваем к "рту"
            // Можно добавить логику поднесения ко рту (цель - точка у рта агента)
            tipTargetWorldPos = agentBodyTransform.position + agentBodyTransform.up * 0.3f; // Примерная точка "рта"
        } else if (currentState == State.Retracting) {
            targetExtension = 0f;
            tipTargetWorldPos = rootTransform.position; // Втягиваем к основанию
        }

        currentExtensionRatio = Mathf.MoveTowards(currentExtensionRatio, targetExtension,
                                                  extensionRetractionSpeed * Time.deltaTime);
        
        // Обновляем LineRenderer или меш на основе currentExtensionRatio и tipTargetWorldPos
        // Для LineRenderer:
        if (lineRenderer != null) {
             if (currentState == State.Idle && currentExtensionRatio < 0.01f) {
                 lineRenderer.enabled = false;
             } else {
                 lineRenderer.enabled = true;
                 Vector3 startPos = rootTransform.position;
                 Vector3 endPos = CalculateTipPosition(tipTargetWorldPos);
                 
                 // Простая прямая линия
                 // lineRenderer.positionCount = 2;
                 // lineRenderer.SetPosition(0, startPos);
                 // lineRenderer.SetPosition(1, endPos);

                 // Изогнутая линия (пример с одной промежуточной точкой)
                 int pointCount = lineRenderer.positionCount;
                 Vector3 controlPoint = startPos + (endPos - startPos) * 0.5f + rootTransform.right * (endPos - startPos).magnitude * 0.1f * Mathf.Sin(Time.time*2f + tentacleId); // Небольшой изгиб

                 for (int i=0; i < pointCount; ++i) {
                    float t = (float)i / (pointCount-1);
                    // Квадратичная кривая Безье
                    Vector3 p = (1-t)*(1-t)*startPos + 2*(1-t)*t*controlPoint + t*t*endPos;
                    lineRenderer.SetPosition(i, p);
                 }
             }
        }
    }

    Vector3 CalculateTipPosition(Vector3 worldTargetPos) {
        Vector3 directionToTarget = (worldTargetPos - rootTransform.position);
        float distToTarget = directionToTarget.magnitude;
        float currentMaxLength = GetMaxPossibleLength() * currentExtensionRatio;
        
        if (distToTarget > currentMaxLength) {
            return rootTransform.position + directionToTarget.normalized * currentMaxLength;
        }
        return worldTargetPos; // Можем достать до цели
    }

    bool CheckTargetReached() {
        if (currentTargetGO == null) return false;
        float distance = Vector3.Distance(CalculateTipPosition(currentTargetGO.transform.position), currentTargetGO.transform.position);
        // Учитываем размер цели (например, радиус коллайдера)
        Collider2D targetCollider = currentTargetGO.GetComponent<Collider2D>();
        float targetRadius = targetCollider != null ? (targetCollider.bounds.size.x + targetCollider.bounds.size.y) / 4f : 0.1f;
        return distance < targetRadius + 0.1f; // Небольшой допуск
    }

    void TryGraspTarget() {
        if (currentTargetGO == null || heldFoodItemComponent != null) return; // Уже что-то держим или нет цели

        Food food = currentTargetGO.GetComponent<Food>();
        if (food != null) {
            heldFoodItemComponent = food; // Запоминаем компонент
            Destroy(currentTargetGO); // Уничтожаем GameObject еды со сцены
            currentState = State.GraspingFood;
            if(FindFirstObjectByType<EventLogPanel>() != null)
                 FindFirstObjectByType<EventLogPanel>().AddLogMessage($"{agentBodyTransform.name} grasped {food.type} food.");
        }
    }
    
    float GetMaxPossibleLength() {
        return genome.baseGraspTentacleLength * genome.maxGraspTentacleLengthFactor;
    }

    public bool IsHoldingFoodItem() { return heldFoodItemComponent != null; }
    
    public Food RetrieveAndReleaseHeldFood() {
        Food itemToReturn = null;
        if (heldFoodItemComponent != null) {
            itemToReturn = heldFoodItemComponent; // Это ссылка на компонент, GameObject уже уничтожен
            heldFoodItemComponent = null; // Освобождаем "руку"
            currentState = State.Retracting; // Начинаем втягивать пустое щупальце
        }
        return itemToReturn; // Возвращаем только данные о еде
    }
    
    void UpdateVisuals() { // Вызывается из PerformStateActions или если нужно отдельно
        // Логика обновления LineRenderer уже в PerformStateActions
    }
    public bool IsBusy() { return currentState != State.Idle; }
}
