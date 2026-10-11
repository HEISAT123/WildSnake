# CLAUDE.md — Wild Snake (Unity)

Корневая карта проекта: читается в каждой сессии, поэтому коротко. Подробности — во вложенных CLAUDE.md
(подгружаются, когда работаешь с файлами папки): `Assets/Scripts/`, `Assets/Editor/`, `Assets/Tests/`.

## Проект
- Симулятор выживания змеи от третьего лица (WASD + мышь): охота, засады, хищники (орёл, лиса), день и ночь, норы,
  линька с улучшениями. Главная цель «Продолжить род»: повзрослеть → ночью кладка в гнезде → охрана → следующее поколение.
  Одиночная игра с ИИ-соперницей, без сети.
- Unity 6000.6.5f1, URP 17.6, старый Input Manager (весь ввод — `Assets/Scripts/GameInput.cs`). C#, комментарии и коммиты на русском.
- Сторонних пакетов и ассетов нет: всё из примитивов, мешей и материалов, созданных кодом.

## Где что
```
Assets/Scripts/   игровой код (WildSnake.asmdef)        → детали: Assets/Scripts/CLAUDE.md
Assets/Editor/    сборка сцены, UI, окружения, URP    → детали: Assets/Editor/CLAUDE.md
Assets/Tests/     EditMode/PlayMode-тесты              → детали: Assets/Tests/CLAUDE.md
Assets/Materials/ Meshes/ Settings/ Scenes/  — генерируются сборщиком сцены (Settings: GameConfig.asset, URP/)
```
Ключевые узлы: `GameManager` (связующее звено, состояния и правила), `SnakeController` (змея, управление «намерением»),
`GridManager` (поле), `GameConfig` (баланс), `UIController` + HUD-компоненты, `SnakeSceneBuilder` (вся сцена кодом).
Ищи класс по имени файла — один класс = один файл.

## Правила кода
- Логика в данных, Transform только отображает. Связи — через ссылки инспектора или события; Singleton — только с обоснованием.
  UI-код и игровую логику не смешивать.
- Поля: `[SerializeField] private` с `[Header]`/`[Tooltip]`. Имена: `PascalCase`, приватные `_camelCase`.
- В рантайме нельзя: `FindObjectOfType`, `GameObject.Find`, `GetComponent` в `Update`; аллокации в `Update` (LINQ, new, конкатенация строк).
- Пулы вместо `Instantiate`/`Destroy` в игре. Подписка в `OnEnable`, отписка в `OnDisable`. Свои ссылки — в `Awake`, чужие — в `Start`.
- Комментарии краткие, про «почему». Не использовать устаревшие API; при сомнении — проверить, что API есть в этой версии Unity.

## Нельзя трогать
`Library/`, `Temp/`, `Logs/`, `obj/`, `UserSettings/`; `.meta` вручную; `.unity`/`.prefab` вручную — только через Editor-скрипт.

## Проверка и сборка
- После правок `.cs` — компиляция: `Unity -batchmode -nographics -quit -projectPath . -logFile -` (путь — `UNITY_EXE`,
  см. `tools/check-compile.ps1`). Если редактор открыт — работать в копии проекта (с её Library). Ошибки чинить сразу.
- Сцена: `Tools/Build Snake Scene` / `-executeMethod SnakeSceneBuilder.Build`. Новые объекты сцены — только через сборщик.
- Игра: `Tools/Build Game (Windows)` / `-executeMethod GameBuilder.BuildWindows` → `Builds/` (не в git).

## Порядок работы
1. Читать только связанные файлы. 2. Для задач больше одного файла — сначала короткий план.
3. Мелкие шаги, компиляция после каждого. 4. Без непрошеного рефакторинга.
5. В конце: что изменено, как проверить, допущения. 6. Неоднозначное — разумный дефолт и список допущений, без остановки на вопросы.
- Git: коммит перед крупной правкой, сообщения короткие; Visible Meta Files и Force Text включены.
