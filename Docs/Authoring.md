# Authoring Workflow

## 1. Goal

GPU 植被系统不能要求美术完全放弃 Scene View 工作流。

因此本项目允许三种来源共存：

```text
Manual
Procedural
Imported
```

最终都写入同一个 VegetationDatabase。

## 2. Manual Painter

Painter 面向局部、可控的美术编辑。

典型操作：

- Paint
- Erase
- Select
- Move
- Species selection
- Brush radius / density
- Placement constraints

Painter 修改的是 Database，而不是直接创建最终 Renderer GameObject。

## 3. Procedural Generator

程序化生成负责大面积基础分布。

候选点流程：

```text
Distribution
↓
Biome Mask
↓
Ground Projection
↓
Height Rule
↓
Slope Rule
↓
Terrain Layer Rule
↓
Parent Species Influence
↓
Exclusion Volume
↓
Density Volume
↓
Weighted Species Selection
↓
Minimum Spacing
↓
Transform Placement
↓
Database
```

### Distribution Modes

包括：

```text
Uniform
Noise
Clustered
```

不同模式负责“候选点怎么产生”，后续规则负责“候选点是否能留下”。

## 4. Parent Species Influence

某些 Species 可以依赖另一个 Species 的空间关系。

例如：

```text
Tree
↓
nearby zone
↓
Bush
↓
smaller nearby zone
↓
Flower
```

用于建立比完全独立随机更自然的层级分布。

## 5. Generation Ownership

程序生成实例记录：

```text
generationOwnerID
generationBatchID
```

因此某个 Generator 重新生成时，可以只清理自己上一批结果。

不会误删：

- 手工刷取实例
- Imported 实例
- 其他 Generator 结果

## 6. GameObject Baker

GameObject Baker 用于处理已有场景。

流程：

```text
Existing GameObject
↓
Analyze Prefab / LODGroup / Mesh / Material
↓
Resolve / Create Species
↓
Check duplicate
↓
Write to Database
↓
Mark as Imported
```

转换完成后，源对象可以根据需求：

```text
Keep
Disable Renderer
Disable GameObject
Delete
Archive
```

这让 GPU 化不要求重新布置整个场景。

## 7. Supporting Tools

系统还包含：

- Species Factory
- Material Adapter
- Grass Mesh Generator
- Collider Auto Estimator
- Terrain Color Map Baker

这些工具的共同目标是减少“为了进入 GPU Pipeline 必须手工重做资产”的成本。
