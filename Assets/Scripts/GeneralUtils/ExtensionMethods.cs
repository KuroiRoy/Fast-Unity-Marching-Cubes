using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;

namespace GeneralUtils {

public static class ExtensionMethods {

    public static int Pow (this int input, int power) {
        return (int) math.pow(input, power);
    }

    public static unsafe void CopyToFast<T> (this NativeArray<T> source, T[] target) where T : struct {
        if (target == null) {
            throw new NullReferenceException(nameof(target) + " is null");
        }

        var nativeArrayLength = source.Length;
        if (target.Length < nativeArrayLength) {
            throw new IndexOutOfRangeException(nameof(target) + " is shorter than " + nameof(source));
        }

        var byteLength = source.Length * Marshal.SizeOf(default(T));
        var managedBuffer = UnsafeUtility.AddressOf(ref target[0]);
        var nativeBuffer = source.GetUnsafePtr();
        Buffer.MemoryCopy(nativeBuffer, managedBuffer, byteLength, byteLength);
    }

    public static void Deconstruct<T1, T2> (this KeyValuePair<T1, T2> pair, out T1 key, out T2 value) {
        key = pair.Key;
        value = pair.Value;
    }

}

}