using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Snake3D
{
    /// <summary>Сообщает меню, что мышь навели на пункт, — выделение следует за курсором.</summary>
    public class MenuItemHover : MonoBehaviour, IPointerEnterHandler
    {
        public event Action<int> Entered;

        public int Index { get; set; }

        public void OnPointerEnter(PointerEventData eventData)
        {
            Entered?.Invoke(Index);
        }
    }
}
