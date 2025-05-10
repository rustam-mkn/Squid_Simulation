// /Users/user/Dev/Unity/Squid/Assets/Core/InputManager.cs
using UnityEngine;

public class InputManager : MonoBehaviour
{
    private SimulationManager simManager;
    private Camera mainCamera;
    private UIManager uiManager; // Для взаимодействия с UI при кликах

    [Header("Camera Controls")]
    public float panSpeed = 20f;
    public float scrollSpeed = 20f;
    public float minZoomOrthographic = 2f;
    public float maxZoomOrthographic = 50f;

    [Header("Divine Tools (Example)")]
    public GameObject plantFoodPrefabToSpawn; // Назначить в инспекторе
    public bool divineToolsEnabled = false; // Включать/выключать через UI

    void Start()
    {
        simManager = FindFirstObjectByType<SimulationManager>();
        uiManager = FindFirstObjectByType<UIManager>();
        mainCamera = Camera.main;

        if (mainCamera == null) {
            Debug.LogError("Main Camera not found by InputManager!");
            enabled = false;
        }
    }

    void Update()
    {
        HandleCameraControls();
        // HandleSimulationControls(); // Перенесено в UIManager для кнопок
        HandleAgentSelectionAndDivineTools();
    }

    void HandleCameraControls()
    {
        if (mainCamera == null) return;

        // Проверяем, не находится ли курсор над UI элементом, чтобы не двигать камеру при кликах по UI
        if (UnityEngine.EventSystems.EventSystem.current != null &&
            UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject())
        {
            return;
        }

        float horizontal = 0f;
        float vertical = 0f;

        // Используем стандартные оси, если они настроены, или прямые клавиши
        if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow)) vertical = 1f;
        if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow)) vertical = -1f;
        if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)) horizontal = -1f;
        if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) horizontal = 1f;


        if (Mathf.Abs(horizontal) > 0.01f || Mathf.Abs(vertical) > 0.01f)
        {
            // Используем Time.unscaledDeltaTime, чтобы камера двигалась даже на паузе
            Vector3 move = new Vector3(horizontal, vertical, 0) * panSpeed * Time.unscaledDeltaTime;
            mainCamera.transform.Translate(move, Space.World);
        }
        
        float scroll = Input.GetAxis("Mouse ScrollWheel");
        if (mainCamera.orthographic && Mathf.Abs(scroll) > 0.01f)
        {
            mainCamera.orthographicSize -= scroll * scrollSpeed * Time.unscaledDeltaTime * 10f;
            mainCamera.orthographicSize = Mathf.Clamp(mainCamera.orthographicSize, minZoomOrthographic, maxZoomOrthographic);
        }
    }

    void HandleAgentSelectionAndDivineTools()
    {
        if (mainCamera == null) return;

        // Не обрабатывать клики мыши, если курсор над UI
        if (UnityEngine.EventSystems.EventSystem.current != null &&
            UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject())
        {
            return;
        }

        if (Input.GetMouseButtonDown(0)) // Левая кнопка - выбор агента
        {
            Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
            RaycastHit2D hit = Physics2D.GetRayIntersection(ray, Mathf.Infinity);

            if (hit.collider != null)
            {
                SquidAgent agent = hit.collider.GetComponentInParent<SquidAgent>(); // GetComponentInParent на случай, если кликнули по щупальцу
                if (agent != null && uiManager != null)
                {
                    uiManager.SelectAgentForInspector(agent);
                }
            } else {
                 if (uiManager != null) uiManager.DeselectAgentForInspector();
            }
        }

        if (divineToolsEnabled && plantFoodPrefabToSpawn != null) {
            if (Input.GetMouseButtonDown(1)) // Правая кнопка - спавн еды (пример)
            {
                Vector3 mouseWorldPos = mainCamera.ScreenToWorldPoint(Input.mousePosition);
                mouseWorldPos.z = 0;
                Instantiate(plantFoodPrefabToSpawn, mouseWorldPos, Quaternion.identity);
                if(FindFirstObjectByType<EventLogPanel>() != null)
                    FindFirstObjectByType<EventLogPanel>().AddLogMessage("Divine intervention: Plant food spawned.");
            }
        }
    }
}
