// /Users/user/Dev/Unity/Squid/Assets/Agents/SquidMovement.cs
using UnityEngine;

public class SquidMovement : MonoBehaviour
{
    private Rigidbody2D rb;
    private Genome genome;

    [Header("Movement Parameters (Base Values)")]
    public float baseForwardForce = 10f; // Сила для движения вперед/назад
    public float turnTorque = 5f;      // Крутящий момент для поворота
    public float maxSpeedBase = 3f;    // Базовая максимальная скорость

    // Динамические параметры, зависящие от генома
    private float currentMoveForce;
    private float currentTurnTorque;
    private float currentMaxSpeed;
    
    // public Transform siphonTransform; // Для визуализации сифона, если будет

    public void Initialize(Genome agentGenome, Rigidbody2D rigidBody)
    {
        this.genome = agentGenome;
        this.rb = rigidBody;

        if (genome == null || rb == null) {
            Debug.LogError("SquidMovement initialized with null genome or Rigidbody2D!");
            enabled = false; return;
        }

        // Настройка сил/моментов на основе генома
        // Размер мантии может влиять на силу и макс. скорость
        float sizeFactor = Mathf.Clamp(genome.mantleLength, 0.5f, 2.0f); // Ограничиваем влияние размера
        currentMoveForce = baseForwardForce * sizeFactor;
        currentTurnTorque = turnTorque / Mathf.Sqrt(sizeFactor); // Большие медленнее поворачивают
        currentMaxSpeed = maxSpeedBase * Mathf.Sqrt(sizeFactor); // Большие могут быть чуть быстрее
        
        rb.linearDamping = 1f; // Линейное сопротивление для более плавного замедления
        rb.angularDamping = 2f; // Угловое сопротивление
    }

    public void ExecuteMovement(SquidBrain.BrainOutput brainOutput)
    {
        if (!enabled || rb == null) return;

        // Движение вперед/назад
        // brainOutput.moveForward: -1 (назад) to 1 (вперед)
        Vector2 forceDirection = transform.up; // "Вперед" для кальмара - это transform.up
        rb.AddForce(forceDirection * brainOutput.moveForward * currentMoveForce * Time.fixedDeltaTime, ForceMode2D.Force); // ForceMode2D.Force для постоянного ускорения

        // Поворот
        // brainOutput.turn: -1 (влево) to 1 (вправо)
        rb.AddTorque(-brainOutput.turn * currentTurnTorque * Time.fixedDeltaTime, ForceMode2D.Force); // Знак минус, т.к. AddTorque вращает по Z против часовой

        // Ограничение максимальной скорости
        if (rb.linearVelocity.magnitude > currentMaxSpeed)
        {
            rb.linearVelocity = rb.linearVelocity.normalized * currentMaxSpeed;
        }
        
        // TODO: Анимация сифона, если он есть и управляется отдельно
        // if (siphonTransform != null) {
        //    // Поворот сифона может быть основан на brainOutput.turn или отдельном выходе НС
        //    float siphonAngle = -brainOutput.turn * 45f; // Пример: сифон поворачивается до 45 градусов
        //    siphonTransform.localRotation = Quaternion.Euler(0, 0, siphonAngle);
        // }
    }
}
