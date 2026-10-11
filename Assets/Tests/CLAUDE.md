# Assets/Tests — тесты

- EditMode — чистые C#-классы (`SurvivalStats`, `UpgradeSet`, `GridManager` и т. п.); PlayMode — `GameSmokeTests` на собранной сцене.
- Запуск в копии проекта: `-runTests -testPlatform EditMode|PlayMode -testResults <xml>` (PlayMode — без `-nographics`).
- Ввод в тестах не нажать: выключить `PlayerSnakeInput`, задавать намерение змеи напрямую (`SetMoveIntent`, `SetCoil`, `TryStrike`).
- Визуальная проверка — временный PlayMode-тест только в копии: `Camera.Render()` в RenderTexture → PNG; канвас перевести
  в ScreenSpaceCamera. Ждать через `WaitForSecondsRealtime` (линька и пауза ставят timeScale = 0).
- Копия проекта в Temp с длинным путём: URP не импортирует один свой шейдер (MAX_PATH) и пишет ошибку — во временных тестах
  `LogAssert.ignoreFailingMessages = true`. В самом проекте этой ошибки нет.
