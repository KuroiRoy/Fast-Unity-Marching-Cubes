using System;
using System.Diagnostics;
using UnityEngine;

namespace GeneralUtils {

public class FrameTimer : MonoBehaviour {

    private static FrameTimer Instance;

    public static double TimeElapsedThisFrame => Instance.stopwatch.Elapsed.TotalMilliseconds;

    private Stopwatch stopwatch;

    private void Awake () {
        Instance = this;
        stopwatch = Stopwatch.StartNew();
    }

    private void Update () {
        stopwatch.Restart();
    }

}

}