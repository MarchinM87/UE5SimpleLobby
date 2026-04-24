$createBody = @{
    playerName = "TestPlayer"
    maxPlayers = 4
    mapName = "/Game/Maps/Level_01"
    dsIp = "8.166.112.80"
    dsPort = 7777
} | ConvertTo-Json

$createResponse = Invoke-WebRequest -Uri "http://8.166.112.80:5000/api/rooms" -Method POST -Headers @{"Content-Type" = "application/json"} -Body $createBody -UseBasicParsing

$createResponse.Content | ConvertFrom-Json | Select-Object roomId, dsIp, dsPort