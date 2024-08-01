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
            "required": [ "cik", "companyName" ]
        }            
        """
        let parameters = BinaryData.FromString parameterDefinition
        ChatTool.CreateFunctionTool(name, description, parameters)

    let getSummary =
        let name = nameof(EdgarData.CIK.getSummary)
        let description = "Get the summary data for a given CIK, Company and Profit"
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
                },
                "profit": {
                    "type": "number",
                    "description": "The profit of the company"
                }
            },
            "required": [ "cik", "companyName", "profit" ]
        }            
        """
        let parameters = BinaryData.FromString parameterDefinition
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

        | "getSummary" ->
            let doc = JsonDocument.Parse(toolCall.FunctionArguments)
            let mutable data = new JsonElement()
            if doc.RootElement.TryGetProperty("cik", &data) then
                let cik = data.GetString()
                if doc.RootElement.TryGetProperty("companyName", &data) then
                    let cn = data.GetString()
                    if doc.RootElement.TryGetProperty("profit", &data) then
                        let profit = data.GetInt32()
                        let summary = CIK.getSummary cik cn profit
                        summary
                    else
                        ""
                else    
                    ""
            else
                ""

        | _ ->
            "unkown function"
