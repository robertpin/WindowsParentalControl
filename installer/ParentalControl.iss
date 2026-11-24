; ParentalControl Inno Setup Installer
; Requires Inno Setup 6.2+ (https://jrsoftware.org/isdownload.php)

#define MyAppName      "Parental Control"
#define MyAppVersion   "1.0.0"
#define MyAppPublisher "ParentalControl"
#define MyAppExeName   "ParentalControl.Admin.exe"
#define ServiceExeName "ParentalControl.Service.exe"
#define ServiceName    "ParentalControl.Service"

[Setup]
AppId={{A1B2C3D4-E5F6-7890-ABCD-EF1234567890}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={commonpf}\ParentalControl
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
PrivilegesRequired=admin
OutputDir=Output
OutputBaseFilename=ParentalControlSetup
Compression=lzma2
SolidCompression=yes
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
UninstallDisplayName={#MyAppName}
UninstallDisplayIcon={app}\admin\{#MyAppExeName}
SetupIconFile=..\icon.ico
CloseApplications=force

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Files]
; Service files
Source: "..\publish\service\*"; DestDir: "{app}\service"; Flags: ignoreversion recursesubdirs createallsubdirs

; Admin app files
Source: "..\publish\admin\*"; DestDir: "{app}\admin"; Flags: ignoreversion recursesubdirs createallsubdirs

[InstallDelete]
; Older versions were self-contained (the .NET runtime copied into each folder, ~250 MB); start clean so
; no stale runtime files are left next to the framework-dependent build. The service is stopped by then.
Type: filesandordirs; Name: "{app}\service"
Type: filesandordirs; Name: "{app}\admin"

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\admin\{#MyAppExeName}"; \
    IconFilename: "{app}\admin\{#MyAppExeName}"; Comment: "Launch Parental Control Admin"
Name: "{commondesktop}\{#MyAppName}"; Filename: "{app}\admin\{#MyAppExeName}"; \
    IconFilename: "{app}\admin\{#MyAppExeName}"; Comment: "Launch Parental Control Admin"

[UninstallRun]
; Stop and delete the service before files are removed
Filename: "sc"; Parameters: "stop {#ServiceName}"; \
    RunOnceId: "StopService"; Flags: runhidden waituntilterminated
Filename: "sc"; Parameters: "delete {#ServiceName}"; \
    RunOnceId: "DeleteService"; Flags: runhidden waituntilterminated

[UninstallDelete]
; Remove desktop shortcut
Type: files; Name: "{commondesktop}\{#MyAppName}.lnk"
; Remove ProgramData folder on uninstall (logs, database)
Type: filesandordirs; Name: "C:\ProgramData\ParentalControl"
; Remove entire install directory (Program Files\ParentalControl)
Type: filesandordirs; Name: "{app}"

[Code]
// The apps are framework-dependent: they need the .NET 8 Desktop Runtime (it includes the base .NET runtime
// the service uses). If it is missing, the official installer is downloaded from Microsoft and run silently.
const
    DotNetRuntimeUrl = 'https://aka.ms/dotnet/8.0/windowsdesktop-runtime-win-x64.exe';
    DotNetRuntimeFile = 'windowsdesktop-runtime-8-win-x64.exe';

var
    DownloadPage: TDownloadWizardPage;

function IsDotNetDesktop8Installed(): Boolean;
var
    FindRec: TFindRec;
begin
    Result := FindFirst(ExpandConstant('{commonpf64}\dotnet\shared\Microsoft.WindowsDesktop.App\8.*'), FindRec);
    if Result then
        FindClose(FindRec);
end;

procedure InitializeWizard();
begin
    DownloadPage := CreateDownloadPage(SetupMessage(msgWizardPreparing), SetupMessage(msgPreparingDesc), nil);
end;

// Download the runtime (if needed) when the user clicks Install, with a progress page
function NextButtonClick(CurPageID: Integer): Boolean;
begin
    Result := True;
    if (CurPageID <> wpReady) or IsDotNetDesktop8Installed() then
        Exit;

    DownloadPage.Clear;
    DownloadPage.Add(DotNetRuntimeUrl, DotNetRuntimeFile, '');
    DownloadPage.Show;
    try
        try
            DownloadPage.Download;
        except
            if DownloadPage.AbortedByUser then
                Log('.NET runtime download aborted by the user')
            else
                SuppressibleMsgBox('Could not download the .NET 8 Desktop Runtime: ' + GetExceptionMessage + #13#10#13#10
                    + 'Install it from https://dotnet.microsoft.com/download/dotnet/8.0 and run this setup again.',
                    mbCriticalError, MB_OK, IDOK);
            Result := False;
        end;
    finally
        DownloadPage.Hide;
    end;
end;

procedure InstallDotNetRuntimeIfNeeded();
var
    ResultCode: Integer;
    Installer: String;
begin
    if IsDotNetDesktop8Installed() then
        Exit;

    Installer := ExpandConstant('{tmp}\' + DotNetRuntimeFile);
    if not FileExists(Installer) then
    begin
        // Silent installs skip the wizard pages: download here, without a progress page
        try
            DownloadTemporaryFile(DotNetRuntimeUrl, DotNetRuntimeFile, '', nil);
        except
            MsgBox('Could not download the .NET 8 Desktop Runtime: ' + GetExceptionMessage + #13#10#13#10
                   + 'Install it from https://dotnet.microsoft.com/download/dotnet/8.0, '
                   + 'then start the Parental Control service or reboot.', mbError, MB_OK);
            Exit;
        end;
    end;

    WizardForm.StatusLabel.Caption := 'Installing the .NET 8 Desktop Runtime...';
    // 0 = installed, 3010 = installed, reboot required, 1638 = a newer version is already installed
    if not Exec(Installer, '/install /quiet /norestart', '', SW_HIDE, ewWaitUntilTerminated, ResultCode)
       or ((ResultCode <> 0) and (ResultCode <> 3010) and (ResultCode <> 1638)) then
        MsgBox(Format('The .NET 8 Desktop Runtime could not be installed (code %d). '
               + 'Install it from https://dotnet.microsoft.com/download/dotnet/8.0, '
               + 'then start the Parental Control service or reboot.', [ResultCode]),
               mbError, MB_OK);
end;

function ServiceExists(): Boolean;
var
    ResultCode: Integer;
begin
    Result := Exec('sc', ExpandConstant('query {#ServiceName}'),
                   '', SW_HIDE, ewWaitUntilTerminated, ResultCode)
              and (ResultCode = 0);
end;

procedure StopAndDeleteService();
var
    ResultCode: Integer;
begin
    Exec('sc', ExpandConstant('stop {#ServiceName}'),
         '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
    Sleep(2000);
    Exec('sc', ExpandConstant('delete {#ServiceName}'),
         '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
    Sleep(1000);
end;

procedure CreateService();
var
    ResultCode: Integer;
    BinPath: String;
begin
    BinPath := ExpandConstant('"{app}\service\{#ServiceExeName}"');

    Exec('sc', ExpandConstant('create {#ServiceName}')
         + ' binPath= ' + BinPath
         + ' start= auto'
         + ' obj= LocalSystem'
         + ' DisplayName= "Parental Control Service"',
         '', SW_HIDE, ewWaitUntilTerminated, ResultCode);

    // Set description
    Exec('sc', ExpandConstant('description {#ServiceName}')
         + ' "Monitors and enforces parental control time limits."',
         '', SW_HIDE, ewWaitUntilTerminated, ResultCode);

    // Configure service to restart on failure (10s, 30s, 60s)
    Exec('sc', ExpandConstant('failure {#ServiceName}')
         + ' reset= 86400 actions= restart/10000/restart/30000/restart/60000',
         '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
end;

procedure StartService();
var
    ResultCode: Integer;
    Retries: Integer;
    Started: Boolean;
begin
    Started := False;
    Sleep(2000);  // Let SCM finish registering the service

    for Retries := 1 to 3 do
    begin
        Log(Format('StartService: attempt %d of 3', [Retries]));
        if Exec('sc', ExpandConstant('start {#ServiceName}'),
                '', SW_HIDE, ewWaitUntilTerminated, ResultCode) then
        begin
            if (ResultCode = 0) or (ResultCode = 1056) then  // 1056 = already running
            begin
                Log('StartService: service started successfully');
                Started := True;
                Break;
            end;
            Log(Format('StartService: sc start returned %d', [ResultCode]));
        end
        else
            Log('StartService: Exec call itself failed');

        if Retries < 3 then
            Sleep(1000 + Retries * 1000);  // Escalating: 2s, 3s
    end;

    if not Started then
        MsgBox('The Parental Control service could not be started automatically. '
               + 'It will start after the next reboot, or you can start it manually '
               + 'from the Services console (services.msc).',
               mbInformation, MB_OK);
end;

procedure CurStepChanged(CurStep: TSetupStep);
begin
    if CurStep = ssInstall then
    begin
        // Before files are copied: stop existing service so files are not locked
        if ServiceExists() then
            StopAndDeleteService();
    end
    else if CurStep = ssPostInstall then
    begin
        // After files are copied: make sure the runtime is there, then create and start the service
        InstallDotNetRuntimeIfNeeded();
        CreateService();
        StartService();
    end;
end;
