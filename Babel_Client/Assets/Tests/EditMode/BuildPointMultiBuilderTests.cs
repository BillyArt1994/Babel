using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Babel.Tests
{
    public class BuildPointMultiBuilderTests
    {
        private GameObject _go;
        private BuildPoint _bp;

        [SetUp]
        public void SetUp()
        {
            _go = new GameObject("BP");
            _bp = _go.AddComponent<BuildPoint>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_go);
        }

        private class FakeBuilder : IBuildInterruptible
        {
            public List<BuildPoint> Interrupted = new List<BuildPoint>();
            public void OnTargetBuildCompleted(BuildPoint point) => Interrupted.Add(point);
        }

        [Test]
        public void AttachBuilder_OnBuildComplete_CallsOnTargetBuildCompleted()
        {
            var b1 = new FakeBuilder();
            var b2 = new FakeBuilder();
            _bp.AttachBuilder(b1);
            _bp.AttachBuilder(b2);

            _bp.AddBuildProgress(99999); // 触发完成

            Assert.That(b1.Interrupted, Has.Count.EqualTo(1));
            Assert.That(b2.Interrupted, Has.Count.EqualTo(1));
            Assert.That(b1.Interrupted[0], Is.SameAs(_bp));
        }

        [Test]
        public void DetachBuilder_NotCalledAfterDetach()
        {
            var b1 = new FakeBuilder();
            _bp.AttachBuilder(b1);
            _bp.DetachBuilder(b1);

            _bp.AddBuildProgress(99999);

            Assert.That(b1.Interrupted, Is.Empty);
        }

        [Test]
        public void MultipleBuilders_AllNotifiedOnce()
        {
            const int builderCount = 5;
            var builders = new FakeBuilder[builderCount];
            for (int i = 0; i < builderCount; i++)
            {
                builders[i] = new FakeBuilder();
                _bp.AttachBuilder(builders[i]);
            }

            _bp.AddBuildProgress(99999);

            for (int i = 0; i < builderCount; i++)
                Assert.That(builders[i].Interrupted, Has.Count.EqualTo(1), $"builder[{i}] not notified");
        }

        [Test]
        public void AttachBuilder_DuplicateIgnored()
        {
            var b = new FakeBuilder();
            _bp.AttachBuilder(b);
            _bp.AttachBuilder(b); // 重复注册

            _bp.AddBuildProgress(99999);

            // 只被通知一次
            Assert.That(b.Interrupted, Has.Count.EqualTo(1));
        }

        [Test]
        public void Reset_ClearsActiveBuilders()
        {
            var b = new FakeBuilder();
            _bp.AttachBuilder(b);
            _bp.Reset();

            // Reset 后建造完成不应再通知
            _bp.AddBuildProgress(99999);
            Assert.That(b.Interrupted, Is.Empty);
        }
    }
}
