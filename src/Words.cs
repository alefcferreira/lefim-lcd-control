using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

internal static class Words
{
    public static volatile bool English;
    static readonly Dictionary<string,string> Translations = new Dictionary<string,string>(StringComparer.Ordinal)
    {
        { "\n\nDetalhes técnicos\n", "\n\nTechnical details\n" },
        { "\n\nRegistro de envio\n", "\n\nPlayback log\n" },
        { "\nDesenvolvido por LeFiM Scripts\nAplicativo gratuito para telas USB compatíveis.", "\nDeveloped by LeFiM Scripts\nFree software for compatible USB displays." },
        { "\nEstado: ", "\nStatus: " },
        { "\nMídia: ", "\nMedia: " },
        { "\nUSB esperado: 2A55:9990", "\nExpected USB device: 2A55:9990" },
        { "\nVeja os detalhes em Diagnóstico USB.", "\nSee USB diagnostics for details." },
        { " Nenhuma imagem foi enviada. Feche o controlador antes de recuperar a tela.", " No image was sent. Exit the controller before recovering the display." },
        { " ao LCD.", " to the LCD." },
        { " bytes. Cabeçalho: ", " bytes. Header: " },
        { " bytes. Status: ", " bytes. Status: " },
        { " quadros", " frames" },
        { " — erro de leitura WinUSB ", " — WinUSB read error " },
        { " — o painel devolveu o status sem informar a resolução.", " — the display returned status without reporting its resolution." },
        { " — resposta completa não encontrada. Dados: ", " — complete response missing. Data: " },
        { " — tamanho inesperado. Último pacote: ", " — unexpected length. Last packet: " },
        { " • Porta: ", " • Port: " },
        { " • falha de descritor (43)", " • descriptor failure (43)" },
        { "A consulta de tamanho foi enviada, mas a resposta do LCD não chegou a tempo.", "The LCD did not answer the resolution query in time." },
        { "A imagem excede o limite de 25 megapixels.", "The image exceeds 25 megapixels." },
        { "A interface WinUSB não pôde ser aberta.", "Could not open the WinUSB interface." },
        { "A porta do LCD não pôde ser identificada com segurança.", "Could not reliably identify the LCD port." },
        { "A transmissão foi interrompida. O visor pode continuar mostrando o último quadro recebido.", "Playback was interrupted. The LCD may still show the last frame it received." },
        { "Abrir LeFiM LCD Control", "Open LeFiM LCD Control" },
        { "Aguardando o LCD para reproduzir a configuração salva…", "Waiting for the LCD to play the saved media…" },
        { "Ainda não há resultado de envio nesta sessão.", "No playback results in this session." },
        { "Aplicar tema ", "Apply theme " },
        { "Arquivo salvo indisponível: ", "Saved file unavailable: " },
        { "Arraste a mídia na prévia para enquadrar.", "Drag media in the preview to frame it." },
        { "Arraste para girar. Use a rodinha ou as setas para ajustar 1°.", "Drag to rotate. Use the wheel or arrow keys for 1° steps." },
        { "Arraste para posicionar. Use a rodinha para redimensionar.", "Drag to position. Scroll over a reading to resize." },
        { "Arraste uma imagem ou um GIF por vez.", "Drop one image or GIF at a time." },
        { "Atalho criado na área de trabalho.", "Desktop shortcut created." },
        { "Aumentar rotação em um grau", "Rotate one degree clockwise" },
        { "Aumentar texto", "Increase text size" },
        { "CPU sem leitura. Confira o sensor do processador no Libre Hardware Monitor e as permissões do Windows.", "CPU temperature unavailable. Check sensor access in Libre Hardware Monitor." },
        { "Cabeçalho do quadro", "Frame header" },
        { "Cancelar endpoint 0x{0:X2}: {1}", "Abort endpoint 0x{0:X2}: {1}" },
        { "Comando de resolução", "Resolution command" },
        { "Conectando e conferindo a resolução do LCD…", "Connecting to the LCD…" },
        { "Configuração salva.", "Settings saved." },
        { "Configuração salva. Reprodução automática ativada.", "Settings saved. Autoplay enabled." },
        { "Consulta de resolução", "Resolution query" },
        { "Consultando a interface WinUSB.", "Checking WinUSB…" },
        { "Copiar diagnóstico", "Copy diagnostics" },
        { "Cor…", "Color…" },
        { "Criar atalho na área de trabalho", "Create desktop shortcut" },
        { "Dados atualizados a cada segundo. — indica uma leitura indisponível.", "— indicates an unavailable reading." },
        { "Descritor de configuração: ", "Configuration descriptor: " },
        { "Descritor de dispositivo: ", "Device descriptor: " },
        { "Diagnóstico USB", "USB diagnostics" },
        { "Diagnóstico USB · ", "USB diagnostics · " },
        { "Diagnóstico copiado", "Diagnostics copied" },
        { "Diminuir rotação em um grau", "Rotate one degree counterclockwise" },
        { "Diminuir texto", "Decrease text size" },
        { "Eco do comando ignorado (", "Ignored command echo (" },
        { "Encaixar", "Fit" },
        { "Encerrando envio", "Stopping playback" },
        { "Endpoint 0x{0:X2} {1} • {2} • pacote de até {3} bytes\r\n", "Endpoint 0x{0:X2} {1} • {2} • max packet size {3} bytes\r\n" },
        { "Enquadramento", "Media and framing" },
        { "Enviando", "Sending" },
        { "Enviando ", "Sending " },
        { "Enviar ao LCD", "Play on LCD" },
        { "Envio do JPEG", "JPEG transfer" },
        { "Envio em andamento.", "Playback in progress." },
        { "Envio encerrado", "Playback stopped" },
        { "Envio encerrado antes da confirmação do primeiro quadro.", "Playback stopped before the first frame was acknowledged." },
        { "Envio encerrado.", "Playback stopped." },
        { "Exibindo", "Displaying" },
        { "Exportado: ", "Exported: " },
        { "Exportando…", "Exporting…" },
        { "Exportar em 480 × 480", "Export at 480 × 480" },
        { "Fabricante: ", "Manufacturer: " },
        { "Falha ao exportar: ", "Export failed: " },
        { "Falha ao importar: ", "Import failed: " },
        { "Falha ao restaurar configuração: ", "Could not restore settings: " },
        { "Falha no diagnóstico: ", "Diagnostics failed: " },
        { "Falha no envio", "Playback failed" },
        { "Falha no envio: ", "Playback failed: " },
        { "Feche o outro aplicativo do LCD para liberar o USB.", "Close the other LCD app to release the USB interface." },
        { "Feche o outro aplicativo do LCD pela bandeja para liberar o USB.", "Exit the other LCD app in the system tray to release the USB interface." },
        { "GIF animado|*.gif|PNG do primeiro quadro|*.png", "Animated GIF|*.gif|First frame PNG|*.png" },
        { "GIF incompleto.", "Incomplete GIF." },
        { "GIF inválido ou com mais de 300 quadros.", "Invalid GIF or more than 300 frames." },
        { "GIF inválido.", "Invalid GIF." },
        { "GIF sem paleta.", "GIF has no color palette." },
        { "GIF sem quadros.", "GIF has no frames." },
        { "Hub: ", "Hub: " },
        { "Imagem PNG|*.png", "PNG image|*.png" },
        { "Imagem aceita pelo LCD.", "Image accepted by the LCD." },
        { "Imagens e GIFs", "Images and GIFs" },
        { "Imagens e GIFs|*.png;*.jpg;*.jpeg;*.gif;*.bmp;*.tif;*.tiff|Todos os arquivos|*.*", "Images and GIFs|*.png;*.jpg;*.jpeg;*.gif;*.bmp;*.tif;*.tiff|All files|*.*" },
        { "Importar imagem ou GIF", "Import image or GIF" },
        { "Informações", "Telemetry" },
        { "Inicialização automática desativada.", "Autostart disabled." },
        { "Iniciar com o Windows e reproduzir", "Start with Windows and autoplay" },
        { "Interface aberta: ", "Interface opened: " },
        { "Interface {0}, alternativa {1} • classe {2:X2}/{3:X2}/{4:X2}\r\n", "Interface {0}, alternate {1} • class {2:X2}/{3:X2}/{4:X2}\r\n" },
        { "LCD detectada", "LCD detected" },
        { "LCD indisponível", "LCD unavailable" },
        { "LCD indisponível. Clique em Enviar ao LCD quando ele estiver conectado.", "LCD unavailable. Select Play on LCD after connecting it." },
        { "Limpar buffer de recepção: ", "Flush receive buffer: " },
        { "Mídia inicial: ", "Initial media: " },
        { "Não foi possível salvar a configuração. Sair mesmo assim?\n", "Could not save settings. Exit anyway?\n" },
        { "Não foi possível salvar a configuração: ", "Could not save settings: " },
        { "O LCD confirmou o recebimento de quadros. A exibição foi encerrada.", "The LCD acknowledged frames. Playback stopped." },
        { "O LCD confirmou o recebimento do quadro.", "LCD playback started." },
        { "O LCD informou {0} × {1}. O editor está configurado em 480 × 480.", "The LCD reported {0} × {1}. The editor is set to 480 × 480." },
        { "O Windows detectou o LCD, mas o envio da consulta de tamanho expirou.", "Windows detected the LCD, but sending the resolution query timed out." },
        { "O Windows não informou a interface do hub do LCD.", "Windows did not report the LCD hub interface." },
        { "O arquivo excede 48 MB.", "The file exceeds 48 MB." },
        { "O dispositivo da porta mudou ou não corresponde ao LCD.", "The device on this port changed or does not match the LCD." },
        { "O hub não confirmou o ciclo da porta USB.", "The hub did not acknowledge the USB port power cycle." },
        { "Ocultar na bandeja", "Hide to tray" },
        { "Os descritores não correspondem ao perfil SmartCool deste equipamento.", "USB descriptors do not match this SmartCool display." },
        { "Outra janela do LeFiM LCD Control está usando a tela.", "Another LeFiM LCD Control window is using the display." },
        { "Pacote IN após falha de escrita: ", "IN packet after write failure: " },
        { "Pacote IN: ", "IN packet: " },
        { "Parar envio ao LCD", "Stop LCD playback" },
        { "Parar exibição no LCD", "Stop LCD playback" },
        { "Pausar", "Pause" },
        { "Personalização do LCD", "Display customization" },
        { "Preencher", "Fill" },
        { "Preparando mídia em 480 × 480…", "Preparing 480 × 480 media…" },
        { "Primeiro JPEG: ", "First JPEG: " },
        { "Primeiro quadro: JPEG de ", "First frame: JPEG, " },
        { "Produto: ", "Product: " },
        { "Pronto.", "Ready." },
        { "Protocolo identificado: SmartCool, JPEG em transferências bulk.", "Protocol: SmartCool, JPEG over bulk transfers." },
        { "Prévia circular", "Circular preview" },
        { "Prévia do LCD", "Display preview" },
        { "Prévia pronta para exportar.", "Preview ready." },
        { "Prévia quadrada", "Square preview" },
        { "Quadro fora das dimensões do GIF.", "Frame is outside the GIF dimensions." },
        { "Recuperar endpoint 0x{0:X2}: {1}", "Reset endpoint 0x{0:X2}: {1}" },
        { "Relógio", "Clock" },
        { "Reproduzindo", "Playing" },
        { "Reproduzir", "Play" },
        { "Resposta de resolução: ", "Resolution response: " },
        { "Rotação da ventoinha", "Fan speed" },
        { "Rotação em graus", "Rotation in degrees" },
        { "Sair", "Exit" },
        { "Salvar configuração atual", "Save settings" },
        { "Saída do LCD", "LCD output" },
        { "Selecione o texto e pressione Ctrl+C", "Select the text and press Ctrl+C" },
        { "Sem resposta do LCD", "LCD not responding" },
        { "Sobre o LeFiM LCD Control", "About LeFiM LCD Control" },
        { "Status após falha de escrita", "Status after write failure" },
        { "Status da resolução: ", "Resolution status: " },
        { "Status do quadro JPEG", "JPEG frame status" },
        { "Série: ", "Serial: " },
        { "Tag do quadro com falha: ", "Failed frame tag: " },
        { "Telemetria", "Display metrics" },
        { "Temas", "Theme presets" },
        { "Temperatura da CPU", "CPU temperature" },
        { "Temperatura da GPU", "GPU temperature" },
        { "Tempo de envio: {0:F1} s. Quadros confirmados: {1}. Último JPEG preparado: {2} bytes. Próxima tag: {3}.", "Playback time: {0:F1} s. Acknowledged frames: {1}. Last encoded JPEG: {2} bytes. Next tag: {3}." },
        { "Tempo limite WinUSB: ", "WinUSB timeout: " },
        { "Troca {0} na conexão aberta após {1} quadros confirmados. Próxima tag: {2}.", "Switch {0} on open connection after {1} acknowledged frames. Next tag: {2}." },
        { "Trocar imagem ou GIF no LCD", "Replace LCD media" },
        { "Uso de RAM", "RAM usage" },
        { "Utilização da CPU", "CPU utilization" },
        { "Utilização da GPU", "GPU utilization" },
        { "VID:PID {0:X4}:{1:X4} • revisão {2:X4} • USB {3:X4}\r\n", "VID:PID {0:X4}:{1:X4} • revision {2:X4} • USB {3:X4}\r\n" },
        { "VRAM em uso", "VRAM usage" },
        { "Verificando LCD", "Checking LCD" },
        { "_Ajuda", "_Help" },
        { "_Arquivo", "_File" },
        { "_Idioma", "_Language" },
        { "_Opções", "_Settings" },
        { "indisponível", "unavailable" },
        { "{0}\n{1} × {2} original · {3}{4}", "{0}\n{1} × {2} source · {3}{4}" },
        { "{0} — LCD retornou código 0x{1:X2}. Dados: {2}", "{0} — LCD returned code 0x{1:X2}. Data: {2}" },
        { "{0} — enviados {1} de {2} bytes.", "{0} — sent {1} of {2} bytes." },
        { "{0} — erro de escrita WinUSB {1} ao enviar {2} bytes: {3}", "{0} — WinUSB write error {1} sending {2} bytes: {3}" },
        { "{0} — status USB com {1} bytes, esperados 13. Dados: {2}", "{0} — USB status has {1} bytes, expected 13. Data: {2}" },
        { "{0} — tag USB {1} recebida, esperada {2}. Dados: {3}", "{0} — received USB tag {1}, expected {2}. Data: {3}" },
        { "Órbita", "Orbit" },
    };
    static readonly Dictionary<string,string> Portuguese = Translations.GroupBy(pair => pair.Value)
        .ToDictionary(group => group.Key, group => group.First().Key, StringComparer.Ordinal);

