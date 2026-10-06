# Interaction & Collision

## 1. Two Different Problems

植被交互被拆成两类：

```text
Visual Interaction
Physical Collision
```

草更适合 GPU 顶点交互；树木、岩石等才需要真实 Collider。

---

## 2. GPU Grass Interaction

流程：

```text
VegetationGrassInteractor
↓
position / radius / strength
velocity / motion
priority
↓
VegetationInteractionController
↓
select limited interactors
↓
Shader global arrays
↓
Grass vertex deformation
```

## 3. Bounded Interaction Budget

Shader 支持固定上限的 Interactor 数量。

Controller 不会无上限上传场景中所有角色，而会根据：

```text
Priority
Camera Distance
```

选择最重要的一小部分。

因此每个草顶点处理的交互源数量有确定上限。

## 4. Motion-aware Bend Direction

静止时主要使用：

```text
Interactor -> Grass
```

的径向方向。

移动时逐渐混入：

```text
Interactor Movement Direction
```

因此角色移动时草更像被沿运动方向扫开。

## 5. Stable Interaction Space

交互范围基于风变形前的位置判断，而最终 Offset 应用到风变形后的位置。

这样风不会让顶点在交互边界附近反复进出范围。

## 6. Root-to-tip Mask

Grass Mesh 提供根部到尖端的权重。

```text
Root -> low movement
Tip  -> high movement
```

因此交互主要表现为弯曲，而不是整棵草平移。

## 7. Multiple Interactors

多个 Interactor 的水平位移可以累积，但最终受最大位移限制。

Flatten 使用最大值而不是简单累加，避免多个角色重叠时把草不断向地下压。

## 8. Pass Synchronization

交互同时进入：

- Forward
- ShadowCaster
- DepthOnly
- DepthNormals

保证阴影、深度与画面中的草形状一致。

---

## 9. Near-field Collider Proxy

树木、岩石等需要真实物理交互的 Species 使用 Collider Proxy。

```text
Player
↓
Nearby Chunks
↓
Collision-enabled instances
↓
Activation radius
↓
Proxy Pool
```

离开较大的 Deactivation Radius 后回收。

Activation 与 Deactivation 使用不同阈值形成 hysteresis，避免边界频繁开关。

## 10. Why Not Use GameObjects for All Instances?

如果几十万实例都保留：

```text
GameObject
Transform
Renderer
Collider
```

会失去 GPU-driven 渲染的主要收益。

因此：

- Render Representation：GPU Instance
- Physical Representation：Near-field Proxy

二者解耦。
