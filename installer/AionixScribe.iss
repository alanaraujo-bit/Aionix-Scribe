; Instalador do Aionix Scribe (Inno Setup 6).
;
; Decisões que NÃO podem mudar depois sem quebrar quem já instalou:
;
;  - AppId fixo: é ele que faz uma instalação nova SUBSTITUIR a anterior em vez de empilhar duas
;    entradas em "Aplicativos instalados". Trocar esse GUID cria um produto novo aos olhos do
;    Windows e deixa a versão antiga órfã na máquina do usuário.
;
;  - Instalação POR USUÁRIO (PrivilegesRequired=lowest, {localappdata}\Programs): é o que permite a
;    atualização automática rodar em silêncio. Sob "Arquivos de Programas" toda atualização
;    dispararia UAC — o usuário veria um pedido de administrador a cada versão, ou a atualização
;    simplesmente falharia. Também mantém funcionando o "Iniciar com o Windows" (HKCU\...\Run),
;    que aponta para o caminho do executável.
;
; A versão vem do .exe compilado, que por sua vez vem de <Version> no .csproj — um número só,
; nunca digitado duas vezes.

#define AppName "Aionix Scribe"
#define AppPublisher "Aionix"
#define AppExe "AionixScribe.exe"
#define AppUrl "https://github.com/alanaraujo-bit/Aionix-Scribe"
#define SourceDir "..\desktop\AionixScribe\bin\Release\net8.0-windows\win-x64\publish"
; Versão de exibição vem de ProductVersion ("0.2.0"); a numérica de 4 partes só alimenta
; VersionInfoVersion, que o Windows exige nesse formato.
#define AppVersion GetStringFileInfo(SourceDir + "\" + AppExe, "ProductVersion")
#define AppVersionNumeric GetVersionNumbersString(SourceDir + "\" + AppExe)

[Setup]
AppId={{CDE0D1BA-F94C-4B5D-A423-A4F2134A6A70}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
VersionInfoVersion={#AppVersionNumeric}
AppPublisher={#AppPublisher}
AppPublisherURL={#AppUrl}
AppSupportURL={#AppUrl}
AppUpdatesURL={#AppUrl}/releases

PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
DefaultDirName={localappdata}\Programs\AionixScribe
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes
; Página de boas-vindas LIGADA de propósito: o Inno 6 a desliga por padrão, e é justamente
; nela (e na final) que a arte de marca aparece — sem isso o instalador abre numa pergunta de
; pasta e não tem identidade nenhuma. Verificado por captura de tela, não por suposição.
DisableWelcomePage=no
; Não perguntamos a pasta: instalação por usuário tem um destino certo e único, e a pergunta
; só adiciona uma tela de atrito para quem só quer usar o programa.
DisableDirPage=yes
UninstallDisplayIcon={app}\{#AppExe}
UninstallDisplayName={#AppName}

OutputDir=..\dist
OutputBaseFilename=AionixScribe-Setup-{#AppVersion}
SetupIconFile=..\desktop\AionixScribe\Assets\AionixScribe.ico
Compression=lzma2/max
SolidCompression=yes
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible

; Fecha o app em execução antes de sobrescrever os arquivos e reabre depois — sem isto, uma
; atualização por cima de uma instância aberta falharia com arquivo bloqueado (o mesmo erro que
; aparece ao recompilar com o app rodando).
CloseApplications=yes
RestartApplications=yes

WizardStyle=modern
WizardSizePercent=100
WizardImageFile=wizard-large.bmp
WizardSmallImageFile=wizard-small.bmp
WizardImageStretch=yes
ShowLanguageDialog=no

[Languages]
Name: "brazilianportuguese"; MessagesFile: "compiler:Languages\BrazilianPortuguese.isl"

[Messages]
brazilianportuguese.WelcomeLabel1=Instalar o [name]
brazilianportuguese.WelcomeLabel2=Ditado por voz inteligente para Windows.%n%nVocê pressiona um atalho, fala, e o texto limpo aparece onde o cursor estiver — em qualquer aplicativo.%n%nSerá instalada a versão [name/ver].
brazilianportuguese.FinishedHeadingLabel=Tudo pronto
brazilianportuguese.FinishedLabelNoIcons=O [name] foi instalado e vai ficar na bandeja do sistema, ao lado do relógio.
brazilianportuguese.FinishedLabel=O [name] foi instalado e vai ficar na bandeja do sistema, ao lado do relógio.%n%nNa primeira execução ele mostra qual atalho ficou ativo na sua máquina.
brazilianportuguese.ClickFinish=Clique em Concluir para encerrar a instalação.

[Tasks]
; Atalho na área de trabalho vem DESMARCADO: o Aionix Scribe vive na bandeja e é acionado por
; atalho de teclado — um ícone na área de trabalho é entulho para o uso real dele.
Name: "desktopicon"; Description: "Criar um atalho na área de trabalho"; GroupDescription: "Atalhos:"; Flags: unchecked
; Já iniciar com o Windows vem MARCADO: um ditado global que só funciona depois de a pessoa
; lembrar de abrir o programa não serve para nada. Reversível em Configurações → Inicialização.
Name: "startupicon"; Description: "Iniciar o Aionix Scribe junto com o Windows"; GroupDescription: "Ao ligar o computador:"

[Files]
Source: "{#SourceDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#AppName}"; Filename: "{app}\{#AppExe}"
Name: "{group}\Desinstalar o {#AppName}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExe}"; Tasks: desktopicon

[Registry]
; A chave de inicialização automática também é escrita pelo próprio app (Configurações →
; Inicialização). Declarada aqui só com uninsdeletevalue para que desinstalar NÃO deixe para trás
; uma entrada apontando para um executável que não existe mais.
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "AionixScribe"; ValueData: """{app}\{#AppExe}"""; Flags: uninsdeletevalue; Tasks: startupicon
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: none; ValueName: "AionixScribe"; Flags: uninsdeletevalue; Tasks: not startupicon

[Run]
Filename: "{app}\{#AppExe}"; Description: "Abrir o {#AppName} agora"; Flags: nowait postinstall skipifsilent

[Code]
// Os dados do usuário (histórico de ditados, atalho, tema, gravações pendentes) vivem em
// %LOCALAPPDATA%\AionixScribe\ e NÃO são apagados junto com o programa. Isso é intencional e
// coerente com o texto de Privacidade dentro do app: aquela pasta é do usuário, não do
// instalador. Apagar só acontece se a pessoa pedir explicitamente na desinstalação.
procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  DataDir: String;
begin
  if CurUninstallStep = usPostUninstall then
  begin
    DataDir := ExpandConstant('{localappdata}\AionixScribe');
    if DirExists(DataDir) then
    begin
      if MsgBox('Apagar também seus dados locais do Aionix Scribe?' + #13#10 + #13#10 +
                'Isso inclui o histórico de ditados, o atalho escolhido, o tema e as gravações pendentes.' + #13#10 +
                'Escolha Não se você pretende reinstalar depois e quer manter tudo.',
                mbConfirmation, MB_YESNO or MB_DEFBUTTON2) = IDYES then
      begin
        DelTree(DataDir, True, True, True);
      end;
    end;
  end;
end;
