// /Users/user/Dev/Unity/Squid/Assets/Agents/Genome.cs
using UnityEngine;
using System.Collections.Generic;

[System.Serializable]
public class Genome
{
    public int inputNodes;
    public int hiddenNodes;
    public int outputNodes;
    public List<float> nnWeights;

    [Header("Physical Traits")]
    public float mantleLength = 1f;
    public float mantleMaxDiameter = 0.4f;
    public Color mantleColor = Color.grey;
    public float baseSwimTentacleLength = 0.8f; // Только плавательные щупальца
    public float swimTentacleThickness = 0.08f;
    public float eyeSize = 0.15f;

    [Header("Movement Genes")]
    public float baseTurnTorqueFactor = 1.0f;
    public float baseMoveForceFactor = 1.0f;

    [Header("Metabolic Traits")]
    public float metabolismRateFactor = 1.0f;
    public float maxAge = 100f;
    public float energyToReproduceThresholdFactor = 0.90f;
    public float energyCostOfReproductionFactor = 0.60f;

    [Header("Behavioral Traits")]
    public float aggression = 0.2f;
    public float foodPreference = 0.5f;

    public float fitness;

    public Genome(int inputs, int hiddens, int outputs)
    {
        inputNodes = inputs;
        hiddenNodes = hiddens;
        outputNodes = outputs;
        nnWeights = new List<float>();
        InitializeRandomNNWeights();
        fitness = 0f;
    }

    public Genome(Genome parentGenome)
    {
        inputNodes = parentGenome.inputNodes;
        hiddenNodes = parentGenome.hiddenNodes;
        outputNodes = parentGenome.outputNodes;
        nnWeights = new List<float>(parentGenome.nnWeights);
        
        mantleLength = parentGenome.mantleLength;
        mantleMaxDiameter = parentGenome.mantleMaxDiameter;
        mantleColor = parentGenome.mantleColor;
        baseSwimTentacleLength = parentGenome.baseSwimTentacleLength;
        swimTentacleThickness = parentGenome.swimTentacleThickness;
        eyeSize = parentGenome.eyeSize;

        baseTurnTorqueFactor = parentGenome.baseTurnTorqueFactor;
        baseMoveForceFactor = parentGenome.baseMoveForceFactor;
        
        metabolismRateFactor = parentGenome.metabolismRateFactor;
        maxAge = parentGenome.maxAge;
        energyToReproduceThresholdFactor = parentGenome.energyToReproduceThresholdFactor;
        energyCostOfReproductionFactor = parentGenome.energyCostOfReproductionFactor;
        
        aggression = parentGenome.aggression;
        foodPreference = parentGenome.foodPreference;
        
        fitness = 0f;
    }

    void InitializeRandomNNWeights()
    {
        int expectedWeights = (inputNodes * hiddenNodes) + hiddenNodes + (hiddenNodes * outputNodes) + outputNodes;
        if (nnWeights.Count != expectedWeights && nnWeights.Count != 0) {
            nnWeights.Clear();
        }
        if (nnWeights.Count == 0) {
            for (int i = 0; i < expectedWeights; i++)
                nnWeights.Add(Random.Range(-1f, 1f));
        }
    }

    public void InitializeRandomPhysicalGenes()
    {
        mantleLength = Random.Range(0.6f, 1.2f);
        mantleMaxDiameter = Random.Range(0.35f, 0.55f) * mantleLength;
        mantleColor = new Color(Random.value, Random.value, Random.value, 1f);

        baseSwimTentacleLength = Random.Range(0.6f, 1.0f) * mantleLength;
        swimTentacleThickness = Random.Range(0.05f, 0.08f) * mantleMaxDiameter;
        
        eyeSize = Random.Range(0.15f, 0.25f) * mantleMaxDiameter;
        eyeSize = Mathf.Max(eyeSize, 0.04f);

        baseTurnTorqueFactor = Random.Range(0.7f, 1.5f);
        baseMoveForceFactor = Random.Range(0.7f, 1.5f);

        metabolismRateFactor = Random.Range(0.8f, 1.2f);
        maxAge = Random.Range(60f, 120f);

        energyToReproduceThresholdFactor = Random.Range(0.80f, 0.95f);
        energyCostOfReproductionFactor = Random.Range(0.50f, 0.65f);

        aggression = Random.Range(0.1f, 0.3f);
        foodPreference = Random.Range(0.4f, 0.6f);
    }
}
