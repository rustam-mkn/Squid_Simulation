// /Users/user/Dev/Unity/Squid/Assets/Environment/Food.cs
using UnityEngine;

public enum FoodType { Plant, Meat }

public class Food : MonoBehaviour
{
    public FoodType type = FoodType.Plant;
    public float energyValue = 10f;
    public float lifetime = 60f;

    private bool isConsumed = false; // Чтобы не потребить дважды

    void Start()
    {
        if (lifetime > 0) Destroy(gameObject, lifetime);
        
        // Простая визуализация цветом, если нет SpriteRenderer
        // Предполагаем, что на префабе еды есть MeshRenderer и простой материал
        if (TryGetComponent<MeshRenderer>(out var mr)) {
            // Создаем экземпляр материала, чтобы изменение цвета не влияло на другие объекты с тем же материалом
            if (mr.material != null) { // Проверка на случай, если материала нет
                 mr.material = new Material(mr.material);
                 mr.material.color = (type == FoodType.Plant) ? new Color(0.1f,0.7f,0.1f) : new Color(0.7f,0.1f,0.1f);
            }
        } else if (TryGetComponent<SpriteRenderer>(out var sr)) { // Если все же спрайт
            sr.color = (type == FoodType.Plant) ? Color.green : Color.red;
        }
    }

    // Вызывается, когда щупальце "схватило" еду
    // Возвращает себя, чтобы щупальце могло получить данные и затем сообщить агенту
    public Food TryConsume()
    {
        if (isConsumed) return null;
        isConsumed = true;
        // Не уничтожаем здесь, щупальце или агент сделает это после получения данных
        // Destroy(gameObject, 0.1f); // Небольшая задержка, чтобы щупальце успело обработать
        return this;
    }
}
