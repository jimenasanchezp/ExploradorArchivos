using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace ExploradorArchivos.AppCamara;

/// <summary>
/// Escritor de video AVI usando la API nativa Video for Windows (avifil32.dll).
/// No requiere ningún paquete NuGet adicional.
/// </summary>
internal sealed class AviGrabador : IDisposable
{
    // ── P/Invoke — avifil32.dll ──────────────────────────────────────────────
    [DllImport("avifil32.dll")] private static extern void AVIFileInit();
    [DllImport("avifil32.dll")] private static extern int  AVIFileOpen(out IntPtr ppfile, string szFile, int uMode, int pclsidHandler);
    [DllImport("avifil32.dll")] private static extern int  AVIFileCreateStream(IntPtr pfile, out IntPtr ppavi, ref AVISTREAMINFO psi);
    [DllImport("avifil32.dll")] private static extern int  AVIMakeCompressedStream(out IntPtr ppsCompressed, IntPtr ppsUncompressed, ref AVICOMPRESSOPTIONS opts, IntPtr pclsid);
    [DllImport("avifil32.dll")] private static extern int  AVIStreamSetFormat(IntPtr pavi, int lPos, ref BITMAPINFOHEADER lpFormat, int cbFormat);
    [DllImport("avifil32.dll")] private static extern int  AVIStreamWrite(IntPtr pavi, int lStart, int lSamples, IntPtr lpBuffer, int cbBuffer, int dwFlags, IntPtr plSampWritten, IntPtr plBytesWritten);
    [DllImport("avifil32.dll")] private static extern int  AVIStreamRelease(IntPtr pavi);
    [DllImport("avifil32.dll")] private static extern int  AVIFileRelease(IntPtr pfile);
    [DllImport("avifil32.dll")] private static extern void AVIFileExit();

