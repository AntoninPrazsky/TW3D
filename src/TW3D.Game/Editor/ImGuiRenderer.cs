//Adapted from the ImGui.NET sample for XNA-likes (src/ImGui.NET.SampleProgram.XNA/ImGuiRenderer.cs),
//Copyright (c) 2017 Eric Mellino and ImGui.NET contributors, MIT License:
//
//Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated
//documentation files (the "Software"), to deal in the Software without restriction, including without limitation the
//rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and to permit
//persons to whom the Software is furnished to do so, subject to the following conditions:
//
//The above copyright notice and this permission notice shall be included in all copies or substantial portions of the
//Software.
//
//THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE
//WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR
//COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR
//OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.

using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using ImGuiNET;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace TW3D.Editor
{
    /// <summary>
    /// Draws the user interface of Dear ImGui (the ImGui.NET package) with MonoGame and feeds it the mouse and the
    /// keyboard: the context of ImGui, its font atlas as a texture, the textures the interface shows (bound by
    /// <see cref="BindTexture"/>) and the triangles ImGui produces, drawn by a <see cref="BasicEffect"/> with the
    /// scissor test. A frame is <see cref="BeforeLayout"/>, the ImGui calls of the interface, <see cref="AfterLayout"/>.
    /// </summary>
    sealed class ImGuiRenderer : IDisposable
    {
        //The vertex of ImGui (ImDrawVert): position, texture coordinates, colour
        private static readonly int VertexSize;
        private static readonly VertexDeclaration VertexDeclaration;

        //Mouse wheel units of MonoGame per notch
        private const float WheelDelta = 120f;

        //The modifier keys ImGui gets as its modifiers (either side) and as keys of their own
        private static readonly (Keys Key, ImGuiKey ImGuiKey)[] ModifierKeys =
        {
            (Keys.LeftShift, ImGuiKey.LeftShift), (Keys.RightShift, ImGuiKey.RightShift),
            (Keys.LeftControl, ImGuiKey.LeftCtrl), (Keys.RightControl, ImGuiKey.RightCtrl),
            (Keys.LeftAlt, ImGuiKey.LeftAlt), (Keys.RightAlt, ImGuiKey.RightAlt)
        };

        private readonly Game game;
        private readonly GraphicsDevice device;
        private readonly RasterizerState rasterizerState;
        private readonly Keys[] allKeys = Enum.GetValues<Keys>();
        private readonly Dictionary<IntPtr, Texture2D> textures = new Dictionary<IntPtr, Texture2D>();
        //The next id of a bound texture; 0 is kept out, as ImGui takes it for no texture
        private int nextTextureId = 1;
        private IntPtr? fontTextureId;
        private Texture2D fontTexture;
        //The settings file of ImGui, kept alive for ImGui, which holds the pointer
        private IntPtr iniFileName;

        private BasicEffect effect;
        private byte[] vertexData, indexData;
        private DynamicVertexBuffer vertexBuffer;
        private DynamicIndexBuffer indexBuffer;
        private int vertexBufferSize, indexBufferSize;
        private int scrollWheelValue, horizontalScrollWheelValue;
        private bool disposed;

        static unsafe ImGuiRenderer()
        {
            VertexSize = sizeof(ImDrawVert);
            VertexDeclaration = new VertexDeclaration(VertexSize,
                new VertexElement(0, VertexElementFormat.Vector2, VertexElementUsage.Position, 0),
                new VertexElement(8, VertexElementFormat.Vector2, VertexElementUsage.TextureCoordinate, 0),
                new VertexElement(16, VertexElementFormat.Color, VertexElementUsage.Color, 0));
        }

        /// <param name="settingsFile">Where ImGui keeps the positions and sizes of its windows; null for nowhere.</param>
        public unsafe ImGuiRenderer(Game game, string settingsFile)
        {
            this.game = game;
            device = game.GraphicsDevice;

            IntPtr context = ImGui.CreateContext();
            ImGui.SetCurrentContext(context);
            ImGuiIOPtr io = ImGui.GetIO();
            if (settingsFile != null)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(settingsFile));
                iniFileName = Marshal.StringToHGlobalAnsi(settingsFile);
            }
            io.NativePtr->IniFilename = (byte*)iniFileName;
            io.NativePtr->LogFilename = null;

            rasterizerState = new RasterizerState
            {
                CullMode = CullMode.None,
                DepthBias = 0,
                FillMode = FillMode.Solid,
                MultiSampleAntiAlias = false,
                ScissorTestEnable = true,
                SlopeScaleDepthBias = 0
            };

            game.Window.TextInput += OnTextInput;
        }

        /// <summary>
        /// Makes a font the font of the interface: a TrueType file at a size in pixels, or the built-in font of ImGui
        /// when the file is null or missing. Called between frames, before <see cref="BeforeLayout"/>.
        /// </summary>
        public void LoadFont(string fontFile, float size)
        {
            ImFontAtlasPtr fonts = ImGui.GetIO().Fonts;
            fonts.Clear();
            if (fontFile != null && File.Exists(fontFile))
                fonts.AddFontFromFileTTF(fontFile, size);
            else
                fonts.AddFontDefault();
            RebuildFontAtlas();
        }

        /// <summary>Builds the font atlas of ImGui into a texture; called whenever the fonts change.</summary>
        private unsafe void RebuildFontAtlas()
        {
            ImGuiIOPtr io = ImGui.GetIO();
            io.Fonts.GetTexDataAsRGBA32(out byte* pixelData, out int width, out int height, out int bytesPerPixel);
            byte[] pixels = new byte[width * height * bytesPerPixel];
            Marshal.Copy(new IntPtr(pixelData), pixels, 0, pixels.Length);

            Texture2D texture = new Texture2D(device, width, height, false, SurfaceFormat.Color);
            texture.SetData(pixels);

            if (fontTextureId.HasValue)
            {
                UnbindTexture(fontTextureId.Value);
                fontTexture.Dispose();
            }
            fontTexture = texture;
            fontTextureId = BindTexture(texture);
            io.Fonts.SetTexID(fontTextureId.Value);
            io.Fonts.ClearTexData();
        }

        /// <summary>Gives a texture an id for ImGui (ImGui.Image and the like take it).</summary>
        public IntPtr BindTexture(Texture2D texture)
        {
            IntPtr id = new IntPtr(nextTextureId++);
            textures.Add(id, texture);
            return id;
        }

        /// <summary>Takes the id of a texture back; the texture itself is not disposed.</summary>
        public void UnbindTexture(IntPtr textureId)
        {
            textures.Remove(textureId);
        }

        /// <summary>Starts a frame of the interface: the input of MonoGame goes to ImGui.</summary>
        public void BeforeLayout(GameTime gameTime)
        {
            ImGuiIOPtr io = ImGui.GetIO();
            io.DeltaTime = Math.Max((float)gameTime.ElapsedGameTime.TotalSeconds, 1e-6f);
            UpdateInput(io);
            ImGui.NewFrame();
        }

        /// <summary>Ends a frame of the interface and draws it.</summary>
        public void AfterLayout()
        {
            ImGui.Render();
            RenderDrawData(ImGui.GetDrawData());
        }

        private void UpdateInput(ImGuiIOPtr io)
        {
            io.DisplaySize = new System.Numerics.Vector2(device.PresentationParameters.BackBufferWidth, device.PresentationParameters.BackBufferHeight);
            io.DisplayFramebufferScale = new System.Numerics.Vector2(1f, 1f);

            MouseState mouse = Mouse.GetState();
            KeyboardState keyboard = Keyboard.GetState();
            if (game.IsActive)
            {
                io.AddMousePosEvent(mouse.X, mouse.Y);
                io.AddMouseButtonEvent(0, mouse.LeftButton == ButtonState.Pressed);
                io.AddMouseButtonEvent(1, mouse.RightButton == ButtonState.Pressed);
                io.AddMouseButtonEvent(2, mouse.MiddleButton == ButtonState.Pressed);
                io.AddMouseButtonEvent(3, mouse.XButton1 == ButtonState.Pressed);
                io.AddMouseButtonEvent(4, mouse.XButton2 == ButtonState.Pressed);
                io.AddMouseWheelEvent((mouse.HorizontalScrollWheelValue - horizontalScrollWheelValue) / WheelDelta,
                    (mouse.ScrollWheelValue - scrollWheelValue) / WheelDelta);
            }
            else
            {
                //The mouse is elsewhere
                io.AddMousePosEvent(-float.MaxValue, -float.MaxValue);
                io.AddFocusEvent(false);
            }
            scrollWheelValue = mouse.ScrollWheelValue;
            horizontalScrollWheelValue = mouse.HorizontalScrollWheelValue;

            bool shift = false, control = false, alt = false;
            foreach ((Keys key, ImGuiKey imGuiKey) in ModifierKeys)
            {
                bool down = keyboard.IsKeyDown(key);
                io.AddKeyEvent(imGuiKey, down);
                switch (key)
                {
                    case Keys.LeftShift: case Keys.RightShift: shift |= down; break;
                    case Keys.LeftControl: case Keys.RightControl: control |= down; break;
                    default: alt |= down; break;
                }
            }
            io.AddKeyEvent(ImGuiKey.ModShift, shift);
            io.AddKeyEvent(ImGuiKey.ModCtrl, control);
            io.AddKeyEvent(ImGuiKey.ModAlt, alt);

            foreach (Keys key in allKeys)
            {
                ImGuiKey imGuiKey = Map(key);
                if (imGuiKey != ImGuiKey.None)
                    io.AddKeyEvent(imGuiKey, keyboard.IsKeyDown(key));
            }
        }

        /// <summary>The key of ImGui for a key of MonoGame; None for keys ImGui has no use for (and the modifiers, sent apart).</summary>
        private static ImGuiKey Map(Keys key)
        {
            return key switch
            {
                Keys.Back => ImGuiKey.Backspace,
                Keys.Tab => ImGuiKey.Tab,
                Keys.Enter => ImGuiKey.Enter,
                Keys.CapsLock => ImGuiKey.CapsLock,
                Keys.Escape => ImGuiKey.Escape,
                Keys.Space => ImGuiKey.Space,
                Keys.PageUp => ImGuiKey.PageUp,
                Keys.PageDown => ImGuiKey.PageDown,
                Keys.End => ImGuiKey.End,
                Keys.Home => ImGuiKey.Home,
                Keys.Left => ImGuiKey.LeftArrow,
                Keys.Right => ImGuiKey.RightArrow,
                Keys.Up => ImGuiKey.UpArrow,
                Keys.Down => ImGuiKey.DownArrow,
                Keys.PrintScreen => ImGuiKey.PrintScreen,
                Keys.Insert => ImGuiKey.Insert,
                Keys.Delete => ImGuiKey.Delete,
                >= Keys.D0 and <= Keys.D9 => ImGuiKey._0 + (key - Keys.D0),
                >= Keys.A and <= Keys.Z => ImGuiKey.A + (key - Keys.A),
                >= Keys.NumPad0 and <= Keys.NumPad9 => ImGuiKey.Keypad0 + (key - Keys.NumPad0),
                Keys.Multiply => ImGuiKey.KeypadMultiply,
                Keys.Add => ImGuiKey.KeypadAdd,
                Keys.Subtract => ImGuiKey.KeypadSubtract,
                Keys.Decimal => ImGuiKey.KeypadDecimal,
                Keys.Divide => ImGuiKey.KeypadDivide,
                >= Keys.F1 and <= Keys.F24 => ImGuiKey.F1 + (key - Keys.F1),
                Keys.NumLock => ImGuiKey.NumLock,
                Keys.Scroll => ImGuiKey.ScrollLock,
                Keys.OemSemicolon => ImGuiKey.Semicolon,
                Keys.OemPlus => ImGuiKey.Equal,
                Keys.OemComma => ImGuiKey.Comma,
                Keys.OemMinus => ImGuiKey.Minus,
                Keys.OemPeriod => ImGuiKey.Period,
                Keys.OemQuestion => ImGuiKey.Slash,
                Keys.OemTilde => ImGuiKey.GraveAccent,
                Keys.OemOpenBrackets => ImGuiKey.LeftBracket,
                Keys.OemCloseBrackets => ImGuiKey.RightBracket,
                Keys.OemPipe => ImGuiKey.Backslash,
                Keys.OemQuotes => ImGuiKey.Apostrophe,
                _ => ImGuiKey.None
            };
        }

        private void OnTextInput(object sender, TextInputEventArgs e)
        {
            if (e.Character != '\t')
                ImGui.GetIO().AddInputCharacter(e.Character);
        }

        private void RenderDrawData(ImDrawDataPtr drawData)
        {
            Viewport lastViewport = device.Viewport;
            Rectangle lastScissor = device.ScissorRectangle;
            RasterizerState lastRasterizer = device.RasterizerState;
            DepthStencilState lastDepthStencil = device.DepthStencilState;
            Color lastBlendFactor = device.BlendFactor;
            BlendState lastBlendState = device.BlendState;

            device.BlendFactor = Color.White;
            device.BlendState = BlendState.NonPremultiplied;
            device.RasterizerState = rasterizerState;
            device.DepthStencilState = DepthStencilState.None;
            device.SamplerStates[0] = SamplerState.LinearClamp;

            drawData.ScaleClipRects(ImGui.GetIO().DisplayFramebufferScale);
            device.Viewport = new Viewport(0, 0, device.PresentationParameters.BackBufferWidth, device.PresentationParameters.BackBufferHeight);

            UpdateBuffers(drawData);
            RenderCommandLists(drawData);

            device.Viewport = lastViewport;
            device.ScissorRectangle = lastScissor;
            device.RasterizerState = lastRasterizer;
            device.DepthStencilState = lastDepthStencil;
            device.BlendState = lastBlendState;
            device.BlendFactor = lastBlendFactor;
        }

        /// <summary>Copies the vertices and indices of the frame into the buffers, grown when they do not fit.</summary>
        private unsafe void UpdateBuffers(ImDrawDataPtr drawData)
        {
            if (drawData.TotalVtxCount == 0)
                return;

            if (drawData.TotalVtxCount > vertexBufferSize)
            {
                vertexBuffer?.Dispose();
                vertexBufferSize = (int)(drawData.TotalVtxCount * 1.5f);
                vertexBuffer = new DynamicVertexBuffer(device, VertexDeclaration, vertexBufferSize, BufferUsage.None);
                vertexData = new byte[vertexBufferSize * VertexSize];
            }
            if (drawData.TotalIdxCount > indexBufferSize)
            {
                indexBuffer?.Dispose();
                indexBufferSize = (int)(drawData.TotalIdxCount * 1.5f);
                indexBuffer = new DynamicIndexBuffer(device, IndexElementSize.SixteenBits, indexBufferSize, BufferUsage.None);
                indexData = new byte[indexBufferSize * sizeof(ushort)];
            }

            int vertexOffset = 0, indexOffset = 0;
            for (int n = 0; n < drawData.CmdListsCount; n++)
            {
                ImDrawListPtr commands = drawData.CmdLists[n];
                fixed (void* vertices = &vertexData[vertexOffset * VertexSize])
                fixed (void* indices = &indexData[indexOffset * sizeof(ushort)])
                {
                    Buffer.MemoryCopy((void*)commands.VtxBuffer.Data, vertices, vertexData.Length - vertexOffset * VertexSize, commands.VtxBuffer.Size * VertexSize);
                    Buffer.MemoryCopy((void*)commands.IdxBuffer.Data, indices, indexData.Length - indexOffset * sizeof(ushort), commands.IdxBuffer.Size * sizeof(ushort));
                }
                vertexOffset += commands.VtxBuffer.Size;
                indexOffset += commands.IdxBuffer.Size;
            }

            //Discarded: the data of the last frame is not needed, so the driver need not wait for the GPU to finish with it
            vertexBuffer.SetData(vertexData, 0, drawData.TotalVtxCount * VertexSize, SetDataOptions.Discard);
            indexBuffer.SetData(indexData, 0, drawData.TotalIdxCount * sizeof(ushort), SetDataOptions.Discard);
        }

        private void RenderCommandLists(ImDrawDataPtr drawData)
        {
            if (drawData.TotalVtxCount == 0)
                return;
            device.SetVertexBuffer(vertexBuffer);
            device.Indices = indexBuffer;

            int vertexOffset = 0, indexOffset = 0;
            for (int n = 0; n < drawData.CmdListsCount; n++)
            {
                ImDrawListPtr commands = drawData.CmdLists[n];
                for (int i = 0; i < commands.CmdBuffer.Size; i++)
                {
                    ImDrawCmdPtr command = commands.CmdBuffer[i];
                    if (command.ElemCount == 0)
                        continue;
                    if (!textures.TryGetValue(command.TextureId, out Texture2D texture))
                        throw new InvalidOperationException("ImGui draws a texture that was not bound: " + command.TextureId);

                    device.ScissorRectangle = new Rectangle((int)command.ClipRect.X, (int)command.ClipRect.Y,
                        (int)(command.ClipRect.Z - command.ClipRect.X), (int)(command.ClipRect.W - command.ClipRect.Y));

                    effect ??= new BasicEffect(device) { TextureEnabled = true, VertexColorEnabled = true, World = Matrix.Identity, View = Matrix.Identity };
                    effect.Projection = Matrix.CreateOrthographicOffCenter(0f, drawData.DisplaySize.X, drawData.DisplaySize.Y, 0f, -1f, 1f);
                    effect.Texture = texture;
                    foreach (EffectPass pass in effect.CurrentTechnique.Passes)
                    {
                        pass.Apply();
                        device.DrawIndexedPrimitives(PrimitiveType.TriangleList, (int)command.VtxOffset + vertexOffset,
                            (int)command.IdxOffset + indexOffset, (int)command.ElemCount / 3);
                    }
                }
                vertexOffset += commands.VtxBuffer.Size;
                indexOffset += commands.IdxBuffer.Size;
            }
        }

        public void Dispose()
        {
            if (disposed)
                return;
            disposed = true;
            game.Window.TextInput -= OnTextInput;
            vertexBuffer?.Dispose();
            indexBuffer?.Dispose();
            effect?.Dispose();
            fontTexture?.Dispose();
            rasterizerState.Dispose();
            ImGui.DestroyContext();
            if (iniFileName != IntPtr.Zero)
                Marshal.FreeHGlobal(iniFileName);
        }
    }
}
