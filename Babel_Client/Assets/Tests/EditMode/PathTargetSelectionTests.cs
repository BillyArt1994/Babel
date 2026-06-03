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
        public void ReserveBuildPoint_ExcludesCompletedAndOccupied()
        {
            CreatePathWithPointXs(1f, 10f, 20f, 30f);
            CompleteBuildPoint(2);
            SetOccupied(3, true);

            int reservedIndex = ReserveFrom(Vector3.zero);

            Assert.That(reservedIndex == 0 || reservedIndex == 1, Is.True);
            Assert.That(IsOccupied(reservedIndex), Is.True);
        }

        [Test]
        public void ReserveBuildPoint_WithAllAvailable_ReservesSomeValidPoint()
        {
            UnityEngine.Random.InitState(7);
            CreatePathWithPointXs(1f, 2f, 3f);

            int reservedIndex = ReserveFrom(Vector3.zero);

            Assert.That(reservedIndex, Is.InRange(0, 2));
            Assert.That(IsOccupied(reservedIndex), Is.True);
        }

        [Test]
        public void IsGatewayBuilt_TrueOnlyAfterGatewayPointCompleted()
        {
            CreatePathWithPointXs(1f, 2f);
            SetGateway(1, true);

            Assert.That(IsGatewayBuilt(), Is.False);

            CompleteBuildPoint(1);

            Assert.That(IsGatewayBuilt(), Is.True);
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
            MethodInfo method = _pathType.GetMethod("ReserveBuildPoint", new Type[] { typeof(Vector3) });
            Assert.That(method, Is.Not.Null, "Path.ReserveBuildPoint(Vector3) should remain public.");
            return (int)method.Invoke(_path, new object[] { fromPosition });
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

        private void SetGateway(int index, bool value)
        {
            FieldInfo f = _buildPointType.GetField("isGateway", BindingFlags.Instance | BindingFlags.Public);
            Assert.That(f, Is.Not.Null);
            f.SetValue(_buildPoints[index], value);
        }

        private bool IsGatewayBuilt()
        {
            MethodInfo m = _pathType.GetMethod("IsGatewayBuilt", BindingFlags.Instance | BindingFlags.Public);
            Assert.That(m, Is.Not.Null, "Path.IsGatewayBuilt should be public.");
            return (bool)m.Invoke(_path, null);
        }

        private static Type RequireType(string fullName)
        {
            Type type = Type.GetType($"{fullName}, Babel")
                      ?? Type.GetType($"{fullName}, Assembly-CSharp")
                      ?? Type.GetType(fullName);
            if (type == null)
            {
                foreach (var asm in System.AppDomain.CurrentDomain.GetAssemblies())
                {
                    type = asm.GetType(fullName);
                    if (type != null) break;
                }
            }
            Assert.That(type, Is.Not.Null, $"{fullName} should exist in a loaded assembly.");
            return type;
        }
    }
}
