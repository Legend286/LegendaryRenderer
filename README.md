**LegendaryRenderer**

LegendaryRenderer is a high-performance C# real-time rendering engine built from the ground up with a focus on physically based rendering (PBR), advanced lighting techniques, and an extensible editor interface.

✨ Features

✅ Implemented

- 💡 Spot, Point, and Orthographic Lights

- 🌫️ Shadow Mapping (Per-light shadow passes)

- 💎 Physically Based Rendering with Brute Force Specular

- 🎥 Camera System

- 🖱️ IMGUI-based Editor UI

- 🎞️ Per-pixel Object and Camera Motion Blur

- 🧪 Rough-Metallic PBR Workflow

🔜 Coming Soon

- 📦 Instanced Shadow Map Generation

- 🌐 Cubemap Convolution and Mip Generation

- 🧩 GameObjects with Components and ECS

- 🪟 Docked Viewport UI, Outliner & Scene Graph

- 🟦 Planar Textured Area Lights with Linearly Transformed Cosines (LTC)

- 🌞 Cascaded Shadow-Mapped Directional Lights

- 🔵 Tube and Sphere Area Lights (WIP)

🧠 Tech Stack

C# with OpenTK for rendering

GLSL for shaders

Dear ImGui for editor interface

Math & utility libs: Custom or based on System.Numerics

🧪 Development Status

LegendaryRenderer is currently in active development. Expect frequent changes and new features being added as the architecture evolves.

🗂 Project Structure

```
📁 LegendaryRenderer/
 ┣━ Program.cs              # Entry point
 ┣━ LegendaryRuntime/
 ┃  ┣━ Application/         # Application Specifics & Base Engine Implementation
 ┃  ┣━ Engine/              # Engine Root
 ┃  ┃  ┣━ Components/       # **UNUSED**
 ┃  ┃  ┣━ EngineTypes/      # Base Classes and Helpers
 ┃  ┃  ┣━ GameObjects/      # Game Object Derived Classes
 ┃  ┃  ┣━ Renderer/         # Rendering Systems
 ┃  ┃  ┣━ Shaders/          # Shader Sources and Systems to Handle Them
 ┃  ┃  ┣━ Utilities/        # Utility classes like GL and Math helper classes
 ┃  ┣━ Resources/           # Models, textures, etc.
```
🎮 Goals

Real-time photorealistic rendering

Full-featured in-engine editor

Modern lighting techniques and material workflows

Highly extensible architecture for scene management and ECS

📄 License

TBD
