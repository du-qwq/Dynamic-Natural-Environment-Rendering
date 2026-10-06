# Data Pipeline

## 1. VegetationDatabase

Database 是系统的统一实例数据源。

实例除了 Transform 外，还保留稳定身份与生产来源信息：

```text
persistentID
speciesID
speciesIndex
sourceType
generationOwnerID
generationBatchID
```

## 2. Persistent ID

`persistentID` 解决的是“这个实例是谁”。

如果只依赖数组索引：

```text
remove index 10
↓
11..N shift
↓
all following identities change
```

这会使 GPU Slot、Selection、Editor State、Imported Data 与程序生成批次管理都变得脆弱。

因此实例拥有独立于数组布局的稳定 ID。

## 3. Species ID

Species 同样拥有稳定 ID。

运行时 `speciesIndex` 可以因为排序、加载、增删而变化，但 `speciesID` 不需要变化。

Renderer、Generator 与 Editor Tool 可以用稳定 ID 建立关系，而不是依赖“当前正好是第几个 Species”。

## 4. Chunk Spatial Index

实例按世界空间划分到 Chunk。

Chunk 主要用于：

- Scene View 局部查询
- Painter 操作
- Near-player collision scan
- CPU coarse culling
- GPU candidate range reduction
- Partial rebuild

Chunk 不是最终可见性判断，而是粗粒度空间管理单元。

## 5. ChangeSet

Database 发生变化时，不要求 Renderer 重新读取全部实例。

ChangeSet 可以表达：

```text
Added
Removed
TransformUpdated
SpeciesAffected
FullRebuildRequired
```

Renderer 根据 ChangeSet 决定局部更新还是退化为较大范围重建。

## 6. GPU Slot Mapping

Renderer 内部为 `Species × Chunk` 管理 Slot。

```text
persistentID -> slot
slot -> instance data
freeSlots
capacity
activeCount
```

删除：

```text
instance removed
↓
lookup persistentID
↓
release slot
↓
push slot into free list
```

新增：

```text
instance added
↓
reuse free slot if available
↓
otherwise grow / rebuild capacity
```

跨 Chunk：

```text
release old chunk slot
↓
allocate new chunk slot
↓
upload changed data only
```

## 7. Batched Upload

多个 Slot 发生连续变化时，可以先排序并合并连续区间：

```text
4,5,6,9,10
```

转换为：

```text
[4..6]
[9..10]
```

从而减少 Buffer API 调用。

## 8. Source Tracking

系统区分实例来源：

```text
Manual
Procedural
Imported
```

程序化实例还可以通过 `generationOwnerID / generationBatchID` 区分属于哪个生成器与哪次生成。

这使重新生成时可以只清理自己负责的数据，不影响手工刷取或其他生成器产生的实例。

## 9. Rebuild Strategy

系统不强制所有变化都走局部更新。

当结构发生大变化、容量不匹配或无法安全应用 Delta 时，可以退化为：

```text
Species Rebuild
```

再必要时：

```text
Full Rebuild
```

核心原则是：

> 优先局部更新，但不为了局部更新牺牲状态正确性。
