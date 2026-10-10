using NUnit.Framework;

namespace Snake3D.Tests
{
    /// <summary>Цели суток: выбор, прогресс, выполнение и награды.</summary>
    public class DailyGoalsTests
    {
        [Test]
        public void FirstDay_HasOneGoal_LaterTwoDistinct()
        {
            var goals = new DailyGoals();
            goals.Roll(1, new System.Random(1));
            Assert.AreEqual(1, goals.Count);

            for (int seed = 0; seed < 50; seed++)
            {
                goals.Roll(3, new System.Random(seed));
                Assert.AreEqual(DailyGoals.MAX_GOALS, goals.Count);
                Assert.AreNotEqual(goals.Get(0).Type, goals.Get(1).Type, "Цели одного дня не должны повторяться");
                Assert.AreEqual(0, goals.DoneCount);
            }
        }

        [Test]
        public void Report_CompletesGoal_OnlyOnce()
        {
            var goals = new DailyGoals();
            DailyGoal goal = RollUntil(goals, GoalType.CatchMice);
            Assert.AreEqual(3, goal.Target);
            int index = IndexOf(goals, GoalType.CatchMice);

            Assert.AreEqual(-1, goals.Report(GoalType.CatchMice));
            Assert.AreEqual(-1, goals.Report(GoalType.CatchMice));
            Assert.AreEqual(index, goals.Report(GoalType.CatchMice), "Третья мышь выполняет цель");
            Assert.IsTrue(goals.Get(index).Done);
            Assert.AreEqual(-1, goals.Report(GoalType.CatchMice), "Выполненная цель не засчитывается повторно");
            Assert.IsFalse(goals.Has(GoalType.CatchMice));
        }

        [Test]
        public void Report_UnrelatedEvent_DoesNothing()
        {
            var goals = new DailyGoals();
            goals.Roll(1, new System.Random(4));
            GoalType other = goals.Get(0).Type == GoalType.ScareFox ? GoalType.EvadeEagle : GoalType.ScareFox;
            Assert.AreEqual(-1, goals.Report(other));
            Assert.AreEqual(0, goals.DoneCount);
        }

        [Test]
        public void Rewards_HuntingGivesMolt_OtherRestores()
        {
            Assert.IsTrue(DailyGoals.GivesMolt(GoalType.AmbushBird));
            Assert.IsTrue(DailyGoals.GivesMolt(GoalType.ConstrictRabbit));
            Assert.IsFalse(DailyGoals.GivesMolt(GoalType.NightOutside));
            Assert.IsFalse(DailyGoals.GivesMolt(GoalType.ScareFox));
        }

        [Test]
        public void TypeCount_MatchesEnum()
        {
            Assert.AreEqual(System.Enum.GetValues(typeof(GoalType)).Length, DailyGoals.TYPE_COUNT);
        }

        private static DailyGoal RollUntil(DailyGoals goals, GoalType type)
        {
            for (int seed = 0; seed < 500; seed++)
            {
                goals.Roll(5, new System.Random(seed));
                int i = IndexOf(goals, type);
                if (i >= 0) return goals.Get(i);
            }
            Assert.Fail("Цель " + type + " так и не выпала");
            return default;
        }

        private static int IndexOf(DailyGoals goals, GoalType type)
        {
            for (int i = 0; i < goals.Count; i++)
                if (goals.Get(i).Type == type) return i;
            return -1;
        }
    }
}
