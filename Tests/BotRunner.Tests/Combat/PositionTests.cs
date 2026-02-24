using GameData.Core.Models;

namespace BotRunner.Tests.Combat
{
    public class PositionTests
    {
        // ======== Constructor ========

        [Fact]
        public void Constructor_SetsXYZ()
        {
            var pos = new Position(1.5f, 2.5f, 3.5f);
            Assert.Equal(1.5f, pos.X);
            Assert.Equal(2.5f, pos.Y);
            Assert.Equal(3.5f, pos.Z);
        }

        [Fact]
        public void Constructor_FromXYZ()
        {
            var xyz = new XYZ(10.0f, 20.0f, 30.0f);
            var pos = new Position(xyz);
            Assert.Equal(10.0f, pos.X);
            Assert.Equal(20.0f, pos.Y);
            Assert.Equal(30.0f, pos.Z);
        }

        [Fact]
        public void Constructor_ZeroValues()
        {
            var pos = new Position(0, 0, 0);
            Assert.Equal(0f, pos.X);
            Assert.Equal(0f, pos.Y);
            Assert.Equal(0f, pos.Z);
        }

        [Fact]
        public void Constructor_NegativeValues()
        {
            var pos = new Position(-100.5f, -200.3f, -50.1f);
            Assert.Equal(-100.5f, pos.X);
            Assert.Equal(-200.3f, pos.Y);
            Assert.Equal(-50.1f, pos.Z);
        }

        // ======== DistanceTo ========

        [Fact]
        public void DistanceTo_SamePoint_Zero()
        {
            var a = new Position(5, 10, 15);
            Assert.Equal(0f, a.DistanceTo(a));
        }

        [Fact]
        public void DistanceTo_AlongXAxis()
        {
            var a = new Position(0, 0, 0);
            var b = new Position(3, 0, 0);
            Assert.Equal(3.0f, a.DistanceTo(b), 0.001f);
        }

        [Fact]
        public void DistanceTo_AlongYAxis()
        {
            var a = new Position(0, 0, 0);
            var b = new Position(0, 4, 0);
            Assert.Equal(4.0f, a.DistanceTo(b), 0.001f);
        }

        [Fact]
        public void DistanceTo_AlongZAxis()
        {
            var a = new Position(0, 0, 0);
            var b = new Position(0, 0, 5);
            Assert.Equal(5.0f, a.DistanceTo(b), 0.001f);
        }

        [Fact]
        public void DistanceTo_3D_345Triangle()
        {
            // 3-4-5 in XY, Z=0 → distance = 5
            var a = new Position(0, 0, 0);
            var b = new Position(3, 4, 0);
            Assert.Equal(5.0f, a.DistanceTo(b), 0.001f);
        }

        [Fact]
        public void DistanceTo_IsSymmetric()
        {
            var a = new Position(1, 2, 3);
            var b = new Position(4, 6, 8);
            Assert.Equal(a.DistanceTo(b), b.DistanceTo(a), 0.001f);
        }

        [Fact]
        public void DistanceTo_IncludesZComponent()
        {
            var a = new Position(0, 0, 0);
            var b = new Position(1, 2, 2);
            // sqrt(1+4+4) = 3
            Assert.Equal(3.0f, a.DistanceTo(b), 0.001f);
        }

        // ======== DistanceTo2D ========

        [Fact]
        public void DistanceTo2D_IgnoresZ()
        {
            var a = new Position(0, 0, 0);
            var b = new Position(3, 4, 100);
            Assert.Equal(5.0f, a.DistanceTo2D(b), 0.001f);
        }

        [Fact]
        public void DistanceTo2D_SameXY_Zero()
        {
            var a = new Position(5, 10, 0);
            var b = new Position(5, 10, 999);
            Assert.Equal(0f, a.DistanceTo2D(b), 0.001f);
        }

        [Fact]
        public void DistanceTo2D_IsSymmetric()
        {
            var a = new Position(1, 2, 3);
            var b = new Position(4, 6, 100);
            Assert.Equal(a.DistanceTo2D(b), b.DistanceTo2D(a), 0.001f);
        }

        // ======== GetNormalizedVector ========

        [Fact]
        public void GetNormalizedVector_UnitX()
        {
            var pos = new Position(5, 0, 0);
            var norm = pos.GetNormalizedVector();
            Assert.Equal(1.0f, norm.X, 0.001f);
            Assert.Equal(0.0f, norm.Y, 0.001f);
            Assert.Equal(0.0f, norm.Z, 0.001f);
        }

