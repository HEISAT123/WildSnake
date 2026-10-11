using UnityEngine;

namespace WildSnake
{
    /// <summary>Медленно поворачивает слой облаков вокруг центра — небо «живёт».</summary>
    public class CloudDrift : MonoBehaviour
    {
        [Header("Движение")]
        [Tooltip("Скорость вращения слоя облаков, град/с")]
        [SerializeField] private float _degreesPerSecond = 0.6f;

        private void Update()
        {
            transform.Rotate(0f, _degreesPerSecond * Time.deltaTime, 0f, Space.World);
        }
    }
}
