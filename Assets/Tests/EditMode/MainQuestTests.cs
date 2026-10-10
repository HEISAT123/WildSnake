using NUnit.Framework;

namespace Snake3D.Tests
{
    /// <summary>Главная цель: повзрослеть, затем ночью и сытой отложить яйца в гнезде.</summary>
    public class MainQuestTests
    {
        private static MainQuest Create()
        {
            var q = new MainQuest { TargetLength = 10f, TargetMolts = 2, NestSatiety = 0.6f };
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
            Assert.AreEqual(QuestStage.Done, q.Stage);
            Assert.AreEqual(NestCheck.AlreadyDone, q.CanLay(true, 1f));
            Assert.IsFalse(q.TryLay(true, 1f));
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
