Import-Module ExchangeOnlineManagement
Connect-ExchangeOnline

# Zakres czasu (możesz zmienić)
$start = (Get-Date).AddDays(-30)
$end   = Get-Date

# Maksymalny możliwy rozmiar (Search-UnifiedAuditLog limit)
$maxSize = 50000

Write-Host "Pobieram logi CopilotInteraction..." -ForegroundColor Cyan

$logs = Search-UnifiedAuditLog `
    -StartDate $start `
    -EndDate $end `
    -RecordType MicrosoftCopilotInteraction `
    -ResultSize $maxSize

Write-Host "Przetwarzam logi..." -ForegroundColor Cyan

# Parsowanie JSON
$parsed = $logs | ForEach-Object {
    $json = $null
    try { $json = $_.AuditData | ConvertFrom-Json } catch {}

    [PSCustomObject]@{
        TimeGenerated       = $_.CreationTime
        UserPrincipalName   = $json.UserId
        Application         = $json.Application
        AgentId             = $json.AgentId
        Workload            = $json.Workload
        ClientIP            = $json.ClientIP
    }
}

# Automatyczne wykrywanie agentów
$agents = $parsed | Group-Object AgentId, Application | ForEach-Object {
    [PSCustomObject]@{
        AgentId   = $_.Group[0].AgentId
        AgentName = $_.Group[0].Application
    }
}

Write-Host "Znaleziono $($agents.Count) agentów Copilot." -ForegroundColor Green

# Telemetria: Agent → User → Date → Count + Workload
$telemetry = $parsed | Group-Object `
    -Property AgentId, Application, UserPrincipalName, Workload, @{Name="Date";Expression={($_.TimeGenerated).ToString("yyyy-MM-dd")}} |
    ForEach-Object {
        [PSCustomObject]@{
            AgentId           = $_.Group[0].AgentId
            AgentName         = $_.Group[0].Application
            User              = $_.Group[0].UserPrincipalName
            Workload          = $_.Group[0].Workload
            Date              = $_.Group[0].TimeGenerated.ToString("yyyy-MM-dd")
            InteractionsCount = $_.Count
        }
    }

# Heatmapa aktywności (per godzina)
$heatmap = $parsed | Group-Object @{Name="Hour";Expression={($_.TimeGenerated).ToString("yyyy-MM-dd HH:00")}} |
    ForEach-Object {
        [PSCustomObject]@{
            Hour              = $_.Group[0].TimeGenerated.ToString("yyyy-MM-dd HH:00")
            InteractionsCount = $_.Count
        }
    }

# Eksport do CSV
$telemetry | Export-Csv -Path ".\Copilot_Telemetry.csv" -NoTypeInformation -Encoding UTF8
$heatmap   | Export-Csv -Path ".\Copilot_Heatmap.csv" -NoTypeInformation -Encoding UTF8
$agents    | Export-Csv -Path ".\Copilot_Agents.csv" -NoTypeInformation -Encoding UTF8

Write-Host "Eksport zakończony:" -ForegroundColor Green
Write-Host " - Copilot_Telemetry.csv"
Write-Host " - Copilot_Heatmap.csv"
Write-Host " - Copilot_Agents.csv"
