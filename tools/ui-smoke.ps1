<#
.SYNOPSIS
    Дымовая проверка интерфейса: собирает приложение во временную папку, запускает его
    с чистыми (демонстрационными) данными, по очереди открывает формы и сохраняет скриншоты.

.DESCRIPTION
    Нужна Windows PowerShell 5.1 или PowerShell 7 на Windows. Файл сохранен в UTF-8 с BOM:
    без BOM Windows PowerShell 5.1 читает кириллицу в ANSI и скрипт не разбирается.
    Скрипт не трогает данные в bin\Debug — приложение запускается из отдельной папки.
    Кнопки нажимаются щелчком мыши: в Windows Forms вызов InvokePattern у кнопки, которая
    открывает модальное окно, ждет закрытия этого окна.

.EXAMPLE
    powershell -ExecutionPolicy Bypass -File tools\ui-smoke.ps1
    powershell -ExecutionPolicy Bypass -File tools\ui-smoke.ps1 -OutDir docs\screenshots
#>
param(
    [string]$OutDir = (Join-Path $env:TEMP "Hotel-ui-smoke\screenshots")
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot
$appDir = Join-Path $env:TEMP "Hotel-ui-smoke\app"
$exe = Join-Path $appDir "Hotel.exe"

Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes, System.Drawing, System.Windows.Forms
Add-Type @"
using System;
using System.Runtime.InteropServices;
public static class SmokeWin {
    [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left, Top, Right, Bottom; }
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
    [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
    [DllImport("user32.dll")] public static extern bool PrintWindow(IntPtr h, IntPtr hdc, uint flags);
    [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
    [DllImport("user32.dll")] public static extern void mouse_event(uint flags, int dx, int dy, uint data, UIntPtr extra);
    [DllImport("dwmapi.dll")] public static extern int DwmGetWindowAttribute(IntPtr h, int attr, out RECT r, int size);
}
"@
[SmokeWin]::SetProcessDPIAware() | Out-Null

$AE = [System.Windows.Automation.AutomationElement]
$TS = [System.Windows.Automation.TreeScope]
$CT = [System.Windows.Automation.ControlType]

function Save-Screenshot($element, [string]$name) {
    $handle = [IntPtr]$element.Current.NativeWindowHandle
    [SmokeWin]::SetForegroundWindow($handle) | Out-Null
    Start-Sleep -Milliseconds 900
    # Окно рисуется само в себя (PrintWindow), поэтому на снимок не попадают другие окна.
    # Рамка без невидимых краев и тени — DWMWA_EXTENDED_FRAME_BOUNDS.
    $r = New-Object SmokeWin+RECT
    [SmokeWin]::GetWindowRect($handle, [ref]$r) | Out-Null
    $frame = New-Object SmokeWin+RECT
    [SmokeWin]::DwmGetWindowAttribute($handle, 9, [ref]$frame, 16) | Out-Null
    $full = New-Object System.Drawing.Bitmap ($r.Right - $r.Left), ($r.Bottom - $r.Top)
    $graphics = [System.Drawing.Graphics]::FromImage($full)
    $hdc = $graphics.GetHdc()
    [SmokeWin]::PrintWindow($handle, $hdc, 2) | Out-Null
    $graphics.ReleaseHdc($hdc); $graphics.Dispose()
    $crop = New-Object System.Drawing.Rectangle ($frame.Left - $r.Left), ($frame.Top - $r.Top), ($frame.Right - $frame.Left), ($frame.Bottom - $frame.Top)
    $bitmap = $full.Clone($crop, $full.PixelFormat)
    $path = Join-Path $OutDir "$name.png"
    $bitmap.Save($path, [System.Drawing.Imaging.ImageFormat]::Png)
    $full.Dispose(); $bitmap.Dispose()
    Write-Host "  скриншот: $path"
}

function Find-ByName($root, [string]$name) {
    $condition = New-Object System.Windows.Automation.PropertyCondition($AE::NameProperty, $name)
    for ($i = 0; $i -lt 10; $i++) {
        $element = $root.FindFirst($TS::Descendants, $condition)
        if ($element) { return $element }
        Start-Sleep -Milliseconds 300
    }
    throw "Не найден элемент «$name»"
}

function Find-ByType($root, $controlType) {
    $condition = New-Object System.Windows.Automation.PropertyCondition($AE::ControlTypeProperty, $controlType)
    return $root.FindFirst($TS::Descendants, $condition)
}

function Click($element) {
    $window = $element
    $walker = [System.Windows.Automation.TreeWalker]::ControlViewWalker
    while ($window.Current.ControlType -ne $CT::Window) { $window = $walker.GetParent($window) }
    [SmokeWin]::SetForegroundWindow([IntPtr]$window.Current.NativeWindowHandle) | Out-Null
    Start-Sleep -Milliseconds 200
    $rect = $element.Current.BoundingRectangle
    [SmokeWin]::SetCursorPos([int]($rect.X + $rect.Width / 2), [int]($rect.Y + $rect.Height / 2)) | Out-Null
    [SmokeWin]::mouse_event(0x0002, 0, 0, 0, [UIntPtr]::Zero)
    [SmokeWin]::mouse_event(0x0004, 0, 0, 0, [UIntPtr]::Zero)
    Start-Sleep -Milliseconds 400
}

function Click-Button($root, [string]$name) { Click (Find-ByName $root $name) }

function Get-Windows {
    $condition = New-Object System.Windows.Automation.PropertyCondition($AE::ProcessIdProperty, $process.Id)
    return @($AE::RootElement.FindAll($TS::Children, $condition))
}

function Wait-TopWindow([string]$name) {
    for ($i = 0; $i -lt 40; $i++) {
        Start-Sleep -Milliseconds 300
        $window = Get-Windows | Where-Object { $_.Current.Name -eq $name } | Select-Object -First 1
        if ($window) { return $window }
    }
    throw "Окно «$name» не открылось"
}

# Новое окно — модальное окно поверх владельца: дочернее окно владельца или новое окно процесса.
function Wait-Dialog($owner, [string]$title = "*") {
    $condition = New-Object System.Windows.Automation.PropertyCondition($AE::ControlTypeProperty, $CT::Window)
    for ($i = 0; $i -lt 30; $i++) {
        Start-Sleep -Milliseconds 300
        $dialog = @($owner.FindAll($TS::Children, $condition)) | Where-Object { $_.Current.Name -like $title } | Select-Object -First 1
        if ($dialog) { return $dialog }
    }
    throw "Окно «$title» не открылось"
}

function Close-Window($window) {
    $window.GetCurrentPattern([System.Windows.Automation.WindowPattern]::Pattern).Close()
    Start-Sleep -Milliseconds 500
}

function Select-Tab($root, [string]$name) {
    (Find-ByName $root $name).GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern).Select()
    Start-Sleep -Milliseconds 500
}

function Show-Dialog($owner, [string]$button, [string]$name) {
    Write-Host $button
    Click-Button $owner $button
    $dialog = Wait-Dialog $owner
    Save-Screenshot $dialog $name
    return $dialog
}

function Open-Menu($main, [string]$menu, [string]$item, [string]$name) {
    Write-Host "$menu → $item"
    Click-Button $main $menu
    $entry = $null
    for ($i = 0; $i -lt 10 -and -not $entry; $i++) {
        Start-Sleep -Milliseconds 300
        $condition = New-Object System.Windows.Automation.PropertyCondition($AE::NameProperty, $item)
        $entry = $AE::RootElement.FindFirst($TS::Descendants, $condition)
    }
    if (-not $entry) { throw "Не найден пункт меню «$item»" }
    Click $entry
    $dialog = Wait-Dialog $main
    Save-Screenshot $dialog $name
    return $dialog
}

function Sign-In([string]$login, [string]$password) {
    $window = Wait-TopWindow "Администратор гостиницы — вход"
    $edit = Find-ByType $window $CT::Edit
    $edit.GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern).SetValue($login)
    $passwordBox = $window.FindFirst($TS::Descendants,
        (New-Object System.Windows.Automation.PropertyCondition($AE::IsPasswordProperty, $true)))
    $passwordBox.GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern).SetValue($password)
    return $window
}

