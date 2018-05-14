﻿using System;
using System.Collections.Generic;
using ImGui.Layout;
using System.Diagnostics;
using System.Threading;
using ImGui.Common;
using ImGui.Common.Primitive;
using ImGui.GraphicsAbstraction;
using ImGui.Input;
using ImGui.OSAbstraction.Graphics;
using ImGui.Rendering;

namespace ImGui
{
    [DebuggerDisplay("{Name}:[{ID}]")]
    internal class Window
    {
        /// <summary>
        /// ID
        /// </summary>
        public int ID;

        /// <summary>
        /// Name/Title
        /// </summary>
        public string Name;

        /// <summary>
        /// Position (rounded-up to nearest pixel)
        /// </summary>
        /// <remarks>Top-left point relative to the form.</remarks>
        public Point Position;

        /// <summary>
        /// Position
        /// </summary>
        public Point PosFloat;

        /// <summary>
        /// Size
        /// </summary>
        public Size Size;

        /// <summary>
        /// Size when the window is not collapsed.
        /// </summary>
        public Size FullSize { get; set; }

        /// <summary>
        /// Window flags. See <see cref="WindowFlags"/>.
        /// </summary>
        public WindowFlags Flags;

        /// <summary>
        /// Style
        /// </summary>
        public GUIStyle Style;

        /// <summary>
        /// Style of the title bar
        /// </summary>
        public GUIStyle TitleBarStyle;

        /// <summary>
        /// Draw list
        /// </summary>
        public DrawList DrawList;

        /// <summary>
        /// Render tree
        /// </summary>
        public RenderTree RenderTree;

        public Rect ClipRect;

        /// <summary>
        /// Scroll values: (horizontal, vertical)
        /// </summary>
        public Vector Scroll;

        /// <summary>
        /// Last frame count when this window is active.
        /// </summary>
        public long LastActiveFrame;

        /// <summary>
        /// stack layout manager
        /// </summary>
        public StackLayout StackLayout { get; set; }

        /// <summary>
        /// ID stack
        /// </summary>
        public Stack<int> IDStack { get; set; } = new Stack<int>();

        #region Window original sub nodes

        private Node titleBarNode;

        private Node frameNode;
        private Node backgroundNode;
        private Node borderNode;

        #endregion

