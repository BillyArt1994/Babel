using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace Babel.Tests
{
    public class PathTargetSelectionTests
    {
        private Type _pathType;
        private Type _buildPointType;
        private Component _path;
        private Component[] _buildPoints = Array.Empty<Component>();
        private GameObject _pathObject;
        private GameObject[] _buildPointObjects = Array.Empty<GameObject>();

        [TearDown]
        public void TearDown()
        {
            if (_pathObject != null)
            {
                UnityEngine.Object.DestroyImmediate(_pathObject);
            }

            for (int i = 0; i < _buildPointObjects.Length; i++)
            {
                if (_buildPointObjects[i] != null)
                {
                    UnityEngine.Object.DestroyImmediate(_buildPointObjects[i]);
                }
            }

            _path = null;
            _pathObject = null;
            _buildPoints = Array.Empty<Component>();
            _buildPointObjects = Array.Empty<GameObject>();
        }

        [Test]
        public void GetFarthestSelectionCount_UsesFloorHalfWithMinimumOne()
        {
            _pathType = RequireType("Babel.Path");

            Assert.That(GetFarthestSelectionCount(0), Is.EqualTo(0));
            Assert.That(GetFarthestSelectionCount(1), Is.EqualTo(1));
            Assert.That(GetFarthestSelectionCount(2), Is.EqualTo(1));
            Assert.That(GetFarthestSelectionCount(3), Is.EqualTo(1));
            Assert.That(GetFarthestSelectionCount(4), Is.EqualTo(2));
            Assert.That(GetFarthestSelectionCount(5), Is.EqualTo(2));
            Assert.That(GetFarthestSelectionCount(6), Is.EqualTo(3));
        }

        [Test]
        public void ReserveBuildPoint_WithThreeCandidates_ReservesFarthestPoint()
        {
            CreatePathWithPointXs(1f, 2f, 3f);

            int reservedIndex = ReserveFrom(Vector3.zero);

            Assert.That(reservedIndex, Is.EqualTo(2));
            Assert.That(IsOccupied(2), Is.True);
            Assert.That(IsOccupied(0), Is.False);
            Assert.That(IsOccupied(1), Is.False);
        }

        [Test]
        public void ReserveBuildPoint_WithFourCandidates_ReservesOnlyFromFarthestHalf()
        {
            UnityEngine.Random.InitState(12345);
            CreatePathWithPointXs(1f, 2f, 3f, 4f);

            int reservedIndex = ReserveFrom(Vector3.zero);

            Assert.That(reservedIndex == 2 || reservedIndex == 3, Is.True);
            Assert.That(IsOccupied(reservedIndex), Is.True);
            Assert.That(IsOccupied(0), Is.False);
            Assert.That(IsOccupied(1), Is.False);
        }

        [Test]
        public void ReserveBuildPoint_ExcludesCompletedAndOccupiedBeforeChoosingFarthestHalf()
        {
            CreatePathWithPointXs(1f, 10f, 20f, 30f);
            CompleteBuildPoint(2);
            SetOccupied(3, true);

            int reservedIndex = ReserveFrom(Vector3.zero);

            Assert.That(reservedIndex, Is.EqualTo(1));
            Assert.That(IsOccupied(1), Is.True);
            Assert.That(IsOccupied(0), Is.False);
        }

        private void CreatePathWithPointXs(params float[] xPositions)
        {
            _pathType = RequireType("Babel.Path");
            _buildPointType = RequireType("Babel.BuildPoint");
            _pathObject = new GameObject("PathTargetSelectionTest_Path");
            _path = _pathObject.AddComponent(_pathType);
            _buildPoints = new Component[xPositions.Length];
            _buildPointObjects = new GameObject[xPositions.Length];

            Array wayPoints = Array.CreateInstance(_buildPointType, xPositions.Length);
            for (int i = 0; i < xPositions.Length; i++)
            {
                _buildPointObjects[i] = new GameObject($"PathTargetSelectionTest_BuildPoint_{i}");
                _buildPointObjects[i].transform.position = new Vector3(xPositions[i], 0f, 0f);
                Component buildPoint = _buildPointObjects[i].AddComponent(_buildPointType);
                _buildPoints[i] = buildPoint;
                wayPoints.SetValue(buildPoint, i);
            }

            FieldInfo wayPointListField = _pathType.GetField("wayPointList", BindingFlags.Instance | BindingFlags.Public);
            Assert.That(wayPointListField, Is.Not.Null, "Path.wayPointList should remain a public field.");
            wayPointListField.SetValue(_path, wayPoints);
        }

        private int ReserveFrom(Vector3 fromPosition)
        {
            MethodInfo method = _pathType.GetMethod("ReserveBuildPoint", BindingFlags.Instance | BindingFlags.Public);
            Assert.That(method, Is.Not.Null, "Path.ReserveBuildPoint should remain public.");
            return (int)method.Invoke(_path, new object[] { fromPosition });
        }

        private int GetFarthestSelectionCount(int candidateCount)
        {
            MethodInfo method = _pathType.GetMethod(
                "GetFarthestSelectionCount",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, "Path should expose a private testable farthest-half count helper.");
            return (int)method.Invoke(null, new object[] { candidateCount });
        }

        private bool IsOccupied(int index)
        {
            PropertyInfo property = _buildPointType.GetProperty("IsOccupied", BindingFlags.Instance | BindingFlags.Public);
            Assert.That(property, Is.Not.Null, "BuildPoint.IsOccupied should remain public.");
            return (bool)property.GetValue(_buildPoints[index]);
        }

        private void SetOccupied(int index, bool occupied)
        {
            MethodInfo method = _buildPointType.GetMethod("SetOccupied", BindingFlags.Instance | BindingFlags.Public);
            Assert.That(method, Is.Not.Null, "BuildPoint.SetOccupied should remain public.");
            method.Invoke(_buildPoints[index], new object[] { occupied });
        }

        private void CompleteBuildPoint(int index)
        {
            MethodInfo method = _buildPointType.GetMethod("AddBuildProgress", BindingFlags.Instance | BindingFlags.Public);
            Assert.That(method, Is.Not.Null, "BuildPoint.AddBuildProgress should remain public.");
            method.Invoke(_buildPoints[index], new object[] { 9999 });
        }

        private static Type RequireType(string fullName)
        {
            Type type = Type.GetType($"{fullName}, Assembly-CSharp");
            Assert.That(type, Is.Not.Null, $"{fullName} should exist in Assembly-CSharp.");
            return type;
        }
    }
}
