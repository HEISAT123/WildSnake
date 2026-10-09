using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Snake3D.Tests
{
    /// <summary>
    /// Дымовые тесты сцены: каждый режим запускается и живёт пару секунд без ошибок в логе
    /// (Test Framework валит тест на LogType.Error/Exception), меню и Resume не ломают состояние.
    /// </summary>
    public class GameSmokeTests
    {
        private const string SCENE_NAME = "Snake3D";
        private const float RUN_SECONDS = 2f;

        private GameManager _game;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene(SCENE_NAME, LoadSceneMode.Single);
            // LoadScene завершается в следующем кадре, Start объектов сцены — ещё через кадр.
            yield return null;
            yield return null;

            Assert.AreEqual(SCENE_NAME, SceneManager.GetActiveScene().name, "Сцена не загрузилась");
            _game = Object.FindFirstObjectByType<GameManager>();
            Assert.IsNotNull(_game, "В сцене нет GameManager");
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Time.timeScale = 1f;
            yield return null;
        }

        [UnityTest]
        public IEnumerator Scene_StartsInMenu()
        {
            Assert.AreEqual(GameState.Menu, _game.State);
            Assert.AreEqual(1f, Time.timeScale);
            yield return null;
        }

        [UnityTest]
        public IEnumerator Classic_RunsWithoutErrors()
        {
            yield return RunMode(GameMode.Classic);
            if (_game.State == GameState.Playing)
                Assert.Greater(_game.Stats.Time, 0f, "Время партии не идёт");
        }

        [UnityTest]
        public IEnumerator TimeAttack_RunsWithoutErrors()
        {
            float before = -1f;
            yield return RunMode(GameMode.TimeAttack, () => before = _game.TimeLeft);
            Assert.IsTrue(_game.IsTimed);
            Assert.Greater(before, 0f, "Таймер режима «На время» не выставлен");
            if (_game.State == GameState.Playing)
                Assert.Less(_game.TimeLeft, before, "Таймер режима «На время» не уменьшается");
        }

        [UnityTest]
        public IEnumerator Survival_RunsWithoutErrors()
        {
            yield return RunMode(GameMode.Survival);
            Assert.IsFalse(_game.IsTimed);
        }

        [UnityTest]
        public IEnumerator Resume_WhilePlaying_DoesNothing()
        {
            _game.StartGame(GameMode.Classic);
            yield return null;
            _game.Resume();
            Assert.AreEqual(GameState.Playing, _game.State);
            Assert.AreEqual(1f, Time.timeScale);

            yield return new WaitForSeconds(0.5f);
            Assert.That(_game.State, Is.EqualTo(GameState.Playing).Or.EqualTo(GameState.GameOver));
        }

        [UnityTest]
        public IEnumerator Resume_InMenu_DoesNothing()
        {
            _game.Resume();
            Assert.AreEqual(GameState.Menu, _game.State);
            yield return null;
        }

        [UnityTest]
        public IEnumerator OpenMenu_AfterStart_ReturnsToMenu()
        {
            _game.StartGame(GameMode.Classic);
            yield return new WaitForSeconds(0.5f);

            _game.OpenMenu();
            Assert.AreEqual(GameState.Menu, _game.State);
            Assert.AreEqual(1f, Time.timeScale);

            yield return new WaitForSeconds(0.3f);
            Assert.AreEqual(GameState.Menu, _game.State, "Игра сама вышла из меню");
            Assert.AreEqual(1f, Time.timeScale);
        }

        [UnityTest]
        public IEnumerator Restart_FromAnyMode_StartsPlaying()
        {
            _game.StartGame(GameMode.Survival);
            yield return new WaitForSeconds(0.5f);
            _game.Restart();
            Assert.AreEqual(GameState.Playing, _game.State);
            Assert.AreEqual(GameMode.Survival, _game.Mode);
            Assert.AreEqual(1, _game.Level);
            Assert.AreEqual(-1, _game.LastRank);
            yield return new WaitForSeconds(0.5f);
        }

        [UnityTest]
        public IEnumerator SwitchingModes_InSequence_WorksWithoutErrors()
        {
            GameMode[] modes = { GameMode.Classic, GameMode.TimeAttack, GameMode.Survival, GameMode.Classic };
            foreach (GameMode mode in modes)
            {
                _game.StartGame(mode);
                Assert.AreEqual(mode, _game.Mode);
                Assert.AreEqual(GameState.Playing, _game.State);
                yield return new WaitForSeconds(0.5f);
                _game.OpenMenu();
                Assert.AreEqual(GameState.Menu, _game.State);
                yield return null;
            }
        }

        private IEnumerator RunMode(GameMode mode, System.Action afterStart = null)
        {
            _game.StartGame(mode);
            Assert.AreEqual(mode, _game.Mode);
            Assert.AreEqual(GameState.Playing, _game.State);
            Assert.AreEqual(1f, Time.timeScale);
            Assert.AreEqual(1, _game.Level);
            afterStart?.Invoke();

            yield return new WaitForSeconds(RUN_SECONDS);

            Assert.That(_game.State, Is.EqualTo(GameState.Playing).Or.EqualTo(GameState.GameOver),
                $"Неожиданное состояние после {RUN_SECONDS} с в режиме {mode}");
        }
    }
}
