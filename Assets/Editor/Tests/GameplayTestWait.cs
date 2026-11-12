using System.Collections;
using NUnit.Framework;
using UnityEngine;

namespace Gameplay.Tests
{
    internal static class GameplayTestWait
    {
        // These EditMode fixtures enter Play Mode. The 1.1.x runner can still drive
        // nested enumerators on EditorApplication.update, where WaitForSeconds is unsupported.
        public static IEnumerator ForSeconds(float seconds)
        {
            Assert.IsTrue(Application.isPlaying, "Gameplay timing requires Play Mode");
            var deadline = Time.time + seconds;
            var realDeadline = Time.realtimeSinceStartup + Mathf.Max(10f, seconds * 10f);
            while (Time.time < deadline)
            {
                Assert.Less(Time.realtimeSinceStartup, realDeadline, "Gameplay time stopped advancing");
                yield return null;
            }
        }
    }
}
