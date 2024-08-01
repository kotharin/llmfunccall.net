namespace EdgarData

module CIK =

    open System.IO
    let fileName = "cik.txt"
    let rnd = System.Random.Shared
 
    let parseLine (line:string) = 
        let splitInfo = line.Split([|':'|])
        if (Array.length splitInfo > 1) then
            //if (splitInfo.[0].Contains("Apple")) then
            //    printfn "n: %s, k:%s" splitInfo.[0] splitInfo.[1]
            
            Some(splitInfo.[0].ToLower(), splitInfo.[1])
        else
            //printfn "bad data : %s" line
            None
    
    let cikMap =
        File.ReadAllLines fileName
        |> Array.map parseLine
        |> Array.choose id
        |> Map.ofArray

    //printfn "cikMap length:%i" (Map.count cikMap)

    let getCIK (companyName:string) =
        Map.tryFind (companyName.ToLower()) cikMap


    let getFinancialData (cik:string) =
        let value = rnd.Next(10) * 10000
        //printfn "profit:%i" value
        value
    // Apple Inc = 0000320193 

    let getSummary (cik:string) (companyName:string) (profit:int) =
        sprintf "{\"cik\":\"%s\", \"companyName\":\"%s\", \"profit\": %i }" cik companyName profit