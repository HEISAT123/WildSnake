---
name: unity-conventions
description: Правила кода и архитектуры Unity для проекта WildSnake. Используй при написании или правке любых C# скриптов, Editor-скриптов и тестов.
---

# Unity conventions (WildSnake)

- Поля инспектора: `[SerializeField] private` + `[Header]`/`[Tooltip]`; публичные поля только при необходимости.
- В рантайме запрещены `FindObjectOfType`, `GameObject.Find`, `GetComponent` в `Update` (кэш в `Awake`).
- Никаких аллокаций в `Update`/`FixedUpdate`: без LINQ, `new` списков/строк, конкатенации строк, лямбд-замыканий в горячем пути.
- Сегменты тела и эффекты - из пула, без `Instantiate`/`Destroy` на каждый шаг.
- События: подписка в `OnEnable`, отписка в `OnDisable`.
- `Awake` - свои компоненты, `Start` - связи с другими объектами.
- Корутины останавливать при отключении; не плодить `WaitForSeconds` (кэшировать).
- Дискретная логика сетки отделена от визуала (интерполяция). UI не содержит игровой логики.
- Один класс - один файл. Имена: `PascalCase`, поля `_camelCase`, константы `UPPER_SNAKE`.
- Singleton только с обоснованием в комментарии. Комментарии на русском, объясняют «почему».
- Не использовать устаревшие API; сверяться с версией в `ProjectSettings/ProjectVersion.txt`.
- Не смешивать `UnityEngine.Input` и Input System при включённом одном бэкенде.
- `.meta`, `.unity`, `.prefab` вручную не править.
