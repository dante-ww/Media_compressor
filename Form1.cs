using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Xabe.FFmpeg;
using Xabe.FFmpeg.Downloader;

namespace MediaCruncher;

public partial class Form1 : Form
{
    // =================================================================
    // Lines 22 to 24
    // Configuration constants that define the target file size,
    // audio bitrate, and image quality for the process.
    // =================================================================
    private const double TARGET_FILE_SIZE_MB = 9.2;
    private const long TARGET_AUDIO_BITRATE_BPS = 96000;
    private const long TARGET_JPEG_QUALITY = 75L;

    // =================================================================
    // Lines 32 to 40
    // Declaration of UI controls, timers, and state variables
    // used to manage visual feedback, task cancellation, and
    // the PayPal support button.
    // =================================================================
    private TextLabel statusLabel;
    private PictureBox backgroundPictureBox;
    private RoundedProgressBar modernProgressBar;
    private TextLabel hdPercentageLabel;
    private GlowingButton cancelButton;
    private PictureBox donateButton;
    private System.Windows.Forms.Timer animationTimer;
    private int finalYPosition;
    private CancellationTokenSource? cancellationTokenSource;

    // =================================================================
    // Lines 48 to 137
    // Form constructor. Initializes the UI components, calculates
    // startup coordinates, wires up drag-and-drop events, and
    // configures the cancel and PayPal donation buttons.
    // =================================================================
    public Form1()
    {
        InitializeComponent();
        this.Text = "Media Cruncher Express";
        this.Size = new Size(450, 300);
        this.Icon = new Icon(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "MediaCruncher.ico"));
        this.FormBorderStyle = FormBorderStyle.FixedSingle;
        this.BackColor = Color.FromArgb(32, 34, 37);
        Rectangle workArea = Screen.PrimaryScreen!.WorkingArea;
        this.StartPosition = FormStartPosition.Manual;
        this.Left = (workArea.Width - this.Width) / 2;
        this.Top = workArea.Bottom;
        this.MaximizeBox = false;
        this.FormBorderStyle = FormBorderStyle.FixedSingle;
        finalYPosition = workArea.Bottom - this.Height - 30;
        animationTimer = new System.Windows.Forms.Timer();
        animationTimer.Interval = 10;
        animationTimer.Tick += AnimationTimerTick;
        this.Load += async (sender, e) =>
        {
            animationTimer.Start();
            await InitializeFFmpeg();
        };
        backgroundPictureBox = new PictureBox();
        backgroundPictureBox.Dock = DockStyle.Fill;
        backgroundPictureBox.SizeMode = PictureBoxSizeMode.StretchImage;
        this.Controls.Add(backgroundPictureBox);
        statusLabel = new TextLabel();
        statusLabel.Text = "Starting application...";
        statusLabel.Font = new Font("Baskerville Old Face", 16, FontStyle.Regular);
        statusLabel.ForeColor = Color.White;
        statusLabel.TextAlign = ContentAlignment.MiddleCenter;
        statusLabel.Parent = backgroundPictureBox;
        statusLabel.BackColor = Color.Transparent;
        statusLabel.AutoSize = false;
        statusLabel.Width = this.ClientSize.Width;
        statusLabel.Height = 60;
        statusLabel.Location = new Point(0, 120);
        modernProgressBar = new RoundedProgressBar();
        modernProgressBar.Width = 230;
        modernProgressBar.Height = 22;
        modernProgressBar.Location = new Point(100, 165);
        modernProgressBar.Visible = false;
        backgroundPictureBox.Controls.Add(modernProgressBar);
        hdPercentageLabel = new TextLabel();
        hdPercentageLabel.Text = "0%";
        hdPercentageLabel.Font = new Font("Segoe UI", 17, FontStyle.Regular);
        hdPercentageLabel.ForeColor = Color.White;
        hdPercentageLabel.AutoSize = false;
        hdPercentageLabel.Width = 100;
        hdPercentageLabel.Height = 50;
        hdPercentageLabel.Location = new Point(330, 155);
        hdPercentageLabel.Visible = false;
        backgroundPictureBox.Controls.Add(hdPercentageLabel);

