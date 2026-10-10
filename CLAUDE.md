# CLAUDE.md — 3D Змейка (Unity)

Этот файл читается Claude Code в начале каждой сессии. Следуй ему всегда.

## Проект
- Игра: 3D-змейка-выживание в дикой природе: свободное движение, охота на живую добычу, хищники, голод, смена дня и ночи, линька с улучшениями. Одиночная (с ИИ-соперницей), без сети.
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
  Materials/      материалы и текстуры (создаются SnakeSceneBuilder), Environment/ — текстуры окружения
  Meshes/         сгенерированные меши окружения (рельеф, камни, кроны, трава, звёзды)
  Settings/       GameConfig.asset (баланс), Minimap.renderTexture
  Scenes/         Snake3D.unity
  Tests/          тесты Unity Test Framework (EditMode/PlayMode), свои asmdef
```

## Архитектура
Связующее звено — `GameManager`: один бесконечный режим выживания, состояния Menu/Playing/Paused/Molting/GameOver,
голод и здоровье, охота, удары, орёл, день и ночь, линька, соперница, статистика и рекорды. Остальные системы не знают друг о друге.
- Баланс: `GameConfig` (ScriptableObject в Assets/Settings): движение, здоровье и голод, нюх, линька, параметры добычи (`PreyStats` по `PreyKind`), сутки, орёл.
- Выживание: `SurvivalStats` (здоровье/сытость, чистый C#), `UpgradeSet` + `UpgradeType` (улучшения линьки, чистый C#), `DamageSource`, `RunStats`.
  Удары не убивают сразу: `SnakeController` отскакивает и шлёт `Hurt`, урон и смерть решает `GameManager`. Голод: сытость тает (ночью быстрее),
  на нуле тело худеет, потом идёт урон. Сытая змея лечится. Набрала `MoltGrowth` клеток роста — линька: пауза, выбор 1 из 3 улучшений, полное здоровье, `SkinShedEffect`.
- Поле: `GridManager` (44x44, препятствия по сиду, заросли высокой травы — укрытия `IsCover`, клетки для появления), `BoardView` (визуал:
  брёвна, пни и стоячие камни, валуны, высокая трава одним мешем).
- Змейка: `SnakeController` (движение, руль, прыжок, бросок, длина, столкновения с отскоком, `IsHidden` в траве; траектория в кольцевом буфере),
  `SnakeBodyMesh` (процедурный меш-трубка), `SnakeHeadAnimator` (язык, моргание), `SnakeDeathEffect`, `DashTrail`,
  `SnakeAI` (соперница и автопилот меню: A* по сетке с весами + рефлексы-щупальца + охота на выгодную добычу с упреждением и броском).
- Природа: `WildlifeManager` (пул `PreyAnimal`: жук, мышь, лягушка, птица; численность днём/ночью, поимка: жук — касанием, остальные — броском),
  `PreyAnimal` (бродит, замечает змею, бежит/улетает, устаёт), `EagleHunter` (кружит, пикирует; трава и бросок спасают),
  `DayNightCycle` (солнце, луна, звёзды, туман, облака, светлячки; счёт суток), `ScentTracker` (нюх: след к добыче и метки).
- Сервисы: `LeaderboardService` + `Leaderboard` (топ-10 по прожитому времени), `SettingsService`,
  `AudioService` + `SfxSynth` (эффекты) + `MusicSynth` (synthwave-петля) на общих блоках `SynthDsp`/`SynthRng`.
  Синтез идёт в фоновом потоке — в этих классах только System.Math, никакого Unity API.
- Окружение (Editor): `EnvironmentBuilder` (небо, звёзды, рельеф, лес, камни, трава, озеро, облака, пыльца), `ProceduralTextures`, `ProceduralMeshes`; рантайм — `CloudDrift`.
- Отображение: `CameraRig` (третье лицо, крен, тряска), `UIController` (HUD выживания и экраны), `SettingsPanel`, `LeaderboardPanel`, `GuidePanel` (справочник), `TutorialController`, `TimeFormat`.
- UI строит `UIBuilder` (Editor), графику рисует `UISpriteFactory` (`Assets/Materials/UI`). Стиль: крупная типографика Bahnschrift
  (системный шрифт через `UIFontApplier`), чернильное затемнение слева поверх живой сцены, «костяной» текст, единственный акцент — лайм.
  Никаких карточек: экраны — заголовок с кикером, нумерованные строки «01 02», тонкие линии, клавиши-подсказки. Мелкий текст (≤22) — без жирного.
- Списки (главное меню, пауза, линька, итоги) — `MenuList`: выбор стрелками/мышью, выезд выбранной строки, появление лесенкой, рекорд в сутках.
  Живой фон меню (attract-режим): в состоянии Menu змея игрока едет на автопилоте (`SnakeAI` с `_canJump = false`, `SnakeController.AutoPilot`),
  соперница тоже охотится; в меню нет урона, голода и звуков, рост змей ограничен, орёл только кружит. Время суток в меню — золотой час.
  Камера в меню — `CameraRig.Showcase` (облёт).
- HUD показывается только в состоянии Playing.
- Переключатели настроек — `ToggleSwitch` (вид «свитча» для Toggle).
- Курсор скрыт и заблокирован только в состоянии Playing (`UIController.ApplyCursor`); «Выйти» — `Application.Quit`, в редакторе останавливает Play Mode.
- Управление: A/D или ←/→ — руль (держать), Space — прыжок, Shift — бросок, E или ПКМ — нюх, Esc — пауза, R — заново, F11 — полный экран.

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
- `MaterialPropertyBlock` не сохраняется в сцене — то, что задаётся через него, применяй и в рантайме (см. `BoardView.ApplyFloorTiling`).
- Визуальная проверка: PlayMode-тест в копии проекта рендерит `Camera.Render()` в RenderTexture и сохраняет PNG (без `-nographics`).

## Порядок работы
1. Прочитай связанные файлы, прежде чем менять код.
2. Для задач больше одного файла сначала кратко опиши план.
3. Работай небольшими шагами, после каждого шага проверяй компиляцию.
4. Не делай рефакторинг, о котором не просили.
5. В конце сообщи: что изменено, как проверить в редакторе, какие допущения сделаны.
6. Если что-то неоднозначно, выбери разумное значение по умолчанию и перечисли допущения, а не останавливайся с вопросами.

## Сборка игры
- `Assets/Editor/GameBuilder.cs`, меню `Tools/Build Game (Windows)`; batch: `-executeMethod GameBuilder.BuildWindows`.
- Результат: `Builds/Snake3D_Windows/` (exe + Snake3D_Data + MonoBleedingEdge + dll) и `Builds/Snake3D_Windows.zip`. Папка `Builds/` в git не попадает.
- Бэкенд Mono (модуль IL2CPP не нужен). Иконка рисуется кодом в `Assets/Settings/GameIcon.png`.

## Git
- Коммит перед каждой крупной правкой, сообщения короткие и по делу.
- В репозиторий не добавляй `Library/`, `Temp/`, `Logs/`, `obj/`, `UserSettings/`.
- В настройках проекта должны быть включены Visible Meta Files и Force Text.

## Unity MCP
- Пакет: CoderGamester/mcp-unity (Unity 6+, Node.js 18+). Установка: Window > Package Manager > Add package from git URL > `https://github.com/CoderGamester/mcp-unity.git`, затем Tools > MCP Unity > Server Window > Configure для Claude Code > Start Server.
- Переменная окружения `UNITY_EXE` задаёт путь к Unity.exe для `tools/check-compile.ps1` (hook на правку `.cs`).
