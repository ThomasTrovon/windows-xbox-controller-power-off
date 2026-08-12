using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Principal;
using Microsoft.Win32.SafeHandles;

namespace XboxControllerOff;

internal static class Program
{
    private const string WorkerArgument = "--worker";
    private const string LogArgument = "--log";
    private const string ResultMarker = "RESULT=";

    private static int Main(string[] args)
    {
        try
        {
            if (args.Contains(WorkerArgument, StringComparer.OrdinalIgnoreCase))
            {
                return RunSystemWorker(GetArgumentValue(args, LogArgument));
            }

            if (!IsAdministrator())
            {
                Console.Error.WriteLine("Este aplicativo precisa ser executado como administrador.");
                return 1;
            }

            return RunControllerShutdown();
        }
        catch (Exception exception)
        {
            TryWriteFatalLog(exception);
            Console.Error.WriteLine($"Falha inesperada: {exception.Message}");
            return 1;
        }
    }

    private static int RunControllerShutdown()
    {
        string logPath = GetDefaultLogPath();
        Directory.CreateDirectory(Path.GetDirectoryName(logPath)!);
        string taskName = $"XboxControllerOff-{Guid.NewGuid():N}";
        string executablePath = Environment.ProcessPath
            ?? throw new InvalidOperationException("Não foi possível localizar o executável atual.");

        if (File.Exists(logPath))
        {
            File.Delete(logPath);
        }

        // O driver permite o comando administrativo somente para SYSTEM. A tarefa é criada
        // com nome aleatório, executada uma única vez e sempre removida no bloco finally.
        string taskCommand =
            $"\"{executablePath}\" {WorkerArgument} {LogArgument} \"{logPath}\"";

        try
        {
            RunSchtasks(
                "/Create", "/TN", taskName,
                "/TR", taskCommand,
                "/SC", "ONCE", "/ST", "00:00",
                "/RU", "SYSTEM", "/RL", "HIGHEST", "/F");
            RunSchtasks("/Run", "/TN", taskName);

            string log = WaitForWorkerLog(logPath, TimeSpan.FromSeconds(15));
            Console.WriteLine(log.TrimEnd());

            int? result = ParseWorkerResult(log);
            if (result is null)
            {
                Console.Error.WriteLine("O trabalhador não registrou um resultado final.");
                return 1;
            }

            return result.Value;
        }
        finally
        {
            // A exclusão também é tentada quando a criação, execução ou leitura do log falha.
            TryRunSchtasks("/Delete", "/TN", taskName, "/F");
        }
    }

    private static int RunSystemWorker(string? logPath)
    {
        if (string.IsNullOrWhiteSpace(logPath))
        {
            Console.Error.WriteLine("O caminho do log não foi informado ao trabalhador.");
            return 1;
        }

        WorkerLog log = new(logPath);

        try
        {
            WindowsIdentity identity = WindowsIdentity.GetCurrent();
            log.Write($"Trabalhador executando como: {identity.Name}");
            log.Write($"Horário: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");

            if (!IsLocalSystem(identity))
            {
                log.Write("ERRO: o trabalhador precisa executar como NT AUTHORITY\\SYSTEM.");
                log.WriteResult(1);
                return 1;
            }

            IReadOnlyList<ulong> controllerIds = XboxGip.DiscoverControllers();
            if (controllerIds.Count == 0)
            {
                log.Write("Nenhum controle Xbox GIP conectado foi encontrado.");
                log.WriteResult(2);
                return 2;
            }

            log.Write($"Encontrado(s) {controllerIds.Count} controle(s):");
            foreach (ulong controllerId in controllerIds)
            {
                log.Write($"  - {controllerId:X16}");
            }

            int failures = 0;
            foreach (ulong controllerId in controllerIds)
            {
                XboxGip.PowerOffResult result = XboxGip.PowerOffController(controllerId);
                log.Write(
                    $"Desligar {controllerId:X16}: " +
                    (result.Success ? "OK" : $"FALHOU (erro Win32 {result.Win32Error})"));

                if (!result.Success)
                {
                    failures++;
                }
            }

            int exitCode = failures == 0 ? 0 : 3;
            log.Write(failures == 0
                ? "Todos os controles encontrados foram desligados."
                : $"Falha ao desligar {failures} controle(s).");
            log.WriteResult(exitCode);
            return exitCode;
        }
        catch (Exception exception)
        {
            log.Write($"ERRO: {exception.Message}");
            log.Write(exception.ToString());
            log.WriteResult(1);
            return 1;
        }
    }

    private static string WaitForWorkerLog(string logPath, TimeSpan timeout)
    {
        Stopwatch stopwatch = Stopwatch.StartNew();
        string log = string.Empty;

        while (stopwatch.Elapsed < timeout)
        {
            if (File.Exists(logPath))
            {
                try
                {
                    log = File.ReadAllText(logPath);
                    if (log.Contains(ResultMarker, StringComparison.Ordinal))
                    {
                        return log;
                    }
                }
                catch (IOException)
                {
                    // O trabalhador pode estar entre duas gravações; a próxima leitura tenta de novo.
                }
            }

            Thread.Sleep(100);
        }

        return log;
    }

