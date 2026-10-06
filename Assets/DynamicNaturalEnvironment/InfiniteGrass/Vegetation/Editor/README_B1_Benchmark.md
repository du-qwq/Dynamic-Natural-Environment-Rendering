# Vegetation B1 Benchmark

The harness extends `VegetationPerformanceDiagnostic`. It makes an in-memory database for each scale; it never writes to `VD_MainEnvironment.asset`. It restores camera, light, renderer, quality, and feature settings when a run ends or its component is disabled.

## Run

1. Open `Assets/Scenes/Environment_Showcase.unity`.
2. For a short Editor validation, enter Play Mode and select **Tools > Vegetation > B1 Run Editor Smoke**. This runs two cases at 1,000 instances with 0.25 s warm-up and 0.75 s sampling. Editor FPS is not a portfolio result.
3. For a full player run, select **Tools > Vegetation > B1 Build Release Player** or **B1 Build Development Player**. The build command temporarily enables Unity Frame Timing Statistics and restores the project setting afterward.
4. Launch `Builds/VegetationBenchmark/<Release or Development>/VegetationBenchmark.exe -vegetationBenchmark`. The first loaded scene is `Environment_Showcase`. Leave the player in focus until the completion message appears.
5. To run only the 100k core case, launch the Release player with `-vegetationBenchmarkCore100k`. This exports one `FullyOptimizedCore` row and then exits.
6. To validate the 100k GPU culling ablation case, launch with `-vegetationBenchmarkGPUCull100k`. This exports one `+GPUInstanceCull` row, `player_log_audit.txt`, and `validation.png`, then exits.
7. To run only the B1 final supplemental suite, launch with `-vegetationBenchmarkSupplemental`. It exports the three replacement `+GPUInstanceCull` scales and the two 500k `Core_CPUChunk_OFF/ON` rows to one new result directory.

The output is `Application.persistentDataPath/VegetationBenchmark/<UTC timestamp>/summary.csv`, `raw_frames.csv`, `readbacks.csv`, and `method.txt`. The completion log prints the exact directory. Use a standalone Release or Development player for published measurements.

## Matrix and path

- Scales: 100,000; 500,000; 1,000,000 instances. The 1M case can be disabled on the component if the target machine cannot hold the runtime copy. A disabled or failed 1M case is not silently replaced with an invented result.
- Sequential core ablation: Baseline, +CPU Chunk Cull, +GPU Instance Cull, +Mesh/Density LOD, FullyOptimizedCore (adds Shadow Optimization). DepthNormals is OFF and Additional Lights are 0 in all five cases.
- At 500k: `FullyOptimizedCore_AL_0/1/4/8` with DepthNormals OFF; `FullyOptimizedCore_DN_OFF/ON` with Additional Lights 0; and `FullyOptimizedCore_Shadow_OFF/ON` with CPU/GPU Cull, Mesh/Density LOD, DepthNormals OFF, and Additional Lights 0 fixed.
- The absolute camera route runs from `(352.45, 50, 234.35)` to `(270, 50, 300)`, with a fixed downward look angle and FOV 40. Every case starts at the same transform. Default warm-up is 5 s and measurement is 30 s; the measured path speed is approximately 3.51 m/s.
- Target resolution is 1920×1080, VSync is off, and target frame rate is unrestricted. The CSV records actual resolution and flags any mismatch.
- Eight benchmark Point Lights have fixed positions relative to the path, range 55, intensity 3, and no shadows. Non-main scene lights are disabled temporarily. The Vegetation cap remains eight.

## Metric semantics

`summary.csv` contains frame mean, P50/P95/P99, CPU/GPU means and coverage, FPS, GC allocation, database and renderer counts, camera/used additional lights, per-case benchmark warning/error counts, and environment metadata. `raw_frames.csv` preserves every accepted frame sample. Percentiles use the nearest-rank index `ceil(p × n) - 1` after sorting ascending. The first eight samples after the measured phase begins are discarded because Unity 2022.3 FrameTimingManager reports completed frames with latency. `player_log_audit.txt` records warnings and errors observed during the benchmark; Unity's compute-property-not-set message is classified as a warning even though its text has no `Warning:` prefix.

GPU timing uses `FrameTimingManager`. If timing stats are disabled or fewer than 80% of measured frames have a valid nonzero GPU value, average GPU time is `N/A`. Editor results always have `PortfolioEligible=False`. Sparse LOD counts reuse `VegetationDiagnostics` asynchronous Append Counter readback; `readbacks.csv` records the underlying snapshots. Crossfade can append one instance to two LODs, so unique visible instances are `N/A` when crossfade is active. With GPU Culling disabled, indirect args include reserved slots, so visible instance and LOD counts are `N/A` rather than capacity values.

`VisibleRenderGroupChunkRangeCount` sums Species/RenderGroup chunk ranges and is not a unique Database chunk count. `ShadowCandidateSlotReferenceCount` is the existing shadow dispatch workload and can include reserved slots; it is not a unique instance count. Exact unique shadow candidates and visible shadow instances have no reliable API; their columns are `N/A`. The dedicated shadow comparison changes only shadow settings: chunk/influence culling, shadow LOD offset, wind LOD, and alpha cutoff LOD. DepthNormals OFF skips only the Vegetation Renderer Feature's DepthNormals pass; other URP features may still request a camera normals texture.

The harness does not calculate percentage improvements. Compare raw measurements only after checking resolution, sample count, GPU coverage, and feature flags. A full 1M Standalone run has not been measured by the Editor smoke test.
