// /Users/user/Dev/Unity/Squid/Assets/UI/UIManager.cs
using UnityEngine;
using UnityEngine.UI; // Для стандартных UI элементов Button, Slider, Toggle
using TMPro;          // Для TextMeshPro элементов
using System.Text;    // Для StringBuilder

public class UIManager : MonoBehaviour
{
    [Header("Core Managers (Assign or will be found)")]
    public SimulationManager simManager;
    public InputManager inputManager;
    private CameraController cameraController;

    [Header("Control Panel Elements")]
    public Button startButton;
    public Button pauseButton;
    public Button resumeButton;
    public Slider timeScaleSlider;
    public TMP_Text timeScaleText;
    public TMP_Text generationText;
    public Toggle divineToolsToggle;
    public Toggle showAllGizmosToggle;
    // УДАЛЕНЫ: public Slider energyDrainSlider;
    // УДАЛЕНЫ: public TMP_Text energyDrainText;

    [Header("Agent Inspector Panel")]
    public GameObject agentInspectorPanelGO;
    public TMP_Text agentNameText;
    public TMP_Text agentEnergyText;
    public TMP_Text agentAgeText;
    public TMP_Text agentFitnessText;
    public TMP_Text agentSightRadiusText;
    public TMP_Text agentGenomeMultiLineText;
    public Button closeInspectorButton;
    public Button followAgentButton;

    public SquidAgent selectedAgentUI { get; private set; }
    public bool showAllAgentGizmos { get; private set; } = false;

    // Для отложенного обновления TimeScale от слайдера
    private float lastTimeScaleSliderValue = 1f;
    private float timeSinceLastTimeScaleUpdate = 0f;
    private const float TIMESCALE_SLIDER_UPDATE_DEBOUNCE_TIME = 0.2f;
    private bool timeScaleSliderValueChangedSinceLastUpdate = false;

    void Start()
    {
        if (simManager == null) simManager = FindFirstObjectByType<SimulationManager>();
        if (inputManager == null) inputManager = FindFirstObjectByType<InputManager>();
        cameraController = FindFirstObjectByType<CameraController>();

        if (simManager == null) {
            Debug.LogError("UIManager could not find SimulationManager! UI will not function correctly.");
            enabled = false;
            return;
        }
        
        if (startButton) startButton.onClick.AddListener(simManager.RequestStartSimulation);
        else Debug.LogWarning("UIManager: StartButton not assigned.");

        if (pauseButton) pauseButton.onClick.AddListener(simManager.RequestPauseSimulation);
        else Debug.LogWarning("UIManager: PauseButton not assigned.");
        
        if (resumeButton) resumeButton.onClick.AddListener(simManager.RequestResumeSimulation);
        else Debug.LogWarning("UIManager: ResumeButton not assigned.");

        if (timeScaleSlider) {
            timeScaleSlider.onValueChanged.AddListener(OnTimeScaleSliderValueChangedInternal);
        } else Debug.LogWarning("UIManager: TimeScaleSlider not assigned.");
        
        if (divineToolsToggle && inputManager != null) {
            divineToolsToggle.isOn = inputManager.divineToolsEnabled;
            divineToolsToggle.onValueChanged.AddListener((value) => inputManager.divineToolsEnabled = value);
        } else if (inputManager == null && divineToolsToggle != null) Debug.LogWarning("UIManager: InputManager not found for DivineToolsToggle.");
        
        if (showAllGizmosToggle) {
            showAllGizmosToggle.isOn = showAllAgentGizmos;
            showAllGizmosToggle.onValueChanged.AddListener((value) => showAllAgentGizmos = value);
        } else Debug.LogWarning("UIManager: ShowAllGizmosToggle not assigned.");
        
        // УДАЛЕНА: Подписка на слайдер скорости голода
        
        if (closeInspectorButton && agentInspectorPanelGO) {
             closeInspectorButton.onClick.AddListener(DeselectAgentForInspector);
        } else if (closeInspectorButton == null && agentInspectorPanelGO != null) Debug.LogWarning("UIManager: CloseInspectorButton not assigned.");
        
        if (followAgentButton && cameraController != null) {
            followAgentButton.onClick.AddListener(() => {
                if (selectedAgentUI != null) cameraController.SetTargetToFollow(selectedAgentUI.transform);
            });
        } else if (cameraController == null && followAgentButton != null) Debug.LogWarning("UIManager: CameraController not found for FollowAgent button.");

        if (agentInspectorPanelGO) agentInspectorPanelGO.SetActive(false);
        
        // InitializeUIValues теперь не принимает initialEnergyDrain
        InitializeUIValues(Time.timeScale, simManager.currentGenerationNumber);
    }
    
    void OnTimeScaleSliderValueChangedInternal(float value) {
        lastTimeScaleSliderValue = value;
        timeScaleSliderValueChangedSinceLastUpdate = true;
        UpdateTimeScaleTextValue(value);
    }

    // УДАЛЕНЫ: OnEnergyDrainSliderChanged и UpdateEnergyDrainText
    
    // InitializeUIValues теперь не принимает initialEnergyDrain
    public void InitializeUIValues(float initialTimeScale, int initialGen) {
        if (timeScaleSlider) {
            timeScaleSlider.minValue = 0.1f;
            timeScaleSlider.maxValue = 10f;
            timeScaleSlider.value = initialTimeScale;
            lastTimeScaleSliderValue = initialTimeScale;
        }
        UpdateTimeScaleTextValue(initialTimeScale);
        UpdateGenerationText(initialGen);
        // УДАЛЕНО: Инициализация UI для слайдера голода
    }

