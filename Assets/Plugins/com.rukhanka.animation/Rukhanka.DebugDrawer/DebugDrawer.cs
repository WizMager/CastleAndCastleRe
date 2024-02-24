
using System;
using Unity.Burst;
using Unity.Mathematics;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using UnityEngine;

/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

namespace Rukhanka.DebugDrawer
{
[BurstCompile]
public struct Drawer: IComponentData
{
    [NativeDisableParallelForRestriction]
    internal NativeArray<DrawerManagedSingleton.LineData> lineData;
    [NativeDisableParallelForRestriction]
    internal NativeArray<DrawerManagedSingleton.TriangleData> triData;
    [NativeDisableParallelForRestriction]
    internal NativeArray<DrawerManagedSingleton.ThickLineData> thickLineData;
    [NativeDisableParallelForRestriction]
    internal NativeArray<DrawerManagedSingleton.BoneData> boneData;

    [NativeDisableUnsafePtrRestriction] 
    internal UnsafeAtomicCounter32 lineCounter;
    [NativeDisableUnsafePtrRestriction] 
    internal UnsafeAtomicCounter32 triCounter;
    [NativeDisableUnsafePtrRestriction] 
    internal UnsafeAtomicCounter32 thickLineCounter;
    [NativeDisableUnsafePtrRestriction] 
    internal UnsafeAtomicCounter32 boneMeshCounter;
    
/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

    public static Drawer Create(DrawerManagedSingleton ds)
    {
        var rv = new Drawer()
        {
            lineCounter = ds.linesBuf.counterAtomic,
            lineData = ds.lineData,
            triCounter = ds.trianglesBuf.counterAtomic,
            triData = ds.triData,
            thickLineCounter = ds.thickLinesBuf.counterAtomic,
            thickLineData = ds.thickLineData,
            boneMeshCounter = ds.bonesBuf.counterAtomic,
            boneData = ds.boneData
        };
        return rv;
    }
    
/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

    int GetTriangleWriteIndex(int numTrianglesToDraw) => GetWriteIndex(numTrianglesToDraw, triCounter, triData.Length);
    int GetLineWriteIndex(int numLinesToDraw) => GetWriteIndex(numLinesToDraw, lineCounter, lineData.Length);
    int GetThickLineWriteIndex(int numLinesToDraw) => GetWriteIndex(numLinesToDraw, thickLineCounter, thickLineData.Length);
    int GetBoneMeshWriteIndex(int numBonesToDraw) => GetWriteIndex(numBonesToDraw, boneMeshCounter, boneData.Length);
    
/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

    public static uint ColorToUINT(float4 cl)
    {
        uint rv = (uint)(cl.x * 255) << 24 | (uint)(cl.y * 255) << 16 | (uint)(cl.z * 255) << 8 | (uint)(cl.w * 255);
        return rv;
    }

/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

    public static uint ColorToUINT(Color cl)
    {
        uint rv = (uint)(cl.r * 255) << 24 | (uint)(cl.g * 255) << 16 | (uint)(cl.b * 255) << 8 | (uint)(cl.a * 255);
        return rv;
    }

/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

    int GetWriteIndex(int numPrimitivesToDraw, UnsafeAtomicCounter32 counter, int maxCount)
    {
        var writeIndex = counter.Add(numPrimitivesToDraw);
        
        if (writeIndex >= maxCount)
            return -1;

        return writeIndex;
    }

/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

    public void DrawThickLine(float3 p0, float3 p1, uint color, float thickness)
    {
        //  Make line as two triangles
        var writeIndex = GetThickLineWriteIndex(1);
        if (writeIndex < 0)
            return;

        var tlc = new DrawerManagedSingleton.ThickLineData()
        {
            p0 = p0,
            p1 = p1,
            thickness = thickness,
            color = color
        };

        thickLineData[writeIndex] = tlc;
    }

/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

