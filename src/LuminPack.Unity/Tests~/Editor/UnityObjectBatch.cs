#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;

/// <summary>Provides a focused batch-mode entry point without invoking the full Unity test suite.</summary>
public static class UnityObjectBatch
{
    /// <summary>Executes native-owner tests and exits with a diagnostic status.</summary>
    public static void Run()
    {
        try { UnityObjectContract.RunAll(); EditorApplication.Exit(0); }
        catch (Exception error) { Debug.LogException(error); EditorApplication.Exit(1); }
    }
}
#endif
