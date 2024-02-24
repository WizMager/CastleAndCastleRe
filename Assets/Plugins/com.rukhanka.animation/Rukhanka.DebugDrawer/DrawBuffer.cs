using System;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEngine;

/////////////////////////////////////////////////////////////////////////////////

namespace Rukhanka.DebugDrawer
{
public class DrawBuffer<T>: IDisposable where T: unmanaged
{
    internal GraphicsBuffer gpuBuffer;
    internal int counter;
    internal UnsafeAtomicCounter32 counterAtomic;
    
/////////////////////////////////////////////////////////////////////////////////

    public DrawBuffer()
    {
        unsafe
        {
            fixed (void* counterPtr = &counter)
            {
                counterAtomic = new UnsafeAtomicCounter32(counterPtr);
            }
        }
    }

/////////////////////////////////////////////////////////////////////////////////

    internal NativeArray<T> BeginFrame()
    {
        ResizeBuffer();
        counterAtomic.Reset();

        var rv = gpuBuffer.LockBufferForWrite<T>(0, gpuBuffer.count);
        return rv;
    }

/////////////////////////////////////////////////////////////////////////////////

    internal int EndFrame()
    {
        var cnt = math.min(gpuBuffer.count, counter);
        gpuBuffer.UnlockBufferAfterWrite<T>(cnt);
        return cnt;
    }

/////////////////////////////////////////////////////////////////////////////////

    void ResizeBuffer()
    {
        if (gpuBuffer == null || gpuBuffer.count < counter)
        {
            if (gpuBuffer != null)
                gpuBuffer.Dispose();
            var cnt = math.max(counter, 0xffff);
            gpuBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, GraphicsBuffer.UsageFlags.LockBufferForWrite, cnt, UnsafeUtility.SizeOf<T>());
        }
    }

/////////////////////////////////////////////////////////////////////////////////

    public void Dispose()
    {
        if (gpuBuffer != null)
            gpuBuffer.Dispose();
        gpuBuffer = null;
    }
}
}