        public Window(string name, Point position, Size size, WindowFlags Flags)
        {
            Form form = Form.current;
            GUIContext g = form.uiContext;
            WindowManager w = g.WindowManager;

            this.ID = name.GetHashCode();
            this.Name = name;
            this.IDStack.Push(this.ID);
            this.Flags = Flags;
            this.PosFloat = position;
            this.Position = new Point((int)PosFloat.X, (int)PosFloat.Y);
            this.Size = this.FullSize = size;
            this.DrawList = new DrawList();
            this.RenderTree = new RenderTree(this.ID, this.Position, this.ClientRect.Size);
            this.MoveID = GetID("#MOVE");
            this.Active = WasActive = false;

            // window styles
            {
                var style = new GUIStyle();
                style.Set(GUIStyleName.BorderTop, 1.0);
                style.Set(GUIStyleName.BorderRight, 1.0);
                style.Set(GUIStyleName.BorderBottom, 1.0);
                style.Set(GUIStyleName.BorderLeft, 1.0);
                style.Set(GUIStyleName.PaddingTop, 5.0);
                style.Set(GUIStyleName.PaddingRight, 10.0);
                style.Set(GUIStyleName.PaddingBottom, 5.0);
                style.Set(GUIStyleName.PaddingLeft, 10.0);
                style.Set(GUIStyleName.WindowBorderColor, Color.Rgb(170, 170, 170), GUIState.Normal);
                style.Set(GUIStyleName.WindowBorderColor, Color.Rgb(24, 131, 215), GUIState.Active);
                style.Set(GUIStyleName.WindowShadowColor, Color.Argb(100, 227, 227, 227));
                style.Set(GUIStyleName.WindowShadowWidth, 15.0);
                style.Set(GUIStyleName.BackgroundColor, Color.White);
                style.Set(GUIStyleName.ResizeGripSize, 20.0);
                style.Set(GUIStyleName.ResizeGripColor, Color.Argb(75, 102, 102, 102));
                style.Set(GUIStyleName.ResizeGripColor, Color.Argb(150, 102, 102, 102), GUIState.Hover);
                style.Set(GUIStyleName.ResizeGripColor, Color.Argb(225, 102, 102, 102), GUIState.Active);
                style.Set(GUIStyleName.WindowRounding, 3.0);
                style.Set(GUIStyleName.ScrollBarWidth, CurrentOS.IsDesktopPlatform ? 10.0 : 20.0);
                style.Set(GUIStyleName.ScrollBarBackgroundColor, Color.Rgb(240));
                style.Set(GUIStyleName.ScrollBarButtonColor, Color.Rgb(205), GUIState.Normal);
                style.Set(GUIStyleName.ScrollBarButtonColor, Color.Rgb(166), GUIState.Hover);
                style.Set(GUIStyleName.ScrollBarButtonColor, Color.Rgb(96), GUIState.Active);
                this.Style = style;
            }

            // window header styles
            {
                var style = new GUIStyle();
                style.Set(GUIStyleName.BackgroundColor, Color.White);
                style.Set(GUIStyleName.BackgroundColor, Color.White, GUIState.Active);
                style.Set(GUIStyleName.BackgroundColor, Color.White, GUIState.Disabled);
                style.Set<double>(GUIStyleName.BorderTopLeftRadius, 3.0);
                style.Set<double>(GUIStyleName.BorderTopRightRadius, 3.0);
                style.Set(GUIStyleName.PaddingTop, 8.0);
                style.Set(GUIStyleName.PaddingRight, 8.0);
                style.Set(GUIStyleName.PaddingBottom, 8.0);
                style.Set(GUIStyleName.PaddingLeft, 8.0);
                style.Set(GUIStyleName.FontColor, Color.Black, GUIState.Normal);
                style.Set(GUIStyleName.FontColor, Color.Rgb(153, 153, 153), GUIState.Active);
                style.FontFamily = GUIStyle.Default.FontFamily;
                style.FontSize = 12.0;
                this.TitleBarStyle = style;
            }

            var scrollBarWidth = this.Style.Get<double>(GUIStyleName.ScrollBarWidth);
            var clientSize = new Size(
                this.Size.Width - scrollBarWidth - this.Style.PaddingHorizontal - this.Style.BorderHorizontal,
                this.Size.Height - this.Style.PaddingVertical - this.Style.BorderVertical - this.TitleBarHeight);
            this.StackLayout = new StackLayout(this.ID, clientSize);

            //title bar node
            {
                Node node = new Node();
                node.Id = this.GetID(this.Name + "#" + this.ID + "#Titlebar");
                var primitive = new PathPrimitive();
                primitive.PathRect(this.TitleBarRect.Min, this.TitleBarRect.Max);
                var brush = new Brush();
                brush.FillColor = this.TitleBarStyle.Get<Color>(GUIStyleName.BackgroundColor);
                node.Primitive = primitive;
                node.Brush = brush;
                this.titleBarNode = node;
            }
            this.RenderTree.Root.Add(this.titleBarNode);

            //Window frame node
            if(!Collapsed)
            {
                //create nodes
                {
                    var node = new Node();
                    node.Id = this.GetID(this.Name + "#" + this.ID + "#WindowFrame");
                    this.frameNode = node;
                }
                //background
                {
                    var node = new Node();
                    node.Id = this.GetID(this.Name + "#" + this.ID + "#WindowBackground");
                    var primitive = new PathPrimitive();
                    primitive.PathRect(this.Position + new Vector(0, this.TitleBarHeight),
                        this.Rect.BottomRight);
                    var brush = new Brush();
                    brush.FillColor = this.Style.BackgroundColor;
                    node.Primitive = primitive;
                    node.Brush = brush;
                    this.backgroundNode = node;
                }
                //border
                {
                    var node = new Node();
                    node.Id = this.GetID(this.Name + "#" + this.ID + "WindowBorder");
                    var primitive = new PathPrimitive();
                    primitive.PathRect(this.Position + new Vector(0, this.TitleBarHeight), this.Rect.BottomRight);
                    var strokeStyle = new StrokeStyle();
                    node.IsFill = false;
                    node.Primitive = primitive;
                    node.StrokeStyle = strokeStyle;
                    this.borderNode = node;
                }
                //hierchary
                this.frameNode.Add(this.backgroundNode);
                this.frameNode.Add(this.borderNode);
                this.RenderTree.Root.Add(this.frameNode);
            }

        }

