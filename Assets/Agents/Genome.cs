// /Users/user/Dev/Unity/Squid/Assets/Agents/Genome.cs
using UnityEngine;
using System.Collections.Generic;

[System.Serializable]
public class Genome
{
    // --- Neural Network Structure ---
    public int inputNodes;
    public int hiddenNodes;
    public int outputNodes;
    public List<float> nnWeights;

    // --- Physical Traits (Examples) ---
    [Header("Physical Traits")]
    public float mantleLength = 1f;
    public float mantleMaxDiameter = 0.4f;
    public Color mantleColor = Color.grey;

    public float baseSwimTentacleLength = 0.8f;
    public float swimTentacleThickness = 0.08f;
    // public const int numSwimTentacles = 8; // Константа

    public float baseGraspTentacleLength = 1.0f;
    public float maxGraspTentacleLengthFactor = 2.0f; // Множитель к базовой длине
    public float graspTentacleThickness = 0.05f;
    // public const int numGraspTentacles = 2; // Константа

    public float eyeSize = 0.15f;
    // public Color eyeColor = Color.black;

    // --- Metabolic & Lifespan Traits ---
    [Header("Metabolic Traits")]
    public float metabolismRateFactor = 1.5f; // Множитель к базовому расходу
    public float maxAge = 40f; // Секунд
    public float energyToReproduceThresholdFactor = 0.9f; // % от макс. энергии
    public float energyCostOfReproductionFactor = 0.4f; // % от макс. энергии

    // --- Behavioral Traits ---
    [Header("Behavioral Traits")]
    public float aggression = 0.2f; // 0-1
    public float foodPreference = 0.5f; // 0 (растения) - 1 (мясо)
    // public float cautionFactor = 0.5f; // Насколько осторожен


    public float fitness; // Оценка приспособленности

    // Конструктор для НС и дефолтных физических параметров
    public Genome(int inputs, int hiddens, int outputs)
    {
        inputNodes = inputs;
        hiddenNodes = hiddens;
        outputNodes = outputs;
        nnWeights = new List<float>();
        InitializeRandomNNWeights();
        // InitializeRandomPhysicalGenes(); // Вызывать явно после создания объекта, чтобы не перезаписывать при копировании
        fitness = 0f;
    }

    // Копирующий конструктор
    public Genome(Genome parentGenome)
    {
        inputNodes = parentGenome.inputNodes;
        hiddenNodes = parentGenome.hiddenNodes;
        outputNodes = parentGenome.outputNodes;
        nnWeights = new List<float>(parentGenome.nnWeights);

        // Копирование всех остальных генов
        mantleLength = parentGenome.mantleLength;
        mantleMaxDiameter = parentGenome.mantleMaxDiameter;
        mantleColor = parentGenome.mantleColor;
        baseSwimTentacleLength = parentGenome.baseSwimTentacleLength;
        swimTentacleThickness = parentGenome.swimTentacleThickness;
        baseGraspTentacleLength = parentGenome.baseGraspTentacleLength;
        maxGraspTentacleLengthFactor = parentGenome.maxGraspTentacleLengthFactor;
        graspTentacleThickness = parentGenome.graspTentacleThickness;
        eyeSize = parentGenome.eyeSize;
        metabolismRateFactor = parentGenome.metabolismRateFactor;
        maxAge = parentGenome.maxAge;
        energyToReproduceThresholdFactor = parentGenome.energyToReproduceThresholdFactor;
        energyCostOfReproductionFactor = parentGenome.energyCostOfReproductionFactor;
        aggression = parentGenome.aggression;
        foodPreference = parentGenome.foodPreference;
        
        fitness = 0f; // Фитнес не наследуется напрямую
    }

    void InitializeRandomNNWeights()
    {
        int expectedWeights = (inputNodes * hiddenNodes) + hiddenNodes + (hiddenNodes * outputNodes) + outputNodes;
        if (nnWeights.Count != expectedWeights && nnWeights.Count != 0) { // Если уже есть веса, но не то кол-во
            Debug.LogWarning($"NNWeights count ({nnWeights.Count}) differs from expected ({expectedWeights}). Clearing and reinitializing.");
            nnWeights.Clear();
        }
        if(nnWeights.Count == 0) { // Только если список пуст
            for (int i = 0; i < expectedWeights; i++)
            {
                nnWeights.Add(Random.Range(-1f, 1f));
            }
        }
    }

    // Метод для инициализации физических генов случайными значениями (вызывать для первого поколения)
    public void InitializeRandomPhysicalGenes()
    {
        mantleLength = Random.Range(0.7f, 1.5f);
        mantleMaxDiameter = Random.Range(0.3f, 0.7f) * mantleLength * 0.5f; // Диаметр зависит от длины
        mantleColor = new Color(Random.value, Random.value, Random.value, 1f);

        baseSwimTentacleLength = Random.Range(0.4f, 0.8f) * mantleLength;
        swimTentacleThickness = Random.Range(0.05f, 0.1f) * mantleMaxDiameter * 0.3f;

        baseGraspTentacleLength = Random.Range(0.6f, 1.2f) * mantleLength;
        maxGraspTentacleLengthFactor = Random.Range(1.5f, 3.5f);
        graspTentacleThickness = Random.Range(0.03f, 0.08f) * mantleMaxDiameter * 0.25f;

        eyeSize = Random.Range(0.1f, 0.2f) * mantleMaxDiameter;
        metabolismRateFactor = Random.Range(0.8f, 1.3f);
        maxAge = Random.Range(60f, 180f);
        energyToReproduceThresholdFactor = Random.Range(0.6f, 0.85f);
        energyCostOfReproductionFactor = Random.Range(0.3f, 0.5f);

        aggression = Random.Range(0.05f, 0.6f);
        foodPreference = Random.value; // 0 до 1
    }
}
