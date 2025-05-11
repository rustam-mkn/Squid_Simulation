// /Users/user/Dev/Unity/Squid/Assets/Agents/SquidAgent.cs
using UnityEngine;
using System.Linq; // Для LastOrDefault()

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(SquidBrain), typeof(SquidSenses), typeof(SquidMovement))]
[RequireComponent(typeof(SquidMetabolism), typeof(SquidMeshGenerator), typeof(TentacleController))]
public class SquidAgent : MonoBehaviour
{
    public Genome genome { get; private set; }
    private SimulationManager simManager;

    // Компоненты кальмара
    private SquidBrain brain;
    private SquidSenses senses;
    private SquidMovement movement;
    private SquidMetabolism metabolism;
    private SquidMeshGenerator meshGenerator;
    private TentacleController tentacleController; // Остается для управления плавательными щупальцами
    private Rigidbody2D rb;

    // --- Коллайдер для "рта" ---
    private CircleCollider2D mouthCollider;
    // Параметры рта будут настраиваться в Initialize на основе генома
    // ---------------------------

    public bool isInitialized { get; private set; } = false;

    void Awake()
    {
        brain = GetComponent<SquidBrain>();
        senses = GetComponent<SquidSenses>();
        movement = GetComponent<SquidMovement>();
        metabolism = GetComponent<SquidMetabolism>();
        meshGenerator = GetComponent<SquidMeshGenerator>();
        tentacleController = GetComponent<TentacleController>(); // Получаем ссылку
        rb = GetComponent<Rigidbody2D>();

        bool componentsMissing = brain == null || senses == null || movement == null || metabolism == null ||
                                 meshGenerator == null || tentacleController == null || rb == null;
        if (componentsMissing)
        {
            Debug.LogError($"SquidAgent {gameObject.name} is missing one or more required components in Awake! Disabling.");
            enabled = false;
        }

        // Создаем коллайдер рта в Awake, чтобы он точно был
        mouthCollider = gameObject.AddComponent<CircleCollider2D>();
        mouthCollider.isTrigger = true;
        mouthCollider.radius = 0.1f; // Временное значение, будет перезаписано
        mouthCollider.offset = Vector2.zero; // Временное значение
    }

    public void Initialize(Genome agentGenome, SimulationManager manager)
    {
        if (!enabled) {
            Debug.LogError($"SquidAgent {gameObject.name} Initialize called but component is disabled. Destroying.");
            Destroy(gameObject);
            return;
        }
        if (agentGenome == null || manager == null) {
            Debug.LogError($"Initialization failed for {gameObject.name}: Genome or SimManager is null. Destroying.");
            Destroy(gameObject);
            return;
        }

        this.genome = agentGenome;
        this.simManager = manager;
        
        meshGenerator.Initialize(this.transform);
        meshGenerator.GenerateInitialMeshes(genome);

        brain.Initialize(genome);
        senses.Initialize(genome, transform);
        movement.Initialize(genome, rb);
        metabolism.Initialize(genome, simManager, this);
        // TentacleController инициализируется, но теперь управляет только плавательными щупальцами
        tentacleController.Initialize(genome, this.transform, meshGenerator);

        // Настройка коллайдера рта на основе генома
        float mouthRadiusFromGenome = Mathf.Max(genome.mantleMaxDiameter * 0.20f, 0.08f); // Уменьшил множитель для радиуса рта
        // Рот должен быть в "головной" части, где глаза и основание щупалец.
        // Если "вперед" для агента - это локальный -Y (как мы позиционировали глаза), то:
        Vector2 mouthOffsetFromGenome = new Vector2(0, -genome.mantleLength * 0.42f); // Чуть ниже глаз
        
        mouthCollider.radius = mouthRadiusFromGenome;
        mouthCollider.offset = mouthOffsetFromGenome;
        // Debug.Log($"{name} Mouth Collider Initialized: Radius={mouthCollider.radius:F2}, Offset={mouthCollider.offset}");

        isInitialized = true;
        gameObject.name = "Squid_" + GetInstanceID();
    }

