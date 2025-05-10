// /Users/user/Dev/Unity/Squid/Assets/UI/UIManager.cs
using UnityEngine;
using UnityEngine.UI; // Для работы с UI элементами (Button, Text, Slider)
using TMPro; // Если используете TextMeshPro

public class UIManager : MonoBehaviour
{
    public SimulationManager simManager;
    // StatisticsManager и EventLogPanel могут быть найдены или назначены

    [Header("Control Panel Elements")]
    public Button startButton;
    public Button pauseButton;
    public Button resumeButton;
    public Slider timeScaleSlider;
    public TMP_Text timeScaleText; // Используем TMP_Text для TextMeshPro
    public TMP_Text generationText;
    public Toggle divineToolsToggle; // Для включения инструментов "бога"

    [Header("Agent Inspector Panel")]
    public GameObject agentInspectorPanelGO;
    public TMP_Text agentNameText;
    public TMP_Text agentEnergyText;
    public TMP_Text agentAgeText;
    public TMP_Text agentFitnessText;
    public TMP_Text agentGenomeText; // Для отображения части генома
    public Button closeInspectorButton;

    private SquidAgent selectedAgentForUI;
    private InputManager inputManager; // Для управления divine tools

    void Start()
    {
        if (simManager == null) simManager = FindFirstObjectByType<SimulationManager>();
        inputManager = FindFirstObjectByType<InputManager>();

        if (simManager == null) {
            Debug.LogError("UIManager could not find SimulationManager!");
            enabled = false; return;
        }
        
        // Назначение обработчиков на кнопки
        if (startButton) startButton.onClick.AddListener(simManager.RequestStartSimulation);
        else Debug.LogWarning("StartButton not assigned in UIManager.");

        if (pauseButton) pauseButton.onClick.AddListener(simManager.RequestPauseSimulation);
        else Debug.LogWarning("PauseButton not assigned in UIManager.");
        
        if (resumeButton) resumeButton.onClick.AddListener(simManager.RequestResumeSimulation);
        else Debug.LogWarning("ResumeButton not assigned in UIManager.");

        if (timeScaleSlider) timeScaleSlider.onValueChanged.AddListener(simManager.RequestAdjustTimeScale);
        else Debug.LogWarning("TimeScaleSlider not assigned in UIManager.");

        if (divineToolsToggle && inputManager != null) {
            divineToolsToggle.onValueChanged.AddListener((value) => inputManager.divineToolsEnabled = value);
        } else if (inputManager == null) Debug.LogWarning("InputManager not found for DivineToolsToggle.");
        
        if (closeInspectorButton && agentInspectorPanelGO) {
             closeInspectorButton.onClick.AddListener(() => agentInspectorPanelGO.SetActive(false));
        }

        if (agentInspectorPanelGO) agentInspectorPanelGO.SetActive(false);
        
        InitializeUIValues();
    }
    
    void InitializeUIValues() {
        if (timeScaleSlider) {
            timeScaleSlider.minValue = 0.1f; timeScaleSlider.maxValue = 5f; timeScaleSlider.value = 1f;
        }
        UpdateTimeScaleTextValue(1f);
        UpdateGenerationText(0);
    }

    public float GetCurrentTimeScaleRequest() {
        return timeScaleSlider != null ? timeScaleSlider.value : 1f;
    }
    
    public void UpdateTimeScaleSliderValue(float currentTimeScale) {
        if (timeScaleSlider) timeScaleSlider.value = currentTimeScale;
        UpdateTimeScaleTextValue(currentTimeScale);
    }

    void UpdateTimeScaleTextValue(float value) {
        if(timeScaleText) timeScaleText.text = $"Time: x{value:F1}";
    }
    
    void UpdateGenerationText(int genNumber) {
        if (generationText) generationText.text = $"Gen: {genNumber}";
    }

    public void UpdateSimulationStateUI(bool isRunning, float currentTimeScale, int generationNum) {
        if (startButton) startButton.interactable = !isRunning;
        if (pauseButton) pauseButton.interactable = isRunning && currentTimeScale > 0;
        if (resumeButton) resumeButton.interactable = !isRunning || currentTimeScale == 0; // Активна если не запущена или на паузе
        
        UpdateTimeScaleSliderValue(currentTimeScale); // Обновит и текст
        UpdateGenerationText(generationNum);
    }


    public void SelectAgentForInspector(SquidAgent agent)
    {
        selectedAgentForUI = agent;
        if (agentInspectorPanelGO) agentInspectorPanelGO.SetActive(true);
        UpdateAgentInspectorUI();
    }
    public void DeselectAgentForInspector() {
        selectedAgentForUI = null;
        if (agentInspectorPanelGO) agentInspectorPanelGO.SetActive(false);
    }

    void Update() { // Обновляем инспектор только если он активен и агент выбран
        if (selectedAgentForUI != null && agentInspectorPanelGO != null && agentInspectorPanelGO.activeSelf) {
            UpdateAgentInspectorUI();
        }
    }

    void UpdateAgentInspectorUI()
    {
        if (selectedAgentForUI == null || !selectedAgentForUI.isInitialized) {
            if (agentInspectorPanelGO) agentInspectorPanelGO.SetActive(false);
            return;
        }

        if (agentNameText) agentNameText.text = "Name: " + selectedAgentForUI.gameObject.name;
        
        if (selectedAgentForUI.TryGetComponent<SquidMetabolism>(out var meta)) {
             if (agentEnergyText) agentEnergyText.text = $"Energy: {meta.CurrentEnergy:F1} / {meta.maxEnergyGeno:F1}";
             if (agentAgeText) agentAgeText.text = $"Age: {meta.Age:F1} / {selectedAgentForUI.genome.maxAge:F1}";
        }
        if (agentFitnessText) agentFitnessText.text = $"Fitness: {selectedAgentForUI.genome.fitness:F2}";
        
        if (agentGenomeText && selectedAgentForUI.genome != null) {
            // Отображаем только часть генома для краткости
            Genome g = selectedAgentForUI.genome;
            agentGenomeText.text = $"Mantle: L{g.mantleLength:F2} D{g.mantleMaxDiameter:F2}\n" +
                                   $"GraspTent: L{g.baseGraspTentacleLength:F2} Factor{g.maxGraspTentacleLengthFactor:F1}\n" +
                                   $"Eyes: {g.eyeSize:F2}  Metab: {g.metabolismRateFactor:F2}\n" +
                                   $"Aggro: {g.aggression:F2} FoodPref: {g.foodPreference:F2}";
        }
    }
}
