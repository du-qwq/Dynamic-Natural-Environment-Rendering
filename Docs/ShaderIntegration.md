# Shader Integration

## 1. Purpose

最终项目使用的是对现有资产 Shader 的适配，而不是单独依赖测试用 Vegetation Shader。

核心问题是：

> 如何让原本基于 GameObject Renderer 的 Shader 使用自定义 GPU Instance Transform，同时保留原有效果？

## 2. Instance Transform

传统路径：

```text
unity_ObjectToWorld
```

Indirect 路径：

```text
SV_InstanceID
↓
Visible Instance Index
↓
Source Instance Index
↓
VegetationMatrices
↓
Custom ObjectToWorld
```

公共 HLSL 负责隐藏这层差异。

## 3. Final Shader Groups

实际适配主要包含：

- Grass
- Tree Bark
- Tree Leaf
- Props / Rock

它们共享公共实例化、LOD 与 DepthNormals 支持。

## 4. Grass

Grass Shader 同时处理：

- Indirect Transform
- Wind
- Grass Interaction
- LOD Dither
- Main Light
- Additional Lights
- Shadow
- Depth
- DepthNormals

交互在风之后继续修改顶点位置。

## 5. Tree Bark / Leaf

Tree Shader 继续保留原资产的树木风动逻辑，同时改造实例矩阵获取方式。

Leaf 还可以包含独立 Flutter / Sway。

## 6. Props

Props / Rock 不需要草地交互，但仍需：

- Indirect Transform
- LOD
- Lighting
- Shadow
- Depth / DepthNormals

## 7. Pass Consistency

顶点变形不能只发生在 Forward Pass。

如果：

```text
Forward = deformed
Shadow = original
Depth = original
```

会产生阴影、SSAO、DepthNormals 与可见几何不一致。

因此实际适配会尽量让：

```text
Forward
ShadowCaster
DepthOnly
DepthNormals
```

使用一致的实例位置与必要的顶点变形。

## 8. Technical Artist Relevance

该模块强调的不是“从零写一个新 Shader”，而是：

```text
Existing Art Shader
↓
Preserve visual style
↓
Integrate custom rendering data
↓
Keep URP pass compatibility
```

这是 TA 工作中非常常见的一类问题。
