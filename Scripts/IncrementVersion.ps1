param(
    [Parameter(Mandatory=$true)]
    [string]$assemblyInfoPath
)

# Đọc nội dung tệp
$content = Get-Content $assemblyInfoPath -Raw

# Regex để tìm AssemblyVersion và AssemblyFileVersion
$regex = '(\[assembly: Assembly(File)?Version\("(?<major>\d+)\.(?<minor>\d+)\.(?<build>\d+)\.(?<revision>\d+)"\)\])'

# Hàm để tăng Revision lên 1
$newContent = [System.Text.RegularExpressions.Regex]::Replace($content, $regex, {
    param($Match)
    $Major = $Match.Groups["major"].Value
    $Minor = $Match.Groups["minor"].Value
    $Build = $Match.Groups["build"].Value
    $Revision = [int]$Match.Groups["revision"].Value + 1
    
    # Giữ nguyên phần AssemblyVersion hoặc AssemblyFileVersion đang được xử lý
    $type = "Version"
    if ($Match.Groups[2].Value -ne "") { $type = "FileVersion" }
    
    return "[assembly: Assembly$type(`"$Major.$Minor.$Build.$Revision`")]"
})

# Ghi nội dung đã cập nhật trở lại tệp
Set-Content $assemblyInfoPath $newContent -Encoding UTF8