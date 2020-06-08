using System;
using UnityEngine;

namespace WorldGeneration {

[Serializable]
public class NoiseSettings {

    public float surfaceLevel;
    public float freq;
    public float ampl;
    public int oct;
    public Vector3 offset;

}

}