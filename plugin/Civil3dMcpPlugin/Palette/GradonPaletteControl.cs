using System.Drawing;
using System.Windows.Forms;

namespace Civil3DMcpPlugin.Palette;

using Civil3DMcpPlugin.Agent;

/// <summary>
/// The Gradon chat palette: transcript, step log, write-mode toggle, input
/// box, and status line. WinForms (not WPF) so the plugin keeps compiling on
/// non-Windows hosts. Monochrome brand — black on white, mid-grey accents,
/// no color coding.
/// </summary>
public sealed class GradonPaletteControl : UserControl
{
  private static readonly Color InkColor = Color.Black;
  private static readonly Color PaperColor = Color.White;
  private static readonly Color GreyColor = Color.FromArgb(120, 120, 120);
  private static readonly Font BaseFont = new("Segoe UI", 9F);

  private readonly GradonConfig _config;
  private readonly GradonClient _client;
  private readonly GradonConversation _conversation;

  private CancellationTokenSource _cts = new();
  private bool _busy;
  private TaskCompletionSource<bool>? _confirmTcs;

  private readonly Label _statusLabel;
  private readonly RichTextBox _transcript;
  private readonly ListBox _stepLog;
  private readonly Panel _confirmPanel;
  private readonly Label _confirmLabel;
  private readonly RadioButton _askRadio;
  private readonly RadioButton _runRadio;
  private readonly TextBox _input;
  private readonly Button _sendButton;
  private readonly Button _stopButton;

  public GradonPaletteControl()
  {
    _config = GradonConfig.Load();
    _client = new GradonClient(_config);
    _conversation = new GradonConversation(_client)
    {
      ConfirmWriteAsync = ShowConfirmAsync,
    };
    _conversation.AssistantText += text => AppendTranscript("Gradon", text);
    _conversation.StepExecuted += OnStepExecuted;
    _conversation.Completed += OnCompleted;
    _conversation.Failed += text => AppendTranscript("Gradon", $"Error: {text}");

    Dock = DockStyle.Fill;
    BackColor = PaperColor;
    ForeColor = InkColor;
    Font = BaseFont;

    var root = new TableLayoutPanel
    {
      Dock = DockStyle.Fill,
      ColumnCount = 1,
      RowCount = 6,
      BackColor = PaperColor,
    };
    root.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
    root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
    root.RowStyles.Add(new RowStyle(SizeType.Absolute, 110));
    root.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
    root.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
    root.RowStyles.Add(new RowStyle(SizeType.Absolute, 72));

    // Row 0: status line + stop button
    var statusPanel = new Panel { Dock = DockStyle.Fill, BackColor = PaperColor };
    _statusLabel = new Label
    {
      Dock = DockStyle.Fill,
      TextAlign = ContentAlignment.MiddleLeft,
      ForeColor = GreyColor,
      Text = "Gradon — starting…",
      AutoEllipsis = true,
    };
    _stopButton = new Button
    {
      Dock = DockStyle.Right,
      Width = 56,
      Text = "Stop",
      FlatStyle = FlatStyle.Flat,
      Enabled = false,
    };
    _stopButton.FlatAppearance.BorderColor = GreyColor;
    _stopButton.Click += (_, _) => _cts.Cancel();
    statusPanel.Controls.Add(_statusLabel);
    statusPanel.Controls.Add(_stopButton);

    // Row 1: transcript
    _transcript = new RichTextBox
    {
      Dock = DockStyle.Fill,
      ReadOnly = true,
      BorderStyle = BorderStyle.FixedSingle,
      BackColor = PaperColor,
      ForeColor = InkColor,
      Font = BaseFont,
    };

    // Row 2: step log
    _stepLog = new ListBox
    {
      Dock = DockStyle.Fill,
      BorderStyle = BorderStyle.FixedSingle,
      BackColor = PaperColor,
      ForeColor = InkColor,
      Font = new Font("Consolas", 8.5F),
      IntegralHeight = false,
    };

    // Row 3: inline write-confirmation prompt (hidden until needed)
    _confirmPanel = new Panel { Dock = DockStyle.Fill, BackColor = PaperColor, Visible = false };
    _confirmLabel = new Label { Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, AutoEllipsis = true };
    var yesButton = new Button { Dock = DockStyle.Right, Width = 48, Text = "Yes", FlatStyle = FlatStyle.Flat };
    var noButton = new Button { Dock = DockStyle.Right, Width = 48, Text = "No", FlatStyle = FlatStyle.Flat };
    yesButton.FlatAppearance.BorderColor = GreyColor;
    noButton.FlatAppearance.BorderColor = GreyColor;
    yesButton.Click += (_, _) => ResolveConfirm(true);
    noButton.Click += (_, _) => ResolveConfirm(false);
    _confirmPanel.Controls.Add(_confirmLabel);
    _confirmPanel.Controls.Add(noButton);
    _confirmPanel.Controls.Add(yesButton);

    // Row 4: write-mode toggle
    var modePanel = new FlowLayoutPanel { Dock = DockStyle.Fill, BackColor = PaperColor, WrapContents = false };
    _askRadio = new RadioButton { Text = "Ask before every write", Checked = true, AutoSize = true, Margin = new Padding(0, 4, 12, 0) };
    _runRadio = new RadioButton { Text = "Run without asking", AutoSize = true, Margin = new Padding(0, 4, 0, 0) };
    _askRadio.CheckedChanged += (_, _) => { if (_askRadio.Checked) _conversation.Mode = WriteMode.AskBeforeWrite; };
    _runRadio.CheckedChanged += (_, _) => { if (_runRadio.Checked) _conversation.Mode = WriteMode.RunWithoutAsking; };
    modePanel.Controls.Add(_askRadio);
    modePanel.Controls.Add(_runRadio);

    // Row 5: input + send
    var inputPanel = new Panel { Dock = DockStyle.Fill, BackColor = PaperColor };
    _sendButton = new Button { Dock = DockStyle.Right, Width = 64, Text = "Send", FlatStyle = FlatStyle.Flat };
    _sendButton.FlatAppearance.BorderColor = GreyColor;
    _sendButton.Click += async (_, _) => await SendCurrentInputAsync();
    _input = new TextBox
    {
      Dock = DockStyle.Fill,
      Multiline = true,
      Font = BaseFont,
      BorderStyle = BorderStyle.FixedSingle,
      ScrollBars = ScrollBars.Vertical,
    };
    _input.KeyDown += Input_KeyDown;
    inputPanel.Controls.Add(_input);
    inputPanel.Controls.Add(_sendButton);

    root.Controls.Add(statusPanel, 0, 0);
    root.Controls.Add(_transcript, 0, 1);
    root.Controls.Add(_stepLog, 0, 2);
    root.Controls.Add(_confirmPanel, 0, 3);
    root.Controls.Add(modePanel, 0, 4);
    root.Controls.Add(inputPanel, 0, 5);

    Controls.Add(root);
  }

