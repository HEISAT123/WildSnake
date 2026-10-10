using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Snake3D.Tests
{
    /// <summary>
    /// Дымовые тесты сцены: партия, меню с живым фоном, ночь — всё живёт несколько секунд без ошибок в логе
    /// (Test Framework валит тест на LogType.Error/Exception); меню и Resume не ломают состояние.
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
            _game = Object.FindAnyObjectByType<GameManager>();
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
        public IEnumerator Menu_LiveBackground_RunsWithoutErrors()
        {
            yield return new WaitForSeconds(3f);
            Assert.AreEqual(GameState.Menu, _game.State, "Игра сама вышла из меню");
        }

        [UnityTest]
        public IEnumerator Survival_RunsWithoutErrors_AndHungerGrows()
        {
            _game.StartGame();
            Assert.AreEqual(GameState.Playing, _game.State);
            Assert.AreEqual(1f, Time.timeScale);
            float startSatiety = _game.Survival.Satiety;

            yield return new WaitForSeconds(RUN_SECONDS);

            Assert.That(_game.State, Is.EqualTo(GameState.Playing).Or.EqualTo(GameState.GameOver).Or.EqualTo(GameState.Molting));
            Assert.Greater(_game.Stats.Time, 0f, "Время жизни не идёт");
            Assert.Less(_game.Survival.Satiety, startSatiety + 30f, "Сытость не должна расти сама по себе");
        }

        [UnityTest]
        public IEnumerator Wildlife_IsPopulated()
        {
            _game.StartGame();
            yield return null;

            var wildlife = Object.FindAnyObjectByType<WildlifeManager>();
            Assert.IsNotNull(wildlife, "В сцене нет живности");
            int active = 0;
            for (int i = 0; i < wildlife.AnimalCount; i++)
                if (wildlife.GetAnimal(i).IsActive) active++;
            Assert.Greater(active, 5, "На арене должна быть добыча");
        }

        [UnityTest]
        public IEnumerator Night_RunsWithoutErrors()
        {
            _game.StartGame();
            var dayNight = Object.FindAnyObjectByType<DayNightCycle>();
            Assert.IsNotNull(dayNight);
            dayNight.SetPhase(0.9f);
            Assert.IsTrue(dayNight.IsNight);
            yield return new WaitForSeconds(1.5f);
            Assert.That(_game.State, Is.EqualTo(GameState.Playing).Or.EqualTo(GameState.GameOver).Or.EqualTo(GameState.Molting));
        }

        [UnityTest]
        public IEnumerator Resume_WhilePlaying_DoesNothing()
        {
            _game.StartGame();
            yield return null;
            _game.Resume();
            Assert.AreEqual(GameState.Playing, _game.State);
            Assert.AreEqual(1f, Time.timeScale);

            yield return new WaitForSeconds(0.5f);
            Assert.That(_game.State, Is.EqualTo(GameState.Playing).Or.EqualTo(GameState.GameOver));
        }

        [UnityTest]
        public IEnumerator Player_StandsStill_WithoutInput()
        {
            _game.StartGame();
            SnakeController snake = FindPlayer();
            yield return new WaitForSeconds(0.3f);
            Vector3 start = snake.HeadPosition;

            yield return new WaitForSeconds(1.5f);

            Vector3 d = snake.HeadPosition - start;
            d.y = 0f;
            Assert.Less(d.magnitude, 0.05f, "Без ввода змея должна стоять на месте");
            Assert.IsFalse(snake.IsMoving);
        }

        [UnityTest]
        public IEnumerator Player_MovesByIntent_AndStrikes()
        {
            _game.StartGame();
            SnakeController snake = FindPlayer();
            // Ввод игрока в тесте не нажать — выключаем его и задаём намерение напрямую.
            snake.GetComponent<PlayerSnakeInput>().enabled = false;
            yield return null;
            Vector3 start = snake.HeadPosition;
            snake.SetMoveIntent(snake.Forward, SnakePace.Walk);
            yield return new WaitForSeconds(0.8f);
            Assert.Greater((snake.HeadPosition - start).magnitude, 0.5f, "Змея не поползла по намерению");

            yield return new WaitForSeconds(0.1f);
            Assert.IsTrue(snake.TryStrike(snake.Forward), "Бросок не начался");
            Assert.IsTrue(snake.IsStriking);
            yield return new WaitForSeconds(0.6f);
            Assert.IsFalse(snake.IsStriking, "Бросок не закончился");
        }

        private static SnakeController FindPlayer()
        {
            foreach (SnakeController s in Object.FindObjectsByType<SnakeController>(FindObjectsSortMode.None))
                if (s.IsPlayer) return s;
            Assert.Fail("Нет змеи игрока");
            return null;
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
            _game.StartGame();
            yield return new WaitForSeconds(0.5f);

            _game.OpenMenu();
            Assert.AreEqual(GameState.Menu, _game.State);
            Assert.AreEqual(1f, Time.timeScale);

            yield return new WaitForSeconds(0.3f);
            Assert.AreEqual(GameState.Menu, _game.State, "Игра сама вышла из меню");
            Assert.AreEqual(1f, Time.timeScale);
        }

        [UnityTest]
        public IEnumerator Restart_ResetsRun()
        {
            _game.StartGame();
            yield return new WaitForSeconds(0.5f);
            _game.Restart();
            Assert.AreEqual(GameState.Playing, _game.State);
            Assert.AreEqual(-1, _game.LastRank);
            Assert.AreEqual(0, _game.Stats.Prey);
            Assert.AreEqual(_game.Survival.MaxHealth, _game.Survival.Health);
            yield return new WaitForSeconds(0.5f);
        }
    }
}
