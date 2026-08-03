using System;

namespace Babel.Foundation
{
    public readonly struct Float2 : IEquatable<Float2>
    {
        public static Float2 Zero => default;

        public Float2(float x, float y)
        {
            X = x;
            Y = y;
        }

        public float X { get; }
        public float Y { get; }
        public float LengthSquared => (X * X) + (Y * Y);

        public bool Equals(Float2 other) => X.Equals(other.X) && Y.Equals(other.Y);
        public override bool Equals(object obj) => obj is Float2 other && Equals(other);

        public override int GetHashCode()
        {
            unchecked { return (X.GetHashCode() * 397) ^ Y.GetHashCode(); }
        }

        public static Float2 operator +(Float2 left, Float2 right) => new Float2(left.X + right.X, left.Y + right.Y);
        public static Float2 operator -(Float2 left, Float2 right) => new Float2(left.X - right.X, left.Y - right.Y);
        public static Float2 operator *(Float2 value, float scalar) => new Float2(value.X * scalar, value.Y * scalar);
        public static bool operator ==(Float2 left, Float2 right) => left.Equals(right);
        public static bool operator !=(Float2 left, Float2 right) => !left.Equals(right);
        public override string ToString() => $"({X}, {Y})";
    }
}
