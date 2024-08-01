module Program =
    
    open System
    open OpenAI.Chat
    open Wrapper.EdgarFunctions

    open EdgarData
    open Config

    let systemPromptText = 
        "You are a Finaincial AI Assistant. 
        When asked to fetch data for a company, you will first get the CIK information for that company and then use that to get financial data about the company.
        Then return the summary of the  CIK, Company Name and Profit using the summary tool. 
        Only use the data retrieved. 
        Please use the tools provided to accomplish your tasks."

    let model = "gpt-3.5-turbo"

    let userMessage = "What is the financial data for Apple Inc. and Google Inc."

    let rec processMessage (client:ChatClient) (options:ChatCompletionOptions) (messages:seq<ChatMessage>) =
        
        let completion = client.CompleteChat(messages, options)

        match completion.Value.FinishReason with
        | ChatFinishReason.Stop ->
            let newMsg = new AssistantChatMessage(completion)
            let newMessages = Seq.append messages (Seq.singleton newMsg)
            newMessages
        | ChatFinishReason.ToolCalls ->

            let assistantMessage = (new AssistantChatMessage(completion));
            let procMessages = Seq.append  messages (Seq.singleton (assistantMessage :> ChatMessage))
            let nextMessages =
                completion.Value.ToolCalls
                |> Seq.fold(fun allmessages tc -> 
                    let result = 
                        execFunction tc
                    let newMsg = new ToolChatMessage(tc.Id, result)
                    
                    Seq.append allmessages (Seq.singleton (newMsg :> ChatMessage))
                ) procMessages
            processMessage client options nextMessages
        | _ ->
            messages

    let processUserMessage key (message:string) =
        let options = new ChatCompletionOptions()
        let creds = new ClientModel.ApiKeyCredential(key)
        let client = new ChatClient("gpt-3.5-turbo", creds)
        options.Tools.Add(getCIK)
        options.Tools.Add(getFinancials)
        options.Tools.Add(getSummary)
        let systemPrompt = new SystemChatMessage(systemPromptText) :> ChatMessage
        let messages = 
            [
                systemPrompt;
                new UserChatMessage(message)
            ] |> List.toSeq
            |> Seq.cast
        processMessage client options messages

    let showOutput (messages:seq<ChatMessage>) =
        messages
        |> Seq.iter(fun res ->
            if (res.Content.Count > 0) then
                res.Content
                |> Seq.iter (fun c -> 
                    printfn "type:%s, %s" (res.GetType().FullName) (c.Text)
            )    
        )

    let [<EntryPoint>] main _ =
        Env.init
        let oaiKey = Environment.GetEnvironmentVariable "OPEN_AI_KEY"
        
        processUserMessage oaiKey userMessage 
        |> showOutput
        
        0
