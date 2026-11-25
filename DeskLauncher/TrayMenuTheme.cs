using System.Drawing;
using System.Windows.Forms;

namespace DeskLauncher;

/// <summary>Dark Fluent-like theme for the WinForms tray context menu (and its submenus).</summary>
public static class TrayMenuTheme
{
    static Color Bg = Color.FromArgb(0x2B, 0x2B, 0x2B);
    static Color Border = Color.FromArgb(0x45, 0x45, 0x45);
    static Color Hover = Color.FromArgb(0x3D, 0x3D, 0x3D);
    static Color Text = Color.White;
    static Color Separator = Color.FromArgb(0x45, 0x45, 0x45);

    static ContextMenuStrip? _menu;

    /// <summary>Switches the palette (dark/light) and restyles the menu already applied.</summary>
    public static void SetDark(bool dark)
    {
        if (dark)
        {
            Bg = Color.FromArgb(0x2B, 0x2B, 0x2B); Border = Color.FromArgb(0x45, 0x45, 0x45);
            Hover = Color.FromArgb(0x3D, 0x3D, 0x3D); Text = Color.White;
            Separator = Color.FromArgb(0x45, 0x45, 0x45);
        }
        else
        {
            Bg = Color.FromArgb(0xF9, 0xF9, 0xF9); Border = Color.FromArgb(0xD0, 0xD0, 0xD0);
            Hover = Color.FromArgb(0xE5, 0xE5, 0xE5); Text = Color.FromArgb(0x1A, 0x1A, 0x1A);
            Separator = Color.FromArgb(0xD8, 0xD8, 0xD8);
        }
        if (_menu == null) return;
        _menu.BackColor = Bg;
        _menu.ForeColor = Text;
        foreach (ToolStripItem i in _menu.Items) StyleItem(i);
        _menu.Invalidate();
    }

    public static void Apply(ContextMenuStrip menu)
    {
        _menu = menu;
        menu.Renderer = new DarkRenderer();
        menu.BackColor = Bg;
        menu.ForeColor = Text;
        menu.Font = new Font("Segoe UI Variable Text", 9.5f);
        menu.ShowImageMargin = false;
        menu.Padding = new Padding(2, 4, 2, 4);
        menu.ItemAdded += (_, e) => StyleItem(e.Item);
        foreach (ToolStripItem i in menu.Items) StyleItem(i);
    }

    static void StyleItem(ToolStripItem? item)
    {
        if (item == null) return;
        item.ForeColor = Text;
        item.BackColor = Bg;
        item.Padding = new Padding(4, 5, 4, 5);
        if (item is ToolStripMenuItem mi)
        {
            if (mi.DropDown is ToolStripDropDownMenu dd)
            {
                dd.BackColor = Bg;
                dd.ForeColor = Text;
                dd.ShowImageMargin = false;
                dd.Font = new Font("Segoe UI Variable Text", 9.5f);
            }
            mi.DropDown.ItemAdded += (_, e) => StyleItem(e.Item); // profiles are re-added on each open
            foreach (ToolStripItem child in mi.DropDownItems) StyleItem(child);
        }
    }

    sealed class DarkRenderer : ToolStripProfessionalRenderer
    {
        public DarkRenderer() : base(new DarkColors()) { RoundedEdges = false; }

        protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
        {
            e.TextColor = e.Item.Enabled ? Text : Color.FromArgb(0x8E, 0x8E, 0x8E);
            base.OnRenderItemText(e);
        }

        protected override void OnRenderArrow(ToolStripArrowRenderEventArgs e)
        {
            e.ArrowColor = Text;
            base.OnRenderArrow(e);
        }

        protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
        {
            var r = new Rectangle(4, 1, e.Item.Width - 8, e.Item.Height - 2);
            using var b = new SolidBrush(e.Item.Selected && e.Item.Enabled ? Hover : Bg);
            e.Graphics.FillRectangle(b, e.Item.Selected ? r : new Rectangle(Point.Empty, e.Item.Size));
        }

        protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs e)
        {
            int y = e.Item.Height / 2;
            using var p = new Pen(Separator);
            e.Graphics.DrawLine(p, 8, y, e.Item.Width - 8, y);
        }

        protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs e)
        {
            using var b = new SolidBrush(Bg);
            e.Graphics.FillRectangle(b, e.AffectedBounds);
        }

        protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e)
        {
            using var p = new Pen(Border);
            var r = new Rectangle(0, 0, e.ToolStrip.Width - 1, e.ToolStrip.Height - 1);
            e.Graphics.DrawRectangle(p, r);
        }

        protected override void OnRenderImageMargin(ToolStripRenderEventArgs e) { /* no margin column */ }
    }

    sealed class DarkColors : ProfessionalColorTable
    {
        public override Color ToolStripDropDownBackground => Bg;
        public override Color MenuItemSelected => Hover;
        public override Color MenuItemBorder => Hover;
        public override Color MenuBorder => Border;
        public override Color ImageMarginGradientBegin => Bg;
        public override Color ImageMarginGradientMiddle => Bg;
        public override Color ImageMarginGradientEnd => Bg;
        public override Color SeparatorDark => Separator;
        public override Color SeparatorLight => Separator;
    }
}