        cancelButton = new GlowingButton();
        cancelButton.Text = "Cancel";
        cancelButton.Font = new Font("Segoe UI", 8, FontStyle.Bold);
        cancelButton.Size = new Size(85, 26);
        cancelButton.Location = new Point((this.ClientSize.Width - 85) / 2, 195);
        cancelButton.ForeColor = Color.White;
        cancelButton.Cursor = Cursors.Hand;
        cancelButton.Visible = false;
        cancelButton.Click += CancelButton_Click;
        backgroundPictureBox.Controls.Add(cancelButton);

        donateButton = new PictureBox();
        donateButton.Size = new Size(35, 35);
        donateButton.Location = new Point(this.ClientSize.Width - 45, this.ClientSize.Height - 45);
        donateButton.BackColor = Color.Transparent;
        donateButton.Cursor = Cursors.Hand;
        donateButton.SizeMode = PictureBoxSizeMode.Zoom;
        donateButton.Visible = true;
        donateButton.ErrorImage = null;
        donateButton.InitialImage = null;
        donateButton.Paint += (sender, e) =>
        {
            using (Pen bordeNegro = new Pen(Color.Black, 1))
            {
                e.Graphics.DrawRectangle(bordeNegro, 1, 1, donateButton.Width - 2, donateButton.Height - 2);
            }
        };

        donateButton.Click += DonateButton_Click;
        backgroundPictureBox.Controls.Add(donateButton);
        LoadPaypalIcon();

