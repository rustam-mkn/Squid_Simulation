using UnityEngine;

public class SquidMetabolism : MonoBehaviour
{
    private Genome genome;
    private SimulationManager simManager;
    private SquidAgent agent;

    public float CurrentEnergy { get; private set; }
    public float Age { get; private set; }
    public float maxEnergyGeno { get; private set; }

    private float baseStartingEnergyFactor = 0.7f;
    private float baseEnergyPerSecond = 2f; // Было 1.0f — увеличено

    private GeneticAlgorithmManager gaManager;

    // === Новое: кулдаун на размножение ===
    private float timeSinceLastReproduction = 0f;
    public float reproductionCooldown = 20f;

    public void Initialize(Genome agentGenome, SimulationManager manager, SquidAgent ownerAgent)
    {
        this.genome = agentGenome;
        this.simManager = manager;
        this.agent = ownerAgent;
        this.gaManager = manager.gaManager;

        if (genome == null || simManager == null || agent == null || gaManager == null) {
            Debug.LogError("SquidMetabolism initialized with null dependencies!");
            enabled = false; return;
        }

        maxEnergyGeno = 50f + 50f * Mathf.Clamp(genome.mantleLength, 0.5f, 2.0f);
        CurrentEnergy = maxEnergyGeno * baseStartingEnergyFactor;
        Age = 0f;
        timeSinceLastReproduction = reproductionCooldown; // Можно размножаться сразу, если хватает энергии
    }

    public void UpdateMetabolism()
    {
        if (!enabled) return;

        Age += Time.deltaTime;
        timeSinceLastReproduction += Time.deltaTime;

        // === Фитнес ===
        genome.fitness = Age;

        // === Расход энергии ===
        float energyDrainThisFrame = baseEnergyPerSecond * genome.metabolismRateFactor;

        if (agent.TryGetComponent<Rigidbody2D>(out var rb)) {
            energyDrainThisFrame += rb.linearVelocity.magnitude * 0.3f; // Усилен расход на движение
        }

        CurrentEnergy -= energyDrainThisFrame * Time.deltaTime;

        // === Смерть ===
        if (CurrentEnergy <= 0 || Age >= genome.maxAge)
        {
            Die();
            return;
        }

        // === Размножение ===
        float reproductionThreshold = maxEnergyGeno * genome.energyToReproduceThresholdFactor;
        if (CurrentEnergy >= reproductionThreshold && timeSinceLastReproduction >= reproductionCooldown)
        {
            TryReproduce();
        }
    }

    public void Eat(float energyValue, FoodType foodType)
    {
        CurrentEnergy += energyValue;
        CurrentEnergy = Mathf.Clamp(CurrentEnergy, 0, maxEnergyGeno);
        genome.fitness += energyValue * 0.2f; // Бонус за еду
    }

    void TryReproduce()
    {
        float costOfReproduction = maxEnergyGeno * genome.energyCostOfReproductionFactor;
        if (CurrentEnergy < costOfReproduction + 10f) return;

        CurrentEnergy -= costOfReproduction;
        genome.fitness += costOfReproduction * 0.3f;

        Genome offspringGenome = new Genome(genome);

        if (gaManager != null)
        {
            gaManager.Mutate(offspringGenome);
        }
        else
        {
            Debug.LogWarning("GAManager not found for mutation during reproduction.");
        }

        agent.ReportReproduction(offspringGenome);
        timeSinceLastReproduction = 0f; // Сброс кулдауна
    }

    void Die()
    {
        agent.ReportDeath();
        enabled = false;
    }
}
