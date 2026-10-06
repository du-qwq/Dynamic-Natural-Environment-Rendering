# Performance & Diagnostics

## 1. Why Benchmark the System

大规模植被优化很容易出现“看起来更复杂，但实际没有更快”的情况。

因此项目包含专门 Benchmark 与 Diagnostics，而不是只展示最终 FPS。

## 2. Optimization Matrix

可以按阶段测试：

```text
Baseline
+ CPU Chunk Culling
+ GPU Instance Culling
+ Mesh / Density LOD
Fully Optimized Core
```

用于观察每一层优化是否真的产生收益。

## 3. Scale Tests

典型实例规模：

```text
100k
500k
1M
```

重点观察系统从中等规模扩展到极大规模时 CPU / GPU 瓶颈如何变化。

## 4. Feature Cost Tests

也可以测试：

- Additional Lights
- DepthNormals
- Shadow optimization
- Different renderer configurations

这些测试用于区分“植被数量成本”和“URP Feature 成本”。

## 5. Metrics

采集指标包括：

```text
CPU Frame Time
GPU Frame Time
Memory
GC
Visible Chunk Ranges
Compute Dispatch Count
Draw Count
LOD Count
Shadow Candidate Count
```

## 6. Automated Diagnostics

Diagnostics 用于验证：

- 不同 Camera 角度
- 不同 Renderer Feature 状态
- Shadow 开关
- GPU Culling 开关
- Scene View + Game View
- 分辨率变化
- Black frame / abnormal output
- Profiler markers

它们的作用不是替代人工验证，而是让常见回归更容易重复检测。

## 7. Interpreting Results

Benchmark 的重点不是只给一个最高 FPS，而是回答：

```text
Which optimization reduces CPU work?
Which optimization reduces GPU work?
Where does the bottleneck move?
At what instance scale does the strategy become useful?
```

最终结果应结合具体硬件环境展示。