        this.AllowDrop = false;
        this.DragEnter += Form1_DragEnter;
        this.DragDrop += Form1_DragDrop;
        this.DragLeave += Form1_DragLeave;
    }

    // =================================================================
    // Lines 144 to 159
    // Asynchronously downloads the PayPal icon from the web,
    // bypassing server restrictions by spoofing a browser user-agent.
    // =================================================================
    private async void LoadPaypalIcon()
    {
        try
        {
            using (var client = new System.Net.Http.HttpClient())
            {
                client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64)");
                var imageBytes = await client.GetByteArrayAsync("https://img.icons8.com/color/48/paypal.png");
                using (var ms = new System.IO.MemoryStream(imageBytes))
                {
                    donateButton.Image = Image.FromStream(ms);
                }
            }
        }
        catch { }
    }

    /// =================================================================
    // Lines 167 to 189
    // Click event handlers for the cancel and donate buttons.
    // Safely requests task cancellation or opens the PayPal
    // support link in the user's default web browser.
    // =================================================================
    private void CancelButton_Click(object? sender, EventArgs e)
    {
        if (cancellationTokenSource != null && !cancellationTokenSource.IsCancellationRequested)
        {
            cancelButton.Text = "Canceling...";
            cancelButton.Enabled = false;
            cancellationTokenSource.Cancel();
        }
    }

    private void DonateButton_Click(object? sender, EventArgs e)
    {
        string url = "https://www.paypal.me/dantecode07";
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true
            });
        }
        catch { }
    }

    // =================================================================
    // Lines 197 to 226
    // Asynchronous method to initialize FFmpeg. It downloads
    // the executables on first use if missing, sets up the UI, 
    // and displays user-friendly error alerts if the download fails.
    // =================================================================
    private async Task InitializeFFmpeg()
    {
        string appPath = AppDomain.CurrentDomain.BaseDirectory;
        string ffmpegExePath = Path.Combine(appPath, "ffmpeg.exe");
        if (!File.Exists(ffmpegExePath))
        {
            ChangeBackground("process.gif");

            statusLabel.Text = "Downloading FFmpeg for first use...\nPlease wait, this only happens once.";
            try
            {
                await FFmpegDownloader.GetLatestVersion(FFmpegVersion.Official, appPath);
            }
            catch (Exception ex)
            {
                using (var alert = new CustomAlertWindow("Start Error", $"We couldn't download the required files.\nPlease check your internet connection and try again.\n\nTechnical: {ex.Message}", null))
                {
                    alert.ShowDialog();
                }
                statusLabel.Text = "Start error.\nPlease reopen the app or download FFmpeg manually.";
                return;
            }
        }
        Xabe.FFmpeg.FFmpeg.SetExecutablesPath(appPath);
        modernProgressBar.Visible = false;
        hdPercentageLabel.Visible = false;
        this.AllowDrop = true;
        statusLabel.Text = "Drag your video or photo here\n(It will be compressed to less than 10 MB)";
        ChangeBackground("idle.gif");
    }

    // =================================================================
    // Lines 235 to 282
    // UI Interaction and Visual Feedback Handlers.
    // Manages the smooth startup popup animation, dynamically updates
    // the animated GIF background based on the application state, and 
    // handles drag-and-drop file validation for supported media types.
    // =================================================================
    private void AnimationTimerTick(object? sender, EventArgs e)
    {
        int speed = 12;
        if (this.Top > finalYPosition)
        {
            this.Top -= speed;
            if (this.Top < finalYPosition) this.Top = finalYPosition;
        }
        else
        {
            animationTimer.Stop();
        }
    }

    private void ChangeBackground(string gifName)
    {
        string gifPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "resources", "gifs", gifName);
        backgroundPictureBox.Image = File.Exists(gifPath) ? Image.FromFile(gifPath) : null;
    }

    private void Form1_DragEnter(object? sender, DragEventArgs e)
    {
        if (e.Data != null && e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            string[] files = (string[])e.Data.GetData(DataFormats.FileDrop)!;
            bool isValid = false;
            if (files.Length > 0)
            {
                string extension = Path.GetExtension(files[0]).ToLower();
                isValid = extension == ".mp4" || extension == ".mkv" || extension == ".avi" || extension == ".mov" ||
                           extension == ".jpg" || extension == ".jpeg" || extension == ".png" || extension == ".webp";
            }
            if (isValid)
            {
                e.Effect = DragDropEffects.Copy;
                ChangeBackground("file_accepted.gif");
            }
            else
            {
                e.Effect = DragDropEffects.None;
                ChangeBackground("file_denied.gif");
            }
        }
    }
    private void Form1_DragLeave(object? sender, EventArgs e)
    {
        ChangeBackground("idle.gif");
    }

    // =================================================================
    // Lines 291 to 392
    // Main drag-and-drop logic. Sets up the cancellation token,
    // updates the UI, loops through dropped files, and triggers the
    // appropriate compression tasks. Also handles user-friendly
    // alerts for critical errors or successful process completion.
    // =================================================================
    private async void Form1_DragDrop(object? sender, DragEventArgs e)
    {
        if (e.Data != null && e.Data.GetData(DataFormats.FileDrop) is string[] files)
        {
            if (files.Length > 0)
            {
                string outputBaseFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Files Compressed");
                this.AllowDrop = false;

                modernProgressBar.Visible = true;
                hdPercentageLabel.Visible = true;

                cancellationTokenSource = new CancellationTokenSource();
                cancelButton.Enabled = true;
                cancelButton.Text = "Cancel";
                cancelButton.Visible = true;

                modernProgressBar.BringToFront();
                hdPercentageLabel.BringToFront();
                cancelButton.BringToFront();
                donateButton.BringToFront();

                modernProgressBar.Refresh();
                hdPercentageLabel.Refresh();
                ChangeBackground("process.gif");

                int totalFiles = files.Length;
                int processedFiles = 0;
                int compressedFiles = 0;

                try
                {
                    foreach (string originalPath in files)
                    {
                        cancellationTokenSource.Token.ThrowIfCancellationRequested();
                        processedFiles++;
                        string extension = Path.GetExtension(originalPath).ToLower();
                        if (extension == ".mp4" || extension == ".mkv" || extension == ".avi" || extension == ".mov")
                        {
                            statusLabel.Text = $"Processing {processedFiles} of {totalFiles}...";
                            modernProgressBar.Value = 0;
                            hdPercentageLabel.Text = "0%";
                            string videoFolder = Path.Combine(outputBaseFolder, "video");
                            if (!Directory.Exists(videoFolder)) Directory.CreateDirectory(videoFolder);
                            string finalPath = GenerateUniqueName(videoFolder, "film_compressed", ".mp4");
                            await ProcessVideo(originalPath, finalPath, cancellationTokenSource.Token);
                            compressedFiles++;
                        }
                        else if (extension == ".jpg" || extension == ".jpeg" || extension == ".png" || extension == ".webp")
                        {
                            statusLabel.Text = $"Processing {processedFiles} of {totalFiles}...";
                            modernProgressBar.Value = 0;
                            hdPercentageLabel.Text = "0%";
                            string imagesFolder = Path.Combine(outputBaseFolder, "images");
                            if (!Directory.Exists(imagesFolder)) Directory.CreateDirectory(imagesFolder);
                            string finalPath = GenerateUniqueName(imagesFolder, "image_compressed", ".jpg");
                            await ProcessImage(originalPath, finalPath, cancellationTokenSource.Token);
                            compressedFiles++;
                        }
                    }
                    if (compressedFiles > 0)
                    {
                        string victoryGifPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "resources", "gifs", "uma_victory.gif");
                        if (File.Exists(victoryGifPath))
                        {
                            using (Image celebrationGif = Image.FromFile(victoryGifPath))
                            using (var alert = new CustomAlertWindow("Media Cruncher", $"Process completed!\n\n{compressedFiles} file(s) were compressed successfully.", celebrationGif))
                            {
                                alert.ShowDialog();
                            }
                        }
                        else
                        {
                            MessageBox.Show($"Process completed!\n\n{compressedFiles} file(s) were compressed successfully.", "Media Cruncher", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    statusLabel.Text = "Process canceled by user.";
                }
                catch (Exception ex)
                {
                    using (var alert = new CustomAlertWindow("Critical Error", $"Something went wrong while processing the files.\nPlease restart the app and try again.\n\nTechnical: {ex.Message}", null))
                    {
                        alert.ShowDialog();
                    }
                }
                finally
                {
                    this.AllowDrop = true;
                    ChangeBackground("idle.gif");
                    statusLabel.Text = "Drag your video or photo here\n(It will be compressed to less than 10 MB)";
                    modernProgressBar.Visible = false;
                    hdPercentageLabel.Visible = false;
                    cancelButton.Visible = false;
                    cancellationTokenSource?.Dispose();
                    cancellationTokenSource = null;
                }
            }
        }
    }

    // =================================================================
    // Lines 402 to 509
    // Core Media Processing and File Management Engines.
    // Handles the generation of unique filenames to prevent overwrites,
    // and executes the heavy compression tasks for both videos and images.
    // Includes dynamic bitrate calculation, UI progress updates, graceful
    // cancellation support, and user-friendly error handling.
    // =================================================================
    private string GenerateUniqueName(string folder, string prefix, string extension)
    {
        string finalPath = Path.Combine(folder, $"{prefix}{extension}");
        int counter = 1;
        while (File.Exists(finalPath))
        {
            finalPath = Path.Combine(folder, $"{prefix}_({counter}){extension}");
            counter++;
        }
        return finalPath;
    }
    private async Task ProcessVideo(string origin, string destination, CancellationToken token)
    {
        try
        {
            IMediaInfo videoInfo = await Xabe.FFmpeg.FFmpeg.GetMediaInfo(origin);
            double durationSeconds = videoInfo.Duration.TotalSeconds;
            IVideoStream? videoStream = videoInfo.VideoStreams.FirstOrDefault();
            IAudioStream? audioStream = videoInfo.AudioStreams.FirstOrDefault();
            if (videoStream == null) return;
            long targetBits = (long)(TARGET_FILE_SIZE_MB * 1024 * 1024 * 8);
            long totalBitrateBps = (long)(targetBits / durationSeconds);
            long videoBitrateBps = audioStream != null ? totalBitrateBps - TARGET_AUDIO_BITRATE_BPS : totalBitrateBps;
            if (videoBitrateBps < 150000) videoBitrateBps = 150000;
            if (videoBitrateBps > 8000000) videoBitrateBps = 8000000;
            if (videoBitrateBps < 1200000) videoStream.SetSize(VideoSize.Hd480);
            else if (videoBitrateBps < 2500000) videoStream.SetSize(VideoSize.Hd720);
            videoStream.SetBitrate(videoBitrateBps);
            audioStream?.SetBitrate(TARGET_AUDIO_BITRATE_BPS);
            string outputMp4Path = Path.ChangeExtension(destination, ".mp4");
            string bitrateParameters = $"-maxrate {videoBitrateBps} -bufsize {videoBitrateBps * 2}";
            IConversion conversion = Xabe.FFmpeg.FFmpeg.Conversions.New()
                .AddStream(videoStream)
                .SetOutput(outputMp4Path);
            if (audioStream != null) conversion.AddStream(audioStream);
            conversion.AddParameter($"-c:v libx264 -preset slow {bitrateParameters}");
            conversion.OnProgress += (sender, args) =>
            {
                this.Invoke(new Action(() =>
                {
                    int percentage = args.Percent;
                    hdPercentageLabel.Text = $"{percentage}%";
                    modernProgressBar.Value = percentage;
                }));
            };
            await conversion.Start(token);
        }
        catch (OperationCanceledException)
        {
            string outputMp4Path = Path.ChangeExtension(destination, ".mp4");
            await Task.Delay(500);
            if (File.Exists(outputMp4Path))
            {
                try { File.Delete(outputMp4Path); } catch { }
            }
            throw;
        }
        catch (Exception ex)
        {
            using (var alert = new CustomAlertWindow("Conversion Error", $"This video couldn't be compressed.\nIt might be open in another program or unsupported.\n\nTechnical: {ex.Message}", null))
            {
                alert.ShowDialog();
            }
        }
    }

    private async Task ProcessImage(string origin, string destination, CancellationToken token)
    {
        try
        {
            await Task.Run(() =>
            {
                token.ThrowIfCancellationRequested();
                using (Image originalImage = Image.FromFile(origin))
                {
                    ImageCodecInfo? jpgEncoder = GetEncoder(ImageFormat.Jpeg);
                    if (jpgEncoder == null) throw new Exception("The image compression engine could not be started.");
                    Encoder qualityProperty = Encoder.Quality;
                    EncoderParameters encoderParameters = new EncoderParameters(1);
                    EncoderParameter qualityParameter = new EncoderParameter(qualityProperty, TARGET_JPEG_QUALITY);
                    encoderParameters.Param[0] = qualityParameter;
                    string outputJpgPath = Path.ChangeExtension(destination, ".jpg");
                    originalImage.Save(outputJpgPath, jpgEncoder, encoderParameters);
                }
            }, token);
            this.Invoke(new Action(() =>
            {
                hdPercentageLabel.Text = "100%";
                modernProgressBar.Value = 100;
            }));
        }
        catch (OperationCanceledException)
        {
            string outputJpgPath = Path.ChangeExtension(destination, ".jpg");
            if (File.Exists(outputJpgPath))
            {
                try { File.Delete(outputJpgPath); } catch { }
            }
            throw;
        }
        catch (Exception ex)
        {
            using (var alert = new CustomAlertWindow("Image Error", $"This image couldn't be compressed.\nPlease ensure the file isn't corrupted.\n\nTechnical: {ex.Message}", null))
            {
                alert.ShowDialog();
            }
        }
    }

    // =================================================================
    // Lines 518 to 578
    // Image Processing Helpers & Custom UI Controls.
    // Contains the codec retrieval method required for image encoding,
    // followed by the definition of the GlowingButton class, a custom
    // control featuring a responsive radial gradient hover effect.
    // =================================================================
    private ImageCodecInfo? GetEncoder(ImageFormat format)
    {
        ImageCodecInfo[] encoders = ImageCodecInfo.GetImageEncoders();
        return encoders.FirstOrDefault(encoder => encoder.FormatID == format.Guid);
    }
}
public class GlowingButton : Button
{
    private bool isHovered = false;
    public GlowingButton()
    {
        this.SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
        this.BackColor = Color.Transparent;
    }
    protected override void OnMouseEnter(EventArgs e)
    {
        base.OnMouseEnter(e);
        isHovered = true;
        this.Invalidate();
    }
    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        isHovered = false;
        this.Invalidate();
    }
    protected override void OnPaint(PaintEventArgs e)
    {
        Rectangle rect = this.ClientRectangle;
        if (rect.Width == 0 || rect.Height == 0) return;
        e.Graphics.SmoothingMode = SmoothingMode.HighQuality;
        using (GraphicsPath path = new GraphicsPath())
        {
            path.AddRectangle(rect);
            using (PathGradientBrush brush = new PathGradientBrush(path))
            {
                if (isHovered)
                {
                    brush.CenterColor = Color.FromArgb(255, 90, 90);
                    brush.SurroundColors = new Color[] { Color.FromArgb(170, 40, 40) };
                }
                else
                {
                    brush.CenterColor = Color.FromArgb(220, 65, 65);
                    brush.SurroundColors = new Color[] { Color.FromArgb(130, 20, 20) };
                }
                e.Graphics.FillRectangle(brush, rect);
            }
        }
        Rectangle glassRect = new Rectangle(rect.X, rect.Y, rect.Width, rect.Height / 2);
        using (LinearGradientBrush glassBrush = new LinearGradientBrush(glassRect, Color.FromArgb(80, Color.White), Color.FromArgb(10, Color.White), LinearGradientMode.Vertical))
        {
            e.Graphics.FillRectangle(glassBrush, glassRect);
        }
        using (Pen borderPen = new Pen(Color.Black, 1))
        {
            e.Graphics.DrawRectangle(borderPen, 0, 0, rect.Width - 1, rect.Height - 1);
        }
        TextRenderer.DrawText(e.Graphics, this.Text, this.Font, rect, this.ForeColor, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
    }
}

// =================================================================
// Lines 587 to 714
// Custom Styled UI Controls.
// Contains the definitions for aesthetically enhanced UI elements:
// a TextLabel that draws a readable black border around text, and a
// RoundedProgressBar featuring gradient fills and a glossy reflection.
// =================================================================
public class TextLabel : Label
{
    public TextLabel()
    {
        this.BackColor = Color.Transparent;
        this.ForeColor = Color.White;
        this.Padding = new Padding(3);
    }
    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
        if (string.IsNullOrEmpty(this.Text)) return;
        using (StringFormat stringFormat = new StringFormat())
        {
            stringFormat.FormatFlags = StringFormatFlags.NoClip;
            if (this.TextAlign == ContentAlignment.TopLeft || this.TextAlign == ContentAlignment.MiddleLeft || this.TextAlign == ContentAlignment.BottomLeft)
                stringFormat.Alignment = StringAlignment.Near;
            else if (this.TextAlign == ContentAlignment.TopCenter || this.TextAlign == ContentAlignment.MiddleCenter || this.TextAlign == ContentAlignment.BottomCenter)
                stringFormat.Alignment = StringAlignment.Center;
            else
                stringFormat.Alignment = StringAlignment.Far;
            if (this.TextAlign == ContentAlignment.TopLeft || this.TextAlign == ContentAlignment.TopCenter || this.TextAlign == ContentAlignment.TopRight)
                stringFormat.LineAlignment = StringAlignment.Near;
            else if (this.TextAlign == ContentAlignment.MiddleLeft || this.TextAlign == ContentAlignment.MiddleCenter || this.TextAlign == ContentAlignment.MiddleRight)
                stringFormat.LineAlignment = StringAlignment.Center;
            else
                stringFormat.LineAlignment = StringAlignment.Far;
            Rectangle rectangle = new Rectangle(3, 3, this.ClientSize.Width - 6, this.ClientSize.Height - 6);
            using (SolidBrush borderBrush = new SolidBrush(Color.Black))
            using (SolidBrush centerBrush = new SolidBrush(Color.White))
            {
                int borderThickness = 1;
                e.Graphics.DrawString(this.Text, this.Font, borderBrush, new Rectangle(rectangle.X - borderThickness, rectangle.Y, rectangle.Width, rectangle.Height), stringFormat);
                e.Graphics.DrawString(this.Text, this.Font, borderBrush, new Rectangle(rectangle.X + borderThickness, rectangle.Y, rectangle.Width, rectangle.Height), stringFormat);
                e.Graphics.DrawString(this.Text, this.Font, borderBrush, new Rectangle(rectangle.X, rectangle.Y - borderThickness, rectangle.Width, rectangle.Height), stringFormat);
                e.Graphics.DrawString(this.Text, this.Font, borderBrush, new Rectangle(rectangle.X, rectangle.Y + borderThickness, rectangle.Width, rectangle.Height), stringFormat);
                e.Graphics.DrawString(this.Text, this.Font, borderBrush, new Rectangle(rectangle.X - borderThickness, rectangle.Y - borderThickness, rectangle.Width, rectangle.Height), stringFormat);
                e.Graphics.DrawString(this.Text, this.Font, borderBrush, new Rectangle(rectangle.X + borderThickness, rectangle.Y - borderThickness, rectangle.Width, rectangle.Height), stringFormat);
                e.Graphics.DrawString(this.Text, this.Font, borderBrush, new Rectangle(rectangle.X - borderThickness, rectangle.Y + borderThickness, rectangle.Width, rectangle.Height), stringFormat);
                e.Graphics.DrawString(this.Text, this.Font, borderBrush, new Rectangle(rectangle.X + borderThickness, rectangle.Y + borderThickness, rectangle.Width, rectangle.Height), stringFormat);
                e.Graphics.DrawString(this.Text, this.Font, centerBrush, rectangle, stringFormat);
            }
        }
    }
}