    public float GetCurrentTimeScaleRequest() {
        return timeScaleSlider != null ? timeScaleSlider.value : 1f;
    }
    
    public void UpdateTimeScaleSliderValue(float currentTimeScale) {
        if (timeScaleSlider && Mathf.Abs(timeScaleSlider.value - currentTimeScale) > 0.01f) {
            timeScaleSlider.value = currentTimeScale;
        }
        UpdateTimeScaleTextValue(currentTimeScale);
    }

    void UpdateTimeScaleTextValue(float value) {
        if(timeScaleText) timeScaleText.text = $"Time: x{value:F1}";
    }
    
    void UpdateGenerationText(int genNumber) {
        if (generationText) generationText.text = $"Gen: {genNumber}";
    }
    
    public void UpdateSimulationStateUI(bool currentIsRunning, bool currentIsPaused, float currentTimeScale, int generationNum) {
        if (startButton) startButton.interactable = !currentIsRunning;
        if (pauseButton) pauseButton.interactable = currentIsRunning && !currentIsPaused;
        if (resumeButton) resumeButton.interactable = currentIsRunning && currentIsPaused;
        
        UpdateTimeScaleSliderValue(currentTimeScale);
        UpdateGenerationText(generationNum);
    }

    public void SelectAgentForInspector(SquidAgent agent)
    {
        selectedAgentUI = agent;
        if (agentInspectorPanelGO != null) agentInspectorPanelGO.SetActive(true);
        UpdateAgentInspectorUI();
    }

    public void DeselectAgentForInspector() {
        selectedAgentUI = null;
        if (agentInspectorPanelGO != null) agentInspectorPanelGO.SetActive(false);
        if (cameraController != null) cameraController.ClearTargetToFollow();
    }

    void Update()
    {
        if (selectedAgentUI != null && agentInspectorPanelGO != null && agentInspectorPanelGO.activeSelf) {
            if (selectedAgentUI.gameObject == null || !selectedAgentUI.isInitialized) {
                DeselectAgentForInspector();
                // return; // Убрал return, чтобы debounce для TimeScaleSlider продолжал работать
            } else {
                UpdateAgentInspectorUI();
            }
        }

        if (timeScaleSliderValueChangedSinceLastUpdate) {
            timeSinceLastTimeScaleUpdate += Time.unscaledDeltaTime;
            if (timeSinceLastTimeScaleUpdate >= TIMESCALE_SLIDER_UPDATE_DEBOUNCE_TIME) {
                if (simManager != null) {
                    simManager.RequestAdjustTimeScale(lastTimeScaleSliderValue);
                }
                timeSinceLastTimeScaleUpdate = 0f;
                timeScaleSliderValueChangedSinceLastUpdate = false;
            }
        }
    }

    void UpdateAgentInspectorUI()
    {
        if (selectedAgentUI == null || !selectedAgentUI.isInitialized || selectedAgentUI.genome == null) {
            if (agentInspectorPanelGO != null && agentInspectorPanelGO.activeSelf) {
                 agentInspectorPanelGO.SetActive(false);
            }
            return;
        }

        if (agentNameText) agentNameText.text = selectedAgentUI.gameObject.name;
        
        if (selectedAgentUI.TryGetComponent<SquidMetabolism>(out var meta)) {
             if (agentEnergyText) agentEnergyText.text = $"E: {meta.CurrentEnergy:F0}/{meta.maxEnergyGeno:F0}";
             if (agentAgeText) agentAgeText.text = $"Age: {meta.Age:F1}s";
        } else {
            if (agentEnergyText) agentEnergyText.text = "E: N/A";
            if (agentAgeText) agentAgeText.text = "Age: N/A";
        }

        if (agentFitnessText) agentFitnessText.text = $"Fit: {selectedAgentUI.genome.fitness:F1}";
        
        if (agentSightRadiusText != null && selectedAgentUI.TryGetComponent<SquidSenses>(out var senses)) {
            agentSightRadiusText.text = $"Sight R: {senses.currentSightRadius:F1}, A: {senses.currentSightAngle:F0}°";
        } else if (agentSightRadiusText != null) {
            agentSightRadiusText.text = "Sight: N/A";
        }
        
        if (agentGenomeMultiLineText) {
            Genome g = selectedAgentUI.genome;
            StringBuilder sb = new StringBuilder();
            sb.AppendLine($"Mantle L: {g.mantleLength:F2}, D: {g.mantleMaxDiameter:F2}");
            sb.AppendLine($"Color: ({g.mantleColor.r:F1},{g.mantleColor.g:F1},{g.mantleColor.b:F1})");
            sb.AppendLine($"SwimTent L: {g.baseSwimTentacleLength:F2}, Th: {g.swimTentacleThickness:F3}");
            sb.AppendLine($"Eye Size: {g.eyeSize:F2}");
            sb.AppendLine($"MoveF: {g.baseMoveForceFactor:F2}, TurnF: {g.baseTurnTorqueFactor:F2}");
            sb.AppendLine($"Metab.Rate: {g.metabolismRateFactor:F2}, MaxAge: {g.maxAge:F0}s");
            sb.AppendLine($"Repr.Thresh: {g.energyToReproduceThresholdFactor:P0}, Cost: {g.energyCostOfReproductionFactor:P0}");
            sb.AppendLine($"Aggression: {g.aggression:F2}, FoodPref: {g.foodPreference:F2}");
            agentGenomeMultiLineText.text = sb.ToString();
        }
    }
}
