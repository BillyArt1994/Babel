using System;
using System.Collections;
using System.Collections.Generic;
using Babel.Unity.Presentation.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Babel.Tests
{
    public sealed class ScreenRouterPlayModeTests
    {
        private readonly List<GameObject> _objects = new List<GameObject>();
        private ScreenRouter _router;

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (_router != null && !_router.IsDisposed)
                _router.Dispose();
            _router = null;

            for (int i = 0; i < _objects.Count; i++)
            {
                if (_objects[i] != null) Object.Destroy(_objects[i]);
            }
            _objects.Clear();
            yield return null;
        }

        [Test]
        public void ShowAndHide_UseExplicitRegistrationAndVisibility()
        {
            ScreenRouter router = CreateRouter();
            ProbeScreen menu = CreateScreen("Menu");
            router.Register("menu", menu);

            Assert.That(menu.gameObject.activeSelf, Is.False);
            Assert.That(router.Show("menu"), Is.True);
            Assert.That(router.CurrentId, Is.EqualTo("menu"));
            Assert.That(router.CurrentScreen, Is.SameAs(menu));
            Assert.That(menu.IsVisible, Is.True);
            Assert.That(menu.gameObject.activeSelf, Is.True);
            Assert.That(menu.ShownCount, Is.EqualTo(1));

            Assert.That(router.Hide("menu"), Is.True);
            Assert.That(router.CurrentId, Is.Null);
            Assert.That(menu.IsVisible, Is.False);
            Assert.That(menu.gameObject.activeSelf, Is.False);
            Assert.That(menu.HiddenCount, Is.EqualTo(1));
            Assert.That(router.Hide("menu"), Is.False);
        }

        [Test]
        public void Show_SwitchesScreensAndRepeatedCurrentShowIsIdempotent()
        {
            ScreenRouter router = CreateRouter();
            ProbeScreen first = CreateScreen("First");
            ProbeScreen second = CreateScreen("Second");
            router.Register("first", first);
            router.Register("second", second);

            Assert.That(router.Show("first"), Is.True);
            Assert.That(router.Show("first"), Is.False);
            Assert.That(first.ShownCount, Is.EqualTo(1));
            Assert.That(router.HistoryDepth, Is.Zero);

            Assert.That(router.Show("second"), Is.True);
            Assert.That(first.IsVisible, Is.False);
            Assert.That(second.IsVisible, Is.True);
            Assert.That(first.HiddenCount, Is.EqualTo(1));
            Assert.That(second.ShownCount, Is.EqualTo(1));
            Assert.That(router.CurrentId, Is.EqualTo("second"));
            Assert.That(router.HistoryDepth, Is.EqualTo(1));
        }

        [Test]
        public void Back_RestoresScreensInLastShownOrderWithoutGrowingHistory()
        {
            ScreenRouter router = CreateRouter();
            ProbeScreen first = CreateScreen("First");
            ProbeScreen second = CreateScreen("Second");
            ProbeScreen third = CreateScreen("Third");
            router.Register("first", first);
            router.Register("second", second);
            router.Register("third", third);

            router.Show("first");
            router.Show("second");
            router.Show("third");

            Assert.That(router.HistoryDepth, Is.EqualTo(2));
            Assert.That(router.Back(), Is.True);
            Assert.That(router.CurrentId, Is.EqualTo("second"));
            Assert.That(router.HistoryDepth, Is.EqualTo(1));
            Assert.That(second.ShownCount, Is.EqualTo(2));

            Assert.That(router.Back(), Is.True);
            Assert.That(router.CurrentId, Is.EqualTo("first"));
            Assert.That(router.HistoryDepth, Is.Zero);
            Assert.That(first.ShownCount, Is.EqualTo(2));
            Assert.That(router.Back(), Is.False);
        }

        [Test]
        public void ScreenDisableRouterDisableAndDispose_CleanVisibleSubscriptionsAndState()
        {
            ScreenRouter router = CreateRouter();
            ProbeScreen screen = CreateScreen("Screen");
            router.Register("screen", screen);
            router.Show("screen");

            screen.gameObject.SetActive(false);

            Assert.That(screen.IsVisible, Is.False);
            Assert.That(screen.HiddenCount, Is.EqualTo(1));
            Assert.That(screen.UnsubscribeCount, Is.EqualTo(1));
            Assert.That(router.Show("screen"), Is.True, "The current route can recover an externally disabled screen.");
            Assert.That(screen.ShownCount, Is.EqualTo(2));

            router.enabled = false;

            Assert.That(screen.IsVisible, Is.False);
            Assert.That(screen.UnsubscribeCount, Is.EqualTo(2));
            Assert.That(router.CurrentId, Is.Null);
            Assert.That(router.HistoryDepth, Is.Zero);
            Assert.That(router.RegistrationCount, Is.EqualTo(1));

            router.enabled = true;
            router.Show("screen");
            router.Dispose();

            Assert.That(router.IsDisposed, Is.True);
            Assert.That(router.RegistrationCount, Is.Zero);
            Assert.That(screen.IsVisible, Is.False);
            Assert.That(screen.UnsubscribeCount, Is.EqualTo(3));
            Assert.That(screen.IsDisposed, Is.False, "The router does not own registered screen objects.");
            Assert.Throws<ObjectDisposedException>(() => router.Show("screen"));
        }


        [Test]
        public void ScreenDispose_ReleasesSubscriptionsAndRemovesRouterRegistration()
        {
            ScreenRouter router = CreateRouter();
            ProbeScreen screen = CreateScreen("Disposable Screen");
            router.Register("disposable", screen);
            router.Show("disposable");

            screen.Dispose();

            Assert.That(screen.IsDisposed, Is.True);
            Assert.That(screen.IsVisible, Is.False);
            Assert.That(screen.UnsubscribeCount, Is.EqualTo(1));
            Assert.That(router.RegistrationCount, Is.Zero);
            Assert.That(router.CurrentId, Is.Null);
            Assert.Throws<KeyNotFoundException>(() => router.Show("disposable"));
        }

        private ScreenRouter CreateRouter()
        {
            GameObject host = Track(new GameObject("Screen Router"));
            _router = host.AddComponent<ScreenRouter>();
            return _router;
        }

        private ProbeScreen CreateScreen(string name)
        {
            GameObject host = Track(new GameObject(name));
            return host.AddComponent<ProbeScreen>();
        }

        private GameObject Track(GameObject instance)
        {
            _objects.Add(instance);
            return instance;
        }

        private sealed class ProbeScreen : Babel.Unity.Presentation.UI.Screen
        {
            public int ShownCount { get; private set; }
            public int HiddenCount { get; private set; }
            public int UnsubscribeCount { get; private set; }

            protected override void OnScreenShown()
            {
                ShownCount++;
                VisibilitySubscriptions.Add(() => UnsubscribeCount++);
            }

            protected override void OnScreenHidden()
            {
                HiddenCount++;
            }
        }
    }
}
