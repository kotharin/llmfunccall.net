namespace Wrapper

module EdgarFunctions =
    open System
    open System.Text.Json
    open OpenAI.Chat
    open EdgarData

    let getCIK =
        let name = nameof(EdgarData.CIK.getCIK)
        let description = "Get the CIK information given a company name"
        let parameterDefinition = """
        {
            "type": "object",
            "properties": {
                "companyName": {
                    "type": "string",
                    "description": "The name of the company"
                }
            },
            "required": [ "companyName" ]
        }            
        """
        let parameters = BinaryData.FromString parameterDefinition
        //printfn "getCIK: name = %s" name
        ChatTool.CreateFunctionTool(name, description, parameters)

    let getFinancials =
        let name = nameof(EdgarData.CIK.getFinancialData)
        let description = "Get the financial data for a given CIK"
        let parameterDefinition = """
        {
            "type": "object",
            "properties": {
                "cik": {
                    "type": "string",
                    "description": "The CIK of the company"
                },
                "companyName": {
                    "type": "string",
                    "description": "The name of the company"
                }
            },
            "required": [ "cik" ]
        }            
        """
        let parameters = BinaryData.FromString parameterDefinition
        //printfn "getFD: name = %s" name
        ChatTool.CreateFunctionTool(name, description, parameters)


    let execFunction (toolCall: ChatToolCall) =
        match toolCall.FunctionName with
        | "getCIK" ->
            // extract the arguments
            let doc = JsonDocument.Parse(toolCall.FunctionArguments)
            let mutable cn = new JsonElement()

            if doc.RootElement.TryGetProperty("companyName", &cn) then
                let companyName = cn.GetString()
                let x = sprintf "CIK for %s is %s " companyName (Option.defaultValue "unknown" (CIK.getCIK companyName))
                printfn "%s" x
                x
            else
                ""
        | "getFinancialData" ->
            // extract the arguments
            let doc = JsonDocument.Parse(toolCall.FunctionArguments)
            let mutable edgarCID = new JsonElement()
            let mutable edgarCN = new JsonElement()
            if doc.RootElement.TryGetProperty("cik", &edgarCID) then
                let cik = edgarCID.GetString()
                if doc.RootElement.TryGetProperty("companyName", &edgarCN) then
                    let cn = edgarCN.GetString()
                    let fin = CIK.getFinancialData cik
                    let x = sprintf "Profit for %s= $%i" cn fin
                    printfn "%s" x
                    x
                else    
                    ""
            else
                ""

        | _ ->
            "unkown function"
