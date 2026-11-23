using System;
using System.Threading.Tasks;
using System.Windows;
using Forms = System.Windows.Forms;

internal sealed partial class Editor
{
    Forms.NotifyIcon tray;
    Forms.ToolStripItem trayOpen, trayStop, trayExit;
    bool exiting, restoring, savePending;

    void SetupTray()
    {
        var menu = new Forms.ContextMenuStrip();
        trayOpen = menu.Items.Add(Words.Get("Abrir LeFiM LCD Control"), null, (s,e) => OpenWindow());
        trayStop = menu.Items.Add(Words.Get("Parar envio ao LCD"), null, (s,e) => StopSending());
        menu.Opening += (s,e) => trayStop.Enabled = playback != null && !playback.IsCancellationRequested;
        menu.Items.Add(new Forms.ToolStripSeparator());
        trayExit = menu.Items.Add(Words.Get("Sair"), null, async (s,e) => await Exit());
        tray = new Forms.NotifyIcon {
            Text = "LeFiM LCD Control",
            Icon = System.Drawing.Icon.ExtractAssociatedIcon(Exe),
            ContextMenuStrip = menu,
            Visible = true
        };
        tray.MouseDoubleClick += (s,e) => {
            if (e.Button == Forms.MouseButtons.Left) OpenWindow();
        };
    }

    void UpdateTrayLanguage()
    {
        if (trayOpen == null) return;
        trayOpen.Text = Words.Get("Abrir LeFiM LCD Control");
        trayStop.Text = Words.Get("Parar envio ao LCD");
        trayExit.Text = Words.Get("Sair");
    }

    void OpenWindow(bool maximize = false)
    {
        window.Show();
        if (maximize) window.WindowState = WindowState.Maximized;
        else if (window.WindowState == WindowState.Minimized)
            window.WindowState = WindowState.Normal;
        window.Activate();
        ShowFrame();
    }

    void HideWindow()
    {
        timer.Stop();
        window.Hide();
    }

    void Remember()
    {
        if (restoring || checking) return;
        savePending = true;
        try { Save(); }
        catch (Exception ex) { Status(Words.Get("Não foi possível salvar a configuração: ") + ex.Message); }
    }

    async Task Exit()
    {
        if (exiting) return;
        exiting = true;
        if (playback != null) playback.Cancel();
        while (busy || applyingAngle) await Task.Delay(50);
        if (savePending)
        {
            try { Save(); }
            catch (Exception ex)
            {
                OpenWindow();
                if (MessageBox.Show(window, Words.Get("Não foi possível salvar a configuração. Sair mesmo assim?\n") + ex.Message,
                    "LeFiM LCD Control", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
                {
                    exiting = false;
                    return;
                }
            }
        }
        window.Close();
        Application.Current.Shutdown();
    }
}
