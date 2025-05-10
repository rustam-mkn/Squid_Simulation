// /Users/user/Dev/Unity/Squid/Assets/Agents/SquidBrain.cs
using UnityEngine;
using System.Collections.Generic;

public class SquidBrain : MonoBehaviour
{
    private Genome genome;
    private NeuralNetwork nn;

    // Структура для типизированного вывода НС
    public struct BrainOutput
    {
        // Движение
        public float moveForward;    // -1 (назад) to 1 (вперед)
        public float turn;           // -1 (влево) to 1 (вправо)
        // public float moveIntensity;  // 0 to 1 (если нужно отдельное управление силой)

        // Хватательные щупальца (0 - левое, 1 - правое)
        public Vector2 graspTentacleTargetDir0; // Нормализованное направление от кальмара
        public float graspTentacleExtend0;      // 0 (втянуто) to 1 (макс вытянуто)
        public bool graspTentacleTryGrasp0;
        public bool graspTentacleTryAttack0; // TODO: Для атаки

        public Vector2 graspTentacleTargetDir1;
        public float graspTentacleExtend1;
        public bool graspTentacleTryGrasp1;
        public bool graspTentacleTryAttack1; // TODO: Для атаки
        
        public bool shouldEat;
        public bool shouldReproduce;
    }

    public void Initialize(Genome agentGenome)
    {
        if (agentGenome == null) {
            Debug.LogError("SquidBrain initialized with null genome!");
            enabled = false; return;
        }
        this.genome = agentGenome;
        this.nn = new NeuralNetwork(genome); // Передаем весь геном, НС сама возьмет нужные параметры
    }

    public BrainOutput ProcessInputs(List<float> inputs)
    {
        if (nn == null)
        {
            Debug.LogError("Neural Network in SquidBrain is not initialized!");
            return new BrainOutput();
        }
        if (inputs == null) {
            Debug.LogError("SquidBrain received null inputs!");
            return new BrainOutput();
        }


        float[] nnOutputsArray = nn.FeedForward(inputs.ToArray());
        BrainOutput output = new BrainOutput();

        // Распределение выходов НС по полям BrainOutput
        // Это должно строго соответствовать genome.outputNodes и их назначению
        int outIdx = 0;
        // Движение (2 нейрона)
        if (nnOutputsArray.Length > outIdx) output.moveForward = nnOutputsArray[outIdx++];
        if (nnOutputsArray.Length > outIdx) output.turn = nnOutputsArray[outIdx++];
        // output.moveIntensity = Mathf.Clamp01((output.moveForward + 1f)/2f); // Пример: интенсивность от движения вперед

        // Щупальце 0 (3 или 4 нейрона)
        if (nnOutputsArray.Length > outIdx + 2) { // dirX, dirY, extend
            output.graspTentacleTargetDir0 = new Vector2(nnOutputsArray[outIdx++], nnOutputsArray[outIdx++]).normalized;
            output.graspTentacleExtend0 = Mathf.Clamp01((nnOutputsArray[outIdx++] + 1f) / 2f); // Tanh -1..1 -> 0..1
        }
        if (nnOutputsArray.Length > outIdx) output.graspTentacleTryGrasp0 = nnOutputsArray[outIdx++] > 0.0f; // Порог для Tanh
        // if (nnOutputsArray.Length > outIdx) output.graspTentacleTryAttack0 = nnOutputsArray[outIdx++] > 0.5f;


        // Щупальце 1 (3 или 4 нейрона)
         if (nnOutputsArray.Length > outIdx + 2) {
            output.graspTentacleTargetDir1 = new Vector2(nnOutputsArray[outIdx++], nnOutputsArray[outIdx++]).normalized;
            output.graspTentacleExtend1 = Mathf.Clamp01((nnOutputsArray[outIdx++] + 1f) / 2f);
        }
        if (nnOutputsArray.Length > outIdx) output.graspTentacleTryGrasp1 = nnOutputsArray[outIdx++] > 0.0f;
        // if (nnOutputsArray.Length > outIdx) output.graspTentacleTryAttack1 = nnOutputsArray[outIdx++] > 0.5f;

        // Другие действия
        if (nnOutputsArray.Length > outIdx) output.shouldEat = nnOutputsArray[outIdx++] > 0.5f;
        if (nnOutputsArray.Length > outIdx) output.shouldReproduce = nnOutputsArray[outIdx++] > 0.7f; // Более высокий порог для размножения

        return output;
    }
}

// NeuralNetwork класс (оставляем здесь для простоты, можно вынести)
public class NeuralNetwork
{
    private List<float> weights;
    private int numInputs, numHidden, numOutputs;

    public NeuralNetwork(Genome genome) // Принимает весь геном
    {
        if (genome == null) {
             Debug.LogError("NeuralNetwork created with null Genome!");
             return;
        }
        numInputs = genome.inputNodes;
        numHidden = genome.hiddenNodes;
        numOutputs = genome.outputNodes;
        
        // Проверка и инициализация весов
        int expectedWeights = (numInputs * numHidden) + numHidden + (numHidden * numOutputs) + numOutputs;
        if (genome.nnWeights == null || genome.nnWeights.Count != expectedWeights)
        {
            Debug.LogWarning($"NN Weight mismatch or null: Expected {expectedWeights}, Got {(genome.nnWeights == null ? "null" : genome.nnWeights.Count.ToString())}. Reinitializing weights for this NN instance.");
            weights = new List<float>();
            for(int i=0; i<expectedWeights; ++i) weights.Add(Random.Range(-1f, 1f));
            // Важно: это не меняет геном, только локальные веса этой НС. Геном должен быть корректен.
            // Если геном изначально неверен, он будет таким для всех НС, использующих его.
        } else {
             weights = new List<float>(genome.nnWeights); // Копируем веса из генома
        }
    }

    public float[] FeedForward(float[] inputs)
    {
        if (weights == null || weights.Count == 0) {
            Debug.LogError("NN FeedForward called with uninitialized weights!");
            return new float[numOutputs];
        }
        if (inputs.Length != numInputs)
        {
            Debug.LogError($"NN input size mismatch! Expected {numInputs}, Got {inputs.Length}");
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
                if (weightIndex >= weights.Count) { Debug.LogError("NN weight index out of bounds (input to hidden)"); return finalOutputs; }
                sum += inputs[j] * weights[weightIndex++];
            }
            if (weightIndex >= weights.Count) { Debug.LogError("NN weight index out of bounds (hidden bias)"); return finalOutputs; }
            sum += weights[weightIndex++]; // Смещение (bias)
            hiddenOutputs[i] = Tanh(sum);
        }

        // Скрытый -> Выходной слой
        for (int i = 0; i < numOutputs; i++)
        {
            float sum = 0;
            for (int j = 0; j < numHidden; j++)
            {
                if (weightIndex >= weights.Count) { Debug.LogError("NN weight index out of bounds (hidden to output)"); return finalOutputs; }
                sum += hiddenOutputs[j] * weights[weightIndex++];
            }
            if (weightIndex >= weights.Count) { Debug.LogError("NN weight index out of bounds (output bias)"); return finalOutputs; }
            sum += weights[weightIndex++]; // Смещение (bias)
            finalOutputs[i] = Tanh(sum);
        }
        return finalOutputs;
    }

    private float Tanh(float x) { return (float)System.Math.Tanh(x); }
}