        public void FirstUpdate(string name, Point position, Size size, ref bool open, double backgroundAlpha,
            WindowFlags flags,
            long currentFrame, Window parentWindow)
        {
            //short names
            var form = Form.current;
            var g = form.uiContext;
            var w = g.WindowManager;

            Active = true;
            BeginCount = 0;
            ClipRect = Rect.Big;
            LastActiveFrame = currentFrame;

            var fullScreenRect = new Rect(0, 0, form.ClientSize);
            if (flags.HaveFlag(WindowFlags.ChildWindow) && !flags.HaveFlag(WindowFlags.ComboBox | WindowFlags.Popup))
            {
                //PushClipRect(parentWindow.ClipRect, true);
                //ClipRect = GetCurrentClipRect();
            }
            else
            {
                //PushClipRect(fullScreenRect, true);
                //ClipRect = GetCurrentClipRect();
            }

            // (draw outer clip rect for test only here)

            // Collapse window by double-clicking on title bar
            if (!flags.HaveFlag(WindowFlags.NoTitleBar) && !flags.HaveFlag(WindowFlags.NoCollapse))
            {
                if (w.HoveredWindow == this && g.IsMouseHoveringRect(TitleBarRect) &&
                    Mouse.Instance.LeftButtonDoubleClicked)
                {
                    Collapsed = !Collapsed;
                    w.FocusWindow(this);
                }
            }
            else
            {
                Collapsed = false;
            }

            #region size

            ApplySize(FullSize);
            Size = Collapsed ? TitleBarRect.Size : FullSize;

            #endregion

            #region position

            Position = new Point((int) PosFloat.X, (int) PosFloat.Y);
            if (flags.HaveFlag(WindowFlags.ChildWindow))
            {
                Position = PosFloat = position;
                Size = FullSize = size; // 'size' provided by user passed via BeginChild()->Begin().
            }

            #endregion

            // Draw window + handle manual resize
            var style = Style;
            var titleBarStyle = TitleBarStyle;
            var titleBarRect = TitleBarRect;
            var windowRounding = (float) style.Get<double>(GUIStyleName.WindowRounding);
            if (Collapsed)
            {
                var primitive = (PathPrimitive)this.titleBarNode.Primitive;
                primitive.PathClear();
                primitive.PathRect(this.TitleBarRect.Min, this.TitleBarRect.Max);
            }
            else
            {
                var resizeGripColor = Color.Clear;
                var resizeGripSize = Style.Get<double>(GUIStyleName.ResizeGripSize);
                var resizeCornerSize = Math.Max(resizeGripSize * 1.35, windowRounding + 1.0 + resizeGripSize * 0.2);
                if (!flags.HaveFlag(WindowFlags.AlwaysAutoResize) && !flags.HaveFlag(WindowFlags.NoResize))
                {
                    // Manual resize
                    var br = Rect.BottomRight;
                    var resizeRect = new Rect(br - new Vector(resizeCornerSize * 0.75f, resizeCornerSize * 0.75f),
                        br);
                    var resizeId = GetID("#RESIZE");
                    GUIBehavior.ButtonBehavior(resizeRect, resizeId, out var hovered, out var held,
                        ButtonFlags.FlattenChilds);
                    resizeGripColor =
                        held
                            ? style.Get<Color>(GUIStyleName.ResizeGripColor, GUIState.Active)
                            : hovered
                                ? style.Get<Color>(GUIStyleName.ResizeGripColor, GUIState.Hover)
                                : style.Get<Color>(GUIStyleName.ResizeGripColor);

                    if (hovered || held)
                    {
                        //Mouse.Instance.Cursor = Cursor.NeswResize;
                    }

                    if (held)
                    {
                        // We don't use an incremental MouseDelta but rather compute an absolute target size based on mouse position
                        var t = Mouse.Instance.Position - g.ActiveIdClickOffset - Position;
                        var newSizeWidth = t.X + resizeRect.Width;
                        var newSizeHeight = t.Y + resizeRect.Height;
                        newSizeWidth =
                            MathEx.Clamp(newSizeWidth, 330, fullScreenRect.Width); //min size of a window is 145x235
                        newSizeHeight = MathEx.Clamp(newSizeHeight, 150, fullScreenRect.Height);
                        var resizeSize = new Size(newSizeWidth, newSizeHeight);
                        ApplySize(resizeSize);

                        // adjust scroll parameters
                        var contentSize = ContentRect.Size;
                        if (contentSize != Size.Zero)
                        {
                            var vH = Rect.Height - TitleBarHeight - Style.BorderVertical -
                                     Style.PaddingVertical;
                            var cH = contentSize.Height;
                            if (cH > vH)
                            {
                                var oldScrollY = Scroll.Y;
                                oldScrollY = MathEx.Clamp(oldScrollY, 0, cH - vH);
                                Scroll.Y = oldScrollY;
                            }
                        }
                    }

                    Size = FullSize;
                    titleBarRect = TitleBarRect;
                }


                // Window background
                {
                    var bgColor = style.BackgroundColor;
                    if (backgroundAlpha >= 0.0f)
                    {
                        bgColor.A = backgroundAlpha;
                    }
                    if (bgColor.A > 0.0f)
                    {
                        this.backgroundNode.Brush.FillColor = bgColor;
                        var primitive = (PathPrimitive)this.backgroundNode.Primitive;
                        Debug.Assert(primitive != null);
                        primitive.PathClear();
                        primitive.PathRect(this.Position + new Vector(0, this.TitleBarHeight),
                            this.Rect.BottomRight);
                    }
                }

                // Title bar
                if (!flags.HaveFlag(WindowFlags.NoTitleBar))
                {
                    var brush = this.titleBarNode.Brush;
                    brush.FillColor = w.FocusedWindow == this
                        ? titleBarStyle.Get<Color>(GUIStyleName.BackgroundColor, GUIState.Active)
                        : titleBarStyle.Get<Color>(GUIStyleName.BackgroundColor);

                    var primitive = (PathPrimitive)this.titleBarNode.Primitive;
                    Debug.Assert(primitive != null);
                    primitive.PathRect(titleBarRect.TopLeft, titleBarRect.BottomRight, windowRounding, 1 | 2);
                }

                // Render resize grip
                // (after the input handling so we don't have a frame of latency)
                if (!flags.HaveFlag(WindowFlags.NoResize))
                {
                    var br = Rect.BottomRight;
                    var borderBottom = Style.BorderBottom;
                    var borderRight = Style.BorderRight;
                    //DrawList.PathLineTo(br + new Vector(-resizeCornerSize, -borderBottom));
                    //DrawList.PathLineTo(br + new Vector(-borderRight, -resizeCornerSize));
                    //DrawList.PathArcToFast(
                    //    new Point(br.X - windowRounding - borderRight, br.Y - windowRounding - borderBottom),
                    //    windowRounding,
                    //    0, 3);
                    //DrawList.PathFill(resizeGripColor);
                }

                // Scroll bar
                if (flags.HaveFlag(WindowFlags.VerticalScrollbar))
                {
                    //get content size without clip
                    var contentPosition = ContentRect.TopLeft;
                    var contentSize = ContentRect.Size;
                    if (contentSize != Size.Zero)
                    {
                        var id = GetID("#SCROLLY");

                        var scrollBarWidth = Style.Get<double>(GUIStyleName.ScrollBarWidth);
                        var scrollTopLeft = new Point(
                            Rect.Right - scrollBarWidth - Style.BorderRight - Style.PaddingRight,
                            Rect.Top + TitleBarHeight + Style.BorderTop + Style.PaddingTop);
                        var sH = Rect.Height - TitleBarHeight - Style.BorderVertical -
                                 Style.PaddingVertical
                                 + (flags.HaveFlag(WindowFlags.NoResize) ? 0 : -resizeCornerSize);
                        var vH = Rect.Height - TitleBarHeight - Style.BorderVertical -
                                 Style.PaddingVertical;
                        var scrollBottomRight = scrollTopLeft + new Vector(scrollBarWidth, sH);
                        var bgRect = new Rect(scrollTopLeft, scrollBottomRight);

                        var cH = contentSize.Height;
                        var top = Scroll.Y * sH / cH;
                        var height = sH * vH / cH;

                        if (height < sH)
                        {
                            // handle mouse click/drag
                            var held = false;
                            var hovered = false;
                            var previouslyHeld = g.ActiveId == id;
                            GUIBehavior.ButtonBehavior(bgRect, id, out hovered, out held);
                            if (held)
                            {
                                top = Mouse.Instance.Position.Y - bgRect.Y - 0.5 * height;
                                top = MathEx.Clamp(top, 0, sH - height);
                                var targetScrollY = top * cH / sH;
                                SetWindowScrollY(targetScrollY);
                            }

                            var scrollButtonTopLeft = scrollTopLeft + new Vector(0, top);
                            var scrllButtonBottomRight = scrollButtonTopLeft + new Vector(scrollBarWidth, height);
                            var buttonRect = new Rect(scrollButtonTopLeft, scrllButtonBottomRight);

                            //Draw vertical scroll bar and button
                            {
                                var bgColor = Style.Get<Color>(GUIStyleName.ScrollBarBackgroundColor);
                                var buttonColor = Style.Get<Color>(GUIStyleName.ScrollBarButtonColor,
                                    held ? GUIState.Active : hovered ? GUIState.Hover : GUIState.Normal);
                                //DrawList.AddRectFilled(bgRect.TopLeft, buttonRect.TopRight, bgColor);
                                //DrawList.AddRectFilled(buttonRect.TopLeft, buttonRect.BottomRight, buttonColor);
                                //DrawList.AddRectFilled(buttonRect.BottomLeft, bgRect.BottomRight, bgColor);
                            }
                        }
                        else
                        {
                            var bgColor = Style.Get<Color>(GUIStyleName.ScrollBarBackgroundColor);
                            //DrawList.AddRectFilled(bgRect.TopLeft, bgRect.BottomRight, bgColor);
                        }
                    }
                }

                ContentRect = Rect.Zero;
            }

            // draw title bar text
            if (!flags.HaveFlag(WindowFlags.NoTitleBar))
            {
                // title text
                var state = w.FocusedWindow == this ? GUIState.Active : GUIState.Normal;
                //DrawList.DrawBoxModel(titleBarRect, name, titleBarStyle, state);

                // close button
                if (CloseButton(GetID("#CLOSE"),
                    new Rect(titleBarRect.TopRight + new Vector(-45, 0), titleBarRect.BottomRight))) open = false;
            }

            // Borders
            if (flags.HaveFlag(WindowFlags.ShowBorders))
            {
                var state = w.FocusedWindow == this ? GUIState.Active : GUIState.Normal;
                // window border
                var borderColor = Style.Get<Color>(GUIStyleName.WindowBorderColor, state);
                //DrawList.AddRect(Position, Position + new Vector(Size.Width, Size.Height),
                //    borderColor, windowRounding);
            }

            // Save clipped aabb so we can access it in constant-time in FindHoveredWindow()
            WindowClippedRect = Rect;
            WindowClippedRect.Intersect(ClipRect);
        }

