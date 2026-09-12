Dynamic Natural Environment Rendering - Water System Import
Unity target: 2022.3 / URP

Included:
- WaterLake.shader: main water shader (depth color, refraction, Fresnel, Gerstner, foam, planar reflection hooks, caustics hooks, ripple hooks)
- M_WaterLake_Base.mat: recommended first-pass material. Reflection / caustics / ripple-field effects are disabled so the base water can be verified first.
- M_WaterLake_Legacy.mat: the latest saved material from the old project, preserved for reference.
- PlanarReflectionRenderer.cs
- WaterCausticsRenderFeature.cs + WaterCausticsOverlay.shader + material
- Ripple / interaction scripts and shaders
- Original water normal / foam / caustics textures

FIRST IMPORT TEST:
1. Import this package.
2. Keep your current lake as a normal Plane.
3. Put the lake Plane at water Y = 40 (or your actual water level).
4. Assign M_WaterLake_Base to the Plane Renderer.
5. In the active URP Asset, enable Depth Texture and Opaque Texture.
6. Do NOT add PlanarReflectionRenderer, WaterInteractionController, or WaterCausticsRenderFeature yet.
7. First verify base water: depth color, refraction, normals, foam, Fresnel, Gerstner waves.

Next integration order:
Base water -> high-subdivision water grid -> planar reflection -> interaction ripple -> caustics.

Important:
The shader uses vertex-displaced Gerstner waves. A default Unity Plane is fine for initial shader validation but is too low-poly for the final lake. We will replace it with a dense rectangular grid while keeping the same Plane-style workflow.
