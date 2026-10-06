# Known Limitations

本页记录当前系统中不属于核心完成路径、需要版本约束或未来仍可优化的部分。

## 1. Legacy / Unused Shader Code

工程中可能仍保留旧版、测试版或备用 Vegetation Shader。

README 与核心文档只描述实际生产路径。

发布仓库时建议将未使用内容：

- 删除；
- 或移动到 `Legacy/` / `Experimental/`；
- 或在文件顶部明确标注状态。

## 2. URP Version Coupling

部分 Renderer Feature / internal integration 可能依赖具体 URP 版本实现。

如果使用 Reflection 或访问非稳定内部结构，应明确测试版本。

## 3. Hard-coded Project Paths

若工具脚本中存在：

- 固定 Scene Path
- 固定 Assets Include Path
- 固定 Build Output
- Windows-only executable path

公开仓库前建议改成配置项或相对工程结构。

## 4. Benchmark Portability

Standalone Benchmark 的结果与：

- GPU
- CPU
- Resolution
- URP settings
- Shadow settings
- Build target

强相关。

仓库中应记录测试环境，而不是把某一台机器的数据描述成普遍性能。

## 5. Procedural Generation

当前程序化规则以离线/编辑器生产工作流为主。

未来如果扩展到 Runtime Streaming，需要进一步处理：

- Async generation
- Chunk streaming
- GPU buffer growth
- Persistence
- Save / load

## 6. Collision

Collider Proxy 只解决近场真实物理碰撞，不代表所有远景实例都拥有 Physics representation。

这是有意的性能取舍。

## 7. Future Work

可以继续扩展：

- Runtime chunk streaming
- Hierarchical spatial structure
- GPU-driven placement
- Occlusion culling
- Better shadow LOD
- More generalized shader adapter
- Editor visualization for GPU slots / chunk occupancy