        /// <summary>
        /// Gets the rect of this window
        /// </summary>
        public Rect Rect => new Rect(Position, Size);

        /// <summary>
        /// Gets the height of the title bar
        /// </summary>
        public double TitleBarHeight
        {
            get
            {
                if(this.Flags.HaveFlag(WindowFlags.NoTitleBar))
                {
                    return 0;
                }

                return TitleBarStyle.PaddingVertical + 30;
            }
        }

        /// <summary>
        /// Gets the rect of the title bar
        /// </summary>
        public Rect TitleBarRect => new Rect(Position, Size.Width, TitleBarHeight);

        /// <summary>
        /// Gets or sets the rect of the client area
        /// </summary>
        public Rect ClientRect { get; set; }

        /// <summary>
        /// Gets or sets if the window is collapsed.
        /// </summary>
        public bool Collapsed { get; set; } = true;//FIXME TEMP collapsed

        /// <summary>
        /// Gets or sets if the window is active
        /// </summary>
        public bool Active
        {
            get;
            internal set;
        }

        /// <summary>
        /// Gets or sets the content rect
        /// </summary>
        public Rect ContentRect
        {
            get;
            set;
        } = Rect.Zero;

        /// <summary>
        /// Gets or sets the root window
        /// </summary>
        public Window RootWindow { get; set; }

