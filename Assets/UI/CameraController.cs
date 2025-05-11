// /Users/user/Dev/Unity/Squid/Assets/UI/CameraController.cs
using UnityEngine;

public class CameraController : MonoBehaviour
{
    [Header("Pan & Zoom")]
    public float panSpeed = 20f;
    public float scrollSpeed = 30f; // Увеличил чувствительность
    public float minZoomOrthographic = 2f;
    public float maxZoomOrthographic = 100f; // Увеличил макс зум

    [Header("Follow Target")]
    public Transform targetToFollow;
    public float followSmoothSpeed = 0.125f;
    public Vector3 followOffset = new Vector3(0, 0, -10); // Z остается -10

    private Camera cam;

    void Awake()
    {
        cam = GetComponent<Camera>();
        if (cam == null) {
            Debug.LogError("CameraController script must be attached to a Camera GameObject!");
            enabled = false;
        }
    }

    void LateUpdate() // Используем LateUpdate для камеры, чтобы она двигалась после всех агентов
    {
        if (cam == null) return;

        if (targetToFollow != null)
        {
            Vector3 desiredPosition = targetToFollow.position + followOffset;
            Vector3 smoothedPosition = Vector3.Lerp(transform.position, desiredPosition, followSmoothSpeed * Time.deltaTime * 10f); // *10f для более быстрой реакции
            transform.position = smoothedPosition;
        }
        else // Ручное управление, если нет цели для слежения
        {
            HandleManualPan();
        }
        HandleManualZoom(); // Зум работает всегда
    }

    void HandleManualPan()
    {
        if (UnityEngine.EventSystems.EventSystem.current != null &&
            UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject())
        {
            return;
        }

        float horizontal = 0f;
        float vertical = 0f;

        if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow)) vertical = 1f;
        if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow)) vertical = -1f;
        if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)) horizontal = -1f;
        if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) horizontal = 1f;

        if (Mathf.Abs(horizontal) > 0.01f || Mathf.Abs(vertical) > 0.01f)
        {
            Vector3 move = new Vector3(horizontal, vertical, 0) * panSpeed * Time.unscaledDeltaTime;
            transform.Translate(move, Space.World);
        }
    }

    void HandleManualZoom()
    {
        if (UnityEngine.EventSystems.EventSystem.current != null &&
            UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject() && Input.mouseScrollDelta.y !=0) // Не зумить если курсор над UI и есть скролл
        {
            return;
        }

        float scroll = Input.GetAxis("Mouse ScrollWheel");
        if (cam.orthographic && Mathf.Abs(scroll) > 0.01f)
        {
            cam.orthographicSize -= scroll * scrollSpeed * Time.unscaledDeltaTime * 10f;
            cam.orthographicSize = Mathf.Clamp(cam.orthographicSize, minZoomOrthographic, maxZoomOrthographic);
        }
    }

    public void SetTargetToFollow(Transform target)
    {
        targetToFollow = target;
        EventLogPanel.Instance?.AddLogMessage($"Camera now following {target.name}.");
    }

    public void ClearTargetToFollow()
    {
        if(targetToFollow != null) EventLogPanel.Instance?.AddLogMessage($"Camera stopped following {targetToFollow.name}.");
        targetToFollow = null;
    }
}
