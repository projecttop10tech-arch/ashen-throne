using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using AshenThrone.Core;
using AshenThrone.UI;

namespace AshenThrone.Tests.UI
{
    [TestFixture]
    public class SceneTransitionOverlayTests
    {
        private GameObject _go;

        [SetUp]
        public void SetUp()
        {
            ServiceLocator.Initialize();
            EventBus.Initialize();
            _go = new GameObject("TransitionTest");
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_go);
            EventBus.Shutdown();
            ServiceLocator.Shutdown();
        }

        [Test]
        public void SceneTransitionOverlay_CreatesWithoutError()
        {
            Assert.DoesNotThrow(() => _go.AddComponent<SceneTransitionOverlay>());
        }

        [Test]
        public void SceneTransitionStartedEvent_DoesNotThrow()
        {
            _go.AddComponent<SceneTransitionOverlay>();
            Assert.DoesNotThrow(() =>
            {
                EventBus.Publish(new SceneTransitionStartedEvent(SceneName.Combat));
            });
        }

        [Test]
        public void SceneTransitionCompletedEvent_DoesNotThrow()
        {
            _go.AddComponent<SceneTransitionOverlay>();
            Assert.DoesNotThrow(() =>
            {
                EventBus.Publish(new SceneTransitionCompletedEvent(SceneName.Combat));
            });
        }
    }
}
