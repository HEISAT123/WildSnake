# CLAUDE.md — 3D Змейка (Unity)

Этот файл читается Claude Code в начале каждой сессии. Следуй ему всегда.

## Проект
- Игра: 3D-змейка со свободным движением, одиночная (с ИИ-соперником), без сети.
- Движок: Unity 6000.6.5f1
- Рендер-пайплайн: Built-in (код и SnakeSceneBuilder поддерживают и URP)
- Ввод: Input Manager (старый). Весь ввод через `Assets/Scripts/GameInput.cs`, там же ветка для Input System
- Язык кода: C#. Комментарии и сообщения коммитов на русском.
- Сторонние пакеты и платные ассеты не используем. Все объекты строим из примитивов (Cube, Sphere, Plane) и материалов, созданных кодом.

## Структура папок
```
Assets/
  Scripts/        игровой код, сборка Snake3D.asmdef
  Editor/         Editor-скрипты (SnakeSceneBuilder), сборка Snake3D.Editor.asmdef
  Materials/      материалы и текстуры (создаются SnakeSceneBuilder)
  Settings/       GameConfig.asset (баланс), Minimap.renderTexture
  Scenes/         Snake3D.unity
  Tests/          тесты Unity Test Framework (EditMode/PlayMode), свои asmdef
```

## Архитектура
Связующее звено — `GameManager`: режимы (Classic/TimeAttack/Survival), состояния Menu/Playing/Paused/GameOver,
уровни, еда, бонусы, опасности, соперник, статистика. Остальные системы не знают друг о друге.
- Баланс: `GameConfig` (ScriptableObject в Assets/Settings).
- Поле: `GridManager` (размер 44x44, препятствия по сиду, границы арены, клетки для предметов), `BoardView` (визуал),
  `HazardManager` + `SpinnerHazard` (балки уровней), `ShrinkingArena` (режим «Выживание»).
- Змейка: `SnakeController` (логика: движение, руль, прыжок, рывок, щит, длина, столкновения; траектория в кольцевом буфере),
  `SnakeBodyMesh` (процедурный меш-трубка), `SnakeHeadAnimator` (язык, моргание), `SnakeDeathEffect`, `DashTrail`, `SnakeAI` (соперник).
- Предметы: `FoodSpawner` (обычное/золотое/гнилое яблоко, магнит), `PowerUpSpawner` + `PowerUpService` (бонусы).
- Сервисы: `ScoreService`, `LeaderboardService` + `Leaderboard` (топ-10 по режимам), `SettingsService`, `AudioService` + `ProceduralAudio` (звук кодом).
- Отображение: `CameraRig` (третье лицо, крен, тряска), `UIController`, `SettingsPanel`, `LeaderboardPanel`, `TutorialController`.
- Управление: A/D или ←/→ — руль (держать), Space — прыжок, Shift — рывок, Esc — пауза, R — заново, F11 — полный экран.

