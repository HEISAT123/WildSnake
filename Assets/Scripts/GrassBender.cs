using UnityEngine;

namespace WildSnake
{
    /// <summary>
    /// Существа раздвигают траву: позиции (голова змеи, лиса) каждый кадр уходят в глобальный массив шейдера
    /// WildSnake/Grass, сам изгиб считается в вершинах на GPU. Выключенные объекты пропускаются.
    /// Массивы созданы один раз — аллокаций в кадре нет.
    /// </summary>
    public class GrassBender : MonoBehaviour
    {
        private const int MAX_BENDERS = 4;
        private static readonly int BendersId = Shader.PropertyToID("_GrassBenders");
        private static readonly int CountId = Shader.PropertyToID("_GrassBenderCount");

        [Header("Ссылки")]
        [Tooltip("Кто раздвигает траву (не больше 4)")]
        [SerializeField] private Transform[] _targets = new Transform[0];

        [Header("Изгиб")]
        [Tooltip("Радиус раздвигания для каждой цели, м (по индексу; не хватает — берётся последний)")]
        [SerializeField] private float[] _radii = { 0.9f };

        private readonly Vector4[] _benders = new Vector4[MAX_BENDERS];

        private void Start()
        {
            // Длина глобального массива фиксируется первой установкой — сразу отдаём все четыре.
            Shader.SetGlobalVectorArray(BendersId, _benders);
        }

        private void OnDisable()
        {
            Shader.SetGlobalFloat(CountId, 0f);
        }

        private void LateUpdate()
        {
            int n = 0;
            for (int i = 0; i < _targets.Length && n < MAX_BENDERS; i++)
            {
                Transform t = _targets[i];
                if (t == null || !t.gameObject.activeInHierarchy) continue;
                float r = _radii.Length > 0 ? _radii[Mathf.Min(i, _radii.Length - 1)] : 0.9f;
                Vector3 p = t.position;
                _benders[n++] = new Vector4(p.x, p.y, p.z, r);
            }
            Shader.SetGlobalVectorArray(BendersId, _benders);
            Shader.SetGlobalFloat(CountId, n);
        }
    }
}
