using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace WildSnake
{
    /// <summary>
    /// «Экран» видео-обучения: кадр рисуется из простых фигур (круги, эллипсы, отрезки, дуги) прямо в меш UI,
    /// поверх — надписи и клавиши из небольшого пула. Координаты сцены — 16×9 единиц, (0, 0) — левый нижний угол.
    /// Кадр собирается каждый кадр между BeginFrame и EndFrame; буферы выделены заранее — аллокаций нет.
    /// Что рисовать, решает TutorialFilm, когда — TutorialPlayer.
    /// </summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public class TutorialCanvas : MaskableGraphic
    {
        public const float WIDTH = 16f;
        public const float HEIGHT = 9f;

        [Header("Надписи и клавиши в кадре (пулы)")]
        [Tooltip("Подписи поверх кадра: «!», «+ мышь», «−14»…")]
        [SerializeField] private Text[] _labels = new Text[0];

        [Tooltip("Клавиши-подсказки поверх кадра (корни)")]
        [SerializeField] private RectTransform[] _keys = new RectTransform[0];

        [Tooltip("Рамки клавиш: подсвечиваются при «нажатии»")]
        [SerializeField] private Image[] _keyFrames = new Image[0];

        [Tooltip("Надписи на клавишах")]
        [SerializeField] private Text[] _keyTexts = new Text[0];

        [Header("Цвета клавиш")]
        [SerializeField] private Color _keyIdle = new Color(0.95f, 0.93f, 0.88f, 0.55f);
        [SerializeField] private Color _keyPressed = new Color(0.78f, 0.96f, 0.2f, 1f);

        private readonly List<UIVertex> _verts = new List<UIVertex>(16384);
        private readonly List<int> _indices = new List<int>(32768);
        private int _labelsUsed;
        private int _keysUsed;
        private Rect _rect;

        // ---------------- Кадр ----------------

        public void BeginFrame()
        {
            _verts.Clear();
            _indices.Clear();
            _labelsUsed = 0;
            _keysUsed = 0;
            _rect = rectTransform.rect;
        }

        public void EndFrame()
        {
            for (int i = _labelsUsed; i < _labels.Length; i++) SetActive(_labels[i].gameObject, false);
            for (int i = _keysUsed; i < _keys.Length; i++) SetActive(_keys[i].gameObject, false);
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            if (_verts.Count > 0) vh.AddUIVertexStream(_verts, _indices);
        }

        // ---------------- Фигуры ----------------

        public void Rect(float x0, float y0, float x1, float y1, Color c)
        {
            Quad(new Vector2(x0, y0), new Vector2(x0, y1), new Vector2(x1, y1), new Vector2(x1, y0), c, c);
        }

        /// <summary>Прямоугольник с вертикальным градиентом (низ → верх).</summary>
        public void RectGradient(float x0, float y0, float x1, float y1, Color bottom, Color top)
        {
            Quad(new Vector2(x0, y0), new Vector2(x0, y1), new Vector2(x1, y1), new Vector2(x1, y0), bottom, top);
        }

        /// <summary>Прямоугольник с горизонтальным градиентом (лево → право).</summary>
        public void RectGradientH(float x0, float y0, float x1, float y1, Color left, Color right)
        {
            Quad(new Vector2(x0, y1), new Vector2(x1, y1), new Vector2(x1, y0), new Vector2(x0, y0), left, right);
        }

        public void Circle(Vector2 center, float radius, Color c)
        {
            Ellipse(center, radius, radius, 0f, c);
        }

        /// <summary>Эллипс, повёрнутый на angle градусов (полуось rx — вдоль направления angle).</summary>
        public void Ellipse(Vector2 center, float rx, float ry, float angle, Color c)
        {
            const int segments = 20;
            float rad = angle * Mathf.Deg2Rad;
            float cos = Mathf.Cos(rad), sin = Mathf.Sin(rad);
            int start = _verts.Count;
            AddVert(center, c);
            for (int i = 0; i < segments; i++)
            {
                float a = i * Mathf.PI * 2f / segments;
                float lx = Mathf.Cos(a) * rx, ly = Mathf.Sin(a) * ry;
                AddVert(new Vector2(center.x + lx * cos - ly * sin, center.y + lx * sin + ly * cos), c);
            }
            for (int i = 0; i < segments; i++)
            {
                _indices.Add(start);
                _indices.Add(start + 1 + i);
                _indices.Add(start + 1 + (i + 1) % segments);
            }
        }

        /// <summary>Дуга-кольцо: fill01 — доля окружности от угла start (град, против часовой).</summary>
        public void Ring(Vector2 center, float radius, float width, float fill01, float startAngle, Color c)
        {
            fill01 = Mathf.Clamp01(fill01);
            if (fill01 <= 0f) return;
            int segments = Mathf.Max(2, Mathf.CeilToInt(32 * fill01));
            float inner = radius - width * 0.5f, outer = radius + width * 0.5f;
            float a0 = startAngle * Mathf.Deg2Rad;
            float span = fill01 * Mathf.PI * 2f;
            for (int i = 0; i < segments; i++)
            {
                float a = a0 + span * i / segments, b = a0 + span * (i + 1) / segments;
                var da = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                var db = new Vector2(Mathf.Cos(b), Mathf.Sin(b));
                Quad(center + da * inner, center + da * outer, center + db * outer, center + db * inner, c, c);
            }
        }

        public void Line(Vector2 a, Vector2 b, float width, Color c)
        {
            Vector2 d = b - a;
            if (d.sqrMagnitude < 1e-8f) return;
            Vector2 n = new Vector2(-d.y, d.x).normalized * (width * 0.5f);
            Quad(a - n, a + n, b + n, b - n, c, c);
        }

        public void Triangle(Vector2 a, Vector2 b, Vector2 c, Color color)
        {
            int start = _verts.Count;
            AddVert(a, color);
            AddVert(b, color);
            AddVert(c, color);
            _indices.Add(start);
            _indices.Add(start + 1);
            _indices.Add(start + 2);
        }

        // ---------------- Надписи и клавиши ----------------

        /// <summary>Подпись в точке сцены (центр). Строку передавать константой — текст меняется, только если другой.</summary>
        public void Label(Vector2 at, string text, Color color)
        {
            if (_labelsUsed >= _labels.Length) return;
            Text t = _labels[_labelsUsed++];
            SetActive(t.gameObject, true);
            t.text = text;
            t.color = color;
            t.rectTransform.anchoredPosition = ToLocal(at);
        }

        /// <summary>Клавиша-подсказка в точке сцены; pressed — подсвечена, как будто её нажали.</summary>
        public void Key(Vector2 at, string key, bool pressed)
        {
            if (_keysUsed >= _keys.Length) return;
            int i = _keysUsed++;
            SetActive(_keys[i].gameObject, true);
            _keyTexts[i].text = key;
            _keyFrames[i].color = pressed ? _keyPressed : _keyIdle;
            _keyTexts[i].color = pressed ? _keyPressed : Color.white;
            _keys[i].sizeDelta = new Vector2(Mathf.Max(40f, 24f + key.Length * 13f), 38f);
            _keys[i].anchoredPosition = ToLocal(at);
            _keys[i].localScale = Vector3.one * (pressed ? 1.12f : 1f);
        }

        // ---------------- Внутреннее ----------------

        /// <summary>Точка сцены → позиция относительно левого нижнего угла холста (для надписей с якорем (0, 0)).</summary>
        private Vector2 ToLocal(Vector2 p)
        {
            return new Vector2(p.x / WIDTH * _rect.width, p.y / HEIGHT * _rect.height);
        }

        private void AddVert(Vector2 scenePoint, Color c)
        {
            UIVertex v = UIVertex.simpleVert;
            v.position = new Vector3(_rect.xMin + scenePoint.x / WIDTH * _rect.width, _rect.yMin + scenePoint.y / HEIGHT * _rect.height, 0f);
            v.color = c * color;
            _verts.Add(v);
        }

        private void Quad(Vector2 a, Vector2 b, Vector2 c, Vector2 d, Color colorAD, Color colorBC)
        {
            int start = _verts.Count;
            AddVert(a, colorAD);
            AddVert(b, colorBC);
            AddVert(c, colorBC);
            AddVert(d, colorAD);
            _indices.Add(start);
            _indices.Add(start + 1);
            _indices.Add(start + 2);
            _indices.Add(start);
            _indices.Add(start + 2);
            _indices.Add(start + 3);
        }

        private static void SetActive(GameObject go, bool on)
        {
            if (go.activeSelf != on) go.SetActive(on);
        }
    }
}
