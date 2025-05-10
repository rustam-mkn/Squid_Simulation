// /Users/user/Dev/Unity/Squid/Assets/UI/Panels/EventLogPanel.cs
using UnityEngine;
using UnityEngine.UI; // Для ScrollRect
using TMPro; // Для TextMeshPro
using System.Collections.Generic;
using System.Linq; // Для Enumerable.Reverse

public class EventLogPanel : MonoBehaviour
{
    public TMP_Text logTextDisplay; // Используем TMP_Text
    public ScrollRect scrollRect;
    public int maxLogMessages = 30;
    private List<string> logMessages = new List<string>(); // Используем List для удобного добавления в начало

    private static EventLogPanel instance;
    public static EventLogPanel Instance { get { return instance; } }

    void Awake()
    {
        if (instance == null) instance = this;
        else if (instance != this) Destroy(gameObject);

        // Application.logMessageReceivedThreaded += HandleUnityLog; // Для системных логов Unity (безопасно для потоков)
        if (logTextDisplay) logTextDisplay.text = ""; // Очищаем при старте
    }

    // void OnDestroy()
    // {
    //    Application.logMessageReceivedThreaded -= HandleUnityLog;
    // }

    public void AddLogMessage(string message)
    {
        if (logMessages.Count >= maxLogMessages)
        {
            logMessages.RemoveAt(logMessages.Count -1); // Удаляем самое старое (последнее в списке)
        }
        // Добавляем в начало списка, чтобы новые сообщения были сверху
        logMessages.Insert(0, $"[{System.DateTime.Now:HH:mm:ss}] {message}");
        UpdateLogText();
    }

    // void HandleUnityLog(string logString, string stackTrace, LogType type)
    // {
    //     // Этот метод будет вызван из другого потока, нужна синхронизация или отправка в главный поток
    //     // Для простоты, пока не будем обрабатывать системные логи здесь
    // }

    void UpdateLogText()
    {
        if (logTextDisplay != null)
        {
            logTextDisplay.text = string.Join("\n", logMessages); // Новые вверху
            // Автопрокрутка вверх (к новым сообщениям)
            if (scrollRect != null) {
                 Canvas.ForceUpdateCanvases();
                 scrollRect.normalizedPosition = new Vector2(0, 1); // Прокрутка в самый верх
            }
        }
    }
}
