<#
    Transport Reply Analyzer – Exchange Online
    Wersja z LOGGING + TELEMETRY
#>

# -----------------------------
# CONFIG
# -----------------------------
$DistributionGroup = "grupa@contoso.com"   # <-- ustaw grupę
$Days = 7                                   # <-- zmień na 1 dla raportu jednodniowego

# -----------------------------
# PATHS
# -----------------------------
$timestamp = (Get-Date -Format "yyyy-MM-dd_HH-mm-ss")
$logPath = ".\TransportAnalyzer_$timestamp.log"
$telemetryPath = ".\TransportTelemetry_$timestamp.json"
$csvPath = ".\TransportReport_${Days}days.csv"
$htmlPath = ".\TransportReplyReport_${Days}days.html"

# -----------------------------
# LOGGING FUNCTION
# -----------------------------
function Write-Log {
    param([string]$level, [string]$message)
    $line = "[{0}] {1}  {2}" -f (Get-Date -Format "yyyy-MM-dd HH:mm:ss"), $level, $message
    Add-Content -Path $logPath -Value $line
    Write-Host $line
}

Write-Log "INFO" "Start Transport Reply Analyzer"
Write-Log "INFO" "Group: $DistributionGroup"
Write-Log "INFO" "Days: $Days"

# -----------------------------
# CHECK EXCHANGE ONLINE SESSION
# -----------------------------
try {
    Get-OrganizationConfig -ErrorAction Stop | Out-Null
    Write-Log "INFO" "Connected to Exchange Online"
} catch {
    Write-Log "ERROR" "Not connected to Exchange Online. Run Connect-ExchangeOnline."
    exit
}

# -----------------------------
# DATE RANGE
# -----------------------------
$End   = (Get-Date)
$Start = (Get-Date).AddDays(-$Days)
Write-Log "INFO" "Date range: $Start -> $End"

# -----------------------------
# GET MESSAGE TRACE
# -----------------------------
Write-Log "INFO" "Fetching MessageTrace..."
$trace = Get-MessageTrace -RecipientAddress $DistributionGroup -StartDate $Start -EndDate $End

if (-not $trace) {
    Write-Log "WARNING" "No MessageTrace results found."
    exit
}

Write-Log "INFO" "MessageTrace records: $($trace.Count)"

# -----------------------------
# BUILD TRANSPORT REPORT
# -----------------------------
$report = foreach ($msg in $trace) {

    $details = Get-MessageTraceDetail -MessageTraceId $msg.MessageTraceId -RecipientAddress $msg.RecipientAddress

    $expandedEvents = $details | Where-Object { $_.EventType -eq "Expanded" }
    $expandedMembers = @()

    foreach ($ev in $expandedEvents) {
        if ($ev.RecipientAddress) {
            $expandedMembers += $ev.RecipientAddress
        }
    }

    [PSCustomObject]@{
        MessageTraceId     = $msg.MessageTraceId
        Direction          = $msg.Direction
        SenderAddress      = $msg.SenderAddress
        RecipientAddress   = $msg.RecipientAddress
        Received           = $msg.Received
        Subject            = $msg.Subject
        Status             = $msg.Status
        MessageSizeBytes   = $msg.Size
        ExpandedMembers    = ($expandedMembers -join ";")
        ExpandedCount      = $expandedMembers.Count
    }
}

Write-Log "INFO" "Transport report built."

# -----------------------------
# ROZSZERZONY DELTA TIME
# -----------------------------
function Normalize-Subject {
    param($subject)
    if (-not $subject) { return "" }

    $s = $subject
    $s = $s -replace '^(Re:|RE:)\s*', ''
    $s = $s -replace '^(Fw:|FW:)\s*', ''
    $s = $s -replace '\[EXTERNAL\]\s*', ''
    $s = $s.Trim()
    return $s
}

$report | ForEach-Object {
    $_ | Add-Member -NotePropertyName NormalizedSubject -NotePropertyValue (Normalize-Subject $_.Subject)
}

$groups = $report | Group-Object NormalizedSubject
$threadCount = $groups.Count

foreach ($g in $groups) {

    $sorted = $g.Group | Sort-Object Received
    $replyTimes = @()

    for ($i = 0; $i -lt $sorted.Count; $i++) {

        $current = $sorted[$i]

        if ($i -eq 0) {
            $current | Add-Member -NotePropertyName ReplyDeltaMinutes -NotePropertyValue $null
            $current | Add-Member -NotePropertyName ReplyToSender -NotePropertyValue $null
        }
        else {
            $previous = $sorted[$i - 1]
            $delta = ($current.Received - $previous.Received).TotalMinutes
            $deltaRounded = [math]::Round($delta, 1)

            $current | Add-Member -NotePropertyName ReplyDeltaMinutes -NotePropertyValue $deltaRounded
            $current | Add-Member -NotePropertyName ReplyToSender -NotePropertyValue $previous.SenderAddress

            $replyTimes += $deltaRounded
        }
    }

    $firstReply = if ($replyTimes.Count -gt 0) { $replyTimes[0] } else { $null }
    $avgReply   = if ($replyTimes.Count -gt 0) { [math]::Round(($replyTimes | Measure-Object -Average).Average, 1) } else { $null }
    $replyCount = $replyTimes.Count
    $rootSender = $sorted[0].SenderAddress

    foreach ($msg in $sorted) {
        $msg | Add-Member -NotePropertyName ThreadFirstReplyMinutes -NotePropertyValue $firstReply
        $msg | Add-Member -NotePropertyName ThreadAverageReplyMinutes -NotePropertyValue $avgReply
        $msg | Add-Member -NotePropertyName ThreadReplyCount -NotePropertyValue $replyCount
        $msg | Add-Member -NotePropertyName ThreadRootSender -NotePropertyValue $rootSender
    }
}

