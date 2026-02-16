$cred = [System.Convert]::ToBase64String([System.Text.Encoding]::ASCII.GetBytes("ADMINISTRATOR:PASSWORD"))

$commands = @(
    "account set password TESTBOT1 PASSWORD PASSWORD",
    "account set gmlevel TESTBOT1 3 -1"
)

foreach ($cmd in $commands) {
    Write-Output "=== $cmd ==="
    $body = "<?xml version=`"1.0`" encoding=`"utf-8`"?><soap:Envelope xmlns:soap=`"http://schemas.xmlsoap.org/soap/envelope/`"><soap:Body><ns1:executeCommand xmlns:ns1=`"urn:MaNGOS`"><command>$cmd</command></ns1:executeCommand></soap:Body></soap:Envelope>"

    try {
        $wc = New-Object System.Net.WebClient
        $wc.Headers.Add("Content-Type", "text/xml")
        $wc.Headers.Add("Authorization", "Basic $cred")
        $result = $wc.UploadString("http://127.0.0.1:7878/", $body)
        Write-Output "Response: $result"
    } catch {
        Write-Output "Failed: $($_.Exception.InnerException.Message)"
        if ($_.Exception.InnerException -is [System.Net.WebException]) {
            $we = $_.Exception.InnerException
            if ($we.Response) {
                $sr = New-Object System.IO.StreamReader($we.Response.GetResponseStream())
                Write-Output "Body: $($sr.ReadToEnd())"
            }
        }
    }
    Write-Output ""
}
