Unicode True
!include "MUI2.nsh"

!define APP_NAME "Your Cat Desktop Pet"
!define APP_VERSION "0.1.0"
!define APP_EXE "YourCatDesktopPet.exe"
!define APP_PUBLISHER "Your Cat"
!define APP_REG_KEY "Software\Microsoft\Windows\CurrentVersion\Uninstall\YourCatDesktopPet"
!define SOURCE_DIR "..\unity-client\Build\DesktopPetV13"

Name "${APP_NAME}"
OutFile "..\dist\YourCatDesktopPet-Setup-${APP_VERSION}.exe"
InstallDir "$LOCALAPPDATA\Programs\YourCatDesktopPet"
InstallDirRegKey HKCU "${APP_REG_KEY}" "InstallLocation"
RequestExecutionLevel user
SetCompressor /SOLID lzma
ShowInstDetails show
ShowUninstDetails show
VIProductVersion "0.1.0.0"
VIAddVersionKey "ProductName" "${APP_NAME}"
VIAddVersionKey "ProductVersion" "${APP_VERSION}"
VIAddVersionKey "FileVersion" "${APP_VERSION}"
VIAddVersionKey "CompanyName" "${APP_PUBLISHER}"
VIAddVersionKey "FileDescription" "${APP_NAME} Installer"
VIAddVersionKey "LegalCopyright" "Copyright 2026 Your Cat"

!define MUI_ABORTWARNING
!define MUI_FINISHPAGE_RUN "$INSTDIR\${APP_EXE}"
!insertmacro MUI_PAGE_WELCOME
!insertmacro MUI_PAGE_DIRECTORY
!insertmacro MUI_PAGE_INSTFILES
!insertmacro MUI_PAGE_FINISH
!insertmacro MUI_UNPAGE_CONFIRM
!insertmacro MUI_UNPAGE_INSTFILES
!insertmacro MUI_LANGUAGE "SimpChinese"
!insertmacro MUI_LANGUAGE "English"

Section "Desktop Pet" MainSection
  SectionIn RO
  SetOutPath "$INSTDIR"
  File /r "${SOURCE_DIR}\*"

  WriteUninstaller "$INSTDIR\Uninstall.exe"
  CreateDirectory "$SMPROGRAMS\Your Cat Desktop Pet"
  CreateShortcut "$SMPROGRAMS\Your Cat Desktop Pet\Your Cat Desktop Pet.lnk" "$INSTDIR\${APP_EXE}"
  CreateShortcut "$SMPROGRAMS\Your Cat Desktop Pet\Uninstall.lnk" "$INSTDIR\Uninstall.exe"

  WriteRegStr HKCU "${APP_REG_KEY}" "DisplayName" "${APP_NAME}"
  WriteRegStr HKCU "${APP_REG_KEY}" "DisplayVersion" "${APP_VERSION}"
  WriteRegStr HKCU "${APP_REG_KEY}" "Publisher" "${APP_PUBLISHER}"
  WriteRegStr HKCU "${APP_REG_KEY}" "DisplayIcon" "$INSTDIR\${APP_EXE}"
  WriteRegStr HKCU "${APP_REG_KEY}" "InstallLocation" "$INSTDIR"
  WriteRegStr HKCU "${APP_REG_KEY}" "UninstallString" '"$INSTDIR\Uninstall.exe"'
  WriteRegDWORD HKCU "${APP_REG_KEY}" "NoModify" 1
  WriteRegDWORD HKCU "${APP_REG_KEY}" "NoRepair" 1
SectionEnd

Section /o "Desktop shortcut" DesktopShortcut
  CreateShortcut "$DESKTOP\Your Cat Desktop Pet.lnk" "$INSTDIR\${APP_EXE}"
SectionEnd

Section "Uninstall"
  DeleteRegValue HKCU "Software\Microsoft\Windows\CurrentVersion\Run" "YourCatDesktopPet"
  DeleteRegKey HKCU "${APP_REG_KEY}"
  Delete "$DESKTOP\Your Cat Desktop Pet.lnk"
  RMDir /r "$SMPROGRAMS\Your Cat Desktop Pet"
  RMDir /r "$INSTDIR"
SectionEnd
