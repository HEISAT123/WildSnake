using NUnit.Framework;

namespace WildSnake.Tests
{
    /// <summary>Главная цель: повзрослеть, ночью и сытой отложить яйца, охранять кладку, вылупление — новое поколение.</summary>
    public class MainQuestTests
    {
        private static MainQuest Create()
        {
            var q = new MainQuest { BaseLength = 10f, LengthStep = 2f, TargetMolts = 2, NestSatiety = 0.6f, ClutchSize = 4, HatchSeconds = 10f };
            q.Reset();
            return q;
        }

        [Test]
        public void Grow_NeedsBothLengthAndMolts()
        {
            MainQuest q = Create();
            Assert.IsFalse(q.Update(12f, 1), "Одной длины мало — нужны линьки");
            Assert.IsFalse(q.Update(8f, 3), "Одних линек мало — нужна длина");
            Assert.AreEqual(QuestStage.Grow, q.Stage);
            Assert.IsTrue(q.Update(10f, 2));
            Assert.AreEqual(QuestStage.Nest, q.Stage);
            Assert.IsFalse(q.Update(15f, 5), "Повзрослеть можно только один раз");
        }

        [Test]
        public void GrowProgress_IsAverageOfLengthAndMolts()
        {
            MainQuest q = Create();
            Assert.AreEqual(0f, q.GrowProgress01(0f, 0), 1e-4f);
            Assert.AreEqual(0.5f, q.GrowProgress01(10f, 0), 1e-4f);
            Assert.AreEqual(0.75f, q.GrowProgress01(5f, 2), 1e-4f);
            Assert.AreEqual(1f, q.GrowProgress01(20f, 4), 1e-4f);
        }

        [Test]
        public void Lay_ExplainsWhatIsMissing()
        {
            MainQuest q = Create();
            Assert.AreEqual(NestCheck.NotGrown, q.CanLay(true, 1f));
            q.Update(10f, 2);
            Assert.AreEqual(NestCheck.NeedNight, q.CanLay(false, 1f));
            Assert.AreEqual(NestCheck.NeedFood, q.CanLay(true, 0.3f));
            Assert.AreEqual(NestCheck.Ok, q.CanLay(true, 0.8f));
        }

        [Test]
        public void Lay_CompletesQuestOnce()
        {
            MainQuest q = Create();
            q.Update(10f, 2);
            Assert.IsFalse(q.TryLay(false, 1f));
            Assert.IsTrue(q.TryLay(true, 1f));
            Assert.AreEqual(QuestStage.Guard, q.Stage);
            Assert.AreEqual(4, q.Eggs);
            Assert.AreEqual(NestCheck.AlreadyDone, q.CanLay(true, 1f));
            Assert.IsFalse(q.TryLay(true, 1f));
        }

        [Test]
        public void Hatch_StartsNextGeneration_WithHigherTarget()
        {
            MainQuest q = Create();
            q.Update(10f, 2);
            q.TryLay(true, 1f);
            Assert.IsFalse(q.TickHatch(6f));
            Assert.AreEqual(0.6f, q.HatchProgress01, 1e-4f);
            Assert.IsTrue(q.TickHatch(5f), "Через HatchSeconds кладка вылупляется");
            Assert.AreEqual(4, q.Hatch());
            Assert.AreEqual(2, q.Generation);
            Assert.AreEqual(QuestStage.Grow, q.Stage);
            Assert.AreEqual(12f, q.TargetLength, 1e-4f, "Следующее поколение должно вырасти больше");
            Assert.IsFalse(q.Update(11f, 2));
            Assert.IsTrue(q.Update(12f, 2));
        }

        [Test]
        public void LosingAllEggs_ReturnsToNest()
        {
            MainQuest q = Create();
            q.Update(10f, 2);
            q.TryLay(true, 1f);
            Assert.IsFalse(q.LoseEgg());
            Assert.IsFalse(q.LoseEgg());
            Assert.IsFalse(q.LoseEgg());
            Assert.IsTrue(q.LoseEgg(), "Последнее яйцо — кладка погибла");
            Assert.AreEqual(QuestStage.Nest, q.Stage);
            Assert.AreEqual(NestCheck.Ok, q.CanLay(true, 1f), "Можно отложить яйца снова");
            Assert.IsFalse(q.TickHatch(100f), "Без яиц ничего не вылупляется");
        }

        [Test]
        public void Restore_KeepsGenerationAndClutch()
        {
            MainQuest q = Create();
            q.Restore(QuestStage.Guard, 3, 2, 4f);
            Assert.AreEqual(3, q.Generation);
            Assert.AreEqual(2, q.Eggs);
            Assert.AreEqual(4f, q.HatchTime, 1e-4f);
            Assert.AreEqual(14f, q.TargetLength, 1e-4f);
        }

        [Test]
        public void Restore_SetsStage()
        {
            MainQuest q = Create();
            q.Restore(QuestStage.Nest);
            Assert.AreEqual(NestCheck.Ok, q.CanLay(true, 1f));
        }
    }
}
