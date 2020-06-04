using Unity.Mathematics;

namespace WorldGeneration {

public static class ExtensionMethods {

    public static int Pow (this int input, int power) {
        return (int) math.pow(input, power);
    }

}

}