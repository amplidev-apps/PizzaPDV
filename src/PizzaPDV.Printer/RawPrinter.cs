using System.Runtime.InteropServices;
using System.Text;

namespace PizzaPDV.Printer;

// Batata: fala direto com WinSpool (sem driver extra) — funciona na JP-58H 58mm genérica
public static class RawPrinter
{
    [DllImport("winspool.Drv", EntryPoint = "OpenPrinterA", SetLastError = true, CharSet = CharSet.Ansi)]
    private static extern bool OpenPrinter(string pPrinterName, out IntPtr phPrinter, IntPtr pDefault);

    [DllImport("winspool.Drv", SetLastError = true)]
    private static extern bool ClosePrinter(IntPtr hPrinter);

    [DllImport("winspool.Drv", EntryPoint = "StartDocPrinterA", SetLastError = true, CharSet = CharSet.Ansi)]
    private static extern bool StartDocPrinter(IntPtr hPrinter, int level, [In, MarshalAs(UnmanagedType.LPStruct)] DOCINFOA di);

    [DllImport("winspool.Drv", SetLastError = true)]
    private static extern bool EndDocPrinter(IntPtr hPrinter);

    [DllImport("winspool.Drv", SetLastError = true)]
    private static extern bool StartPagePrinter(IntPtr hPrinter);

    [DllImport("winspool.Drv", SetLastError = true)]
    private static extern bool EndPagePrinter(IntPtr hPrinter);

    [DllImport("winspool.Drv", SetLastError = true)]
    private static extern bool WritePrinter(IntPtr hPrinter, IntPtr pBytes, int dwCount, out int dwWritten);

    [ClassInterface(ClassInterfaceType.AutoDual)]
    [StructLayout(LayoutKind.Sequential)]
    private class DOCINFOA
    {
        [MarshalAs(UnmanagedType.LPStr)] public string pDocName = "PizzaPDV";
        [MarshalAs(UnmanagedType.LPStr)] public string pOutputFile = null!;
        [MarshalAs(UnmanagedType.LPStr)] public string pDataType = "RAW";
    }

    public static bool Print(string printerName, string text, out string error)
    {
        error = "";
        // ESC/POS: texto puro já funciona na JP-58H; adiciona cut no final
        var bytes = Encoding.GetEncoding(850).GetBytes(text + "\n\n\n\x1D\x56\x00");
        IntPtr pBytes = Marshal.AllocCoTaskMem(bytes.Length);
        Marshal.Copy(bytes, 0, pBytes, bytes.Length);
        try
        {
            if (!OpenPrinter(printerName.Normalize(), out var hPrinter, IntPtr.Zero))
            {
                error = "OpenPrinter falhou: " + Marshal.GetLastWin32Error();
                return false;
            }
            var di = new DOCINFOA();
            if (!StartDocPrinter(hPrinter, 1, di)) { error = "StartDoc falhou"; ClosePrinter(hPrinter); return false; }
            StartPagePrinter(hPrinter);
            WritePrinter(hPrinter, pBytes, bytes.Length, out _);
            EndPagePrinter(hPrinter);
            EndDocPrinter(hPrinter);
            ClosePrinter(hPrinter);
            return true;
        }
        catch (Exception ex) { error = ex.Message; return false; }
        finally { Marshal.FreeCoTaskMem(pBytes); }
    }

    // Helper: tenta JP-58H, depois impressora padrão
    public static (bool ok, string via) PrintAuto(string text)
    {
        var candidates = new[] { "JP-58H", "JP-58H (Copy 1)", "POS-58", "Generic Text Only" };
        foreach (var name in candidates)
        {
            if (Print(name, text, out var err) && string.IsNullOrEmpty(err)) return (true, name);
        }
        // Mock: salva em arquivo para debug batata sem impressora
        try
        {
            var path = Path.Combine(Path.GetTempPath(), $"pizzapdv_mock_{DateTime.Now:yyyyMMdd_HHmmss}.txt");
            File.WriteAllText(path, text, Encoding.UTF8);
            return (false, $"Mock salvo em {path}");
        }
        catch { return (false, "Sem impressora e falha mock"); }
    }
}
