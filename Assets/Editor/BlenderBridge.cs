using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using UnityEngine;
using Debug = UnityEngine.Debug;

// Reusable helper for invoking headless Blender scripts from inside the Unity
// Editor. Exists so that every future Blender call is a one-line call to an
// already-tested method instead of freshly-authored Process.Start boilerplate
// (less chance of typos/bugs each time), and so the Blender process runs
// asynchronously - a long bake/export no longer risks freezing the Editor's
// main thread the way a blocking Process.WaitForExit() call would.
//
// Typical usage from an MCP execute_code call:
//   BlenderBridge.RunScript("BlenderTools/fix_uv_layers.py", "\"Assets/CharacterModels/Yemoja/models/Yemoja.fbx\"");
//   // ...then, in a LATER separate call, once enough time has passed:
//   BlenderBridge.PrintStatus();
public static class BlenderBridge
{
    // Adjust here if Blender is ever installed to a different path/version on this machine.
    private const string DefaultBlenderExe = @"C:\Program Files\Blender Foundation\Blender 5.2\blender.exe";

    private static Process activeProcess;
    private static string lastLogPath;
    private static readonly StringBuilder outputBuffer = new StringBuilder();

    // Launches "blender.exe --background --python <script> -- <args>" without
    // blocking the Editor. Output (stdout+stderr) is captured and written to
    // BlenderTools/_logs/last_run.log when the process exits, and logged to the
    // Unity console via Debug.Log so it's visible through read_console too.
    public static void RunScript(string relativeScriptPath, string scriptArgs = "", string blenderExePath = DefaultBlenderExe)
    {
        string projectRoot = Directory.GetParent(Application.dataPath).FullName;
        string scriptPath = Path.Combine(projectRoot, relativeScriptPath);
        if (!File.Exists(scriptPath))
        {
            Debug.LogError("[BlenderBridge] Script not found: " + scriptPath);
            return;
        }

        if (!File.Exists(blenderExePath))
        {
            Debug.LogError("[BlenderBridge] Blender executable not found at: " + blenderExePath +
                            " - pass the correct path as the blenderExePath argument.");
            return;
        }

        string logDir = Path.Combine(projectRoot, "BlenderTools", "_logs");
        Directory.CreateDirectory(logDir);
        lastLogPath = Path.Combine(logDir, "last_run.log");

        var psi = new ProcessStartInfo
        {
            FileName = blenderExePath,
            Arguments = "--background --python \"" + scriptPath + "\" -- " + scriptArgs,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };

        outputBuffer.Clear();
        activeProcess = new Process { StartInfo = psi, EnableRaisingEvents = true };
        activeProcess.OutputDataReceived += (s, e) => { if (e.Data != null) outputBuffer.AppendLine(e.Data); };
        activeProcess.ErrorDataReceived += (s, e) => { if (e.Data != null) outputBuffer.AppendLine("[stderr] " + e.Data); };
        activeProcess.Exited += (s, e) =>
        {
            try
            {
                File.WriteAllText(lastLogPath, outputBuffer.ToString());
                Debug.Log("[BlenderBridge] Finished (exit code " + activeProcess.ExitCode + "). Full log: " + lastLogPath);
            }
            catch (Exception ex)
            {
                Debug.LogError("[BlenderBridge] Error writing log: " + ex.Message);
            }
        };

        activeProcess.Start();
        activeProcess.BeginOutputReadLine();
        activeProcess.BeginErrorReadLine();
        Debug.Log("[BlenderBridge] Launched (async, Editor stays responsive): " + scriptPath + " " + scriptArgs);
    }

    // Call this from a separate, later execute_code call to check whether the
    // most recent RunScript call has finished, and print its output if so.
    // Note: if Unity recompiles scripts (a domain reload) while Blender is
    // still running, this static state is lost - check BlenderTools/_logs/last_run.log
    // directly via a file read in that case instead.
    public static void PrintStatus()
    {
        if (activeProcess == null)
        {
            Debug.Log("[BlenderBridge] No script has been run yet this Editor session.");
            return;
        }

        if (!activeProcess.HasExited)
        {
            Debug.Log("[BlenderBridge] Still running...");
            return;
        }

        Debug.Log("[BlenderBridge] Exit code: " + activeProcess.ExitCode);
        if (lastLogPath != null && File.Exists(lastLogPath))
            Debug.Log("[BlenderBridge] Output:\n" + File.ReadAllText(lastLogPath));
    }
}