Write-Log "INFO" "Delta-time calculated."
Write-Log "INFO" "Threads: $threadCount"

# -----------------------------
# PIVOT PER NADAWCA
# -----------------------------
$senderStats = $report |
    Where-Object { $_.ReplyDeltaMinutes -ne $null } |
    Group-Object SenderAddress |
    ForEach-Object {
        $avg = ($_.Group.ReplyDeltaMinutes | Measure-Object -Average).Average
        [PSCustomObject]@{
            SenderAddress = $_.Name
            AvgReplyMinutes = [math]::Round($avg, 1)
            ReplyCount = $_.Group.Count
        }
    } |
    Sort-Object AvgReplyMinutes

Write-Log "INFO" "Pivot per sender calculated."

# -----------------------------
# EXPORT CSV
# -----------------------------
$report | Export-Csv -Path $csvPath -NoTypeInformation -Encoding UTF8
Write-Log "INFO" "CSV saved: $csvPath"

# -----------------------------
# HTML + CHART.JS
# -----------------------------
$labels = ($senderStats.SenderAddress -join '","')
$data   = ($senderStats.AvgReplyMinutes -join ',')

$head = @"
<meta charset="UTF-8">
<title>Transport Reply Time Report</title>
<script src="https://cdn.jsdelivr.net/npm/chart.js"></script>
<style>
body { font-family: Segoe UI, Arial, sans-serif; margin: 20px; }
h1 { font-size: 24px; margin-bottom: 10px; }
canvas { max-width: 1200px; max-height: 500px; }
table { border-collapse: collapse; margin-top: 20px; }
th, td { border: 1px solid #ccc; padding: 4px 8px; font-size: 11px; }
th { background-color: #f3f3f3; }
</style>
"@

$body = @"
<h1>Transport Reply Time Report – $($DistributionGroup) – ostatnie $Days dni</h1>

<h2>Średni czas odpowiedzi per nadawca (minuty)</h2>
<canvas id="replyChart"></canvas>

<script>
const ctx = document.getElementById('replyChart').getContext('2d');
const replyChart = new Chart(ctx, {
    type: 'bar',
    data: {
        labels: ["$labels"],
        datasets: [{
            label: 'Średni czas odpowiedzi (minuty)',
            data: [$data],
            backgroundColor: 'rgba(54, 162, 235, 0.6)',
            borderColor: 'rgba(54, 162, 235, 1)',
            borderWidth: 1
        }]
    },
    options: {
        responsive: true,
        plugins: {
            legend: { display: true },
            tooltip: { enabled: true }
        },
        scales: {
            x: { ticks: { autoSkip: false, maxRotation: 60, minRotation: 30 } },
            y: { beginAtZero: true }
        }
    }
});
</script>

<h2>Szczegóły wiadomości (transport + delta time)</h2>
"@

$htmlTable = $report |
    Select-Object SenderAddress, RecipientAddress, Received, Subject,
                  ReplyDeltaMinutes, ReplyToSender,
                  ThreadFirstReplyMinutes, ThreadAverageReplyMinutes,
                  ThreadReplyCount, ThreadRootSender |
    ConvertTo-Html -Fragment

$html = ConvertTo-Html -Head $head -Body ($body + $htmlTable)
$html | Out-File -FilePath $htmlPath -Encoding UTF8

Write-Log "INFO" "HTML saved: $htmlPath"

# -----------------------------
# TELEMETRY JSON
# -----------------------------
$telemetry = [PSCustomObject]@{
    timestamp = (Get-Date).ToString("o")
    group = $DistributionGroup
    days = $Days
    messageCount = $trace.Count
    threadCount = $threadCount
    avgReplyMinutes = ($report.ThreadAverageReplyMinutes | Where-Object { $_ -ne $null } | Measure-Object -Average).Average
    firstReplyMinutes = ($report.ThreadFirstReplyMinutes | Where-Object { $_ -ne $null } | Measure-Object -Average).Average
    senders = $senderStats.Count
    topSlow = $senderStats | Sort-Object AvgReplyMinutes -Descending | Select-Object -First 5
    topFast = $senderStats | Sort-Object AvgReplyMinutes -Ascending | Select-Object -First 5
}

$telemetry | ConvertTo-Json -Depth 5 | Out-File -FilePath $telemetryPath -Encoding UTF8
Write-Log "INFO" "Telemetry saved: $telemetryPath"

# -----------------------------
# END
# -----------------------------
Write-Log "INFO" "Completed."