        /// <summary>
        /// Gets or sets move ID, equals to <code>window.GetID("#MOVE")</code>.
        /// </summary>
        public int MoveID { get; internal set; }

        public Rect WindowClippedRect { get; internal set; }

        /// <summary>
        /// Gets or sets whether the window was active in last frame.
        /// </summary>
        public bool WasActive { get; internal set; }

        /// <summary>
        /// Gets or sets whether the window does nothing.
        /// </summary>
        public bool SkipItems { get; internal set; } = false;

        /// <summary>
        /// Gets or sets how many times <code>Begin()</code> was called in this frame.
        /// </summary>
        public int BeginCount { get; internal set; }

        /// <summary>
        /// Gets or sets whether the window is used in this frame
        /// </summary>
        public bool Accessed { get; internal set; }

        #region ID
        private int Hash(int seed, int int_id)
        {
            int hash = seed + 17;
            hash = hash * 23 + this.ID.GetHashCode();
            var result = hash * 23 + int_id;
            return result;
        }

        public int GetID(int int_id)
        {
            int seed = IDStack.Peek();
            var id = Hash(seed, int_id);

            GUIContext g = Form.current.uiContext;
            g.KeepAliveID(id);
            return id;
        }

        public int GetID(string str_id)
        {
            int seed = IDStack.Peek();
            int int_id = str_id.GetHashCode();
            var id = Hash(seed, int_id);

            GUIContext g = Form.current.uiContext;
            g.KeepAliveID(id);

            return id;
        }

