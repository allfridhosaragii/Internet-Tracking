@echo off
echo ===================================================
echo MENJALANKAN INTERNET TRACER (Dev Mode)
echo ===================================================
echo.
echo 1. Menjalankan Service (Membutuhkan akses Administrator)
echo.

:: Memeriksa hak akses administrator
net session >nul 2>&1
if %errorLevel% == 0 (
    echo [OK] Hak akses Administrator terdeteksi.
) else (
    echo [PERINGATAN] Script ini WAJIB dijalankan sebagai Administrator!
    echo Silakan klik kanan file .bat ini lalu pilih "Run as Administrator".
    pause
    exit /b
)

echo Memulai InternetTracer.Service.exe...
start "InternetTracer Service" cmd /k ""%~dp0InternetTracerService\InternetTracer.Service.exe""

echo Menunggu Service siap...
timeout /t 2 /nobreak >nul

echo Memulai InternetTracer.App via dotnet run...
cd /d "p:\Internet Tracer"
dotnet run --project "InternetTracer.App\InternetTracer.App.csproj"

echo Selesai!
exit
