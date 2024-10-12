/*
 * using LegendaryRenderer.EngineTypes;
using NUnit.Framework;
using OpenTK.Mathematics;

namespace LegendaryRenderer.Engine.UnitTests;

public class AABBTests
{
    [Test]
    public void TestAABBEncapsulate()
    {
        Vector3 minA = new Vector3(-22.33f, -5.25f, -9.0f);
        Vector3 maxA = new Vector3(22.223f, 1.53f, 5.4f);
        
        Vector3 minB = new Vector3(-10.5f, -16.6f, -8.2f);
        Vector3 maxB = new Vector3(8.43f, 3.5f, 12.43f);

        AABB testBounds = new AABB(minA, maxA);
        AABB testBounds2 = new AABB(minB, maxB);

        AABB bounds = new AABB(minA, maxA);
        
        bounds.Encapsulate(testBounds);
        bounds.Encapsulate(testBounds2);

        // Min test 
        
        Assert.AreEqual(minA.X, bounds.Min.X);
        Assert.AreEqual(minB.Y, bounds.Min.Y);
        Assert.AreEqual(minA.Z,bounds.Min.Z);
        
        // Max test

        Assert.AreEqual(maxA.X, bounds.Max.X);
        Assert.AreEqual(maxB.Y, bounds.Max.Y);
        Assert.AreEqual(maxB.Z, bounds.Max.Z);
    }

    [Test]
    public void TestAABBModify()
    {
        Vector3 minA = new Vector3(-6.3f, -2.5f, -2.0f);
        Vector3 maxA = new Vector3(12.6f, 3.46f, 8.1f);

        float growAmount = 5.3f;
        
        AABB testBounds = new AABB(minA, maxA);

        testBounds.Modify(growAmount);

        
        // Tests
        Assert.AreEqual(minA.X - growAmount, testBounds.Min.X);
        Assert.AreEqual(minA.Y - growAmount, testBounds.Min.Y);
        Assert.AreEqual(minA.Z - growAmount, testBounds.Min.Z);
        Assert.AreEqual(maxA.X + growAmount, testBounds.Max.X);
        Assert.AreEqual(maxA.Y + growAmount, testBounds.Max.Y);
        Assert.AreEqual(maxA.Z + growAmount, testBounds.Max.Z);
    }
}*/