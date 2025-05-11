// /Users/user/Dev/Unity/Squid/Assets/Core/GeneticAlgorithmManager.cs
using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class GeneticAlgorithmManager : MonoBehaviour
{
    [Header("GA Parameters")]
    public float mutationRate = 0.05f;
    public float mutationAmount = 0.1f;
    public int tournamentSize = 3;
    public int elitismCount = 1;

    private StatisticsManager statsManager;

    void Start() {
        statsManager = FindFirstObjectByType<StatisticsManager>();
    }

    public List<Genome> EvolvePopulation(List<Genome> parentGenomes)
    {
        if (statsManager != null) statsManager.RecordMaxFitness(parentGenomes);

        List<Genome> newPopulation = new List<Genome>();
        int populationSize = parentGenomes.Count;

        if (populationSize == 0)
        {
            Debug.LogWarning("Parent population for GA is empty!");
            return newPopulation;
        }

        List<Genome> sortedParents = parentGenomes.OrderByDescending(g => g.fitness).ToList();

        for (int i = 0; i < Mathf.Min(elitismCount, sortedParents.Count); i++)
        {
            newPopulation.Add(new Genome(sortedParents[i]));
        }

        while (newPopulation.Count < populationSize)
        {
            Genome parent1 = TournamentSelection(sortedParents);
            Genome parent2 = TournamentSelection(sortedParents);

            Genome offspring = Crossover(parent1, parent2);
            Mutate(offspring);

            newPopulation.Add(offspring);
        }
        Debug.Log($"GA: Evolved {newPopulation.Count} new genomes.");
        return newPopulation;
    }

    Genome TournamentSelection(List<Genome> population)
    {
        if (population == null || population.Count == 0) {
             Debug.LogError("TournamentSelection called with empty population!");
             SimulationManager sm = FindFirstObjectByType<SimulationManager>();
             // Возвращаем геном с корректными размерами НС, если sm найден
             if (sm != null) return new Genome(sm.numInputNodes, sm.numHiddenNodes, sm.numOutputNodes);
             else return new Genome(1,1,1); // Абсолютный fallback, если все плохо
        }

        Genome bestInTournament = population[Random.Range(0, population.Count)];
        for (int i = 1; i < tournamentSize; i++)
        {
            Genome candidate = population[Random.Range(0, population.Count)];
            if (candidate.fitness > bestInTournament.fitness)
            {
                bestInTournament = candidate;
            }
        }
        return new Genome(bestInTournament);
    }

    Genome Crossover(Genome parent1, Genome parent2)
    {
        Genome offspring = new Genome(parent1);
        
        if (parent1.nnWeights.Count == parent2.nnWeights.Count) {
            int crossoverPointWeights = Random.Range(0, parent1.nnWeights.Count);
            for (int i = crossoverPointWeights; i < parent1.nnWeights.Count; i++)
            {
                offspring.nnWeights[i] = parent2.nnWeights[i];
            }
        } else {
            Debug.LogWarning("NN weights count mismatch during crossover. Offspring inherits parent1's weights fully.");
        }

        if (Random.value < 0.5f) offspring.mantleLength = parent2.mantleLength;
        offspring.mantleMaxDiameter = (parent1.mantleMaxDiameter + parent2.mantleMaxDiameter) / 2f;
        if (Random.value < 0.5f) offspring.mantleColor = parent2.mantleColor;
        
        if (Random.value < 0.5f) offspring.baseSwimTentacleLength = parent2.baseSwimTentacleLength;
        offspring.swimTentacleThickness = (parent1.swimTentacleThickness + parent2.swimTentacleThickness) / 2f;
        
        // --- УДАЛЕНЫ СТРОКИ ДЛЯ ХВАТАТЕЛЬНЫХ ЩУПАЛЕЦ ---
        // offspring.baseGraspTentacleLength = (parent1.baseGraspTentacleLength + parent2.baseGraspTentacleLength) / 2f;
        // if (Random.value < 0.5f) offspring.maxGraspTentacleLengthFactor = parent2.maxGraspTentacleLengthFactor;
        // if (Random.value < 0.5f) offspring.graspTentacleThickness = parent2.graspTentacleThickness;
        // ---------------------------------------------
        
        offspring.eyeSize = (parent1.eyeSize + parent2.eyeSize) / 2f;
        if (Random.value < 0.5f) offspring.baseTurnTorqueFactor = parent2.baseTurnTorqueFactor; // Добавлено скрещивание для новых генов
        if (Random.value < 0.5f) offspring.baseMoveForceFactor = parent2.baseMoveForceFactor;   // Добавлено скрещивание для новых генов

        if (Random.value < 0.5f) offspring.metabolismRateFactor = parent2.metabolismRateFactor;
        if (Random.value < 0.5f) offspring.maxAge = parent2.maxAge;
        
        offspring.aggression = (parent1.aggression + parent2.aggression) / 2f;
        if (Random.value < 0.5f) offspring.foodPreference = parent2.foodPreference;

        return offspring;
    }

    public void Mutate(Genome genome)
    {
        for (int i = 0; i < genome.nnWeights.Count; i++)
        {
            if (Random.value < mutationRate)
            {
                genome.nnWeights[i] += Random.Range(-mutationAmount, mutationAmount);
                genome.nnWeights[i] = Mathf.Clamp(genome.nnWeights[i], -1f, 1f);
            }
        }

        if (Random.value < mutationRate) genome.mantleLength += Random.Range(-mutationAmount * 0.2f, mutationAmount * 0.2f);
        genome.mantleLength = Mathf.Max(0.2f, genome.mantleLength);

        if (Random.value < mutationRate) genome.mantleMaxDiameter += Random.Range(-mutationAmount * 0.1f, mutationAmount * 0.1f);
        genome.mantleMaxDiameter = Mathf.Max(0.1f, genome.mantleMaxDiameter);
        
        if (Random.value < mutationRate) genome.baseSwimTentacleLength += Random.Range(-mutationAmount * 0.15f, mutationAmount * 0.15f);
        genome.baseSwimTentacleLength = Mathf.Max(0.1f, genome.baseSwimTentacleLength);

        if (Random.value < mutationRate) genome.swimTentacleThickness += Random.Range(-mutationAmount * 0.05f, mutationAmount * 0.05f);
        genome.swimTentacleThickness = Mathf.Max(0.01f, genome.swimTentacleThickness);

        // --- УДАЛЕНЫ СТРОКИ ДЛЯ ХВАТАТЕЛЬНЫХ ЩУПАЛЕЦ ---
        // if (Random.value < mutationRate) genome.baseGraspTentacleLength += Random.Range(-mutationAmount * 0.1f, mutationAmount * 0.1f);
        // genome.baseGraspTentacleLength = Mathf.Max(0.1f, genome.baseGraspTentacleLength);
        // if (Random.value < mutationRate) genome.maxGraspTentacleLengthFactor += Random.Range(-mutationAmount * 0.1f, mutationAmount * 0.1f);
        // genome.maxGraspTentacleLengthFactor = Mathf.Clamp(genome.maxGraspTentacleLengthFactor, 1f, 4f);
        // if (Random.value < mutationRate) genome.graspTentacleThickness += Random.Range(-mutationAmount * 0.02f, mutationAmount * 0.02f);
        // genome.graspTentacleThickness = Mathf.Max(0.01f, genome.graspTentacleThickness);
        // ---------------------------------------------

        if (Random.value < mutationRate) genome.eyeSize += Random.Range(-mutationAmount * 0.05f, mutationAmount * 0.05f);
        genome.eyeSize = Mathf.Max(0.05f, genome.eyeSize);

        if (Random.value < mutationRate) genome.baseTurnTorqueFactor += Random.Range(-mutationAmount * 0.2f, mutationAmount * 0.2f); // Добавлена мутация
        genome.baseTurnTorqueFactor = Mathf.Clamp(genome.baseTurnTorqueFactor, 0.1f, 3f);
        if (Random.value < mutationRate) genome.baseMoveForceFactor += Random.Range(-mutationAmount * 0.2f, mutationAmount * 0.2f); // Добавлена мутация
        genome.baseMoveForceFactor = Mathf.Clamp(genome.baseMoveForceFactor, 0.1f, 3f);


        if (Random.value < mutationRate)
            genome.mantleColor = new Color(
                Mathf.Clamp01(genome.mantleColor.r + Random.Range(-mutationAmount * 2f, mutationAmount * 2f)),
                Mathf.Clamp01(genome.mantleColor.g + Random.Range(-mutationAmount * 2f, mutationAmount * 2f)),
                Mathf.Clamp01(genome.mantleColor.b + Random.Range(-mutationAmount * 2f, mutationAmount * 2f))
            );

        if (Random.value < mutationRate) genome.metabolismRateFactor += Random.Range(-mutationAmount * 0.1f, mutationAmount * 0.1f);
        genome.metabolismRateFactor = Mathf.Clamp(genome.metabolismRateFactor, 0.5f, 2f);

        if (Random.value < mutationRate) genome.maxAge += Random.Range(-mutationAmount * 20f, mutationAmount * 20f);
        genome.maxAge = Mathf.Max(10f, genome.maxAge);

        if (Random.value < mutationRate) genome.energyToReproduceThresholdFactor += Random.Range(-mutationAmount * 0.1f, mutationAmount * 0.1f);
        genome.energyToReproduceThresholdFactor = Mathf.Clamp01(genome.energyToReproduceThresholdFactor);

        if (Random.value < mutationRate) genome.energyCostOfReproductionFactor += Random.Range(-mutationAmount * 0.1f, mutationAmount * 0.1f);
        genome.energyCostOfReproductionFactor = Mathf.Clamp01(genome.energyCostOfReproductionFactor);

        if (Random.value < mutationRate) genome.aggression += Random.Range(-mutationAmount * 0.2f, mutationAmount * 0.2f);
        genome.aggression = Mathf.Clamp01(genome.aggression);

        if (Random.value < mutationRate) genome.foodPreference += Random.Range(-mutationAmount * 0.3f, mutationAmount * 0.3f);
        genome.foodPreference = Mathf.Clamp01(genome.foodPreference);
    }
}
