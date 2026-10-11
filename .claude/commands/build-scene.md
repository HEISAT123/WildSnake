---
description: Пересобрать сцену WildSnake через Editor-скрипт Tools/Build Snake Scene
allowed-tools: Bash, PowerShell, Read
---
Пересобери сцену `Assets/Scenes/WildSnake.unity`.
1. Убедись, что есть `Assets/Editor/SnakeSceneBuilder.cs` с методом меню `Tools/Build Snake Scene`; если нет - сообщи и остановись.
2. Если подключён Unity MCP - выполни пункт меню `Tools/Build Snake Scene` через MCP и прочитай консоль.
3. Иначе (редактор закрыт): `& $env:UNITY_EXE -batchmode -nographics -quit -projectPath . -executeMethod SnakeSceneBuilder.Build -logFile -` (имя метода сверь с кодом).
4. Сообщи результат и ошибки, если были.
