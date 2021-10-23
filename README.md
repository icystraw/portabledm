# Portable HTTP Download Manager
An easy to use, fully-featured HTTP file download manager.
* HTTP and HTTPS support
* HTTP cookie, redirection and authentication support
* Multi-file, multi-thread support for maximum download speed, with dynamic and intelligent download section creation
* Supports video downloading from YouTube and bilibili.com, including YouTube signature-ciphered videos
* Supports downloading only a specific section of a file so it is great for repairing broken downloads
* Chromium based browser extension avaiable for taking over downloads from browser
* Well-organised and informative user interface to monitor current downloads
* Written in C#/.NET/C++ with efficiency in mind
## How to build and run
* This project is tested on Visual Studio 2019 with .NET Desktop and C++ Desktop workloads installed.
* Open partialdownloadgui.sln with Visual Studio and build both projects.
* Copy secreplace.exe from x64\Release to partialdownloadgui\bin\Release\net5.0-windows.
* Press Ctrl-F5 within Visual Studio to run the program.
* Chromium based browser extension is located in link-grabber folder.
