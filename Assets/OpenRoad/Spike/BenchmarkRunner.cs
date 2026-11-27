using System;
using System.Globalization;
using System.IO;
using System.Text;
using OpenRoad.Core;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace OpenRoad.Spike
{
    /// <summary>
    /// Deterministic Phase 0 benchmark. Frame samples are stored in preallocated arrays and written
    /// after the timed interval so file I/O does not run inside the measured loop.
    /// </summary>
    public sealed class BenchmarkRunner : MonoBehaviour
    {
        public const float FixedBenchmarkSpeedKph = 80f;
        public const int TrafficSeed = 20261127;
        public const int LowDrawCallCap = 200;
        private const float InitialRenderScale = 0.80f;
        private const float MinimumRenderScale = 0.65f;
        private const float MaximumRenderScale = 1.00f;

        public SpikeRideController rideController;
        public TrafficWorld trafficWorld;
        public MirrorCameraRig mirrorRig;
        public SpikeCameraRig cameraRig;
        public LensCameraMarker lensMarker;
        public Camera playerCamera;

        private readonly FrameTiming[] _latestFrameTiming = new FrameTiming[1];
        private ProfilerRecorder _drawCallRecorder;
        private BenchmarkSample[] _samples;
        private float[] _sortedFrameTimes;
        private readonly int[] _histogram = new int[7];

        private bool _showPanel = true;
        private bool _isWarmingUp;
        private bool _isMeasuring;
        private bool _frameTimingObserved;
        private bool _budgetViolationLogged;
        private bool _dynamicResolutionSupported;
        private float _durationSeconds = 40f;
        private float _warmupSeconds = 5f;
        private float _warmupEndTime;
        private float _measurementElapsed;
        private float _currentRenderScale = InitialRenderScale;
        private float _renderScaleTotal;
        private float _renderScaleMin = 1f;
        private float _renderScaleMax;
        private float _resolutionAdjustElapsed;
        private float _lastFrameMs;
        private float _lastCpuFrameMs = -1f;
        private float _lastCpuMainMs = -1f;
        private float _lastGpuFrameMs = -1f;
        private float _cpuFrameTotal;
        private float _cpuMainTotal;
        private float _gpuFrameTotal;
        private int _cpuFrameSamples;
        private int _cpuMainSamples;
        private int _gpuFrameSamples;
        private long _peakDrawCalls = -1;
        private int _sampleCount;
        private int _droppedSamples;
        private int _previousVSync;
        private int _previousTargetFrameRate;
        private UniversalRenderPipelineAsset _pipelineAsset;
        private float _previousPipelineScale = 1f;
        private bool _changedPipelineScale;
        private string _summaryPath = string.Empty;
        private string _csvPath = string.Empty;
        private string _lastSummary = string.Empty;
        private string _cachedOverlayText = string.Empty;
        private float _lastOverlayRefresh;
        private GUIStyle _titleStyle;
        private GUIStyle _labelStyle;
        private GUIStyle _buttonStyle;

        private struct BenchmarkSample
        {
            public float elapsedSeconds;
            public float frameTimeMs;
            public float cpuFrameMs;
            public float cpuMainThreadMs;
            public float gpuFrameMs;
            public float renderScale;
            public long drawCalls;
            public int trafficCount;
        }

        private void Start()
        {
            if (rideController == null) rideController = FindFirstObjectByType<SpikeRideController>();
            if (trafficWorld == null) trafficWorld = FindFirstObjectByType<TrafficWorld>();
            if (mirrorRig == null) mirrorRig = FindFirstObjectByType<MirrorCameraRig>();
            if (cameraRig == null) cameraRig = FindFirstObjectByType<SpikeCameraRig>();
            if (playerCamera == null) playerCamera = cameraRig != null ? cameraRig.PlayerCamera : Camera.main;
            if (lensMarker == null && playerCamera != null) lensMarker = playerCamera.GetComponent<LensCameraMarker>();

            try
            {
                _drawCallRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Draw Calls Count", 1);
            }
            catch (Exception)
            {
                _drawCallRecorder = default;
            }

            if (HasArgument("-benchmark"))
            {
                _durationSeconds = Mathf.Clamp(ReadFloatArgument("-benchmark-seconds", 40f), 10f, 180f);
                _warmupSeconds = Mathf.Clamp(ReadFloatArgument("-benchmark-warmup-seconds", 5f), 0f, 30f);
                StartBenchmark();
            }
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.H))
            {
                _showPanel = !_showPanel;
            }

            if (!_isWarmingUp && !_isMeasuring)
            {
                if (Input.GetKeyDown(KeyCode.F2)) StartBenchmark();
                if (Input.GetKeyDown(KeyCode.C) && cameraRig != null) cameraRig.ToggleView();
                if (Input.GetKeyDown(KeyCode.M) && mirrorRig != null) mirrorRig.SetMirrorsEnabled(!mirrorRig.MirrorsEnabled);
                if (Input.GetKeyDown(KeyCode.F1)) CycleLensMode();
            }

            if (_isWarmingUp)
            {
                if (Time.unscaledTime >= _warmupEndTime)
                {
                    BeginMeasuredInterval();
                }
                return;
            }

            if (!_isMeasuring)
            {
                return;
            }

            CaptureFrameSample();
            UpdateDynamicResolution();
            if (_measurementElapsed >= _durationSeconds)
            {
                FinishBenchmark();
            }
        }

        private void StartBenchmark()
        {
            if (_isWarmingUp || _isMeasuring)
            {
                return;
            }

            if (rideController == null || trafficWorld == null || playerCamera == null)
            {
                Debug.LogError("OPEN ROAD benchmark could not start: missing ride, traffic, or player camera reference.");
                return;
            }

            _previousVSync = QualitySettings.vSyncCount;
            _previousTargetFrameRate = Application.targetFrameRate;
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = -1;

            if (!Application.isEditor && SystemInfo.graphicsDeviceType != GraphicsDeviceType.Null)
            {
                Screen.SetResolution(1920, 1080, FullScreenMode.Windowed);
            }

            _dynamicResolutionSupported = SystemInfo.supportsDynamicResolution && SystemInfo.graphicsDeviceType != GraphicsDeviceType.Null;
            playerCamera.allowDynamicResolution = _dynamicResolutionSupported;
            _pipelineAsset = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            if (!_dynamicResolutionSupported && _pipelineAsset != null)
            {
                _previousPipelineScale = _pipelineAsset.renderScale;
                _pipelineAsset.renderScale = InitialRenderScale;
                _changedPipelineScale = true;
            }

            _currentRenderScale = InitialRenderScale;
            if (_dynamicResolutionSupported)
            {
                ScalableBufferManager.ResizeBuffers(_currentRenderScale, _currentRenderScale);
            }

            if (cameraRig != null) cameraRig.SetCockpitView(false);
            if (lensMarker != null)
            {
                lensMarker.distortionStrength = 0.92f;
                lensMarker.edgeCoverage = 0.93f;
                lensMarker.verticalScale = 1f;
            }
            if (mirrorRig != null) mirrorRig.SetMirrorsEnabled(true);
            trafficWorld.seed = TrafficSeed;
            trafficWorld.ResetTraffic();
            rideController.BeginBenchmarkRide(FixedBenchmarkSpeedKph);

            _durationSeconds = Mathf.Clamp(_durationSeconds, 10f, 180f);
            _warmupSeconds = Mathf.Clamp(_warmupSeconds, 0f, 30f);
            _warmupEndTime = Time.unscaledTime + _warmupSeconds;
            _isWarmingUp = _warmupSeconds > 0f;
            if (!_isWarmingUp) BeginMeasuredInterval();
            RefreshOverlayCache(force: true);
        }

        private void BeginMeasuredInterval()
        {
            _isWarmingUp = false;
            _isMeasuring = true;
            _measurementElapsed = 0f;
            _resolutionAdjustElapsed = 0f;
            _lastFrameMs = 0f;
            _lastCpuFrameMs = _lastCpuMainMs = _lastGpuFrameMs = -1f;
            _cpuFrameTotal = _cpuMainTotal = _gpuFrameTotal = 0f;
            _cpuFrameSamples = _cpuMainSamples = _gpuFrameSamples = 0;
            _peakDrawCalls = -1;
            _sampleCount = 0;
            _droppedSamples = 0;
            _renderScaleTotal = 0f;
            _renderScaleMin = 1f;
            _renderScaleMax = 0f;
            _frameTimingObserved = false;
            _budgetViolationLogged = false;
            _lastSummary = string.Empty;
            Array.Clear(_histogram, 0, _histogram.Length);

            int capacity = Mathf.Max(2048, Mathf.CeilToInt(_durationSeconds * 360f) + 1024);
            _samples = new BenchmarkSample[capacity];
            _sortedFrameTimes = new float[capacity];
            trafficWorld.ResetTraffic();
            rideController.BeginBenchmarkRide(FixedBenchmarkSpeedKph);
            if (cameraRig != null) cameraRig.SnapToCurrentView();
            RefreshOverlayCache(force: true);
            Debug.Log($"OPEN ROAD benchmark started: {1920}x{1080} output, {FixedBenchmarkSpeedKph:0} km/h, seed {TrafficSeed}, {trafficWorld.ActiveVehicleCount} traffic vehicles.");
        }

        private void CaptureFrameSample()
        {
            float deltaSeconds = Mathf.Max(0.000001f, Time.unscaledDeltaTime);
            float frameMs = deltaSeconds * 1000f;
            _measurementElapsed += deltaSeconds;
            _lastFrameMs = frameMs;

            float cpuFrameMs = -1f;
            float cpuMainMs = -1f;
            float gpuFrameMs = -1f;
            FrameTimingManager.CaptureFrameTimings();
            uint timingCount = FrameTimingManager.GetLatestTimings(1, _latestFrameTiming);
            if (timingCount > 0)
            {
                FrameTiming timing = _latestFrameTiming[0];
                cpuFrameMs = ToPositiveFloat(timing.cpuFrameTime);
                cpuMainMs = ToPositiveFloat(timing.cpuMainThreadFrameTime);
                gpuFrameMs = ToPositiveFloat(timing.gpuFrameTime);
                if (cpuFrameMs >= 0f) { _cpuFrameTotal += cpuFrameMs; _cpuFrameSamples++; }
                if (cpuMainMs >= 0f) { _cpuMainTotal += cpuMainMs; _cpuMainSamples++; }
                if (gpuFrameMs >= 0f) { _gpuFrameTotal += gpuFrameMs; _gpuFrameSamples++; }
                _frameTimingObserved = true;
            }

            _lastCpuFrameMs = cpuFrameMs;
            _lastCpuMainMs = cpuMainMs;
            _lastGpuFrameMs = gpuFrameMs;

            long drawCalls = _drawCallRecorder.Valid ? _drawCallRecorder.LastValue : -1;
            if (drawCalls >= 0 && drawCalls > _peakDrawCalls) _peakDrawCalls = drawCalls;
            if (drawCalls > LowDrawCallCap && !_budgetViolationLogged)
            {
                _budgetViolationLogged = true;
                Debug.LogError($"OPEN ROAD Phase 0 Low draw-call ceiling exceeded: {drawCalls} > {LowDrawCallCap}. Review this run before advancing.");
            }

            float scale = GetCurrentRenderScale();
            _renderScaleTotal += scale;
            _renderScaleMin = Mathf.Min(_renderScaleMin, scale);
            _renderScaleMax = Mathf.Max(_renderScaleMax, scale);
            _histogram[BenchmarkMath.HistogramBucket(frameMs)]++;

            if (_samples != null && _sampleCount < _samples.Length)
            {
                _samples[_sampleCount++] = new BenchmarkSample
                {
                    elapsedSeconds = _measurementElapsed,
                    frameTimeMs = frameMs,
                    cpuFrameMs = cpuFrameMs,
                    cpuMainThreadMs = cpuMainMs,
                    gpuFrameMs = gpuFrameMs,
                    renderScale = scale,
                    drawCalls = drawCalls,
                    trafficCount = trafficWorld != null ? trafficWorld.ActiveVehicleCount : 0
                };
            }
            else
            {
                _droppedSamples++;
            }

            _resolutionAdjustElapsed += deltaSeconds;
        }

        private void UpdateDynamicResolution()
        {
            bool canAdjustScale = _dynamicResolutionSupported || (_changedPipelineScale && _pipelineAsset != null);
            if (!canAdjustScale || _resolutionAdjustElapsed < 0.5f)
            {
                return;
            }

            _resolutionAdjustElapsed = 0f;
            if (_lastFrameMs > 25.5f)
            {
                _currentRenderScale = Mathf.Max(MinimumRenderScale, _currentRenderScale - 0.04f);
            }
            else if (_lastFrameMs < 20.5f)
            {
                _currentRenderScale = Mathf.Min(MaximumRenderScale, _currentRenderScale + 0.02f);
            }

            if (_dynamicResolutionSupported)
            {
                ScalableBufferManager.ResizeBuffers(_currentRenderScale, _currentRenderScale);
            }
            else
            {
                // Retain a bounded fallback for drivers that do not expose Unity's dynamic buffers.
                _pipelineAsset.renderScale = _currentRenderScale;
            }
        }

        private float GetCurrentRenderScale()
        {
            if (!_dynamicResolutionSupported)
            {
                return _changedPipelineScale && _pipelineAsset != null ? _pipelineAsset.renderScale : 1f;
            }

            float widthScale = ScalableBufferManager.widthScaleFactor;
            return widthScale > 0f ? widthScale : _currentRenderScale;
        }

        private void FinishBenchmark()
        {
            if (!_isMeasuring)
            {
                return;
            }

            _isMeasuring = false;
            _isWarmingUp = false;
            if (rideController != null) rideController.EndBenchmarkRide();

            string writeError = null;
            try
            {
                WriteBenchmarkFiles();
            }
            catch (Exception exception)
            {
                writeError = exception.Message;
                Debug.LogError("OPEN ROAD benchmark output failed: " + exception);
            }

            if (_dynamicResolutionSupported)
            {
                ScalableBufferManager.ResizeBuffers(1f, 1f);
            }
            if (_changedPipelineScale && _pipelineAsset != null)
            {
                _pipelineAsset.renderScale = _previousPipelineScale;
                _changedPipelineScale = false;
            }
            QualitySettings.vSyncCount = _previousVSync;
            Application.targetFrameRate = _previousTargetFrameRate;

            if (!string.IsNullOrEmpty(writeError))
            {
                _lastSummary = "Benchmark finished, but files could not be written: " + writeError;
            }
            RefreshOverlayCache(force: true);
        }

        private void WriteBenchmarkFiles()
        {
            if (_sampleCount <= 0)
            {
                throw new InvalidOperationException("No frame samples were captured.");
            }

            float frameTimeTotal = 0f;
            for (int i = 0; i < _sampleCount; i++)
            {
                _sortedFrameTimes[i] = _samples[i].frameTimeMs;
                frameTimeTotal += _samples[i].frameTimeMs;
            }
            Array.Sort(_sortedFrameTimes, 0, _sampleCount);

            float averageFps = _measurementElapsed > 0f ? _sampleCount / _measurementElapsed : 0f;
            float averageFrameMs = frameTimeTotal / _sampleCount;
            float p99FrameMs = BenchmarkMath.Percentile(_sortedFrameTimes, _sampleCount, 0.99f);
            float p999FrameMs = BenchmarkMath.Percentile(_sortedFrameTimes, _sampleCount, 0.999f);
            float low1Fps = BenchmarkMath.FpsForFrameTime(p99FrameMs);
            float low01Fps = BenchmarkMath.FpsForFrameTime(p999FrameMs);
            float averageCpuFrameMs = _cpuFrameSamples > 0 ? _cpuFrameTotal / _cpuFrameSamples : -1f;
            float averageCpuMainMs = _cpuMainSamples > 0 ? _cpuMainTotal / _cpuMainSamples : -1f;
            float averageGpuMs = _gpuFrameSamples > 0 ? _gpuFrameTotal / _gpuFrameSamples : -1f;
            float averageScale = _sampleCount > 0 ? _renderScaleTotal / _sampleCount : 1f;
            float driverMemoryMb = GetDriverGraphicsAllocationMb();
            float processWorkingSetMb = GetProcessWorkingSetMb();
            int adapterMemoryMb = SystemInfo.graphicsMemorySize;
            bool fpsThresholdPassed = averageFps >= 40f && low1Fps >= 30f;
            string fpsResult = fpsThresholdPassed ? "PASS for this run" : "NOT MET for this run";
            string drawResult = _peakDrawCalls < 0 ? "UNAVAILABLE" : _peakDrawCalls <= LowDrawCallCap ? "PASS" : "OVER BUDGET";

            string stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture);
            string outputDirectory = Path.Combine(Application.persistentDataPath, "Benchmark");
            Directory.CreateDirectory(outputDirectory);
            _csvPath = Path.Combine(outputDirectory, "benchmark_" + stamp + ".csv");
            _summaryPath = Path.Combine(outputDirectory, "benchmark_summary.txt");

            using (var writer = new StreamWriter(_csvPath, false, new UTF8Encoding(false)))
            {
                writer.WriteLine("frame_index,elapsed_seconds,frame_time_ms,fps,cpu_frame_ms,cpu_main_thread_ms,gpu_frame_ms,draw_calls,render_scale,traffic_count");
                for (int i = 0; i < _sampleCount; i++)
                {
                    BenchmarkSample sample = _samples[i];
                    writer.Write(i.ToString(CultureInfo.InvariantCulture)); writer.Write(',');
                    writer.Write(sample.elapsedSeconds.ToString("0.000", CultureInfo.InvariantCulture)); writer.Write(',');
                    writer.Write(sample.frameTimeMs.ToString("0.000", CultureInfo.InvariantCulture)); writer.Write(',');
                    writer.Write(BenchmarkMath.FpsForFrameTime(sample.frameTimeMs).ToString("0.00", CultureInfo.InvariantCulture)); writer.Write(',');
                    writer.Write(FormatMetric(sample.cpuFrameMs)); writer.Write(',');
                    writer.Write(FormatMetric(sample.cpuMainThreadMs)); writer.Write(',');
                    writer.Write(FormatMetric(sample.gpuFrameMs)); writer.Write(',');
                    writer.Write(sample.drawCalls >= 0 ? sample.drawCalls.ToString(CultureInfo.InvariantCulture) : string.Empty); writer.Write(',');
                    writer.Write(sample.renderScale.ToString("0.000", CultureInfo.InvariantCulture)); writer.Write(',');
                    writer.WriteLine(sample.trafficCount.ToString(CultureInfo.InvariantCulture));
                }
            }

            var summary = new StringBuilder(1800);
            summary.AppendLine("OPEN ROAD — PHASE 0 BENCHMARK");
            summary.AppendLine("==============================");
            summary.AppendLine("Timestamp: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss zzz", CultureInfo.InvariantCulture));
            summary.AppendLine("Version: " + BuildInfo.Version);
            summary.AppendLine("Build number: " + BuildInfo.BuildNumber + " (player ID " + BuildInfo.ShortBuildId + ")");
            summary.AppendLine("Route: 1 km straight fixture; fixed speed " + FixedBenchmarkSpeedKph.ToString("0", CultureInfo.InvariantCulture) + " km/h");
            summary.AppendLine("Weather: clear (fixed Phase 0 scene)");
            summary.AppendLine("Traffic seed/count: " + TrafficSeed.ToString(CultureInfo.InvariantCulture) + "/" + (trafficWorld != null ? trafficWorld.ActiveVehicleCount : 0).ToString(CultureInfo.InvariantCulture));
            summary.AppendLine("Output request: 1920x1080 windowed; render scale starts at 0.80 and is bounded 0.65–1.00");
            summary.AppendLine("Dynamic resolution supported by runtime: " + (_dynamicResolutionSupported ? "yes" : "no"));
            summary.AppendLine("Lens: " + (lensMarker != null ? lensMarker.ModeName : "unknown") + "; mirrors enabled: " + (mirrorRig != null && mirrorRig.MirrorsEnabled ? "yes" : "no"));
            summary.AppendLine();
            summary.AppendLine("Samples: " + _sampleCount.ToString(CultureInfo.InvariantCulture) + (_droppedSamples > 0 ? " (dropped due to preallocated capacity: " + _droppedSamples.ToString(CultureInfo.InvariantCulture) + ")" : string.Empty));
            summary.AppendLine("Average FPS: " + averageFps.ToString("0.00", CultureInfo.InvariantCulture));
            summary.AppendLine("Average frame time: " + averageFrameMs.ToString("0.00", CultureInfo.InvariantCulture) + " ms");
            summary.AppendLine("1% low FPS (1000 / p99 frame time): " + low1Fps.ToString("0.00", CultureInfo.InvariantCulture) + " FPS (p99 " + p99FrameMs.ToString("0.00", CultureInfo.InvariantCulture) + " ms)");
            summary.AppendLine("0.1% low FPS (1000 / p99.9 frame time): " + low01Fps.ToString("0.00", CultureInfo.InvariantCulture) + " FPS (p99.9 " + p999FrameMs.ToString("0.00", CultureInfo.InvariantCulture) + " ms)");
            summary.AppendLine("Frame-time threshold (40 average / 30 FPS 1% low): " + fpsResult);
            summary.AppendLine("Average CPU frame time: " + FormatMetric(averageCpuFrameMs) + " ms");
            summary.AppendLine("Average CPU main-thread time: " + FormatMetric(averageCpuMainMs) + " ms (budget 18 ms)");
            summary.AppendLine("Average GPU frame time: " + FormatMetric(averageGpuMs) + " ms (budget 22 ms)");
            summary.AppendLine("FrameTimingManager returned timings: " + (_frameTimingObserved ? "yes" : "no; CPU/GPU timing unavailable"));
            summary.AppendLine("Peak draw calls: " + (_peakDrawCalls >= 0 ? _peakDrawCalls.ToString(CultureInfo.InvariantCulture) : "unavailable") + " (Low cap " + LowDrawCallCap.ToString(CultureInfo.InvariantCulture) + "; " + drawResult + ")");
            summary.AppendLine("Dynamic render scale min / average / max: " + _renderScaleMin.ToString("0.000", CultureInfo.InvariantCulture) + " / " + averageScale.ToString("0.000", CultureInfo.InvariantCulture) + " / " + _renderScaleMax.ToString("0.000", CultureInfo.InvariantCulture));
            summary.AppendLine("GPU driver allocated memory: " + FormatMetric(driverMemoryMb) + " MB (budget 1536 MB; API may be unavailable)");
            summary.AppendLine("GPU adapter memory capacity: " + (adapterMemoryMb > 0 ? adapterMemoryMb.ToString(CultureInfo.InvariantCulture) : "unavailable") + " MB (capacity, not current usage)");
            summary.AppendLine("Process working set: " + FormatMetric(processWorkingSetMb) + " MB");
            summary.AppendLine("Texture memory: unavailable from this runtime counter");
            summary.AppendLine("Render-target memory: estimate not reported as exact allocation; mirrors are two 256x128 targets");
            summary.AppendLine("Frame-time histogram:");
            summary.AppendLine("  < 8.33 ms: " + _histogram[0].ToString(CultureInfo.InvariantCulture));
            summary.AppendLine("  8.33–12.50 ms: " + _histogram[1].ToString(CultureInfo.InvariantCulture));
            summary.AppendLine("  12.50–16.67 ms: " + _histogram[2].ToString(CultureInfo.InvariantCulture));
            summary.AppendLine("  16.67–22.00 ms: " + _histogram[3].ToString(CultureInfo.InvariantCulture));
            summary.AppendLine("  22.00–33.33 ms: " + _histogram[4].ToString(CultureInfo.InvariantCulture));
            summary.AppendLine("  33.33–50.00 ms: " + _histogram[5].ToString(CultureInfo.InvariantCulture));
            summary.AppendLine("  >= 50.00 ms: " + _histogram[6].ToString(CultureInfo.InvariantCulture));
            summary.AppendLine();
            summary.AppendLine("Important: this pass/fail is only for this machine/run. It is not a milestone acceptance until the owner runs it on the specified target PC and reviews the full report.");
            summary.AppendLine("CSV: " + _csvPath);

            _lastSummary = summary.ToString();
            File.WriteAllText(_summaryPath, _lastSummary, new UTF8Encoding(false));
            Debug.Log(_lastSummary);
        }

        private void CycleLensMode()
        {
            if (lensMarker == null) return;
            if (lensMarker.distortionStrength < 0.01f) lensMarker.distortionStrength = 0.55f;
            else if (lensMarker.distortionStrength < 0.75f) lensMarker.distortionStrength = 0.92f;
            else lensMarker.distortionStrength = 0f;
            RefreshOverlayCache(force: true);
        }

        private void OnGUI()
        {
            if (!_showPanel)
            {
                GUI.Label(new Rect(12f, 12f, 270f, 24f), "OPEN ROAD · Spike panel hidden (H)");
                return;
            }

            if (_titleStyle == null)
            {
                _titleStyle = new GUIStyle(GUI.skin.label) { fontSize = 20, fontStyle = FontStyle.Bold, wordWrap = false };
                _labelStyle = new GUIStyle(GUI.skin.label) { fontSize = 13, wordWrap = true };
                _buttonStyle = new GUIStyle(GUI.skin.button) { fontSize = 14, fontStyle = FontStyle.Bold };
            }

            RefreshOverlayCache(force: false);
            float width = Mathf.Min(520f, Mathf.Max(360f, Screen.width - 24f));
            float height = _lastSummary.Length > 0 ? 300f : 245f;
            Rect panel = new Rect(12f, 12f, width, height);
            GUI.Box(panel, GUIContent.none);
            GUI.Label(new Rect(panel.x + 14f, panel.y + 10f, width - 28f, 30f), "OPEN ROAD  /  PHASE 0 SPIKE", _titleStyle);
            GUI.Label(new Rect(panel.x + 14f, panel.y + 43f, width - 28f, 75f), _cachedOverlayText, _labelStyle);

            if (!_isWarmingUp && !_isMeasuring)
            {
                if (GUI.Button(new Rect(panel.x + 14f, panel.y + 120f, width - 28f, 34f), "Run 40 s Benchmark  (F2)", _buttonStyle))
                {
                    StartBenchmark();
                }
            }
            else
            {
                string action = _isWarmingUp ? "Warm-up…" : "Benchmark running… click to stop";
                if (GUI.Button(new Rect(panel.x + 14f, panel.y + 120f, width - 28f, 34f), action, _buttonStyle) && _isMeasuring)
                {
                    FinishBenchmark();
                }
            }

            GUI.Label(new Rect(panel.x + 14f, panel.y + 162f, width - 28f, 70f),
                "W/S accelerate/brake · A/D steer · C camera · M mirrors · F1 lens · H panel\n"
                + "Kinematic bike and simple traffic are test fixtures, not simulator physics.", _labelStyle);

            if (!string.IsNullOrEmpty(_lastSummary))
            {
                string brief = "Summary: " + _summaryPath + "\nCSV: " + _csvPath;
                GUI.Label(new Rect(panel.x + 14f, panel.y + 225f, width - 28f, 62f), brief, _labelStyle);
            }
        }

        private void RefreshOverlayCache(bool force)
        {
            if (!force && Time.unscaledTime - _lastOverlayRefresh < 0.15f)
            {
                return;
            }

            _lastOverlayRefresh = Time.unscaledTime;
            string phase = _isWarmingUp ? "Warm-up" : _isMeasuring ? "Measuring" : "Ready";
            string drawText = _drawCallRecorder.Valid ? _drawCallRecorder.LastValue.ToString(CultureInfo.InvariantCulture) : "unavailable";
            float fps = _lastFrameMs > 0f ? 1000f / _lastFrameMs : 0f;
            string timingText = _frameTimingObserved
                ? "CPU " + FormatMetric(_lastCpuMainMs) + " ms · GPU " + FormatMetric(_lastGpuFrameMs) + " ms"
                : "CPU/GPU timing pending or unavailable";
            string scaleText = _dynamicResolutionSupported ? _currentRenderScale.ToString("0.00", CultureInfo.InvariantCulture) : "fixed";
            _cachedOverlayText = phase + " · " + BuildInfo.Version + " · build " + BuildInfo.BuildNumber + "\n"
                + "FPS " + fps.ToString("0.0", CultureInfo.InvariantCulture) + " · frame " + _lastFrameMs.ToString("0.0", CultureInfo.InvariantCulture) + " ms · " + timingText + "\n"
                + "Traffic " + (trafficWorld != null ? trafficWorld.ActiveVehicleCount : 0).ToString(CultureInfo.InvariantCulture) + "/50 · draw calls " + drawText + "/" + LowDrawCallCap.ToString(CultureInfo.InvariantCulture) + " · scale " + scaleText + "\n"
                + "Lens " + (lensMarker != null ? lensMarker.ModeName : "unknown") + " · mirrors " + (mirrorRig != null && mirrorRig.MirrorsEnabled ? "on" : "off")
                + (_isMeasuring ? " · " + _measurementElapsed.ToString("0.0", CultureInfo.InvariantCulture) + "/" + _durationSeconds.ToString("0", CultureInfo.InvariantCulture) + " s" : string.Empty);
        }

        private void OnDestroy()
        {
            if (_drawCallRecorder.Valid) _drawCallRecorder.Dispose();
        }

        private static float ToPositiveFloat(double value)
        {
            return value > 0.0 && !double.IsNaN(value) && !double.IsInfinity(value) ? (float)value : -1f;
        }

        private static float GetDriverGraphicsAllocationMb()
        {
            try
            {
                long bytes = Profiler.GetAllocatedMemoryForGraphicsDriver();
                return bytes > 0 ? bytes / (1024f * 1024f) : -1f;
            }
            catch (Exception)
            {
                return -1f;
            }
        }

        private static float GetProcessWorkingSetMb()
        {
            try
            {
                using (System.Diagnostics.Process process = System.Diagnostics.Process.GetCurrentProcess())
                {
                    return process.WorkingSet64 / (1024f * 1024f);
                }
            }
            catch (Exception)
            {
                return -1f;
            }
        }

        private static string FormatMetric(float value)
        {
            return value >= 0f && !float.IsNaN(value) && !float.IsInfinity(value)
                ? value.ToString("0.00", CultureInfo.InvariantCulture)
                : "unavailable";
        }

        private static bool HasArgument(string name)
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length; i++)
            {
                if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase) ||
                    args[i].StartsWith(name + "=", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            return false;
        }

        private static float ReadFloatArgument(string name, float fallback)
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length; i++)
            {
                if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length &&
                    float.TryParse(args[i + 1], NumberStyles.Float, CultureInfo.InvariantCulture, out float nextValue))
                {
                    return nextValue;
                }

                string prefix = name + "=";
                if (args[i].StartsWith(prefix, StringComparison.OrdinalIgnoreCase) &&
                    float.TryParse(args[i].Substring(prefix.Length), NumberStyles.Float, CultureInfo.InvariantCulture, out float inlineValue))
                {
                    return inlineValue;
                }
            }
            return fallback;
        }
    }
}