    private static int? ParseWorkerResult(string log)
    {
        string? resultLine = log
            .Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries)
            .LastOrDefault(line => line.StartsWith(ResultMarker, StringComparison.Ordinal));

        return resultLine is not null &&
               int.TryParse(resultLine[ResultMarker.Length..], out int result)
            ? result
            : null;
    }

    private static string GetDefaultLogPath()
    {
        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "XboxControllerOff",
            "ultima-execucao.log");
    }

    private static void TryWriteFatalLog(Exception exception)
    {
        try
        {
            string logPath = GetDefaultLogPath();
            Directory.CreateDirectory(Path.GetDirectoryName(logPath)!);
            File.WriteAllText(
                logPath,
                $"Falha no lançador em {DateTime.Now:yyyy-MM-dd HH:mm:ss}:{Environment.NewLine}" +
                exception + Environment.NewLine + ResultMarker + "1" + Environment.NewLine);
        }
        catch
        {
            // Se nem o log puder ser gravado, o código de saída ainda sinaliza a falha.
        }
    }

    private static void RunSchtasks(params string[] arguments)
    {
        ProcessResult result = ExecuteProcess("schtasks.exe", arguments);
        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"Falha ao executar schtasks (código {result.ExitCode}): " +
                $"{result.StandardError}{result.StandardOutput}".Trim());
        }
    }

    private static void TryRunSchtasks(params string[] arguments)
    {
        try
        {
            ExecuteProcess("schtasks.exe", arguments);
        }
        catch
        {
            // A limpeza é de melhor esforço para não ocultar a falha original.
        }
    }

    private static ProcessResult ExecuteProcess(string fileName, IEnumerable<string> arguments)
    {
        ProcessStartInfo startInfo = new(fileName)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        foreach (string argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using Process process = Process.Start(startInfo)
            ?? throw new InvalidOperationException($"Não foi possível iniciar {fileName}.");
        string standardOutput = process.StandardOutput.ReadToEnd();
        string standardError = process.StandardError.ReadToEnd();
        process.WaitForExit();

        return new ProcessResult(process.ExitCode, standardOutput, standardError);
    }

    private static string? GetArgumentValue(string[] args, string argumentName)
    {
        for (int index = 0; index < args.Length - 1; index++)
        {
            if (string.Equals(args[index], argumentName, StringComparison.OrdinalIgnoreCase))
            {
                return args[index + 1];
            }
        }

        return null;
    }

    private static bool IsAdministrator()
    {
        using WindowsIdentity identity = WindowsIdentity.GetCurrent();
        WindowsPrincipal principal = new(identity);
        return principal.IsInRole(WindowsBuiltInRole.Administrator);
    }

    private static bool IsLocalSystem(WindowsIdentity identity)
    {
        return identity.User?.IsWellKnown(WellKnownSidType.LocalSystemSid) == true;
    }

    private sealed record ProcessResult(int ExitCode, string StandardOutput, string StandardError);

    private sealed class WorkerLog
    {
        private readonly string _path;

        public WorkerLog(string path)
        {
            _path = path;
            string? directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(_path, string.Empty);
        }

        public void Write(string message)
        {
            File.AppendAllText(_path, message + Environment.NewLine);
            Console.WriteLine(message);
        }

        public void WriteResult(int exitCode) => Write($"{ResultMarker}{exitCode}");
    }
}

internal static class XboxGip
{
    private const uint GenericRead = 0x80000000;
    private const uint GenericWrite = 0x40000000;
    private const uint ShareReadWrite = 0x00000003;
    private const uint OpenExisting = 3;
    private const uint FileFlagOverlapped = 0x40000000;
    private const uint GipReenumerate = 0x40001CD0;
    private const uint GipControlDevice = 0x40001C4C;
    private const byte ControlTurnOff = 0x02;
    private const int ErrorIoPending = 997;
    private const uint WaitTimeout = 258;
    private const int MaximumControllers = 8;