    public void DrawRectangle(float2 xySize, uint color, in RigidTransform xform)
    {
        var writeIndex = GetTriangleWriteIndex(2);
        if (writeIndex < 0)
            return;
        
        var p0 = new float3(xySize, 0) * 0.5f;
        var p1 = new float3(xySize.x, -xySize.y, 0) * 0.5f;
        var p2 = new float3(-xySize, 0) * 0.5f;
        var p3 = new float3(-xySize.x, xySize.y, 0) * 0.5f;

        p0 = math.transform(xform, p0);
        p1 = math.transform(xform, p1);
        p2 = math.transform(xform, p2);
        p3 = math.transform(xform, p3);

        var t0 = new DrawerManagedSingleton.TriangleData()
        {
            p0 = p0,
            p1 = p1,
            p2 = p2,
            color = color
        };
        
        var t1 = new DrawerManagedSingleton.TriangleData()
        {
            p0 = p2,
            p1 = p3,
            p2 = p0,
            color = color
        };

        triData[writeIndex + 0] = t0;
        triData[writeIndex + 1] = t1;
    }

/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

    public void DrawCuboid(float3 size, uint color, in RigidTransform xform)
    {
        var halfSize = size * 0.5f;
        
        var f0c = math.transform(xform, new float3(halfSize.x, 0, 0));
        var f0s = size.yz;
        var r0 = quaternion.RotateY(math.PI * 0.5f);
        r0 = math.mul(quaternion.RotateX(math.PI * 0.5f), r0);
        r0 = math.mul(xform.rot, r0);
        DrawRectangle(f0s, color, new RigidTransform(r0, f0c));
        
        var f1c = math.transform(xform, new float3(-halfSize.x, 0, 0));
        var f1s = size.yz;
        var r1 = quaternion.RotateY(-math.PI * 0.5f);
        r1 = math.mul(quaternion.RotateX(math.PI * 0.5f), r1);
        r1 = math.mul(xform.rot, r1);
        DrawRectangle(f1s, color, new RigidTransform(r1, f1c));
        
        var f2c = math.transform(xform, new float3(0, halfSize.y, 0));
        var f2s = size.xz;
        var r2 = quaternion.RotateX(-math.PI * 0.5f);
        r2 = math.mul(xform.rot, r2);
        DrawRectangle(f2s, color, new RigidTransform(r2, f2c));
        
        var f3c = math.transform(xform, new float3(0, -halfSize.y, 0));
        var f3s = size.xz;
        var r3 = quaternion.RotateX(math.PI * 0.5f);
        r3 = math.mul(xform.rot, r3);
        DrawRectangle(f3s, color, new RigidTransform(r3, f3c));
        
        var f4c = math.transform(xform, new float3(0, 0, halfSize.z));
        var f4s = size.xy;
        DrawRectangle(f4s, color, new RigidTransform(xform.rot, f4c));
        
        var f5c = math.transform(xform, new float3(0, 0, -halfSize.z));
        var f5s = size.xy;
        var r5 = quaternion.RotateX(math.PI);
        r5 = math.mul(xform.rot, r5);
        DrawRectangle(f5s, color, new RigidTransform(r5, f5c));
    }

/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

