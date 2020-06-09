using System.Collections.Generic;
using Unity.Mathematics;

namespace WorldGeneration {

public static class ExtensionMethods {

    public static int Pow (this int input, int power) {
        return (int) math.pow(input, power);
    }
    
    public static void Deconstruct<T1, T2> (this KeyValuePair<T1, T2> pair, out T1 key, out T2 value) {
        key   = pair.Key;
        value = pair.Value;
    }

}

}