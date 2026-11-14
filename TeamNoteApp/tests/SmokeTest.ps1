# Run while the API is started with its https launch profile:
# powershell -File .\tests\SmokeTest.ps1
param(
    [string]$BaseUrl = "https://localhost:7231"
)

$ErrorActionPreference = "Stop"

function Assert-True([bool]$Condition, [string]$Message) {
    if (-not $Condition) { throw "ASSERTION FAILED: $Message" }
}

function Invoke-Api {
    param(
        [string]$Method,
        [string]$Path,
        [string]$Token = "",
        $Body = $null,
        [int]$ExpectedStatus = 200
    )

    $headers = @{}
    if ($Token) { $headers["Authorization"] = "Bearer $Token" }

    $params = @{
        Uri = "$BaseUrl$Path"
        Method = $Method
        Headers = $headers
        UseBasicParsing = $true
        ErrorAction = "Stop"
    }
    if ($null -ne $Body) {
        $params["ContentType"] = "application/json"
        $params["Body"] = ConvertTo-Json -InputObject $Body -Compress -Depth 5
    }

    try {
        $response = Invoke-WebRequest @params
        $status = [int]$response.StatusCode
        if ($status -ne $ExpectedStatus) {
            throw "Expected HTTP $ExpectedStatus, received $status for $Method $Path"
        }
        Write-Host "PASS $status $Method $Path"
        if ([string]::IsNullOrWhiteSpace($response.Content)) { return $null }
        return $response.Content | ConvertFrom-Json
    }
    catch {
        # Windows PowerShell and PowerShell 7 expose non-2xx responses differently,
        # but both provide the HTTP status code through Exception.Response.
        $httpResponse = $_.Exception.Response
        if ($null -ne $httpResponse) {
            $status = [int]$httpResponse.StatusCode
            if ($status -eq $ExpectedStatus) {
                Write-Host "PASS $status $Method $Path"
                return $null
            }
            throw "Expected HTTP $ExpectedStatus, received $status for $Method $Path"
        }
        throw
    }
}

# Registration also creates a new owned team for each user.
$alice = Invoke-Api -Method POST -Path "/api/users" -Body @{ name = "Smoke Alice" } -ExpectedStatus 201
$james = Invoke-Api -Method POST -Path "/api/users" -Body @{ name = "Smoke James" } -ExpectedStatus 201
Assert-True ($alice.apiToken -and $james.apiToken -and $alice.apiToken -ne $james.apiToken) "Distinct API tokens"
Assert-True ($alice.teamId -ne $james.teamId) "Each user has a distinct owned team"

$me = Invoke-Api -Method GET -Path "/api/users/me" -Token $alice.apiToken
Assert-True ($me.id -eq $alice.id) "Authenticated user is Alice"
Invoke-Api -Method GET -Path "/api/users/me" -ExpectedStatus 401 | Out-Null
Invoke-Api -Method POST -Path "/api/users" -Body @{ name = " " } -ExpectedStatus 400 | Out-Null

# Unauthorized team access is intentionally hidden behind HTTP 404.
Invoke-Api -Method GET -Path "/api/teams/$($alice.teamId)/notes" -Token $james.apiToken -ExpectedStatus 404 | Out-Null

Invoke-Api -Method POST -Path "/api/teams/$($alice.teamId)/members" -Token $alice.apiToken -Body @{ userId = $james.id } -ExpectedStatus 204 | Out-Null
$members = @(Invoke-Api -Method GET -Path "/api/teams/$($alice.teamId)/members" -Token $alice.apiToken)
Assert-True ($members.Count -eq 2) "Team has both Alice and James"
Assert-True (-not ($members[0].PSObject.Properties.Name -contains 'ApiTokenHash')) "Public team members do not expose token hashes"

# Non-owner cannot change team membership; owner cannot remove themselves.
Invoke-Api -Method POST -Path "/api/teams/$($alice.teamId)/members" -Token $james.apiToken -Body @{ userId = $alice.id } -ExpectedStatus 403 | Out-Null
Invoke-Api -Method DELETE -Path "/api/teams/$($alice.teamId)/members/$($alice.id)" -Token $alice.apiToken -ExpectedStatus 409 | Out-Null

$note = Invoke-Api -Method POST -Path "/api/teams/$($alice.teamId)/notes" -Token $james.apiToken -Body @{ title = "Hello"; content = "From James" } -ExpectedStatus 201
Assert-True ($note.createdByUserId -eq $james.id) "James remains the original author"
$notePath = "/api/teams/$($alice.teamId)/notes/$($note.id)"
$read = Invoke-Api -Method GET -Path $notePath -Token $alice.apiToken
Assert-True ($read.title -eq "Hello") "Alice reads James's note"

$updated = Invoke-Api -Method PUT -Path $notePath -Token $alice.apiToken -Body @{ title = "Updated"; content = "From Alice" }
Assert-True ($updated.title -eq "Updated" -and $updated.createdByUserId -eq $james.id) "Update preserves authorship"
Invoke-Api -Method GET -Path "/api/teams/$($james.teamId)/notes/$($note.id)" -Token $alice.apiToken -ExpectedStatus 404 | Out-Null
Invoke-Api -Method DELETE -Path $notePath -Token $alice.apiToken -ExpectedStatus 204 | Out-Null
Invoke-Api -Method GET -Path $notePath -Token $james.apiToken -ExpectedStatus 404 | Out-Null

# Test the previously failing DELETE endpoint, including authorization after removal.
Invoke-Api -Method DELETE -Path "/api/teams/$($alice.teamId)/members/$($james.id)" -Token $alice.apiToken -ExpectedStatus 204 | Out-Null
Invoke-Api -Method GET -Path "/api/teams/$($alice.teamId)/notes" -Token $james.apiToken -ExpectedStatus 404 | Out-Null

Write-Host "All smoke/integration checks passed."