    // ── Estructuras VFW ──────────────────────────────────────────────────────
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    private struct AVISTREAMINFO
    {
        public int    fccType;
        public int    fccHandler;
        public int    dwFlags;
        public int    dwCaps;
        public short  wPriority;
        public short  wLanguage;
        public int    dwScale;
        public int    dwRate;
        public int    dwStart;
        public int    dwLength;
        public int    dwInitialFrames;
        public int    dwSuggestedBufferSize;
        public int    dwQuality;
        public int    dwSampleSize;
        public int    rcFrameLeft;
        public int    rcFrameTop;
        public int    rcFrameRight;
        public int    rcFrameBottom;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
        public string szName;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    private struct AVICOMPRESSOPTIONS
    {
        public int  fccType;
        public int  fccHandler;
        public int  dwKeyFrameEvery;
        public int  dwQuality;
        public int  dwBytesPerSecond;
        public int  dwFlags;
        public IntPtr lpFormat;
        public int  cbFormat;
        public IntPtr lpParms;
        public int  cbParms;
        public int  dwInterleaveEvery;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    private struct BITMAPINFOHEADER
    {
        public int    biSize;
        public int    biWidth;
        public int    biHeight;
        public short  biPlanes;
        public short  biBitCount;
        public int    biCompression;
        public int    biSizeImage;
        public int    biXPelsPerMeter;
        public int    biYPelsPerMeter;
        public int    biClrUsed;
        public int    biClrImportant;
    }

    // ── Constantes VFW ───────────────────────────────────────────────────────
    private const int OF_WRITE        = 0x00000001;
    private const int OF_CREATE       = 0x00001000;
    private const int AVIIF_KEYFRAME  = 0x00000010;
    private static readonly int streamtypeVIDEO = FourCC("vids");

    private static int FourCC(string s) =>
        s[0] | (s[1] << 8) | (s[2] << 16) | (s[3] << 24);

    // ── Estado interno ───────────────────────────────────────────────────────
    private IntPtr _pFile       = IntPtr.Zero;
    private IntPtr _pStream     = IntPtr.Zero;
    private IntPtr _pCompressed = IntPtr.Zero;
    private int    _frameCount  = 0;
    private int    _width;
    private int    _height;
    private bool   _abierto     = false;
    private bool   _disposed    = false;

    // ════════════════════════════════════════════════════════════════════════
    /// <summary>Abre el archivo AVI y configura el stream de video sin compresión (DIB).</summary>
    public void Abrir(string rutaArchivo, int ancho, int alto, int fps)
    {
        if (_abierto) throw new InvalidOperationException("El grabador ya está abierto.");

        _width  = ancho;
        _height = alto;

        AVIFileInit();

        // Abrir/crear archivo AVI
        int hr = AVIFileOpen(out _pFile, rutaArchivo, OF_WRITE | OF_CREATE, 0);
        if (hr != 0) throw new Exception($"AVIFileOpen falló: 0x{hr:X8}");

        // Configurar stream de video
        var info = new AVISTREAMINFO
        {
            fccType               = streamtypeVIDEO,
            fccHandler            = FourCC("DIB "),   // sin compresión
            dwScale               = 1,
            dwRate                = fps,
            dwSuggestedBufferSize = ancho * alto * 3,
            dwQuality             = -1,
            rcFrameRight          = ancho,
            rcFrameBottom         = alto,
            szName                = "Video"
        };

        hr = AVIFileCreateStream(_pFile, out _pStream, ref info);
        if (hr != 0) throw new Exception($"AVIFileCreateStream falló: 0x{hr:X8}");

        // Usar stream sin compresión (sin diálogo de codec)
        var opts = new AVICOMPRESSOPTIONS
        {
            fccHandler = FourCC("DIB "),
            dwQuality  = 10000
        };

        hr = AVIMakeCompressedStream(out _pCompressed, _pStream, ref opts, IntPtr.Zero);
        if (hr != 0) throw new Exception($"AVIMakeCompressedStream falló: 0x{hr:X8}");

        // Establecer formato de frame (BMP 24bpp)
        var bih = new BITMAPINFOHEADER
        {
            biSize        = Marshal.SizeOf<BITMAPINFOHEADER>(),
            biWidth       = ancho,
            biHeight      = alto,
            biPlanes      = 1,
            biBitCount    = 24,
            biCompression = 0,  // BI_RGB
            biSizeImage   = ancho * alto * 3
        };

        hr = AVIStreamSetFormat(_pCompressed, 0, ref bih, Marshal.SizeOf<BITMAPINFOHEADER>());
        if (hr != 0) throw new Exception($"AVIStreamSetFormat falló: 0x{hr:X8}");

        _frameCount = 0;
        _abierto    = true;
    }

    // ════════════════════════════════════════════════════════════════════════
    /// <summary>Escribe un frame de imagen al archivo AVI.</summary>
    public void EscribirFrame(Bitmap bmp)
    {
        if (!_abierto) return;

        // Redimensionar si es necesario
        Bitmap fuente = bmp;
        bool   creado = false;

        if (bmp.Width != _width || bmp.Height != _height)
        {
            fuente = new Bitmap(bmp, new Size(_width, _height));
            creado = true;
        }

        // Convertir a formato BMP raw (24bpp, invertido verticalmente para VFW)
        Bitmap bmp24 = new Bitmap(_width, _height, PixelFormat.Format24bppRgb);
        using (Graphics g = Graphics.FromImage(bmp24))
        {
            g.DrawImage(fuente, 0, 0, _width, _height);
        }

        // Bloquear bits y escribir al stream AVI
        BitmapData bmpData = bmp24.LockBits(
            new Rectangle(0, 0, _width, _height),
            ImageLockMode.ReadOnly,
            PixelFormat.Format24bppRgb);

        try
        {
            int stride    = Math.Abs(bmpData.Stride);
            int bufferLen = stride * _height;

            // VFW espera el BMP con filas invertidas (bottom-up)
            byte[] invertido = new byte[bufferLen];
            for (int y = 0; y < _height; y++)
            {
                int srcOff = (_height - 1 - y) * stride;
                Marshal.Copy(bmpData.Scan0 + y * stride, invertido, srcOff, stride);
            }

            GCHandle handle = GCHandle.Alloc(invertido, GCHandleType.Pinned);
            try
            {
                AVIStreamWrite(_pCompressed, _frameCount, 1,
                    handle.AddrOfPinnedObject(), bufferLen,
                    AVIIF_KEYFRAME, IntPtr.Zero, IntPtr.Zero);
            }
            finally
            {
                handle.Free();
            }
        }
        finally
        {
            bmp24.UnlockBits(bmpData);
        }

        bmp24.Dispose();
        if (creado) fuente.Dispose();

        _frameCount++;
    }

    // ════════════════════════════════════════════════════════════════════════
    /// <summary>Cierra y libera todos los recursos VFW.</summary>
    public void Cerrar()
    {
        if (!_abierto) return;
        _abierto = false;

        if (_pCompressed != IntPtr.Zero) { AVIStreamRelease(_pCompressed); _pCompressed = IntPtr.Zero; }
        if (_pStream     != IntPtr.Zero) { AVIStreamRelease(_pStream);     _pStream     = IntPtr.Zero; }
        if (_pFile       != IntPtr.Zero) { AVIFileRelease(_pFile);         _pFile       = IntPtr.Zero; }

        AVIFileExit();
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            Cerrar();
            _disposed = true;
        }
    }
}
