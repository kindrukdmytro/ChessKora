using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using UnityEngine;
using Debug = UnityEngine.Debug;

public class UciAnalysisResult
{
    public int Depth;
    public bool HasCentipawnScore;
    public int CentipawnScore;
    public bool HasMateScore;
    public int MateIn;
    public string BestMove;
    public string PrincipalVariation;
    public bool IsComplete;
}

public class UciEngineClient : IDisposable
{
    public event Action<string> OnEngineStatusChanged;
    public event Action<UciAnalysisResult> OnAnalysisUpdated;

    public bool IsRunning => process != null && !process.HasExited;
    public bool IsReady { get; private set; }

    private Process process;
    private readonly ConcurrentQueue<string> outputQueue = new ConcurrentQueue<string>();
    private UciAnalysisResult currentResult = new UciAnalysisResult();

    public bool StartEngine(string executablePath)
    {
        if (string.IsNullOrWhiteSpace(executablePath))
        {
            EmitStatus("Engine path is empty.");
            return false;
        }

        if (!File.Exists(executablePath))
        {
            EmitStatus($"Engine file not found: {executablePath}");
            return false;
        }

        try
        {
            Shutdown();

            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                FileName = executablePath,
                WorkingDirectory = GetWorkingDirectory(executablePath),
                UseShellExecute = false,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            process = new Process
            {
                StartInfo = startInfo,
                EnableRaisingEvents = true
            };

            process.OutputDataReceived += OnProcessOutputDataReceived;
            process.ErrorDataReceived += OnProcessOutputDataReceived;

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            IsReady = false;
            currentResult = new UciAnalysisResult();

            EmitStatus("Engine started. Initializing...");
            SendCommand("uci");
            SendCommand("isready");

            return true;
        }
        catch (Exception ex)
        {
            CleanupProcess();
            EmitStatus($"Failed to start engine: {ex.Message}");
            return false;
        }
    }

    public void AnalyzeFen(string fen, int depth)
    {
        if (!IsRunning)
        {
            EmitStatus("Engine is not running.");
            return;
        }

        if (!IsReady)
        {
            EmitStatus("Engine is not ready yet.");
            return;
        }

        if (string.IsNullOrWhiteSpace(fen))
        {
            EmitStatus("FEN is empty.");
            return;
        }

        currentResult = new UciAnalysisResult();

        int safeDepth = Mathf.Max(1, depth);

        SendCommand("stop");
        SendCommand($"position fen {fen}");
        SendCommand($"go depth {safeDepth}");

        EmitStatus($"Analyzing position at depth {safeDepth}...");
    }

    public void StopAnalysis()
    {
        if (!IsRunning)
            return;

        SendCommand("stop");
        EmitStatus("Analysis stopped.");
    }

    public void PumpEvents()
    {
        while (outputQueue.TryDequeue(out string line))
            ParseEngineLine(line);
    }

    public void Shutdown()
    {
        try
        {
            if (process == null)
                return;

            if (!process.HasExited)
            {
                TrySendQuitCommands();

                if (!process.WaitForExit(500))
                    process.Kill();
            }
        }
        catch
        {
        }
        finally
        {
            CleanupProcess();
            EmitStatus("Engine stopped.");
        }
    }

    public void Dispose()
    {
        Shutdown();
    }

    private void OnProcessOutputDataReceived(object sender, DataReceivedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(e.Data))
            return;

        outputQueue.Enqueue(e.Data.Trim());
    }

    private void ParseEngineLine(string line)
    {
        if (string.IsNullOrWhiteSpace(line))
            return;

        if (line == "uciok")
        {
            EmitStatus("Engine UCI initialized.");
            return;
        }

        if (line == "readyok")
        {
            IsReady = true;
            EmitStatus("Engine ready.");
            return;
        }

        if (line.StartsWith("info ", StringComparison.Ordinal))
        {
            ParseInfoLine(line);
            return;
        }

        if (line.StartsWith("bestmove ", StringComparison.Ordinal))
        {
            ParseBestMoveLine(line);
        }
    }

    private void ParseInfoLine(string line)
    {
        string[] tokens = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (tokens.Length == 0)
            return;

        int depth = currentResult.Depth;
        bool hasCp = currentResult.HasCentipawnScore;
        int cp = currentResult.CentipawnScore;
        bool hasMate = currentResult.HasMateScore;
        int mate = currentResult.MateIn;
        string pv = currentResult.PrincipalVariation;

        for (int i = 0; i < tokens.Length; i++)
        {
            string token = tokens[i];

            if (token == "depth" && i + 1 < tokens.Length)
            {
                int.TryParse(tokens[i + 1], out depth);
                continue;
            }

            if (token == "score" && i + 2 < tokens.Length)
            {
                string scoreType = tokens[i + 1];
                string scoreValue = tokens[i + 2];

                if (scoreType == "cp")
                {
                    hasCp = int.TryParse(scoreValue, out cp);
                    hasMate = false;
                }
                else if (scoreType == "mate")
                {
                    hasMate = int.TryParse(scoreValue, out mate);
                    hasCp = false;
                }

                continue;
            }

            if (token == "pv" && i + 1 < tokens.Length)
            {
                pv = string.Join(" ", tokens, i + 1, tokens.Length - (i + 1));
                break;
            }
        }

        currentResult.Depth = depth;
        currentResult.HasCentipawnScore = hasCp;
        currentResult.CentipawnScore = cp;
        currentResult.HasMateScore = hasMate;
        currentResult.MateIn = mate;
        currentResult.PrincipalVariation = pv;

        EmitAnalysis();
    }

    private void ParseBestMoveLine(string line)
    {
        string[] tokens = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        currentResult.BestMove = tokens.Length >= 2 ? tokens[1] : string.Empty;
        currentResult.IsComplete = true;

        EmitStatus("Analysis complete.");
        EmitAnalysis();
    }

    private void SendCommand(string command)
    {
        if (!IsRunning || process == null)
            return;

        try
        {
            process.StandardInput.WriteLine(command);
            process.StandardInput.Flush();
        }
        catch
        {
        }
    }

    private void TrySendQuitCommands()
    {
        try
        {
            SendCommand("stop");
            SendCommand("quit");
        }
        catch
        {
        }
    }

    private void CleanupProcess()
    {
        if (process != null)
        {
            process.OutputDataReceived -= OnProcessOutputDataReceived;
            process.ErrorDataReceived -= OnProcessOutputDataReceived;
            process.Dispose();
            process = null;
        }

        IsReady = false;
        currentResult = new UciAnalysisResult();

        while (outputQueue.TryDequeue(out _))
        {
        }
    }

    private static string GetWorkingDirectory(string executablePath)
    {
        string directory = Path.GetDirectoryName(executablePath);
        return string.IsNullOrWhiteSpace(directory) ? Environment.CurrentDirectory : directory;
    }

    private void EmitStatus(string status)
    {
        Debug.Log($"[UciEngineClient] {status}");
        OnEngineStatusChanged?.Invoke(status);
    }

    private void EmitAnalysis()
    {
        UciAnalysisResult snapshot = new UciAnalysisResult
        {
            Depth = currentResult.Depth,
            HasCentipawnScore = currentResult.HasCentipawnScore,
            CentipawnScore = currentResult.CentipawnScore,
            HasMateScore = currentResult.HasMateScore,
            MateIn = currentResult.MateIn,
            BestMove = currentResult.BestMove,
            PrincipalVariation = currentResult.PrincipalVariation,
            IsComplete = currentResult.IsComplete
        };

        OnAnalysisUpdated?.Invoke(snapshot);
    }
}