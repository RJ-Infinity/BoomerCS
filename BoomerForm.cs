using System.Drawing.Imaging;
using System.Drawing;
using static System.Windows.Forms.LinkLabel;

namespace BoomerCS
{
    public partial class BoomerForm : Form
    {
        Bitmap Screen;
        Bitmap BaseScreen;
        float Zoom=1;
        PointF MouseLocation=new();
        PointF ZoomLocation=new();
        int HoodRadius = 50;
        bool Hood = false;
        bool FollowMouse = false;
        public BoomerForm()
        {
            InitializeComponent();
        }
        protected override void OnLoad(EventArgs e)
        {
            Left = SystemInformation.VirtualScreen.Left;
            Top = SystemInformation.VirtualScreen.Top;
            Width = SystemInformation.VirtualScreen.Width;
            Height = SystemInformation.VirtualScreen.Height;

            BaseScreen = new Bitmap(Width, Height);
            using (Graphics g = Graphics.FromImage(BaseScreen))
            { g.CopyFromScreen(Left, Top, 0, 0, BaseScreen.Size); }
            base.OnLoad(e);

            Focus();
            TopMost = true;
            Activate();
        }

        private float? oldZoom;
        private PointF? oldZoomLocation;
        private PointF? oldMouseLocation;
        private int? oldHoodRadius;
        private bool? oldFollowMouse;
        private bool? oldHood;
        private void ApplyTransformations()
        {
            if (
                oldZoom == Zoom &&
                oldZoomLocation == ZoomLocation &&
                oldFollowMouse == FollowMouse &&
                oldHood == Hood &&
                (!(Hood || FollowMouse) || oldMouseLocation == MouseLocation) &&
                (!Hood || oldHoodRadius == HoodRadius)
            ){ return; }
            oldZoom = Zoom;
            oldZoomLocation = MouseLocation;
            oldHoodRadius = HoodRadius;
            oldFollowMouse = FollowMouse;
            oldHood = Hood;
            Screen = new Bitmap(Width,Height);
            using (Graphics g = Graphics.FromImage(Screen))
            {

                g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
                g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.Half;
                g.DrawImage(
                    BaseScreen,
                    FollowMouse?
                        (Width - (Width * Zoom))*(MouseLocation.X/Width):
                        ZoomLocation.X,
                    FollowMouse?
                        (Height - (Height * Zoom)) * (MouseLocation.Y / Height):
                        ZoomLocation.Y,
                    (int)(Width * Zoom),
                    (int)(Height * Zoom)
                );
            }
            if (Hood) { Screen = MaskImages(Screen, CreateHood()); }
        }
        protected override void OnMouseMove(MouseEventArgs e)
        {
            if (panning)
            {
                ZoomLocation.X += e.X - MouseLocation.X;
                ZoomLocation.Y += e.Y - MouseLocation.Y;
            }
            MouseLocation = e.Location;
            Invalidate();
            base.OnMouseMove(e);
        }
        bool CtrlKeyDown = false;
        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.ControlKey) { CtrlKeyDown = true; }
            if (e.KeyCode == Keys.Escape || e.KeyCode == Keys.Q) { Close(); }
            if (e.KeyCode == Keys.F) {
                Hood = !Hood;
                Invalidate();
            }
            if (e.KeyCode == Keys.M) {
                FollowMouse = !FollowMouse;
                Invalidate();
            }
            if (e.KeyCode == Keys.D0)
            {
                Zoom = 1;
                MouseLocation = new();
                ZoomLocation = new();
                HoodRadius = 50;
                Hood = false;
                FollowMouse = false;
                Invalidate();
            }
            base.OnKeyDown(e);
        }
        protected override void OnKeyUp(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.ControlKey) { CtrlKeyDown = false; }
            base.OnKeyDown(e);
        }
        protected override bool IsInputKey(Keys keyData)
        => keyData == Keys.ControlKey ? true : base.IsInputKey(keyData);
        protected override void OnMouseWheel(MouseEventArgs e)
        {
            PointF oldLoc=new();
            if (!FollowMouse)
            { oldLoc = new((e.X - ZoomLocation.X) / Zoom, (e.Y - ZoomLocation.Y) / Zoom); }
            if (CtrlKeyDown)
            { HoodRadius -= 10 * e.Delta / SystemInformation.MouseWheelScrollDelta; }
            else
            {
                for (int i = 0; i < Math.Abs(e.Delta) /SystemInformation.MouseWheelScrollDelta; i++)
                { Zoom *= e.Delta>0?1.1f:0.9f; } // repeat for each mouse wheel click
            }
            if (HoodRadius < 0) { HoodRadius = 0; }
            if (FollowMouse)
            { MouseLocation = e.Location; }
            else
            {
                ZoomLocation.X = -oldLoc.X * Zoom + e.X;
                ZoomLocation.Y = -oldLoc.Y * Zoom + e.Y;
            }

            Invalidate();
            base.OnMouseWheel(e);
        }
        bool panning = false;
        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            panning = !FollowMouse;
        }
        protected override void OnMouseUp(MouseEventArgs e)
        {
            panning = false;
            base.OnMouseUp(e);
        }
        protected override void OnLostFocus(EventArgs e)
        {
            Close();
            base.OnLostFocus(e);
        }

        private Bitmap MaskImages(Bitmap bmp, Bitmap mask)
        {
            if (mask.Size != bmp.Size) { throw new ArgumentException("the two images must be the same size"); }
            // Lock the bitmap's bits.
            Rectangle rect = new Rectangle(0, 0, bmp.Width, bmp.Height);
            BitmapData bmpData = bmp.LockBits(rect, ImageLockMode.ReadWrite, bmp.PixelFormat);
            BitmapData maskData = mask.LockBits(rect, ImageLockMode.ReadWrite, mask.PixelFormat);

            int bmpBytes = Math.Abs(bmpData.Stride) * bmp.Height;
            byte[] bmpValues = new byte[bmpBytes];
            System.Runtime.InteropServices.Marshal.Copy(bmpData.Scan0, bmpValues, 0, bmpBytes);

            int maskBytes = Math.Abs(maskData.Stride) * mask.Height;
            byte[] maskValues = new byte[maskBytes];
            System.Runtime.InteropServices.Marshal.Copy(maskData.Scan0, maskValues, 0, maskBytes);

            if (bmpValues.Length != maskValues.Length) { throw new Exception("????"); }

            // Set every third value to 255. A 24bpp bitmap will look red.
            for (int counter = 0; counter < bmpValues.Length; counter++)
            { bmpValues[counter] = (byte)(bmpValues[counter]* maskValues[counter]/255); }

            // Copy the RGB values back to the bitmap
            System.Runtime.InteropServices.Marshal.Copy(bmpValues, 0, bmpData.Scan0, bmpBytes);

            // Unlock the bits.
            bmp.UnlockBits(bmpData);

            return bmp;
        }
        private Bitmap CreateHood()
        {
            Bitmap bmp = new Bitmap(Width, Height);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.Clear(Color.FromArgb(0xff,0x12,0x12,0x12));
                g.FillEllipse(new SolidBrush(Color.White), new Rectangle(
                    (int)MouseLocation.X - HoodRadius,
                    (int)MouseLocation.Y - HoodRadius,
                    HoodRadius*2,
                    HoodRadius*2
                ));
            }
            return bmp;
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            ApplyTransformations();
            e.Graphics.Clear(Color.Black);
            e.Graphics.DrawImage(Screen, 0, 0);
            base.OnPaint(e);
        }
    }
}