public class RoundedProgressBar : Control
{
    private int value = 0;
    [System.ComponentModel.Browsable(true)]
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Visible)]
    public int Value
    {
        get { return value; }
        set
        {
            this.value = value;
            if (this.value < 0) this.value = 0;
            if (this.value > 100) this.value = 100;
            this.Invalidate();
        }
    }
    [System.ComponentModel.Browsable(true)]
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Visible)]
    public Color FillColor { get; set; } = Color.Gold;
    public RoundedProgressBar()
    {
        this.SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.SupportsTransparentBackColor, true);
        this.BackColor = Color.Transparent;
    }
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        e.Graphics.SmoothingMode = SmoothingMode.HighQuality;
        e.Graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
        float borderThickness = 2f;
        int diameter = this.Height - (int)borderThickness - 1;
        Rectangle backgroundRect = new Rectangle(
            (int)(borderThickness / 2),
            (int)(borderThickness / 2),
            this.Width - (int)borderThickness - 1,
            this.Height - (int)borderThickness - 1
        );
        using (GraphicsPath backgroundPath = CreateRoundedPath(backgroundRect, diameter))
        using (LinearGradientBrush backgroundBrush = new LinearGradientBrush(backgroundRect, Color.FromArgb(180, 15, 15, 15), Color.FromArgb(140, 45, 45, 45), LinearGradientMode.Vertical))
        {
            e.Graphics.FillPath(backgroundBrush, backgroundPath);
        }
        if (value > 0)
        {
            int fillWidth = (int)((value / 100.0) * backgroundRect.Width);
            if (fillWidth < diameter) fillWidth = diameter;
            Rectangle fillRect = new Rectangle(backgroundRect.X, backgroundRect.Y, fillWidth, backgroundRect.Height);
            using (GraphicsPath fillPath = CreateRoundedPath(fillRect, diameter))
            {
                e.Graphics.SetClip(fillPath);
                Color lightColor = ControlPaint.Light(FillColor, 0.4f);
                Color shadowColor = ControlPaint.Dark(FillColor, 0.2f);
                using (LinearGradientBrush bodyBrush = new LinearGradientBrush(fillRect, lightColor, shadowColor, LinearGradientMode.Vertical))
                {
                    e.Graphics.FillPath(bodyBrush, fillPath);
                }
                Rectangle glassRect = new Rectangle(fillRect.X, fillRect.Y, fillWidth, fillRect.Height / 2);
                using (LinearGradientBrush glassBrush = new LinearGradientBrush(glassRect, Color.FromArgb(130, Color.White), Color.FromArgb(20, Color.White), LinearGradientMode.Vertical))
                {
                    e.Graphics.FillRectangle(glassBrush, glassRect);
                }
            }
            e.Graphics.ResetClip();
        }
        using (GraphicsPath borderPath = CreateRoundedPath(backgroundRect, diameter))
        using (Pen borderPen = new Pen(Color.Black, borderThickness))
        {
            borderPen.LineJoin = LineJoin.Round;
            e.Graphics.DrawPath(borderPen, borderPath);
        }
    }
    private GraphicsPath CreateRoundedPath(Rectangle rectangle, float diameter)
    {
        GraphicsPath path = new GraphicsPath();
        path.AddArc(rectangle.X, rectangle.Y, diameter, diameter, 180, 90);
        path.AddArc(rectangle.Right - diameter, rectangle.Y, diameter, diameter, 270, 90);
        path.AddArc(rectangle.Right - diameter, rectangle.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(rectangle.X, rectangle.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        return path;
    }
}

// =================================================================
// Lines 722 to 771
// Custom alert dialog window used to display errors or
// success messages. Dynamically resizes itself to a compact
// layout when no animated image is provided.
// =================================================================
public class CustomAlertWindow : Form
{
    public CustomAlertWindow(string title, string message, Image? gifToShow)
    {
        this.Text = title;
        this.FormBorderStyle = FormBorderStyle.FixedDialog;
        this.MaximizeBox = false;
        this.MinimizeBox = false;
        this.StartPosition = FormStartPosition.CenterParent;
        this.BackColor = Color.White;

        Label messageLabel = new Label();
        messageLabel.Text = message;
        messageLabel.Font = new Font("Segoe UI", 11, FontStyle.Regular);
        messageLabel.TextAlign = ContentAlignment.MiddleCenter;
        messageLabel.Dock = DockStyle.Top;
        messageLabel.Height = 85;
        messageLabel.Padding = new Padding(10, 25, 10, 0);

        Button closeButton = new Button();
        closeButton.Text = "OK";
        closeButton.Font = new Font("Segoe UI", 10, FontStyle.Regular);
        closeButton.Height = 45;
        closeButton.Dock = DockStyle.Bottom;
        closeButton.Cursor = Cursors.Hand;
        closeButton.DialogResult = DialogResult.OK;

        this.Controls.Add(messageLabel);
        this.Controls.Add(closeButton);

        if (gifToShow != null)
        {
            this.Size = new Size(380, 355);

            PictureBox chibiPictureBox = new PictureBox();
            chibiPictureBox.Image = gifToShow;
            chibiPictureBox.SizeMode = PictureBoxSizeMode.Zoom;
            chibiPictureBox.BackColor = Color.Transparent;
            chibiPictureBox.Size = new Size(180, 180);
            chibiPictureBox.Location = new Point(92, 85);

            this.Controls.Add(chibiPictureBox);
            chibiPictureBox.BringToFront();
        }
        else
        {
            this.Size = new Size(380, 180);
        }
    }
}