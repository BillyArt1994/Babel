using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Babel.Tests
{
    public class TargetSelectorTests
    {
        [Test]
        public void DefaultBuildSelector_ReturnsOneOfTheCandidates()
        {
            Random.InitState(1);
            var selector = new DefaultBuildSelector();
            var candidates = new List<int> { 2, 5, 7 };
            int chosen = selector.Select(candidates, null, Vector3.zero);
            Assert.That(candidates, Does.Contain(chosen));
        }

        [Test]
        public void DefaultBuildSelector_WithEmptyCandidates_ReturnsMinusOne()
        {
            var selector = new DefaultBuildSelector();
            int chosen = selector.Select(new List<int>(), null, Vector3.zero);
            Assert.That(chosen, Is.EqualTo(-1));
        }

        [Test]
        public void GatewayFirstSelector_WhenGatewayInCandidates_SelectsGatewayIndex()
        {
            var pathGo = new GameObject("P");
            var path = pathGo.AddComponent<Path>();
            var bps = new BuildPoint[3];
            var bpGos = new GameObject[3];
            for (int i = 0; i < 3; i++)
            {
                bpGos[i] = new GameObject($"BP{i}");
                bpGos[i].transform.position = new Vector3(i, 0, 0);
                bps[i] = bpGos[i].AddComponent<BuildPoint>();
            }
            bps[1].isGateway = true;
            path.wayPointList = bps;

            var selector = new GatewayFirstSelector();
            int chosen = selector.Select(new List<int> { 0, 1, 2 }, path, Vector3.zero);

            Assert.That(chosen, Is.EqualTo(1));

            Object.DestroyImmediate(pathGo);
            for (int i = 0; i < 3; i++) Object.DestroyImmediate(bpGos[i]);
        }

        [Test]
        public void GatewayFirstSelector_WhenNoGatewayInCandidates_FallsBackToCandidate()
        {
            Random.InitState(1);
            var pathGo = new GameObject("P");
            var path = pathGo.AddComponent<Path>();
            var bps = new BuildPoint[3];
            var bpGos = new GameObject[3];
            for (int i = 0; i < 3; i++)
            {
                bpGos[i] = new GameObject($"BP{i}");
                bpGos[i].transform.position = new Vector3(i, 0, 0);
                bps[i] = bpGos[i].AddComponent<BuildPoint>();
            }
            bps[1].isGateway = true;
            path.wayPointList = bps;

            var selector = new GatewayFirstSelector();
            var candidates = new List<int> { 0, 2 };
            int chosen = selector.Select(candidates, path, Vector3.zero);

            Assert.That(candidates, Does.Contain(chosen));

            Object.DestroyImmediate(pathGo);
            for (int i = 0; i < 3; i++) Object.DestroyImmediate(bpGos[i]);
        }
    }
}
