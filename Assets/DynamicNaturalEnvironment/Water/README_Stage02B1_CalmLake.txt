Stage 02B1 - Calm Lake Core

目标：先做“平静高山湖核心”，不接 Gerstner、泡沫、折射、Planar Reflection、Caustics、Ripple。

包含：
- 深浅水颜色
- 深度透明度
- 吸收
- 散射
- 双流世界空间 Normal
- 主光源高光
- Fresnel
- 临时环境反射颜色（仅用于 Stage 02B1，Stage 02B2 将由 Planar Reflection 替代）

使用：
1. 将 Lake_Placeholder / Plane 的材质替换为 M_WaterCalmLake_Core。
2. 需要 URP Depth Texture = On。
3. Opaque Texture 本阶段不依赖，可保持 On，后续浅水折射会使用。
4. 本阶段不需要高细分网格；当前高细分网格可以暂时继续用，但不会产生任何额外视觉效果。
5. 不要挂 PlanarReflectionRenderer、Ripple Controller、Caustics Render Feature。

本阶段验收：
- 水面整体平静
- 深水偏蓝绿、岸边自然变浅
- 没有白色泡沫圈
- 有细小、缓慢的水纹
- 太阳方向可以看到高光
- 斜视时 Fresnel 更明显