    public static IReadOnlyList<ulong> DiscoverControllers()
    {
        using SafeFileHandle handle = CreateFileW(
            @"\\.\XboxGIP",
            GenericRead | GenericWrite,
            ShareReadWrite,
            IntPtr.Zero,
            OpenExisting,
            FileFlagOverlapped,
            IntPtr.Zero);

        if (handle.IsInvalid)
        {
            throw new InvalidOperationException(
                $"Não foi possível abrir \\.\\XboxGIP (erro Win32 {Marshal.GetLastWin32Error()}).");
        }

        byte[] reenumerateOutput = new byte[16];
        _ = DeviceIoControl(
            handle,
            GipReenumerate,
            null,
            0,
            reenumerateOutput,
            reenumerateOutput.Length,
            out _,
            IntPtr.Zero);

        IntPtr eventHandle = CreateEventW(IntPtr.Zero, true, false, null);
        if (eventHandle == IntPtr.Zero)
        {
            throw new InvalidOperationException(
                $"Não foi possível criar o evento de leitura GIP (erro Win32 {Marshal.GetLastWin32Error()}).");
        }

        try
        {
            List<ulong> controllerIds = new(MaximumControllers);
            HashSet<ulong> seenIds = new();
            int consecutiveTimeouts = 0;

            // A reenumeração publica mensagens de anúncio. Lemos várias mensagens porque o
            // adaptador novo pode manter até oito controles conectados simultaneamente.
            for (int attempt = 0; attempt < 24 && controllerIds.Count < MaximumControllers; attempt++)
            {
                ResetEvent(eventHandle);
                byte[] buffer = new byte[4096];
                NativeOverlappedData overlapped = new() { EventHandle = eventHandle };

                bool readStarted = ReadFile(
                    handle,
                    buffer,
                    buffer.Length,
                    out int bytesRead,
                    ref overlapped);

                if (!readStarted)
                {
                    int error = Marshal.GetLastWin32Error();
                    if (error != ErrorIoPending)
                    {
                        continue;
                    }

                    if (WaitForSingleObject(eventHandle, 300) == WaitTimeout)
                    {
                        CancelIo(handle);
                        WaitForSingleObject(eventHandle, 100);

                        // Depois que o primeiro controle aparece, dois intervalos sem novos
                        // anúncios são suficientes para concluir a enumeração rapidamente.
                        consecutiveTimeouts++;
                        if (controllerIds.Count > 0 && consecutiveTimeouts >= 2)
                        {
                            break;
                        }

                        continue;
                    }

                    if (!GetOverlappedResult(handle, ref overlapped, out bytesRead, false))
                    {
                        continue;
                    }
                }

                if (bytesRead < 12)
                {
                    continue;
                }

                consecutiveTimeouts = 0;

                ulong deviceId = BitConverter.ToUInt64(buffer, 0);
                byte commandId = buffer[8];
                if (deviceId != 0 &&
                    (commandId == 0x01 || commandId == 0x02) &&
                    seenIds.Add(deviceId))
                {
                    controllerIds.Add(deviceId);
                }
            }

            return controllerIds;
        }
        finally
        {
            CloseHandle(eventHandle);
        }
    }

    public static PowerOffResult PowerOffController(ulong controllerId)
    {
        using SafeFileHandle adminHandle = CreateFileW(
            @"\\.\XboxGIP_Admin",
            GenericRead | GenericWrite,
            ShareReadWrite,
            IntPtr.Zero,
            OpenExisting,
            0,
            IntPtr.Zero);

        if (adminHandle.IsInvalid)
        {
            return new PowerOffResult(false, Marshal.GetLastWin32Error());
        }

        // O buffer administrativo contém o identificador GIP de 64 bits e o
        // subcomando 0x02 (ControlTurnOff), descoberto por engenharia reversa.
        byte[] input = new byte[9];
        BitConverter.GetBytes(controllerId).CopyTo(input, 0);
        input[8] = ControlTurnOff;

        bool success = DeviceIoControl(
            adminHandle,
            GipControlDevice,
            input,
            input.Length,
            null,
            0,
            out _,
            IntPtr.Zero);

        return new PowerOffResult(success, success ? 0 : Marshal.GetLastWin32Error());
    }

    internal sealed record PowerOffResult(bool Success, int Win32Error);

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeOverlappedData
    {
        public UIntPtr Internal;
        public UIntPtr InternalHigh;
        public uint Offset;
        public uint OffsetHigh;
        public IntPtr EventHandle;
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern SafeFileHandle CreateFileW(
        string fileName,
        uint desiredAccess,
        uint shareMode,
        IntPtr securityAttributes,
        uint creationDisposition,
        uint flagsAndAttributes,
        IntPtr templateFile);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DeviceIoControl(
        SafeFileHandle device,
        uint controlCode,
        byte[]? inputBuffer,
        int inputBufferSize,
        byte[]? outputBuffer,
        int outputBufferSize,
        out int bytesReturned,
        IntPtr overlapped);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ReadFile(
        SafeFileHandle file,
        byte[] buffer,
        int bytesToRead,
        out int bytesRead,
        ref NativeOverlappedData overlapped);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr CreateEventW(
        IntPtr eventAttributes,
        [MarshalAs(UnmanagedType.Bool)] bool manualReset,
        [MarshalAs(UnmanagedType.Bool)] bool initialState,
        string? name);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetOverlappedResult(
        SafeFileHandle file,
        ref NativeOverlappedData overlapped,
        out int bytesTransferred,
        [MarshalAs(UnmanagedType.Bool)] bool wait);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CancelIo(SafeFileHandle file);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern uint WaitForSingleObject(IntPtr handle, uint milliseconds);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ResetEvent(IntPtr handle);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseHandle(IntPtr handle);
}
