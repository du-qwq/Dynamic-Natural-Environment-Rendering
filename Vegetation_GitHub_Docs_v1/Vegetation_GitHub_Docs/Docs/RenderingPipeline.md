# Rendering Pipeline

## 1. Rendering Goal

渲染层负责把 Database 中的大规模实例转换为少量 GPU Buffer、Compute Dispatch 与 Indirect Draw。

主路径：

```text
Database
↓
CPU Chunk Culling
↓
Candidate Ranges
↓
Compute Instance Culling
↓
LOD Classification
↓
Visible Index Buffers
↓
Indirect Args
↓
DrawMeshInstancedIndirect
```

## 2. CPU Chunk Culling

CPU 不逐实例判断可见性，只判断 Chunk Bounds。

这样将：

```text
O(all instances)
```

的 CPU 检查转换为：

```text
O(chunks)
```

的粗筛。

连续可见 Chunk 范围还可以合并，减少后续 Dispatch 参数数量。

## 3. GPU Instance Culling

GPU 对候选范围内的实例执行更细粒度判断：

- Camera frustum
- Distance
- Mesh LOD
- Density LOD
- Cull distance

结果写入不同的 Visible Index Buffer。

## 4. Mesh LOD

一个 Species 可以具有多级 Mesh LOD：

```text
LOD0
LOD1
LOD2
LOD3
Cull
```

Compute Shader 根据距离将 Source Instance 分流到对应 LOD 的可见索引列表。

## 5. Cross Fade

在 LOD 边界附近，相邻两个 LOD 可以同时存在。

Shader 使用稳定 Dither 进行像素级裁剪，实现比硬切更平滑的转换。

## 6. Density LOD

草地远景更适合减少实例密度，而不是仅切换 Mesh。

Density LOD 使用稳定 Hash：

```text
instance identity
↓
deterministic random
↓
keep / discard
```

因此同一个实例不会因为每帧随机数不同而闪烁。

## 7. Indirect Args

Visible Buffer 的计数被用于构建 Draw Indirect 参数。

CPU 不需要读取回 GPU 可见实例数量后再提交绘制。

## 8. Shadow / Camera Separation

Camera 可见性与 Shadow 可见性不是同一问题。

Shadow Candidate 需要按照光照与阴影范围重新考虑，不能简单复用 Camera Frustum 结果，否则可能出现 Camera 外投影者缺失。

## 9. Draw Submission

最终由 Renderer 对：

```text
Species × LOD × Pass
```

提交 Indirect Draw。

Shader 再通过：

```text
SV_InstanceID
↓
VisibleIndices
↓
Source Instance Index
↓
VegetationMatrices
```

恢复正确的 Object-to-World Transform。
