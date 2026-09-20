$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$output = Join-Path $env:TEMP 'dsh-manager-layout-check'
[IO.Directory]::CreateDirectory($output) | Out-Null
$source = [IO.File]::ReadAllText((Join-Path $root 'dsh-manager.cs'))
$start = $source.IndexOf('            // 先加载保存的设置')
$end = $source.IndexOf('        protected override void OnFormClosed', $start)
# Exercise the real UI builders without contacting providers, loading user settings or applying patches.
$source = $source.Substring(0,$start) + @'
            installDir = @"E:\Ai\DSH\DeepSeek Harness";
            BuildModernUI();
            ResumeLayout(true);
            pluginTimer.Stop();
            pluginLoading = true;
            beautyLoading = true;
        }

'@ + $source.Substring($end)
[IO.File]::WriteAllText((Join-Path $output 'manager-ui.cs'), $source, (New-Object Text.UTF8Encoding($false)))
$compiler = Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
& $compiler /nologo /codepage:65001 /target:exe /main:LayoutCheck "/out:$output/layout-check.exe" /r:System.dll /r:System.Core.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll /r:System.Runtime.Serialization.dll /r:System.IO.Compression.dll /r:System.IO.Compression.FileSystem.dll /r:System.Management.dll (Join-Path $output 'manager-ui.cs') (Join-Path $PSScriptRoot 'layout-check.cs')
if ($LASTEXITCODE -ne 0) { throw 'Layout harness compilation failed' }
& (Join-Path $output 'layout-check.exe') (Join-Path $output 'layout.txt')
Get-Content (Join-Path $output 'layout.txt')
if ($LASTEXITCODE -ne 0) { throw 'Layout checks failed; see reports in the temporary output directory' }