    public void DrawWireCuboid(float3 size, uint color, in RigidTransform xform)
    {
        var writeIndex = GetLineWriteIndex(12);
        if (writeIndex < 0)
            return;
        
        Span<float3> corners = stackalloc float3[8];
        var halfSize = size * 0.5f;

        corners[0] = halfSize.xyz;
        corners[1] = new float3(halfSize.x, -halfSize.y, halfSize.z);
        corners[2] = new float3(-halfSize.x, -halfSize.y, halfSize.z);
        corners[3] = new float3(-halfSize.x, halfSize.y, halfSize.z);
        corners[4] = new float3(halfSize.x, halfSize.y, -halfSize.z);
        corners[5] = new float3(halfSize.x, -halfSize.y, -halfSize.z);
        corners[6] = new float3(-halfSize.x, -halfSize.y, -halfSize.z);
        corners[7] = new float3(-halfSize.x, halfSize.y, -halfSize.z);

        for (var i = 0; i < corners.Length; ++i)
        {
            corners[i] = math.transform(xform, corners[i]);
        }

        var l0 = new DrawerManagedSingleton.LineData() { p0 = corners[0], p1 = corners[1], color = color };
        lineData[writeIndex + 0] = l0;
        var l1 = new DrawerManagedSingleton.LineData() { p0 = corners[1], p1 = corners[2], color = color };
        lineData[writeIndex + 1] = l1;
        var l2 = new DrawerManagedSingleton.LineData() { p0 = corners[2], p1 = corners[3], color = color };
        lineData[writeIndex + 2] = l2;
        var l3 = new DrawerManagedSingleton.LineData() { p0 = corners[3], p1 = corners[0], color = color };
        lineData[writeIndex + 3] = l3;
        
        var l4 = new DrawerManagedSingleton.LineData() { p0 = corners[4], p1 = corners[5], color = color };
        lineData[writeIndex + 4] = l4;
        var l5 = new DrawerManagedSingleton.LineData() { p0 = corners[5], p1 = corners[6], color = color };
        lineData[writeIndex + 5] = l5;
        var l6 = new DrawerManagedSingleton.LineData() { p0 = corners[6], p1 = corners[7], color = color };
        lineData[writeIndex + 6] = l6;
        var l7 = new DrawerManagedSingleton.LineData() { p0 = corners[7], p1 = corners[4], color = color };
        lineData[writeIndex + 7] = l7;
        
        var l8 = new DrawerManagedSingleton.LineData() { p0 = corners[0], p1 = corners[4], color = color };
        lineData[writeIndex + 8] = l8;
        var l9 = new DrawerManagedSingleton.LineData() { p0 = corners[1], p1 = corners[5], color = color };
        lineData[writeIndex + 9] = l9;
        var l10 = new DrawerManagedSingleton.LineData() { p0 = corners[2], p1 = corners[6], color = color };
        lineData[writeIndex + 10] = l10;
        var l11 = new DrawerManagedSingleton.LineData() { p0 = corners[3], p1 = corners[7], color = color };
        lineData[writeIndex + 11] = l11;
    }

/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

    public void DrawWireTriangle(float3 p0, float3 p1, float3 p2, uint color)
    {
        var writeIndex = GetLineWriteIndex(3);
        if (writeIndex < 0)
            return;

        var l0 = new DrawerManagedSingleton.LineData()
        {
            p0 = p0,
            p1 = p1,
            color = color,
        };
        
        var l1 = new DrawerManagedSingleton.LineData()
        {
            p0 = p1,
            p1 = p2,
            color = color,
        };
        
        var l2 = new DrawerManagedSingleton.LineData()
        {
            p0 = p2,
            p1 = p0,
            color = color,
        };

        lineData[writeIndex + 0] = l0;
        lineData[writeIndex + 1] = l1;
        lineData[writeIndex + 2] = l2;
    }

/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

    public void DrawWirePyramid(float baseRadius, float apexHeight, int numBaseEdges, uint color, in RigidTransform xform)
    {
        if (numBaseEdges < 3)
            return;
        
        var writeIndex = GetLineWriteIndex(numBaseEdges * 2);
        if (writeIndex < 0)
            return;
        
        Span<float3> basePoints = stackalloc float3[numBaseEdges];
        CreateCircleEdgePointsXY(ref basePoints, baseRadius);
        DrawWireCircleInternal(basePoints, color, xform, writeIndex);
        
        var apex = math.transform(xform, new float3(0, 0, apexHeight));
        for (var i = 0; i < basePoints.Length; ++i)
        {
            var l = new DrawerManagedSingleton.LineData()
            {
                p0 = basePoints[i],
                p1 = apex,
                color = color
            };
            lineData[writeIndex + numBaseEdges + i] = l;
        }
    }

/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

    public void DrawPyramid(float baseRadius, float height, int numBaseEdges, uint color, in RigidTransform xform)
    {
        if (numBaseEdges < 3)
            return;
        
        var writeIndex = GetTriangleWriteIndex(numBaseEdges * 2);
        if (writeIndex < 0)
            return;
        
        Span<float3> basePoints = stackalloc float3[numBaseEdges];
        CreateCircleEdgePointsXY(ref basePoints, baseRadius);
        DrawCircleInternal(basePoints, color, xform, false, writeIndex);
        
        var apex = math.transform(xform, new float3(0, 0, height));
        for (var i = 0; i < basePoints.Length; ++i)
        {
            var t = new DrawerManagedSingleton.TriangleData()
            {
                p0 = basePoints[i],
                p1 = basePoints[(i + 1) % basePoints.Length],
                p2 = apex,
                color = color
            };
            triData[writeIndex + numBaseEdges + i] = t;
        }
    }

/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

