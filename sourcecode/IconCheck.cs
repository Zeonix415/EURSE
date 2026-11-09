using System;
using System.Drawing;
using System.Runtime.InteropServices;
class IconCheck {
    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    static extern uint ExtractIconEx(string file, int index, IntPtr[] large, IntPtr[] small, uint count);
    [DllImport("user32.dll")]
    static extern bool DestroyIcon(IntPtr icon);
    static int Main(string[] args) {
        var handles = new IntPtr[1];
        if (ExtractIconEx(System.IO.Path.GetFullPath(args[0]), 0, handles, null, 1) != 1 || handles[0] == IntPtr.Zero)
            throw new Exception("Windows could not extract an icon");
        try {
            using (var native = Icon.FromHandle(handles[0]))
            using (var expected = new Icon(args[1], native.Size))
            using (var actualBitmap = native.ToBitmap())
            using (var expectedBitmap = expected.ToBitmap()) {
                for (int y = 0; y < actualBitmap.Height; y++)
                    for (int x = 0; x < actualBitmap.Width; x++)
                        if (actualBitmap.GetPixel(x, y).ToArgb() != expectedBitmap.GetPixel(x, y).ToArgb())
                            throw new Exception("Extracted icon differs from supplied image");
                Console.WriteLine("Windows extracted the expected {0}x{1} icon.", actualBitmap.Width, actualBitmap.Height);
            }
        } finally { DestroyIcon(handles[0]); }
        return 0;
    }
}
