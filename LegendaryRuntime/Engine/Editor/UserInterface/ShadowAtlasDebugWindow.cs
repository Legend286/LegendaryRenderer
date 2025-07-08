using ImGuiNET;
using LegendaryRenderer.LegendaryRuntime.Engine.Engine.Renderer;
using LegendaryRenderer.LegendaryRuntime.Engine.Engine.GameObjects;
using OpenTK.Mathematics;
using System.Collections.Generic;
using System.Linq;

namespace LegendaryRenderer.LegendaryRuntime.Engine.Editor.UserInterface
{
    public class ShadowAtlasDebugWindow
    {
        private bool isOpen = true;
        private float atlasDisplaySize = 512.0f;
        
        public void Draw()
        {
            if (!isOpen) return;
            
            ImGui.Begin("Shadow Atlas Debug", ref isOpen, ImGuiWindowFlags.AlwaysAutoResize);
            
            var shadowAtlas = Engine.Engine.ShadowAtlas;
            if (shadowAtlas == null)
            {
                ImGui.Text("Shadow Atlas not initialized");
                ImGui.End();
                return;
            }
            
            // Show atlas settings
            ImGui.Text($"Atlas Size: {shadowAtlas.AtlasSize}x{shadowAtlas.AtlasSize}");
            ImGui.Text($"Atlas Enabled: {Engine.Engine.UseShadowAtlas}");
            ImGui.Text($"Enable Shadows: {Engine.Engine.EnableShadows}");
            ImGui.Separator();
            
            // Show atlas texture
            ImGui.Text("Shadow Atlas Texture:");
            ImGui.SliderFloat("Display Size", ref atlasDisplaySize, 128.0f, 1024.0f);
            
            // Display the atlas texture
            var atlasTexture = shadowAtlas.AtlasTexture;
            if (atlasTexture != 0)
            {
                ImGui.Image((nint)atlasTexture, new System.Numerics.Vector2(atlasDisplaySize, atlasDisplaySize));
            }
            else
            {
                ImGui.Text("Atlas texture not created");
            }
            
            ImGui.Separator();
            
            // Show allocated tiles information
            var allocatedEntries = GetAllocatedEntries(shadowAtlas);
            ImGui.Text($"Allocated Tiles: {allocatedEntries.Count}");
            
            if (ImGui.BeginTable("Atlas Allocations", 6, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg))
            {
                ImGui.TableSetupColumn("Light Name");
                ImGui.TableSetupColumn("Type");
                ImGui.TableSetupColumn("Face");
                ImGui.TableSetupColumn("Tile Size");
                ImGui.TableSetupColumn("Position");
                ImGui.TableSetupColumn("Priority");
                ImGui.TableHeadersRow();
                
                foreach (var entry in allocatedEntries.OrderByDescending(e => e.Priority))
                {
                    ImGui.TableNextRow();
                    ImGui.TableSetColumnIndex(0);
                    ImGui.Text(entry.Light.Name);
                    
                    ImGui.TableSetColumnIndex(1);
                    ImGui.Text(entry.Light.Type.ToString());
                    
                    ImGui.TableSetColumnIndex(2);
                    ImGui.Text(entry.Face.ToString());
                    
                    ImGui.TableSetColumnIndex(3);
                    ImGui.Text($"{entry.Tile.Bounds.Width}x{entry.Tile.Bounds.Height}");
                    
                    ImGui.TableSetColumnIndex(4);
                    ImGui.Text($"({entry.Tile.Bounds.X}, {entry.Tile.Bounds.Y})");
                    
                    ImGui.TableSetColumnIndex(5);
                    ImGui.Text($"{entry.Priority:F1}");
                }
                
                ImGui.EndTable();
            }
            
            ImGui.Separator();
            
            // Show light information
            var lights = Engine.Engine.GameObjects.OfType<Light>().Where(l => l.EnableShadows).ToList();
            ImGui.Text($"Shadow Casting Lights: {lights.Count}");
            
            if (ImGui.BeginTable("Shadow Lights", 5, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg))
            {
                ImGui.TableSetupColumn("Name");
                ImGui.TableSetupColumn("Type");
                ImGui.TableSetupColumn("Visible");
                ImGui.TableSetupColumn("Range");
                ImGui.TableSetupColumn("Intensity");
                ImGui.TableHeadersRow();
                
                foreach (var light in lights)
                {
                    ImGui.TableNextRow();
                    ImGui.TableSetColumnIndex(0);
                    ImGui.Text(light.Name);
                    
                    ImGui.TableSetColumnIndex(1);
                    ImGui.Text(light.Type.ToString());
                    
                    ImGui.TableSetColumnIndex(2);
                    ImGui.Text(light.IsVisible ? "Yes" : "No");
                    
                    ImGui.TableSetColumnIndex(3);
                    ImGui.Text($"{light.Range:F1}");
                    
                    ImGui.TableSetColumnIndex(4);
                    ImGui.Text($"{light.Intensity:F1}");
                }
                
                ImGui.EndTable();
            }
            
            ImGui.Separator();
            
            // Manual atlas update button
            if (ImGui.Button("Force Atlas Update"))
            {
                shadowAtlas.MarkDirty();
            }
            
            ImGui.End();
        }
        
        private List<AtlasEntry> GetAllocatedEntries(ShadowAtlas atlas)
        {
            return atlas.AllocatedEntries;
        }
        
        public bool IsOpen 
        { 
            get => isOpen; 
            set => isOpen = value; 
        }
    }
} 