  /// <summary>Called once after the palette is shown. Fire-and-forget by design (UI entry point).</summary>
  public async void Initialize()
  {
    AppendTranscript("Gradon", "Ready. Tell me what to do in this drawing.");
    await RefreshHealthAsync();
  }

  /// <summary>Called when the AutoCAD command GRADONSTOP runs, or the palette is closing for good.</summary>
  public void Shutdown()
  {
    _cts.Cancel();
    _client.Dispose();
  }

  private async Task RefreshHealthAsync()
  {
    _statusLabel.Text = $"{_config.ServiceUrl} — checking…";
    var (ok, detail) = await _client.CheckHealthAsync(CancellationToken.None);
    _statusLabel.Text = ok
      ? $"{_config.ServiceUrl} — connected"
      : $"{_config.ServiceUrl} — disconnected ({detail})";
  }

  private void Input_KeyDown(object? sender, KeyEventArgs e)
  {
    if (e.KeyCode == Keys.Enter && !e.Shift)
    {
      e.SuppressKeyPress = true;
      e.Handled = true;
      _ = SendCurrentInputAsync();
    }
  }

  private async Task SendCurrentInputAsync()
  {
    if (_busy) return;

    var message = _input.Text.Trim();
    if (message.Length == 0) return;

    _input.Clear();
    AppendTranscript("You", message);
    SetBusy(true);

    if (_cts.IsCancellationRequested)
      _cts = new CancellationTokenSource();

    try
    {
      await _conversation.SendUserMessageAsync(message, _cts.Token);
    }
    catch (OperationCanceledException)
    {
      AppendTranscript("Gradon", "Stopped.");
    }
    catch (Exception ex)
    {
      AppendTranscript("Gradon", $"Error: {ex.Message}");
    }
    finally
    {
      SetBusy(false);
    }
  }

  private void SetBusy(bool busy)
  {
    _busy = busy;
    _sendButton.Enabled = !busy;
    _input.Enabled = !busy;
    _stopButton.Enabled = busy;
  }

  private Task<bool> ShowConfirmAsync(string description)
  {
    _confirmTcs = new TaskCompletionSource<bool>();
    _confirmLabel.Text = description;
    _confirmPanel.Visible = true;
    return _confirmTcs.Task;
  }

  private void ResolveConfirm(bool proceed)
  {
    _confirmPanel.Visible = false;
    _confirmTcs?.TrySetResult(proceed);
    _confirmTcs = null;
  }

  private void OnStepExecuted(StepLogItem step)
  {
    var mark = step.Ok ? "✓" : "✗";
    var line = step.Error == null
      ? $"{mark} {step.Description} ({step.DurationMs} ms)"
      : $"{mark} {step.Description} ({step.DurationMs} ms) — {step.Error}";
    _stepLog.Items.Add(line);
    _stepLog.TopIndex = _stepLog.Items.Count - 1;
  }

  private void OnCompleted(TurnSummary summary)
  {
    AppendTranscript(
      "Gradon",
      $"Done — {summary.Steps} step(s), {summary.Writes} write(s), " +
      $"~{summary.TimeSavedMin:0.#} min saved.");
  }

  private void AppendTranscript(string speaker, string text)
  {
    _transcript.SelectionStart = _transcript.TextLength;
    _transcript.SelectionLength = 0;

    _transcript.SelectionFont = new Font(BaseFont, FontStyle.Bold);
    _transcript.SelectionColor = speaker == "You" ? InkColor : GreyColor;
    _transcript.AppendText($"{speaker}: ");

    _transcript.SelectionFont = new Font(BaseFont, FontStyle.Regular);
    _transcript.SelectionColor = InkColor;
    _transcript.AppendText(text.TrimEnd() + Environment.NewLine + Environment.NewLine);

    _transcript.SelectionStart = _transcript.TextLength;
    _transcript.ScrollToCaret();
  }
}