        [Fact]
        public void GetNormalizedVector_UnitY()
        {
            var pos = new Position(0, 10, 0);
            var norm = pos.GetNormalizedVector();
            Assert.Equal(0.0f, norm.X, 0.001f);
            Assert.Equal(1.0f, norm.Y, 0.001f);
            Assert.Equal(0.0f, norm.Z, 0.001f);
        }

        [Fact]
        public void GetNormalizedVector_MagnitudeIsOne()
        {
            var pos = new Position(3, 4, 5);
            var norm = pos.GetNormalizedVector();
            var magnitude = (float)Math.Sqrt(norm.X * norm.X + norm.Y * norm.Y + norm.Z * norm.Z);
            Assert.Equal(1.0f, magnitude, 0.001f);
        }

        [Fact]
        public void GetNormalizedVector_NegativeInput()
        {
            var pos = new Position(-3, -4, 0);
            var norm = pos.GetNormalizedVector();
            Assert.Equal(-0.6f, norm.X, 0.001f);
            Assert.Equal(-0.8f, norm.Y, 0.001f);
        }

        // ======== Operators ========

        [Fact]
        public void Subtract_TwoPositions()
        {
            var a = new Position(5, 10, 15);
            var b = new Position(1, 3, 5);
            var result = a - b;
            Assert.Equal(4.0f, result.X);
            Assert.Equal(7.0f, result.Y);
            Assert.Equal(10.0f, result.Z);
        }

        [Fact]
        public void Add_TwoPositions()
        {
            var a = new Position(1, 2, 3);
            var b = new Position(4, 5, 6);
            var result = a + b;
            Assert.Equal(5.0f, result.X);
            Assert.Equal(7.0f, result.Y);
            Assert.Equal(9.0f, result.Z);
        }

        [Fact]
        public void Multiply_ByInt()
        {
            var pos = new Position(2, 3, 4);
            var result = pos * 3;
            Assert.Equal(6.0f, result.X);
            Assert.Equal(9.0f, result.Y);
            Assert.Equal(12.0f, result.Z);
        }

        [Fact]
        public void Multiply_ByZero()
        {
            var pos = new Position(5, 10, 15);
            var result = pos * 0;
            Assert.Equal(0f, result.X);
            Assert.Equal(0f, result.Y);
            Assert.Equal(0f, result.Z);
        }

        [Fact]
        public void Multiply_ByNegative()
        {
            var pos = new Position(2, 3, 4);
            var result = pos * -1;
            Assert.Equal(-2.0f, result.X);
            Assert.Equal(-3.0f, result.Y);
            Assert.Equal(-4.0f, result.Z);
        }

        [Fact]
        public void Operators_DoNotMutateOriginals()
        {
            var a = new Position(1, 2, 3);
            var b = new Position(4, 5, 6);
            _ = a + b;
            _ = a - b;
            _ = a * 5;
            Assert.Equal(1.0f, a.X);
            Assert.Equal(2.0f, a.Y);
            Assert.Equal(3.0f, a.Z);
        }

        // ======== ToXYZ ========

        [Fact]
        public void ToXYZ_ConvertsCorrectly()
        {
            var pos = new Position(1.5f, 2.5f, 3.5f);
            var xyz = pos.ToXYZ();
            Assert.Equal(1.5f, xyz.X);
            Assert.Equal(2.5f, xyz.Y);
            Assert.Equal(3.5f, xyz.Z);
        }

        // ======== ToVector3 ========

        [Fact]
        public void ToVector3_ConvertsCorrectly()
        {
            var pos = new Position(10, 20, 30);
            var v3 = pos.ToVector3();
            Assert.Equal(10.0f, v3.X);
            Assert.Equal(20.0f, v3.Y);
            Assert.Equal(30.0f, v3.Z);
        }

        // ======== ToString ========

        [Fact]
        public void ToString_FormatsCorrectly()
        {
            var pos = new Position(1.123f, 2.456f, 3.789f);
            var str = pos.ToString();
            Assert.Contains("X:", str);
            Assert.Contains("Y:", str);
            Assert.Contains("Z:", str);
        }

        [Fact]
        public void ToString_RoundsToTwoDecimals()
        {
            var pos = new Position(1.999f, 2.001f, 3.555f);
            var str = pos.ToString();
            Assert.Contains("2", str);   // 1.999 rounds to 2
            Assert.Contains("3.56", str); // 3.555 rounds to 3.56
        }

        // ======== XYZ struct ========

        [Fact]
        public void XYZ_Constructor_SetsFields()
        {
            var xyz = new XYZ(1.0f, 2.0f, 3.0f);
            Assert.Equal(1.0f, xyz.X);
            Assert.Equal(2.0f, xyz.Y);
            Assert.Equal(3.0f, xyz.Z);
        }
    }
}
