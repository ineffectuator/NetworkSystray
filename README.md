# NetworkSystray

This is a system tray application for Windows that shows all physical network interfaces and allows the user to Enable/Disable those interfaces, and in the case of WiFi interfaces, to Connect/Disconnect those interfaces.

# Building

## Application
This application was built using Visual Studio 2022

## Installer
The installer is created using Wix. 
- Wix can be installed by running install_wix.ps1
- Create the installer by running
`candle.exe Product.wxs -out Product.wixobj && light.exe Product.wixobj -out NetworkManagerAppModern.msi -ext WixUIExtension`
 to generate the installer

