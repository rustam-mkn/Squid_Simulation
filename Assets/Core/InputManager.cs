// /Users/user/Dev/Unity/Squid/Assets/Core/InputManager.cs
using UnityEngine;

public class InputManager : MonoBehaviour
{
    // SimulationManager больше не нужен здесь напрямую для управления симуляцией
    private Camera mainCamera;
    private UIManager uiManager;
    private CameraController cameraController; // Для управления слежением

    [Header("Divine Tools (Example)")]
    public GameObject plantFoodPrefabToSpawn;
    public bool divineToolsEnabled = false;

    void Start()
    {
        uiManager = FindFirstObjectByType<UIManager>();
        cameraController = FindFirstObjectByType<CameraController>();
        mainCamera = Camera.main;

        if (mainCamera == null) {
            Debug.LogError("Main Camera not found by InputManager!");
            enabled = false;
        }
        if (cameraController == null) {
            Debug.LogWarning("CameraController not found by InputManager. Follow agent feature might not work.");
        }
    }

    void Update()
    {
        // cameraController будет сам обрабатывать свое движение и зум.
        // Этот скрипт теперь отвечает за клики по миру (выбор агента, божественные инструменты).
        HandleAgentSelectionAndDivineTools();
    }


    void HandleAgentSelectionAndDivineTools()
    {
        if (mainCamera == null) return;

        if (UnityEngine.EventSystems.EventSystem.current != null &&
            UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject())
        {
            return;
        }

        if (Input.GetMouseButtonDown(0))
        {
            Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
            RaycastHit2D hit = Physics2D.GetRayIntersection(ray, Mathf.Infinity);

            if (hit.collider != null)
            {
                SquidAgent agent = hit.collider.GetComponentInParent<SquidAgent>();
                if (agent != null && uiManager != null)
                {
                    uiManager.SelectAgentForInspector(agent);
                } else if (agent == null && uiManager != null) { // Кликнули не по агенту
                     uiManager.DeselectAgentForInspector();
                }
            } else {
                 if (uiManager != null) uiManager.DeselectAgentForInspector();
            }
        }

        if (divineToolsEnabled && plantFoodPrefabToSpawn != null) {
            if (Input.GetMouseButton(1)) // Правая кнопка мыши (удерживание для спавна)
            {
                // Спавнить с некоторой задержкой, чтобы не создавать слишком много
                // Для простоты пока оставим по клику, но лучше сделать rate limit
                if (Input.GetMouseButtonDown(1)) { // Только по первому клику в кадре
                    Vector3 mouseWorldPos = mainCamera.ScreenToWorldPoint(Input.mousePosition);
                    mouseWorldPos.z = 0;
                    Instantiate(plantFoodPrefabToSpawn, mouseWorldPos, Quaternion.identity);
                    EventLogPanel.Instance?.AddLogMessage("Divine: Plant food spawned.");
                }
            }
        }
    }
}