        public int GetID(ITexture texture)
        {
            int seed = IDStack.Peek();
            int int_id = texture.GetHashCode();
            var id = Hash(seed, int_id);

            GUIContext g = Form.current.uiContext;
            g.KeepAliveID(id);
            return id;
        }
        #endregion

        /// <summary>
        /// Apply new size to window
        /// </summary>
        /// <param name="new_size"></param>
        public void ApplySize(Size new_size)
        {
            if (this.FullSize != new_size)
            {
                {
                    var topLeft = this.Position + new Vector(this.Style.PaddingLeft + this.Style.BorderLeft, this.Style.PaddingTop + this.Style.BorderTop);
                    var bottomRight = this.Rect.BottomRight
                        - new Vector(this.Style.PaddingRight + this.Style.BorderRight, this.Style.PaddingBottom + this.Style.BorderBottom)
                        - new Vector(this.Style.Get<double>(GUIStyleName.ScrollBarWidth), 0);
                    this.ClientRect = new Rect(topLeft, bottomRight);
                }
                this.StackLayout.SetRootSize(this.ClientRect.Size);
            }
            this.FullSize = new_size;
        }

        /// <summary>
        /// Get the rect for an automatic-layout control
        /// </summary>
        /// <param name="id">id of the control</param>
        /// <param name="size">size of content, border and padding NOT included</param>
        /// <param name="style">style that will apply to requested rect</param>
        /// <returns></returns>
        public Rect GetRect(int id, Size size, LayoutOptions? options = null, string str_id = null, bool isGroup = false)
        {
            //var rect = StackLayout.GetRect(id, size, options, str_id);

            var node = this.RenderTree.GetNodeById(id);
            if(node == null)
            {
                node = new Node();
                node.Id = id;
                node.StrId = str_id;
                if (isGroup)
                {
                    node.AttachLayoutGroup(true, options);
                }
                else
                {
                    node.AttachLayoutEntry(size, options);
                }
                this.RenderTree.CurrentContainer.Add(node);
            }

            var rect = node.Rect;

            if(rect == StackLayout.DummyRect)
            {
                Rect newContentRect = ContentRect;
                newContentRect.Union(rect);
                ContentRect = newContentRect;

                // Apply window position, style(border and padding) and titlebar
                rect.Offset(this.Position.X + this.Style.BorderLeft + this.Style.PaddingLeft, this.Position.Y + this.TitleBarHeight + this.Style.BorderTop + this.Style.PaddingTop);
                rect.Offset(-this.Scroll);
            }

            return rect;
        }

