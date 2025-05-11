// /Users/user/Dev/Unity/Squid/Assets/Agents/SquidMovement.cs
using UnityEngine;

public class SquidMovement : MonoBehaviour
{
    private Rigidbody2D rb;
    private Genome genome;

    [Header("Movement Parameters (Base Values)")]
    public float baseForwardForce = 12f; // Немного увеличил
    public float baseTurnTorque = 7f;  // Немного увеличил
    public float maxSpeedBase = 3.5f;

    private float currentMoveForce;
    private float currentTurnTorque;
    private float currentMaxSpeed;
    
    public void Initialize(Genome agentGenome, Rigidbody2D rigidBody)
    {
        this.genome = agentGenome;
        this.rb = rigidBody;

        if (genome == null || rb == null) {
            enabled = false; return;
        }

        float sizeFactor = Mathf.Clamp(genome.mantleLength, 0.5f, 1.8f);
        currentMoveForce = baseForwardForce * sizeFactor * genome.baseMoveForceFactor; // Используем ген
        currentTurnTorque = baseTurnTorque * genome.baseTurnTorqueFactor / Mathf.Sqrt(sizeFactor); // Используем ген
        currentMaxSpeed = maxSpeedBase * Mathf.Sqrt(sizeFactor);
        
        rb.linearDamping = 1.2f;
        rb.angularDamping = 2.5f; // Чуть больше угловое сопротивление для стабильности
    }

    public void ExecuteMovement(SquidBrain.BrainOutput brainOutput)
    {
        if (!enabled || rb == null) return;

        Vector2 forceDirection = transform.up;
        // Умножаем на Time.fixedDeltaTime, так как AddForce с ForceMode2D.Force уже учитывает время,
        // но для более предсказуемого поведения при разном Fixed Timestep лучше явно умножать.
        // Однако, стандартно для ForceMode2D.Force НЕ нужно умножать на deltaTime.
        // Оставим без deltaTime для ForceMode2D.Force, но убедимся, что силы не слишком большие.
        rb.AddForce(forceDirection * brainOutput.moveForward * currentMoveForce * 0.1f); // Уменьшил множитель силы, т.к. baseForwardForce увеличен

        // Поворот
        // Debug.Log($"Agent: {gameObject.name}, Turn Input: {brainOutput.turn:F3}, Torque Applied: {-brainOutput.turn * currentTurnTorque * 0.1f :F3}");
        rb.AddTorque(-brainOutput.turn * currentTurnTorque * 0.1f); // Уменьшил множитель силы

        if (rb.linearVelocity.magnitude > currentMaxSpeed)
        {
            rb.linearVelocity = rb.linearVelocity.normalized * currentMaxSpeed;
        }
    }
}
