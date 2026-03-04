using System.Reflection;
using System.Runtime.InteropServices;
using DTScan.Models;
using NTwain;
using NTwain.Data;

namespace DTScan.Services;

public sealed class ScannerService
{
    // ═══════════════════════════════════════════════
    //  WIA CONSTANTS
    // ═══════════════════════════════════════════════

    private const string WIA_FORMAT_BMP = "{B96B3CAB-0728-11D3-9D7B-0000F81EF32E}";

    private const int WIA_DPS_DOCUMENT_HANDLING_SELECT = 3088;
    private const int WIA_DPS_DOCUMENT_HANDLING_STATUS = 3087;
    private const int WIA_IPS_XRES = 6147;
    private const int WIA_IPS_YRES = 6148;
    private const int WIA_IPS_CUR_INTENT = 6146;
    private const int WIA_IPS_PAGES = 3096;

    private const int WIA_FEEDER = 1;
    private const int WIA_DUPLEX = 4;
    private const int WIA_FEED_READY = 1;

    private const int WIA_ERROR_PAPER_EMPTY = unchecked((int)0x80210003);
    private const int WIA_ERROR_OFFLINE = unchecked((int)0x80210005);

    // ═══════════════════════════════════════════════
    //  PUBLIC API
    // ═══════════════════════════════════════════════

    public List<string> GetAvailableScanners(IntPtr windowHandle = default)
    {
        var names = new List<string>();

        // TWAIN sources
        try
        {
            if (windowHandle == IntPtr.Zero)
                windowHandle = Form.ActiveForm?.Handle ?? IntPtr.Zero;

            var appId = TWIdentity.CreateFromAssembly(
                DataGroups.Image | DataGroups.Control,
                Assembly.GetExecutingAssembly());
            var session = new TwainSession(appId);
            try
            {
                session.Open(new WindowsFormsMessageLoopHook(windowHandle));
                foreach (var ds in session.GetSources())
                    names.Add($"[TWAIN] {ds.Name}");
            }
            finally { session.Close(); }
        }
        catch { }

        // WIA devices
        try
        {
            var managerType = Type.GetTypeFromProgID("WIA.DeviceManager");
            if (managerType != null)
            {
                dynamic manager = Activator.CreateInstance(managerType)!;
                dynamic deviceInfos = manager.DeviceInfos;
                int count = (int)deviceInfos.Count;
                for (int i = 1; i <= count; i++)
                {
                    dynamic info = deviceInfos[i];
                    if ((int)info.Type == 1)
                    {
                        string name = (string)info.Properties["Name"].get_Value();
                        names.Add($"[WIA] {name}");
                    }
                }
            }
        }
        catch { }

        return names;
    }

    public List<Image> Scan(ScanSource source, int dpi = 300,
        ScanColorMode colorMode = ScanColorMode.Color,
        ScanDriver driver = ScanDriver.Auto,
        IntPtr windowHandle = default,
        string preferredScanner = "")
    {
        if (windowHandle == IntPtr.Zero)
            windowHandle = Form.ActiveForm?.Handle ?? IntPtr.Zero;

        return driver switch
        {
            ScanDriver.Twain => TwainScan(source, dpi, colorMode, windowHandle, preferredScanner),
            ScanDriver.Wia => WiaScan(source, dpi, colorMode, preferredScanner),
            _ => AutoScan(source, dpi, colorMode, windowHandle, preferredScanner)
        };
    }

    private List<Image> AutoScan(ScanSource source, int dpi,
        ScanColorMode colorMode, IntPtr hwnd, string preferredScanner)
    {
        try
        {
            return TwainScan(source, dpi, colorMode, hwnd, preferredScanner);
        }
        catch
        {
            return WiaScan(source, dpi, colorMode, preferredScanner);
        }
    }

    // ═══════════════════════════════════════════════
    //  TWAIN
    // ═══════════════════════════════════════════════

    private static List<Image> TwainScan(ScanSource source, int dpi,
        ScanColorMode colorMode, IntPtr hwnd, string preferredScanner)
    {
        var pages = new List<Image>();
        Exception? scanError = null;
        bool complete = false;

        var appId = TWIdentity.CreateFromAssembly(
            DataGroups.Image | DataGroups.Control,
            Assembly.GetExecutingAssembly());
        var session = new TwainSession(appId);

        session.DataTransferred += (_, e) =>
        {
            try
            {
                if (e.NativeData != IntPtr.Zero)
                {
                    using var stream = e.GetNativeImageStream();
                    if (stream != null)
                    {
                        using var img = Image.FromStream(stream);
                        pages.Add(new Bitmap(img));
                    }
                }
            }
            catch (Exception ex) { scanError ??= ex; }
        };

        session.TransferError += (_, e) =>
        {
            scanError ??= e.Exception
                ?? new InvalidOperationException("TWAIN transfer error");
        };

        session.SourceDisabled += (_, _) => complete = true;

        try
        {
            session.Open(new WindowsFormsMessageLoopHook(hwnd));
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                "Cannot initialize TWAIN.\n" +
                "Ensure TWAIN drivers are installed.", ex);
        }

