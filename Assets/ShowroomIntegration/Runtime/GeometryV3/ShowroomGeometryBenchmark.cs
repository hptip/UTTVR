using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.XR;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

// Opt-in only. F8 in Editor/desktop, or the documented persistent-data JSON on Quest.
// Does not disable HMD tracking, change XR/render settings, or interpret Editor FPS as Quest FPS.
[DisallowMultipleComponent]
public sealed class ShowroomGeometryBenchmark : MonoBehaviour
{
    [Serializable] public sealed class Configuration { public bool autoStart; public int warmupFrames = 90, sampleFrames = 600; }
    public int warmupFrames = 90, sampleFrames = 600;
    public bool Running { get; private set; }
    public string OutputDirectory { get; private set; }
    public string LastFailure { get; private set; }
    struct Sample { public double frameMs; public long[] counters; public double gpuMs, cpuMs; public float positionDrift, angleDrift; }
    static readonly string[] CounterNames = { "Triangles Count", "Vertices Count", "Draw Calls Count", "SetPass Calls Count", "Main Thread", "Render Thread", "Total Used Memory", "Mesh Memory", "Texture Memory", "GC Allocated In Frame" };
    static readonly ShowroomGeometryMode[] Modes = { ShowroomGeometryMode.Original, ShowroomGeometryMode.Quality50, ShowroomGeometryMode.Quality25, ShowroomGeometryMode.Quality10 };
    readonly ProfilerRecorder[] counters = new ProfilerRecorder[CounterNames.Length];
    readonly bool[] valid = new bool[CounterNames.Length];
    readonly FrameTiming[] timing = new FrameTiming[1];
    Sample[] samples;
    ShowroomGeometryController controller;
    ShowroomGeometryMode previousMode;
    Camera cameraToMeasure; Vector3 cameraPosition; Quaternion cameraRotation;
    int modeIndex, frame, sample;
    readonly List<string> summary = new List<string>();
    UTT.SaBan.SaBanRoot model;
    bool[] layerStates; List<UTT.SaBan.SaBanLayer> layers;
    bool resumeTrain;
    void Start()
    {
        string path = Path.Combine(Application.persistentDataPath, "ShowroomGeometryBenchmark.json");
        if (!File.Exists(path)) return;
        try { var config = JsonUtility.FromJson<Configuration>(File.ReadAllText(path)); if (config != null && config.autoStart) { warmupFrames = config.warmupFrames; sampleFrames = config.sampleFrames; Begin(); } }
        catch (Exception e) { LastFailure = e.Message; Debug.LogWarning("[ShowroomGeometryV3] Benchmark configuration: " + e.Message); }
    }
    [ContextMenu("Run four-mode geometry benchmark")]
    public void Begin()
    {
        if (!Application.isPlaying || Running || gameObject.scene.name != "Showroom") return;
        controller = GetComponent<ShowroomGeometryController>(); if (controller == null || controller.profile == null) return;
        sampleFrames = Mathf.Clamp(sampleFrames, 90, 3600); warmupFrames = Mathf.Max(30, warmupFrames);
        LastFailure = null; previousMode = controller.mode;
        cameraToMeasure = Camera.main; if (cameraToMeasure == null) { LastFailure = "No main camera"; return; }
        cameraPosition = cameraToMeasure.transform.position; cameraRotation = cameraToMeasure.transform.rotation;
        model = GetComponent<UTT.SaBan.SaBanRoot>();
        if (model != null)
        {
            layers = model.GetLayers(); layerStates = layers.Select(l => l.IsVisible).ToArray(); foreach (var l in layers) l.SetVisible(true);
            if (model.trainRunner != null) { resumeTrain = model.trainRunner.IsRunning; model.trainRunner.Pause(); }
        }
        for (int i = 0; i < counters.Length; i++)
        {
            try { counters[i] = ProfilerRecorder.StartNew(i >= 6 ? ProfilerCategory.Memory : i >= 4 ? ProfilerCategory.Internal : ProfilerCategory.Render, CounterNames[i], 1); valid[i] = counters[i].Valid; }
            catch { valid[i] = false; }
        }
        string root = Application.isEditor ? Path.GetFullPath(Path.Combine(Application.dataPath,"../Temp/ShowroomGeometryV3Benchmarks")) : Path.Combine(Application.persistentDataPath,"ShowroomGeometryV3");
        OutputDirectory = Path.Combine(root, DateTime.UtcNow.ToString("yyyyMMdd_HHmmss")); Directory.CreateDirectory(OutputDirectory);
        File.WriteAllText(Path.Combine(OutputDirectory, "Environment.txt"), "Platform=" + Application.platform + "\nUnity=" + Application.unityVersion + "\nGPU=" + SystemInfo.graphicsDeviceName + "\nAPI=" + SystemInfo.graphicsDeviceType + "\nResolution=" + Screen.width + "x" + Screen.height + "\nXR=" + XRSettings.enabled + "\nEyeResolution=" + XRSettings.eyeTextureWidth + "x" + XRSettings.eyeTextureHeight + "\nStereo=" + XRSettings.stereoRenderingMode + "\nQuality=" + QualitySettings.names[QualitySettings.GetQualityLevel()] + "\nPipeline=" + GraphicsSettings.currentRenderPipeline + "\nVSync=" + QualitySettings.vSyncCount + "\nTargetFrameRate=" + Application.targetFrameRate + "\nSamplesPerMode=" + sampleFrames + "\nWarmupFrames=" + warmupFrames + "\nFrame timing/GPU counters may be unavailable; blank means not measured.\nEditor results are not standalone Quest results.\nCamera tracking is preserved. Compare poses in results; head drift invalidates strict A/B comparison.");
        samples = new Sample[sampleFrames]; for (int i = 0; i < samples.Length; i++) samples[i].counters = new long[counters.Length];
        summary.Clear(); summary.Add("Mode,Metric,Median,p95,p99,Unit"); modeIndex = frame = sample = 0; controller.SetMode(Modes[0]); Running = true;
    }
    void Update()
    {
#if ENABLE_INPUT_SYSTEM
        if (!Running && Keyboard.current != null && Keyboard.current.f8Key.wasPressedThisFrame) Begin();
#endif
        if (!Running) return;
        try
        {
            FrameTimingManager.CaptureFrameTimings();
            if (frame == Mathf.Max(1,warmupFrames/3)) ScreenCapture.CaptureScreenshot(Path.Combine(OutputDirectory, Modes[modeIndex] + ".png"));
            if (frame++ < warmupFrames) return;
            ref var row = ref samples[sample]; row.frameMs = Time.unscaledDeltaTime * 1000.0;
            for (int i = 0; i < counters.Length; i++) row.counters[i] = valid[i] ? counters[i].LastValue : -1;
            row.gpuMs = row.cpuMs = -1;
            if (FrameTimingManager.GetLatestTimings(1, timing) > 0) { if (timing[0].gpuFrameTime > 0) row.gpuMs = timing[0].gpuFrameTime; if (timing[0].cpuFrameTime > 0) row.cpuMs = timing[0].cpuFrameTime; }
            row.positionDrift = Vector3.Distance(cameraPosition, cameraToMeasure.transform.position); row.angleDrift = Quaternion.Angle(cameraRotation, cameraToMeasure.transform.rotation);
            if (++sample < sampleFrames) return;
            WriteMode(); sample = frame = 0;
            if (++modeIndex == Modes.Length) { Finish(); return; }
            controller.SetMode(Modes[modeIndex]);
        }
        catch (Exception e) { LastFailure = e.ToString(); if (OutputDirectory != null) File.WriteAllText(Path.Combine(OutputDirectory, "Error.txt"), LastFailure); Finish(); }
    }
    static string Number(double value) => value.ToString("F4", CultureInfo.InvariantCulture);
    static double Pct(double[] values, double p) => values[Mathf.Clamp((int)Math.Ceiling(values.Length * p) - 1, 0, values.Length - 1)];
    void Metric(string name, IEnumerable<double> values, string unit)
    {
        var a = values.Where(v => v >= 0).OrderBy(v => v).ToArray();
        summary.Add(Modes[modeIndex] + "," + name + "," + (a.Length == 0 ? ",," : Number(Pct(a,.5)) + "," + Number(Pct(a,.95)) + "," + Number(Pct(a,.99))) + "," + unit);
    }
    void WriteMode()
    {
        var csv = new StringBuilder("Frame,FrameMs,Triangles,Vertices,DrawCalls,SetPass,MainThreadNs,RenderThreadNs,TotalUsedMemoryBytes,MeshMemoryBytes,TextureMemoryBytes,GCAllocatedBytes,GpuMs,CpuFrameMs,CameraDriftM,CameraDriftDegrees\n");
        for (int i = 0; i < sampleFrames; i++) { var r = samples[i]; csv.Append(i).Append(',').Append(Number(r.frameMs)); foreach (long c in r.counters) csv.Append(',').Append(c >= 0 ? c.ToString(CultureInfo.InvariantCulture) : ""); csv.Append(',').Append(r.gpuMs < 0 ? "" : Number(r.gpuMs)).Append(',').Append(r.cpuMs < 0 ? "" : Number(r.cpuMs)).Append(',').Append(Number(r.positionDrift)).Append(',').Append(Number(r.angleDrift)).AppendLine(); }
        File.WriteAllText(Path.Combine(OutputDirectory, Modes[modeIndex] + ".csv"), csv.ToString());
        Metric("Frame time", samples.Select(s => s.frameMs), "ms"); Metric("FPS", samples.Select(s => 1000.0 / Math.Max(.001,s.frameMs)), "fps");
        for (int c = 0; c < counters.Length; c++) { int index = c; Metric(CounterNames[c], samples.Select(s => (double)s.counters[index]), c >= 6 ? "bytes" : c >= 4 ? "ns" : "count"); }
        Metric("GPU time", samples.Select(s => s.gpuMs), "ms"); Metric("CPU frame time", samples.Select(s => s.cpuMs), "ms"); Metric("Camera position drift", samples.Select(s => (double)s.positionDrift), "m"); Metric("Camera angle drift", samples.Select(s => (double)s.angleDrift), "degrees");
        File.WriteAllLines(Path.Combine(OutputDirectory, "Summary.csv"), summary);
    }
    void Finish()
    {
        Running = false; foreach (var r in counters) r.Dispose();
        if (controller != null) controller.SetMode(previousMode);
        if (layers != null) for (int i = 0; i < layers.Count; i++) if (layers[i] != null) layers[i].SetVisible(layerStates[i]);
        if (model != null && model.trainRunner != null && resumeTrain) model.trainRunner.Play();
        if (OutputDirectory != null) File.WriteAllText(Path.Combine(OutputDirectory, "Completed.txt"), LastFailure == null ? "Measured all four modes. Check Environment and pose drift before comparing. This is not proof of 72 FPS on Quest unless measured on that headset." : "FAILED: " + LastFailure);
    }
    void OnDisable() { if (Running) Finish(); }
}