    void FixedUpdate()
    {
        if (!isInitialized || simManager == null || !simManager.isRunning || Time.timeScale == 0f) return;

        var sensoryInput = senses.GatherSenses();
        var brainOutput = brain.ProcessInputs(sensoryInput);

        movement.ExecuteMovement(brainOutput);
        // UpdateAllTentacles теперь не нуждается в sensedTargets, так как нет хватательных щупалец, выбирающих цель
        tentacleController.UpdateAllTentacles(brainOutput, null);
        metabolism.UpdateMetabolism();
        
        // HandleInteractions(brainOutput); // ЭТОТ МЕТОД УДАЛЕН, поедание через OnTriggerEnter2D

        meshGenerator.UpdateDynamicMeshes(brainOutput); // РАСКОММЕНТИРОВАНО для анимации глаз
    }

    // HandleInteractions УДАЛЕН

    // Обработка столкновений для поедания через коллайдер "рта"
    void OnTriggerEnter2D(Collider2D otherCollider)
    {
        if (!isInitialized || !enabled || metabolism == null) return; 

        // Проверяем, что столкновение произошло именно с нашим "ртом" (mouthCollider)
        // И что другой объект - это еда.
        // otherCollider - это коллайдер ДРУГОГО объекта (еды).
        // Нам нужно, чтобы НАШ mouthCollider (который является триггером) столкнулся с коллайдером еды.
        // Событие OnTriggerEnter2D вызывается на скрипте, где есть Rigidbody И один из коллайдеров - триггер.
        // В нашем случае Rigidbody на SquidAgent, и mouthCollider - триггер.
        // Коллайдер еды тоже должен быть триггером, чтобы они могли "пройти" друг в друга для срабатывания.

        // Убедимся, что событие вызвано именно нашим mouthCollider, а не основным физическим коллайдером,
        // если бы он тоже был триггером (но он не должен).
        // Однако, Unity вызывает OnTriggerEnter2D на обоих объектах, если ОБА имеют Rigidbody и один из них триггер,
        // или если один имеет Rigidbody и триггер, а другой просто коллайдер.
        // В нашем случае: Агент (Rigidbody + триггер-рот), Еда (просто триггер-коллайдер).
        // Событие будет вызвано на Агенте. Нам нужно проверить, что otherCollider - это еда.

        Food food = otherCollider.gameObject.GetComponent<Food>();
        if (food != null)
        {
            // Debug.Log($"{name} MOUTH COLLIDER detected food: {food.type} from {otherCollider.name}");
            Food consumedFood = food.TryConsume(); // Пытаемся "потребить" (этот метод в Food.cs должен пометить еду как потребленную)
            if (consumedFood != null) // Если еда еще не была потреблена
            {
                // Debug.Log($"{name} about to eat {consumedFood.type} with energy {consumedFood.energyValue}");
                metabolism.Eat(consumedFood.energyValue, consumedFood.type);
                EventLogPanel.Instance?.AddLogMessage($"{name.Split('_').LastOrDefault()} ATE {consumedFood.type}. E: {metabolism.CurrentEnergy:F0}");
                Destroy(otherCollider.gameObject); // Уничтожаем объект еды
            } else {
                // Debug.Log($"{name} detected food {otherCollider.name}, but it was already consumed.");
            }
        }
    }
    
    public void ReportDeath()
    {
        if (!isInitialized) return;
        isInitialized = false;
        if (simManager != null) simManager.ReportAgentDeath(this, new Genome(genome));
        Destroy(gameObject);
    }

    public void ReportReproduction(Genome offspringGenome)
    {
        if (simManager != null) simManager.ReportAgentReproduction(this, offspringGenome);
    }

    void OnDrawGizmosSelected() {
        if (Application.isPlaying && isInitialized && mouthCollider != null && mouthCollider.enabled) { // Проверяем, что коллайдер активен
            Gizmos.color = Color.magenta;
            // Преобразуем смещение рта в мировые координаты
            Vector3 mouthWorldPosition = transform.TransformPoint(mouthCollider.offset);
            // Рисуем сферу, учитывая масштаб агента (если он есть и не равен 1)
            Gizmos.DrawWireSphere(mouthWorldPosition, mouthCollider.radius * Mathf.Max(transform.localScale.x, transform.localScale.y));
        }
    }
}
