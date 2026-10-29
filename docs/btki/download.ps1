$ProgressPreference = 'SilentlyContinue'
$base = 'https://repository.beacukai.go.id/download/2022/03/'
$files = @{
 1='d4ffeb744a3ac786f5bf0ad344b0982e-chapter-1-(converted).pdf'; 2='211e2e72b754ec0608f783fceb68c597-chapter-2-(converted).pdf'
 3='9745e10f5025a90e0fb4960b9e2f99d4-chapter-3-(converted).pdf'; 4='29f219ed9b96f2570a04f7ecfcdb0854-chapter-4-(converted).pdf'
 5='372e536d0264b1ab743b245fca25bb8b-chapter-5-(converted).pdf'; 6='b175813b38b035d109b66c6c4e454428-chapter-6-(converted).pdf'
 7='2a3a7583178b15d5830bf605d4955a43-chapter-7-(converted).pdf'; 8='50b27c9d70bb6199637193f22bd480dc-chapter-8-(converted).pdf'
 9='e9e7bdb771c932d68ac3c2aaa665b8a7-chapter-9-(converted).pdf'; 10='550b2e47a2e3e77e6aa4ee643d723a30-chapter-10-(converted).pdf'
 11='6e0e3730d4753db460e9553536ca2597-chapter-11-(converted).pdf'; 12='92d403524ee502a33a6fcb49f9df6491-chapter-12-(converted).pdf'
 13='2c91e9cee6b9c6519e8b9476317afa9c-chapter-13-(converted).pdf'; 14='f1a38e78da02d1e4a895fb6efc43fb45-chapter-14-(converted).pdf'
 15='f4a8bb00cddbcdb9635466f4c0be05d7-chapter-15-(converted).pdf'; 16='a16d302918464777d55ca9c6e51a70d0-chapter-16.pdf'
 17='50f8ee98dcab7ddb3a05f391deac339e-chapter-17.pdf'; 18='8d7f8ea7c4bf75b2f414dc852c040263-chapter-18.pdf'
 19='e43bf6fab04603e316db9601e8ce5919-chapter-19.pdf'; 20='9ea5a8b7e2db6c92091e430895d1584e-chapter-20.pdf'
 21='11eb5152cae7ffe0b514bc38ba95e15a-chapter-21.pdf'; 22='c05c139a65c6124446cfbef445f3ef9e-chapter-22.pdf'
 23='3824b82965c7eca5ea95b581edbbd65f-chapter-23.pdf'; 24='0d4a8074ede83342b67487be456d78b5-chapter-24.pdf'
 25='69abce74ef82333f866e8e571612a9a4-chapter-25.pdf'; 26='d658a04397029b16695b3e224c19e6ac-chapter-26.pdf'
 27='7a6705be33e256967f1ac1e42195500f-chapter-27.pdf'; 28='ed17bf2d565c40061570eb696d85d193-chapter-28.pdf'
 29='52589447c19854a917777117e64b2036-chapter-29.pdf'; 30='8bea3a7795708df780b844139465f58d-chapter-30.pdf'
 31='c1fac116e1d3313fa2806fdd167548e1-chapter-31.pdf'; 32='c84e060e0c35583ee575e9b9a3b10660-chapter-32.pdf'
 33='37dcadbb1367deadc70a9fbea128a6aa-chapter-33.pdf'; 34='12ab66e0efbb1cf66dda55dd851a64ed-chapter-34.pdf'
 35='5b3b01dae838907e9731bee795d55f56-chapter-35.pdf'; 36='e6964fb66e21e56b0feed44bea626532-chapter-36.pdf'
 37='1475e91a8e5e8f621b81872bdfece3ac-chapter-37.pdf'; 38='7f62f120f809ec1969b44162b895931a-chapter-38.pdf'
 39='fe0d53bc4778251a1a3d590151a51eed-chapter-39.pdf'; 40='a81ee0ea78c86940cd23317d089976d1-chapter-40.pdf'
 41='0be729e6cf0128e88c9a36c934ef3951-chapter-41.pdf'; 42='351f72ed7094c15a18975f72f2d58f31-chapter-42.pdf'
 43='820ba6d91c6c79a4aa6ae3b73d62cc72-chapter-43.pdf'; 44='45ec99c218c93a641439ef582dca5429-chapter-44.pdf'
 45='96ccd6f17fe999f29ac8b12bca93e411-chapter-45.pdf'; 46='dd4d89dd7a3ee80a69ca84519f50ce79-chapter-46.pdf'
 47='c6eb2ef07b268d7b62d40cac2aa3f986-chapter-47.pdf'; 48='2554e12defa0b51d320664cf76362a00-chapter-48.pdf'
 49='bdb4a4e44a945a0b38ef0e1eb0652f3a-chapter-49.pdf'; 50='9f8da9fe45739d64ed67d02f7decb53d-chapter-50.pdf'
 51='0dccd91eed66df9da941fd6d27b6cf5c-chapter-51.pdf'; 52='62df1fbc292bd246b674684ff20af883-chapter-52.pdf'
 53='fc098e2966c72dc7ced2f71ba06967a2-chapter-53.pdf'; 54='92c93df6c13f110d6fa6a4c9e19c3113-chapter-54.pdf'
 55='7e0f967cec6ff04faa9e2e505733205f-chapter-55.pdf'; 56='1d182d79d10a246c03879a73fab42643-chapter-56.pdf'
 57='b5ac314b2a22368c27b6a51c891442b2-chapter-57.pdf'; 58='690ad2d9539ea1994100b9dff97af97b-chapter-58.pdf'
 59='9db0e7dd65d3bf8addec4aada5412370-chapter-59.pdf'; 60='4814ea6d4c923ca28122129946df854f-chapter-60.pdf'
 61='bfa9ad80ffe304776378cd2310364dad-chapter-61.pdf'; 62='aab1000c00b67cb7c231e11c3a281074-chapter-62.pdf'
 63='6123fbc242bac81df310db711e633156-chapter-63.pdf'; 64='879792c629b94d81282310523c31938f-chapter-64.pdf'
 65='a829524cd2f9fa8e0387ca5e47d22700-chapter-65.pdf'; 66='4fbe018280347eb361fa459310fbc730-chapter-66.pdf'
 67='9d1f804f24549b8819bc1f7cbdabb7a9-chapter-67.pdf'; 68='9ce3a9df55c7b0414474d699ed42da6c-chapter-68.pdf'
 69='bf666ed4f1b786b36592c8cb290f0382-chapter-69.pdf'; 70='a7879c801cafe0090d3193b8e5515d59-chapter-70.pdf'
 71='390ad1738903f52e5e803f606449e957-chapter-71.pdf'; 72='5e5e3bb4404056403be4dd63773c49dd-chapter-72.pdf'
 73='9bb99a9fb54734aa764f625de9ae63eb-chapter-73.pdf'; 74='7c000e46067caeddd4ab8b24f6af96b3-chapter-74.pdf'
 75='f909310e2fea220aa6468cb6f5fef1da-chapter-75.pdf'; 76='c7d5992259eec1bcd238aa83605e171e-chapter-76.pdf'
 78='980e24a4c8610245bff889d196741cf2-chapter-78.pdf'; 79='70589489a1efb875d7281f92a2ef5c02-chapter-79.pdf'
 80='8b930e797502548467cbe9e2a8240750-chapter-80.pdf'; 81='2c773795fbc16ab5a2474d1227f66875-chapter-81.pdf'
 82='c9a438409a5d417c2f221dd400eb034b-chapter-82.pdf'; 83='7e9881b8ba675b0ccbbc0e456a049d32-chapter-83.pdf'
 84='3b564ac9fa2e6ee3ec10d87b59f3e9c0-chapter-84.pdf'; 85='a9e033e776c2f740a53f5d005de0c727-chapter-85.pdf'
 86='9abcb44b255d04b0cc9b79463182d5a7-chapter-86.pdf'; 87='545023d2e5abede7577a2794fb7dbfc7-chapter-87.pdf'
 88='1b39fca9376957679e1d6d6733d6876d-chapter-88.pdf'; 89='9cf9a431bfeb3224b4effd322decf659-chapter-89.pdf'
 90='1899672360710b606d1c5e0a43691747-chapter-90.pdf'; 91='fe266b1f3441259676cf3a9f0cffbeed-chapter-91.pdf'
 92='bfe6303f11e29e2f1d4a5007ec04e02a-chapter-92.pdf'; 93='56ccba2c41b516e46b285faebecd896a-chapter-93.pdf'
 94='dae986d2994baeb91ff114f5eaba6617-chapter-94.pdf'; 95='93225ab6cad7132bf34d85e31f41e421-chapter-95.pdf'
 96='d0a0cfe04fd2adabfdc8fb7fc1dad7a4-chapter-96.pdf'; 97='af3d932f52d4ef9e184e79dc54d817db-chapter-97.pdf'
}
$dir = $PSScriptRoot; $log = @()
foreach ($n in ($files.Keys | Sort-Object)) {
    $out = Join-Path $dir ('chapter-{0:D2}.pdf' -f $n)
    if ((Test-Path $out) -and (Get-Item $out).Length -gt 1000) { $log += "$n skip"; continue }
    $ok = $false
    for ($try = 1; $try -le 3 -and -not $ok; $try++) {
        try {
            Invoke-WebRequest ($base + $files[$n]) -OutFile $out -UseBasicParsing -TimeoutSec 180
            $head = [Text.Encoding]::ASCII.GetString([IO.File]::ReadAllBytes($out), 0, 5)
            if ($head -eq '%PDF-') { $ok = $true } else { throw "not a PDF ($head)" }
        } catch { $err = $_.Exception.Message; Start-Sleep 3 }
    }
    $log += if ($ok) { "$n ok $((Get-Item $out).Length)" } else { "$n FAIL $err" }
}
[IO.File]::WriteAllLines((Join-Path $dir 'download.log'), $log)
