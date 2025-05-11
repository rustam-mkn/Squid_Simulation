// /Users/user/Dev/Unity/Squid/Assets/Agents/SquidMetabolism.cs
using UnityEngine;
using System.Linq; // Для LastOrDefault()

public class SquidMetabolism : MonoBehaviour
{
    private Genome genome;
    private SimulationManager simManager;
    private SquidAgent agent;

    public float CurrentEnergy { get; private set; }
    public float Age { get; private set; }
    public float maxEnergyGeno { get; private set; }

    // Базовые значения, которые могут быть модифицированы геномом
    private float baseStartingEnergyFactor = 0.50f; // Начинаем с 50% от макс. энергии
    private float baseEnergyPerSecond = 0.8f;       // Базовый расход (можно настроить)
    
    private GeneticAlgorithmManager gaManager;

    public void Initialize(Genome agentGenome, SimulationManager manager, SquidAgent ownerAgent)
    {
        this.genome = agentGenome;
        this.simManager = manager;
        this.agent = ownerAgent;
        
        if (simManager != null) this.gaManager = simManager.gaManager;

        if (genome == null || simManager == null || agent == null ) {
            Debug.LogError($"SquidMetabolism on {ownerAgent?.name} initialized with null dependencies! G:{genome != null} SM:{simManager != null} A:{agent != null}. Disabling.");
            enabled = false; return;
        }

        // Максимальная энергия зависит от размера (длины мантии)
        maxEnergyGeno = 40f + 60f * Mathf.Clamp(genome.mantleLength, 0.5f, 2.0f); // Диапазон макс. энергии от 70 до 160
        CurrentEnergy = maxEnergyGeno * baseStartingEnergyFactor;
        Age = 0f;
    }

    public void UpdateMetabolism()
    {
        if (!enabled || genome == null) return;

        Age += Time.deltaTime;
        // Фитнес: время жизни + бонус за энергию + большой бонус за еду (добавляется в Eat)
        genome.fitness = Age + (CurrentEnergy / maxEnergyGeno) * 15f; // Бонус за энергию, множитель 15

        float energyDrainThisFrame = baseEnergyPerSecond * genome.metabolismRateFactor;
        if (agent.TryGetComponent<Rigidbody2D>(out var rb)) {
             energyDrainThisFrame += rb.linearVelocity.magnitude * 0.25f; // Увеличен расход на движение
        }
        // TODO: Добавить расход на активные щупальца, если нужно (например, если currentExtensionRatio > 0.1)

        CurrentEnergy -= energyDrainThisFrame * Time.deltaTime;

        if (CurrentEnergy <= 0 || Age >= genome.maxAge)
        {
            Die();
            return;
        }

        TryReproduce();
    }

    public void Eat(float energyValue, FoodType foodType)
    {
        if (!enabled) return;
        CurrentEnergy += energyValue;
        CurrentEnergy = Mathf.Clamp(CurrentEnergy, 0, maxEnergyGeno);
        genome.fitness += energyValue * 0.75f; // <<< ЗНАЧИТЕЛЬНО УВЕЛИЧЕН БОНУС К ФИТНЕСУ ЗА ЕДУ (0.75 от ценности еды)
        
        // EventLogPanel.Instance?.AddLogMessage($"{agent.name.Split('_').LastOrDefault()} ate {foodType}. E: {CurrentEnergy:F0}, Fit+={energyValue * 0.75f:F1}");
    }

    void TryReproduce()
    {
        if (!enabled || genome == null) return;

        float reproductionEnergyActualThreshold = maxEnergyGeno * genome.energyToReproduceThresholdFactor;
        float costOfReproductionActual = maxEnergyGeno * genome.energyCostOfReproductionFactor;

        if (CurrentEnergy < reproductionEnergyActualThreshold) {
            return;
        }

        float energyAfterReproduction = CurrentEnergy - costOfReproductionActual;
        if (energyAfterReproduction < maxEnergyGeno * 0.25f || energyAfterReproduction < 20f) // Должно остаться хотя бы 25% или 20 единиц
        {
            return;
        }
        
        CurrentEnergy -= costOfReproductionActual;
        genome.fitness += costOfReproductionActual * 0.5f; // Бонус к фитнесу за успешное размножение

        Genome offspringGenome = new Genome(genome);
        
        if (gaManager != null) {
            gaManager.Mutate(offspringGenome);
        } else {
            Debug.LogWarning("GAManager not found for mutation during reproduction on " + agent.name);
        }
        
        agent.ReportReproduction(offspringGenome);
    }

    void Die()
    {
        if (!enabled) return; // Предотвращаем многократный вызов, если уже умерли
        // Финальный фитнес уже должен быть накоплен в genome.fitness
        agent.ReportDeath();
        enabled = false;
    }
}
