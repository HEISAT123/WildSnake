# Assets/Editor — сборка сцены, UI и окружения

- `SnakeSceneBuilder` (`Tools/Build Snake Scene`, batch `-executeMethod SnakeSceneBuilder.Build`): создаёт сцену, текстуры, материалы,
  GameConfig, поле, змей, живность, эффекты, звук, камеру, UI и сохраняет `Assets/Scenes/WildSnake.unity`. Сцену создаёт первой
  (NewScene выгружает ассеты). Ассеты переиспользует по пути, чтобы не ломать ссылки; устаревшие — в `ObsoleteMaterials`/`DeleteObsoleteAssets`.
  Поля компонентов — через `Wire`/`WireArray`/`SetBool`/`SetFloat`/`SetInt`.
- `RenderPipelineSetup`: ассеты URP в `Assets/Settings/URP` (Low/Medium/High под 6 уровней Quality, общий рендерер, профиль с bloom),
  назначение в Graphics/Quality, `FixupMaterials` — ключевые слова материалов URP. Код под `#if SNAKE_URP` (versionDefines в asmdef).
  Свечение материалов: флаг `RealtimeEmissive`, иначе URP выключит `_EMISSION`. Прозрачность как Fade: `_BlendModePreserveSpecular = 0`.
- `EnvironmentBuilder` (небо, рельеф, лес, камни, трава, вода, облака, материалы арены), `ProceduralTextures`, `ProceduralMeshes`.
- Модели из кода: `MeshKit` (`MeshDraft` — эллипсоиды, лофты по кривой, пластины, гранёные капли; подмеш = материал;
  сохранение ассетами в `Assets/Meshes/Models` с сохранением GUID), `CreatureModels` (существа: одна подвижная часть — один меш;
  шарниры лап/крыльев/хвоста прежние — их анимируют скрипты), `NatureModels` (деревья ствол+крона вариантами, пни, брёвна по длине,
  дупла, ежевика, грибы, рогоз, гнёзда, цветы). У окклюдеров и крон — один материал на рендерер (CanopyFader меняет `sharedMaterial`).
  Объединённые меши BoardView хранятся в сцене — мелочь из сотен копий держать в паре сотен вершин.
- Шейдеры `Assets/Shaders` (только URP, без них — запасной Lit): `WildSnake/Water` (`EnvironmentBuilder.WaterMaterial`: волны, цвет по глубине
  из UV2, пена, рябь), `WildSnake/Grass` (`MakeCutout` переводит на него траву, камыш, папоротник: ветер, тень у корня, раздвигание).
- `ModelGallery` (`Tools/Capture Model Gallery`, batch `-executeMethod ModelGallery.Capture` без `-nographics`): листы PNG существ,
  природы и видов поля + `meshes.txt`; `Tools/Scene Render Stats` (`SceneStats`) — рендереры/вызовы/треугольники/тени по группам в `stats.txt` (треугольники ассетов и вершины мешей в сцене) в `Screenshots/` (не в git).
- `UIBuilder` строит весь интерфейс; `UISpriteFactory` рисует графику в `Assets/Materials/UI` (значки карты 128 px с mip-картами,
  мягкие тени-градиенты). Градиентные текстуры создавать один раз за сборку — пересохранение обнуляет ссылки.
  Стиль: Bahnschrift, чернильное затемнение слева поверх живой сцены, «костяной» текст, акцент — лайм, опасность — тлеющий красный;
  без карточек — заголовок с кикером, строки «01 02», тонкие линии, клавиши-подсказки; мелкий текст (≤22) без жирного.
- `GameBuilder` (`Tools/Build Game (Windows)`): Mono, результат в `Builds/`; иконка рисуется кодом в `Assets/Settings/GameIcon.png`.
- В Editor-коде `SettingsService` конфликтует с `UnityEditor.SettingsService` — `using SettingsService = WildSnake.SettingsService;`.
- Если включат Input System — добавить `Unity.InputSystem` в оба asmdef.
- Unity MCP (по желанию): пакет CoderGamester/mcp-unity; если подключён — ошибки читать из консоли редактора вместо batch.