    public static string Get(string portuguese)
    {
        string translated;
        return English && Translations.TryGetValue(portuguese, out translated) ? translated : portuguese;
    }

    public static string InLanguage(string text)
    {
        if (text == null) return null;
        if (English) return Get(text);
        string original;
        return Portuguese.TryGetValue(text, out original) ? original : text;
    }

    public static void Apply(DependencyObject root)
    {
        var text = root as TextBlock;
        if (text != null) text.Text = InLanguage(text.Text);
        var header = root as HeaderedItemsControl;
        if (header != null && header.Header is string) header.Header = InLanguage((string)header.Header);
        var tab = root as TabItem;
        if (tab != null && tab.Header is string) tab.Header = InLanguage((string)tab.Header);
        var content = root as ContentControl;
        if (content != null && content.Content is string) content.Content = InLanguage((string)content.Content);
        var element = root as FrameworkElement;
        if (element != null && element.ToolTip is string) element.ToolTip = InLanguage((string)element.ToolTip);
        var name = System.Windows.Automation.AutomationProperties.GetName(root);
        if (name.Length != 0) System.Windows.Automation.AutomationProperties.SetName(root, InLanguage(name));
        foreach (var child in LogicalTreeHelper.GetChildren(root).OfType<DependencyObject>().ToArray()) Apply(child);
    }
}
