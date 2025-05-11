// /Users/user/Dev/Unity/Squid/Assets/Agents/SquidBrain.cs
using UnityEngine;
using System.Collections.Generic;

public class SquidBrain : MonoBehaviour
{
    private Genome genome;
    private NeuralNetwork nn;
    private Transform agentTransform;

    // Структура выходов НС БЕЗ хватательных щупалец
    public struct BrainOutput
    {
        public float moveForward;
        public float turn;
        public bool shouldEat;
        public bool shouldReproduce;
        // Выходы для хватательных щупалец УДАЛЕНЫ
    }

    public void Initialize(Genome agentGenome)
    {
        if (agentGenome == null) {
            Debug.LogError($"SquidBrain on {gameObject.name} initialized with null genome! Disabling.");
            enabled = false;
            return;
        }
        this.genome = agentGenome;
        this.nn = new NeuralNetwork(genome); // NeuralNetwork конструктор должен быть готов к genome.outputNodes
        this.agentTransform = transform;
    }

    public BrainOutput ProcessInputs(List<float> inputs)
    {
        if (nn == null || !nn.IsInitializedProperly()) { // Добавил проверку IsInitializedProperly
            // Debug.LogError($"SquidBrain on {agentTransform.name}: Neural Network not properly initialized or null.");
            return new BrainOutput();
        }
        if (inputs == null || inputs.Count != genome.inputNodes) {
            // Debug.LogError($"SquidBrain on {agentTransform.name}: Inputs null or count mismatch. Expected {genome.inputNodes}, Got {inputs?.Count}.");
            return new BrainOutput();
        }

        float[] nnOutputsArray = nn.FeedForward(inputs.ToArray());
        BrainOutput output = new BrainOutput();
        int outIdx = 0;

        // --- Используем выходы НС для ВСЕХ действий ---
        // Движение (2 нейрона)
        if (nnOutputsArray.Length > outIdx) output.moveForward = nnOutputsArray[outIdx++]; else output.moveForward = 0;
        if (nnOutputsArray.Length > outIdx) output.turn = nnOutputsArray[outIdx++]; else output.turn = 0;

        // Выходы для хватательных щупалец УДАЛЕНЫ из этой секции

        // Другие действия (теперь идут сразу после движения)
        if (nnOutputsArray.Length > outIdx) output.shouldEat = nnOutputsArray[outIdx++] > 0.5f; else output.shouldEat = false;
        if (nnOutputsArray.Length > outIdx) output.shouldReproduce = nnOutputsArray[outIdx++] > 0.7f; else output.shouldReproduce = false;

        // Отладка выходов НС
        // if (Time.frameCount % 120 == 0 && agentTransform != null && agentTransform.name.EndsWith("0")) {
        //    Debug.Log($"{agentTransform.name} BrainOut: Fwd:{output.moveForward:F2} Turn:{output.turn:F2} Eat:{output.shouldEat} Repr:{output.shouldReproduce}");
        // }

        return output;
    }
}

// Класс NeuralNetwork (убедитесь, что он у вас есть и корректен)
public class NeuralNetwork
{
    private List<float> weights;
    private int numInputs, numHidden, numOutputs;
    private bool nnInitializedCorrectly = false;

    public bool IsInitializedProperly() => nnInitializedCorrectly;

    public NeuralNetwork(Genome genome)
    {
        if (genome == null) {
             Debug.LogError("NeuralNetwork constructor received a null Genome!");
             return;
        }
        // Проверка на валидность размеров НС из генома
        if (genome.inputNodes <=0 || genome.hiddenNodes <=0 || genome.outputNodes <=0) {
            Debug.LogError($"NeuralNetwork constructor: Invalid NN dimensions in Genome for agent. I:{genome.inputNodes} H:{genome.hiddenNodes} O:{genome.outputNodes}");
            return;
        }

        numInputs = genome.inputNodes;
        numHidden = genome.hiddenNodes;
        numOutputs = genome.outputNodes; // Это значение теперь должно быть меньше (например, 4)
        
        int expectedWeights = (numInputs * numHidden) + numHidden + (numHidden * numOutputs) + numOutputs;
        if (genome.nnWeights == null || genome.nnWeights.Count != expectedWeights)
        {
            Debug.LogWarning($"NeuralNetwork: NN Weights in Genome are null or count mismatch. Expected {expectedWeights}, Got {(genome.nnWeights == null ? "null" : genome.nnWeights.Count.ToString())}. Initializing new random weights for this NN instance. The Genome itself is NOT modified here.");
            weights = new List<float>();
            for(int i=0; i<expectedWeights; ++i) weights.Add(Random.Range(-1f, 1f));
        } else {
             weights = new List<float>(genome.nnWeights); // Копируем веса из генома
        }
        nnInitializedCorrectly = true;
    }

    public float[] FeedForward(float[] inputs)
    {
        if (!nnInitializedCorrectly) {
            Debug.LogError("NN FeedForward called on an incorrectly initialized NeuralNetwork instance.");
            return new float[numOutputs > 0 ? numOutputs : 1];
        }

        if (inputs.Length != numInputs)
        {
            Debug.LogError($"NN input size mismatch! Expected {numInputs}, Got {inputs.Length}. Cannot proceed.");
            return new float[numOutputs];
        }

        float[] hiddenOutputs = new float[numHidden];
        float[] finalOutputs = new float[numOutputs];
        int weightIndex = 0;

        // Входной -> Скрытый слой
        for (int i = 0; i < numHidden; i++)
        {
            float sum = 0;
            for (int j = 0; j < numInputs; j++)
            {
                if (weightIndex >= weights.Count) { Debug.LogError("NN weight index out of bounds (input to hidden layer weights)."); return finalOutputs; }
                sum += inputs[j] * weights[weightIndex++];
            }
            if (weightIndex >= weights.Count) { Debug.LogError("NN weight index out of bounds (hidden layer bias)."); return finalOutputs; }
            sum += weights[weightIndex++];
            hiddenOutputs[i] = Tanh(sum);
        }

        // Скрытый -> Выходной слой
        for (int i = 0; i < numOutputs; i++)
        {
            float sum = 0;
            for (int j = 0; j < numHidden; j++)
            {
                if (weightIndex >= weights.Count) { Debug.LogError("NN weight index out of bounds (hidden to output layer weights)."); return finalOutputs; }
                sum += hiddenOutputs[j] * weights[weightIndex++];
            }
            if (weightIndex >= weights.Count) { Debug.LogError("NN weight index out of bounds (output layer bias)."); return finalOutputs; }
            sum += weights[weightIndex++];
            finalOutputs[i] = Tanh(sum);
        }
        return finalOutputs;
    }

    private float Tanh(float x) { return (float)System.Math.Tanh(x); }
}
