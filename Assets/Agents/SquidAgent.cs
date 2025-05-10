// /Users/user/Dev/Unity/Squid/Assets/Agents/SquidAgent.cs
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))] // Добавим сразу
[RequireComponent(typeof(SquidBrain), typeof(SquidSenses), typeof(SquidMovement))]
[RequireComponent(typeof(SquidMetabolism), typeof(SquidMeshGenerator), typeof(TentacleController))]
public class SquidAgent : MonoBehaviour
{
    public Genome genome { get; private set; } // Сделал private set для большей инкапсуляции
    private SimulationManager simManager;

    // Компоненты кальмара
    private SquidBrain brain;
    private SquidSenses senses;
    private SquidMovement movement;
    private SquidMetabolism metabolism;
    private SquidMeshGenerator meshGenerator;
    private TentacleController tentacleController;
    private Rigidbody2D rb;


    public bool isInitialized { get; private set; } = false;

    void Awake() // Получаем компоненты в Awake, чтобы они были доступны для Initialize
    {
        brain = GetComponent<SquidBrain>();
        senses = GetComponent<SquidSenses>();
        movement = GetComponent<SquidMovement>();
        metabolism = GetComponent<SquidMetabolism>();
        meshGenerator = GetComponent<SquidMeshGenerator>();
        tentacleController = GetComponent<TentacleController>();
        rb = GetComponent<Rigidbody2D>();

        if (brain == null || senses == null || movement == null || metabolism == null || meshGenerator == null || tentacleController == null || rb == null)
        {
            Debug.LogError($"SquidAgent {gameObject.name} is missing one or more required components in Awake!");
            enabled = false; // Отключаем, если что-то не так
        }
    }


    public void Initialize(Genome agentGenome, SimulationManager manager)
    {
        if (agentGenome == null || manager == null) {
            Debug.LogError($"Initialization failed for {gameObject.name}: Genome or SimManager is null.");
            Destroy(gameObject); // Уничтожаем, если не можем инициализировать
            return;
        }

        this.genome = agentGenome;
        this.simManager = manager;
        
        // Инициализация компонентов с передачей генома и менеджера
        // Порядок может быть важен, если компоненты зависят друг от друга при инициализации

        // Сначала те, кто не зависит от других или нужен другим для инициализации мешей
        meshGenerator.Initialize(this.transform); // Передаем transform для создания дочерних объектов мешей
        meshGenerator.GenerateInitialMeshes(genome); // Генерируем меши ДО инициализации контроллеров, которые могут их использовать

        // Затем остальные
        brain.Initialize(genome);
        senses.Initialize(genome, transform); // Передаем transform для расчетов направления
        movement.Initialize(genome, rb);
        metabolism.Initialize(genome, simManager, this);
        tentacleController.Initialize(genome, this.transform, meshGenerator); // После генерации мешей щупалец

        isInitialized = true;
        gameObject.name = "Squid_" + GetInstanceID();
    }

    void FixedUpdate() // Используем FixedUpdate для физики и основной логики, если движение физическое
    {
        if (!isInitialized || simManager == null || !simManager.isRunning || Time.timeScale == 0f) return;

        // 1. Сбор сенсорной информации
        var sensoryInput = senses.GatherSenses();
        
        // 2. Принятие решения Нейросетью
        var brainOutput = brain.ProcessInputs(sensoryInput);

        // 3. Выполнение действий
        movement.ExecuteMovement(brainOutput);
        tentacleController.UpdateAllTentacles(brainOutput, senses.GetTargetInfo());

        // 4. Метаболизм
        metabolism.UpdateMetabolism(); // Расход энергии, старение, проверка на смерть/размножение

        // 5. Взаимодействие с едой/атака
        HandleInteractions(brainOutput);

        // 6. Обновление Визуализации (если меши динамические)
        // meshGenerator.UpdateMeshes(genome, brainOutput); // Раскомментировать, если есть динамическое обновление мешей
    }

    void HandleInteractions(SquidBrain.BrainOutput output)
    {
        if (output.shouldEat && tentacleController.IsHoldingFood())
        {
            Food foodItem = tentacleController.ConsumeHeldFood(); // Щупальце отдает еду
            if (foodItem != null) // foodItem уже уничтожен в TentacleController
            {
                metabolism.Eat(foodItem.energyValue, foodItem.type); // Передаем значение и тип
            }
        }
        
        // TODO: Логика атаки
        // if (output.graspTentacle1TryAttack && tentacleController.CanTentacleAttack(0)) {
        //     SquidAgent targetAgent = tentacleController.AttemptAttack(0);
        //     if (targetAgent != null) {
        //         targetAgent.GetComponent<SquidMetabolism>().TakeDamage(genome.attackPower);
        //     }
        // }
    }
    
    public void ReportDeath() // Вызывается из SquidMetabolism
    {
        if (!isInitialized) return; // Избегаем двойного вызова или вызова на неинициализированном
        isInitialized = false; // Предотвращаем дальнейшие действия

        // Фитнес должен быть обновлен в genome до этого момента (в SquidMetabolism)
        simManager.ReportAgentDeath(this, new Genome(genome)); // Передаем копию генома с финальным фитнесом
        Destroy(gameObject);
    }

    public void ReportReproduction(Genome offspringGenome) // Вызывается из SquidMetabolism
    {
        simManager.ReportAgentReproduction(this, offspringGenome);
    }
}
