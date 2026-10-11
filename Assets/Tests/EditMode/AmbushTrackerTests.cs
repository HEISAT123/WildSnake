using NUnit.Framework;

namespace WildSnake.Tests
{
    /// <summary>Засада: маскировка после неподвижности, срыв движением, трава.</summary>
    public class AmbushTrackerTests
    {
        private static AmbushTracker Create()
        {
            return new AmbushTracker { Delay = 2f, Ramp = 1f, OpenAwareness = 0.4f, CoverAwareness = 0.1f };
        }

        [Test]
        public void NoConcealment_BeforeDelay()
        {
            AmbushTracker a = Create();
            a.Tick(1.9f, true);
            Assert.AreEqual(0f, a.Concealment01);
            Assert.AreEqual(1f, a.AwarenessMultiplier(false));
        }

        [Test]
        public void Concealment_Ramps_AfterDelay()
        {
            AmbushTracker a = Create();
            a.Tick(2.5f, true);
            Assert.AreEqual(0.5f, a.Concealment01, 1e-4f);
            Assert.IsFalse(a.IsLuring);
            a.Tick(1f, true);
            Assert.AreEqual(1f, a.Concealment01);
            Assert.IsTrue(a.IsLuring);
        }

        [Test]
        public void Moving_BreaksAmbush()
        {
            AmbushTracker a = Create();
            a.Tick(5f, true);
            a.Tick(0.1f, false);
            Assert.AreEqual(0f, a.Concealment01);
            a.Tick(5f, true);
            a.Break();
            Assert.AreEqual(0f, a.StillTime);
        }

        [Test]
        public void Cover_HidesBetter()
        {
            AmbushTracker a = Create();
            a.Tick(10f, true);
            Assert.AreEqual(0.4f, a.AwarenessMultiplier(false), 1e-4f);
            Assert.AreEqual(0.1f, a.AwarenessMultiplier(true), 1e-4f);
            Assert.Less(a.AwarenessMultiplier(true), a.AwarenessMultiplier(false));
        }
    }
}
