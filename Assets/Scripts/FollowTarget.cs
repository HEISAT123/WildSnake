using UnityEngine;

namespace Snake3D
{
    /// <summary>
    /// Держит объект над целью (пыльца и светлячки летают вокруг змеи, а не по всему огромному миру).
    /// Частицы живут в мировых координатах, поэтому за объектом едет только место их рождения.
    /// </summary>
    public class FollowTarget : MonoBehaviour
    {
        [Tooltip("За кем следовать")]
        [SerializeField] private Transform _target;

        [Tooltip("Смещение по высоте над целью")]
        [SerializeField] private float _height = 1.4f;

        private void LateUpdate()
        {
            if (_target == null) return;
            transform.position = _target.position + Vector3.up * _height;
        }
    }
}