    public void DrawFreeFormOctahedron(float radius, float height, float topBottomProportion, uint color, in RigidTransform xform)
    {
        var writeIndex = GetTriangleWriteIndex(8);
        if (writeIndex < 0)
            return;
        
        var heightVec = new float3(0, 0, height);
        var fwdVec = heightVec * topBottomProportion;
        var backVec = -heightVec * (1 - topBottomProportion);
        
        Span<float3> middlePoints = stackalloc float3[4];
        CreateCircleEdgePointsXY(ref middlePoints, radius);

        for (int i = 0; i < middlePoints.Length; ++i)
        {
            middlePoints[i] = math.transform(xform, middlePoints[i]);
        }

        fwdVec = math.transform(xform, fwdVec);
        backVec = math.transform(xform, backVec);

        for (var i = 0; i < middlePoints.Length; ++i)
        {
            var t0 = new DrawerManagedSingleton.TriangleData()
            {
                p0 = middlePoints[i],
                p1 = middlePoints[(i + 1) & 3],
                p2 = fwdVec,
                color = color
            };
            triData[writeIndex + i * 2] = t0;
            
            var t1 = new DrawerManagedSingleton.TriangleData()
            {
                p0 = middlePoints[(i + 1) & 3],
                p1 = middlePoints[i],
                p2 = backVec,
                color = color
            };
            triData[writeIndex + i * 2 + 1] = t1;
        }
    }

/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

    public void DrawBoneMesh(float3 pos0, float3 pos1, uint bodyColor, uint outlineColor)
    {
        var writeIndex = GetBoneMeshWriteIndex(1);
        if (writeIndex < 0)
            return;
        
        var bd = new DrawerManagedSingleton.BoneData()
        {
            pos0 = pos0,
            pos1 = pos1,
            colorLines = outlineColor,
            colorTri = bodyColor
        };

        boneData[writeIndex] = bd;
    }

/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

    void CreateCircleEdgePointsXY(ref Span<float3> pts, float radius)
    {
        var angleStep = math.PI * 2 / pts.Length;

        for (var i = 0; i < pts.Length; ++i)
        {
            math.sincos(i * angleStep, out var s, out var c);
            pts[i] = new float3(s, c, 0) * radius;
        }
    }

/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

    void DrawCircleInternal(Span<float3> pts, uint color, in RigidTransform xform, bool ccw, int writeIndex)
    {
        var center = xform.pos;
        for (var i = 0; i < pts.Length; ++i)
            pts[i] = math.transform(xform, pts[i]);

        for (var i = 0; i < pts.Length; ++i)
        {
            var p0 = pts[(i + 1) % pts.Length];
            var p1 = pts[i];
            var t = new DrawerManagedSingleton.TriangleData()
            {
                p0 = math.select(p0, p1, ccw),
                p1 = math.select(p0, p1, !ccw),
                p2 = center,
                color = color
            };
            triData[writeIndex + i] = t;
        }
    }

/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

    public void DrawCircle(float radius, int numEdges, uint color, in RigidTransform xform)
    {
        if (numEdges < 3)
            return;

        var writeIndex = GetTriangleWriteIndex(numEdges);
        if (writeIndex < 0)
            return;
        
        Span<float3> edgePoints = stackalloc float3[numEdges];
        CreateCircleEdgePointsXY(ref edgePoints, radius);
        DrawCircleInternal(edgePoints, color, xform, false, writeIndex);
    }

/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

    void DrawWireCircleInternal(Span<float3> pts, uint color, in RigidTransform xform, int writeIndex)
    {
        for (var i = 0; i < pts.Length; ++i)
            pts[i] = math.transform(xform, pts[i]);
        
        for (var i = 0; i < pts.Length; ++i)
        {
            var l = new DrawerManagedSingleton.LineData()
            {
                p0 = pts[i],
                p1 = pts[(i + 1) % pts.Length],
                color = color
            };
            lineData[writeIndex + i] = l;
        }
    }

/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