        try
        {
            var sources = session.GetSources().ToList();
            if (sources.Count == 0)
                throw new InvalidOperationException(
                    "No TWAIN scanner found.\n\n" +
                    "• Ensure the scanner is connected and powered on.\n" +
                    "• Check that TWAIN drivers are installed.");

            var preferred = preferredScanner.StartsWith("[TWAIN] ")
                ? preferredScanner[8..] : preferredScanner;
            var ds = (!string.IsNullOrEmpty(preferred)
                ? sources.FirstOrDefault(s => s.Name == preferred) : null) ?? sources[0];
            if (ds.Open() != ReturnCode.Success)
                throw new InvalidOperationException(
                    $"Cannot open TWAIN source: {ds.Name}");

            // ── DPI ──
            TwainTrySet(() => ds.Capabilities.ICapXResolution.SetValue((TWFix32)dpi));
            TwainTrySet(() => ds.Capabilities.ICapYResolution.SetValue((TWFix32)dpi));

            // ── Color mode ──
            var pixelType = colorMode switch
            {
                ScanColorMode.BlackWhite => PixelType.BlackWhite,
                ScanColorMode.Grayscale => PixelType.Gray,
                _ => PixelType.RGB
            };
            TwainTrySet(() => ds.Capabilities.ICapPixelType.SetValue(pixelType));

            // ── Feeder / Duplex ──
            if (source is ScanSource.Feeder or ScanSource.Duplex)
            {
                TwainTrySet(() => ds.Capabilities.CapFeederEnabled.SetValue(BoolType.True));
                TwainTrySet(() => ds.Capabilities.CapAutoFeed.SetValue(BoolType.True));
                if (source == ScanSource.Duplex)
                    TwainTrySet(() => ds.Capabilities.CapDuplexEnabled.SetValue(BoolType.True));
            }
            else
            {
                TwainTrySet(() => ds.Capabilities.CapFeederEnabled.SetValue(BoolType.False));
            }

            // ── Start scanning with profile settings (no driver UI) ──
            ds.Enable(SourceEnableMode.NoUI, false, hwnd);

            // Pump WinForms messages cho đến khi scan xong
            while (!complete)
            {
                Application.DoEvents();
                Thread.Sleep(10);
            }

            ds.Close();
        }
        finally
        {
            session.Close();
        }

        if (scanError != null)
            throw new InvalidOperationException(
                $"Scan error: {scanError.Message}", scanError);

