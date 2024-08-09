module Program =
    
    open System
    open System.IO
    open FSharp.Core
    open OpenAI.Chat
    open Milvus.Client
    open Wrapper.EdgarFunctions

    open EdgarData
    open Config
    open VectorDB




    

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

        let systemPromptText = 
            "You are a Finaincial AI Assistant. 
            When asked to fetch data for a company, you will first get the CIK information for that company and then use that to get financial data about the company.
            Then return the summary of the  CIK, Company Name and Profit using the summary tool. 
            Only use the data retrieved. 
            Please use the tools provided to accomplish your tasks."

        let model = "gpt-3.5-turbo"

        let options = new ChatCompletionOptions()
        let creds = new ClientModel.ApiKeyCredential(key)
        let client = new ChatClient(model, creds)
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

    let financialRAGQuery key gptModel embeddingModel query = task {

        let systemPromptPreamble = "You are a helpful AI assistant that answers users queries. Please use only the data provided below to asnwer the users question. If you cannot find the answer in the data below, please say I don't know."

        let! result = 
            Data.search key embeddingModel "CompanyData" query

        let possibleMatch =
            result
            |> Seq.fold(fun state (score,chunk) ->
                //printfn "*** Score: %f, Chunk: %s" score chunk
                state + Environment.NewLine + chunk
            ) ""
        let systemPrompt = systemPromptPreamble + Environment.NewLine + " Data: " + Environment.NewLine + possibleMatch

        let messages = [
            new SystemChatMessage(systemPrompt) :> ChatMessage;
            new UserChatMessage(query)
        ]
        let creds = new ClientModel.ApiKeyCredential(key)
        let client = new ChatClient(gptModel, creds)

        return processMessage client (new ChatCompletionOptions()) messages 

    }

    let map f g = async {
        let! x = g
        return f x
    }
    let [<EntryPoint>] main _ =
        Env.init
        let oaiKey = Environment.GetEnvironmentVariable "OPEN_AI_KEY"
        let embeddingModel = "text-embedding-3-small"
        
        let userMessage = "What is the financial data for Apple Inc. and Google Inc."
        (*
        processUserMessage oaiKey userMessage 
        |> showOutput
        *)

        //Data.createDatabase("financials") |> Async.RunSynchronously
        //let x = Data.getCollections() |> Async.RunSynchronously
        //printfn "%A" x

        (*
        let x = 
            Data.createFinancialsCollection()
            |> map(fun coll ->
                coll.Name
            ) |> Async.RunSynchronously

        printfn "created coll name:%s" x
        
        let y = Data.getCollections() |> Async.RunSynchronously
        printfn "checking: %A" y
        
        
        let data = File.ReadAllText "data.txt"
        let result = 
            Data.insertData oaiKey embeddingModel "CompanyData" 1L "0000320193" data
            |> Async.RunSynchronously

        printfn "%A" result
        *)

        let query = "Are there any legal proceedings and when was an antitrust lawsuit filed?"
        financialRAGQuery oaiKey "gpt-3.5-turbo" embeddingModel query
        |> Async.AwaitTask
        |> Async.RunSynchronously
        |> showOutput
        
        0