Правила:
- Логика (позиции, траектория, столкновения) считается в данных, Transform только отображает результат.
- Связи между классами через ссылки в инспекторе или события (C# events / UnityEvent). Singleton только если без него нельзя, и с объяснением в комментарии.
- Не смешивай UI-код и игровую логику.

## Правила кода
- Поля инспектора: `[SerializeField] private`, с `[Header]` и `[Tooltip]`. Публичные поля без необходимости не делать.
- Запрещено в рантайме: `FindObjectOfType`, `GameObject.Find`, `GetComponent` в `Update` (кэшируй в `Awake`).
- Никаких аллокаций в `Update`/`FixedUpdate`: без LINQ, без `new` для списков и строк в горячем пути, без конкатенации строк каждый кадр.
- Сегменты тела и эффекты берём из пула объектов, не `Instantiate`/`Destroy` на каждый шаг.
- Подписки на события: подписка в `OnEnable`, отписка в `OnDisable`.
- Порядок инициализации: ссылки на себя и компоненты в `Awake`, связи с другими объектами в `Start`.
- Корутины используй осознанно, останавливай при отключении объекта.
- Имена: классы и методы `PascalCase`, приватные поля `_camelCase`, константы `UPPER_SNAKE` или `PascalCase`.
- Один класс — один файл, имя файла совпадает с именем класса.
- Комментарии краткие, объясняют «почему», а не «что».

## Версии и API
- Перед использованием API проверь, что оно существует в версии Unity проекта (см. `ProjectSettings/ProjectVersion.txt` и `Packages/manifest.json`).
- Не используй устаревшие API. Если сомневаешься, выбери более простой и стабильный способ.
- Учитывай выбранную систему ввода: не смешивай `Input.GetKey` и Input System, если включён только один бэкенд.

## Файлы, которые нельзя трогать
- Папки `Library/`, `Temp/`, `Logs/`, `obj/`, `UserSettings/`.
- Файлы `.meta`: не редактируй и не удаляй вручную. Новые создаёт Unity сам.
- Сцены и префабы (`.unity`, `.prefab`) вручную не правь. Меняй через Editor-скрипт или MCP.

## Проверка компиляции
После любых правок `.cs` проверь, что проект собирается:
```
Unity -batchmode -nographics -quit -projectPath . -logFile -
```
- Путь к исполняемому файлу Unity: переменная окружения `UNITY_EXE` (см. `tools/check-compile.ps1`).
- Редактор Unity на время команды должен быть закрыт, иначе проект заблокирован.
  Если редактор открыт — копируй проект (с Library, без Temp) во временную папку и запускай batch там.
- В Editor-коде `SettingsService` конфликтует с `UnityEditor.SettingsService` — используй псевдоним `using SettingsService = Snake3D.SettingsService;`.
- Если включат Input System, добавь ссылку `Unity.InputSystem` в оба asmdef.
- Если подключён Unity MCP, читай ошибки из консоли редактора вместо batch-запуска.
- Ошибки компиляции исправляй сразу, не оставляй их «на потом».

## Сборка сцены
- Сцена собирается Editor-скриптом `Assets/Editor/SnakeSceneBuilder.cs`, пункт меню `Tools/Build Snake Scene`.
- Скрипт создаёт сцену, текстуры, материалы, GameConfig, поле, змей, предметы, эффекты, звук, камеры, мини-карту и UI и сохраняет `Assets/Scenes/Snake3D.unity`. Batch: `-executeMethod SnakeSceneBuilder.Build`.
- Новые объекты сцены добавляй через этот скрипт, чтобы сцену можно было пересобрать с нуля одной командой.

## Порядок работы
1. Прочитай связанные файлы, прежде чем менять код.
2. Для задач больше одного файла сначала кратко опиши план.
3. Работай небольшими шагами, после каждого шага проверяй компиляцию.
4. Не делай рефакторинг, о котором не просили.
5. В конце сообщи: что изменено, как проверить в редакторе, какие допущения сделаны.
6. Если что-то неоднозначно, выбери разумное значение по умолчанию и перечисли допущения, а не останавливайся с вопросами.

## Git
- Коммит перед каждой крупной правкой, сообщения короткие и по делу.
- В репозиторий не добавляй `Library/`, `Temp/`, `Logs/`, `obj/`, `UserSettings/`.
- В настройках проекта должны быть включены Visible Meta Files и Force Text.

## Unity MCP
- Пакет: CoderGamester/mcp-unity (Unity 6+, Node.js 18+). Установка: Window > Package Manager > Add package from git URL > `https://github.com/CoderGamester/mcp-unity.git`, затем Tools > MCP Unity > Server Window > Configure для Claude Code > Start Server.
- Переменная окружения `UNITY_EXE` задаёт путь к Unity.exe для `tools/check-compile.ps1` (hook на правку `.cs`).