        /// <summary>
        /// Get the rect of a manual-positioned control
        /// </summary>
        public Rect GetRect(Rect rect)
        {
            Rect newContentRect = ContentRect;
            newContentRect.Union(rect);
            ContentRect = newContentRect;

            rect.Offset(this.Position.X, this.Position.Y + this.TitleBarHeight);
            rect.Offset(-this.Scroll);
            return rect;
        }

        /// <summary>
        /// Sets scroll-y paramter
        /// </summary>
        /// <param name="newScrollY">new value</param>
        public void SetWindowScrollY(double newScrollY)
        {
            this.Scroll.Y = newScrollY;
        }

        bool CloseButton(int id, Rect rect)
        {
            Window window = GUI.GetCurrentWindow();

            bool pressed = GUIBehavior.ButtonBehavior(rect, id, out bool hovered, out bool held);

            GUIStyle style = GUIStyle.Basic;
            style.Save();
            style.ApplySkin(GUIControlName.Button);
            style.PushBgColor(Color.White, GUIState.Normal);
            style.PushBgColor(Color.Rgb(232, 17, 35), GUIState.Hover);
            style.PushBgColor(Color.Rgb(241, 112, 122), GUIState.Active);

            // Render
            var d = window.DrawList;
            var state = (hovered && held) ? GUIState.Active : hovered ? GUIState.Hover : GUIState.Normal;
            var color = style.Get<Color>(GUIStyleName.BackgroundColor, state);
            //d.AddRectFilled(rect, color);

            Point center = rect.Center;
            //float cross_extent = (15 * 0.7071f) - 1.0f;
            var fontColor = style.Get<Color>(GUIStyleName.FontColor, state);
            //d.AddLine(center + new Vector(+cross_extent, +cross_extent), center + new Vector(-cross_extent, -cross_extent), fontColor);
            //d.AddLine(center + new Vector(+cross_extent, -cross_extent), center + new Vector(-cross_extent, +cross_extent), fontColor);

            style.Restore();

            return pressed;
        }
    }
}
