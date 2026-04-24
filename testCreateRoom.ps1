$roomId = "A1B2C3D4"

$joinBody = @{
    playerName = "Player2"
} | ConvertTo-Json

$joinResponse = Invoke-WebRequest -Uri "http://8.166.112.80:5000/api/rooms/$roomId/join" -Method POST -Headers @{"Content-Type" = "application/json"} -Body $joinBody -UseBasicParsing

$joinResponse.Content | ConvertFrom-Json | Select-Object roomId, dsIp, dsPort, joinToken