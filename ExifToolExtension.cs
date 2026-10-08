using System;
using System.Collections;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using ScriptPortal.Vegas;

namespace ExifToolExtension
{
  public class ExifModule : ICustomCommandModule
  {
    private Vegas vegas;
    private CustomCommand cmd = new CustomCommand(CommandCategory.View, "ExifToolExtension");
    private ExifDockView dock;

    public void InitializeModule(Vegas vegas)
    {
      this.vegas = vegas;
    }

    public ICollection GetCustomCommands()
    {

      cmd.DisplayName = "ExifTool Metadata";
      cmd.MenuItemName = "ExifTool Metadata";
      cmd.Invoked += this.HandleInvoked;
      cmd.MenuPopup += this.HandleMenuPopup;
      cmd.IconFile = Path.Combine(Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location), "ExifToolExtension.png");
      cmd.CanAddToToolbar = true;
      cmd.CanAddToMenu = true;
      return new[] { cmd };
    }

    void HandleInvoked(Object sender, EventArgs args)
    {
      if (!vegas.ActivateDockView("ExifToolExtension"))
      {
        DockableControl dockView = new ExifDockView(vegas);
        vegas.LoadDockView(dockView);
      }
    }

    void HandleMenuPopup(Object sender, EventArgs args)
    {
      cmd.Checked = vegas.FindDockView("ExifToolExtension");
    }
  }


  public class ExifDockView : DockableControl
  {
    private readonly Vegas vegas;
    private readonly ComboBox source_selector = new ScriptPortal.MediaSoftware.Skins.ComboBox
    { Dock = DockStyle.Top, DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly TextBox output = new ScriptPortal.MediaSoftware.Skins.TextBox
    {
      Multiline = true,
      ReadOnly = true,
      Dock = DockStyle.Fill,
      ScrollBars = ScrollBars.Both,
      WordWrap = false,
      Font = new Font("Consolas", 9f)
    };

    private readonly Button about = new ScriptPortal.MediaSoftware.Skins.Button
    { Text = "About", Dock = DockStyle.Right, Width = 60 };

    private readonly Timer poll = new Timer { Interval = 500 }; // delay to avoid excessive refreshes
    private string lastPath;

    public ExifDockView(Vegas vegas) : base("ExifToolExtension")
    {
      this.vegas = vegas;
      DisplayName = "ExifTool Metadata";
      PersistDockWindowState = true;
      AutoScroll = true;
      DefaultFloatingSize = new Size(600, 600);
      SetStyle(ControlStyles.ContainerControl, true);


      source_selector.Items.AddRange(new object[] { "Auto", "Timeline", "Project Media", "None" });
      source_selector.SelectedIndex = 0;

      source_selector.Dock = DockStyle.Fill;
      var topBar = new Panel { Dock = DockStyle.Top, Height = source_selector.PreferredHeight + 4 };
      topBar.Controls.Add(source_selector);   
      topBar.Controls.Add(about);    

      Controls.Add(output);
      Controls.Add(topBar);
      about.Click += (s, e) => ShowAbout();
      poll.Tick += (s, e) => Refresh(false);
      vegas.TrackEventStateChanged += OnVegasChanged;
      vegas.TrackEventCountChanged += OnVegasChanged;
      vegas.TrackSelectionChanged += OnVegasChanged;
      vegas.MediaPoolChanged += OnVegasMediaChanged;
      vegas.MediaSelectionChanged += OnVegasMediaChanged;
      source_selector.SelectedIndexChanged += (s, e) => Refresh(true);
    }

    protected override void OnLoaded(EventArgs args)
    {
      base.OnLoaded(args);
      poll.Start();
    }

    protected override void OnClosed(EventArgs args)
    {
      poll.Stop();
      vegas.TrackEventStateChanged -= OnVegasChanged;
      vegas.TrackEventCountChanged -= OnVegasChanged;
      vegas.MediaPoolChanged -= OnVegasMediaChanged;
      vegas.MediaSelectionChanged -= OnVegasMediaChanged;
      base.OnClosed(args);
    }

    bool mediachanged = false;

    private void OnVegasChanged(object sender, EventArgs e)
    {
      mediachanged = false;
      RequestRefresh(false);
    }

    private void OnVegasMediaChanged(object sender, EventArgs e)
    {
      mediachanged = true;
      RequestRefresh(false);
    }

    // Safe to call from any thread
    private void RequestRefresh(bool force)
    {
      if (IsDisposed || !IsHandleCreated) return;
      if (InvokeRequired)
        BeginInvoke(new Action(() => Refresh(force)));
      else
        Refresh(force);
    }

    // ---------- selection ----------
    private string GetTimelinePath()
    {
      foreach (Track t in vegas.Project.Tracks)
        foreach (TrackEvent ev in t.Events)
          if (ev.Selected && ev.ActiveTake != null)
            return ev.ActiveTake.MediaPath;
      return null;
    }

    private string GetProjectMediaPath()
    {
      Media[] sel = vegas.Project.MediaPool.GetSelectedMedia();
      if (sel == null || sel.Length == 0) return null;
      Media m = sel[0];
      if (m.IsGenerated()) return null;
      return m.FilePath;
    }

    private string GetPath()
    {
      switch (source_selector.SelectedIndex)
      {
        case 1: return GetTimelinePath();
        case 2: return GetProjectMediaPath();
        case 3: return null;
        default: return mediachanged ? GetProjectMediaPath():  GetTimelinePath();
      }
    }

    private bool busy;
    private void Refresh(bool force)   // always runs on the UI thread
    {
      if (busy) return;

      string path;
      try
      {
        path = GetPath(); 
      }
      catch { return; }

      if (!force && path == lastPath) return;
      lastPath = path;

      if (string.IsNullOrEmpty(path)) { output.Text = "No clip selected."; return; }
      if (!File.Exists(path)) { output.Text = "File not found:\r\n" + path; return; }

      busy = true;
      output.Text = "Reading…";

      Task.Run(() => RunExifTool(path)).ContinueWith(t =>
      {
        string result = t.IsFaulted ? "Error: " + t.Exception.GetBaseException().Message
                                    : t.Result;
        if (IsDisposed || !IsHandleCreated) { busy = false; return; }
        BeginInvoke(new Action(() =>
        {
          output.Text = result;
          busy = false;
        }));
      });
    }

    private void ShowAbout()
    {
      string ver = System.Reflection.Assembly.GetExecutingAssembly()
                         .GetName().Version.ToString();
      string exifVer = "not found";
      try
      {
        string dir = Path.GetDirectoryName(
            System.Reflection.Assembly.GetExecutingAssembly().Location);
        var psi = new ProcessStartInfo
        {
          FileName = Path.Combine(dir, "exiftool.exe"),
          Arguments = "-ver",
          UseShellExecute = false,
          RedirectStandardOutput = true,
          CreateNoWindow = true
        };
        using (var p = Process.Start(psi))
        {
          exifVer = p.StandardOutput.ReadToEnd().Trim();
          p.WaitForExit();
        }
      }
      catch { }

      using (var dlg = new Form())
      {
        dlg.Text = "About ExifTool Metadata";
        dlg.FormBorderStyle = FormBorderStyle.FixedDialog;
        dlg.StartPosition = FormStartPosition.CenterParent;
        dlg.MaximizeBox = false; dlg.MinimizeBox = false; dlg.ShowInTaskbar = false;
        dlg.ClientSize = new Size(380, 190);

        var info = new Label
        {
          Left = 16,
          Top = 16,
          Width = 348,
          Height = 90,
          Text = "ExifTool Metadata for VEGAS Pro\r\n" +
                   "Version " + ver + "\r\n\r\n" +
                   "© 2026 Harold Linke. All rights reserved.\r\n\r\n" +
                   "Uses ExifTool " + exifVer + " by Phil Harvey."
        };
        var link = new LinkLabel
        {
          Left = 16,
          Top = 112,
          Width = 348,
          Text = "https://exiftool.org"
        };
        link.LinkClicked += (s, e) =>
        {
          try { Process.Start(new ProcessStartInfo("https://exiftool.org") { UseShellExecute = true }); }
          catch { }
        };
        var ok = new Button
        {
          Text = "OK",
          DialogResult = DialogResult.OK,
          Left = 290,
          Top = 150,
          Width = 74
        };
        dlg.Controls.AddRange(new Control[] { info, link, ok });
        dlg.AcceptButton = ok;
        dlg.ShowDialog(this.ParentWindow ?? this.OwnerWindow);
      }
    }

    private static string RunExifTool(string file)
    {
      try
      {
        string dir = Path.GetDirectoryName(
            System.Reflection.Assembly.GetExecutingAssembly().Location);
        var psi = new ProcessStartInfo
        {
          FileName = Path.Combine(dir, "exiftool.exe"),
          Arguments = "-G1 -a -s -charset filename=utf8 \"" + file + "\"",
          UseShellExecute = false,
          RedirectStandardOutput = true,
          RedirectStandardError = true,
          CreateNoWindow = true,
          StandardOutputEncoding = Encoding.UTF8
        };
        using (var p = Process.Start(psi))
        {
          string o = p.StandardOutput.ReadToEnd();
          string e = p.StandardError.ReadToEnd();
          p.WaitForExit();
          return string.IsNullOrEmpty(e) ? o : o + "\r\n" + e;
        }
      }
      catch (Exception ex) { return "ExifTool error: " + ex.Message; }
    }
  }
}