    public void DrawWireCircle(float radius, int numEdges, uint color, in RigidTransform xform)
    {
        if (numEdges < 3)
            return;
        
        var writeIndex = GetLineWriteIndex(numEdges);
        if (writeIndex < 0)
            return;
        
        Span<float3> edgePoints = stackalloc float3[numEdges];
        CreateCircleEdgePointsXY(ref edgePoints, radius);
        
        DrawWireCircleInternal(edgePoints, color, xform, writeIndex);
    }

/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

    public void DrawWireCylinder(float radius, float height, int numEdges, uint color, in RigidTransform xform)
    {
        if (numEdges < 3)
            return;

        var writeIndex = GetLineWriteIndex(numEdges * 3);
        if (writeIndex < 0)
            return;
        
        Span<float3> base0Points = stackalloc float3[numEdges];
        Span<float3> base1Points = stackalloc float3[numEdges];
        CreateCircleEdgePointsXY(ref base0Points, radius);
        base0Points.CopyTo(base1Points);

        var base1Offset = new float3(0, 0, height);
        var base1XForm = new RigidTransform(quaternion.identity, base1Offset);
        base1XForm = math.mul(xform, base1XForm);

        DrawWireCircleInternal(base0Points, color, xform, writeIndex);
        DrawWireCircleInternal(base1Points, color, base1XForm, writeIndex + numEdges);

        for (var i = 0; i < base0Points.Length; ++i)
        {
            var l = new DrawerManagedSingleton.LineData()
            {
                p0 = base0Points[i],
                p1 = base1Points[i],
                color = color
            };
            lineData[writeIndex + numEdges * 2 + i] = l;
        }
    }

/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

    public void DrawCylinder(float radius, float height, int numEdges, uint color, in RigidTransform xform)
    {
        if (numEdges < 3)
            return;

        var writeIndex = GetTriangleWriteIndex(numEdges * 4);
        
        Span<float3> base0Points = stackalloc float3[numEdges];
        Span<float3> base1Points = stackalloc float3[numEdges];
        CreateCircleEdgePointsXY(ref base0Points, radius);
        base0Points.CopyTo(base1Points);

        var base1Offset = new float3(0, 0, height);
        var base1XForm = new RigidTransform(quaternion.identity, base1Offset);
        base1XForm = math.mul(xform, base1XForm);

        DrawCircleInternal(base0Points, color, xform, false, writeIndex);
        DrawCircleInternal(base1Points, color, base1XForm, true, writeIndex + numEdges);

        for (var i = 0; i < base0Points.Length; ++i)
        {
            var t0 = new DrawerManagedSingleton.TriangleData()
            {
                p0 = base0Points[i],
                p1 = base0Points[(i + 1) % base0Points.Length],
                p2 = base1Points[(i + 1) % base0Points.Length],
                color = color
            };

            triData[writeIndex + numEdges * 2 + i * 2 + 0] = t0;
            
            var t1 = new DrawerManagedSingleton.TriangleData()
            {
                p0 = base1Points[(i + 1) % base0Points.Length],
                p1 = base1Points[i],
                p2 = base0Points[i],
                color = color
            };

            triData[writeIndex + numEdges * 2 + i * 2 + 1] = t1;
        }
    }

/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

    public void DrawTriangle(float3 p0, float3 p1, float3 p2, uint color)
    {
        var t = new DrawerManagedSingleton.TriangleData()
        {
            p0 = p0,
            p1 = p1,
            p2 = p2,
            color = color
        };

        var writeIndex = GetTriangleWriteIndex(1);
        if (writeIndex < 0)
            return;

        triData[writeIndex] = t;
    }

/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

    public void DrawLine(float3 p0, float3 p1, uint color)
    {
        var l = new DrawerManagedSingleton.LineData()
        {
            p0 = p0,
            p1 = p1,
            color = color
        };

        var writeIndex = GetLineWriteIndex(1);
        if (writeIndex < 0)
            return;

        lineData[writeIndex] = l;
    }
}
}