function Complete-SignIn($login) {
    Click-Button $login "Войти"
    try { return Wait-TopWindow "Администратор гостиницы" }
    catch { Save-Screenshot $login "login-failed"; throw }
}

Write-Host "Сборка в $appDir"
Remove-Item -Recurse -Force $appDir -ErrorAction SilentlyContinue
dotnet build (Join-Path $repoRoot "src\Hotel") -o $appDir -v q -nologo | Out-Null
if ($LASTEXITCODE -ne 0) { throw "Сборка не удалась" }
New-Item -ItemType Directory -Force $OutDir | Out-Null

$process = Start-Process $exe -PassThru
try {
    Write-Host "Вход с неверным паролем, затем управляющим"
    $login = Sign-In "manager" "wrong"
    Save-Screenshot $login "00-login"
    Click-Button $login "Войти"
    Save-Screenshot $login "00-login-error"
    Close-Window $login
    # Отмена входа закрывает программу — запускаем снова.
    $process.WaitForExit(5000) | Out-Null
    $process = Start-Process $exe -PassThru
    $login = Sign-In "manager" "manager123"
    $main = Complete-SignIn $login
    Save-Screenshot $main "01-guests"
    Select-Tab $main "Заезд сегодня"
    Save-Screenshot $main "02-arrivals"
    Select-Tab $main "Отъезд сегодня"
    Save-Screenshot $main "03-departures"
    Select-Tab $main "Брони"
    Save-Screenshot $main "04-reservations"
    Select-Tab $main "Номера"
    Save-Screenshot $main "05-rooms"

    $search = Show-Dialog $main "Подобрать номер (F2)" "06-room-search"
    Click-Button $search "Подобрать"
    Save-Screenshot $search "07-room-search-results"
    $reservation = Show-Dialog $search "Забронировать…" "08-reservation-new"
    Click-Button $reservation "Сохранить бронь"
    Save-Screenshot $reservation "09-reservation-errors"
    Close-Window $reservation
    Close-Window $search

    Select-Tab $main "Заезд сегодня"
    $checkIn = Show-Dialog $main "Заселить по брони…" "10-check-in"
    $guest = Show-Dialog $checkIn "Зарегистрировать нового гостя…" "11-guest-new"
    Click-Button $guest "Сохранить"
    Save-Screenshot $guest "12-guest-errors"
    Close-Window $guest
    Close-Window $checkIn

    Select-Tab $main "Отъезд сегодня"
    $checkOut = Show-Dialog $main "Выписать…" "13-check-out-overdue"
    Click-Button $checkOut "Выписать"
    $receipt = Wait-Dialog $main "Квитанция*"
    Save-Screenshot $receipt "14-receipt-surcharge"
    Close-Window $receipt
    Close-Window (Show-Dialog $main "Продлить проживание…" "15-extend")

    Select-Tab $main "Гости"
    $receipts = Show-Dialog $main "Квитанции гостя…" "16-guest-receipts"
    Close-Window (Show-Dialog $receipts "Открыть квитанцию…" "17-receipt")
    Close-Window $receipts
    Close-Window (Show-Dialog $main "Выписать / досрочный отъезд…" "18-check-out-early")

    $rooms = Open-Menu $main "Управление" "Номера…" "19-rooms"
    Close-Window (Show-Dialog $rooms "Добавить…" "20-room-new")
    Close-Window $rooms
    Close-Window (Open-Menu $main "Управление" "Тарифы…" "21-tariffs")
    Close-Window (Open-Menu $main "Управление" "Квитанции за период…" "22-receipts-period")
    $users = Open-Menu $main "Управление" "Пользователи…" "23-users"
    Close-Window (Show-Dialog $users "Добавить…" "24-user-new")
    Close-Window $users
    Close-Window (Open-Menu $main "Управление" "Журнал действий…" "25-log")

    Write-Host "Выход и вход администратором"
    Click-Button $main "Выход"
    $login = Sign-In "admin" "admin123"
    $main = Complete-SignIn $login
    Save-Screenshot $main "26-administrator-session"

    $errors = Get-Content -Encoding utf8 (Join-Path $appDir "logs\app.log") | Select-String "Непредвиденная ошибка"
    if ($errors) { throw "В журнале есть непредвиденные ошибки:`n$errors" }
    Write-Host "Готово: все формы открылись без ошибок."
}
finally {
    if (-not $process.HasExited) { Stop-Process -Id $process.Id }
}