        return pages;
    }

    private static void TwainTrySet(Action action)
    {
        try { action(); } catch { /* capability not supported */ }
    }

    // ═══════════════════════════════════════════════
    //  WIA
    // ═══════════════════════════════════════════════

    private static List<Image> WiaScan(ScanSource source, int dpi, ScanColorMode colorMode,
        string preferredScanner)
    {
        return source switch
        {
            ScanSource.Feeder => WiaScanFromDevice(false, dpi, colorMode, preferredScanner),
            ScanSource.Duplex => WiaScanFromDevice(true, dpi, colorMode, preferredScanner),
            _ => WiaScanFlatbed(dpi, colorMode, preferredScanner)
        };
    }

    private static List<Image> WiaScanFlatbed(int dpi, ScanColorMode colorMode,
        string preferredScanner)
    {
        var pages = new List<Image>();

        var dialogType = Type.GetTypeFromProgID("WIA.CommonDialog")
            ?? throw new InvalidOperationException(
                "Windows Image Acquisition (WIA) is not available.");
        dynamic dialog = Activator.CreateInstance(dialogType)!;

        dynamic device;
        try
        {
            device = WiaConnectToScanner(preferredScanner);
        }
        catch (InvalidOperationException) { throw; }
        catch (COMException ex)
        {
            throw new InvalidOperationException(
                $"Cannot connect to scanner (0x{ex.HResult:X8}): {ex.Message}", ex);
        }

        dynamic item;
        try
        {
            item = device.Items[1];
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                "Scanner does not expose any scan items.", ex);
        }

        WiaTrySetProperty(item.Properties, WIA_IPS_XRES, dpi);
        WiaTrySetProperty(item.Properties, WIA_IPS_YRES, dpi);

        int intent = colorMode switch
        {
            ScanColorMode.Color => 1,
            ScanColorMode.Grayscale => 2,
            ScanColorMode.BlackWhite => 4,
            _ => 1
        };
        WiaTrySetProperty(item.Properties, WIA_IPS_CUR_INTENT, intent);

        try
        {
            dynamic imageFile = dialog.ShowTransfer(item, WIA_FORMAT_BMP, false);

            if (imageFile != null)
                pages.Add(LoadWiaImage(imageFile));
        }
        catch (COMException ex)
        {
            throw new InvalidOperationException(
                $"Scanner error (0x{ex.HResult:X8}): {ex.Message}", ex);
        }

        return pages;
    }

    private static List<Image> WiaScanFromDevice(bool duplex, int dpi, ScanColorMode colorMode,
        string preferredScanner)
    {
        var pages = new List<Image>();

        var dialogType = Type.GetTypeFromProgID("WIA.CommonDialog")
            ?? throw new InvalidOperationException(
                "Windows Image Acquisition (WIA) is not available.");
        dynamic dialog = Activator.CreateInstance(dialogType)!;

        dynamic device;
        try
        {
            device = WiaConnectToScanner(preferredScanner);
        }
        catch (InvalidOperationException) { throw; }
        catch (COMException ex)
        {
            throw new InvalidOperationException(
                $"Cannot connect to scanner (0x{ex.HResult:X8}): {ex.Message}", ex);
        }

        int handling = duplex ? (WIA_FEEDER | WIA_DUPLEX) : WIA_FEEDER;
        WiaTrySetProperty(device.Properties, WIA_DPS_DOCUMENT_HANDLING_SELECT, handling);
        WiaTrySetProperty(device.Properties, WIA_IPS_PAGES, 0);

        dynamic item;
        try
        {
            item = device.Items[1];
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                "Scanner does not expose any scan items. " +
                "Try switching to Flatbed mode.", ex);
        }

        WiaTrySetProperty(item.Properties, WIA_IPS_XRES, dpi);
        WiaTrySetProperty(item.Properties, WIA_IPS_YRES, dpi);

        int intent = colorMode switch
        {
            ScanColorMode.Color => 1,
            ScanColorMode.Grayscale => 2,
            ScanColorMode.BlackWhite => 4,
            _ => 1
        };
        WiaTrySetProperty(item.Properties, WIA_IPS_CUR_INTENT, intent);

        bool hasMore = true;
        while (hasMore)
        {
            try
            {
                dynamic imageFile = dialog.ShowTransfer(item, WIA_FORMAT_BMP, false);

                if (imageFile == null)
                {
                    hasMore = false;
                }
                else
                {
                    pages.Add(LoadWiaImage(imageFile));
                    hasMore = WiaIsFeedReady(device);
                }
            }
            catch (COMException ex) when (WiaIsPaperEmpty(ex))
            {
                hasMore = false;
            }
            catch (COMException ex)
            {
                if (pages.Count == 0)
                    throw new InvalidOperationException(
                        $"Scan error (0x{ex.HResult:X8}): {ex.Message}", ex);
                hasMore = false;
            }
        }

        return pages;
    }

    // ═══════════════════════════════════════════════
    //  WIA HELPERS
    // ═══════════════════════════════════════════════

    private static Image LoadWiaImage(dynamic imageFile)
    {
        string tempPath = Path.Combine(Path.GetTempPath(), $"dtscan_{Guid.NewGuid():N}.bmp");
        try
        {
            imageFile.SaveFile(tempPath);
            using var img = Image.FromFile(tempPath);
            return new Bitmap(img);
        }
        finally
        {
            try { File.Delete(tempPath); } catch { }
        }
    }

    private static dynamic WiaConnectToScanner(string preferredScanner = "")
    {
        var managerType = Type.GetTypeFromProgID("WIA.DeviceManager")
            ?? throw new InvalidOperationException(
                "Windows Image Acquisition (WIA) is not available.");
        dynamic manager = Activator.CreateInstance(managerType)!;

        dynamic deviceInfos = manager.DeviceInfos;
        int count = (int)deviceInfos.Count;

        var preferred = preferredScanner.StartsWith("[WIA] ")
            ? preferredScanner[6..] : preferredScanner;

        // Try to find the preferred scanner first
        if (!string.IsNullOrEmpty(preferred))
        {
            for (int i = 1; i <= count; i++)
            {
                dynamic info = deviceInfos[i];
                if ((int)info.Type == 1)
                {
                    string name = (string)info.Properties["Name"].get_Value();
                    if (name == preferred)
                        return info.Connect();
                }
            }
        }

        // Fallback: first available scanner
        for (int i = 1; i <= count; i++)
        {
            dynamic info = deviceInfos[i];
            if ((int)info.Type == 1)
                return info.Connect();
        }

        throw new InvalidOperationException(
            "No WIA scanner found.\n\n" +
            "• Ensure the scanner is connected and powered on.\n" +
            "• Check that WIA-compatible drivers are installed.\n" +
            $"• Total WIA devices detected: {count}");
    }

    private static bool WiaIsFeedReady(dynamic device)
    {
        try
        {
            var val = WiaTryGetProperty(device.Properties, WIA_DPS_DOCUMENT_HANDLING_STATUS);
            return val is int status && (status & WIA_FEED_READY) != 0;
        }
        catch { return false; }
    }

    private static bool WiaIsPaperEmpty(COMException ex)
        => ex.HResult is WIA_ERROR_PAPER_EMPTY or WIA_ERROR_OFFLINE;

    private static void WiaTrySetProperty(dynamic properties, int id, object value)
    {
        try
        {
            foreach (dynamic prop in properties)
            {
                if (prop.PropertyID == id)
                {
                    prop.Value = value;
                    return;
                }
            }
        }
        catch { }
    }

    private static object? WiaTryGetProperty(dynamic properties, int id)
    {
        try
        {
            foreach (dynamic prop in properties)
            {
                if (prop.PropertyID == id)
                    return prop.Value;
            }
        }
        catch { }
        return null;
    }
}
