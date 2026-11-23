using System.Diagnostics;
using System.Net;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Microsoft.Win32;
using System;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Windows.Forms;

internal static class Setup
{
    const string Product = "LeFiM LCD Control";

    const string PawnUrl = "https://github.com/namazso/PawnIO.Setup/releases/download/2.2.0/PawnIO_setup.exe";
    const string PawnHash = "1F519A22E47187F70A1379A48CA604981C4FCF694F4E65B734AAA74A9FBA3032";

    [STAThread]
    static void Main()
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.Run(CreateWindow());
    }

    static Form CreateWindow()
    {
        var form = new Form {
            Text = "Instalar " + Product, ClientSize = new Size(510, 218),
            FormBorderStyle = FormBorderStyle.FixedDialog, MaximizeBox = false,
            StartPosition = FormStartPosition.CenterScreen,
            Icon = Icon.ExtractAssociatedIcon(Assembly.GetExecutingAssembly().Location)
        };
        var label = new Label { AutoSize = true, Left = 16, Top = 16, Text = "Escolha a pasta de instalação" };
        var directory = new TextBox {
            Left = 16, Top = 40, Width = 385,
            Text = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", Product)
        };
        var browse = new Button { Left = 409, Top = 38, Width = 85, Text = "Procurar…" };
        browse.Click += (sender, args) => {
            using (var picker = new FolderBrowserDialog { Description = label.Text, SelectedPath = directory.Text })
            {
                if (picker.ShowDialog(form) == DialogResult.OK) directory.Text = picker.SelectedPath;
            }
        };
        var note = new Label { AutoSize = true, Left = 16, Top = 73,
            Text = "Para atualizar, encerre o LeFiM pelo menu Arquivo → Sair." };
        bool pawnInstalled = PawnIsInstalled();
        var pawn = new CheckBox { AutoSize = true, Left = 16, Top = 102, Enabled = !pawnInstalled, Name = "PawnIO",
            Text = pawnInstalled ? "PawnIO já instalado" : "Instalar PawnIO para leitura de sensores (opcional)" };
        string libre = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "Downloads", "LibreHardwareMonitor", "LibreHardwareMonitorLib.dll");
        var sensorNote = new Label { AutoSize = true, Left = 16, Top = 128,
            Text = File.Exists(libre) ? "Libre Hardware Monitor encontrado em Downloads."
                : "Para sensores compatíveis, extraia o Libre Hardware Monitor em Downloads." };
        var status = new Label { AutoSize = true, Left = 16, Top = 171 };
        var install = new Button { Left = 409, Top = 166, Width = 85, Text = "Instalar" };
        bool installingPawn = false;
        form.FormClosing += (sender, args) => { if (installingPawn) args.Cancel = true; };
        form.Controls.AddRange(new Control[] { label, directory, browse, note, pawn, sensorNote, status, install });
        form.AcceptButton = install;
        install.Click += async (sender, args) => {
            string target;
            try { target = Install(directory.Text); }
            catch (Exception error)
            {
                MessageBox.Show(form, error.Message, "Falha na instalação", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            if (pawn.Checked && !PawnIsInstalled())
            {
                installingPawn = true;
                install.Enabled = browse.Enabled = directory.Enabled = pawn.Enabled = false;
                status.Text = "Baixando e verificando PawnIO…";
                try
                {
                    int code = await Task.Run(() => InstallPawnIO());
                    if (code == 3010 || code == 1641)
                        MessageBox.Show(form, "O PawnIO solicitou uma reinicialização do Windows.", Product,
                            MessageBoxButtons.OK, MessageBoxIcon.Information);
                    else if (code != 0 || !PawnIsInstalled())
                        MessageBox.Show(form, "O LeFiM foi instalado. A instalação do PawnIO não foi confirmada (código " +
                            code + ").", Product, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
                catch (Exception error)
                {
                    MessageBox.Show(form, "O LeFiM foi instalado. Etapa PawnIO não concluída.\r\n" + error.Message,
                        Product, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
                finally
                {
                    installingPawn = false;
                    status.Text = "";
                }
            }
            var choice = MessageBox.Show(form, "Instalação concluída. Deseja criar um atalho na Área de Trabalho?",
                Product, MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (choice == DialogResult.Yes)
            {
                try { CreateShortcut(target); }
                catch (Exception error)
                {
                    MessageBox.Show(form, "O aplicativo foi instalado. Falha ao criar o atalho.\r\n" + error.Message,
                        Product, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
            form.Close();
        };
        return form;
    }

    static bool PawnIsInstalled()
    {
        try
        {
            using (var service = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Services\PawnIO"))
                return service != null;
        }
        catch (System.Security.SecurityException) { return false; }
        catch (UnauthorizedAccessException) { return false; }
    }

    sealed class PawnDownload : WebClient
    {
        protected override WebRequest GetWebRequest(Uri address)
        {
            var request = base.GetWebRequest(address);
            request.Timeout = 30000;
            if (request is HttpWebRequest) ((HttpWebRequest)request).ReadWriteTimeout = 30000;
            return request;
        }
    }

    static bool MatchesPawnHash(Stream input)
    {
        using (var sha = SHA256.Create())
            return string.Equals(BitConverter.ToString(sha.ComputeHash(input)).Replace("-", ""),
                PawnHash, StringComparison.Ordinal);
    }

    static int InstallPawnIO()
    {
        string folder = Path.Combine(Path.GetTempPath(), "LeFiM-PawnIO-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        string path = Path.Combine(folder, "PawnIO_setup.exe");
        try
        {
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
            using (var download = new PawnDownload())
                download.DownloadFile(new Uri(PawnUrl), path);
            using (var input = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                if (!MatchesPawnHash(input))
                    throw new InvalidDataException("O arquivo difere da versão verificada do PawnIO.");
                using (var installer = Process.Start(new ProcessStartInfo(path) { UseShellExecute = true, Verb = "runas" }))
                {
                    if (installer == null) throw new InvalidOperationException("Não foi possível abrir o instalador oficial.");
                    installer.WaitForExit();
                    return installer.ExitCode;
                }
            }
        }
        finally
        {
            try
            {
                if (File.Exists(path)) File.Delete(path);
                Directory.Delete(folder);
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }

    static string Install(string directory)
    {
        if (string.IsNullOrWhiteSpace(directory)) throw new ArgumentException("Escolha uma pasta de instalação.");
        directory = Path.GetFullPath(directory.Trim());
        Directory.CreateDirectory(directory);
        string target = Path.Combine(directory, "LeFiM-LCD-Control.exe");
        string temporary = Path.Combine(directory, "." + Guid.NewGuid().ToString("N") + ".tmp");
        try
        {
            using (var input = Assembly.GetExecutingAssembly().GetManifestResourceStream("LeFiM.Lcd.Control.Payload"))
            using (var output = File.Open(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                input.CopyTo(output);
            if (File.Exists(target)) File.Replace(temporary, target, null);
            else File.Move(temporary, target);
            return target;
        }
        finally
        {
            if (File.Exists(temporary)) File.Delete(temporary);
        }
    }

    static void CreateShortcut(string target)
    {
        string desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
        dynamic shell = Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell"));
        dynamic shortcut = shell.CreateShortcut(Path.Combine(desktop, Product + ".lnk"));
        shortcut.TargetPath = target;
        shortcut.WorkingDirectory = Path.GetDirectoryName(target);
        shortcut.IconLocation = target + ",0";
        shortcut.Save();
    }
}
